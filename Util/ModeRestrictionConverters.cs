using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace d2c_launcher.Util;

/// <summary>Returns a "not-allowed" cursor when the value is true, "hand" otherwise.</summary>
public sealed class BoolToNotAllowedCursorConverter : IValueConverter
{
    private static readonly Cursor CursorNotAllowed = new(StandardCursorType.No);
    private static readonly Cursor CursorHand       = new(StandardCursorType.Hand);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? CursorNotAllowed : CursorHand;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Returns 0.45 opacity when the value is true (restricted), 1.0 otherwise.</summary>
public sealed class BoolToRestrictedOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.45 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
