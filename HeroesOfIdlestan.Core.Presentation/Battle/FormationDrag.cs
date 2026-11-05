using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Presentation.Battle;

/// <summary>
/// Le glisser-déposer d'un personnage dans la formation, commun aux deux piles : chacune traduit son
/// input (souris, doigt) en <see cref="Begin"/>, <see cref="MoveTo"/> et <see cref="Drop"/>, dans ses
/// propres coordonnées (monde en Skia, écran en Godot), et dessine les places proposées
/// (<see cref="Targets"/>) et la flèche. Déposé sur une place, le personnage reçoit un ordre de
/// déplacement (<see cref="GameController.OrderFormationMove"/>), exécuté à son prochain cycle
/// d'action ; déposé sur lui-même, son ordre en cours est annulé.
///
/// <para>Ne touche à l'état que par les commandes du contrôleur. À appeler sous le verrou de l'hôte,
/// comme le rendu et l'input.</para>
/// </summary>
public sealed class FormationDrag
{
    /// <summary>Le personnage glissé (indice dans <c>GameState.Heroes</c>) ; null hors glisser.</summary>
    public int? Hero { get; private set; }

    public bool Active => Hero != null;

    /// <summary>Les places où le déposer, recalculées à chaque <see cref="Refresh"/>.</summary>
    public IReadOnlyList<FormationTarget> Targets { get; private set; } = [];

    /// <summary>Où en est le pointeur, dans les coordonnées de la pile.</summary>
    public (float X, float Y) Pointer { get; private set; }

    /// <summary>La place sous le pointeur (null : aucune) ; le personnage lui-même compte comme une place.</summary>
    public FormationTarget? Hovered { get; private set; }

    /// <summary>Le pointeur est revenu sur le personnage glissé : le lâcher annule son ordre.</summary>
    public bool HoversSelf { get; private set; }

    /// <summary>Commence à glisser le personnage <paramref name="hero"/> s'il peut bouger.</summary>
    public bool Begin(GameState state, int hero, float x, float y)
    {
        if (hero < 0 || hero >= state.Heroes.Count || !state.Heroes[hero].IsAlive) return false;
        Hero = hero;
        Pointer = (x, y);
        Refresh(state);
        return true;
    }

    /// <summary>
    /// Recalcule les places (une mort, une avancée ont pu tout décaler) ; arrête le glisser si le
    /// personnage est mort entre-temps.
    /// </summary>
    public void Refresh(GameState state)
    {
        if (Hero is not int hero) return;
        if (hero >= state.Heroes.Count || !state.Heroes[hero].IsAlive)
        {
            Cancel();
            return;
        }
        Targets = FormationOrders.DropTargets(state, hero);
        if (Hovered is { } hovered && !Targets.Contains(hovered)) Hovered = null;
    }

    /// <summary>
    /// Le pointeur bouge. <paramref name="hovered"/> : la place sous le pointeur, trouvée par la pile
    /// (<see cref="Nearest"/>) ; <paramref name="self"/> : le pointeur est sur le personnage glissé.
    /// </summary>
    public void MoveTo(float x, float y, FormationTarget? hovered, bool self)
    {
        Pointer = (x, y);
        HoversSelf = self && hovered == null;
        Hovered = hovered;
    }

    /// <summary>Lâche le personnage : ordre vers la place survolée, annulation sur lui-même, rien ailleurs.</summary>
    public void Drop(GameController game)
    {
        if (Hero is int hero)
        {
            if (Hovered is { } target) game.OrderFormationMove(hero, target);
            else if (HoversSelf) game.CancelFormationMove(hero);
        }
        Cancel();
    }

    public void Cancel()
    {
        Hero = null;
        Targets = [];
        Hovered = null;
        HoversSelf = false;
    }

    /// <summary>
    /// L'élément le plus proche de (<paramref name="x"/>, <paramref name="y"/>) à moins de
    /// <paramref name="radius"/>, parmi des positions dans les coordonnées de la pile ; null si aucun.
    /// </summary>
    public static T? Nearest<T>(IEnumerable<(T Item, float X, float Y)> candidates, float x, float y, float radius)
        where T : class
    {
        T? best = null;
        float bestDistance = radius * radius;
        foreach (var (item, cx, cy) in candidates)
        {
            float d = (cx - x) * (cx - x) + (cy - y) * (cy - y);
            if (d > bestDistance) continue;
            bestDistance = d;
            best = item;
        }
        return best;
    }
}
