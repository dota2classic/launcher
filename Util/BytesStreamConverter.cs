using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace d2c_launcher.Util;

/// <summary>Converts a <c>byte[]</c> to a <see cref="MemoryStream"/> for GIF/image binding.</summary>
public sealed class BytesStreamConverter : IValueConverter
{
    public static readonly BytesStreamConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is byte[] bytes ? new MemoryStream(bytes) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
