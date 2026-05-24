using brightnessControl.Models;

namespace brightnessControl.Services;

public sealed class RecoveryService
{
    private readonly AppLogger _logger;
    private readonly GammaService _gammaService;
    private readonly INvidiaColorService _nvidiaColorService;
    private readonly Func<DisplayTarget> _displayTargetProvider;
    private readonly object _gate = new();
    private bool _profileMayBeApplied;

    public RecoveryService(
        AppLogger logger,
        GammaService gammaService,
        INvidiaColorService nvidiaColorService,
        Func<DisplayTarget> displayTargetProvider)
    {
        _logger = logger;
        _gammaService = gammaService;
        _nvidiaColorService = nvidiaColorService;
        _displayTargetProvider = displayTargetProvider;
    }

    public void ApplyProfile(GameProfile profile)
    {
        lock (_gate)
        {
            try
            {
                _logger.Info($"Applying profile: {profile.GameName}");
                var displayTarget = _displayTargetProvider();
                _logger.Info($"Using display target: {displayTarget.DeviceName}");
                _logger.Info(
                    $"Requested color values: brightness {profile.ColorProfile.BrightnessPercent}%, contrast {profile.ColorProfile.ContrastPercent}%, gamma {profile.ColorProfile.Gamma:N2}, vibrance {profile.ColorProfile.DigitalVibrancePercent ?? 50}%, hue {profile.ColorProfile.HueDegrees}.");
                _profileMayBeApplied = true;
                var nvidiaApplied = TryApplyNvidiaProfile(profile, displayTarget);

                if (nvidiaApplied)
                {
                    ApplyGammaOnlyIfNeeded(profile, displayTarget);
                }
                else
                {
                    _logger.Info("Brightness/contrast/gamma are being applied through Windows gamma ramp fallback.");
                    _gammaService.BackupCurrentSettings(displayTarget);
                    _gammaService.ApplyColorProfile(profile.ColorProfile, displayTarget);
                }

                _logger.Info($"Profile applied: {profile.GameName}");
            }
            catch (Exception ex)
            {
                _logger.Error("Profile application failed. Attempting restore.", ex);
                TryRestore();
            }
        }
    }

    public void RestoreOriginalSettings(string reason, bool resetNeutralIfNoBackup = false)
    {
        lock (_gate)
        {
            _logger.Info($"Restore requested: {reason}");
            TryRestore(resetNeutralIfNoBackup);
        }
    }

    private void TryRestore(bool resetNeutralIfNoBackupOverride = false)
    {
        var fallbackTarget = _displayTargetProvider();
        var resetNeutralIfNoBackup = resetNeutralIfNoBackupOverride || _profileMayBeApplied;

        try
        {
            _nvidiaColorService.RestoreOriginalSettings();
        }
        catch (Exception ex)
        {
            _logger.Error("NVIDIA restore failed.", ex);
        }

        try
        {
            _gammaService.RestoreOriginalSettings(fallbackTarget, resetNeutralIfNoBackup);
        }
        catch (Exception ex)
        {
            _logger.Error("Gamma restore failed.", ex);
        }

        _profileMayBeApplied = false;
    }

    public void RestorePersistedBackupsIfAny()
    {
        lock (_gate)
        {
            _gammaService.RestorePersistedBackupsIfAny(_displayTargetProvider());
        }
    }

    private bool TryApplyNvidiaProfile(GameProfile profile, DisplayTarget displayTarget)
    {
        if (!_nvidiaColorService.IsAvailable)
        {
            _logger.Info("NVAPI is unavailable. Skipping NVIDIA path.");
            return false;
        }

        try
        {
            _nvidiaColorService.BackupCurrentSettings(displayTarget);
            return _nvidiaColorService.ApplyColorProfile(profile, displayTarget);
        }
        catch (Exception ex)
        {
            _logger.Warning($"NVAPI profile application failed. Falling back to Windows gamma ramp. {ex.Message}");
            return false;
        }
    }

    private void ApplyGammaOnlyIfNeeded(GameProfile profile, DisplayTarget displayTarget)
    {
        if (Math.Abs(profile.ColorProfile.Gamma - 1.0) <= 0.001)
        {
            _logger.Info("Gamma is neutral. Windows gamma ramp update skipped after NVAPI brightness/contrast apply.");
            return;
        }

        _logger.Info("Applying gamma through Windows gamma ramp after NVAPI brightness/contrast apply.");
        _gammaService.BackupCurrentSettings(displayTarget);
        _gammaService.ApplyColorProfile(
            new ColorProfile
            {
                BrightnessPercent = 100,
                ContrastPercent = 100,
                Gamma = profile.ColorProfile.Gamma,
                DigitalVibrancePercent = 50,
                HueDegrees = 0
            },
            displayTarget);
    }
}
