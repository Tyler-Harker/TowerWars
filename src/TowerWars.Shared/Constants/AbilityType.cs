namespace TowerWars.Shared.Constants;

public enum AbilityType : byte
{
    None = 0,

    // Offensive
    Fireball = 1,
    LightningStrike = 2,
    Meteor = 3,
    Blizzard = 4,

    // Defensive
    Heal = 10,
    Shield = 11,
    Fortify = 12,

    // Utility
    SpeedBoost = 20,
    GoldRush = 21,
    TimeWarp = 22,

    // Ultimate
    Nuke = 30,
    Resurrection = 31,
    Invincibility = 32
}
