using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public static class LauncherHttp
{
    private const string ProductName = "DotaLegacyLauncher";
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    private static readonly SocketsHttpHandler Handler = new()
    {
        PooledConnectionLifetime = ConnectionLifetime,
        AutomaticDecompression = DecompressionMethods.All,
    };

    private static readonly ProductInfoHeaderValue UserAgent = new(ProductName, AppVersion.Current);

    public static HttpClient Create(TimeSpan? timeout = null, Uri? baseAddress = null)
    {
        var client = new HttpClient(Handler, disposeHandler: false) { BaseAddress = baseAddress };
        if (timeout is { } t)
            client.Timeout = t;
        client.DefaultRequestHeaders.UserAgent.Add(UserAgent);
        return client;
    }
}
