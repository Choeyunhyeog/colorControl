using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using brightnessControl.Models;

namespace brightnessControl.Services;

public sealed class ProfileService
{
    private const int CurrentProfilesSchemaVersion = 2;

    private readonly AppLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProfileService(AppLogger logger, string? filePath = null)
    {
        _logger = logger;
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "brightnessControl",
            "profiles.json");

        var document = LoadDocument();
        DisplayTarget = document.DisplayTarget ?? CreateDefaultDisplayTarget();
        MigrateProfiles(document);
        Profiles = new ObservableCollection<GameProfile>(document.Profiles);
    }

    public string FilePath { get; }

    public DisplayTarget DisplayTarget { get; }

    public ObservableCollection<GameProfile> Profiles { get; }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var document = new ProfilesDocument
        {
            SchemaVersion = CurrentProfilesSchemaVersion,
            DisplayTarget = DisplayTarget,
            Profiles = Profiles.ToList()
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(document, _jsonOptions));
        _logger.Info($"Saved {Profiles.Count} profile(s).");
    }

    private ProfilesDocument LoadDocument()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        if (!File.Exists(FilePath))
        {
            var defaults = new[] { CreateExampleProfile() };
            var document = new ProfilesDocument
            {
                SchemaVersion = CurrentProfilesSchemaVersion,
                DisplayTarget = CreateDefaultDisplayTarget(),
                Profiles = defaults.ToList()
            };
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(document, _jsonOptions));
            _logger.Info("Created default profiles.json.");
            return document;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var document = JsonSerializer.Deserialize<ProfilesDocument>(json, _jsonOptions);
            var profiles = document?.Profiles ?? [];
            _logger.Info($"Loaded {profiles.Count} profile(s).");
            return new ProfilesDocument
            {
                SchemaVersion = document?.SchemaVersion ?? 0,
                DisplayTarget = document?.DisplayTarget ?? CreateDefaultDisplayTarget(),
                Profiles = profiles
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load profiles.json. Using an in-memory default profile.", ex);
            return new ProfilesDocument
            {
                SchemaVersion = CurrentProfilesSchemaVersion,
                DisplayTarget = CreateDefaultDisplayTarget(),
                Profiles = [CreateExampleProfile()]
            };
        }
    }

    private static DisplayTarget CreateDefaultDisplayTarget()
    {
        return new DisplayTarget
        {
            DeviceName = "Primary",
            PrimaryOnly = true
        };
    }

    private void MigrateProfiles(ProfilesDocument document)
    {
        if (document.SchemaVersion >= CurrentProfilesSchemaVersion)
        {
            return;
        }

        foreach (var profile in document.Profiles)
        {
            profile.ColorProfile.BrightnessPercent = profile.ColorProfile.BrightnessPercent + 100;
            profile.ColorProfile.ContrastPercent = profile.ColorProfile.ContrastPercent + 100;
            profile.ColorProfile.DigitalVibrancePercent ??= 50;
            profile.ColorProfile.HueDegrees = profile.ColorProfile.HueDegrees;
        }

        _logger.Info(
            "Migrated color profiles to NVIDIA-style defaults and current limits: brightness 80-120, contrast 80-120, gamma 0.30-2.80, vibrance 0-100, hue 0-359.");
    }

    private static GameProfile CreateExampleProfile()
    {
        return new GameProfile
        {
            GameName = "Notepad Test",
            ProcessName = "notepad.exe",
            Enabled = false,
            ColorProfile = new ColorProfile
            {
                BrightnessPercent = 100,
                ContrastPercent = 100,
                Gamma = 1.0,
                DigitalVibrancePercent = 50,
                HueDegrees = 0
            },
            DisplayTarget = new DisplayTarget
            {
                DeviceName = "Primary",
                PrimaryOnly = true
            }
        };
    }

    private sealed class ProfilesDocument
    {
        public int SchemaVersion { get; set; }

        public DisplayTarget? DisplayTarget { get; set; }

        public List<GameProfile> Profiles { get; set; } = [];
    }
}
