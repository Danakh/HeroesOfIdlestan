using HeroesOfIdlestan.Core.Controller.Modifiers;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Meta;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller.Simulation;

/// <summary>Ce que voit un système pendant un passage du pipeline.</summary>
public sealed class SimulationContext
{
    public required GameState State { get; init; }
    public required ModifierAggregator Modifiers { get; init; }
    public long PreviousTick { get; init; }
    public long CurrentTick { get; init; }
    public long ElapsedTicks => CurrentTick - PreviousTick;
}

/// <summary>
/// Une étape de la simulation. Un système périodique doit compter ses cycles avec
/// <see cref="TickCooldown.ConsumeElapsedCycles"/>, jamais par un simple <c>if</c> : un passage peut
/// couvrir des milliers de ticks pendant un saut de temps.
/// </summary>
public interface ISimulationSystem
{
    /// <summary>Nom stable, unique dans le pipeline : il sert au journal d'erreurs et aux tests d'ordre.</summary>
    string Name { get; }

    void Run(SimulationContext context);
}

/// <summary>
/// Ordre d'exécution explicite des systèmes. Dans SettlersOfIdlestan cet ordre découlait de l'ordre
/// d'abonnement à un événement : déplacer une ligne compilait, passait les tests isolés et changeait le
/// jeu en silence. Ici l'ordre est la liste elle-même, et <see cref="Names"/> se teste.
///
/// <para>Chaque système est isolé par un try/catch : une exception ne prive pas les suivants de leur
/// passage ; elle est consignée dans <see cref="GameLog"/>.</para>
/// </summary>
public sealed class SimulationPipeline
{
    private readonly List<ISimulationSystem> _systems = [];

    public IReadOnlyList<string> Names => _systems.Select(s => s.Name).ToList();

    public SimulationPipeline Add(ISimulationSystem system)
    {
        if (_systems.Any(s => s.Name == system.Name))
            throw new InvalidOperationException($"Système déjà présent dans le pipeline : {system.Name}.");
        _systems.Add(system);
        return this;
    }

    public void Run(SimulationContext context)
    {
        for (int i = 0; i < _systems.Count; i++)
        {
            var system = _systems[i];
            try
            {
                system.Run(context);
            }
            catch (Exception ex)
            {
                GameLog.Error(system.Name, nameof(Run), ex);
            }
        }
    }
}
