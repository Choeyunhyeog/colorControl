using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using brightnessControl.Models;
using Forms = System.Windows.Forms;

namespace brightnessControl;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly StringBuilder _logBuffer = new();

    public bool AllowClose { get; set; }

    public ObservableCollection<DisplayOption> DisplayTargetOptions { get; } = [];

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        DataContext = this;

        ProfilesGrid.ItemsSource = _services.ProfileService.Profiles;
        ProfilePathText.Text = $"profiles.json: {_services.ProfileService.FilePath}";
        RefreshDisplayTargetOptions();

        _services.Logger.MessageLogged += Logger_MessageLogged;
        _services.ProfileService.Profiles.CollectionChanged += Profiles_CollectionChanged;
    }

    private void RefreshDisplayTargetOptions()
    {
        DisplayTargetOptions.Clear();
        DisplayTargetOptions.Add(new DisplayOption
        {
            DeviceName = "Primary",
            DisplayName = "Primary"
        });

        foreach (var screen in Forms.Screen.AllScreens.OrderByDescending(screen => screen.Primary))
        {
            DisplayTargetOptions.Add(new DisplayOption
            {
                DeviceName = screen.DeviceName,
                DisplayName = screen.Primary
                    ? $"{screen.DeviceName} (Primary)"
                    : screen.DeviceName
            });
        }

        DisplayTargetPicker.SelectedValue = DisplayTargetOptions.Any(option =>
            string.Equals(option.DeviceName, _services.ProfileService.DisplayTarget.DeviceName, StringComparison.OrdinalIgnoreCase))
            ? _services.ProfileService.DisplayTarget.DeviceName
            : "Primary";
    }

    private void DisplayTargetPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DisplayTargetPicker.SelectedValue is not string deviceName)
        {
            return;
        }

        _services.ProfileService.DisplayTarget.DeviceName = deviceName;
        _services.ProfileService.DisplayTarget.PrimaryOnly =
            string.Equals(deviceName, "Primary", StringComparison.OrdinalIgnoreCase);
        _services.Logger.Info($"Global display target selected: {deviceName}");
    }

    private void Logger_MessageLogged(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            _logBuffer.AppendLine(message);
            LogTextBox.Text = _logBuffer.ToString();
            LogTextBox.ScrollToEnd();
        });
    }

    private void Profiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _services.ProcessMonitorService.RequestImmediateScan();
    }

    private async void ScanGamesButton_Click(object sender, RoutedEventArgs e)
    {
        ScanGamesButton.IsEnabled = false;
        AddSelectedGameButton.IsEnabled = false;
        GamePicker.IsEnabled = false;

        try
        {
            _services.Logger.Info("Scanning installed games.");
            var games = await Task.Run(_services.GameDiscoveryService.DiscoverInstalledGames);

            GamePicker.ItemsSource = games;
            GamePicker.SelectedIndex = games.Count > 0 ? 0 : -1;
            GamePicker.IsEnabled = games.Count > 0;
            AddSelectedGameButton.IsEnabled = games.Count > 0;

            if (games.Count == 0)
            {
                _services.Logger.Warning("No installed game candidates were found.");
            }
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Game scan failed.", ex);
        }
        finally
        {
            ScanGamesButton.IsEnabled = true;
        }
    }

    private void AddSelectedGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (GamePicker.SelectedItem is not InstalledGame game)
        {
            return;
        }

        var profile = GameProfile.CreateDefault();
        profile.GameName = game.Name;
        profile.ProcessName = game.ProcessName;

        _services.ProfileService.Profiles.Add(profile);
        ProfilesGrid.SelectedItem = profile;
        ProfilesGrid.ScrollIntoView(profile);
        _services.ProcessMonitorService.RequestImmediateScan();
        _services.Logger.Info($"Added game profile: {game.Name} ({game.ProcessName})");
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is not GameProfile profile)
        {
            return;
        }

        _services.ProfileService.Profiles.Remove(profile);
        _services.ProcessMonitorService.RequestImmediateScan();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ProfilesGrid.CommitEdit();
        ProfilesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        _services.ProfileService.Save();
        _services.ProcessMonitorService.RequestImmediateScan(reapplyActiveProfile: true);
    }

    private void ApplySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        ProfilesGrid.CommitEdit();
        ProfilesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        if (ProfilesGrid.SelectedItem is not GameProfile profile)
        {
            _services.Logger.Warning("No profile is selected for manual apply.");
            return;
        }

        _services.Logger.Info($"Manual apply requested: {profile.GameName}");
        if (!Services.ProcessMonitorService.IsProfileProcessRunning(profile))
        {
            _services.Logger.Warning(
                $"Manual apply skipped because {profile.ProcessName} is not running. Restoring original display settings.");
            _services.RecoveryService.RestoreOriginalSettings("Manual apply skipped for non-running game");
            return;
        }

        if (!Services.ProcessMonitorService.IsProfileForeground(profile))
        {
            _services.Logger.Info(
                $"{profile.ProcessName} is running but not foreground because the control window is active. Applying manually; automatic focus restore remains active.");
        }

        _services.RecoveryService.ApplyProfile(profile);
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        _services.RecoveryService.RestoreOriginalSettings("Manual restore button", resetNeutralIfNoBackup: true);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        var closeChoiceWindow = new CloseChoiceWindow
        {
            Owner = this
        };
        closeChoiceWindow.ShowDialog();

        if (closeChoiceWindow.Choice == CloseChoice.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            _services.Logger.Info("Window hidden. The tray app is still running.");
            return;
        }

        if (closeChoiceWindow.Choice == CloseChoice.Exit)
        {
            AllowClose = true;
            _services.Logger.Info("Exit selected from window close prompt.");
            System.Windows.Application.Current.Shutdown();
            return;
        }

        e.Cancel = true;
        _services.Logger.Info("Window close canceled.");
    }
}
