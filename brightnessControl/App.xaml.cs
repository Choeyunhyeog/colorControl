using System.Windows;
using brightnessControl.Services;

namespace brightnessControl;

public partial class App : System.Windows.Application
{
    private AppServices? _services;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            _services?.Logger.Error("Unhandled UI exception. Attempting recovery.", args.Exception);
            _services?.RecoveryService.RestoreOriginalSettings("Unhandled UI exception");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _services?.Logger.Error("Unhandled process exception. Attempting recovery.", args.ExceptionObject as Exception);
            _services?.RecoveryService.RestoreOriginalSettings("Unhandled process exception");
        };

        _services = AppServices.Create();
        _services.RecoveryService.RestorePersistedBackupsIfAny();
        _services.ProcessMonitorService.ProfileStarted += (_, profile) =>
            _services.RecoveryService.ApplyProfile(profile);
        _services.ProcessMonitorService.ProfileStopped += (_, profile) =>
            _services.RecoveryService.RestoreOriginalSettings($"{profile.GameName} deactivated");
        _services.ProcessMonitorService.Start(() => _services.ProfileService.Profiles);

        _mainWindow = new MainWindow(_services);
        _trayService = new TrayService(_mainWindow, _services.Logger, _services.RecoveryService, ExitApplication);
        _trayService.Initialize();

        _mainWindow.Show();
        _services.Logger.Info("Application started.");
        _services.Logger.Info(_services.NvidiaColorService.IsAvailable
            ? "NVAPI scanout color path is active."
            : "NVAPI scanout color path is unavailable; Windows gamma ramp fallback is active.");
    }

    private void ExitApplication()
    {
        _services?.Logger.Info("Exit requested.");
        _services?.RecoveryService.RestoreOriginalSettings("Application exit");
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.RecoveryService.RestoreOriginalSettings("Application shutdown");
        _services?.ProcessMonitorService.Dispose();
        _trayService?.Dispose();
        base.OnExit(e);
    }
}
