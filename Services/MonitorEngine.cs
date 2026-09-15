using XUnlock.Models;

namespace XUnlock.Services;

public sealed class MonitorEngine : IDisposable
{
    private readonly CommunityApiClient _api;
    private readonly TimingEngine _timing;
    private readonly Logger _log;
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _applySent;

    public event Action<string>? StatusChanged;
    public event Action<DiagnosticSnapshot>? SnapshotChanged;

    public MonitorEngine(CommunityApiClient api, TimingEngine timing, Logger log)
    {
        _api = api; _timing = timing; _log = log;
    }

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _applySent = false;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
        StatusChanged?.Invoke("MONITORING");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _running = false;
        StatusChanged?.Invoke("STOPPED");
    }

    public void ResetApplyGuard() => _applySent = false;

    private async Task LoopAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await _runGate.WaitAsync(0, ct))
                {
                    try
                    {
                        var result = await _api.GetStateAsync(ct);
                        _timing.AddSample(result.RoundTrip, result.ServerDate);
                        var phase = result.State == PermissionState.ApplyAvailable ? "READY" : "NORMAL";
                        StatusChanged?.Invoke(result.Message);
                        SnapshotChanged?.Invoke(new(false, true, result.State, result.Message, _timing.Snapshot(phase), DateTimeOffset.Now));

                        if (result.State == PermissionState.ApplyAvailable && !_applySent)
                        {
                            _applySent = true;
                            StatusChanged?.Invoke("SUBMITTING");
                            var apply = await _api.ApplyAsync(ct);
                            _timing.AddSample(apply.RoundTrip, apply.ServerDate);
                            StatusChanged?.Invoke(apply.Message);
                            SnapshotChanged?.Invoke(new(false, true, apply.State == ApplyState.Successful ? PermissionState.Granted : PermissionState.Unknown, apply.Message, _timing.Snapshot("VERIFY"), DateTimeOffset.Now));
                            if (apply.State == ApplyState.Successful)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                                var verify = await _api.GetStateAsync(ct);
                                _timing.AddSample(verify.RoundTrip, verify.ServerDate);
                                StatusChanged?.Invoke("VERIFY: " + verify.Message);
                                if (verify.State != PermissionState.Granted)
                                    _log.Warn("Apply returned success but state verification did not show Granted.");
                                break;
                            }
                            if (apply.State is ApplyState.AuthenticationRequired or ApplyState.ApiChanged or ApplyState.QuotaReached)
                                break;
                            _applySent = false;
                        }
                    }
                    finally { _runGate.Release(); }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.Error("Monitor loop error", ex); StatusChanged?.Invoke("ERROR: " + ex.Message); }
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
        }
        _running = false;
        StatusChanged?.Invoke("STOPPED");
    }

    public void Dispose() { Stop(); _runGate.Dispose(); }
}
