namespace d2c_launcher.Models;

public enum AutoAttackMode
{
    Off,        // dota_player_units_auto_attack 0, dota_player_units_auto_attack_after_spell 0
    AfterSpell, // dota_player_units_auto_attack 0, dota_player_units_auto_attack_after_spell 1
    Always      // dota_player_units_auto_attack 1, dota_player_units_auto_attack_after_spell 1
}
