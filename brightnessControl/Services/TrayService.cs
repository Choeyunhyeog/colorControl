using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace brightnessControl.Services;

public sealed class TrayService : IDisposable
{
    private readonly Window _mainWindow;
    private readonly AppLogger _logger;
    private readonly RecoveryService _recoveryService;
    private readonly Action _exitAction;
    private Forms.NotifyIcon? _notifyIcon;

    public TrayService(
        Window mainWindow,
        AppLogger logger,
        RecoveryService recoveryService,
        Action exitAction)
    {
        _mainWindow = mainWindow;
        _logger = logger;
        _recoveryService = recoveryService;
        _exitAction = exitAction;
    }

    public void Initialize()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Restore Original Settings", null, (_, _) =>
            _recoveryService.RestoreOriginalSettings("Tray menu"));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _exitAction());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Brightness Control",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _logger.Info("Tray icon initialized.");
    }

    private void ShowMainWindow()
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
        });
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }
}
