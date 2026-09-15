using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace XUnlock;

public partial class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X-Unlock", "logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterGlobalHandlers();
        SafeLog($"START pid={Environment.ProcessId} os={Environment.OSVersion} runtime={Environment.Version} x64={Environment.Is64BitProcess}");
        try
        {
            SafeLog($"WebView2 available={CoreWebView2Environment.GetAvailableBrowserVersionString()}");
            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            SafeLog("MainWindow shown");
        }
        catch (Exception ex)
        {
            SafeLog("FATAL startup: " + ex);
            MessageBox.Show($"X-Unlock не запустился.\n\n{ex.Message}\n\nЛог:\n{Path.Combine(LogDirectory, "startup.log")}", "X-Unlock", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void RegisterGlobalHandlers()
    {
        DispatcherUnhandledException += (_, e) => { SafeLog("DispatcherUnhandledException: " + e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => SafeLog("AppDomain.UnhandledException: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { SafeLog("UnobservedTaskException: " + e.Exception); e.SetObserved(); };
    }

    public static void SafeLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(Path.Combine(LogDirectory, "startup.log"), $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
