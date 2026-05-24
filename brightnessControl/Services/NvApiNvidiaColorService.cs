using brightnessControl.Models;

namespace brightnessControl.Services;

internal sealed class NvApiNvidiaColorService : INvidiaColorService
{
    private readonly AppLogger _logger;
    private readonly NvApi _nvApi;
    private readonly HashSet<uint> _touchedDisplayIds = [];

    public NvApiNvidiaColorService(AppLogger logger, NvApi nvApi)
    {
        _logger = logger;
        _nvApi = nvApi;
    }

    public bool IsAvailable => true;

    public void BackupCurrentSettings(DisplayTarget displayTarget)
    {
        var displayId = _nvApi.GetDisplayId(displayTarget);
        _touchedDisplayIds.Add(displayId);
        _logger.Info($"NVAPI display target resolved: {displayTarget.DeviceName} -> 0x{displayId:X8}.");
        _logger.Warning(
            "NVAPI scanout intensity is supported only on selected GPU/driver/display configurations; if the driver rejects it, Windows gamma fallback will be used.");
        _logger.Warning(
            "NVAPI scanout state cannot expose the original texture values; restore will reset scanout intensity to neutral/default.");
    }

    public bool ApplyColorProfile(GameProfile profile, DisplayTarget displayTarget)
    {
        var displayId = _nvApi.GetDisplayId(displayTarget);
        _nvApi.ApplyScanoutColorProfile(displayId, profile.ColorProfile);
        var enabled = _nvApi.IsScanoutIntensityEnabled(displayId);

        if (profile.ColorProfile.DigitalVibrancePercent is not null &&
            profile.ColorProfile.DigitalVibrancePercent != 50)
        {
            _logger.Warning(
                "Digital Vibrance is not applied: NVIDIA's public NVAPI scanout functions do not expose the NVIDIA App Digital Vibrance or game filter slider.");
        }

        if (profile.ColorProfile.HueDegrees != 0)
        {
            _logger.Warning(
                "Hue is not applied: NVIDIA's public NVAPI scanout functions do not expose the NVIDIA App hue slider.");
        }

        _logger.Info(
            $"NVAPI scanout brightness/contrast applied to display 0x{displayId:X8}: brightness {profile.ColorProfile.BrightnessPercent}%, contrast {profile.ColorProfile.ContrastPercent}%, gamma {profile.ColorProfile.Gamma:N2}, vibrance {profile.ColorProfile.DigitalVibrancePercent ?? 50}%, hue {profile.ColorProfile.HueDegrees}, scanout intensity enabled={enabled}.");
        return true;
    }

    public void RestoreOriginalSettings()
    {
        if (_touchedDisplayIds.Count == 0)
        {
            _logger.Info("No NVAPI scanout profile was applied in this session. NVIDIA restore skipped.");
            return;
        }

        foreach (var displayId in _touchedDisplayIds.ToList())
        {
            _nvApi.ResetScanout(displayId);
            _logger.Info($"NVAPI scanout reset to neutral/default for display 0x{displayId:X8}.");
        }

        _touchedDisplayIds.Clear();
    }
}
