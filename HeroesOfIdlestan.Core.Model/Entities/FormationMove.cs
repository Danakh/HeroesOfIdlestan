namespace HeroesOfIdlestan.Core.Model.Entities;

/// <summary>
/// Un déplacement de la formation demandé par le joueur (glisser-déposer d'un personnage) : échange
/// avec un autre personnage, ou passage au bout d'une ligne. Il attend le prochain cycle d'action
/// du personnage (<see cref="Entity.NextActionTick"/>), qui le consacre à se déplacer au lieu d'agir.
///
/// <para>Les personnages sont désignés par leur indice dans <c>GameState.Heroes</c> : la liste ne
/// fait que grandir (un mort y reste), un indice désigne donc toujours le même personnage.</para>
/// </summary>
public sealed class FormationMove
{
    /// <summary>Le personnage qui se déplace.</summary>
    public int Hero { get; set; }

    /// <summary>Le personnage avec qui il échange sa place ; null pour un passage au bout de <see cref="Line"/>.</summary>
    public int? SwapWith { get; set; }

    /// <summary>Ligne visée quand <see cref="SwapWith"/> est null : il s'y place au bout (en bas).</summary>
    public int Line { get; set; } = 1;
}
