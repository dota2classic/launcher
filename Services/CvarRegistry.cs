using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Default implementation of <see cref="ICvarRegistry"/> — delegates to the static
/// <see cref="CvarMapping"/> and <see cref="CompositeCvarMapping"/> arrays used in production.
/// </summary>
public class CvarRegistry : ICvarRegistry
{
    public IReadOnlyList<CvarEntry> GetEntries() => CvarMapping.Entries;
    public IReadOnlyList<CompositeCvarEntry> GetCompositeEntries() => CompositeCvarMapping.Entries;
}
