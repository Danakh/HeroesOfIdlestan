using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Runtime;

namespace HeroesOfIdlestan.Test.Tests.PresentationTests;

/// <summary>La session telle que la voient les heads des deux piles : démarrage, sauvegarde, reprise.</summary>
public class GameSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Lancement, puis « Continuer » : ce que faisaient les heads avant le menu.</summary>
    private static GameSession Continued(IFileSystemService files, Func<DateTimeOffset> now)
    {
        var session = new GameSession(files, now: now);
        session.ContinueGame();
        return session;
    }

    [Fact]
    public void Launch_ShowsTheMenu_WithoutLoadingNorSaving()
    {
        var files = new InMemoryFileSystem();
        using (var session = new GameSession(files, now: () => T0))
        {
            Assert.True(session.InMenu);
            Assert.False(session.HasSavedGame);
            Assert.Empty(session.Game.State.Heroes);
            session.Tick();
        }
        Assert.Null(files.ReadText(GameSession.AutosaveFileName));
    }

    [Fact]
    public void Menu_OffersToContinue_OnceAGameIsSaved()
    {
        var files = new InMemoryFileSystem();
        using (var first = new GameSession(files, now: () => T0)) first.StartNewGame();

        using var second = new GameSession(files, now: () => T0);
        Assert.Equal(new MenuSnapshot(Visible: true, CanContinue: true), second.GetMenuSnapshot());
    }

    [Fact]
    public void DebugGame_PitsFifteenWarriorsAgainstFifteenGoblins_AndIsNeverSaved()
    {
        var files = new InMemoryFileSystem();
        using (var first = new GameSession(files, now: () => T0))
        {
            first.StartNewGame();
            first.Game.State.Gold = 42;
            first.SaveNow();
        }

        using var session = new GameSession(files, now: () => T0);
        session.StartDebugGame();
        Assert.False(session.InMenu);
        Assert.Equal(15, session.Game.State.Heroes.Count);
        Assert.Equal(15, session.Game.State.Monsters.Count);
        Assert.All(session.Game.State.Monsters, m => Assert.Equal(MonsterSpecies.Goblin, m.Species));

        session.Game.State.Gold = 9999;
        session.SaveNow();
        session.ReturnToMenu();
        session.ContinueGame();
        Assert.Equal<long>(42, session.Game.State.Gold);
        Assert.Single(session.Game.State.Heroes);
    }

    [Fact]
    public void DebugGame_NextZone_IsGoblinsAgain()
    {
        var c = new Core.Controller.GameController();
        c.NewDebugGame(seed: 1);
        c.SimulateTicks(120 * GameClock.TicksPerSecond);
        Assert.True(c.State.Run.HighestZone > 1);
        Assert.All(c.State.Monsters, m => Assert.Equal(MonsterSpecies.Goblin, m.Species));
    }

    [Fact]
    public void ReturnToMenu_SavesAndStopsTheGame()
    {
        var files = new InMemoryFileSystem();
        using var session = Continued(files, () => T0);
        session.Game.State.Gold = 555;
        session.ReturnToMenu();

        Assert.True(session.InMenu);
        Assert.Empty(session.Game.State.Heroes);
        var reloaded = new Core.Controller.GameController();
        reloaded.Load(files.ReadText(GameSession.AutosaveFileName)!);
        Assert.Equal<long>(555, reloaded.State.Gold);
    }

    [Fact]
    public void FirstContinue_StartsANewGame_AndSavesIt()
    {
        var files = new InMemoryFileSystem();
        using var runtime = Continued(files, () => T0);
        Assert.NotNull(files.ReadText(GameSession.AutosaveFileName));
        Assert.Single(runtime.Game.State.Heroes);
        Assert.Single(runtime.Game.State.Monsters);
    }

    [Fact]
    public void Relaunch_ReloadsTheGame_AndCreditsOfflineTime()
    {
        var files = new InMemoryFileSystem();
        var now = T0;
        using (var first = Continued(files, () => now))
        {
            first.Game.State.Gold = 1234;
        } // Dispose sauvegarde

        now = T0.AddMinutes(10);
        using var second = Continued(files, () => now);
        Assert.Equal<long>(1234, second.Game.State.Gold);
        Assert.Equal(10 * 60 * GameClock.TicksPerSecond, second.Game.State.Clock.OfflineBankTicks);
    }

    [Fact]
    public void CorruptedAutosave_IsSetAside_NeverOverwrittenSilently()
    {
        var files = new InMemoryFileSystem();
        files.WriteText(GameSession.AutosaveFileName, "garbage");

        using var runtime = Continued(files, () => T0);

        Assert.Equal("garbage", files.ReadText(GameSession.CorruptedSaveFileName));
        Assert.NotEqual("garbage", files.ReadText(GameSession.AutosaveFileName));
    }

    [Fact]
    public void StalledTime_GoesToTheBank()
    {
        using var runtime = Continued(new InMemoryFileSystem(), () => T0);
        long before = runtime.Game.State.Clock.OfflineBankTicks;
        runtime.CreditStalledTime(2.5);
        Assert.Equal(before + 250, runtime.Game.State.Clock.OfflineBankTicks);
    }

    [Fact]
    public void GoingToBackground_SavesTheGame()
    {
        var files = new InMemoryFileSystem();
        using var runtime = Continued(files, () => T0);
        runtime.Game.State.Gold = 777;
        runtime.SetInBackground(true);

        var reloaded = new Core.Controller.GameController();
        reloaded.Load(files.ReadText(GameSession.AutosaveFileName)!);
        Assert.Equal<long>(777, reloaded.State.Gold);
    }
}
