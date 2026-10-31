using HeroesOfIdlestan.Core.Presentation.Runtime;

namespace HeroesOfIdlestan.Core.Presentation.ViewModels;

/// <summary>
/// Menu principal, commun aux deux piles : continuer la partie sauvegardée, en commencer une
/// nouvelle, ou lancer une partie de debug. Le HUD est masqué tant que le menu est affiché.
/// </summary>
public sealed class MenuViewModel(IGameHost host) : ViewModelBase
{
    private MenuSnapshot? _last;
    private bool _visible;
    private bool _canContinue;
    private string _title = "";
    private string _continueLabel = "";
    private string _newGameLabel = "";
    private string _debugGameLabel = "";

    public bool Visible { get => _visible; private set { if (SetProperty(ref _visible, value)) OnPropertyChanged(nameof(HudVisible)); } }
    public bool HudVisible => !_visible;
    public bool CanContinue { get => _canContinue; private set => SetProperty(ref _canContinue, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string ContinueLabel { get => _continueLabel; private set => SetProperty(ref _continueLabel, value); }
    public string NewGameLabel { get => _newGameLabel; private set => SetProperty(ref _newGameLabel, value); }
    public string DebugGameLabel { get => _debugGameLabel; private set => SetProperty(ref _debugGameLabel, value); }

    public void Refresh(MenuSnapshot snapshot)
    {
        if (snapshot == _last) return;
        _last = snapshot;

        Title = host.Localize("app_title");
        ContinueLabel = host.Localize("ui_continue");
        NewGameLabel = host.Localize("ui_new_game");
        DebugGameLabel = host.Localize("ui_debug_game");
        CanContinue = snapshot.CanContinue;
        Visible = snapshot.Visible;
    }

    public void Continue() => host.Invoke(r => r.ContinueGame());
    public void NewGame() => host.Invoke(r => r.StartNewGame());
    public void DebugGame() => host.Invoke(r => r.StartDebugGame());
}
