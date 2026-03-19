using System;
using Avalonia.Markup.Xaml;
using d2c_launcher.Services;

namespace d2c_launcher.Util;

/// <summary>
/// Avalonia markup extension for static string lookup via <see cref="I18n"/>.
/// Usage in XAML: <c>xmlns:l="clr-namespace:d2c_launcher.Util"</c> then <c>{l:T 'some.key'}</c>
/// </summary>
public sealed class TExtension : MarkupExtension
{
    private readonly string _key;

    public TExtension(string key) => _key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => I18n.T(_key);
}
