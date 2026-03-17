using System;
using System.Globalization;
using Avalonia.Data.Converters;
using d2c_launcher.Resources;

namespace d2c_launcher.Util;

/// <summary>
/// Returns true when the bound IntroStep value equals ConverterParameter.
/// Used to show/hide per-step content in the intro overlay.
/// </summary>
public class IntroStepConverter : IValueConverter
{
    public static readonly IntroStepConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int step && parameter is string p && int.TryParse(p, out var target))
            return step == target;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns "Далее" for any step except the last (4), where it returns "Начать играть".
/// </summary>
public class IntroNextButtonTextConverter : IValueConverter
{
    public static readonly IntroNextButtonTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int step && step >= 4 ? Strings.StartPlaying : Strings.Next;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
