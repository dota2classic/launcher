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
}

public class GameManifest
{
    [JsonPropertyName("files")]
    public List<GameManifestFile> Files { get; set; } = new();
}
