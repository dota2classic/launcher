using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Api;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public sealed class SteamAuthApi : ISteamAuthApi
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://api.dotaclassic.ru/"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    public async Task<string?> RefreshTokenAsync(string currentToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentToken))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/auth/steam/refresh_token");
            request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", currentToken);

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                AppLog.Error($"Token refresh failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return null;

            string? token;
            try { token = JsonSerializer.Deserialize<string>(body); }
            catch { token = body.Trim().Trim('"'); }

            AppLog.Info($"Token refreshed successfully.");
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (Exception ex)
        {
            AppLog.Error("Token refresh failed", ex);
            return null;
        }
    }

    public async Task<string?> ExchangeSteamSessionTicketAsync(string ticket, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket))
            return null;

        try
        {
            var url = "v1/auth/steam/steam_session_ticket?ticket=" + Uri.EscapeDataString(ticket);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                AppLog.Error($"Steam ticket exchange failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(body))
                return null;

            string? token;
            try
            {
                token = JsonSerializer.Deserialize<string>(body);
            }
            catch
            {
                token = body.Trim().Trim('"');
            }

            AppLog.Info($"Steam ticket exchanged. Token received: {!string.IsNullOrWhiteSpace(token)}");
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (Exception ex)
        {
            AppLog.Error("Steam ticket exchange failed", ex);
            return null;
        }
    }
}
