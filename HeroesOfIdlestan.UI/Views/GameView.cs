using Avalonia.Controls;
using Avalonia.Threading;
using HeroesOfIdlestan.UI.Controls;
using HeroesOfIdlestan.UI.ViewModels;

namespace HeroesOfIdlestan.UI.Views;

/// <summary>
/// Vue racine : le monde Skia au fond, l'overlay par-dessus. L'ordre d'ajout dans
/// <see cref="Panel.Children"/> fait foi — il décide de ce qui recouvre quoi et de qui reçoit un clic.
/// </summary>
public sealed class GameView : Panel, IDisposable
{
    /// <summary>
    /// Cadence de synchronisation état → UI, découplée des 60 fps du rendu : les libellés ne bougent
    /// que quelques fois par seconde.
    /// </summary>
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMilliseconds(100);

    private readonly GameRuntimeHost _host;
    private readonly HudViewModel _hud;
    private readonly DispatcherTimer _sync;

    public GameView(GameRuntimeHost host)
    {
        _host = host;
        _hud = new HudViewModel(host);

        Children.Add(new GameRuntimeControl(host));
        Children.Add(new HudView(_hud));

        _sync = new DispatcherTimer(SyncInterval, DispatcherPriority.Normal, (_, _) => SyncFromRuntime());
        _sync.Start();
        SyncFromRuntime();
    }

    private void SyncFromRuntime() => _hud.Refresh(_host.GetHudSnapshot());

    public void Dispose() => _sync.Stop();
}
