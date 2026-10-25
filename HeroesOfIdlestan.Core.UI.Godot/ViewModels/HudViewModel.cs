using HeroesOfIdlestan.Core.Render.Godot.Runtime;

namespace HeroesOfIdlestan.Core.UI.Godot.ViewModels;

/// <summary>
/// Barre du haut. Ne lit jamais le runtime lui-même : <see cref="Refresh"/> reçoit un instantané déjà
/// pris sous verrou, et les commandes repassent par l'hôte.
/// </summary>
public sealed class HudViewModel(GameRuntimeHost host) : ViewModelBase
{
    private HudSnapshot _last = HudSnapshot.Empty;
    private string _goldLabel = "";
    private string _zoneLabel = "";
    private string _heroLabel = "";
    private string _autoAdvanceLabel = "";
    private string _bankLabel = "";
    private string _pauseLabel = "";
    private string _saveLabel = "";
    private string _soundLabel = "";
    private bool _soundAvailable;

    public string GoldLabel { get => _goldLabel; private set => SetProperty(ref _goldLabel, value); }
    public string ZoneLabel { get => _zoneLabel; private set => SetProperty(ref _zoneLabel, value); }
    public string HeroLabel { get => _heroLabel; private set => SetProperty(ref _heroLabel, value); }
    public string AutoAdvanceLabel { get => _autoAdvanceLabel; private set => SetProperty(ref _autoAdvanceLabel, value); }
    public string BankLabel { get => _bankLabel; private set => SetProperty(ref _bankLabel, value); }
    public string PauseLabel { get => _pauseLabel; private set => SetProperty(ref _pauseLabel, value); }
    public string SaveLabel { get => _saveLabel; private set => SetProperty(ref _saveLabel, value); }
    public string SoundLabel { get => _soundLabel; private set => SetProperty(ref _soundLabel, value); }
    public bool SoundAvailable { get => _soundAvailable; private set => SetProperty(ref _soundAvailable, value); }

    public void Refresh(HudSnapshot snapshot)
    {
        // Égalité structurelle du record : rien à faire si rien n'a bougé depuis le dernier sondage.
        if (snapshot == _last && GoldLabel.Length > 0) return;
        _last = snapshot;

        GoldLabel = host.Localize("ui_gold", snapshot.Gold);
        ZoneLabel = host.Localize("ui_zone", snapshot.Zone, snapshot.HighestZone);
        HeroLabel = snapshot.HeroXpToNext > 0
            ? host.Localize("ui_warrior_level", snapshot.HeroLevel, snapshot.HeroXp, snapshot.HeroXpToNext)
            : host.Localize("ui_warrior_level_max", snapshot.HeroLevel);
        AutoAdvanceLabel = host.Localize(snapshot.AutoAdvance ? "ui_auto_advance_on" : "ui_auto_advance_off");
        BankLabel = host.Localize("ui_bank", snapshot.BankSeconds);
        PauseLabel = host.Localize(snapshot.IsPaused ? "ui_play" : "ui_pause");
        SaveLabel = host.Localize("ui_save");
        SoundLabel = host.Localize(snapshot.SoundEnabled ? "ui_sound_on" : "ui_sound_off");
        SoundAvailable = snapshot.SoundAvailable;
    }

    public void TogglePause() => host.Invoke(r => r.TogglePause());
    public void ToggleAutoAdvance() => host.Invoke(r => r.ToggleAutoAdvance());
    public void SetSpeed(int speed) => host.Invoke(r => r.SetSpeed(speed));
    public void ToggleSound() => host.Invoke(r => r.ToggleSound());
    public void Save() => host.Invoke(r => r.SaveNow());
    public void NewGame() => host.Invoke(r => r.StartNewGame());
}
