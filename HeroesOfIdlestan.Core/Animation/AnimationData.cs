using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Animation;

/// <summary>
/// Animation d'un squelette : par os, des pistes de clés exprimées en <b>décalage</b> par rapport à
/// la pose de repos — rotation et translation s'ajoutent, l'échelle se multiplie. Un os sans piste
/// reste au repos, ce qui garde les animations indépendantes du squelette exact.
///
/// <para>Entre deux clés, la courbe de la clé de départ s'applique. Avant la première clé et après
/// la dernière, la valeur reste celle de la clé la plus proche : une boucle propre répète sa
/// première clé à <c>time = duration</c>.</para>
/// </summary>
public sealed class AnimationData
{
    public required string Id { get; init; }

    /// <summary>Id du <see cref="SkeletonData"/> visé.</summary>
    public required string Skeleton { get; init; }

    /// <summary>Durée en secondes.</summary>
    public required float Duration { get; init; }

    public bool Loop { get; init; }

    /// <summary>Pistes par nom d'os.</summary>
    public Dictionary<string, BoneTimeline> Bones { get; init; } = [];

    /// <summary>Instants remarquables (impact d'une attaque, pose d'un pied…), triés par temps.</summary>
    public List<AnimationEvent> Events { get; init; } = [];
}

/// <summary>Les pistes d'un os. Chacune est facultative.</summary>
public sealed class BoneTimeline
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RotateKey>? Rotate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VectorKey>? Translate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VectorKey>? Scale { get; init; }
}

public interface IKeyframe
{
    float Time { get; }
    Curve Curve { get; }
}

/// <summary>Décalage de rotation, en degrés.</summary>
public sealed record RotateKey(
    float Time, float Angle,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Curve Curve = Curve.Linear) : IKeyframe;

/// <summary>Décalage de translation (additif) ou facteur d'échelle (multiplicatif).</summary>
public sealed record VectorKey(
    float Time, float X, float Y,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Curve Curve = Curve.Linear) : IKeyframe;

/// <summary>Événement nommé, émis par le lecteur quand la lecture franchit <see cref="Time"/>.</summary>
public sealed record AnimationEvent(float Time, string Name);

/// <summary>Interpolation d'une clé vers la suivante.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Curve>))]
public enum Curve
{
    Linear,

    /// <summary>Garde la valeur jusqu'à la clé suivante.</summary>
    Step,

    /// <summary>Accélère puis ralentit (smoothstep).</summary>
    Smooth,
}
