using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XUnlock;

public sealed class CommunityApiClient : ICommunityApi
{
    private const string StateUrl =
        "https://sgp-api.buy.mi.com/bbs/api/global/user/bl-switch/state";

    private const string ApplyUrl =
        "https://sgp-api.buy.mi.com/bbs/api/global/apply/bl-auth";

    private readonly HttpClient _httpClient;
    private readonly Logger _logger;

    public CommunityApiClient(
        HttpClient? httpClient = null,
        Logger? logger = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
        _logger = logger ?? new Logger();
    }

    public async Task<ApiStateResult> GetStateAsync(
        SessionData session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var request = CreateRequest(
            HttpMethod.Get,
            StateUrl,
            session);

        var started = DateTimeOffset.UtcNow;

        try
        {
            using var response = await _httpClient
                .SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            var elapsed = DateTimeOffset.UtcNow - started;
            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Info(
                $"STATE HTTP={(int)response.StatusCode} RTT={elapsed.TotalMilliseconds:F0}ms");

            return ParseStateResponse(
                response.StatusCode,
                response.Headers.Date,
                body);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"STATE request failed: {ex.Message}");

            return new ApiStateResult
            {
                Code = -1,
                Message = ex.Message,
                HttpStatus = null,
                ServerDate = null
            };
        }
    }

    public async Task<ApiApplyResult> ApplyAsync(
        SessionData session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var request = CreateRequest(
            HttpMethod.Post,
            ApplyUrl,
            session);

        request.Content = new StringContent(
            "{\"is_retry\":true}",
            Encoding.UTF8,
            "application/json");

        var started = DateTimeOffset.UtcNow;

        try
        {
            using var response = await _httpClient
                .SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            var elapsed = DateTimeOffset.UtcNow - started;
            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Info(
                $"APPLY HTTP={(int)response.StatusCode} RTT={elapsed.TotalMilliseconds:F0}ms");

            return ParseApplyResponse(
                response.StatusCode,
                response.Headers.Date,
                body);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"APPLY request failed: {ex.Message}");

            return new ApiApplyResult
            {
                Code = -1,
                Message = ex.Message,
                HttpStatus = null,
                ServerDate = null
            };
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        SessionData session)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "okhttp/4.12.0");

        request.Headers.TryAddWithoutValidation(
            "Accept",
            "application/json");

        request.Headers.TryAddWithoutValidation(
            "versionCode",
            session.VersionCode ?? "500");

        request.Headers.TryAddWithoutValidation(
            "versionName",
            session.VersionName ?? "5.0.0");

        request.Headers.TryAddWithoutValidation(
            "deviceId",
            session.DeviceId ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(session.ServiceToken))
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                BuildCookie(session));
        }

        return request;
    }

    private static string BuildCookie(SessionData session)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(session.ServiceToken))
        {
            parts.Add(
                $"new_bbs_serviceToken={session.ServiceToken}");
        }

        if (!string.IsNullOrWhiteSpace(session.VersionCode))
        {
            parts.Add(
                $"versionCode={session.VersionCode}");
        }

        if (!string.IsNullOrWhiteSpace(session.VersionName))
        {
            parts.Add(
                $"versionName={session.VersionName}");
        }

        if (!string.IsNullOrWhiteSpace(session.DeviceId))
        {
            parts.Add(
                $"deviceId={session.DeviceId}");
        }

        return string.Join(";", parts) + ";";
    }

    private static ApiStateResult ParseStateResponse(
        HttpStatusCode statusCode,
        DateTimeOffset? serverDate,
        string body)
    {
        try
        {
            using var document =
                JsonDocument.Parse(body);

            var root = document.RootElement;

            var code = ReadInt(root, "code", -1);
            var message = ReadString(root, "message")
                          ?? ReadString(root, "msg")
                          ?? string.Empty;

            return new ApiStateResult
            {
                Code = code,
                Message = message,
                HttpStatus = (int)statusCode,
                ServerDate = serverDate,
                RawBody = body
            };
        }
        catch (JsonException)
        {
            return new ApiStateResult
            {
                Code = -1,
                Message = "Invalid JSON response.",
                HttpStatus = (int)statusCode,
                ServerDate = serverDate,
                RawBody = body
            };
        }
    }

    private static ApiApplyResult ParseApplyResponse(
        HttpStatusCode statusCode,
        DateTimeOffset? serverDate,
        string body)
    {
        try
        {
            using var document =
                JsonDocument.Parse(body);

            var root = document.RootElement;

            var code = ReadInt(root, "code", -1);
            var message = ReadString(root, "message")
                          ?? ReadString(root, "msg")
                          ?? string.Empty;

            return new ApiApplyResult
            {
                Code = code,
                Message = message,
                HttpStatus = (int)statusCode,
                ServerDate = serverDate,
                RawBody = body
            };
        }
        catch (JsonException)
        {
            return new ApiApplyResult
            {
                Code = -1,
                Message = "Invalid JSON response.",
                HttpStatus = (int)statusCode,
                ServerDate = serverDate,
                RawBody = body
            };
        }
    }

    private static int ReadInt(
        JsonElement root,
        string property,
        int fallback)
    {
        if (!root.TryGetProperty(property, out var value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return fallback;
    }

    private static string? ReadString(
        JsonElement root,
        string property)
    {
        if (!root.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }
}
