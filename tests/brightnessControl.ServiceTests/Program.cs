using brightnessControl.Models;
using brightnessControl.Services;

var tempDir = Path.Combine(Path.GetTempPath(), "brightnessControl.ServiceTests", Guid.NewGuid().ToString("N"));
var filePath = Path.Combine(tempDir, "profiles.json");

try
{
    var logger = new AppLogger();
    var service = new ProfileService(logger, filePath);

    Assert(File.Exists(filePath), "ProfileService should create profiles.json when missing.");
    Assert(service.Profiles.Count == 1, "ProfileService should load a default profile.");

    service.Profiles.Clear();
    service.Profiles.Add(new GameProfile
    {
        GameName = "Unit Test Game",
        ProcessName = "unit-test-game.exe",
        Enabled = true,
        ColorProfile = new ColorProfile
        {
            Gamma = 1.25,
            DigitalVibrancePercent = 70
        },
        DisplayTarget = new DisplayTarget
        {
            DeviceName = "Primary",
            PrimaryOnly = true
        }
    });
    service.Save();

    var reloaded = new ProfileService(logger, filePath);
    var profile = reloaded.Profiles.Single();

    Assert(profile.GameName == "Unit Test Game", "Profile name should round-trip.");
    Assert(profile.ProcessName == "unit-test-game.exe", "Process name should round-trip.");
    Assert(Math.Abs(profile.ColorProfile.Gamma - 1.25) < 0.001, "Gamma should round-trip.");
    Assert(profile.ColorProfile.DigitalVibrancePercent == 70, "Digital Vibrance value should round-trip.");

    Console.WriteLine("Service tests passed.");
    return 0;
}
finally
{
    if (Directory.Exists(tempDir))
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
