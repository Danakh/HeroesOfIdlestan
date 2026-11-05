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

    /// <summary>Lancer de couteaux du Voleur : portée 2, depuis la ligne 2.</summary>
    public static readonly CombatAction Knives = new(2, 2);

    /// <summary>Sort mono-cible du Mage : portée 2-3, depuis les lignes 2 et 3.</summary>
    public static readonly CombatAction Spell = new(2, 3);
}

/// <summary>
/// Les données du jeu : classes, monstres, récompenses, succès. Les valeurs d'équilibrage sont
/// provisoires (GDD §8, « Formules d'équilibrage ») : un guerrier seul plafonne vers la zone 15 au
/// niveau 20, ce qui laisse la place au recrutement et au prestige.
/// </summary>
public static class GameContent
{
    /// <summary>Niveau maximum d'une classe (GDD §5.4).</summary>
    public const int MaxLevel = 20;

    /// <summary>
    /// Durée d'un déplacement dans la formation (avancée, resserrement, retour en place) : environ une
    /// seconde de marche. Celui qui avance n'agit pas avant d'être arrivé.
    /// </summary>
    public const long MoveTicks = GameClock.TicksPerSecond;

    /// <summary>Durée du repos entre deux combats.</summary>
    public const long RestTicks = 3 * GameClock.TicksPerSecond / 2;

    /// <summary>Modificateur de l'or gagné (charrette, talents…).</summary>
    public const string GoldGainModifier = "gold_gain";

    /// <summary>Modificateur de l'XP gagnée.</summary>
    public const string XpGainModifier = "xp_gain";

    // ── Personnages ──────────────────────────────────────────────────────

    /// <summary>
    /// Résistance (GDD §5.1) : le Guerrier encaisse, le Voleur moins, le Mage peu. Le Mage frappe
    /// fort et lentement, le Voleur peu et vite.
    /// </summary>
    public static FighterStats HeroStats(HeroClass heroClass, int level) => heroClass switch
    {
        HeroClass.Warrior => new FighterStats(100 + 20 * (level - 1), 10 + 3 * (level - 1), GameClock.TicksPerSecond),
        HeroClass.Rogue => new FighterStats(75 + 14 * (level - 1), 7 + 2 * (level - 1), GameClock.TicksPerSecond * 7 / 10),
        HeroClass.Mage => new FighterStats(55 + 9 * (level - 1), 15 + 5 * (level - 1), GameClock.TicksPerSecond * 3 / 2),
        _ => throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, null),
    };

    /// <summary>
    /// Action d'une classe selon sa ligne (GDD §5.2) ; null : le personnage ne fait rien. Le Mage
    /// n'a pour l'instant que son sort mono-cible, en lignes 2 et 3 (les rituels viendront).
    /// </summary>
    public static CombatAction? HeroAction(HeroClass heroClass, int line) => heroClass switch
    {
        HeroClass.Warrior => line == 1 ? CombatAction.Melee : null,
        HeroClass.Rogue => line switch { 1 => CombatAction.Melee, 2 => CombatAction.Knives, _ => null },
        HeroClass.Mage => line is 2 or 3 ? CombatAction.Spell : null,
        _ => null,
    };

    /// <summary>Les classes proposées au recrutement, dans l'ordre des boutons.</summary>
    public static readonly IReadOnlyList<HeroClass> RecruitableClasses = [HeroClass.Warrior, HeroClass.Rogue, HeroClass.Mage];

    /// <summary>
    /// Prix d'une recrue (GDD §6.4) : ne dépend que de la taille de l'équipe, et croît de façon
    /// exponentielle. Valeurs provisoires.
    /// </summary>
    public static long RecruitCost(int teamSize) => (long)Math.Round(20 * Math.Pow(2.2, Math.Max(0, teamSize - 1)));

    /// <summary>XP à accumuler au niveau <paramref name="level"/> pour passer au suivant.</summary>
    public static long XpToNextLevel(int level) => (long)Math.Round(40 * Math.Pow(1.3, level - 1));

    // ── Monstres ─────────────────────────────────────────────────────────

    public static FighterStats MonsterStats(MonsterSpecies species, int zone) => species switch
    {
        MonsterSpecies.Rat => new FighterStats(
            (long)Math.Round(30 * Math.Pow(1.18, zone - 1)),
            (long)Math.Round(6 * Math.Pow(1.15, zone - 1)),
            GameClock.TicksPerSecond * 6 / 5),
        MonsterSpecies.Goblin => new FighterStats(
            (long)Math.Round(50 * Math.Pow(1.18, zone - 1)),
            (long)Math.Round(8 * Math.Pow(1.15, zone - 1)),
            GameClock.TicksPerSecond),
        _ => throw new ArgumentOutOfRangeException(nameof(species), species, null),
    };

    /// <summary>
    /// Action d'un monstre selon sa ligne. Le rat ne se bat qu'au corps à corps ; le gobelin se bat
    /// comme un voleur : corps à corps en ligne 1, couteaux à portée 2 en ligne 2.
    /// </summary>
    public static CombatAction? MonsterAction(MonsterSpecies species, int line) => species switch
    {
        MonsterSpecies.Rat => line == 1 ? CombatAction.Melee : null,
        MonsterSpecies.Goblin => line switch { 1 => CombatAction.Melee, 2 => CombatAction.Knives, _ => null },
        _ => null,
    };

    /// <summary>Zones par monstre supplémentaire : 1 monstre en zones 1-10, 2 en 11-20, etc.</summary>
    public const int ZonesPerExtraMonster = 10;

    /// <summary>Nombre de monstres du groupe d'une zone.</summary>
    public static int MonsterCount(int zone) => 1 + (Math.Max(1, zone) - 1) / ZonesPerExtraMonster;

    /// <summary>
    /// Le groupe de monstres d'une zone, rangé ligne par ligne et de haut en bas : des rats en
    /// ligne 1, des gobelins en ligne 2, toujours moins nombreux pour garder la pyramide
    /// (1, 2, 2-1, 3-1, 3-2, 4-2…).
    /// </summary>
    public static IEnumerable<MonsterEntity> MonsterGroup(int zone)
    {
        int count = MonsterCount(zone);
        int goblins = (count - 1) / 2;
        for (int i = 0; i < count - goblins; i++)
            yield return new MonsterEntity { Species = MonsterSpecies.Rat, Line = 1, PreferredLine = 1 };
        for (int i = 0; i < goblins; i++)
            yield return new MonsterEntity { Species = MonsterSpecies.Goblin, Line = 2, PreferredLine = 2 };
    }

    // ── Partie de debug ──────────────────────────────────────────────────

    /// <summary>
    /// Formation des deux camps d'une partie de debug, ligne par ligne : une pyramide 5-4-3-2-1 de
    /// 15 combattants. Les guerriers ne frappent qu'en ligne 1 ; les gobelins aussi depuis la ligne 2
    /// (couteaux). Les autres attendent qu'une mort les fasse avancer (GDD §3.2).
    /// </summary>
    public static readonly IReadOnlyList<int> DebugFormation = [5, 4, 3, 2, 1];

    /// <summary>Nombre de combattants de chaque camp d'une partie de debug.</summary>
    public static int DebugFighterCount => DebugFormation.Sum();

    /// <summary>Les lignes d'une formation, une par combattant, de la ligne 1 à la dernière.</summary>
    public static IEnumerable<int> LinesOf(IReadOnlyList<int> formation)
    {
        for (int line = 0; line < formation.Count; line++)
            for (int i = 0; i < formation[line]; i++)
                yield return line + 1;
    }

    /// <summary>Le groupe de chaque zone d'une partie de debug : des gobelins en <see cref="DebugFormation"/>.</summary>
    public static IEnumerable<MonsterEntity> DebugMonsterGroup() =>
        LinesOf(DebugFormation).Select(line =>
            new MonsterEntity { Species = MonsterSpecies.Goblin, Line = line, PreferredLine = line });

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
