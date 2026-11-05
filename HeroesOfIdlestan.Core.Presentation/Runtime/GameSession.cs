using System.Diagnostics;
using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Controller.Platform;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Localization;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Audio;

namespace HeroesOfIdlestan.Core.Presentation.Runtime;

/// <summary>
/// La partie en cours, telle que la voient toutes les piles d'affichage : contrôleur, localisation,
/// bruitages, sauvegarde automatique, commandes du joueur et snapshots de l'overlay. Mono-thread :
/// l'hôte (<see cref="GameHost{TRuntime}"/>) sérialise tous les appels.
///
/// <para>Chaque pile la compose dans son <c>GameRuntime</c> avec ce qu'elle dessine (écran Skia,
/// scène 3D Godot). Ce qui ne dépend pas du rendu vit ici, une seule fois : une règle ajoutée à la
/// session vaut pour les deux piles, et une sauvegarde passe de l'une à l'autre.</para>
///
/// <para>Chaque head la construit avec ses propres services : stockage, sortie audio, stores.</para>
///
/// <para>Au lancement, la session est au menu (<see cref="InMenu"/>) : aucune partie n'est chargée
/// et rien n'avance tant que le joueur n'a pas choisi de continuer, de commencer une nouvelle partie
/// ou de lancer une partie de debug. Une partie de debug n'est jamais sauvegardée : elle ne touche
/// pas à la partie en cours.</para>
/// </summary>
public sealed class GameSession : IDisposable
{
    public const string AutosaveFileName = "autosave.sav";
    public const string CorruptedSaveFileName = "autosave.corrupted.sav";
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(30);

    private readonly IFileSystemService _files;
    private readonly StoreController _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly Stopwatch _autosaveTimer = Stopwatch.StartNew();
    private bool _loading;
    private bool _hasSavedGame;

    public GameController Game { get; } = new();
    public Localizer Localizer { get; } = new();
    public GameAudioService Audio { get; }

    /// <summary>Vrai au lancement et après <see cref="ReturnToMenu"/> : aucune partie ne tourne.</summary>
    public bool InMenu { get; private set; } = true;

    /// <summary>Une partie sauvegardée existe : le menu propose de la continuer.</summary>
    public bool HasSavedGame => _hasSavedGame;

    /// <param name="files">Stockage du head.</param>
    /// <param name="audio">Sortie audio du head, ou null pour un jeu muet.</param>
    /// <param name="playSoundInBackground">Défaut de plateforme : faux sur mobile (poche, écran éteint).</param>
    /// <param name="store">Stores de plateforme (succès, cloud), ou aucun.</param>
    /// <param name="now">Horloge réelle, remplaçable dans les tests.</param>
    public GameSession(
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

        _hasSavedGame = _files.ReadText(AutosaveFileName) != null;
    }

    /// <summary>Rebranche ce qui écoute l'état : appelé à chaque nouvelle partie ou chargement.</summary>
    private void WireState() => Game.State.Events.Added += Audio.OnGameEvent;

    // ── Partie ───────────────────────────────────────────────────────────

    /// <summary>Reprend la partie sauvegardée (temps d'absence crédité), ou en commence une s'il n'y en a pas.</summary>
    public void ContinueGame()
    {
        InMenu = false;
        LoadOrCreate();
    }

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
                GameLog.Error(nameof(GameSession), nameof(LoadOrCreate), ex);
                _files.WriteText(CorruptedSaveFileName, content);
            }
            finally
            {
                _loading = false;
            }
        }
        StartNewGame();
    }

    /// <summary>Nouvelle partie : remplace la partie sauvegardée.</summary>
    public void StartNewGame()
    {
        InMenu = false;
        Game.NewGame(Environment.TickCount);
        _store.Connect(Game.Achievements, Game.State);
        SaveNow();
    }

    /// <summary>
    /// Partie de debug (15 guerriers contre 15 gobelins). Ni sauvegardée ni reliée aux stores : la
    /// partie en cours reste intacte, et « Continuer » la retrouve.
    /// </summary>
    public void StartDebugGame()
    {
        InMenu = false;
        Game.NewDebugGame(Environment.TickCount);
    }

    /// <summary>Sauvegarde la partie en cours et revient au menu, sur un champ de bataille vide.</summary>
    public void ReturnToMenu()
    {
        if (InMenu) return;
        SaveNow();
        InMenu = true;
        Game.Clear();
    }

    /// <summary>Sauvegarde la partie en cours ; sans effet au menu et en partie de debug.</summary>
    public void SaveNow()
    {
        if (InMenu || Game.State.Run.Debug) return;
        _files.WriteText(AutosaveFileName, Game.Save(_now()));
        _hasSavedGame = true;
        _autosaveTimer.Restart();
    }

    // ── Boucle ───────────────────────────────────────────────────────────

    public void Tick()
    {
        if (InMenu) return;
        Game.Tick(_now());
        if (_autosaveTimer.Elapsed >= AutosaveInterval) SaveNow();
    }

    // ── Cycle de vie de l'hôte ───────────────────────────────────────────

    /// <summary>Fenêtre sans focus, onglet caché, application en pause — ou retour.</summary>
    public void SetInBackground(bool inBackground)
    {
        Audio.SetHostInBackground(inBackground);
        // Une sauvegarde en quittant le premier plan : sur mobile et navigateur, l'application peut
        // être tuée sans autre préavis.
        if (inBackground) SaveNow();
    }

    /// <summary>
    /// Verse à la banque hors-ligne un temps pendant lequel la boucle n'a pas tourné (onglet bridé
    /// par le navigateur, processus gelé par Android). Le head mesure, la session crédite.
    /// </summary>
    public void CreditStalledTime(double seconds)
    {
        if (seconds <= 0 || InMenu) return;
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

    /// <summary>Recrute un personnage de cette classe si l'or suffit (GDD §6.4).</summary>
    public void Recruit(HeroClass heroClass)
    {
        if (InMenu) return;
        Game.Recruit(heroClass);
    }

    // ── Lecture ──────────────────────────────────────────────────────────

    public MenuSnapshot GetMenuSnapshot() => new(InMenu, _hasSavedGame);

    public HudSnapshot GetHudSnapshot()
    {
        var s = Game.State;
        // Le HUD suit le personnage de départ ; les recrues se lisent sur le champ de bataille.
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
            lead?.Class.ToString() ?? "",
            level,
            lead?.Xp ?? 0,
            level is > 0 and < GameContent.MaxLevel ? GameContent.XpToNextLevel(level) : 0,
            s.Heroes.Count,
            Game.RecruitCost,
            Audio.IsAvailable,
            Audio.Enabled);
    }

    /// <summary>Sauvegarde une dernière fois, puis libère la sortie audio et les stores.</summary>
    public void Dispose()
    {
        try { SaveNow(); }
        catch (Exception ex) { GameLog.Error(nameof(GameSession), nameof(Dispose), ex); }
        Audio.Dispose();
        _store.Dispose();
    }
}
