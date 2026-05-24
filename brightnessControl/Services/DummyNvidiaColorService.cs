using brightnessControl.Models;

namespace brightnessControl.Services;

public sealed class DummyNvidiaColorService : INvidiaColorService
{
    private readonly AppLogger _logger;

    public DummyNvidiaColorService(AppLogger logger)
    {
        _logger = logger;
    }

    public bool IsAvailable => false;

    public void BackupCurrentSettings(DisplayTarget displayTarget)
    {
        _logger.Info($"NVIDIA backup skipped for {displayTarget.DeviceName}; dummy service is active.");
    }

    public bool ApplyColorProfile(GameProfile profile, DisplayTarget displayTarget)
    {
        _logger.Warning(
            $"NVIDIA API is not available. Falling back to Windows gamma ramp for {profile.GameName}.");
        return false;
    }

    public void RestoreOriginalSettings()
    {
        _logger.Info("NVIDIA App/Control Panel restore skipped; no NVIDIA settings were changed by this build.");
    }
}
