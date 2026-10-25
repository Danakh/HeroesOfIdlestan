using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Test.Tests.TestUtilities;

namespace HeroesOfIdlestan.Test.Tests.Saves;

/// <summary>Niveau 1 — export → import → export, en mémoire, sans fixture.</summary>
public class SaveRoundTripTests
{
    [Fact]
    public void NewGame_RoundTrips()
    {
        var c = new GameController();
        c.NewGame(seed: 7);
        SaveFixtures.AssertRoundTrip(c);
    }

    [Fact]
    public void PlayedGame_RoundTrips_AndKeepsEveryValue()
    {
        var c = new GameController();
        c.NewGame(seed: 7);
        for (int i = 0; i < 300; i++)
        {
            c.SimulateTicks(100);
            c.TryBuyProducer();
        }
        Assert.True(c.State.Entities.Count > 1);

        var reloaded = new GameController();
        reloaded.Load(SaveFixtures.AssertRoundTrip(c));

        Assert.Equal<long>(c.State.Gold, reloaded.State.Gold);
        Assert.Equal(c.State.Clock.CurrentTick, reloaded.State.Clock.CurrentTick);
        Assert.Equal(c.State.Prng.State, reloaded.State.Prng.State);
        Assert.Equal(c.State.Entities.Count, reloaded.State.Entities.Count);
        Assert.All(reloaded.State.Entities, e => Assert.IsType<ProducerEntity>(e));
        Assert.Equal(c.State.CompletedAchievements, reloaded.State.CompletedAchievements);
    }

    [Fact]
    public void ReloadedGame_ContinuesExactlyLikeTheOriginal()
    {
        // Le déterminisme survit à la sauvegarde : PRNG et cooldowns sont persistés.
        var original = new GameController();
        original.NewGame(seed: 99);
        original.SimulateTicks(5_000);

        var reloaded = new GameController();
        reloaded.Load(original.Export());

        foreach (var c in new[] { original, reloaded })
            for (int i = 0; i < 50; i++) { c.SimulateTicks(250); c.TryBuyProducer(); }

        Assert.Equal(SaveCodec.Decode(original.Export()), SaveCodec.Decode(reloaded.Export()));
    }

    [Fact]
    public void Save_StampsCurrentVersion()
    {
        var c = new GameController();
        c.NewGame(seed: 1);
        var reloaded = new GameController();
        reloaded.Load(c.Export());
        Assert.Equal(GameVersion.Current, reloaded.State.SavedGameVersion);
    }
}
