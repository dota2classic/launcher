using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace d2c_launcher.Models;

public class ContentPackage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("optional")]
    public bool Optional { get; set; }
}

public class ContentRegistry
{
    [JsonPropertyName("packages")]
    public List<ContentPackage> Packages { get; set; } = new();
}
