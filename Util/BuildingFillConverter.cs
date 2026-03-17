using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace d2c_launcher.Util;

/// <summary>Converts team number (2=Radiant, 3=Dire) to the building fill brush.</summary>
public sealed class BuildingFillConverter : IValueConverter
{
    private static readonly IBrush Radiant = new SolidColorBrush(Color.Parse("#41a525"));
    private static readonly IBrush Dire = new SolidColorBrush(Color.Parse("#c23c2a"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int team && team == 2 ? Radiant : Dire;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
