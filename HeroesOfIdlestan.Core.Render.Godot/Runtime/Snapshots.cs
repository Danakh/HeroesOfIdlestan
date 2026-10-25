namespace HeroesOfIdlestan.Core.Render.Godot.Runtime;

/// <summary>
/// Instantanés destinés à l'overlay. Construits en une seule prise du verrou de l'hôte : lire dix
/// propriétés une à une ferait dix aller-retours et donnerait une vue incohérente si un tick
/// s'intercalait. Ce sont des records : l'égalité structurelle permet aux vues de ne se rafraîchir
/// que si quelque chose a réellement changé.
/// </summary>
public sealed record HudSnapshot(
    bool IsPaused,
    int ActiveSpeed,
    long Gold,
    long BankSeconds,
    int Zone,
    int HighestZone,
    bool AutoAdvance,
    int HeroLevel,
    long HeroXp,
    long HeroXpToNext, // 0 au niveau maximum
    bool SoundAvailable,
    bool SoundEnabled)
{
    public static readonly HudSnapshot Empty = new(false, 1, 0, 0, 1, 1, true, 0, 0, 0, false, false);
}
