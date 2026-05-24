namespace brightnessControl.Models;

public sealed class DisplayTarget
{
    public string DeviceName { get; set; } = "Primary";

    public bool PrimaryOnly { get; set; } = true;
}
