using System.Collections.Generic;
using Avalonia;

namespace d2c_launcher.ViewModels;

public enum BuildingType { Tower, Barrack, Ancient }

public class BuildingOnMapViewModel
{
    public BuildingType Type { get; }
    public int Team { get; } // 2 = radiant, 3 = dire
    public bool Alive { get; }
    public bool Tilt { get; }
    public Thickness Margin { get; }

    // Building sizes on the 320×320 canvas
    private const double TowerSize = 9;
    private const double BarrackSize = 6;
    private const double AncientSize = 14;

    public BuildingOnMapViewModel(BuildingType type, int team, bool alive, double posX, double posY, bool tilt = false)
    {
        Type = type;
        Team = team;
        Alive = alive;
        Tilt = tilt;

        var size = type switch
        {
            BuildingType.Tower => TowerSize,
            BuildingType.Barrack => BarrackSize,
            BuildingType.Ancient => AncientSize,
            _ => TowerSize,
        };

        const double CanvasSize = 320;
        double cx = Remap(posX, 0.02, 0.96) * CanvasSize - size / 2;
        double cy = (1.0 - Remap(posY, 0.04, 0.94)) * CanvasSize - size / 2;
        Margin = new Thickness(cx, cy, 0, 0);
    }

    private static double Remap(double v, double low, double high) => v * (high - low) + low;

    // --- Static building position tables (ported from React web app) ---

    private record Pos(double X, double Y, int Bit);

    private static readonly Pos[] RadiantTowers =
    [
        new(0.07, 0.63, 0),
        new(0.072, 0.441, 1),
        new(0.052, 0.264, 2),
        new(0.393, 0.405, 3),
        new(0.261, 0.296, 4),
        new(0.174, 0.21, 5),
        new(0.845, 0.1, 6),
        new(0.46, 0.069, 7),
        new(0.226, 0.067, 8),
        new(0.098, 0.154, 9),
        new(0.118, 0.134, 10),
    ];

    private static readonly Pos[] DireTowers =
    [
        new(0.165, 0.915, 0),
        new(0.5, 0.912, 1),
        new(0.747, 0.9, 2),
        new(0.56, 0.528, 3),
        new(0.676, 0.649, 4),
        new(0.79, 0.762, 5),
        new(0.908, 0.382, 6),
        new(0.908, 0.527, 7),
        new(0.908, 0.71, 8),
        new(0.85, 0.838, 9),
        new(0.873, 0.813, 10),
    ];

    private static readonly Pos[] RadiantBarracks =
    [
        new(0.062, 0.243, 0),
        new(0.032, 0.243, 1),
        new(0.168, 0.179, 2),
        new(0.143, 0.204, 3),
        new(0.2, 0.049, 4),
        new(0.2, 0.085, 5),
    ];

    private static readonly Pos[] DireBarracks =
    [
        new(0.768, 0.887, 0),
        new(0.768, 0.925, 1),
        new(0.821, 0.766, 2),
        new(0.795, 0.792, 3),
        new(0.898, 0.735, 4),
        new(0.929, 0.735, 5),
    ];

    private static readonly Pos RadiantAncient = new(0.083, 0.118, 0);
    private static readonly Pos DireAncient = new(0.876, 0.843, 0);

    private static readonly HashSet<int> TowerTiltBits = [3, 4, 5, 9, 10];
    private static readonly HashSet<int> BarrackTiltBits = [2, 3];

    private static bool BitSet(int mask, int bit) => (mask & (1 << bit)) != 0;

    /// <summary>
    /// Builds the full list of building view models from the tower/barrack bitmasks.
    /// towers[0] = radiant towers, towers[1] = dire towers (same for barracks).
    /// </summary>
    public static IEnumerable<BuildingOnMapViewModel> BuildAll(int radiantTowers, int direTowers,
        int radiantBarracks, int direBarracks)
    {
        var list = new List<BuildingOnMapViewModel>();

        // Ancients (always alive — no bit for them in the mask)
        list.Add(new(BuildingType.Ancient, 2, true, RadiantAncient.X, RadiantAncient.Y, tilt: true));
        list.Add(new(BuildingType.Ancient, 3, true, DireAncient.X, DireAncient.Y, tilt: true));

        foreach (var t in RadiantTowers)
            if (BitSet(radiantTowers, t.Bit))
                list.Add(new(BuildingType.Tower, 2, true, t.X, t.Y, TowerTiltBits.Contains(t.Bit)));
        foreach (var t in DireTowers)
            if (BitSet(direTowers, t.Bit))
                list.Add(new(BuildingType.Tower, 3, true, t.X, t.Y, TowerTiltBits.Contains(t.Bit)));

        foreach (var b in RadiantBarracks)
            if (BitSet(radiantBarracks, b.Bit))
                list.Add(new(BuildingType.Barrack, 2, true, b.X, b.Y, BarrackTiltBits.Contains(b.Bit)));
        foreach (var b in DireBarracks)
            if (BitSet(direBarracks, b.Bit))
                list.Add(new(BuildingType.Barrack, 3, true, b.X, b.Y, BarrackTiltBits.Contains(b.Bit)));

        return list;
    }
}
