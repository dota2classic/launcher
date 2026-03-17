using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace d2c_launcher.Util;

/// <summary>Converts bool tilt flag to a rotation angle: true → 45°, false → 0°.</summary>
public sealed class BoolToRotationConverter : IValueConverter
{
    public static readonly BoolToRotationConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 45.0 : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
