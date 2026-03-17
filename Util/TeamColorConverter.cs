using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace d2c_launcher.Util;

/// <summary>Converts team number (2=Radiant, 3=Dire) to a border color.</summary>
public sealed class TeamColorConverter : IValueConverter
{
    public static readonly TeamColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int team)
        {
            return team == 2
                ? Color.Parse("#c0de15")  // radiant yellow-green
                : Color.Parse("#c23c2a"); // dire red
        }
        return Color.Parse("#888888");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
