using HeroesOfIdlestan.Core.Controller.Meta;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller.Content;

/// <summary>Statistiques d'un combattant, déduites de sa classe et de son niveau, ou de son espèce et de la zone.</summary>
public sealed record FighterStats(long MaxHealth, long Damage, long CooldownTicks);

/// <summary>
/// Une action de combat : portée minimale et maximale (GDD §4.3). Les lignes depuis lesquelles elle
/// s'utilise sont décidées par <see cref="GameContent.HeroAction"/> et <see cref="GameContent.MonsterAction"/>.
/// </summary>
public sealed record CombatAction(int MinRange, int MaxRange)
{
    /// <summary>Corps à corps : depuis la ligne 1, sur la ligne 1 adverse.</summary>
    public static readonly CombatAction Melee = new(1, 1);
}

/// <summary>
/// Les données du jeu : classes, monstres, récompenses, succès. Les valeurs d'équilibrage sont
/// provisoires (GDD §8, « Formules d'équilibrage ») : un guerrier seul plafonne vers la zone 17 au
/// niveau 20, ce qui laisse la place au recrutement et au prestige.
/// </summary>
public static class GameContent
{
    /// <summary>Niveau maximum d'une classe (GDD §5.4).</summary>
    public const int MaxLevel = 20;

    /// <summary>Durée du repos entre deux combats.</summary>
    public const long RestTicks = 3 * GameClock.TicksPerSecond / 2;

    /// <summary>Modificateur de l'or gagné (charrette, talents…).</summary>
    public const string GoldGainModifier = "gold_gain";

    /// <summary>Modificateur de l'XP gagnée.</summary>
    public const string XpGainModifier = "xp_gain";

    // ── Personnages ──────────────────────────────────────────────────────

    public static FighterStats HeroStats(HeroClass heroClass, int level) => heroClass switch
    {
        HeroClass.Warrior => new FighterStats(100 + 20 * (level - 1), 10 + 3 * (level - 1), GameClock.TicksPerSecond),
        _ => throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, null),
    };

    /// <summary>Action d'une classe selon sa ligne (GDD §5.2) ; null : le personnage ne fait rien.</summary>
    public static CombatAction? HeroAction(HeroClass heroClass, int line) => heroClass switch
    {
        HeroClass.Warrior => line == 1 ? CombatAction.Melee : null,
        _ => null,
    };

    /// <summary>XP à accumuler au niveau <paramref name="level"/> pour passer au suivant.</summary>
    public static long XpToNextLevel(int level) => (long)Math.Round(40 * Math.Pow(1.3, level - 1));

    // ── Monstres ─────────────────────────────────────────────────────────

    public static FighterStats MonsterStats(MonsterSpecies species, int zone) => species switch
    {
        MonsterSpecies.Rat => new FighterStats(
            (long)Math.Round(30 * Math.Pow(1.18, zone - 1)),
            (long)Math.Round(6 * Math.Pow(1.15, zone - 1)),
            GameClock.TicksPerSecond * 6 / 5),
        _ => throw new ArgumentOutOfRangeException(nameof(species), species, null),
    };

    public static CombatAction? MonsterAction(MonsterSpecies species, int line) => species switch
    {
        MonsterSpecies.Rat => line == 1 ? CombatAction.Melee : null,
        _ => null,
    };

    /// <summary>Le groupe de monstres d'une zone, rangé ligne par ligne et de haut en bas.</summary>
    public static IEnumerable<MonsterEntity> MonsterGroup(int zone)
    {
        yield return new MonsterEntity { Species = MonsterSpecies.Rat, Line = 1 };
    }

    // ── Récompenses ──────────────────────────────────────────────────────

    public static long GoldReward(int zone) => (long)Math.Round(3 * Math.Pow(1.12, zone - 1));

    public static long XpReward(int zone) => (long)Math.Round(10 * Math.Pow(1.15, zone - 1));

    // ── Succès ───────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<AchievementDefinition> Achievements =
    [
        new("first_victory", s => s.Run.HighestZone >= 2),
        new("zone_10", s => s.Run.HighestZone >= 10),
        new("max_level", s => s.Heroes.Any(h => h.Level >= MaxLevel)),
    ];
}
