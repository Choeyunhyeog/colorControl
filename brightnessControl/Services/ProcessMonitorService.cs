using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using brightnessControl.Models;

namespace brightnessControl.Services;

public sealed class ProcessMonitorService : IDisposable
{
    private readonly AppLogger _logger;
    private readonly object _gate = new();
    private DispatcherTimer? _timer;
    private Func<IEnumerable<GameProfile>>? _profileProvider;
    private GameProfile? _activeProfile;
    private bool _scanInProgress;
    private bool _reapplyActiveProfileOnNextScan;
    private bool _disposed;
    private DateTime _lastActiveProfileApplyUtc = DateTime.MinValue;

    private static readonly TimeSpan ActiveProfileReapplyInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ForegroundScanInterval = TimeSpan.FromMilliseconds(250);

    public ProcessMonitorService(AppLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler<GameProfile>? ProfileStarted;

    public event EventHandler<GameProfile>? ProfileStopped;

    public static bool IsProfileForeground(GameProfile profile)
    {
        var foregroundProcessName = GetForegroundProcessName();
        return !string.IsNullOrWhiteSpace(foregroundProcessName) &&
               string.Equals(GetProfileProcessName(profile), foregroundProcessName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProfileProcessRunning(GameProfile profile)
    {
        return IsRunning(profile);
    }

    public void Start(Func<IEnumerable<GameProfile>> profileProvider)
    {
        _profileProvider = profileProvider;
        _timer = new DispatcherTimer
        {
            Interval = ForegroundScanInterval
        };
        _timer.Tick += (_, _) => Scan();
        _timer.Start();
        Scan();
        _logger.Info($"Process monitor started. Foreground scan interval: {ForegroundScanInterval.TotalMilliseconds:N0} ms.");
    }

    public void RequestImmediateScan(bool reapplyActiveProfile = false)
    {
        if (_timer is null)
        {
            return;
        }

        if (reapplyActiveProfile)
        {
            lock (_gate)
            {
                _reapplyActiveProfileOnNextScan = true;
            }
        }

        if (_timer.Dispatcher.CheckAccess())
        {
            Scan();
        }
        else
        {
            _timer.Dispatcher.BeginInvoke(Scan);
        }
    }

    private void Scan()
    {
        lock (_gate)
        {
            if (_disposed || _scanInProgress)
            {
                return;
            }

            _scanInProgress = true;
        }

        try
        {
            var reapplyActiveProfile = false;
            lock (_gate)
            {
                reapplyActiveProfile = _reapplyActiveProfileOnNextScan;
                _reapplyActiveProfileOnNextScan = false;
            }

            var profiles = (_profileProvider?.Invoke() ?? [])
                .Where(profile => profile.Enabled)
                .Where(profile => !string.IsNullOrWhiteSpace(profile.ProcessName))
                .ToList();

            var foregroundProfile = FindForegroundProfile(profiles);

            if (_activeProfile is null && foregroundProfile is not null)
            {
                _activeProfile = foregroundProfile;
                _lastActiveProfileApplyUtc = DateTime.UtcNow;
                _logger.Info($"Detected foreground game process: {foregroundProfile.ProcessName}");
                ProfileStarted?.Invoke(this, foregroundProfile);
                return;
            }

            if (_activeProfile is not null && foregroundProfile is null)
            {
                var stoppedProfile = _activeProfile;
                _activeProfile = null;
                _lastActiveProfileApplyUtc = DateTime.MinValue;
                if (IsRunning(stoppedProfile))
                {
                    _logger.Info($"Game focus lost: {stoppedProfile.ProcessName}");
                }
                else
                {
                    _logger.Info($"Game process stopped: {stoppedProfile.ProcessName}");
                }

                ProfileStopped?.Invoke(this, stoppedProfile);
                return;
            }

            if (_activeProfile is not null &&
                foregroundProfile is not null &&
                !SameProfile(_activeProfile, foregroundProfile))
            {
                var stoppedProfile = _activeProfile;
                _activeProfile = foregroundProfile;
                _lastActiveProfileApplyUtc = DateTime.UtcNow;
                _logger.Info($"Switching active foreground profile from {stoppedProfile.GameName} to {foregroundProfile.GameName}.");
                ProfileStopped?.Invoke(this, stoppedProfile);
                ProfileStarted?.Invoke(this, foregroundProfile);
                return;
            }

            if (_activeProfile is not null &&
                foregroundProfile is not null &&
                SameProfile(_activeProfile, foregroundProfile))
            {
                var shouldReapply = reapplyActiveProfile ||
                                    DateTime.UtcNow - _lastActiveProfileApplyUtc >= ActiveProfileReapplyInterval;
                if (shouldReapply)
                {
                    _lastActiveProfileApplyUtc = DateTime.UtcNow;
                    _logger.Info($"Reapplying active foreground profile: {foregroundProfile.GameName}");
                    ProfileStarted?.Invoke(this, foregroundProfile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Process monitor scan failed.", ex);
        }
        finally
        {
            lock (_gate)
            {
                _scanInProgress = false;
            }
        }
    }

    private static bool IsRunning(GameProfile profile)
    {
        var processName = GetProfileProcessName(profile);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static GameProfile? FindForegroundProfile(IEnumerable<GameProfile> profiles)
    {
        var foregroundProcessName = GetForegroundProcessName();
        if (string.IsNullOrWhiteSpace(foregroundProcessName))
        {
            return null;
        }

        return profiles.FirstOrDefault(profile =>
            string.Equals(GetProfileProcessName(profile), foregroundProcessName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetProfileProcessName(GameProfile profile)
    {
        return Path.GetFileNameWithoutExtension(profile.ProcessName.Trim());
    }

    private static string? GetForegroundProcessName()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static bool SameProfile(GameProfile left, GameProfile right)
    {
        return string.Equals(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.GameName, right.GameName, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        _timer?.Stop();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
