using HeroesOfIdlestan.Core.Meta;
using HeroesOfIdlestan.Core.Model;
using HeroesOfIdlestan.Core.Modifiers;
using HeroesOfIdlestan.Core.Persistence;
using HeroesOfIdlestan.Core.Simulation;
using HeroesOfIdlestan.Core.State;

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
    public AchievementSystem Achievements { get; private set; } = new(SampleContent.Achievements);

    /// <summary>Levé après chaque <see cref="Initialize"/> (nouvelle partie, chargement).</summary>
    public event Action? StateReplaced;

    public GameController(SaveService? saveService = null)
    {
        _saveService = saveService ?? new SaveService();
        Initialize(State);
    }

    // ── Cycle de vie ─────────────────────────────────────────────────────

    public void NewGame(int seed)
    {
        var state = new GameState
        {
            WorldPrng = new GamePrng(seed),
            Prng = new GamePrng(seed ^ 0x5EED),
            Gold = 10,
        };
        state.Entities.Add(new ProducerEntity { X = 0, Y = 0 });
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
        Achievements = new AchievementSystem(SampleContent.Achievements);
        Pipeline = new SimulationPipeline()
            .Add(new ProductionSystem())
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

    // ── Commandes (contenu d'exemple) ────────────────────────────────────

    public long NextProducerCost => SampleContent.ProducerCost(State.Entities.Count);

    public bool TryBuyProducer()
    {
        long cost = NextProducerCost;
        if (State.Gold < cost) return false;
        State.Gold = State.Gold - cost;

        // Position tirée du PRNG de gameplay : jamais de System.Random.
        int count = State.Entities.Count;
        double angle = State.Prng.NextDouble() * Math.PI * 2;
        float radius = 80 + 40 * (float)Math.Sqrt(count);
        State.Entities.Add(new ProducerEntity
        {
            X = (float)(Math.Cos(angle) * radius),
            Y = (float)(Math.Sin(angle) * radius),
            LastProductionTick = State.Clock.CurrentTick,
        });
        State.Events.Add(new GameEvent(State.Clock.CurrentTick, "producer_bought"));
        return true;
    }
}
