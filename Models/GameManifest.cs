using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace d2c_launcher.Models;

public enum ManifestFileMode { Exact, Existing }

public class GameManifestFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "exact";

    [JsonIgnore]
    public ManifestFileMode FileMode =>
        Mode == "existing" ? ManifestFileMode.Existing : ManifestFileMode.Exact;

    /// <summary>
    /// Set when loading from a package registry — the registry folder name used to
    /// construct the CDN download URL: /files/{PackageFolder}/{Path}.
    /// </summary>
    [JsonIgnore]
    public string PackageFolder { get; set; } = "";

    [JsonIgnore]
    public string PackageName { get; set; } = "";
}

public class GameManifest
{
    [JsonPropertyName("files")]
    public List<GameManifestFile> Files { get; set; } = new();
}
