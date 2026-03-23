using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Injectable registry of cvar mappings. Replaces direct access to the static
/// <see cref="CvarMapping"/> and <see cref="CompositeCvarMapping"/> arrays so that
/// consumers can be unit-tested with a controlled subset of entries.
/// </summary>
public interface ICvarRegistry
{
    CvarEntry[] GetEntries();
    CompositeCvarEntry[] GetCompositeEntries();
}

/// <summary>
/// Default implementation — delegates to the static <see cref="CvarMapping"/> and
/// <see cref="CompositeCvarMapping"/> arrays used in production.
/// </summary>
public class DefaultCvarRegistry : ICvarRegistry
{
    public CvarEntry[] GetEntries() => CvarMapping.Entries;
    public CompositeCvarEntry[] GetCompositeEntries() => CompositeCvarMapping.Entries;
}
