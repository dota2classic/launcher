using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public static class HardwareInfoService
{
    public static void LogAll()
    {
        try
        {
            var cpuId = QueryFirst("Win32_Processor", "ProcessorId") ?? "";
            var cpuName = QueryFirst("Win32_Processor", "Name") ?? "Unknown";
            var cpuCores = QueryFirst("Win32_Processor", "NumberOfCores") ?? "?";
            var cpuThreads = QueryFirst("Win32_Processor", "NumberOfLogicalProcessors") ?? "?";
            var cpuMhz = QueryFirst("Win32_Processor", "MaxClockSpeed") ?? "?";

            var moboSerial = QueryFirst("Win32_BaseBoard", "SerialNumber") ?? "";
            var moboProduct = QueryFirst("Win32_BaseBoard", "Product") ?? "Unknown";
            var moboMfr = QueryFirst("Win32_BaseBoard", "Manufacturer") ?? "";

            var diskModels = QueryAll("Win32_DiskDrive", "Model");
            var diskSerials = QueryAll("Win32_DiskDrive", "SerialNumber")
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OrderBy(s => s)
                .ToList();

            var macs = QueryAllWhere("Win32_NetworkAdapterConfiguration", "MACAddress", "IPEnabled", "True")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OrderBy(s => s)
                .ToList();

            var ramMb = QueryAll("Win32_PhysicalMemory", "Capacity")
                .Select(s => long.TryParse(s, out var v) ? v : 0L)
                .Sum() / (1024 * 1024);

            var gpus = QueryAllFields("Win32_VideoController", ["Name", "DriverVersion", "AdapterRAM"]);

            var osCaption = QueryFirst("Win32_OperatingSystem", "Caption") ?? "Unknown";
            var osBuild = QueryFirst("Win32_OperatingSystem", "BuildNumber") ?? "?";
            var osArch = QueryFirst("Win32_OperatingSystem", "OSArchitecture") ?? "?";

            var hwid = ComputeHwid(cpuId, moboSerial, diskSerials, macs);

            AppLog.Info($"[HW] HWID: {hwid}");
            AppLog.Info($"[HW] OS: {osCaption.Trim()} (Build {osBuild}), {osArch}");
            AppLog.Info($"[HW] CPU: {cpuName.Trim()} | {cpuCores}C/{cpuThreads}T | {cpuMhz} MHz | ID: {cpuId.Trim()}");

            foreach (var gpu in gpus)
            {
                var name = gpu.GetValueOrDefault("Name", "Unknown").Trim();
                var driver = gpu.GetValueOrDefault("DriverVersion", "?").Trim();
                var vramBytes = long.TryParse(gpu.GetValueOrDefault("AdapterRAM", "0"), out var v) ? v : 0L;
                var vramMb = vramBytes / (1024 * 1024);
                AppLog.Info($"[HW] GPU: {name} | Driver: {driver} | VRAM: {vramMb} MB");
            }

            AppLog.Info($"[HW] RAM: {ramMb} MB total");

            foreach (var (model, i) in diskModels.Select((m, i) => (m, i)))
                AppLog.Info($"[HW] Disk: {model.Trim()}");

            var moboLabel = string.IsNullOrWhiteSpace(moboMfr) ? moboProduct : $"{moboMfr} {moboProduct}";
            AppLog.Info($"[HW] Board: {moboLabel.Trim()} (S/N: {moboSerial.Trim()})");
        }
        catch (Exception ex)
        {
            AppLog.Error("[HW] Failed to collect hardware info", ex);
        }
    }

    private static string ComputeHwid(
        string cpuId, string moboSerial,
        IEnumerable<string> diskSerials, IEnumerable<string> macs)
    {
        var raw = string.Join("|",
            cpuId.Trim(),
            moboSerial.Trim(),
            string.Join(",", diskSerials),
            string.Join(",", macs));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? QueryFirst(string wmiClass, string property)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
        foreach (ManagementObject obj in searcher.Get())
            return obj[property]?.ToString();
        return null;
    }

    private static List<string> QueryAll(string wmiClass, string property)
    {
        var results = new List<string>();
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
        foreach (ManagementObject obj in searcher.Get())
            results.Add(obj[property]?.ToString() ?? "");
        return results;
    }

    private static List<string> QueryAllWhere(string wmiClass, string property, string filterProp, string filterVal)
    {
        var results = new List<string>();
        using var searcher = new ManagementObjectSearcher(
            $"SELECT {property} FROM {wmiClass} WHERE {filterProp} = '{filterVal}'");
        foreach (ManagementObject obj in searcher.Get())
            results.Add(obj[property]?.ToString() ?? "");
        return results;
    }

    private static List<Dictionary<string, string>> QueryAllFields(string wmiClass, string[] properties)
    {
        var results = new List<Dictionary<string, string>>();
        var select = string.Join(", ", properties);
        using var searcher = new ManagementObjectSearcher($"SELECT {select} FROM {wmiClass}");
        foreach (ManagementObject obj in searcher.Get())
        {
            var row = new Dictionary<string, string>();
            foreach (var prop in properties)
                row[prop] = obj[prop]?.ToString() ?? "";
            results.Add(row);
        }
        return results;
    }
}
