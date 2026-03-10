using Avalonia;

namespace d2c_launcher.Services;

/// <summary>
/// Applies a UI font scale step to the application resource dictionary.
/// Each step adds 1pt to every named font size resource.
/// </summary>
public static class UiScaleService
{
    // Base font sizes (scale = 0)
    private static readonly (string Key, double Base)[] FontSizes =
    [
        ("FontSizeXS",  10),
        ("FontSizeSM",  11),
        ("FontSizeBase",12),
        ("FontSizeMD",  13),
        ("FontSizeLG",  14),
        ("FontSizeXL",  16),
        ("FontSize2XL", 18),
        ("FontSize3XL", 22),
        ("FontSize4XL", 28),
    ];

    public static void Apply(int scale)
    {
        var app = Application.Current;
        if (app == null) return;

        foreach (var (key, baseSize) in FontSizes)
            app.Resources[key] = baseSize + scale;
    }
}
