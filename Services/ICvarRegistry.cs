using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Injectable registry of cvar mappings. Replaces direct access to the static
/// <see cref="CvarMapping"/> and <see cref="CompositeCvarMapping"/> arrays so that
/// consumers can be unit-tested with a controlled subset of entries.
/// </summary>
public interface ICvarRegistry
{
    IReadOnlyList<CvarEntry> GetEntries();
    IReadOnlyList<CompositeCvarEntry> GetCompositeEntries();
}
