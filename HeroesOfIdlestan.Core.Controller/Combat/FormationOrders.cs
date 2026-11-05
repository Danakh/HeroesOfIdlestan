using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller.Combat;

/// <summary>
/// Une place où déposer un personnage glissé : celle d'un autre personnage (échange), ou le bout
/// d'une ligne (<see cref="SwapWith"/> null). <see cref="Index"/> et <see cref="Count"/> la situent
/// parmi les places de sa ligne, comme <see cref="Formation.Place"/> : chaque pile en tire sa position
/// (<see cref="Formation.HalfPosition"/>). Le bout d'une ligne de n places vaut (n, n) : la place
/// juste après la dernière, sans recentrer la ligne ; celui d'une ligne vide vaut (0, 1).
/// </summary>
public sealed record FormationTarget(int? SwapWith, int Line, int Index, int Count);

/// <summary>
/// La formation réorganisée par le joueur, en cours de run : glisser-déposer d'un personnage sur un
/// autre (échange) ou au bout d'une ligne, et placement des recrues. Un déplacement demandé
/// (<see cref="FormationMove"/>) attend le prochain cycle d'action du personnage, qu'il consacre à
/// marcher vers sa nouvelle place au lieu d'agir (<see cref="CombatSystem"/>).
///
/// <para>Deux formations coexistent : celle du combat (<see cref="Entity.Line"/>, <see cref="Entity.Rank"/>,
/// changée par les avancées) et celle du joueur (<see cref="HeroEntity.FormationLine"/>,
/// <see cref="HeroEntity.FormationRank"/>, reprise après une défaite). Un échange les modifie toutes
/// les deux de la même façon et garde donc les deux pyramides valides. Un passage au bout d'une ligne
/// change la forme de la pyramide : il n'est proposé que quand toute l'équipe est en vie (les deux
/// formations sont alors la même), et seulement s'il garde la pyramide valide.</para>
/// </summary>
public static class FormationOrders
{
    // ── Cibles ───────────────────────────────────────────────────────────

    /// <summary>Les places où déposer le personnage d'indice <paramref name="hero"/> ; aucune s'il ne peut pas bouger.</summary>
    public static List<FormationTarget> DropTargets(GameState state, int hero)
    {
        var targets = new List<FormationTarget>();
        if (!IsMovable(state, hero)) return targets;
        var moving = state.Heroes[hero];

        for (int i = 0; i < state.Heroes.Count; i++)
        {
            if (i == hero || !IsMovable(state, i)) continue;
            if (SwapTarget(state, i) is { } swap) targets.Add(swap);
        }

        if (!AllAlive(state)) return targets;
        int deepest = state.Heroes.Max(h => h.Line);
        for (int line = 1; line <= deepest + 1; line++)
            if (line != moving.Line && CanMoveToLine(state, moving, line))
                targets.Add(LineEndTarget(state, line));
        return targets;
    }

    /// <summary>Là où va le déplacement <paramref name="move"/> ; null s'il ne mène plus nulle part.</summary>
    public static FormationTarget? TargetOf(GameState state, FormationMove move) =>
        move.SwapWith is int other ? SwapTarget(state, other) : LineEndTarget(state, move.Line);

    private static FormationTarget? SwapTarget(GameState state, int other)
    {
        if (!IsMovable(state, other)) return null;
        var hero = state.Heroes[other];
        return Formation.Place(hero, state.Heroes, state.HeroHoles) is (int index, int count)
            ? new FormationTarget(other, hero.Line, index, count)
            : null;
    }

    /// <summary>La place libre au bout (en bas) de <paramref name="line"/>, juste après ses places actuelles.</summary>
    private static FormationTarget LineEndTarget(GameState state, int line)
    {
        int count = Formation.Slots(state.Heroes, state.HeroHoles, line).Count;
        return count == 0 ? new FormationTarget(null, line, 0, 1) : new FormationTarget(null, line, count, count);
    }

    // ── Ordres ───────────────────────────────────────────────────────────

    /// <summary>
    /// Demande de déposer le personnage <paramref name="hero"/> sur <paramref name="target"/> ; remplace
    /// sa demande précédente. Faux (et rien ne change) si ce n'est pas une place proposée par
    /// <see cref="DropTargets"/>.
    /// </summary>
    public static bool Order(GameState state, int hero, FormationTarget target)
    {
        bool offered = DropTargets(state, hero).Any(t => t.SwapWith == target.SwapWith && (t.SwapWith != null || t.Line == target.Line));
        if (!offered) return false;
        Cancel(state, hero);
        state.FormationMoves.Add(new FormationMove { Hero = hero, SwapWith = target.SwapWith, Line = target.Line });
        return true;
    }

    /// <summary>Annule la demande en cours du personnage <paramref name="hero"/>, s'il en a une.</summary>
    public static void Cancel(GameState state, int hero) => state.FormationMoves.RemoveAll(m => m.Hero == hero);

    /// <summary>Un personnage mort ne bouge plus et ne sert plus de cible : ses demandes disparaissent.</summary>
    public static void Forget(GameState state, int hero) =>
        state.FormationMoves.RemoveAll(m => m.Hero == hero || m.SwapWith == hero);

    /// <summary>Retire et renvoie la demande en attente du personnage <paramref name="hero"/> ; null s'il n'en a pas.</summary>
    public static FormationMove? Take(GameState state, HeroEntity hero)
    {
        int index = state.Heroes.IndexOf(hero);
        var move = state.FormationMoves.Find(m => m.Hero == index);
        if (move != null) state.FormationMoves.Remove(move);
        return move;
    }

    /// <summary>
    /// Exécute <paramref name="move"/> s'il est encore possible (les personnages ont pu mourir, la
    /// formation changer depuis la demande).
    /// </summary>
    /// <returns>Ceux qui changent de place ; vide si le déplacement n'est plus possible.</returns>
    public static List<Entity> Execute(GameState state, FormationMove move, long tick)
    {
        var moved = new List<Entity>();
        if (!IsMovable(state, move.Hero)) return moved;
        var hero = state.Heroes[move.Hero];

        if (move.SwapWith is int otherIndex)
        {
            if (otherIndex == move.Hero || !IsMovable(state, otherIndex)) return moved;
            var other = state.Heroes[otherIndex];
            (hero.Line, other.Line) = (other.Line, hero.Line);
            (hero.Rank, other.Rank) = (other.Rank, hero.Rank);
            (hero.FormationLine, other.FormationLine) = (other.FormationLine, hero.FormationLine);
            (hero.FormationRank, other.FormationRank) = (other.FormationRank, hero.FormationRank);
            hero.PreferredLine = hero.FormationLine;
            other.PreferredLine = other.FormationLine;
            moved.Add(hero);
            moved.Add(other);
        }
        else
        {
            if (move.Line == hero.Line || !AllAlive(state) || !CanMoveToLine(state, hero, move.Line)) return moved;
            hero.Line = move.Line;
            hero.Rank = int.MaxValue; // au bout de la ligne ; Settle renumérote
            hero.PreferredLine = move.Line;
            // La pyramide reste valide : personne n'avance, les lignes touchées se resserrent.
            moved.AddRange(Formation.Settle(state.Heroes, state.HeroHoles, tick));
            moved.Add(hero);
        }

        if (AllAlive(state)) RecordFormation(state);
        return moved;
    }

    // ── Recrutement ──────────────────────────────────────────────────────

    /// <summary>
    /// Ajoute une recrue de niveau 1 à l'équipe, le plus loin possible derrière sans casser la
    /// pyramide : dans la formation du joueur, et dans celle du combat parmi les vivants. Elle entre
    /// en jeu tout de suite ; au combat, elle agit au bout de sa cadence.
    /// </summary>
    public static HeroEntity Recruit(GameState state, HeroClass heroClass, long tick)
    {
        var recruit = new HeroEntity { Class = heroClass };
        var stats = GameContent.HeroStats(heroClass, recruit.Level);
        recruit.Health = stats.MaxHealth;

        recruit.FormationLine = BackmostLine(state.Heroes.Select(h => h.FormationLine));
        recruit.FormationRank = state.Heroes.Where(h => h.FormationLine == recruit.FormationLine)
            .Select(h => h.FormationRank + 1).DefaultIfEmpty(0).Max();
        recruit.PreferredLine = recruit.FormationLine;
        recruit.Line = AllAlive(state)
            ? recruit.FormationLine
            : BackmostLine(state.Heroes.Where(h => h.IsAlive).Select(h => h.Line));
        recruit.Rank = int.MaxValue;
        if (state.Run.Phase == RunPhase.Fighting) recruit.NextActionTick = tick + stats.CooldownTicks;

        state.Heroes.Add(recruit);
        Formation.Settle(state.Heroes, state.HeroHoles, tick);
        if (AllAlive(state)) RecordFormation(state);
        return recruit;
    }

    // ── Pyramide ─────────────────────────────────────────────────────────

    /// <summary>
    /// Vrai si des combattants placés sur ces lignes forment une pyramide (GDD §3.1) : aucune ligne
    /// vide avant la dernière, et chacune strictement plus remplie que celle de derrière.
    /// </summary>
    public static bool IsPyramid(IEnumerable<int> lines)
    {
        var counts = new Dictionary<int, int>();
        int deepest = 0;
        foreach (int line in lines)
        {
            if (line < 1) return false;
            counts[line] = counts.GetValueOrDefault(line) + 1;
            deepest = Math.Max(deepest, line);
        }
        for (int line = 1; line <= deepest; line++)
        {
            int count = counts.GetValueOrDefault(line);
            if (count == 0) return false;
            if (line > 1 && count >= counts[line - 1]) return false;
        }
        return true;
    }

    /// <summary>La ligne la plus en arrière où un combattant de plus garde la pyramide valide.</summary>
    private static int BackmostLine(IEnumerable<int> lines)
    {
        var taken = lines.ToList();
        int deepest = taken.Count == 0 ? 0 : taken.Max();
        for (int line = deepest + 1; line > 1; line--)
            if (IsPyramid(taken.Append(line))) return line;
        return 1;
    }

    private static bool CanMoveToLine(GameState state, HeroEntity hero, int line) =>
        IsPyramid(state.Heroes.Where(h => h.IsAlive && !ReferenceEquals(h, hero)).Select(h => h.Line).Append(line));

    private static bool IsMovable(GameState state, int hero) =>
        hero >= 0 && hero < state.Heroes.Count && state.Heroes[hero].IsAlive;

    private static bool AllAlive(GameState state) => state.Heroes.All(h => h.IsAlive);

    /// <summary>Toute l'équipe en vie : la formation du combat devient celle du joueur.</summary>
    private static void RecordFormation(GameState state)
    {
        foreach (var hero in state.Heroes)
        {
            hero.FormationLine = hero.Line;
            hero.FormationRank = hero.Rank;
        }
    }
}
