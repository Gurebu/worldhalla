namespace Worldhalla.Plugin.UI.Preview.Locations;

// The map learns of boss locations from vegvisirs and runestones placed elsewhere, which pin them under the boss's
// name. Reading those would mean loading every location prefab, so the pins they make are listed here instead
internal static class BossLocations
{
    public static readonly Dictionary<string, string> Captions = new()
    {
        ["Eikthyrnir"] = "$enemy_eikthyr",
        ["GDKing"] = "$enemy_gdking",
        ["Bonemass"] = "$enemy_bonemass",
        ["Dragonqueen"] = "$enemy_dragon",
        ["GoblinKing"] = "$enemy_goblinking",
        ["Mistlands_DvergrBossEntrance1"] = "$enemy_seekerqueen",
        ["FaderLocation"] = "$enemy_fader",
        ["DN_Bossroom"] = "$enemy_frozenking",
    };
}
