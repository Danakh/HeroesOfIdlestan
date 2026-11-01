using HeroesOfIdlestan.Core.Model.Entities;

namespace HeroesOfIdlestan.Core.Controller.Combat;

/// <summary>
/// Géométrie de la formation en pyramide (GDD §3.4) et portées (§4.3). Seuls les vivants comptent :
/// un mort sort de sa ligne, qui se resserre aussitôt.
///
/// <para>Une ligne de <c>n</c> combattants est centrée ; ses positions, en demi-cases, valent
/// −(n−1), −(n−3), …, n−1, dans l'ordre « de haut en bas » : par <see cref="Entity.Rank"/>, puis
/// par ordre de la liste.</para>
/// </summary>
public static class Formation
{
    /// <summary>Les vivants d'une ligne, de haut en bas ; avec <paramref name="includeDead"/>, les morts aussi.</summary>
    public static List<Entity> LineOf(IReadOnlyList<Entity> side, int line, bool includeDead = false)
    {
        var members = new List<Entity>();
        for (int i = 0; i < side.Count; i++)
            if ((includeDead || side[i].IsAlive) && side[i].Line == line) members.Add(side[i]);
        // Tri par insertion, stable : à égalité de rang, l'ordre de la liste.
        for (int i = 1; i < members.Count; i++)
        {
            var member = members[i];
            int j = i - 1;
            while (j >= 0 && members[j].Rank > member.Rank) { members[j + 1] = members[j]; j--; }
            members[j + 1] = member;
        }
        return members;
    }

    // ── Mort et remplacement (GDD §3.2) ──────────────────────────────────

    /// <summary>
    /// Place qu'occupait <paramref name="fallen"/>, qui vient de mourir, dans sa ligne : le nombre de
    /// vivants de la ligne situés au-dessus de lui. À prendre avant de le retirer de la liste.
    /// </summary>
    public static int HoleOf(IReadOnlyList<Entity> side, Entity fallen)
    {
        var line = LineOf(side, fallen.Line, includeDead: true);
        int hole = 0;
        foreach (var member in line)
        {
            if (ReferenceEquals(member, fallen)) break;
            if (member.IsAlive) hole++;
        }
        return hole;
    }

    /// <summary>
    /// Rétablit la règle de la pyramide après une mort (GDD §3.2). La ligne touchée s'est déjà
    /// resserrée (seuls les vivants comptent) ; si elle n'a plus strictement plus de combattants que
    /// la ligne de derrière, un combattant situé derrière le trou avance pour le combler — d'abord
    /// celui dont la ligne préférée est la ligne à combler, puis le plus haut. Son départ laisse à son
    /// tour un trou dans sa ligne : on répète, ligne par ligne, jusqu'à une formation valide.
    ///
    /// <para>Sans trou connu (<paramref name="hole"/> null : formation chargée, début de combat,
    /// arrivée d'un marcheur), tous les combattants de la ligne de derrière sont candidats, et
    /// l'arrivant prend le centre.</para>
    ///
    /// <para>Un combattant encore en marche au tick <paramref name="tick"/> (<see cref="Entity.ArrivalTick"/>)
    /// n'est pas candidat : un mouvement ne franchit qu'une ligne. Faute de candidat, la ligne reste
    /// incomplète ; le prochain appel, à l'arrivée du marcheur, la complète.</para>
    /// </summary>
    /// <returns>Les combattants qui ont avancé, dans l'ordre.</returns>
    public static List<Entity> Settle(IReadOnlyList<Entity> side, int fromLine = 1, int? hole = null, long tick = 0)
    {
        var advanced = new List<Entity>();
        for (int line = Math.Max(1, fromLine); line < DeepestLine(side); line++)
        {
            var front = LineOf(side, line);
            var back = LineOf(side, line + 1);
            int? nextHole = null;
            int moves = 0;
            while (back.Count > 0 && front.Count <= back.Count)
            {
                int slot = Math.Clamp(hole ?? front.Count / 2, 0, front.Count);
                int from = PickAdvancer(back, line, hole.HasValue ? slot : null, tick);
                if (from < 0) break; // tous en marche : la ligne attend leur arrivée
                var advancer = back[from];

                advancer.Line = line;
                front.Insert(slot, advancer);
                for (int i = 0; i < front.Count; i++) front[i].Rank = i;
                back.RemoveAt(from);
                advanced.Add(advancer);

                nextHole = from;
                hole = null; // un deuxième arrivant (formation déjà invalide) n'a plus de trou désigné
                moves++;
            }
            // Le trou se propage à la ligne de derrière seulement si elle a perdu un seul combattant.
            hole = moves == 1 ? nextHole : null;
        }
        return advanced;
    }

    /// <summary>
    /// Index, dans <paramref name="back"/>, du combattant qui avance en ligne <paramref name="line"/>.
    /// Derrière un trou d'une ligne en quinconce, seuls les un ou deux combattants adjacents au trou
    /// sont candidats (indices <c>slot − 1</c> et <c>slot</c>). Ceux encore en marche au tick
    /// <paramref name="tick"/> sont écartés ; −1 s'il ne reste personne.
    /// </summary>
    private static int PickAdvancer(List<Entity> back, int line, int? slot, long tick)
    {
        int first = 0, last = back.Count - 1;
        if (slot is { } s)
        {
            first = Math.Clamp(s - 1, 0, back.Count - 1);
            last = Math.Clamp(s, 0, back.Count - 1);
        }
        int fallback = -1;
        for (int i = first; i <= last; i++)
        {
            if (back[i].ArrivalTick > tick) continue;
            if (back[i].PreferredLine == line) return i;
            if (fallback < 0) fallback = i;
        }
        return fallback;
    }

    private static int DeepestLine(IReadOnlyList<Entity> side)
    {
        int deepest = 0;
        for (int i = 0; i < side.Count; i++)
            if (side[i].IsAlive) deepest = Math.Max(deepest, side[i].Line);
        return deepest;
    }

    // ── Géométrie et portées ─────────────────────────────────────────────

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
        int deepest = DeepestLine(targetSide);
        for (int line = 1; line <= deepest; line++)
        {
            // Chaque ligne parcourue de haut en bas : à égalité, le premier trouvé l'emporte.
            foreach (var candidate in LineOf(targetSide, line))
            {
                int distance = Distance(attacker, attackerSide, candidate, targetSide);
                if (distance < minRange || distance > maxRange) continue;
                int lateral = distance - (attacker.Line + candidate.Line - 1);
                if (best == null || candidate.Line > bestLine || (candidate.Line == bestLine && lateral < bestLateral))
                {
                    best = candidate;
                    bestLine = candidate.Line;
                    bestLateral = lateral;
                }
            }
        }
        return best;
    }
}
