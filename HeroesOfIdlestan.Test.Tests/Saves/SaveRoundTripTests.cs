using HeroesOfIdlestan.Core.Controller;
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
        c.SimulateTicks(30_000);
        Assert.True(c.State.Run.HighestZone > 1);
        Assert.True(c.State.Heroes[0].Level > 1);

        var reloaded = new GameController();
        reloaded.Load(SaveFixtures.AssertRoundTrip(c));

        Assert.Equal<long>(c.State.Gold, reloaded.State.Gold);
        Assert.Equal(c.State.Clock.CurrentTick, reloaded.State.Clock.CurrentTick);
        Assert.Equal(c.State.Prng.State, reloaded.State.Prng.State);
        Assert.Equal(c.State.Run.Zone, reloaded.State.Run.Zone);
        Assert.Equal(c.State.Run.Phase, reloaded.State.Run.Phase);
        Assert.Equal(c.State.Heroes[0].Level, reloaded.State.Heroes[0].Level);
        Assert.Equal<long>(c.State.Heroes[0].Xp, reloaded.State.Heroes[0].Xp);
        Assert.Equal(c.State.Monsters.Count, reloaded.State.Monsters.Count);
        Assert.Equal(c.State.CompletedAchievements, reloaded.State.CompletedAchievements);
    }

    [Fact]
    public void ReloadedGame_ContinuesExactlyLikeTheOriginal()
    {
        // Le déterminisme survit à la sauvegarde : PRNG, phase du run et prochaines actions sont persistés.
        var original = new GameController();
        original.NewGame(seed: 99);
        original.SimulateTicks(5_000);

        var reloaded = new GameController();
        reloaded.Load(original.Export());

        foreach (var c in new[] { original, reloaded })
            for (int i = 0; i < 50; i++) { c.SimulateTicks(250); c.SetAutoAdvance(i % 10 != 0); }

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
