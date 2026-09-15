using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XUnlock.Models;

namespace XUnlock.Services;

public sealed class CommunityApiClient : IDisposable
{
    private const string StateEndpoint =
        "https://sgp-api.buy.mi.com/bbs/api/global/user/bl-switch/state";

    private const string ApplyEndpoint =
        "https://sgp-api.buy.mi.com/bbs/api/global/apply/bl-auth";

    private readonly HttpClient _http;
    private readonly Logger _log;
    private readonly object _sync = new();

    private string _cookie = string.Empty;
    private bool _disposed;

    public CommunityApiClient(
        Logger log,
        HttpMessageHandler? handler = null)
    {
        _log = log;

        handler ??= new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(8),
            UseCookies = false
        };

        _http = new HttpClient(
            handler,
            disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "okhttp/4.12.0");

        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetSession(SessionSnapshot session)
    {
        lock (_sync)
        {
            _cookie = session.CookieHeader;
        }

        _log.Info(
            $"API session set authenticated={session.IsAuthenticated} " +
            $"cookies={session.CookieCount}");
    }

    public async Task<ApiStateResult> GetStateAsync(
        CancellationToken ct)
    {
        EnsureNotDisposed();

        using var request = CreateRequest(
            HttpMethod.Get,
            StateEndpoint);

        return await SendStateAsync(request, ct);
    }

    public async Task<ApplyResult> ApplyAsync(
        CancellationToken ct)
    {
        EnsureNotDisposed();

        using var request = CreateRequest(
            HttpMethod.Post,
            ApplyEndpoint);

        request.Content = new StringContent(
            "{\"is_retry\":true}",
            Encoding.UTF8,
            "application/json");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(ct);
            var date = response.Headers.Date;

            _log.Info(
                $"APPLY http={(int)response.StatusCode} " +
                $"rtt={sw.ElapsedMilliseconds}ms " +
                $"bytes={body.Length}");

            if (response.StatusCode is
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
            {
                return new ApplyResult(
                    ApplyState.AuthenticationRequired,
                    (int)response.StatusCode,
                    "Сессия Xiaomi недействительна",
                    sw.Elapsed,
                    date,
                    "http-auth");
            }

            if ((int)response.StatusCode == 429 ||
                (int)response.StatusCode >= 500)
            {
                return new ApplyResult(
                    ApplyState.TemporaryError,
                    (int)response.StatusCode,
                    "Временная HTTP ошибка",
                    sw.Elapsed,
                    date,
                    "http-temporary");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ApplyResult(
                    ApplyState.ApiChanged,
                    (int)response.StatusCode,
                    $"HTTP {(int)response.StatusCode}",
                    sw.Elapsed,
                    date,
                    "http-error");
            }

            return ApiResponseParser.ParseApply(
                body,
                sw.Elapsed,
                date);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();

            _log.Error(
                "APPLY transport failure",
                ex);

            return new ApplyResult(
                ApplyState.TemporaryError,
                null,
                ex.Message,
                sw.Elapsed,
                null,
                "transport-error");
        }
    }

    private async Task<ApiStateResult> SendStateAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(ct);
            var date = response.Headers.Date;

            _log.Info(
                $"STATE http={(int)response.StatusCode} " +
                $"rtt={sw.ElapsedMilliseconds}ms " +
                $"bytes={body.Length}");

            if (response.StatusCode is
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
            {
                return new ApiStateResult(
                    PermissionState.AuthenticationRequired,
                    (int)response.StatusCode,
                    "Сессия Xiaomi недействительна",
                    sw.Elapsed,
                    date,
                    "http-auth");
            }

            if ((int)response.StatusCode == 429 ||
                (int)response.StatusCode >= 500)
            {
                return new ApiStateResult(
                    PermissionState.TemporaryError,
                    (int)response.StatusCode,
                    "Временная HTTP ошибка",
                    sw.Elapsed,
                    date,
                    "http-temporary");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ApiStateResult(
                    PermissionState.ApiChanged,
                    (int)response.StatusCode,
                    $"HTTP {(int)response.StatusCode}",
                    sw.Elapsed,
                    date,
                    "http-error");
            }

            return ApiResponseParser.ParseState(
                body,
                sw.Elapsed,
                date);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();

            _log.Error(
                "STATE transport failure",
                ex);

            return new ApiStateResult(
                PermissionState.TemporaryError,
                null,
                ex.Message,
                sw.Elapsed,
                null,
                "transport-error");
        }
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url)
    {
        var request = new HttpRequestMessage(
            method,
            url);

        string cookie;

        lock (_sync)
        {
            cookie = _cookie;
        }

        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                cookie);
        }

        request.Headers.TryAddWithoutValidation(
            "versionCode",
            "50001");

        request.Headers.TryAddWithoutValidation(
            "versionName",
            "5.0.1");

        request.Headers.TryAddWithoutValidation(
            "deviceId",
            DeviceIdProvider.GetStableId());

        return request;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(CommunityApiClient));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _http.Dispose();
    }
}

internal static class DeviceIdProvider
{
    private static readonly string Id = Create();

    public static string GetStableId()
    {
        return Id;
    }

    private static string Create()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "X-Unlock");

        Directory.CreateDirectory(directory);

        var file = Path.Combine(
            directory,
            "device.id");

        if (File.Exists(file))
        {
            var existing = File.ReadAllText(file).Trim();

            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        var id = Guid.NewGuid().ToString("N");

        File.WriteAllText(
            file,
            id);

        return id;
    }
}
