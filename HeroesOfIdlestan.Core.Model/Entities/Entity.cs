using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Model.Entities;

/// <summary>
/// Discriminant des entités. Sérialisé par nom, comme tout enum persisté : un entier se décalerait
/// silencieusement à l'insertion d'une valeur et corromprait les sauvegardes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntityKind>))]
public enum EntityKind
{
    Producer,
}

/// <summary>
/// Base des entités du monde. Chaque <see cref="EntityKind"/> doit être enregistré dans
/// <see cref="EntityRegistry"/>, seule table type → constructeur (lue par la fabrique et par la
/// sérialisation). <c>InvariantTests</c> échoue s'il en manque un.
/// </summary>
public abstract class Entity
{
    [JsonIgnore]
    public abstract EntityKind Kind { get; }

    /// <summary>Position dans le monde, en unités monde.</summary>
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>Exemple — produit de l'or à intervalle régulier.</summary>
public sealed class ProducerEntity : Entity
{
    public override EntityKind Kind => EntityKind.Producer;

    public int Level { get; set; } = 1;

    /// <summary>Tick du dernier cycle de production consommé (voir <c>TickCooldown</c>).</summary>
    public long LastProductionTick { get; set; }
}
