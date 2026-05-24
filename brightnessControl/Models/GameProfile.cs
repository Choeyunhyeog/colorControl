namespace brightnessControl.Models;

public sealed class GameProfile
{
    public string GameName { get; set; } = "New Game";

    public string ProcessName { get; set; } = "game.exe";

    public bool Enabled { get; set; } = true;

    public ColorProfile ColorProfile { get; set; } = new();

    public DisplayTarget DisplayTarget { get; set; } = new();

    public static GameProfile CreateDefault()
    {
        return new GameProfile
        {
            GameName = "New Game",
            ProcessName = "game.exe",
            Enabled = true,
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
}
