using Godot;
using HeroesOfIdlestan.Core.Render.Godot.Audio;
using HeroesOfIdlestan.Core.Render.Godot.Runtime;
using HeroesOfIdlestan.Core.UI.Godot;
using HeroesOfIdlestan.Core.UI.Godot.Views;

namespace HeroesOfIdlestan.App.Godot;

/// <summary>
/// Seul script Godot du jeu (scène principale <c>Main.tscn</c>) : crée le runtime avec les services
/// de Godot, le confie à l'hôte, accroche la vue et lui relaie la boucle, l'input et le focus.
/// </summary>
public partial class Main : Node
{
    private GameRuntimeHost? _host;
    private GameView? _view;

    public override void _Ready()
    {
        var runtime = new GameRuntime(new GodotFileSystemService(), audio: new GodotAudioService(this));
        _host = new GameRuntimeHost(runtime);
        _view = new GameView(_host);
        AddChild(_view.Root);
    }

    public override void _Process(double delta) => _view?.Process(delta);

    public override void _UnhandledInput(InputEvent input)
    {
        if (_view?.HandleInput(input) == true) GetViewport().SetInputAsHandled();
    }

    public override void _Notification(int what)
    {
        // Fenêtre sans focus = arrière-plan : les bruitages s'y taisent si le joueur l'a choisi.
        if (what == NotificationApplicationFocusOut) _host?.SetInBackground(true);
        else if (what == NotificationApplicationFocusIn) _host?.SetInBackground(false);
    }

    public override void _ExitTree()
    {
        _view?.Dispose();
        _host?.Dispose(); // sauvegarde une dernière fois
        _view = null;
        _host = null;
    }
}
