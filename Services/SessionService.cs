using Microsoft.Web.WebView2.Core;
using XUnlock.Models;

namespace XUnlock.Services;

public sealed class SessionService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Logger _log;

    public SessionService(Logger log) => _log = log;

    public async Task<SessionSnapshot> CaptureAsync(CoreWebView2 webView)
    {
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            var cookies = await webView.CookieManager.GetCookiesAsync(string.Empty);
            var parts = cookies
                .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => $"{c.Name}={c.Value}")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var hasToken = cookies.Any(c => c.Name.Equals("new_bbs_serviceToken", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Value));
            var header = string.Join(';', parts);
            _log.Info($"Session captured cookies={cookies.Count} token={hasToken}");
            return new SessionSnapshot(hasToken, header, cookies.Count, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _log.Error("Cookie capture failed", ex);
            return new SessionSnapshot(false, string.Empty, 0, DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }
}
