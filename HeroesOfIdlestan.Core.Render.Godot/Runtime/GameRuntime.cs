using System.Diagnostics;
using Godot;
using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Controller.Platform;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Localization;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Render.Godot.Audio;
using HeroesOfIdlestan.Core.Render.Godot.Battle;

namespace HeroesOfIdlestan.Core.Render.Godot.Runtime;

/// <summary>
/// Point d'entrée de la couche View sous Godot : possède le contrôleur, la scène 3D du combat, les
/// bruitages et la sauvegarde automatique. Mono-thread : l'hôte (<c>GameRuntimeHost</c>) sérialise
/// tous les appels.
///
/// <para>Même rôle et même contrat que le <c>GameRuntime</c> de Core.Render (Skia), dont il ne dépend
/// pas : seul le rendu change. Une sauvegarde passe de l'un à l'autre.</para>
/// </summary>
public sealed class GameRuntime : IDisposable
{
    public const string AutosaveFileName = "autosave.sav";
    public const string CorruptedSaveFileName = "autosave.corrupted.sav";
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(30);

    private readonly IFileSystemService _files;
    private readonly StoreController _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly Stopwatch _autosaveTimer = Stopwatch.StartNew();
    private bool _loading;

    public GameController Game { get; } = new();
    public Localizer Localizer { get; } = new();
    public GameAudioService Audio { get; }

    /// <summary>La scène 3D : le head accroche <see cref="BattleScene.Root"/> à son arbre.</summary>
    public BattleScene Scene { get; }

    /// <param name="files">Stockage du head.</param>
    /// <param name="audio">Sortie audio, ou null pour un jeu muet.</param>
    /// <param name="playSoundInBackground">Défaut de plateforme : faux sur mobile (poche, écran éteint).</param>
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

        Scene = new BattleScene(Game);
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
                // Une partie sans équipe (sauvegarde du contenu d'exemple du squelette) ne jouerait
                // jamais : elle est mise de côté comme une sauvegarde illisible.
                if (Game.State.Heroes.Count == 0)
                    throw new SaveCorruptedException("Sauvegarde sans équipe.");
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
        Game.NewGame(System.Environment.TickCount);
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

    /// <summary>Met la scène 3D à jour ; Godot la dessine ensuite sur son propre thread.</summary>
    public void Render(double deltaSeconds) => Scene.Update(Game.State, (float)deltaSeconds);

    /// <summary>Entrée non consommée par le HUD : la caméra. Vrai si l'événement a servi.</summary>
    public bool HandleInput(InputEvent input) => Scene.HandleInput(input);

    // ── Cycle de vie de l'hôte ───────────────────────────────────────────

    /// <summary>Fenêtre sans focus, application en pause — ou retour.</summary>
    public void SetInBackground(bool inBackground)
    {
        Audio.SetHostInBackground(inBackground);
        // Une sauvegarde en quittant le premier plan : sur mobile, l'application peut être tuée sans
        // autre préavis.
        if (inBackground) SaveNow();
    }

    /// <summary>
    /// Verse à la banque hors-ligne un temps pendant lequel la boucle n'a pas tourné (processus gelé).
    /// Le head mesure, le runtime crédite.
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

    public void ToggleAutoAdvance() => Game.SetAutoAdvance(!Game.State.Run.AutoAdvance);

    public void ToggleSound() => Audio.Enabled = !Audio.Enabled;

    // ── Lecture ──────────────────────────────────────────────────────────

    public HudSnapshot GetHudSnapshot()
    {
        var s = Game.State;
        // Une seule ligne de héros dans le HUD tant que l'équipe se limite au personnage de départ.
        var lead = s.Heroes.Count > 0 ? s.Heroes[0] : null;
        int level = lead?.Level ?? 0;
        return new HudSnapshot(
            s.Clock.IsPaused,
            s.Clock.ActiveSpeed,
            s.Gold,
            s.Clock.OfflineBankTicks / GameClock.TicksPerSecond,
            s.Run.Zone,
            s.Run.HighestZone,
            s.Run.AutoAdvance,
            level,
            lead?.Xp ?? 0,
            level is > 0 and < GameContent.MaxLevel ? GameContent.XpToNextLevel(level) : 0,
            Audio.IsAvailable,
            Audio.Enabled);
    }

    public void Dispose()
    {
        try { SaveNow(); }
        catch (Exception ex) { GameLog.Error(nameof(GameRuntime), nameof(Dispose), ex); }
        Scene.Dispose();
        Audio.Dispose();
        _store.Dispose();
    }
}
