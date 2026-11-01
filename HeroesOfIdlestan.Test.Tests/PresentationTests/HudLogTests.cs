using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Presentation.Runtime;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Test.Tests.PresentationTests;

/// <summary>
/// Le journal et le bouton « Log » du HUD : un nouveau message le fait clignoter jusqu'à ce que le
/// journal soit ouvert.
/// </summary>
[Collection(GameLogCollection.Name)]
public class HudLogTests
{
    /// <summary>Hôte sans partie : le HUD n'a besoin que des libellés.</summary>
    private sealed class NoGameHost : IGameHost
    {
        public T Read<T>(Func<GameSession, T> read) => default!;
        public void Invoke(Action<GameSession> command) { }
        public HudSnapshot GetHudSnapshot() => HudSnapshot.Empty;
        public MenuSnapshot GetMenuSnapshot() => MenuSnapshot.Empty;
        public string Localize(string key, params object[] args) => args.Length == 0 ? key : $"{key}:{string.Join(",", args)}";
    }

    private long _now;

    private HudViewModel Hud() => new(new NoGameHost(), () => _now);

    [Fact]
    public void Warning_IsRecordedWithItsLevel_AndBumpsTheVersion()
    {
        long before = GameLog.Version;
        string message = "avertissement " + Guid.NewGuid();

        GameLog.Warning("Test", "Op", message);
        GameLog.Warning("Test", "Op", message);

        Assert.Equal(before + 1, GameLog.Version); // une répétition n'est pas un nouveau message
        var entry = Assert.Single(GameLog.Snapshot(), e => e.Message == message);
        Assert.Equal(GameLog.Level.Warning, entry.Level);
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public void NewMessage_MakesTheButtonBlink_UntilTheLogIsOpened()
    {
        var hud = Hud();
        hud.Refresh(HudSnapshot.Empty);
        hud.ToggleLog(); // tout ce qui précède est vu
        hud.ToggleLog();
        Assert.False(hud.LogUnread);

        GameLog.Warning("Test", "Op", "nouveau " + Guid.NewGuid());
        _now = 0;
        hud.Refresh(HudSnapshot.Empty);
        Assert.True(hud.LogUnread);
        Assert.True(hud.LogHighlighted);

        _now = HudViewModel.BlinkMilliseconds;
        hud.Refresh(HudSnapshot.Empty);
        Assert.False(hud.LogHighlighted); // phase éteinte

        _now = 2 * HudViewModel.BlinkMilliseconds;
        hud.ToggleLog();
        Assert.True(hud.LogOpen);
        Assert.False(hud.LogUnread);
        Assert.False(hud.LogHighlighted);
    }

    [Fact]
    public void OpenLog_ListsNewestFirst_WithLevelAndRepeatCount()
    {
        string older = "ancien " + Guid.NewGuid(), newer = "récent " + Guid.NewGuid();
        GameLog.Error("Test", "Op", older);
        GameLog.Warning("Test", "Op", newer);
        GameLog.Warning("Test", "Op", newer);

        var hud = Hud();
        hud.ToggleLog();

        var lines = hud.LogText.Split('\n');
        int newerLine = Array.FindIndex(lines, l => l.Contains(newer));
        int olderLine = Array.FindIndex(lines, l => l.Contains(older));
        Assert.True(newerLine >= 0 && newerLine < olderLine);
        Assert.Contains("ui_log_warning", lines[newerLine]);
        Assert.Contains("(×2)", lines[newerLine]);
        Assert.Contains("ui_log_error", lines[olderLine]);
    }

    [Fact]
    public void MessageArrivingWhileOpen_IsSeenAtOnce()
    {
        var hud = Hud();
        hud.ToggleLog();
        string message = "pendant " + Guid.NewGuid();

        GameLog.Warning("Test", "Op", message);
        hud.Refresh(HudSnapshot.Empty);

        Assert.False(hud.LogUnread);
        Assert.Contains(message, hud.LogText);
    }
}
