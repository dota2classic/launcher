using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace d2c_launcher.Util;

public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? 0.45 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? false : true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Maps IsBanned (bool) → red border brush for banned players, normal border otherwise.</summary>
public sealed class BanBorderBrushConverter : IValueConverter
{
    private static readonly IBrush BannedBrush = new SolidColorBrush(Color.Parse("#C0392B"));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.Parse("#2D2922"));

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true ? BannedBrush : NormalBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
