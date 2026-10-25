namespace HeroesOfIdlestan.Core.Model.Animation;

/// <summary>
/// Habillage : les morceaux SVG accrochés aux os d'un squelette. Plusieurs habillages peuvent viser
/// le même squelette (guerrier, mage…) et partagent alors toutes ses animations.
///
/// <para>Convention des SVG : chaque morceau est dessiné dans le <b>repère du personnage en pose de
/// repos</b> — même viewBox pour tous, origine aux pieds, Y vers le bas. Superposés, les morceaux
/// redonnent le personnage entier. Le moteur déduit seul l'accroche de chaque morceau à son os : aucun
/// pivot à saisir, un illustrateur découpe simplement son dessin en calques.</para>
/// </summary>
public sealed class SkinData
{
    public required string Id { get; init; }

    /// <summary>Id du <see cref="SkeletonData"/> visé.</summary>
    public required string Skeleton { get; init; }

    /// <summary>Morceaux dans l'ordre de dessin, du fond vers l'avant.</summary>
    public required List<AttachmentData> Attachments { get; init; }
}

/// <summary>Un morceau d'habillage : un SVG qui suit un os.</summary>
public sealed class AttachmentData
{
    public required string Bone { get; init; }

    /// <summary>Chemin du SVG, relatif au fichier d'habillage.</summary>
    public required string Svg { get; init; }
}
