using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace brightnessControl.Models;

public sealed class ColorProfile : INotifyPropertyChanged
{
    public const int MinBrightnessPercent = 80;
    public const int MaxBrightnessPercent = 120;
    public const int MinContrastPercent = 80;
    public const int MaxContrastPercent = 120;
    public const double MinGamma = 0.30;
    public const double MaxGamma = 2.80;
    public const int MinDigitalVibrancePercent = 0;
    public const int MaxDigitalVibrancePercent = 100;
    public const int MinHueDegrees = 0;
    public const int MaxHueDegrees = 359;

    private int _brightnessPercent = 100;
    private int _contrastPercent = 100;
    private double _gamma = 1.0;
    private int? _digitalVibrancePercent = 50;
    private int _hueDegrees;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int BrightnessPercent
    {
        get => _brightnessPercent;
        set => SetField(ref _brightnessPercent, Math.Clamp(value, MinBrightnessPercent, MaxBrightnessPercent));
    }

    public int ContrastPercent
    {
        get => _contrastPercent;
        set => SetField(ref _contrastPercent, Math.Clamp(value, MinContrastPercent, MaxContrastPercent));
    }

    public double Gamma
    {
        get => _gamma;
        set => SetField(ref _gamma, ClampGamma(value));
    }

    public int? DigitalVibrancePercent
    {
        get => _digitalVibrancePercent;
        set => SetField(ref _digitalVibrancePercent, value is null
            ? 50
            : Math.Clamp(value.Value, MinDigitalVibrancePercent, MaxDigitalVibrancePercent));
    }

    public int HueDegrees
    {
        get => _hueDegrees;
        set => SetField(ref _hueDegrees, Math.Clamp(value, MinHueDegrees, MaxHueDegrees));
    }

    private static double ClampGamma(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? 1.0
            : Math.Clamp(value, MinGamma, MaxGamma);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
