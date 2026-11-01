namespace HeroesOfIdlestan.Core.Model.Entities;

/// <summary>
/// Place vide réservée dans une ligne de la formation (GDD §3.2) : celle d'un mort, ou celle qu'a
/// quittée un combattant parti combler un trou devant lui. Tant que quelqu'un de la ligne de derrière
/// doit venir l'occuper, la ligne ne se resserre pas : ses combattants ne bougent pas, et celui qui
/// avance marche jusqu'à cette place. Il peut devoir attendre qu'un candidat termine sa marche.
/// </summary>
public sealed class FormationHole
{
    /// <summary>Ligne du trou, 1 = le front.</summary>
    public int Line { get; set; } = 1;

    /// <summary>Rang du trou dans sa ligne, dans la même numérotation que <see cref="Entity.Rank"/>.</summary>
    public int Rank { get; set; }
}
