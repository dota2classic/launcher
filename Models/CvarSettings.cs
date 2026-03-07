namespace d2c_launcher.Models;

/// <summary>
/// In-memory state for game cvars backed by config.cfg.
/// Separate from <see cref="GameLaunchSettings"/> which stores launcher-only flags in JSON.
/// </summary>
public class CvarSettings
{
    public int? FpsMax { get; set; }
    public bool Console { get; set; } = true;
    public bool DisableCameraZoom { get; set; }
    public bool ForceRightClickAttack { get; set; }
    public AutoAttackMode AutoAttack { get; set; } = AutoAttackMode.AfterSpell;
    public bool RightMouseAutoRepeat { get; set; }
    public bool ResetCameraOnSpawn { get; set; } = true;
    public bool TeleportRequiresHalt { get; set; }
}
