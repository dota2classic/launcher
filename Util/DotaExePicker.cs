using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using d2c_launcher.Services;

namespace d2c_launcher.Util;

/// <summary>
/// Shared helper for picking dota.exe via a file dialog and resolving the game directory.
/// </summary>
public static class DotaExePicker
{
    /// <summary>
    /// Opens a file picker for dota.exe, validates the resulting directory, and returns
    /// the game directory path — or null if the user cancelled or picked an invalid directory.
    /// <paramref name="validationError"/> is set when the directory failed validation.
    /// </summary>
    public static async Task<(string? Dir, string? ValidationError)> PickAsync(TopLevel topLevel)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите dota.exe",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("dota.exe") { Patterns = new[] { "dota.exe" } }
            }
        });

        if (files.Count == 0)
            return (null, null);

        var exePath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(exePath))
            return (null, null);

        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir))
            return (null, null);

        if (!GameDirectoryValidator.IsAcceptable(dir, out var error))
            return (null, error);

        return (dir, null);
    }
}
