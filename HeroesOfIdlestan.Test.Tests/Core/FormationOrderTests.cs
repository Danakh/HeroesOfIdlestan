using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Battle;
using HeroesOfIdlestan.Test.Tests.TestUtilities;

namespace HeroesOfIdlestan.Test.Tests.CoreTests;

/// <summary>Mage et Voleur (GDD §5.2) : leurs actions selon la ligne.</summary>
public class HeroClassTests
{
    [Fact]
    public void Mage_CastsFromLines2And3_AtRange2To3()
    {
        Assert.Null(GameContent.HeroAction(HeroClass.Mage, 1));
        Assert.Equal(new CombatAction(2, 3), GameContent.HeroAction(HeroClass.Mage, 2));
        Assert.Equal(new CombatAction(2, 3), GameContent.HeroAction(HeroClass.Mage, 3));
        Assert.Null(GameContent.HeroAction(HeroClass.Mage, 4));
    }

    [Fact]
    public void Rogue_FightsInMelee_ThenThrowsKnivesAtRange2()
    {
        Assert.Equal(CombatAction.Melee, GameContent.HeroAction(HeroClass.Rogue, 1));
        Assert.Equal(new CombatAction(2, 2), GameContent.HeroAction(HeroClass.Rogue, 2));
        Assert.Null(GameContent.HeroAction(HeroClass.Rogue, 3));
    }

    /// <summary>Depuis la ligne 2, le mage touche la ligne 1 adverse (distance 2) ; le voleur aussi.</summary>
    [Theory]
    [InlineData(HeroClass.Mage)]
    [InlineData(HeroClass.Rogue)]
    public void FromLine2_TheFrontMonsterIsInRange(HeroClass heroClass)
    {
        var front = new HeroEntity { Class = HeroClass.Warrior, Health = 1 };
        var back = new HeroEntity { Class = heroClass, Line = 2, Health = 1 };
        List<Entity> team = [front, back];
        List<Entity> monsters = [new MonsterEntity { Health = 1 }];
        var action = GameContent.HeroAction(heroClass, back.Line)!;
        Assert.Same(monsters[0], Formation.ChooseTarget(back, team, monsters, action.MinRange, action.MaxRange));
    }

    [Fact]
    public void AMage_InLine2_DamagesTheRat()
    {
        var c = FormationOrderTests.TeamOf(HeroClass.Mage, HeroClass.Warrior);
        // 2-1 : le mage recruté en ligne 1, le guerrier en ligne 2 ; on les échange.
        var mage = c.State.Heroes[1];
        var warrior = c.State.Heroes[2];
        Assert.True(c.OrderFormationMove(1, FormationOrders.DropTargets(c.State, 1).Single(t => t.SwapWith == 2)));
        FormationOrderTests.PlayUntil(c, s => mage.Line == 2 && mage.ArrivalTick == 0 && mage.LastActionTick != null);
        Assert.Equal(1, warrior.Line);
    }
}

/// <summary>Recrutement et glisser-déposer dans la formation.</summary>
public class FormationOrderTests
{
    /// <summary>Une partie avec le guerrier de départ, puis une recrue par classe donnée, et de l'or.</summary>
    internal static GameController TeamOf(params HeroClass[] recruits)
    {
        var c = new GameController();
        c.NewGame(seed: 1);
        c.State.Gold = 1_000_000;
        foreach (var heroClass in recruits) Assert.True(c.Recruit(heroClass));
        // Un rat increvable : le combat dure, et les ordres s'exécutent en combat.
        c.State.Monsters[0].Health = 1_000_000_000;
        return c;
    }

    internal static void PlayUntil(GameController c, Func<GameState, bool> condition, int maxTicks = 60 * GameClock.TicksPerSecond)
    {
        for (int i = 0; i < maxTicks && !condition(c.State); i++) c.SimulateTicks(1);
        Assert.True(condition(c.State), "Condition non atteinte.");
    }

    private static string Shape(GameState state) =>
        string.Join("-", Enumerable.Range(1, state.Heroes.Max(h => h.Line)).Select(l => state.Heroes.Count(h => h.IsAlive && h.Line == l)));

    // ── Recrutement ──────────────────────────────────────────────────────

    [Fact]
    public void Recruits_GoAsFarBackAsThePyramidAllows()
    {
        var c = TeamOf();
        var shapes = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            Assert.True(c.Recruit(HeroClass.Rogue));
            shapes.Add(Shape(c.State));
        }
        Assert.Equal(["2", "2-1", "3-1", "3-2", "3-2-1"], shapes);
        Assert.All(c.State.Heroes, h => Assert.Equal(h.Line, h.FormationLine));
    }

    [Fact]
    public void Recruiting_CostsGold_ThatGrowsWithTheTeam()
    {
        var c = new GameController();
        c.NewGame(seed: 1);
        long first = c.RecruitCost;
        c.State.Gold = first - 1;
        Assert.False(c.Recruit(HeroClass.Mage));
        Assert.Single(c.State.Heroes);

        c.State.Gold = first;
        Assert.True(c.Recruit(HeroClass.Mage));
        Assert.Equal<long>(0, c.State.Gold);
        Assert.Equal(HeroClass.Mage, c.State.Heroes[1].Class);
        Assert.True(c.RecruitCost > first);
        Assert.Contains(c.State.Events.Entries, e => e.Type == "hero_recruited");
    }

    [Fact]
    public void ARecruit_EntersTheFight_AtFullHealth()
    {
        var c = TeamOf(HeroClass.Mage);
        var mage = c.State.Heroes[1];
        Assert.Equal(GameContent.HeroStats(HeroClass.Mage, 1).MaxHealth, (long)mage.Health);
        Assert.Equal(c.State.Clock.CurrentTick + GameContent.HeroStats(HeroClass.Mage, 1).CooldownTicks, mage.NextActionTick);
    }

    // ── Places proposées ─────────────────────────────────────────────────

    [Fact]
    public void DropTargets_OfferSwaps_AndOnlyLineEndsThatKeepAPyramid()
    {
        var c = TeamOf(HeroClass.Rogue, HeroClass.Mage); // 2-1 : mage seul en ligne 2
        var front = FormationOrders.DropTargets(c.State, 0);
        Assert.Equal([1, 2], front.Select(t => t.SwapWith).OfType<int>().Order());
        Assert.DoesNotContain(front, t => t.SwapWith == null); // 1-2 ou 1-1-1 : invalides

        var back = FormationOrders.DropTargets(c.State, 2);
        var lineEnd = Assert.Single(back, t => t.SwapWith == null);
        Assert.Equal(1, lineEnd.Line); // 3 de front
        Assert.Equal((2, 2), (lineEnd.Index, lineEnd.Count)); // juste après les deux du front
    }

    [Fact]
    public void AnOrder_OnAPlaceNotOffered_IsRefused()
    {
        var c = TeamOf(HeroClass.Rogue, HeroClass.Mage);
        Assert.False(c.OrderFormationMove(0, new FormationTarget(null, 3, 0, 1)));
        Assert.False(c.OrderFormationMove(0, new FormationTarget(0, 1, 0, 2))); // sur lui-même
        Assert.Empty(c.State.FormationMoves);
    }

    // ── Exécution au prochain cycle ──────────────────────────────────────

    [Fact]
    public void ASwap_WaitsForTheNextCycle_ThenBothWalk()
    {
        var c = TeamOf(HeroClass.Mage, HeroClass.Warrior);
        var mage = c.State.Heroes[1];
        var back = c.State.Heroes[2];
        Assert.Equal((1, 2), (mage.Line, back.Line));

        Assert.True(c.OrderFormationMove(1, FormationOrders.DropTargets(c.State, 1).Single(t => t.SwapWith == 2)));
        long cycle = mage.NextActionTick;
        c.SimulateTicks(cycle - c.State.Clock.CurrentTick - 1, chunkTicks: 1);
        Assert.Equal((1, 2), (mage.Line, back.Line));
        Assert.Single(c.State.FormationMoves);

        c.SimulateTicks(1);
        Assert.Equal((2, 1), (mage.Line, back.Line));
        Assert.Equal((2, 1), (mage.FormationLine, back.FormationLine));
        Assert.Equal((2, 1), (mage.PreferredLine, back.PreferredLine));
        Assert.Empty(c.State.FormationMoves);
        Assert.Equal(cycle + GameContent.MoveTicks, mage.ArrivalTick);
        Assert.Equal(cycle + GameContent.MoveTicks, back.ArrivalTick);
        Assert.Null(mage.LastActionTick); // ce cycle a servi à se déplacer
    }

    [Fact]
    public void AMoveToALineEnd_ReshapesThePyramid()
    {
        var c = TeamOf(HeroClass.Rogue, HeroClass.Mage); // 2-1
        var mage = c.State.Heroes[2];
        Assert.True(c.OrderFormationMove(2, FormationOrders.DropTargets(c.State, 2).Single(t => t.SwapWith == null)));
        PlayUntil(c, s => s.FormationMoves.Count == 0);

        Assert.Equal("3", Shape(c.State));
        Assert.Equal(2, mage.Rank); // au bout de la ligne
        Assert.All(c.State.Heroes, h => Assert.Equal((h.Line, h.Rank), (h.FormationLine, h.FormationRank)));
    }

    [Fact]
    public void ANewOrder_ReplacesTheHerosPreviousOne_AndDroppingOnHimselfCancels()
    {
        var c = TeamOf(HeroClass.Rogue, HeroClass.Mage);
        var targets = FormationOrders.DropTargets(c.State, 2);
        Assert.True(c.OrderFormationMove(2, targets.First(t => t.SwapWith == 0)));
        Assert.True(c.OrderFormationMove(2, targets.First(t => t.SwapWith == 1)));
        Assert.Equal(1, Assert.Single(c.State.FormationMoves).SwapWith);

        c.CancelFormationMove(2);
        Assert.Empty(c.State.FormationMoves);
    }

    [Fact]
    public void AnOrder_IsForgotten_WhenItsHeroDies()
    {
        var c = TeamOf(HeroClass.Mage);
        // Le rat frappe (tick 120) avant le premier cycle du mage (tick 150) : en face, le guerrier.
        c.State.Heroes[0].Health = 1;
        Assert.True(c.OrderFormationMove(1, FormationOrders.DropTargets(c.State, 1).Single(t => t.SwapWith == 0)));
        PlayUntil(c, s => !s.Heroes[0].IsAlive);
        Assert.Empty(c.State.FormationMoves);
    }

    [Fact]
    public void Defeat_RestoresTheSwappedOrder()
    {
        var c = TeamOf(HeroClass.Mage); // 2 de front : guerrier, mage
        var warrior = c.State.Heroes[0];
        var mage = c.State.Heroes[1];
        Assert.True(c.OrderFormationMove(1, FormationOrders.DropTargets(c.State, 1).Single(t => t.SwapWith == 0)));
        PlayUntil(c, s => s.FormationMoves.Count == 0);
        Assert.True(mage.FormationRank < warrior.FormationRank);

        c.State.Run.Zone = 80; // un rat mortel
        CombatSystem.StartFight(c.State, c.State.Clock.CurrentTick);
        PlayUntil(c, s => s.Run.LastOutcome == FightOutcome.Defeat, maxTicks: 600 * GameClock.TicksPerSecond);
        c.SimulateTicks(GameContent.RestTicks);

        Assert.Equal([mage, warrior], Formation.LineOf(c.State.Heroes, 1).Cast<HeroEntity>());
    }

    [Fact]
    public void APendingOrder_SurvivesASave()
    {
        var c = TeamOf(HeroClass.Rogue, HeroClass.Mage);
        Assert.True(c.OrderFormationMove(2, FormationOrders.DropTargets(c.State, 2).First()));
        SaveFixtures.AssertRoundTrip(c);

        var reloaded = new GameController();
        reloaded.Load(c.Export());
        Assert.Single(reloaded.State.FormationMoves);
    }

    // ── Glisser-déposer (Presentation) ───────────────────────────────────

    [Fact]
    public void Drag_DropOnATarget_Orders_DropOnHimself_Cancels()
    {
        var c = TeamOf(HeroClass.Rogue, HeroClass.Mage);
        var drag = new FormationDrag();

        Assert.True(drag.Begin(c.State, 2, 0, 0));
        var lineEnd = drag.Targets.Single(t => t.SwapWith == null);
        drag.MoveTo(5, 5, lineEnd, self: false);
        drag.Drop(c);
        Assert.False(drag.Active);
        Assert.Equal(1, Assert.Single(c.State.FormationMoves).Line);

        Assert.True(drag.Begin(c.State, 2, 0, 0));
        drag.MoveTo(0, 0, null, self: true);
        drag.Drop(c);
        Assert.Empty(c.State.FormationMoves);
    }

    [Fact]
    public void Drag_StopsWhenTheHeroDies()
    {
        var c = TeamOf(HeroClass.Mage);
        var drag = new FormationDrag();
        Assert.True(drag.Begin(c.State, 1, 0, 0));
        c.State.Heroes[1].Health = 0;
        drag.Refresh(c.State);
        Assert.False(drag.Active);
    }

    [Fact]
    public void Nearest_PicksTheClosestWithinTheRadius()
    {
        var items = new[] { ("a", 0f, 0f), ("b", 10f, 0f) };
        Assert.Equal("b", FormationDrag.Nearest(items, 8, 0, 5));
        Assert.Null(FormationDrag.Nearest(items, 30, 0, 5));
    }
}

/// <summary>Les groupes de monstres : un de plus toutes les 10 zones, rats devant, gobelins derrière.</summary>
public class MonsterGroupTests
{
    [Theory]
    [InlineData(1, "Rat")]
    [InlineData(10, "Rat")]
    [InlineData(11, "Rat,Rat")]
    [InlineData(21, "Rat,Rat|Goblin")]
    [InlineData(31, "Rat,Rat,Rat|Goblin")]
    [InlineData(41, "Rat,Rat,Rat|Goblin,Goblin")]
    [InlineData(51, "Rat,Rat,Rat,Rat|Goblin,Goblin")]
    public void AMonsterJoins_EveryTenZones_RatsInFront_GoblinsBehind(int zone, string expected)
    {
        var group = GameContent.MonsterGroup(zone).ToList();
        string shape = string.Join("|", group.GroupBy(m => m.Line).OrderBy(g => g.Key)
            .Select(g => string.Join(",", g.Select(m => m.Species))));
        Assert.Equal(expected, shape);
        Assert.True(FormationOrders.IsPyramid(group.Select(m => m.Line)));
    }

    [Fact]
    public void Goblins_FightLikeRogues()
    {
        Assert.Equal(CombatAction.Melee, GameContent.MonsterAction(MonsterSpecies.Goblin, 1));
        Assert.Equal(CombatAction.Knives, GameContent.MonsterAction(MonsterSpecies.Goblin, 2));
        Assert.Null(GameContent.MonsterAction(MonsterSpecies.Goblin, 3));
        Assert.Null(GameContent.MonsterAction(MonsterSpecies.Rat, 2));
    }

    /// <summary>Un gobelin en ligne 2 lance ses couteaux sur la ligne 1 de l'équipe.</summary>
    [Fact]
    public void AGoblin_InLine2_HitsTheFrontHero()
    {
        var c = new GameController();
        c.NewGame(seed: 1);
        c.State.Run.Zone = 21;
        CombatSystem.StartFight(c.State, c.State.Clock.CurrentTick);
        var goblin = c.State.Monsters.Single(m => m.Species == MonsterSpecies.Goblin);
        Assert.Equal(2, goblin.Line);
        FormationOrderTests.PlayUntil(c, s => goblin.LastActionTick != null || !goblin.IsAlive);
        Assert.NotNull(goblin.LastActionTick);
    }
}

