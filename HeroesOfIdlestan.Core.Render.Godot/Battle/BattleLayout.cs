using Godot;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

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

    /// <summary>
    /// Le sol, allongé en profondeur pour tenir la ligne la plus longue des deux camps, plus une place
    /// côté équipe : celle proposée au bout d'une ligne pendant un glisser-déposer.
    /// </summary>
    public static Vector2 GroundSizeFor(GameState state)
    {
        int widest = Math.Max(Formation.WidestLine(state.Heroes, state.HeroHoles) + 1, Formation.WidestLine(state.Monsters, state.MonsterHoles));
        float depth = Math.Max(GroundSize.Y, 2 * ((widest - 1) * HalfSlot + 1.5f));
        return new Vector2(GroundSize.X, depth);
    }

    /// <summary>Position d'un combattant ; null s'il ne fait pas (ou plus) partie de la formation.</summary>
    public static Vector3? PositionOf(Entity fighter, IReadOnlyList<Entity> side, IReadOnlyList<FormationHole> holes, bool isHero)
    {
        // Les places de la ligne, trous réservés compris : un mort reste là où il est tombé.
        if (Formation.Place(fighter, side, holes) is not (int index, int count)) return null;
        return SlotPosition(fighter.Line, index, count, isHero);
    }

    /// <summary>Position de la place <paramref name="index"/> parmi <paramref name="count"/> d'une ligne.</summary>
    public static Vector3 SlotPosition(int line, int index, int count, bool isHero)
    {
        float x = FrontOffset + (line - 1) * LineSpacing;
        float z = Formation.HalfPosition(index, count) * HalfSlot;
        return new Vector3(isHero ? -x : x, 0, z);
    }

    /// <summary>Position d'une place proposée au glisser-déposer.</summary>
    public static Vector3 TargetPosition(FormationTarget target) =>
        SlotPosition(target.Line, target.Index, target.Count, isHero: true);
}
