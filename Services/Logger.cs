using System.Text;

namespace XUnlock.Services;

public sealed class Logger
{
    private readonly string _file;
    private readonly object _gate = new();

    public Logger()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X-Unlock", "logs");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, $"xunlock-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message} | {ex}");

    private void Write(string level, string message)
    {
        var safe = Redact(message);
        lock (_gate)
        {
            File.AppendAllText(_file, $"[{DateTimeOffset.Now:O}] [{level}] {safe}{Environment.NewLine}", Encoding.UTF8);
        }
    }

    private static string Redact(string value)
    {
        var result = value;
        foreach (var key in new[] { "new_bbs_serviceToken", "serviceToken", "Cookie", "Authorization" })
        {
            var idx = result.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                result = result[..idx] + key + "=<REDACTED>";
        }
        return result;
    }
}
