using HeroesOfIdlestan.Core.Render.Runtime;

namespace HeroesOfIdlestan.Core.UI.ViewModels;

/// <summary>
/// Barre du haut. Ne lit jamais le runtime lui-même : <see cref="Refresh"/> reçoit un instantané déjà
/// pris sous verrou, et les commandes repassent par l'hôte.
/// </summary>
public sealed class HudViewModel(GameRuntimeHost host) : ViewModelBase
{
    private HudSnapshot _last = HudSnapshot.Empty;
    private string _goldLabel = "";
    private string _producersLabel = "";
    private string _bankLabel = "";
    private string _buyLabel = "";
    private string _pauseLabel = "";
    private string _saveLabel = "";
    private string _soundLabel = "";
    private bool _soundAvailable;
    private bool _canBuy;

    public string GoldLabel { get => _goldLabel; private set => SetProperty(ref _goldLabel, value); }
    public string ProducersLabel { get => _producersLabel; private set => SetProperty(ref _producersLabel, value); }
    public string BankLabel { get => _bankLabel; private set => SetProperty(ref _bankLabel, value); }
    public string BuyLabel { get => _buyLabel; private set => SetProperty(ref _buyLabel, value); }
    public string PauseLabel { get => _pauseLabel; private set => SetProperty(ref _pauseLabel, value); }
    public string SaveLabel { get => _saveLabel; private set => SetProperty(ref _saveLabel, value); }
    public string SoundLabel { get => _soundLabel; private set => SetProperty(ref _soundLabel, value); }
    public bool SoundAvailable { get => _soundAvailable; private set => SetProperty(ref _soundAvailable, value); }
    public bool CanBuy { get => _canBuy; private set => SetProperty(ref _canBuy, value); }

    public void Refresh(HudSnapshot snapshot)
    {
        // Égalité structurelle du record : rien à faire si rien n'a bougé depuis le dernier sondage.
        if (snapshot == _last && GoldLabel.Length > 0) return;
        _last = snapshot;

        GoldLabel = host.Localize("ui_gold", snapshot.Gold);
        ProducersLabel = host.Localize("ui_producers", snapshot.ProducerCount);
        BankLabel = host.Localize("ui_bank", snapshot.BankSeconds);
        BuyLabel = host.Localize("ui_buy_producer", snapshot.NextProducerCost);
        PauseLabel = host.Localize(snapshot.IsPaused ? "ui_play" : "ui_pause");
        SaveLabel = host.Localize("ui_save");
        SoundLabel = host.Localize(snapshot.SoundEnabled ? "ui_sound_on" : "ui_sound_off");
        SoundAvailable = snapshot.SoundAvailable;
        CanBuy = snapshot.CanBuyProducer;
    }

    public void TogglePause() => host.Invoke(r => r.TogglePause());
    public void BuyProducer() => host.Invoke(r => r.BuyProducer());
    public void SetSpeed(int speed) => host.Invoke(r => r.SetSpeed(speed));
    public void ToggleSound() => host.Invoke(r => r.ToggleSound());
    public void Save() => host.Invoke(r => r.SaveNow());
    public void NewGame() => host.Invoke(r => r.StartNewGame());
}
