using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Util;

/// <summary>
/// Maps <see cref="TriviaAnswerResult"/> to a background brush for MC answer buttons.
/// None = transparent, Correct = green-tinted, Wrong = red-tinted.
/// </summary>
public sealed class TriviaAnswerResultBrushConverter : IValueConverter
{
    private static readonly IBrush BrushNone    = new SolidColorBrush(Color.Parse("#1a1f26"));
    private static readonly IBrush BrushCorrect = new SolidColorBrush(Color.Parse("#1B4A1B"));
    private static readonly IBrush BrushWrong   = new SolidColorBrush(Color.Parse("#4A1B1B"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            TriviaAnswerResult.Correct => BrushCorrect,
            TriviaAnswerResult.Wrong   => BrushWrong,
            _                          => BrushNone,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
