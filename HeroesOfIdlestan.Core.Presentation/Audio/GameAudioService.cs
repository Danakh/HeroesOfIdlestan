using System.Diagnostics;
using HeroesOfIdlestan.Core.Model.Meta;

namespace HeroesOfIdlestan.Core.Presentation.Audio;

/// <summary>
/// Le chef d'orchestre des bruitages : décide de ce qui se joue et quand, et laisse le
/// <see cref="IAudioService"/> du head faire sonner. Rien d'autre n'appelle le service audio.
///
/// <para><b>Sans service audio, la classe reste utilisable et muette</b> (tests, outils, machine
/// sans carte son) : les événements se branchent sans condition partout ailleurs.</para>
///
/// <para>Garde-fous, à garder quand le jeu grossit :</para>
/// <list type="number">
/// <item>un intervalle minimum par son (<see cref="MinInterval"/>) : un saut de temps en vitesse x10 ne
/// doit pas enchaîner cent victoires sonores ;</item>
/// <item>la suppression pendant un saut de temps ou un chargement (<see cref="Suppressed"/>) ;</item>
/// <item>le silence en arrière-plan si le joueur le veut, avec mise en veille de la sortie (sur
/// Android, un flux ouvert même muet vide la batterie).</item>
/// </list>
/// </summary>
public sealed class GameAudioService : IDisposable
{
    public const float DefaultVolume = 0.6f;

    private readonly IAudioService? _audio;
    private readonly long[] _lastPlayed = new long[Enum.GetValues<SoundId>().Length];
    private bool _hostInBackground;
    private bool _suspended;
    private bool _disposed;

    /// <param name="audio">Sortie du head, ou null pour un jeu muet.</param>
    /// <param name="playInBackground">Défaut de la plateforme : faux sur Android, vrai ailleurs.</param>
    public GameAudioService(IAudioService? audio, bool playInBackground = true)
    {
        _audio = audio;
        PlayInBackground = playInBackground;
        if (_audio != null) SoundBank.LoadAll(_audio);
    }

    public bool IsAvailable => _audio != null;

    public bool Enabled { get; set; } = true;

    public float Volume { get; set; } = DefaultVolume;

    public bool PlayInBackground { get; set; }

    /// <summary>Vrai tant que rien ne doit sonner (saut de temps, chargement…). Posé par le runtime.</summary>
    public Func<bool>? Suppressed { get; set; }

    /// <summary>Le head signale le passage en arrière-plan (fenêtre, onglet caché, activité en pause).</summary>
    public void SetHostInBackground(bool inBackground)
    {
        _hostInBackground = inBackground;
        bool suspended = _hostInBackground && !PlayInBackground;
        if (suspended == _suspended || _disposed) return;
        _suspended = suspended;
        _audio?.SetSuspended(suspended);
    }

    /// <summary>Traduit un événement de jeu en son. Le seul endroit qui relie le modèle aux bruitages.</summary>
    public void OnGameEvent(GameEvent gameEvent)
    {
        switch (gameEvent.Type)
        {
            case "fight_won": Play(SoundId.ZoneCleared); break;
            case "fight_lost": Play(SoundId.FightLost); break;
            case "hero_level_up": Play(SoundId.LevelUp); break;
            case "achievement_unlocked": Play(SoundId.AchievementUnlocked); break;
        }
    }

    public void Play(SoundId id)
    {
        if (_audio == null || _disposed || !Enabled || _suspended) return;
        if (Suppressed?.Invoke() == true) return;

        long now = Stopwatch.GetTimestamp();
        int index = (int)id;
        if (_lastPlayed[index] != 0 && Stopwatch.GetElapsedTime(_lastPlayed[index], now).TotalSeconds < MinInterval(id))
            return;
        _lastPlayed[index] = now;

        _audio.Play(id, Math.Clamp(Volume * Mix(id), 0f, 1f));
    }

    /// <summary>Intervalle minimum entre deux passages d'un même son, en secondes.</summary>
    private static double MinInterval(SoundId id) => id switch
    {
        SoundId.UiConfirm => 0.05,
        _ => 0.35,
    };

    /// <summary>Volume relatif dans le mélange : les sons répétitifs passent sous les annonces.</summary>
    private static float Mix(SoundId id) => id switch
    {
        SoundId.UiConfirm => 0.6f,
        SoundId.ZoneCleared => 0.4f,
        _ => 1f,
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _audio?.Dispose();
    }
}
