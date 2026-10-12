using HeroesOfIdlestan.Core.Platform;
using HeroesOfIdlestan.Core.State;
using HeroesOfIdlestan.Render.Runtime;

namespace HeroesOfIdlestan.Tests.RenderTests;

/// <summary>Le runtime tel que le voient les heads : démarrage, sauvegarde, reprise.</summary>
public class GameRuntimeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstLaunch_StartsANewGame_AndSavesIt()
    {
        var files = new InMemoryFileSystem();
        using var runtime = new GameRuntime(files, now: () => T0);
        Assert.NotNull(files.ReadText(GameRuntime.AutosaveFileName));
        Assert.Single(runtime.Game.State.Entities);
    }

    [Fact]
    public void Relaunch_ReloadsTheGame_AndCreditsOfflineTime()
    {
        var files = new InMemoryFileSystem();
        var now = T0;
        using (var first = new GameRuntime(files, now: () => now))
        {
            first.Game.State.Gold = 1234;
        } // Dispose sauvegarde

        now = T0.AddMinutes(10);
        using var second = new GameRuntime(files, now: () => now);
        Assert.Equal<long>(1234, second.Game.State.Gold);
        Assert.Equal(10 * 60 * GameClock.TicksPerSecond, second.Game.State.Clock.OfflineBankTicks);
    }

    [Fact]
    public void CorruptedAutosave_IsSetAside_NeverOverwrittenSilently()
    {
        var files = new InMemoryFileSystem();
        files.WriteText(GameRuntime.AutosaveFileName, "garbage");

        using var runtime = new GameRuntime(files, now: () => T0);

        Assert.Equal("garbage", files.ReadText(GameRuntime.CorruptedSaveFileName));
        Assert.NotEqual("garbage", files.ReadText(GameRuntime.AutosaveFileName));
    }

    [Fact]
    public void StalledTime_GoesToTheBank()
    {
        using var runtime = new GameRuntime(new InMemoryFileSystem(), now: () => T0);
        long before = runtime.Game.State.Clock.OfflineBankTicks;
        runtime.CreditStalledTime(2.5);
        Assert.Equal(before + 250, runtime.Game.State.Clock.OfflineBankTicks);
    }

    [Fact]
    public void GoingToBackground_SavesTheGame()
    {
        var files = new InMemoryFileSystem();
        using var runtime = new GameRuntime(files, now: () => T0);
        runtime.Game.State.Gold = 777;
        runtime.SetInBackground(true);

        var reloaded = new Core.Controller.GameController();
        reloaded.Load(files.ReadText(GameRuntime.AutosaveFileName)!);
        Assert.Equal<long>(777, reloaded.State.Gold);
    }
}
