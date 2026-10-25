using Godot;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;

namespace HeroesOfIdlestan.Core.Render.Godot.Battle;

/// <summary>
/// Placement des combattants dans la scène, en mètres. L'équipe est à gauche (X négatif), les
/// monstres à droite ; la ligne 1 de chaque camp fait face à l'autre au centre, les lignes suivantes
/// s'éloignent, et chaque ligne s'étale en profondeur (Z). Même formation que la version Skia.
/// </summary>
public static class BattleLayout
{
    /// <summary>Distance entre le centre du champ de bataille et la ligne 1 de chaque camp.</summary>
    public const float FrontOffset = 1.6f;
    public const float LineSpacing = 2.2f;
    /// <summary>Écart en profondeur pour une demi-case (deux voisins d'une même ligne sont à 2 demi-cases).</summary>
    public const float HalfSlot = 1.1f;

    /// <summary>Taille du sol : de quoi tenir trois lignes par camp.</summary>
    public static readonly Vector2 GroundSize = new(16f, 9f);

    /// <summary>Position d'un combattant ; null s'il ne fait pas (ou plus) partie de la formation.</summary>
    public static Vector3? PositionOf(Entity fighter, IReadOnlyList<Entity> side, bool isHero)
    {
        // Les vivants se resserrent ; un mort reste là où il est tombé, parmi toute sa ligne.
        var line = fighter.IsAlive
            ? Formation.LineOf(side, fighter.Line)
            : side.Where(f => f.Line == fighter.Line).ToList();
        int index = line.IndexOf(fighter);
        if (index < 0) return null;

        float x = FrontOffset + (fighter.Line - 1) * LineSpacing;
        float z = Formation.HalfPosition(index, line.Count) * HalfSlot;
        return new Vector3(isHero ? -x : x, 0, z);
    }
}
