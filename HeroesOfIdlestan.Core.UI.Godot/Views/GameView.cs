using Godot;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Godot.Views;

/// <summary>
/// La vue du jeu : la scène 3D au fond, le HUD par-dessus dans un <see cref="CanvasLayer"/>. Porte
/// la boucle de frame (tick, mise à jour de la scène) et le sondage du HUD, découplé des frames.
///
/// <para>Pas un <see cref="Node"/> : un script Godot doit vivre dans l'assembly du projet Godot. Le
/// head accroche <see cref="Root"/> et relaie <c>_Process</c> et <c>_UnhandledInput</c>.</para>
/// </summary>
public sealed class GameView : IDisposable
{
    /// <summary>Le HUD relit les snapshots à ce rythme, pas à chaque frame.</summary>
    private const double HudRefreshSeconds = 0.1;

    private readonly GameRuntimeHost _host;
    private readonly HudViewModel _hud;
    private readonly HudView _hudView;
    private double _sinceHudRefresh = HudRefreshSeconds;

    public GameView(GameRuntimeHost host)
    {
        _host = host;
        _hud = new HudViewModel(host);
        _hudView = new HudView(_hud);

        Root = new Node { Name = "GameView" };
        // La scène appartient au runtime : on ne fait que l'accrocher, sur le thread principal.
        Root.AddChild(host.Read(r => r.Scene.Root));
        var overlay = new CanvasLayer { Name = "Overlay" };
        overlay.AddChild(_hudView.Root);
        Root.AddChild(overlay);
    }

    public Node Root { get; }

    public void Process(double delta)
    {
        // Le tick tourne même quand rien n'est dessiné (fenêtre réduite) : le temps de jeu ne dérive pas.
        _host.Tick();
        _host.Render(delta);

        _sinceHudRefresh += delta;
        if (_sinceHudRefresh < HudRefreshSeconds) return;
        _sinceHudRefresh = 0;
        _hud.Refresh(_host.GetHudSnapshot());
    }

    /// <summary>Entrée que le HUD n'a pas prise. Vrai si elle a servi.</summary>
    public bool HandleInput(InputEvent input) => _host.HandleInput(input);

    public void Dispose() => _hudView.Detach();
}
