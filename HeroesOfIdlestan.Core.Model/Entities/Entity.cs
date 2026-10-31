using System.Text.Json.Serialization;
using HeroesOfIdlestan.Core.Model.Obfuscation;

namespace HeroesOfIdlestan.Core.Model.Entities;

/// <summary>
/// Discriminant des entités. Sérialisé par nom, comme tout enum persisté : un entier se décalerait
/// silencieusement à l'insertion d'une valeur et corromprait les sauvegardes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntityKind>))]
public enum EntityKind
{
    Hero,
    Monster,
}

/// <summary>Classe d'un personnage (GDD §5.2). Seul le Guerrier existe pour l'instant.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeroClass>))]
public enum HeroClass
{
    Warrior,
}

/// <summary>Espèce d'un monstre : ses statistiques et son ciblage en découlent (GDD §4.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MonsterSpecies>))]
public enum MonsterSpecies
{
    Rat,
    Goblin,
}

/// <summary>
/// Base des combattants, des deux camps. Chaque <see cref="EntityKind"/> doit être enregistré dans
/// <see cref="EntityRegistry"/>, seule table type → constructeur (lue par la fabrique et par la
/// sérialisation). <c>InvariantTests</c> échoue s'il en manque un.
///
/// <para>Les statistiques (PV max, dégâts, cadence) ne sont pas persistées : elles se déduisent de la
/// classe et du niveau, ou de l'espèce et de la zone (<c>GameContent</c>).</para>
/// </summary>
public abstract class Entity
{
    [JsonIgnore]
    public abstract EntityKind Kind { get; }

    /// <summary>Ligne de la formation, 1 = le front (GDD §3.1). Change en combat quand le combattant avance (GDD §3.2).</summary>
    public int Line { get; set; } = 1;

    /// <summary>
    /// Rang dans la ligne, pour l'ordre « de haut en bas » : à égalité de rang, l'ordre de la liste.
    /// Renuméroté quand un combattant avance et s'insère dans le trou laissé par un mort (GDD §3.2).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>Ligne préférée (GDD §3.3) : prioritaire pour avancer combler cette ligne.</summary>
    public int PreferredLine { get; set; } = 1;

    /// <summary>Points de vie restants ; 0 = mort.</summary>
    public ObfLong Health { get; set; }

    /// <summary>Tick de la prochaine action. Persisté : une partie rechargée reprend au même rythme.</summary>
    public long NextActionTick { get; set; }

    /// <summary>
    /// Tick de la dernière action réellement faite (une cible touchée) ; null si aucune. Un combattant
    /// sans action possible depuis sa ligne, ou sans personne à portée, ne fait rien (GDD §5.1) : le
    /// rendu ne lui joue donc aucune attaque.
    /// </summary>
    public long? LastActionTick { get; set; }

    [JsonIgnore]
    public bool IsAlive => Health > 0;
}

/// <summary>Un personnage de l'équipe du joueur.</summary>
public sealed class HeroEntity : Entity
{
    public override EntityKind Kind => EntityKind.Hero;

    public HeroClass Class { get; set; }

    /// <summary>
    /// Ligne où le joueur a placé le personnage (GDD §3.1). Les avancées d'un combat ne la changent
    /// pas : l'équipe relevée après une défaite y reprend sa place.
    /// </summary>
    public int FormationLine { get; set; } = 1;

    /// <summary>Niveau dans la classe, de 1 au niveau maximum (GDD §5.4).</summary>
    public int Level { get; set; } = 1;

    /// <summary>Expérience accumulée vers le niveau suivant.</summary>
    public ObfLong Xp { get; set; }
}

/// <summary>Un monstre du groupe adverse.</summary>
public sealed class MonsterEntity : Entity
{
    public override EntityKind Kind => EntityKind.Monster;

    public MonsterSpecies Species { get; set; }
}
