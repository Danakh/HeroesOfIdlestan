using HeroesOfIdlestan.Core.Controller.Meta;
using HeroesOfIdlestan.Core.Controller.Simulation;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller.Content;

/// <summary>
/// Contenu d'exemple du squelette : des producteurs qui rapportent de l'or. À supprimer avec
/// <see cref="ProducerEntity"/> quand le vrai jeu arrive — il n'existe que pour que le canvas tourne,
/// se sauvegarde et produise des fixtures.
/// </summary>
public static class SampleContent
{
    public const string ProductionModifier = "production";
    public const long ProductionCooldownTicks = GameClock.TicksPerSecond;

    public static long ProducerCost(int producerCount) => 10L * (producerCount + 1) * (producerCount + 1);

    public static readonly IReadOnlyList<AchievementDefinition> Achievements =
    [
        new("first_producer", s => s.Entities.Count >= 2),
        new("gold_1000", s => s.Gold >= 1000),
    ];
}

/// <summary>Chaque producteur rapporte <c>Level</c> or par seconde de jeu, modifié par <c>"production"</c>.</summary>
public sealed class ProductionSystem : ISimulationSystem
{
    public string Name => "Production";

    public void Run(SimulationContext context)
    {
        var state = context.State;
        long gained = 0;
        for (int i = 0; i < state.Entities.Count; i++)
        {
            if (state.Entities[i] is not ProducerEntity producer) continue;
            long last = producer.LastProductionTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(context.CurrentTick, ref last, SampleContent.ProductionCooldownTicks);
            producer.LastProductionTick = last;
            if (cycles == 0) continue;
            double perCycle = context.Modifiers.Apply(SampleContent.ProductionModifier, producer.Level);
            gained += (long)(perCycle * cycles);
        }
        if (gained > 0) state.Gold = state.Gold + gained;
    }
}
