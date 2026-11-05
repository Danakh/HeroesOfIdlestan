using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Controller.Meta;
using HeroesOfIdlestan.Core.Controller.Modifiers;
using HeroesOfIdlestan.Core.Controller.Simulation;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Meta;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller;

/// <summary>
/// Contrôleur racine : possède l'état, construit le pipeline et les agrégats runtime, expose les
/// commandes. La couche de rendu et l'UI ne modifient jamais le modèle autrement que par ici.
/// </summary>
public sealed class GameController
{
    private readonly SaveService _saveService;

    public GameState State { get; private set; } = new();
    public SimulationPipeline Pipeline { get; private set; } = new();
    public ModifierAggregator Modifiers { get; private set; } = new();
    public AchievementSystem Achievements { get; private set; } = new(GameContent.Achievements);

    /// <summary>Levé après chaque <see cref="Initialize"/> (nouvelle partie, chargement).</summary>
    public event Action? StateReplaced;

    public GameController(SaveService? saveService = null)
    {
        _saveService = saveService ?? new SaveService();
        Initialize(State);
    }

    // ── Cycle de vie ─────────────────────────────────────────────────────

    /// <summary>Nouvelle partie : un guerrier seul, en zone 1, déjà au combat (GDD §3.1).</summary>
    public void NewGame(int seed) => Start(seed, formation: [1], debug: false);

    /// <summary>
    /// Partie de debug : <see cref="GameContent.DebugFighterCount"/> guerriers contre autant de
    /// gobelins à chaque zone, les deux camps en <see cref="GameContent.DebugFormation"/>.
    /// </summary>
    public void NewDebugGame(int seed) => Start(seed, GameContent.DebugFormation, debug: true);

    /// <summary>Remet le contrôleur dans l'état vide du lancement : aucune équipe, rien ne s'y passe.</summary>
    public void Clear() => Initialize(new GameState());

    /// <param name="formation">Nombre de guerriers par ligne, de la ligne 1 à la dernière.</param>
    private void Start(int seed, IReadOnlyList<int> formation, bool debug)
    {
        var state = new GameState
        {
            WorldPrng = new GamePrng(seed),
            Prng = new GamePrng(seed ^ 0x5EED),
        };
        state.Run.Debug = debug;
        foreach (int line in GameContent.LinesOf(formation))
        {
            var warrior = new HeroEntity { Class = HeroClass.Warrior, Line = line, FormationLine = line, PreferredLine = line };
            warrior.Health = GameContent.HeroStats(warrior.Class, warrior.Level).MaxHealth;
            state.Heroes.Add(warrior);
        }
        CombatSystem.StartFight(state, state.Clock.CurrentTick);
        Initialize(state);
    }

    /// <summary>Charge une sauvegarde (formats acceptés : voir <see cref="SaveCodec"/>).</summary>
    public void Load(string content) => Initialize(_saveService.Import(content));

    /// <summary>Sauvegarde encodée. Pose <see cref="GameClock.LastSaveTime"/> : c'est une commande, pas une lecture.</summary>
    public string Save(DateTimeOffset now)
    {
        State.Clock.LastSaveTime = now;
        State.Clock.WasPausedAtSave = State.Clock.IsPaused;
        return _saveService.Export(State);
    }

    /// <summary>Export sans effet de bord sur l'horloge — pour les tests et les fixtures.</summary>
    public string Export() => _saveService.Export(State);

    /// <summary>
    /// Câble l'état : pipeline, modificateurs, succès. Tout ce qui est dérivé est reconstruit ici, donc
    /// un état chargé et un état neuf passent par le même chemin.
    /// </summary>
    private void Initialize(GameState state)
    {
        State = state;
        Modifiers = new ModifierAggregator();
        Achievements = new AchievementSystem(GameContent.Achievements);
        Pipeline = new SimulationPipeline()
            .Add(new CombatSystem())
            .Add(Achievements);
        if (state.Clock.WasPausedAtSave) state.Clock.Pause();
        StateReplaced?.Invoke();
    }

    // ── Temps ────────────────────────────────────────────────────────────

    /// <summary>Avance en temps réel (boucle de jeu).</summary>
    public void Tick(DateTimeOffset now)
    {
        long previous = State.Clock.CurrentTick;
        if (State.Clock.Advance(now) > 0) RunPipeline(previous);
    }

    /// <summary>
    /// Avance de <paramref name="ticks"/> ticks par tranches de <paramref name="chunkTicks"/>, sans
    /// temps réel : tests, scénarios, sauts de temps. Changer la taille de tranche peut changer le
    /// résultat d'un système mal écrit — d'où <see cref="TickCooldown"/>.
    /// </summary>
    public void SimulateTicks(long ticks, long chunkTicks = 100)
    {
        if (chunkTicks <= 0) throw new ArgumentOutOfRangeException(nameof(chunkTicks));
        while (ticks > 0)
        {
            long chunk = Math.Min(chunkTicks, ticks);
            long previous = State.Clock.CurrentTick;
            State.Clock.CurrentTick += chunk;
            RunPipeline(previous);
            ticks -= chunk;
        }
    }

    private void RunPipeline(long previousTick) =>
        Pipeline.Run(new SimulationContext
        {
            State = State,
            Modifiers = Modifiers,
            PreviousTick = previousTick,
            CurrentTick = State.Clock.CurrentTick,
        });

    // ── Commandes ────────────────────────────────────────────────────────

    /// <summary>Active ou coupe l'avance automatique (GDD §2). Coupée, l'équipe rejoue sa zone en boucle.</summary>
    public void SetAutoAdvance(bool enabled) => State.Run.AutoAdvance = enabled;

    /// <summary>Prix de la prochaine recrue (GDD §6.4).</summary>
    public long RecruitCost => GameContent.RecruitCost(State.Heroes.Count);

    /// <summary>
    /// Recrute un personnage de la classe <paramref name="heroClass"/> contre de l'or. Le GDD réserve
    /// le recrutement à l'entre-deux-runs ; tant que le prestige n'existe pas, il se fait en cours de
    /// run. Faux si l'or manque ou si aucune partie n'est en cours.
    /// </summary>
    public bool Recruit(HeroClass heroClass)
    {
        if (State.Heroes.Count == 0) return false;
        long cost = RecruitCost;
        if (State.Gold < cost) return false;
        State.Gold = State.Gold - cost;
        FormationOrders.Recruit(State, heroClass, State.Clock.CurrentTick);
        State.Events.Add(new GameEvent(State.Clock.CurrentTick, "hero_recruited", heroClass.ToString()));
        return true;
    }

    /// <summary>
    /// Glisser-déposer : le personnage d'indice <paramref name="hero"/> ira sur <paramref name="target"/>
    /// à son prochain cycle d'action. Faux si la place n'est pas proposée (<see cref="FormationOrders.DropTargets"/>).
    /// </summary>
    public bool OrderFormationMove(int hero, FormationTarget target) => FormationOrders.Order(State, hero, target);

    /// <summary>Annule le déplacement demandé pour le personnage <paramref name="hero"/>.</summary>
    public void CancelFormationMove(int hero) => FormationOrders.Cancel(State, hero);
}
