using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Presentation.Animation;

/// <summary>
/// Ce qu'un combattant affiché joue, commun aux deux piles (Godot : clips glTF ; Skia : planches de
/// sprites). Les piles ne gardent que la lecture : enchaînement, fondu, chute.
/// </summary>
public static class FighterAnimation
{
    /// <summary>
    /// Animations propres au robot de test, hors catalogue : jouées si le modèle les a, sans secours
    /// ni avertissement.
    /// </summary>
    public const string Intro = "Intro";
    public const string LookAround = "Look around";

    /// <summary>Animations jouées en boucle ; les autres se jouent une fois.</summary>
    public static readonly IReadOnlyList<string> Loops = [AnimationNames.Idle, AnimationNames.Walk, "Run", LookAround];

    public static bool IsLoop(string name) => Loops.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// L'animation de fond d'un combattant qui ne marche pas vers sa place, hors attaque et coup reçu :
    /// un nom du catalogue (à résoudre avec secours), ou <see cref="LookAround"/>.
    /// </summary>
    public static string Background(RunState run, bool isHero, bool hasLookAround)
    {
        if (!isHero || run.Phase == RunPhase.Fighting) return AnimationNames.Idle;
        // Repos après une victoire : en route vers la zone suivante, ou on reste sur place.
        if (run.LastOutcome == FightOutcome.Victory)
            return run.AutoAdvance ? AnimationNames.Walk : hasLookAround ? LookAround : AnimationNames.Idle;
        return AnimationNames.Idle;
    }
}

/// <summary>
/// Repère, d'une frame à l'autre, ce qui doit lancer une animation ponctuelle : une nouvelle action
/// (attaque) ou une baisse de vie (coup reçu). Un par combattant affiché.
/// </summary>
public sealed class FighterCues(Entity fighter)
{
    private long? _lastAction = fighter.LastActionTick;
    private long _lastHealth = fighter.Health;

    /// <summary>
    /// Compare le combattant à la frame précédente. L'attaque l'emporte : la vue ne lance pas de coup
    /// reçu par-dessus un coup porté.
    /// </summary>
    public (bool Acted, bool Hurt) Observe(Entity fighter)
    {
        bool acted = fighter.LastActionTick is { } action && action != _lastAction;
        bool hurt = fighter.Health < _lastHealth;
        _lastAction = fighter.LastActionTick;
        _lastHealth = fighter.Health;
        return (acted, hurt);
    }
}
