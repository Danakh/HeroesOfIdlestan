using System.Text;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Presentation.Runtime;

namespace HeroesOfIdlestan.Core.Presentation.ViewModels;

/// <summary>
/// Barre du haut, commune aux deux piles. Ne lit jamais la session elle-même : <see cref="Refresh"/>
/// reçoit un instantané déjà pris sous verrou, et les commandes repassent par l'hôte.
///
/// <para>Le bouton « Log » ouvre le <see cref="GameLog"/>. Tant qu'un message arrivé depuis la
/// dernière ouverture n'a pas été vu, <see cref="LogHighlighted"/> alterne (clignotement) au rythme
/// des <see cref="Refresh"/>, que les deux piles appellent toutes les 100 ms.</para>
/// </summary>
/// <param name="clockMilliseconds">Horloge du clignotement ; l'horloge système par défaut.</param>
public sealed class HudViewModel(IGameHost host, Func<long>? clockMilliseconds = null) : ViewModelBase
{
    /// <summary>Durée d'une phase du clignotement (allumé, puis éteint).</summary>
    public const int BlinkMilliseconds = 500;

    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private long _logSeenVersion;
    private bool _logUnread;
    private bool _logHighlighted;
    private bool _logOpen;
    private string _logLabel = "";
    private string _logText = "";

    private HudSnapshot _last = HudSnapshot.Empty;
    private string _goldLabel = "";
    private string _zoneLabel = "";
    private string _heroLabel = "";
    private string _autoAdvanceLabel = "";
    private string _bankLabel = "";
    private string _pauseLabel = "";
    private string _saveLabel = "";
    private string _soundLabel = "";
    private string _menuLabel = "";
    private bool _soundAvailable;
    private string _recruitWarriorLabel = "";
    private string _recruitRogueLabel = "";
    private string _recruitMageLabel = "";
    private bool _canRecruit;

    public string GoldLabel { get => _goldLabel; private set => SetProperty(ref _goldLabel, value); }
    public string ZoneLabel { get => _zoneLabel; private set => SetProperty(ref _zoneLabel, value); }
    public string HeroLabel { get => _heroLabel; private set => SetProperty(ref _heroLabel, value); }
    public string AutoAdvanceLabel { get => _autoAdvanceLabel; private set => SetProperty(ref _autoAdvanceLabel, value); }
    public string BankLabel { get => _bankLabel; private set => SetProperty(ref _bankLabel, value); }
    public string PauseLabel { get => _pauseLabel; private set => SetProperty(ref _pauseLabel, value); }
    public string SaveLabel { get => _saveLabel; private set => SetProperty(ref _saveLabel, value); }
    public string SoundLabel { get => _soundLabel; private set => SetProperty(ref _soundLabel, value); }
    public string MenuLabel { get => _menuLabel; private set => SetProperty(ref _menuLabel, value); }
    public bool SoundAvailable { get => _soundAvailable; private set => SetProperty(ref _soundAvailable, value); }

    public string RecruitWarriorLabel { get => _recruitWarriorLabel; private set => SetProperty(ref _recruitWarriorLabel, value); }
    public string RecruitRogueLabel { get => _recruitRogueLabel; private set => SetProperty(ref _recruitRogueLabel, value); }
    public string RecruitMageLabel { get => _recruitMageLabel; private set => SetProperty(ref _recruitMageLabel, value); }
    /// <summary>L'or suffit pour la prochaine recrue : les boutons de recrutement sont actifs.</summary>
    public bool CanRecruit { get => _canRecruit; private set => SetProperty(ref _canRecruit, value); }

    public string LogLabel { get => _logLabel; private set => SetProperty(ref _logLabel, value); }
    /// <summary>Un message n'a pas encore été vu.</summary>
    public bool LogUnread { get => _logUnread; private set => SetProperty(ref _logUnread, value); }
    /// <summary>Phase allumée du clignotement : les vues mettent alors le bouton en évidence.</summary>
    public bool LogHighlighted { get => _logHighlighted; private set => SetProperty(ref _logHighlighted, value); }
    public bool LogOpen { get => _logOpen; private set => SetProperty(ref _logOpen, value); }
    /// <summary>Le journal, du plus récent au plus ancien, une entrée par ligne.</summary>
    public string LogText { get => _logText; private set => SetProperty(ref _logText, value); }

    public void Refresh(HudSnapshot snapshot)
    {
        RefreshLog();

        // Égalité structurelle du record : rien à faire si rien n'a bougé depuis le dernier sondage.
        if (snapshot == _last && GoldLabel.Length > 0) return;
        _last = snapshot;

        GoldLabel = host.Localize("ui_gold", snapshot.Gold);
        ZoneLabel = host.Localize("ui_zone", snapshot.Zone, snapshot.HighestZone);
        string heroClass = snapshot.HeroClass.Length > 0 ? host.Localize(ClassKey(snapshot.HeroClass)) : "";
        HeroLabel = snapshot.HeroXpToNext > 0
            ? host.Localize("ui_hero_level", heroClass, snapshot.HeroLevel, snapshot.HeroXp, snapshot.HeroXpToNext)
            : host.Localize("ui_hero_level_max", heroClass, snapshot.HeroLevel);
        RecruitWarriorLabel = RecruitLabel(HeroClass.Warrior, snapshot.RecruitCost);
        RecruitRogueLabel = RecruitLabel(HeroClass.Rogue, snapshot.RecruitCost);
        RecruitMageLabel = RecruitLabel(HeroClass.Mage, snapshot.RecruitCost);
        CanRecruit = snapshot.CanRecruit;
        AutoAdvanceLabel = host.Localize(snapshot.AutoAdvance ? "ui_auto_advance_on" : "ui_auto_advance_off");
        BankLabel = host.Localize("ui_bank", snapshot.BankSeconds);
        PauseLabel = host.Localize(snapshot.IsPaused ? "ui_play" : "ui_pause");
        SaveLabel = host.Localize("ui_save");
        SoundLabel = host.Localize(snapshot.SoundEnabled ? "ui_sound_on" : "ui_sound_off");
        MenuLabel = host.Localize("ui_menu");
        SoundAvailable = snapshot.SoundAvailable;
    }

    public void TogglePause() => host.Invoke(r => r.TogglePause());
    public void ToggleAutoAdvance() => host.Invoke(r => r.ToggleAutoAdvance());
    public void SetSpeed(int speed) => host.Invoke(r => r.SetSpeed(speed));
    public void ToggleSound() => host.Invoke(r => r.ToggleSound());
    public void Save() => host.Invoke(r => r.SaveNow());
    public void ReturnToMenu() => host.Invoke(r => r.ReturnToMenu());
    public void Recruit(HeroClass heroClass) => host.Invoke(r => r.Recruit(heroClass));

    /// <summary>Clé de traduction du nom d'une classe : <c>class_warrior</c>, <c>class_mage</c>…</summary>
    public static string ClassKey(string heroClass) => "class_" + heroClass.ToLowerInvariant();

    private string RecruitLabel(HeroClass heroClass, long cost) =>
        host.Localize("ui_recruit", host.Localize(ClassKey(heroClass.ToString())), cost);

    /// <summary>Ouvre ou ferme le journal ; l'ouvrir marque tout comme vu.</summary>
    public void ToggleLog()
    {
        LogOpen = !LogOpen;
        RefreshLog();
    }

    // Le journal est un canal statique et verrouillé, hors de la partie : lu directement, pas par
    // l'instantané, il reste consultable quand aucune session ne tourne.
    private void RefreshLog()
    {
        long version = GameLog.Version;
        if (LogOpen) _logSeenVersion = version;
        LogUnread = version > _logSeenVersion;
        LogHighlighted = LogUnread && _clock() / BlinkMilliseconds % 2 == 0;

        var entries = GameLog.Snapshot();
        LogLabel = host.Localize("ui_log", entries.Count);
        // Recomposé à chaque passage tant qu'il est ouvert : les compteurs de répétition bougent
        // sans changer la version.
        if (LogOpen) LogText = Format(entries);
    }

    private string Format(IReadOnlyList<GameLog.Entry> entries)
    {
        if (entries.Count == 0) return host.Localize("ui_log_empty");
        string error = host.Localize("ui_log_error"), warning = host.Localize("ui_log_warning");
        var text = new StringBuilder();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (text.Length > 0) text.Append('\n');
            text.Append(e.Time.ToString("HH:mm:ss")).Append("  ")
                .Append(e.Level == GameLog.Level.Error ? error : warning)
                .Append("  ").Append(e.Source).Append(" / ").Append(e.Operation).Append(" : ");
            if (e.ExceptionType.Length > 0) text.Append(e.ExceptionType).Append(" — ");
            text.Append(e.Message);
            if (e.Count > 1) text.Append(" (×").Append(e.Count).Append(')');
        }
        return text.ToString();
    }
}
