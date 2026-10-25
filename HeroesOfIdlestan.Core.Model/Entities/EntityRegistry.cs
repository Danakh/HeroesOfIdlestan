using HeroesOfIdlestan.Core.Model.Persistence;

namespace HeroesOfIdlestan.Core.Model.Entities;

/// <summary>Seul endroit où un nouveau type d'entité s'enregistre.</summary>
public static class EntityRegistry
{
    public static readonly PolymorphicRegistry<EntityKind, Entity> Instance = new PolymorphicRegistry<EntityKind, Entity>()
        .Register<ProducerEntity>(EntityKind.Producer);
}
