using System.Diagnostics;
using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Platform;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Localization;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Render.Audio;
using HeroesOfIdlestan.Core.Render.Screens;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Runtime;

/// <summary>
/// Point d'entrée de la couche View : possède le contrôleur, l'écran courant, les bruitages et la
/// sauvegarde automatique. Mono-thread : l'hôte (<c>GameRuntimeHost</c>) sérialise tous les appels.
///
/// <para>Chaque head (Desktop, Browser, Android) le construit avec ses propres services : stockage,
/// sortie audio, stores. C'est la seule différence entre plateformes vue d'ici.</para>
/// </summary>
public sealed class GameRuntime : IDisposable
{
    public const string AutosaveFileName = "autosave.sav";
    public const string CorruptedSaveFileName = "autosave.corrupted.sav";
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(30);

    private readonly IFileSystemService _files;
    private readonly StoreController _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private readonly Stopwatch _autosaveTimer = Stopwatch.StartNew();
    private float _totalTime;
    private bool _loading;

    public GameController Game { get; } = new();
    public Localizer Localizer { get; } = new();
    public GameAudioService Audio { get; }
    public IScreen Screen { get; private set; }

    /// <param name="files">Stockage du head.</param>
    /// <param name="audio">Sortie audio du head, ou null pour un jeu muet.</param>
    /// <param name="playSoundInBackground">Défaut de plateforme : faux sur Android (poche, écran éteint).</param>
    /// <param name="store">Stores de plateforme (succès, cloud), ou aucun.</param>
    /// <param name="now">Horloge réelle, remplaçable dans les tests.</param>
    public GameRuntime(
        IFileSystemService files,
        IAudioService? audio = null,
        bool playSoundInBackground = true,
        StoreController? store = null,
        Func<DateTimeOffset>? now = null)
    {
        _files = files;
        _store = store ?? new StoreController();
        _now = now ?? (() => DateTimeOffset.UtcNow);

        Audio = new GameAudioService(audio, playSoundInBackground) { Suppressed = () => _loading };
        Game.StateReplaced += WireState;
        WireState();

        Screen = new GameScreen(Game);
        LoadOrCreate();
    }

    /// <summary>Rebranche ce qui écoute l'état : appelé à chaque nouvelle partie ou chargement.</summary>
    private void WireState() => Game.State.Events.Added += Audio.OnGameEvent;

    // ── Partie ───────────────────────────────────────────────────────────

    private void LoadOrCreate()
    {
        var content = _files.ReadText(AutosaveFileName);
        if (content != null)
        {
            try
            {
                _loading = true;
                Game.Load(content);
                Game.State.Clock.CreditOfflineTime(_now());
                _store.Connect(Game.Achievements, Game.State);
                return;
            }
            catch (SaveCorruptedException ex)
            {
                // Jamais d'écrasement silencieux d'une sauvegarde illisible : on la met de côté.
                GameLog.Error(nameof(GameRuntime), nameof(LoadOrCreate), ex);
                _files.WriteText(CorruptedSaveFileName, content);
            }
            finally
            {
                _loading = false;
            }
        }
        StartNewGame();
    }

    public void StartNewGame()
    {
        Game.NewGame(Environment.TickCount);
        _store.Connect(Game.Achievements, Game.State);
        SaveNow();
    }

    public void SaveNow()
    {
        _files.WriteText(AutosaveFileName, Game.Save(_now()));
        _autosaveTimer.Restart();
    }

    // ── Boucle ───────────────────────────────────────────────────────────

    public void Tick()
    {
        Game.Tick(_now());
        if (_autosaveTimer.Elapsed >= AutosaveInterval) SaveNow();
    }

    public void Render(SKCanvas canvas, SKSize size)
    {
        float dt = (float)_frameTimer.Elapsed.TotalSeconds;
        _frameTimer.Restart();
        _totalTime += dt;
        canvas.Clear(new SKColor(0x16, 0x1A, 0x20));
        Screen.Render(canvas, size, dt, _totalTime);
    }

    // ── Cycle de vie de l'hôte ───────────────────────────────────────────

    /// <summary>Fenêtre sans focus, onglet caché, activité Android en pause — ou retour.</summary>
    public void SetInBackground(bool inBackground)
    {
        Audio.SetHostInBackground(inBackground);
        // Une sauvegarde en quittant le premier plan : sur mobile et navigateur, l'application peut
        // être tuée sans autre préavis.
        if (inBackground) SaveNow();
    }

    /// <summary>
    /// Verse à la banque hors-ligne un temps pendant lequel la boucle n'a pas tourné (onglet bridé
    /// par le navigateur, processus gelé par Android). Le head mesure, le runtime crédite.
    /// </summary>
    public void CreditStalledTime(double seconds)
    {
        if (seconds <= 0) return;
        Game.State.Clock.OfflineBankTicks += (long)(seconds * GameClock.TicksPerSecond);
    }

    // ── Commandes ────────────────────────────────────────────────────────

    public void TogglePause()
    {
        var clock = Game.State.Clock;
        if (clock.IsPaused) clock.Resume(); else clock.Pause();
    }

    public void SetSpeed(int speed) => Game.State.Clock.SetSpeed(speed);

    public bool BuyProducer() => Game.TryBuyProducer();

    public void ToggleSound() => Audio.Enabled = !Audio.Enabled;

    // ── Lecture ──────────────────────────────────────────────────────────

    public HudSnapshot GetHudSnapshot()
    {
        var s = Game.State;
        long cost = Game.NextProducerCost;
        return new HudSnapshot(
            s.Clock.IsPaused,
            s.Clock.ActiveSpeed,
            s.Gold,
            s.Clock.OfflineBankTicks / GameClock.TicksPerSecond,
            s.Entities.Count,
            cost,
            s.Gold >= cost,
            s.CompletedAchievements.Count,
            Audio.IsAvailable,
            Audio.Enabled);
    }

    public void Dispose()
    {
        try { SaveNow(); }
        catch (Exception ex) { GameLog.Error(nameof(GameRuntime), nameof(Dispose), ex); }
        Screen.Dispose();
        Audio.Dispose();
        _store.Dispose();
    }
}
