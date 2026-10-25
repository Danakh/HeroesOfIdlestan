using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Modifiers;
using HeroesOfIdlestan.Core.Controller.Simulation;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Obfuscation;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Test.Tests.CoreTests;

public class GameClockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Advance_ConvertsRealTimeToTicks()
    {
        var clock = new GameClock();
        clock.Advance(T0);
        long gained = 0;
        for (int frame = 1; frame <= 60; frame++)
            gained += clock.Advance(T0.AddMilliseconds(frame * 1000.0 / 60));
        Assert.Equal(100, gained);
    }

    [Fact]
    public void Advance_CapsASingleCallAt100Ms()
    {
        // Une reprise après un gel ne doit pas faire sauter la partie : le surplus est perdu.
        var clock = new GameClock();
        clock.Advance(T0);
        Assert.Equal(10, clock.Advance(T0.AddSeconds(5)));
    }

    [Fact]
    public void Pause_SendsRealTimeToTheBank()
    {
        var clock = new GameClock();
        clock.Pause();
        clock.Advance(T0);
        clock.Advance(T0.AddMilliseconds(50));
        Assert.Equal(0, clock.CurrentTick);
        Assert.Equal(5, clock.OfflineBankTicks);
    }

    [Fact]
    public void Speed_SpendsTheBank()
    {
        var clock = new GameClock { OfflineBankTicks = 1000 };
        clock.SetSpeed(3);
        clock.Advance(T0);
        long gained = clock.Advance(T0.AddMilliseconds(50));
        Assert.Equal(15, gained);
        Assert.Equal(990, clock.OfflineBankTicks);
    }

    [Fact]
    public void CreditOfflineTime_UsesLastSaveTime()
    {
        var clock = new GameClock { LastSaveTime = T0 };
        clock.CreditOfflineTime(T0.AddMinutes(1));
        Assert.Equal(6000, clock.OfflineBankTicks);
    }
}

public class TickCooldownTests
{
    [Fact]
    public void CountsEveryElapsedCycle_AndKeepsTheRemainder()
    {
        long last = 0;
        Assert.Equal(3, TickCooldown.ConsumeElapsedCycles(350, ref last, 100));
        Assert.Equal(300, last);
        Assert.Equal(0, TickCooldown.ConsumeElapsedCycles(399, ref last, 100));
        Assert.Equal(1, TickCooldown.ConsumeElapsedCycles(400, ref last, 100));
    }

    [Fact]
    public void CombatIsIndependentOfChunkSize()
    {
        // Un saut de temps en une seule tranche doit donner exactement la même partie que le temps réel.
        var small = new GameController();
        small.NewGame(seed: 5);
        small.SimulateTicks(60_000, chunkTicks: 1);

        var large = new GameController();
        large.NewGame(seed: 5);
        large.SimulateTicks(60_000, chunkTicks: 60_000);

        Assert.True(small.State.Run.HighestZone > 1);
        // Les succès sont évalués en fin de passage : leur événement porte le tick de fin de tranche.
        // Seul le journal en dépend ; on compare tout le reste.
        small.State.Events.Entries.Clear();
        large.State.Events.Entries.Clear();
        Assert.Equal(SaveCodec.Decode(small.Export()), SaveCodec.Decode(large.Export()));
    }
}

public class ObfuscationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-123456)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ObfInt_KeepsItsValue(int value) => Assert.Equal(value, ObfInt.From(value).Value);

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void ObfLong_KeepsItsValue(long value) => Assert.Equal(value, ObfLong.From(value).Value);

    [Fact]
    public void Default_IsZero() => Assert.Equal(0L, default(ObfLong).Value);
}

public class ModifierTests
{
    [Fact]
    public void Apply_CombinesAdditiveThenMultiplicative()
    {
        var agg = new ModifierAggregator();
        agg.Register(new StaticModifierProvider([
            new Modifier("production", ModifierType.Additive, 2),
            new Modifier("production", ModifierType.Multiplicative, 0.5),
            new Modifier("other", ModifierType.Additive, 100),
        ]));
        Assert.Equal((10 + 2) * 1.5, agg.Apply("production", 10));
    }

    [Fact]
    public void Replacer_Wins()
    {
        var agg = new ModifierAggregator();
        agg.Register(new StaticModifierProvider([
            new Modifier("cap", ModifierType.Additive, 5),
            new Modifier("cap", ModifierType.Replacer, 42),
        ]));
        Assert.Equal(42, agg.Apply("cap", 10));
    }

    [Fact]
    public void Register_IsIdempotent()
    {
        var agg = new ModifierAggregator();
        var provider = new StaticModifierProvider([new Modifier("x", ModifierType.Additive, 1)]);
        agg.Register(provider);
        agg.Register(provider);
        Assert.Equal(1, agg.Apply("x", 0));
    }
}

public class SimulationPipelineTests
{
    private sealed class Throwing : ISimulationSystem
    {
        public string Name => "Throwing";
        public void Run(SimulationContext context) => throw new InvalidOperationException("boom");
    }

    private sealed class Counting : ISimulationSystem
    {
        public int Runs;
        public string Name => "Counting";
        public void Run(SimulationContext context) => Runs++;
    }

    [Fact]
    public void AFailingSystem_DoesNotSkipTheNextOnes()
    {
        var counting = new Counting();
        var pipeline = new SimulationPipeline().Add(new Throwing()).Add(counting);

        pipeline.Run(new SimulationContext { State = new GameState(), Modifiers = new ModifierAggregator() });

        Assert.Equal(1, counting.Runs);
        Assert.Contains(GameLog.Snapshot(), e => e.Source == "Throwing");
    }

    [Fact]
    public void DuplicateNames_AreRefused() =>
        Assert.Throws<InvalidOperationException>(() => new SimulationPipeline().Add(new Counting()).Add(new Counting()));
}
