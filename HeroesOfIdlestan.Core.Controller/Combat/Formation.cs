using HeroesOfIdlestan.Core.Model.Entities;

namespace HeroesOfIdlestan.Core.Controller.Combat;

/// <summary>
/// Géométrie de la formation en pyramide (GDD §3.4) et portées (§4.3). Seuls les vivants comptent :
/// un mort sort de sa ligne, qui se resserre aussitôt.
///
/// <para>Une ligne de <c>n</c> combattants est centrée ; ses positions, en demi-cases, valent
/// −(n−1), −(n−3), …, n−1, dans l'ordre de la liste (« de haut en bas »).</para>
/// </summary>
public static class Formation
{
    /// <summary>Les vivants d'une ligne, de haut en bas.</summary>
    public static List<Entity> LineOf(IReadOnlyList<Entity> side, int line)
    {
        var members = new List<Entity>();
        for (int i = 0; i < side.Count; i++)
            if (side[i].IsAlive && side[i].Line == line) members.Add(side[i]);
        return members;
    }

    /// <summary>Position en demi-cases du <paramref name="index"/>-ième combattant d'une ligne de <paramref name="count"/>.</summary>
    public static int HalfPosition(int index, int count) => 2 * index - (count - 1);

    /// <summary>
    /// Distance d'un attaquant à une cible du camp adverse (GDD §4.3) : ligne de l'attaquant + ligne
    /// de la cible − 1, plus l'écart latéral entre la cible et l'adversaire le plus en face. Quand deux
    /// adversaires sont autant en face (lignes en quinconce), l'écart se compte depuis le plus proche
    /// des deux (GDD §8, question ouverte). La distance n'est pas réciproque.
    /// </summary>
    public static int Distance(Entity attacker, IReadOnlyList<Entity> attackerSide, Entity target, IReadOnlyList<Entity> targetSide)
    {
        var mine = LineOf(attackerSide, attacker.Line);
        int attackerPosition = HalfPosition(mine.IndexOf(attacker), mine.Count);

        var theirs = LineOf(targetSide, target.Line);
        int targetIndex = theirs.IndexOf(target);

        int bestFacing = int.MaxValue;
        int lateral = int.MaxValue;
        for (int i = 0; i < theirs.Count; i++)
        {
            int facing = Math.Abs(HalfPosition(i, theirs.Count) - attackerPosition);
            int gap = Math.Abs(targetIndex - i);
            if (facing < bestFacing) { bestFacing = facing; lateral = gap; }
            else if (facing == bestFacing) lateral = Math.Min(lateral, gap);
        }
        return attacker.Line + target.Line - 1 + lateral;
    }

    /// <summary>
    /// Ciblage automatique (GDD §4.4) : parmi les adversaires à portée, la plus grande distance de
    /// base (la ligne la plus lointaine), puis le plus en face, puis l'ordre de haut en bas.
    /// Null si personne n'est à portée.
    /// </summary>
    public static Entity? ChooseTarget(Entity attacker, IReadOnlyList<Entity> attackerSide, IReadOnlyList<Entity> targetSide, int minRange, int maxRange)
    {
        Entity? best = null;
        int bestLine = 0, bestLateral = 0;
        for (int i = 0; i < targetSide.Count; i++)
        {
            var candidate = targetSide[i];
            if (!candidate.IsAlive) continue;
            int distance = Distance(attacker, attackerSide, candidate, targetSide);
            if (distance < minRange || distance > maxRange) continue;
            int lateral = distance - (attacker.Line + candidate.Line - 1);
            // Liste parcourue de haut en bas : à égalité, le premier trouvé l'emporte.
            if (best == null || candidate.Line > bestLine || (candidate.Line == bestLine && lateral < bestLateral))
            {
                best = candidate;
                bestLine = candidate.Line;
                bestLateral = lateral;
            }
        }
        return best;
    }
}
