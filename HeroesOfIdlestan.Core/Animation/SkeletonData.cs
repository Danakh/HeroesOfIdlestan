using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Animation;

/// <summary>
/// Squelette : une hiérarchie d'os et sa pose de repos (« setup »). Habillages et animations
/// visent un squelette par son <see cref="Id"/>.
///
/// <para>Repère : Y vers le bas (celui de Skia et du SVG), origine aux pieds du personnage, une unité
/// = un pixel à l'échelle 1. Angles en degrés, positifs dans le sens horaire à l'écran. Un os pointe
/// le long de son axe X local.</para>
/// </summary>
public sealed class SkeletonData
{
    public required string Id { get; init; }

    /// <summary>Os dans l'ordre de déclaration : un parent précède toujours ses enfants.</summary>
    public required List<BoneData> Bones { get; init; }

    private Dictionary<string, int>? _indexByName;

    /// <summary>Index de l'os <paramref name="name"/>, ou -1.</summary>
    public int IndexOf(string name)
    {
        _indexByName ??= Bones.Select((b, i) => (b.Name, i)).ToDictionary(p => p.Name, p => p.i);
        return _indexByName.TryGetValue(name, out var i) ? i : -1;
    }

    /// <summary>Index du parent de chaque os (-1 pour une racine), dans l'ordre de <see cref="Bones"/>.</summary>
    public int[] ParentIndices() =>
        Bones.Select(b => b.Parent is null ? -1 : IndexOf(b.Parent)).ToArray();
}

/// <summary>Un os et sa transformation de repos, relative à son parent.</summary>
public sealed class BoneData
{
    public required string Name { get; init; }

    /// <summary>Nom de l'os parent ; absent pour une racine.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; init; }

    public float X { get; init; }
    public float Y { get; init; }
    public float Rotation { get; init; }
    public float ScaleX { get; init; } = 1;
    public float ScaleY { get; init; } = 1;

    /// <summary>Longueur, pour l'affichage de débogage uniquement : elle ne déplace pas les enfants.</summary>
    public float Length { get; init; }
}
