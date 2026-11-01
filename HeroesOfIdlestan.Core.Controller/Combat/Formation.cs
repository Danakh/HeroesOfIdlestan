using HeroesOfIdlestan.Core.Model.Entities;

namespace HeroesOfIdlestan.Core.Controller.Combat;

/// <summary>
/// Géométrie de la formation en pyramide (GDD §3.4) et portées (§4.3). Seuls les vivants comptent
/// pour la règle de la pyramide ; un mort laisse un trou (<see cref="FormationHole"/>) qui garde sa
/// place tant que quelqu'un de derrière doit venir le combler, puis la ligne se resserre.
///
/// <para>Une ligne de <c>n</c> places (combattants et trous) est centrée ; ses positions, en
/// demi-cases, valent −(n−1), −(n−3), …, n−1, dans l'ordre « de haut en bas » : par
/// <see cref="Entity.Rank"/>, puis par ordre de la liste.</para>
/// </summary>
public static class Formation
{
    private static readonly IReadOnlyList<FormationHole> NoHoles = [];

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

    /// <summary>
    /// Les places d'une ligne, de haut en bas : ses vivants et ses trous réservés (entrées nulles).
    /// C'est sur elles que se calculent positions et portées.
    /// </summary>
    public static List<Entity?> Slots(IReadOnlyList<Entity> side, IReadOnlyList<FormationHole>? holes, int line) =>
        SlotsOf(side, holes ?? NoHoles, line).Select(s => s.Fighter).ToList();

    /// <summary>
    /// Place d'un combattant pour l'affichage : son indice parmi les <paramref name="Count"/> places
    /// de sa ligne. Un mort reste sur le trou qu'il a laissé, ou à défaut parmi toute sa ligne, morts
    /// compris. Null s'il ne fait pas partie de la formation.
    /// </summary>
    public static (int Index, int Count)? Place(Entity fighter, IReadOnlyList<Entity> side, IReadOnlyList<FormationHole>? holes)
    {
        holes ??= NoHoles;
        var slots = SlotsOf(side, holes, fighter.Line);
        if (fighter.IsAlive)
        {
            int index = slots.FindIndex(s => ReferenceEquals(s.Fighter, fighter));
            return index < 0 ? null : (index, slots.Count);
        }
        int hole = slots.FindIndex(s => s.Hole is { } h && h.Rank == fighter.Rank);
        if (hole >= 0) return (hole, slots.Count);
        var line = LineOf(side, fighter.Line, includeDead: true);
        int fallback = line.IndexOf(fighter);
        return fallback < 0 ? null : (fallback, line.Count);
    }

    /// <summary>Le plus grand nombre de places d'une ligne (combattants vivants et trous).</summary>
    public static int WidestLine(IReadOnlyList<Entity> side, IReadOnlyList<FormationHole>? holes)
    {
        int widest = 0;
        for (int line = 1; line <= DeepestLine(side, holes ?? NoHoles); line++)
            widest = Math.Max(widest, SlotsOf(side, holes ?? NoHoles, line).Count);
        return widest;
    }

    // ── Mort et remplacement (GDD §3.2) ──────────────────────────────────

    /// <summary>
    /// <paramref name="fallen"/> vient de mourir : sa place devient un trou réservé. À appeler avant
    /// de le retirer de la liste, puis <see cref="Settle"/>.
    /// </summary>
    public static void Fall(IReadOnlyList<Entity> side, List<FormationHole> holes, Entity fallen)
    {
        // Les rangs de la ligne, renumérotés de haut en bas, le mort compris : son trou a un rang à lui.
        var slots = SlotsOf(side, holes, fallen.Line, fallen);
        Renumber(slots);
        holes.Add(new FormationHole { Line = fallen.Line, Rank = fallen.Rank });
    }

    /// <summary>
    /// Rétablit la règle de la pyramide (GDD §3.2), ligne par ligne depuis le front. Une ligne qui a
    /// encore strictement plus de combattants que la ligne de derrière se resserre (ses trous
    /// disparaissent). Sinon, quelqu'un de derrière doit avancer : celui qui est derrière un trou
    /// (le plus en face, puis la ligne préférée, puis le plus haut) vient l'occuper, et ses voisins
    /// ne bougent pas ; sa place quittée devient à son tour un trou, traité à la ligne suivante. Une
    /// ligne sans trou (formation chargée, début de combat) reçoit l'arrivant au centre.
    ///
    /// <para>Un combattant encore en marche au tick <paramref name="tick"/> (<see cref="Entity.ArrivalTick"/>)
    /// ne peut pas avancer : un mouvement ne franchit qu'une ligne. Si c'est lui qui est derrière le
    /// trou, le trou attend son arrivée, et le prochain appel le comble.</para>
    /// </summary>
    /// <returns>Les combattants qui ont avancé, dans l'ordre.</returns>
    public static List<Entity> Settle(IReadOnlyList<Entity> side, List<FormationHole> holes, long tick = 0)
    {
        var advanced = new List<Entity>();
        for (int line = 1; line <= DeepestLine(side, holes); line++)
        {
            while (true)
            {
                var front = SlotsOf(side, holes, line);
                var back = SlotsOf(side, holes, line + 1);
                Renumber(front);
                Renumber(back);
                int occupants = front.Count(s => s.Fighter != null);
                int behind = back.Count(s => s.Fighter != null);

                if (behind == 0 || occupants > behind)
                {
                    // Personne n'a besoin d'avancer : la ligne se resserre.
                    if (holes.RemoveAll(h => h.Line == line) > 0) Renumber(SlotsOf(side, holes, line));
                    break;
                }

                Entity? advancer = null;
                FormationHole? target = null;
                if (front.Any(s => s.Hole != null))
                {
                    for (int i = 0; i < front.Count && advancer == null; i++)
                        if (front[i].Hole is { } hole && PickBehind(front, i, back, line, tick) is { } pick)
                            (advancer, target) = (pick, hole);
                }
                else
                {
                    advancer = PickAny(back, line, tick);
                }
                if (advancer == null) break; // le candidat marche encore : le trou attend son arrivée

                holes.Add(new FormationHole { Line = line + 1, Rank = advancer.Rank });
                advancer.Line = line;
                if (target != null)
                {
                    advancer.Rank = target.Rank;
                    holes.Remove(target);
                }
                else
                {
                    int center = occupants / 2;
                    foreach (var slot in front)
                        if (slot.Fighter is { } f && f.Rank >= center) f.Rank++;
                    advancer.Rank = center;
                }
                advanced.Add(advancer);
            }
        }
        return advanced;
    }

    /// <summary>
    /// Celui de la ligne de derrière qui vient occuper le trou d'indice <paramref name="hole"/> : parmi
    /// les plus en face du trou, d'abord la ligne préférée, puis le plus haut. Null si ceux-là sont
    /// tous encore en marche.
    /// </summary>
    private static Entity? PickBehind(List<Slot> front, int hole, List<Slot> back, int line, long tick)
    {
        int holePosition = HalfPosition(hole, front.Count);
        int nearest = int.MaxValue;
        for (int j = 0; j < back.Count; j++)
            if (back[j].Fighter != null)
                nearest = Math.Min(nearest, Math.Abs(HalfPosition(j, back.Count) - holePosition));

        Entity? first = null;
        for (int j = 0; j < back.Count; j++)
        {
            if (back[j].Fighter is not { } candidate) continue;
            if (Math.Abs(HalfPosition(j, back.Count) - holePosition) != nearest) continue;
            if (candidate.ArrivalTick > tick) continue;
            if (candidate.PreferredLine == line) return candidate;
            first ??= candidate;
        }
        return first;
    }

    /// <summary>Sans trou désigné : n'importe qui de la ligne de derrière, la ligne préférée d'abord, puis le plus haut.</summary>
    private static Entity? PickAny(List<Slot> back, int line, long tick)
    {
        Entity? first = null;
        foreach (var slot in back)
        {
            if (slot.Fighter is not { } candidate || candidate.ArrivalTick > tick) continue;
            if (candidate.PreferredLine == line) return candidate;
            first ??= candidate;
        }
        return first;
    }

    private readonly record struct Slot(Entity? Fighter, FormationHole? Hole);

    /// <summary>Places d'une ligne de haut en bas ; <paramref name="extra"/> y est compté même mort.</summary>
    private static List<Slot> SlotsOf(IReadOnlyList<Entity> side, IReadOnlyList<FormationHole> holes, int line, Entity? extra = null)
    {
        var slots = new List<Slot>();
        foreach (var fighter in LineOf(side, line, includeDead: true))
            if (fighter.IsAlive || ReferenceEquals(fighter, extra)) slots.Add(new Slot(fighter, null));
        foreach (var hole in holes)
            if (hole.Line == line) slots.Add(new Slot(null, hole));
        // Tri par insertion, stable : à égalité de rang, les combattants avant les trous.
        for (int i = 1; i < slots.Count; i++)
        {
            var slot = slots[i];
            int j = i - 1;
            while (j >= 0 && RankOf(slots[j]) > RankOf(slot)) { slots[j + 1] = slots[j]; j--; }
            slots[j + 1] = slot;
        }
        return slots;
    }

    private static int RankOf(Slot slot) => slot.Fighter?.Rank ?? slot.Hole!.Rank;

    private static void Renumber(List<Slot> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Fighter is { } fighter) fighter.Rank = i;
            else slots[i].Hole!.Rank = i;
        }
    }

    private static int DeepestLine(IReadOnlyList<Entity> side, IReadOnlyList<FormationHole> holes)
    {
        int deepest = 0;
        for (int i = 0; i < side.Count; i++)
            if (side[i].IsAlive) deepest = Math.Max(deepest, side[i].Line);
        foreach (var hole in holes) deepest = Math.Max(deepest, hole.Line);
        return deepest;
    }

    // ── Géométrie et portées ─────────────────────────────────────────────

    /// <summary>Position en demi-cases du <paramref name="index"/>-ième combattant d'une ligne de <paramref name="count"/>.</summary>
    public static int HalfPosition(int index, int count) => 2 * index - (count - 1);

    /// <summary>
    /// Distance d'un attaquant à une cible du camp adverse (GDD §4.3) : ligne de l'attaquant + ligne
    /// de la cible − 1, plus l'écart latéral (en places, trous compris) entre la cible et l'adversaire
    /// le plus en face. Quand deux adversaires sont autant en face (lignes en quinconce), l'écart se
    /// compte depuis le plus proche des deux (GDD §8, question ouverte). La distance n'est pas réciproque.
    /// </summary>
    public static int Distance(Entity attacker, IReadOnlyList<Entity> attackerSide, Entity target, IReadOnlyList<Entity> targetSide,
        IReadOnlyList<FormationHole>? attackerHoles = null, IReadOnlyList<FormationHole>? targetHoles = null)
    {
        var mine = Slots(attackerSide, attackerHoles, attacker.Line);
        int attackerPosition = HalfPosition(mine.IndexOf(attacker), mine.Count);

        var theirs = Slots(targetSide, targetHoles, target.Line);
        int targetIndex = theirs.IndexOf(target);

        int bestFacing = int.MaxValue;
        int lateral = int.MaxValue;
        for (int i = 0; i < theirs.Count; i++)
        {
            if (theirs[i] == null) continue;
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
    public static Entity? ChooseTarget(Entity attacker, IReadOnlyList<Entity> attackerSide, IReadOnlyList<Entity> targetSide, int minRange, int maxRange,
        IReadOnlyList<FormationHole>? attackerHoles = null, IReadOnlyList<FormationHole>? targetHoles = null)
    {
        Entity? best = null;
        int bestLine = 0, bestLateral = 0;
        int deepest = DeepestLine(targetSide, NoHoles);
        for (int line = 1; line <= deepest; line++)
        {
            // Chaque ligne parcourue de haut en bas : à égalité, le premier trouvé l'emporte.
            foreach (var candidate in LineOf(targetSide, line))
            {
                int distance = Distance(attacker, attackerSide, candidate, targetSide, attackerHoles, targetHoles);
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
