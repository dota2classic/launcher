using System;
using System.Collections.Generic;
using System.Text.Json;

namespace d2c_launcher.Services;

/// <summary>
/// Loads <c>Resources/Locales/ru.json</c> as an embedded resource and provides
/// dot-notation string lookup with named placeholder substitution.
/// </summary>
public static class I18n
{
    private static Dictionary<string, string>? _strings;

    private static Dictionary<string, string> Strings => _strings ??= Load();

    private static Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var assembly = typeof(I18n).Assembly;
        var resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("ru.json"));
        if (resourceName is null) return result;

        using var stream = assembly.GetManifestResourceStream(resourceName);

        using var doc = JsonDocument.Parse(stream);
        Flatten(doc.RootElement, string.Empty, result);
        return result;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
                Flatten(property.Value, key, result);
            else if (property.Value.ValueKind == JsonValueKind.String)
                result[key] = property.Value.GetString()!;
        }
    }

    /// <summary>
    /// Returns the localized string for <paramref name="key"/>.
    /// Named placeholders like <c>{cp}</c> are substituted from <paramref name="args"/>.
    /// Falls back to the key itself when not found.
    /// </summary>
    public static string T(string key, params (string Name, object Value)[] args)
    {
        if (!Strings.TryGetValue(key, out var value))
            return key;

        foreach (var (name, val) in args)
            value = value.Replace($"{{{name}}}", val?.ToString() ?? string.Empty, StringComparison.Ordinal);

        return value;
    }
}
