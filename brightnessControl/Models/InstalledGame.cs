namespace brightnessControl.Models;

public sealed class InstalledGame
{
    public required string Name { get; init; }

    public required string ProcessName { get; init; }

    public required string ExecutablePath { get; init; }

    public string DisplayName => $"{Name} ({ProcessName})";
}
