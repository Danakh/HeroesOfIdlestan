using HeroesOfIdlestan.Core.Simulation;
using HeroesOfIdlestan.Core.State;

namespace HeroesOfIdlestan.Core.Meta;

/// <summary>Un succès : identifiant stable (aussi celui du store) et condition d'obtention.</summary>
public sealed record AchievementDefinition(string Id, Func<GameState, bool> IsReached);

/// <summary>
/// Évalue les succès en fin de passage du pipeline. Un succès obtenu est inscrit dans
/// <see cref="GameState.CompletedAchievements"/> et signalé une seule fois par <see cref="Unlocked"/> ;
/// le <c>StoreController</c> s'y abonne et renvoie tout l'historique au lancement, pour rattraper un
/// store qui aurait été déconnecté au moment du déblocage.
/// </summary>
public sealed class AchievementSystem(IReadOnlyList<AchievementDefinition> definitions) : ISimulationSystem
{
    public string Name => "Achievements";

    public IReadOnlyList<AchievementDefinition> Definitions { get; } = definitions;

    public event Action<string>? Unlocked;

    public void Run(SimulationContext context)
    {
        var completed = context.State.CompletedAchievements;
        foreach (var definition in Definitions)
        {
            if (completed.Contains(definition.Id) || !definition.IsReached(context.State)) continue;
            completed.Add(definition.Id);
            context.State.Events.Add(new GameEvent(context.CurrentTick, "achievement_unlocked", definition.Id));
            Unlocked?.Invoke(definition.Id);
        }
    }
}
