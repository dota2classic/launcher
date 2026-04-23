using System;

namespace d2c_launcher.Models;

public sealed class ManifestScanOptions
{
    public static ManifestScanOptions Foreground { get; } = new();

    public bool Throttled { get; init; }
    public int? MaxDegreeOfParallelism { get; init; }
    public TimeSpan BatchDelay { get; init; } = TimeSpan.Zero;
    public int BatchSize { get; init; } = 128;
}
