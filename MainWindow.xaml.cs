using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using XUnlock.Models;
using XUnlock.Services;

namespace XUnlock;

public partial class MainWindow : Window
{
    private const string XiaomiCommunityUrl = "https://new.c.mi.com/global/";
    private readonly Logger _log = new();
    private readonly SessionService _session;
    private readonly CommunityApiClient _api;
    private readonly TimingEngine _timing = new();
    private readonly MonitorEngine _monitor;
    private DispatcherTimer? _sessionTimer;
    private bool _webViewReady;

    public MainWindow()
    {
        InitializeComponent();
        _session = new SessionService(_log);
        _api = new CommunityApiClient(_log);
        _monitor = new MonitorEngine(_api, _timing, _log);
        _monitor.StatusChanged += message => Dispatcher.Invoke(() => StatusText.Text = message);
        _monitor.SnapshotChanged += snapshot => Dispatcher.Invoke(() => ApplySnapshot(snapshot));
        Loaded += MainWindow_Loaded;
        Closing += (_, _) => _monitor.Dispose();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X-Unlock", "WebView2"));
            await Browser.EnsureCoreWebView2Async(env);
            _webViewReady = true;
            AccountText.Text = "WebView2 ready";
            _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _sessionTimer.Tick += async (_, _) => await RefreshSessionAsync(false);
            _sessionTimer.Start();
            Browser.CoreWebView2.Navigate(XiaomiCommunityUrl);
            _log.Info("WebView2 initialized");
        }
        catch (Exception ex)
        {
            _log.Error("WebView2 initialization failed", ex);
            AccountText.Text = "WebView2 ERROR";
            StatusText.Text = ex.Message;
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady) return;
        Browser.CoreWebView2.Navigate(XiaomiCommunityUrl);
    }

    private async void SessionButton_Click(object sender, RoutedEventArgs e) => await RefreshSessionAsync(true);

    private async Task RefreshSessionAsync(bool verbose)
    {
        if (!_webViewReady || Browser.CoreWebView2 is null) return;
        var snapshot = await _session.CaptureAsync(Browser.CoreWebView2);
        _api.SetSession(snapshot);
        AccountText.Text = snapshot.IsAuthenticated ? "Authorized" : "Not authorized";
        if (verbose) StatusText.Text = snapshot.IsAuthenticated ? "Сессия получена. Можно запускать монитор." : "В cookie нет new_bbs_serviceToken. Войдите в Xiaomi Community.";
        AppendDiagnostic($"SESSION auth={snapshot.IsAuthenticated} cookies={snapshot.CookieCount}");
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady)
        {
            StatusText.Text = "WebView2 ещё не готов";
            return;
        }
        if (AccountText.Text != "Authorized")
        {
            StatusText.Text = "Сначала войдите в Xiaomi Community и дождитесь Authorized";
            return;
        }
        _monitor.Start();
        RunState.Text = "RUNNING";
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _monitor.Stop();
        RunState.Text = "STOPPED";
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) StatusText.Text = $"Навигация: {e.WebErrorStatus}";
    }

    private void ApplySnapshot(DiagnosticSnapshot s)
    {
        PermissionText.Text = s.Permission.ToString();
        TimingText.Text = $"RTT {s.Timing.RttMs:F0} ms · p90 {s.Timing.P90Ms:F0} · offset {s.Timing.OffsetMs:F0} ms";
        StatusText.Text = s.Message;
        AppendDiagnostic($"STATE={s.Permission} {s.Message} | {TimingText.Text}");
        if (s.Permission is PermissionState.Granted or PermissionState.AuthenticationRequired or PermissionState.ApiChanged)
        {
            _monitor.Stop();
            RunState.Text = "STOPPED";
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private void AppendDiagnostic(string line)
    {
        Diagnostics.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        Diagnostics.ScrollToEnd();
    }
}
