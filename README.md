# X-Unlock — clean rebuild

A clean Windows WPF implementation for the Xiaomi Community HyperOS bootloader-permission workflow.

## Important scope

This application uses the official Xiaomi Community web UI for authentication. CAPTCHA/2FA remains manual. The HTTP endpoints used by the monitor are undocumented/observed endpoints; they are **not claimed to be an official public Xiaomi API** and may change.

The project deliberately does not attempt to bypass CAPTCHA, 2FA, account restrictions, quotas, or Xiaomi's eligibility rules.

## Architecture

- WPF .NET 8 / win-x64.
- Explicit `App.OnStartup` creates `MainWindow` — no fragile `StartupUri` dependency.
- WebView2 is initialized with a dedicated persistent profile under `%LOCALAPPDATA%\\X-Unlock\\WebView2`.
- Session capture reads cookies from the WebView2 profile and looks specifically for `new_bbs_serviceToken`.
- API traffic is isolated in `CommunityApiClient`.
- API response parsing is isolated and unit-tested.
- Timing uses RTT samples and HTTP `Date` midpoint estimation; Windows system time is never modified.
- Monitor has a single-flight gate and a duplicate-submit guard.
- After a successful POST, the app performs a verification GET.
- 401/403 stop for authentication; 429/5xx are treated as temporary; unknown JSON/HTTP shapes stop as `ApiChanged` instead of guessing.
- Logs are written to `%LOCALAPPDATA%\\X-Unlock\\logs` with cookie/token values redacted.

## Build locally

Requires Windows, .NET 8 SDK, and Microsoft Edge WebView2 Runtime.

```powershell
dotnet restore XUnlock.sln
dotnet build XUnlock.sln -c Release --no-restore
dotnet test Tests/XUnlock.Tests.csproj -c Release --no-build
```

## Publish

```powershell
dotnet restore XUnlock.csproj -r win-x64
dotnet publish XUnlock.csproj -c Release -r win-x64 --self-contained true --no-restore -o publish
```

The GitHub workflow performs a clean restore/build/test/publish, verifies the executable and required files, and creates a ZIP artifact.

## Runtime troubleshooting

1. Start the EXE.
2. Check `%LOCALAPPDATA%\\X-Unlock\\logs\\startup.log` if the window does not appear.
3. Open Xiaomi Community inside the app and sign in manually.
4. Press **Проверить сессию**. The UI must show `Authorized`.
5. Only then press **START MONITOR**.
6. If the API returns an unknown response shape/code, the app stops with `ApiChanged` and records only non-secret diagnostics.
