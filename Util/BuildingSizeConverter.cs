using System;
using System.Globalization;
using Avalonia.Data.Converters;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Util;

/// <summary>Converts <see cref="BuildingType"/> to a canvas pixel size for the minimap rectangle.</summary>
public sealed class BuildingSizeConverter : IValueConverter
{
    public static readonly BuildingSizeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is BuildingType type ? type switch
        {
            BuildingType.Ancient => 14.0,
            BuildingType.Tower => 9.0,
            BuildingType.Barrack => 6.0,
            _ => 8.0,
        } : 8.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
