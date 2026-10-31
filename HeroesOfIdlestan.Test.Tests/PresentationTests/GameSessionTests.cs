using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Runtime;

namespace HeroesOfIdlestan.Test.Tests.PresentationTests;

/// <summary>La session telle que la voient les heads des deux piles : démarrage, sauvegarde, reprise.</summary>
public class GameSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstLaunch_StartsANewGame_AndSavesIt()
    {
        var files = new InMemoryFileSystem();
        using var runtime = new GameSession(files, now: () => T0);
        Assert.NotNull(files.ReadText(GameSession.AutosaveFileName));
        Assert.Single(runtime.Game.State.Heroes);
        Assert.Single(runtime.Game.State.Monsters);
    }

    [Fact]
    public void Relaunch_ReloadsTheGame_AndCreditsOfflineTime()
    {
        var files = new InMemoryFileSystem();
        var now = T0;
        using (var first = new GameSession(files, now: () => now))
        {
            first.Game.State.Gold = 1234;
        } // Dispose sauvegarde

        now = T0.AddMinutes(10);
        using var second = new GameSession(files, now: () => now);
        Assert.Equal<long>(1234, second.Game.State.Gold);
        Assert.Equal(10 * 60 * GameClock.TicksPerSecond, second.Game.State.Clock.OfflineBankTicks);
    }

    [Fact]
    public void CorruptedAutosave_IsSetAside_NeverOverwrittenSilently()
    {
        var files = new InMemoryFileSystem();
        files.WriteText(GameSession.AutosaveFileName, "garbage");

        using var runtime = new GameSession(files, now: () => T0);

        Assert.Equal("garbage", files.ReadText(GameSession.CorruptedSaveFileName));
        Assert.NotEqual("garbage", files.ReadText(GameSession.AutosaveFileName));
    }

    [Fact]
    public void StalledTime_GoesToTheBank()
    {
        using var runtime = new GameSession(new InMemoryFileSystem(), now: () => T0);
        long before = runtime.Game.State.Clock.OfflineBankTicks;
        runtime.CreditStalledTime(2.5);
        Assert.Equal(before + 250, runtime.Game.State.Clock.OfflineBankTicks);
    }

    [Fact]
    public void GoingToBackground_SavesTheGame()
    {
        var files = new InMemoryFileSystem();
        using var runtime = new GameSession(files, now: () => T0);
        runtime.Game.State.Gold = 777;
        runtime.SetInBackground(true);

        var reloaded = new Core.Controller.GameController();
        reloaded.Load(files.ReadText(GameSession.AutosaveFileName)!);
        Assert.Equal<long>(777, reloaded.State.Gold);
    }
}
