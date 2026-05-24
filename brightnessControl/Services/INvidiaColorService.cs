using brightnessControl.Models;

namespace brightnessControl.Services;

public interface INvidiaColorService
{
    bool IsAvailable { get; }

    void BackupCurrentSettings(DisplayTarget displayTarget);

    bool ApplyColorProfile(GameProfile profile, DisplayTarget displayTarget);

    void RestoreOriginalSettings();
}
