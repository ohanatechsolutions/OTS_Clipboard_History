using System.Threading;
using System.Windows;

namespace OTS.Clipboard.History;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, @"Local\OTSClipboardHistorySingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            System.Windows.MessageBox.Show("OTS Clipboard History is already running. Look for its icon in the system tray.",
                "OTS Clipboard History", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstance?.ReleaseMutex();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
