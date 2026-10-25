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
