namespace XUnlock.Models;

public enum PermissionState
{
    Unknown,
    NotEligible,
    ApplyAvailable,
    Granted,
    TemporaryError,
    AuthenticationRequired,
    ApiChanged
}

public enum ApplyState
{
    Unknown,
    Successful,
    AccountError,
    QuotaReached,
    Failed,
    TryAgainMinute,
    TryAgainLater,
    AuthenticationRequired,
    ApiChanged,
    TemporaryError
}

public sealed record SessionSnapshot(bool HasServiceToken, string CookieHeader, int CookieCount, DateTimeOffset CapturedAt)
{
    public bool IsAuthenticated => HasServiceToken && !string.IsNullOrWhiteSpace(CookieHeader);
}

public sealed record ApiStateResult(
    PermissionState State,
    int? Code,
    string Message,
    TimeSpan RoundTrip,
    DateTimeOffset? ServerDate,
    string RawShape);

public sealed record ApplyResult(
    ApplyState State,
    int? Code,
    string Message,
    TimeSpan RoundTrip,
    DateTimeOffset? ServerDate,
    string RawShape);

public sealed record TimingSnapshot(double RttMs, double P90Ms, double OffsetMs, DateTimeOffset? ServerNow, string Phase);

public sealed record DiagnosticSnapshot(
    bool WebViewReady,
    bool Authenticated,
    PermissionState Permission,
    string Message,
    TimingSnapshot Timing,
    DateTimeOffset UpdatedAt);
