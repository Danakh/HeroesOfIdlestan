using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Test.Tests.CoreTests;

/// <summary>Géométrie de la formation et portées (GDD §3.4, §4.3, §4.4).</summary>
public class FormationTests
{
    private static List<Entity> Line(int count) =>
        Enumerable.Range(0, count).Select(_ => (Entity)new HeroEntity { Health = 1 }).ToList();

    private static List<Entity> Monsters(int count) =>
        Enumerable.Range(0, count).Select(_ => (Entity)new MonsterEntity { Health = 1 }).ToList();

    [Fact]
    public void HalfPositions_AreCentered()
    {
        Assert.Equal([-4, -2, 0, 2, 4], Enumerable.Range(0, 5).Select(i => Formation.HalfPosition(i, 5)));
        Assert.Equal([-1, 1], Enumerable.Range(0, 2).Select(i => Formation.HalfPosition(i, 2)));
    }

    /// <summary>L'exemple du GDD §4.3 : un monstre seul contre une ligne 1 de cinq personnages.</summary>
    [Fact]
    public void Distance_IsNotReciprocal()
    {
        var team = Line(5); // A B C D E
        var monster = Monsters(1);
        var m = monster[0];

        Assert.Equal(1, Formation.Distance(team[4], team, m, monster)); // E → M
        Assert.Equal(1, Formation.Distance(m, monster, team[2], team)); // M → C, en face
        Assert.Equal(2, Formation.Distance(m, monster, team[1], team)); // M → B
        Assert.Equal(2, Formation.Distance(m, monster, team[3], team)); // M → D
        Assert.Equal(3, Formation.Distance(m, monster, team[0], team)); // M → A
        Assert.Equal(3, Formation.Distance(m, monster, team[4], team)); // M → E
    }

    [Fact]
    public void Distance_GrowsWithBothLines()
    {
        var team = Line(1);
        var monsters = Monsters(2);
        monsters[1].Line = 2;
        team[0].Line = 2;
        Assert.Equal(2, Formation.Distance(team[0], team, monsters[0], monsters));
        Assert.Equal(3, Formation.Distance(team[0], team, monsters[1], monsters));
    }

    [Fact]
    public void Melee_CannotReachTheEndsOfAWideLine()
    {
        var team = Line(5);
        var monster = Monsters(1);
        Assert.Same(team[2], Formation.ChooseTarget(monster[0], monster, team, 1, 1));

        team[2].Health = 0; // C meurt : la ligne se resserre en A B D E, B et D passent en face
        Assert.Same(team[1], Formation.ChooseTarget(monster[0], monster, team, 1, 1)); // de haut en bas
    }

    [Fact]
    public void Targeting_PrefersTheFarthestReachableLine()
    {
        var archer = Line(1);
        archer[0].Line = 2;
        var monsters = Monsters(2);
        monsters[1].Line = 2;
        // Portée 2-3 depuis la ligne 2 : ligne 1 adverse à 2, ligne 2 à 3 → la ligne 2 (GDD §4.4).
        Assert.Same(monsters[1], Formation.ChooseTarget(archer[0], archer, monsters, 2, 3));
    }

    [Fact]
    public void NobodyInRange_GivesNoTarget()
    {
        var team = Line(1);
        team[0].Line = 2;
        Assert.Null(Formation.ChooseTarget(team[0], team, Monsters(1), 1, 1));
    }
}

/// <summary>Mort d'un personnage : resserrement, avancée et cascade (GDD §3.2).</summary>
public class FormationDeathTests
{
    /// <summary>Une pyramide, ligne par ligne ; chacun préfère sa ligne de départ.</summary>
    private static List<Entity> Pyramid(params int[] counts) =>
        counts.SelectMany((count, line) => Enumerable.Range(0, count)
                .Select(_ => (Entity)new HeroEntity { Health = 1, Line = line + 1, PreferredLine = line + 1 }))
            .ToList();

    private static int[] Shape(List<Entity> side)
    {
        var lines = side.Where(f => f.IsAlive).Select(f => f.Line).ToList();
        return Enumerable.Range(1, lines.DefaultIfEmpty(0).Max()).Select(l => lines.Count(x => x == l)).ToArray();
    }

    private static void Kill(List<Entity> side, Entity fallen, List<FormationHole>? holes = null, long tick = 0)
    {
        holes ??= [];
        fallen.Health = 0;
        Formation.Fall(side, holes, fallen);
        Formation.Settle(side, holes, tick);
    }

    /// <summary>Les places d'une ligne : l'indice de chaque combattant donné, ou −1 pour un trou.</summary>
    private static int[] Layout(List<Entity> side, List<FormationHole> holes, int line) =>
        Formation.Slots(side, holes, line).Select(f => f == null ? -1 : side.IndexOf(f)).ToArray();

    [Fact]
    public void TheNeighbours_DoNotMove_WhenSomeoneAdvances()
    {
        var team = Pyramid(3, 2);       // A B C / D E
        var holes = new List<FormationHole>();
        Kill(team, team[0], holes);     // A meurt, D avance à sa place : B et C ne bougent pas
        Assert.Equal([3, 1, 2], Layout(team, holes, 1));
        Assert.Equal([4], Layout(team, holes, 2)); // derrière, personne n'a à avancer : E se resserre
        Assert.Empty(holes);
    }

    [Fact]
    public void TheHole_WaitsForAWalker_AndKeepsItsPlace()
    {
        var team = Pyramid(3, 2, 1);    // A B C / D E / F
        var holes = new List<FormationHole>();
        team[3].ArrivalTick = 10;       // D, seul derrière A, marche encore
        Kill(team, team[0], holes, tick: 5);
        Assert.Equal([-1, 1, 2], Layout(team, holes, 1)); // la ligne ne se resserre pas
        Assert.Equal(2, team[3].Line);

        team[3].ArrivalTick = 0;        // D arrive
        Assert.Equal([team[3], team[5]], Formation.Settle(team, holes, tick: 10));
        Assert.Equal([3, 1, 2], Layout(team, holes, 1)); // D prend la place de A
        Assert.Equal([5, 4], Layout(team, holes, 2));    // F prend celle de D
        Assert.Equal([3, 2], Shape(team));
        Assert.Empty(holes);
    }

    [Fact]
    public void TheHole_CountsInTheDistances()
    {
        var team = Pyramid(3, 2);       // A B C / D E
        var holes = new List<FormationHole>();
        team[3].ArrivalTick = 10;
        Kill(team, team[0], holes, tick: 5);
        var monster = new List<Entity> { new MonsterEntity { Health = 1 } };
        // Le monstre fait face au centre (B) ; C est à une place de lui, trou ou pas.
        Assert.Equal(2, Formation.Distance(monster[0], monster, team[2], team, targetHoles: holes));
        Assert.Equal(1, Formation.Distance(monster[0], monster, team[1], team, targetHoles: holes));
    }

    [Fact]
    public void ValidAfterTightening_NobodyAdvances()
    {
        var team = Pyramid(4, 2, 1);
        Kill(team, team[0]);
        Assert.Equal([3, 2, 1], Shape(team));
    }

    /// <summary>L'exemple du GDD : 3-2-1 → 2-2-1 → 3-1-1 → 3-2.</summary>
    [Fact]
    public void Cascade_RunsLineByLine()
    {
        var team = Pyramid(3, 2, 1);
        Kill(team, team[1]);
        Assert.Equal([3, 2], Shape(team));
    }

    [Fact]
    public void TheAdvancer_FillsTheHole_FromBehindIt()
    {
        var team = Pyramid(3, 2);       // A B C / D E
        var (a, d, e) = (team[0], team[3], team[4]);
        Kill(team, a);                  // trou en haut : seul D est derrière
        Assert.Equal(1, d.Line);
        Assert.Equal(2, e.Line);
        Assert.Same(d, Formation.LineOf(team, 1)[0]);
    }

    [Fact]
    public void BetweenTwo_AffinityWins_ThenTopToBottom()
    {
        var team = Pyramid(3, 2);       // A B C / D E, trou au centre : D et E derrière
        var (d, e) = (team[3], team[4]);
        Kill(team, team[1]);
        Assert.Equal(1, d.Line);        // égalité : le plus haut
        Assert.Same(d, Formation.LineOf(team, 1)[1]);

        team = Pyramid(3, 2);
        (d, e) = (team[3], team[4]);
        e.PreferredLine = 1;
        Kill(team, team[1]);
        Assert.Equal(1, e.Line);        // affinité
        Assert.Equal(2, d.Line);
    }

    [Fact]
    public void AnInvalidFormation_IsSettledWithoutAHole()
    {
        var team = Pyramid(2, 2, 1);
        Formation.Settle(team, []);
        Assert.Equal([3, 2], Shape(team));
    }
}

/// <summary>La boucle courte : combat, repos, zone suivante ; défaite et recul (GDD §2).</summary>
public class CombatLoopTests
{
    private static GameController NewGame()
    {
        var c = new GameController();
        c.NewGame(seed: 1);
        return c;
    }

    private static void PlayUntil(GameController c, Func<GameState, bool> condition, int maxSeconds = 3600)
    {
        for (int i = 0; i < maxSeconds && !condition(c.State); i++)
            c.SimulateTicks(GameClock.TicksPerSecond);
        Assert.True(condition(c.State), $"Condition non atteinte après {maxSeconds} s (zone {c.State.Run.Zone}).");
    }

    [Fact]
    public void NewGame_IsAWarriorAgainstARat_InZone1()
    {
        var c = NewGame();
        var warrior = Assert.Single(c.State.Heroes);
        Assert.Equal(HeroClass.Warrior, warrior.Class);
        Assert.Equal(1, warrior.Level);
        Assert.Equal(MonsterSpecies.Rat, Assert.Single(c.State.Monsters).Species);
        Assert.Equal(1, c.State.Run.Zone);
        Assert.Equal(RunPhase.Fighting, c.State.Run.Phase);
    }

    [Fact]
    public void Victory_PaysGoldAndXp_RestsThenAdvances()
    {
        var c = NewGame();
        PlayUntil(c, s => s.Run.Phase == RunPhase.Resting);

        Assert.Equal(FightOutcome.Victory, c.State.Run.LastOutcome);
        Assert.Empty(c.State.Monsters);
        Assert.Equal<long>(GameContent.GoldReward(1), c.State.Gold);
        Assert.Equal<long>(GameContent.XpReward(1), c.State.Heroes[0].Xp);
        Assert.Equal(1, c.State.Run.Zone);

        c.SimulateTicks(GameContent.RestTicks);
        Assert.Equal(RunPhase.Fighting, c.State.Run.Phase);
        Assert.Equal(2, c.State.Run.Zone);
        Assert.Equal<long>(GameContent.HeroStats(HeroClass.Warrior, c.State.Heroes[0].Level).MaxHealth, c.State.Heroes[0].Health);
        Assert.Single(c.State.Monsters);
    }

    [Fact]
    public void WithoutAutoAdvance_TheTeamFarmsItsZone()
    {
        var c = NewGame();
        c.SetAutoAdvance(false);
        c.SimulateTicks(60 * GameClock.TicksPerSecond);
        Assert.Equal(1, c.State.Run.Zone);
        Assert.True(c.State.Gold > GameContent.GoldReward(1) * 5);
    }

    [Fact]
    public void Defeat_DisablesAutoAdvance_AndRetreatsOneZone()
    {
        var c = NewGame();
        PlayUntil(c, s => s.Run.LastOutcome == FightOutcome.Defeat);
        int lostZone = c.State.Run.Zone;
        Assert.False(c.State.Run.AutoAdvance);
        Assert.False(c.State.Heroes[0].IsAlive);

        c.SimulateTicks(GameContent.RestTicks);
        Assert.Equal(lostZone - 1, c.State.Run.Zone);
        Assert.True(c.State.Heroes[0].IsAlive);
        Assert.Equal(RunPhase.Fighting, c.State.Run.Phase);
    }

    [Fact]
    public void Defeat_InZone1_StaysInZone1()
    {
        var c = NewGame();
        c.State.Heroes[0].Health = 1;
        PlayUntil(c, s => s.Run.LastOutcome == FightOutcome.Defeat);
        c.SimulateTicks(GameContent.RestTicks);
        Assert.Equal(1, c.State.Run.Zone);
    }

    [Fact]
    public void BackLines_DoNothing_UntilTheyAdvance()
    {
        var c = new GameController();
        c.NewDebugGame(seed: 1);
        c.SimulateTicks(5 * GameClock.TicksPerSecond);

        Assert.All(c.State.Heroes.Where(h => h.Line == 1), h => Assert.NotNull(h.LastActionTick));
        Assert.All(c.State.Heroes.Where(h => h.Line > 1), h => Assert.Null(h.LastActionTick));
    }

    [Fact]
    public void Deaths_KeepBothSidesInAPyramid()
    {
        var c = new GameController();
        c.NewDebugGame(seed: 1);
        bool advanced = false;
        for (int i = 0; i < 4 * 90; i++)
        {
            c.SimulateTicks(GameClock.TicksPerSecond / 4);
            if (c.State.Run.Phase != RunPhase.Fighting) continue;
            advanced |= c.State.Heroes.Any(h => h.IsAlive && h.Line < h.FormationLine);
            // Une ligne peut attendre qu'un marcheur arrive pour se compléter.
            long now = c.State.Clock.CurrentTick;
            if (!c.State.Heroes.Any(h => h.IsAlive && h.ArrivalTick > now)) AssertPyramid(c.State.Heroes);
            if (!c.State.Monsters.Any(m => m.IsAlive && m.ArrivalTick > now)) AssertPyramid(c.State.Monsters);
        }
        Assert.True(advanced, "Aucun héros n'a avancé.");
    }

    [Fact]
    public void AnAdvancer_MovesOneLinePerWalk()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var c = new GameController();
            c.NewDebugGame(seed: seed);
            var lines = new Dictionary<Entity, int>();
            var moved = new Dictionary<Entity, long>();
            for (int i = 0; i < 120 * GameClock.TicksPerSecond; i++)
            {
                c.SimulateTicks(1);
                if (c.State.Run.Phase != RunPhase.Fighting) { lines.Clear(); moved.Clear(); continue; }
                foreach (var f in c.State.Heroes.Cast<Entity>().Concat(c.State.Monsters))
                {
                    if (!f.IsAlive) continue;
                    if (lines.TryGetValue(f, out int line) && line != f.Line)
                    {
                        long now = c.State.Clock.CurrentTick;
                        Assert.True(line - f.Line == 1, $"seed {seed} : {line} -> {f.Line} d'un coup.");
                        if (moved.TryGetValue(f, out long last))
                            Assert.True(now - last >= GameContent.MoveTicks, $"seed {seed} : deux lignes en {now - last} ticks.");
                        moved[f] = now;
                    }
                    lines[f] = f.Line;
                }
            }
        }
    }

    [Fact]
    public void AnAdvancer_WalksBeforeActing()
    {
        var c = new GameController();
        c.NewDebugGame(seed: 1);
        var lines = c.State.Heroes.ToDictionary(h => h, h => h.Line);
        var arrivals = new Dictionary<HeroEntity, long>();
        for (int i = 0; i < 90 * GameClock.TicksPerSecond && arrivals.Count < 3; i++)
        {
            c.SimulateTicks(1);
            foreach (var hero in c.State.Heroes)
            {
                if (hero.Line == lines[hero]) continue;
                lines[hero] = hero.Line;
                arrivals[hero] = c.State.Clock.CurrentTick + GameContent.MoveTicks;
            }
            foreach (var (hero, arrival) in arrivals)
                if (c.State.Clock.CurrentTick < arrival && hero.LastActionTick is { } last)
                    Assert.True(last < arrival - GameContent.MoveTicks, "Il a frappé en marchant.");
        }
        Assert.NotEmpty(arrivals);
    }

    [Fact]
    public void Defeat_RestoresThePlayersFormation()
    {
        var c = new GameController();
        c.NewDebugGame(seed: 1);
        PlayUntil(c, s => s.Run.LastOutcome == FightOutcome.Defeat);
        c.SimulateTicks(GameContent.RestTicks);

        Assert.All(c.State.Heroes, h => Assert.Equal(h.FormationLine, h.Line));
        Assert.All(c.State.Heroes, h => Assert.True(h.IsAlive));
    }

    private static void AssertPyramid(IEnumerable<Entity> side)
    {
        var lines = side.Where(f => f.IsAlive).Select(f => f.Line).ToList();
        if (lines.Count == 0) return;
        var counts = Enumerable.Range(1, lines.Max()).Select(l => lines.Count(x => x == l)).ToList();
        for (int i = 1; i < counts.Count; i++)
            Assert.True(counts[i - 1] > counts[i], $"Pyramide invalide : {string.Join("-", counts)}");
    }

    [Fact]
    public void Leveling_StopsAtTheMaxLevel()
    {
        var c = NewGame();
        var warrior = c.State.Heroes[0];
        warrior.Level = GameContent.MaxLevel - 1;
        warrior.Xp = GameContent.XpToNextLevel(warrior.Level) - 1;
        warrior.Health = GameContent.HeroStats(warrior.Class, warrior.Level).MaxHealth;
        c.SetAutoAdvance(false);

        c.SimulateTicks(60 * GameClock.TicksPerSecond);

        Assert.Equal(GameContent.MaxLevel, warrior.Level);
        Assert.Equal<long>(0, warrior.Xp);
        Assert.Contains("max_level", c.State.CompletedAchievements);
    }
}
