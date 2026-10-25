using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Model.State;

/// <summary>
/// Horloge de simulation. 1 tick = 0,01 s réelle. Le jeu n'avance qu'en ticks, jamais en temps réel :
/// c'est ce qui rend la simulation rejouable en headless (tests, autoplay) et accélérable.
///
/// <para>La banque hors-ligne accumule le temps passé en pause ou application fermée ; les vitesses
/// x2 et plus la dépensent. L'horloge ne déclenche rien elle-même : <see cref="Advance"/> renvoie le
/// nombre de ticks écoulés, et c'est <c>GameController</c> qui fait tourner le pipeline de simulation.</para>
/// </summary>
public sealed class GameClock
{
    public const int TicksPerSecond = 100;

    /// <summary>Plafond de l'accumulateur : au-delà de 100 ms d'un coup (reprise après un gel), le surplus est perdu.</summary>
    private const double MaxAccumulatedTicks = 10.0;

    /// <summary>Tick de simulation courant.</summary>
    public long CurrentTick { get; set; }

    /// <summary>Ticks accumulés hors-ligne ou en pause, dépensés par la vitesse accélérée.</summary>
    public long OfflineBankTicks { get; set; }

    /// <summary>Heure réelle de la dernière sauvegarde : sert à créditer la banque au chargement.</summary>
    public DateTimeOffset LastSaveTime { get; set; }

    /// <summary>État de pause au moment de la sauvegarde, restauré au chargement.</summary>
    public bool WasPausedAtSave { get; set; }

    // ── runtime (non sérialisé) ──────────────────────────────────────────

    /// <summary>0 = pause, sinon la vitesse active.</summary>
    [JsonIgnore] public int SpeedMultiplier { get; private set; } = 1;

    /// <summary>Dernière vitesse non nulle, reprise par <see cref="Resume"/>.</summary>
    [JsonIgnore] public int ActiveSpeed { get; private set; } = 1;

    [JsonIgnore] public bool IsPaused => SpeedMultiplier == 0;

    private DateTimeOffset? _lastAdvanceTime;
    private double _tickAccumulator;

    public void Pause() { SpeedMultiplier = 0; ResetRealTime(); }

    public void Resume() { SpeedMultiplier = ActiveSpeed; ResetRealTime(); }

    public void SetSpeed(int multiplier)
    {
        if (multiplier < 1) throw new ArgumentOutOfRangeException(nameof(multiplier));
        ActiveSpeed = multiplier;
        SpeedMultiplier = multiplier;
        ResetRealTime();
    }

    /// <summary>Crédite la banque du temps écoulé depuis <see cref="LastSaveTime"/>.</summary>
    public void CreditOfflineTime(DateTimeOffset now)
    {
        if (LastSaveTime != default)
            OfflineBankTicks += Math.Max(0L, (long)((now - LastSaveTime).TotalSeconds * TicksPerSecond));
        ResetRealTime();
    }

    /// <summary>
    /// Avance selon le temps réel écoulé depuis l'appel précédent et renvoie les ticks de jeu gagnés.
    /// En pause, le temps réel part dans la banque. Le premier appel ne fait que poser la référence.
    /// </summary>
    public long Advance(DateTimeOffset now)
    {
        if (_lastAdvanceTime == null)
        {
            _lastAdvanceTime = now;
            return 0;
        }

        var elapsed = now - _lastAdvanceTime.Value;
        _lastAdvanceTime = now;

        // La fraction de tick est reportée d'un appel à l'autre : une boucle à plus de 100 Hz ne
        // perd ainsi aucun temps.
        _tickAccumulator = Math.Min(_tickAccumulator + elapsed.TotalSeconds * TicksPerSecond, MaxAccumulatedTicks);
        long realTicks = (long)_tickAccumulator;
        if (realTicks <= 0) return 0;
        _tickAccumulator -= realTicks;

        if (IsPaused)
        {
            OfflineBankTicks += realTicks;
            return 0;
        }

        long extraNeeded = realTicks * (SpeedMultiplier - 1);
        long consumed = Math.Min(extraNeeded, OfflineBankTicks);
        OfflineBankTicks -= consumed;
        long gained = realTicks + consumed;
        CurrentTick += gained;
        return gained;
    }

    private void ResetRealTime()
    {
        _lastAdvanceTime = null;
        _tickAccumulator = 0;
    }
}
