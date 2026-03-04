using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

/// <summary>
/// Sends telemetry to Grafana Alloy (faro.receiver) using the Faro wire protocol.
/// Matches the same logsink endpoint used by the frontend.
/// </summary>
public static class FaroTelemetryService
{
    private const string CollectorUrl = "https://logsink.dotaclassic.ru/collect";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string SessionId = Guid.NewGuid().ToString("N");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string _appVersion = "0.0.0";
    private static string? _steamId;
    private static Dictionary<string, string> _hwAttributes = [];
    private static string _osName = "Windows";

    private static readonly ConcurrentQueue<FaroLog> LogQueue = new();
    private static readonly ConcurrentQueue<FaroEvent> EventQueue = new();
    private static readonly ConcurrentQueue<FaroEx> ExceptionQueue = new();

    private static Timer? _timer;

    public static void Init(string appVersion, HardwareSnapshot? hw = null)
    {
        _appVersion = appVersion;
        if (hw != null) ApplyHardware(hw);
        _timer = new Timer(FlushCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        TrackEvent("launcher_opened");
    }

    public static void SetUser(string steamId) => _steamId = steamId;

    public static void TrackEvent(string name, Dictionary<string, string>? attributes = null)
    {
        EventQueue.Enqueue(new FaroEvent(name, "d2c", attributes ?? [], Now()));
        if (EventQueue.Count >= 20) _ = FlushAsync();
    }

    public static void TrackLog(string level, string message)
    {
        LogQueue.Enqueue(new FaroLog(message, level, Now()));
        if (LogQueue.Count >= 50) _ = FlushAsync();
    }

    public static void TrackException(Exception ex)
    {
        ExceptionQueue.Enqueue(BuildFaroException(ex));
        _ = FlushAsync();
    }

    public static async Task ShutdownAsync()
    {
        _timer?.Dispose();
        _timer = null;
        await FlushAsync();
    }

    public static async Task FlushAsync()
    {
        var logs = Drain(LogQueue);
        var events = Drain(EventQueue);
        var exceptions = Drain(ExceptionQueue);

        if (logs.Count == 0 && events.Count == 0 && exceptions.Count == 0)
            return;

        var payload = new
        {
            meta = BuildMeta(),
            logs,
            exceptions,
            measurements = Array.Empty<object>(),
            events,
        };

        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await Http.PostAsync(CollectorUrl, content);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Faro] Flush failed: {ex.Message}");
        }
    }

    private static void ApplyHardware(HardwareSnapshot hw)
    {
        _osName = hw.OsCaption;
        var attrs = new Dictionary<string, string>
        {
            ["hwid"] = hw.Hwid,
            ["cpu"] = hw.CpuName,
            ["cpu_cores"] = hw.CpuCores,
            ["cpu_threads"] = hw.CpuThreads,
            ["ram_mb"] = hw.RamMb.ToString(),
            ["os"] = hw.OsCaption,
            ["os_build"] = hw.OsBuild,
            ["mobo"] = hw.Mobo,
        };
        for (var i = 0; i < hw.Gpus.Count; i++)
            attrs[$"gpu{i}"] = hw.Gpus[i];
        _hwAttributes = attrs;
    }

    private static object BuildMeta() => new
    {
        sdk = new { name = "d2c-faro-dotnet", version = "1.0.0", integrations = Array.Empty<object>() },
        app = new { name = "dotaclassic", version = _appVersion, environment = "production" },
        session = new { id = SessionId, attributes = _hwAttributes },
        user = (object?)(_steamId != null ? new { id = _steamId } : null),
        browser = new { name = "dotaclassic", version = _appVersion, os = _osName, mobile = false },
        page = new { url = "d2c://launcher" },
    };

    private static FaroEx BuildFaroException(Exception ex)
    {
        var frames = (ex.StackTrace ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.StartsWith("at "))
            .Select(l => new FaroFrame(l[3..]))
            .ToList();

        return new FaroEx(
            ex.GetType().FullName ?? ex.GetType().Name,
            ex.Message,
            new FaroStacktrace(frames),
            Now());
    }

    private static List<T> Drain<T>(ConcurrentQueue<T> queue)
    {
        var list = new List<T>(queue.Count);
        while (queue.TryDequeue(out var item)) list.Add(item);
        return list;
    }

    private static void FlushCallback(object? _) => _ = FlushAsync();

    private static string Now() => DateTime.UtcNow.ToString("O");

    private record FaroLog(string message, string level, string timestamp);
    private record FaroEvent(string name, string domain, Dictionary<string, string> attributes, string timestamp);
    private record FaroEx(string type, string value, FaroStacktrace stacktrace, string timestamp);
    private record FaroStacktrace(List<FaroFrame> frames);
    private record FaroFrame(string function);
}
