using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using brightnessControl.Models;
using Forms = System.Windows.Forms;

namespace brightnessControl.Services;

public sealed class GammaService
{
    private readonly AppLogger _logger;
    private readonly object _gate = new();
    private readonly string _backupFilePath;
    private readonly Dictionary<string, ushort[]> _originalGammaRamps = new(StringComparer.OrdinalIgnoreCase);

    public GammaService(AppLogger logger, string? backupFilePath = null)
    {
        _logger = logger;
        _backupFilePath = backupFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "brightnessControl",
            "gamma-backup.json");
        LoadPersistedBackups();
    }

    public bool HasBackup
    {
        get
        {
            lock (_gate)
            {
                return _originalGammaRamps.Count > 0;
            }
        }
    }

    public void BackupCurrentSettings(DisplayTarget displayTarget)
    {
        lock (_gate)
        {
            var targetKey = GetTargetKey(displayTarget);
            if (_originalGammaRamps.ContainsKey(targetKey))
            {
                _logger.Info($"Gamma backup already exists for {targetKey}.");
                return;
            }

            _originalGammaRamps[targetKey] = ReadGammaRamp(displayTarget);
            SavePersistedBackups();
            _logger.Info($"Backed up current gamma ramp for {targetKey} ({GetResolvedDeviceName(displayTarget)}).");
        }
    }

    public void ApplyColorProfile(ColorProfile colorProfile, DisplayTarget displayTarget)
    {
        lock (_gate)
        {
            var targetKey = GetTargetKey(displayTarget);
            if (!_originalGammaRamps.ContainsKey(targetKey))
            {
                throw new InvalidOperationException("Display color settings cannot be applied before backing up the current settings.");
            }

            var safeBrightness = ClampAndLog(
                colorProfile.BrightnessPercent,
                ColorProfile.MinBrightnessPercent,
                ColorProfile.MaxBrightnessPercent,
                "Brightness");
            var safeContrast = ClampAndLog(
                colorProfile.ContrastPercent,
                ColorProfile.MinContrastPercent,
                ColorProfile.MaxContrastPercent,
                "Contrast");
            var safeGamma = ClampAndLog(
                colorProfile.Gamma,
                ColorProfile.MinGamma,
                ColorProfile.MaxGamma,
                "Gamma");

            var targetRamp = BuildGammaRamp(safeBrightness, safeContrast, safeGamma);
            WriteGammaRamp(displayTarget, targetRamp);
            var appliedRamp = ReadGammaRamp(displayTarget);

            if (GammaRampsMatch(targetRamp, appliedRamp))
            {
                _logger.Info(
                    $"Windows accepted gamma ramp on {targetKey} ({GetResolvedDeviceName(displayTarget)}): brightness {safeBrightness}%, contrast {safeContrast}%, gamma {safeGamma:N2}, samples {FormatRampSamples(appliedRamp)}.");
            }
            else
            {
                _logger.Warning(
                    $"Color profile write returned success, but verification did not match on {targetKey}. Expected samples {FormatRampSamples(targetRamp)}, actual samples {FormatRampSamples(appliedRamp)}. The display driver or game mode may be overriding gamma.");
            }
        }
    }

    public void RestoreOriginalSettings(DisplayTarget fallbackTarget, bool resetNeutralIfNoBackup)
    {
        lock (_gate)
        {
            if (_originalGammaRamps.Count == 0)
            {
                if (resetNeutralIfNoBackup)
                {
                    var neutralRamp = BuildGammaRamp(100, 100, 1.0);
                    WriteGammaRamp(fallbackTarget, neutralRamp);
                    _logger.Warning(
                        $"No gamma backup exists. Reset gamma ramp to neutral for {GetTargetKey(fallbackTarget)} ({GetResolvedDeviceName(fallbackTarget)}), samples {FormatRampSamples(neutralRamp)}.");
                }
                else
                {
                    _logger.Info("No gamma backup exists. Restore skipped.");
                }

                return;
            }

            foreach (var (targetKey, ramp) in _originalGammaRamps.ToList())
            {
                WriteGammaRamp(new DisplayTarget { DeviceName = targetKey }, ramp);
                var restoredRamp = ReadGammaRamp(new DisplayTarget { DeviceName = targetKey });
                _logger.Info($"Restored original gamma ramp for {targetKey}, samples {FormatRampSamples(restoredRamp)}.");
            }

            _originalGammaRamps.Clear();
            DeletePersistedBackups();
        }
    }

    public void RestorePersistedBackupsIfAny(DisplayTarget fallbackTarget)
    {
        lock (_gate)
        {
            if (_originalGammaRamps.Count == 0)
            {
                return;
            }

            _logger.Warning("Persisted gamma backup found from a previous run. Restoring it before monitoring starts.");
            RestoreOriginalSettings(fallbackTarget, resetNeutralIfNoBackup: false);
        }
    }

    private static ushort[] ReadGammaRamp(DisplayTarget displayTarget)
    {
        var hdc = CreateTargetDc(displayTarget);
        if (hdc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open display DC for {GetTargetKey(displayTarget)}.");
        }

        try
        {
            var ramp = new ushort[768];
            if (!GetDeviceGammaRamp(hdc, ramp))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDeviceGammaRamp failed.");
            }

            return (ushort[])ramp.Clone();
        }
        finally
        {
            ReleaseTargetDc(displayTarget, hdc);
        }
    }

    private void LoadPersistedBackups()
    {
        try
        {
            if (!File.Exists(_backupFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_backupFilePath);
            var document = JsonSerializer.Deserialize<GammaBackupDocument>(json);
            if (document?.Ramps is null)
            {
                return;
            }

            foreach (var (targetKey, ramp) in document.Ramps)
            {
                if (ramp.Length == 768)
                {
                    _originalGammaRamps[targetKey] = ramp;
                }
            }

            if (_originalGammaRamps.Count > 0)
            {
                _logger.Warning($"Loaded {_originalGammaRamps.Count} persisted gamma backup(s).");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load persisted gamma backups.", ex);
        }
    }

    private void SavePersistedBackups()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_backupFilePath)!);
            var document = new GammaBackupDocument
            {
                Ramps = _originalGammaRamps.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase)
            };
            File.WriteAllText(_backupFilePath, JsonSerializer.Serialize(document));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to persist gamma backups.", ex);
        }
    }

    private void DeletePersistedBackups()
    {
        try
        {
            if (File.Exists(_backupFilePath))
            {
                File.Delete(_backupFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to delete persisted gamma backup file.", ex);
        }
    }

    private static void WriteGammaRamp(DisplayTarget displayTarget, ushort[] ramp)
    {
        if (ramp.Length != 768)
        {
            throw new ArgumentException("Gamma ramp must contain 768 values.", nameof(ramp));
        }

        var hdc = CreateTargetDc(displayTarget);
        if (hdc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open display DC for {GetTargetKey(displayTarget)}.");
        }

        try
        {
            if (!SetDeviceGammaRamp(hdc, ramp))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetDeviceGammaRamp failed.");
            }
        }
        finally
        {
            ReleaseTargetDc(displayTarget, hdc);
        }
    }

    private int ClampAndLog(int value, int min, int max, string label)
    {
        var clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            _logger.Warning($"{label} {value} was clamped to {clamped}.");
        }

        return clamped;
    }

    private double ClampAndLog(double value, double min, double max, string label)
    {
        var clamped = Math.Clamp(value, min, max);
        if (Math.Abs(clamped - value) > 0.001)
        {
            _logger.Warning($"{label} {value:N2} was clamped to {clamped:N2}.");
        }

        return clamped;
    }

    private static string GetTargetKey(DisplayTarget displayTarget)
    {
        return string.IsNullOrWhiteSpace(displayTarget.DeviceName)
            ? "Primary"
            : displayTarget.DeviceName.Trim();
    }

    private static bool IsPrimaryTarget(DisplayTarget displayTarget)
    {
        return string.Equals(GetTargetKey(displayTarget), "Primary", StringComparison.OrdinalIgnoreCase);
    }

    private static IntPtr CreateTargetDc(DisplayTarget displayTarget)
    {
        return CreateDC("DISPLAY", GetResolvedDeviceName(displayTarget), null, IntPtr.Zero);
    }

    private static void ReleaseTargetDc(DisplayTarget displayTarget, IntPtr hdc)
    {
        DeleteDC(hdc);
    }

    private static string GetResolvedDeviceName(DisplayTarget displayTarget)
    {
        if (!IsPrimaryTarget(displayTarget))
        {
            return GetTargetKey(displayTarget);
        }

        return Forms.Screen.PrimaryScreen?.DeviceName ?? "DISPLAY";
    }

    private static bool GammaRampsMatch(ushort[] expected, ushort[] actual)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > 256)
            {
                return false;
            }
        }

        return true;
    }

    private static ushort[] BuildGammaRamp(int brightnessPercent, int contrastPercent, double gamma)
    {
        var ramp = new ushort[768];
        var brightnessOffset = (brightnessPercent - 100) / 100.0;
        var contrastFactor = contrastPercent / 100.0;

        for (var i = 0; i < 256; i++)
        {
            var normalized = i / 255.0;
            var corrected = Math.Pow(normalized, 1.0 / gamma);
            var adjusted = ((corrected - 0.5) * contrastFactor) + 0.5 + brightnessOffset;
            var value = (ushort)Math.Clamp((int)Math.Round(adjusted * 65535.0), 0, 65535);

            ramp[i] = value;
            ramp[i + 256] = value;
            ramp[i + 512] = value;
        }

        return ramp;
    }

    private static string FormatRampSamples(ushort[] ramp)
    {
        return $"64={ramp[64]}, 128={ramp[128]}, 192={ramp[192]}";
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDC(
        string? driverName,
        string? deviceName,
        string? output,
        IntPtr initData);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool GetDeviceGammaRamp(IntPtr hdc, [Out] ushort[] ramp);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, [In] ushort[] ramp);

    private sealed class GammaBackupDocument
    {
        public Dictionary<string, ushort[]> Ramps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
