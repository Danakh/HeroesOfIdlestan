using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Entities;

namespace HeroesOfIdlestan.Core.Presentation.Animation;

/// <summary>
/// Les noms d'animation que le jeu demande, communs aux deux piles : ids des <c>*.anim.json</c>
/// (Skia) et noms des clips glTF (Godot, comparés sans tenir compte de la casse). Voir
/// design/07-animation.md.
/// </summary>
public static class AnimationNames
{
    // Les cinq animations de base, que tout squelette devrait avoir.
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Attack = "attack";
    public const string Hurt = "hurt";
    public const string Death = "death";

    // Variantes de l'attaque, selon l'arme.
    public const string AttackMelee = "attack_melee";
    public const string AttackBow = "attack_bow";
    public const string AttackMagic = "attack_magic";
}

/// <summary>
/// Le catalogue : les animations connues, l'animation de secours de chacune, et le choix de
/// l'attaque selon le combattant.
/// </summary>
public static class AnimationCatalog
{
    public static readonly IReadOnlyList<string> Base =
        [AnimationNames.Idle, AnimationNames.Walk, AnimationNames.Attack, AnimationNames.Hurt, AnimationNames.Death];

    public static readonly IReadOnlyList<string> All =
        [.. Base, AnimationNames.AttackMelee, AnimationNames.AttackBow, AnimationNames.AttackMagic];

    /// <summary>
    /// L'animation à jouer à la place de <paramref name="name"/> quand elle manque ; null pour
    /// <see cref="AnimationNames.Idle"/>, au bout de toutes les chaînes. Une variante d'attaque se
    /// rabat sur l'attaque de base, tout le reste sur l'attente.
    /// </summary>
    public static string? FallbackOf(string name) => name switch
    {
        AnimationNames.Idle => null,
        AnimationNames.AttackMelee or AnimationNames.AttackBow or AnimationNames.AttackMagic => AnimationNames.Attack,
        _ => AnimationNames.Idle,
    };

    /// <summary>L'attaque d'un combattant, selon son arme. Les monstres jouent l'attaque de base.</summary>
    public static string AttackFor(Entity fighter) => fighter switch
    {
        HeroEntity { Class: HeroClass.Warrior } => AnimationNames.AttackMelee,
        _ => AnimationNames.Attack,
    };
}

/// <summary>
/// Trouve l'animation à jouer pour un modèle (un squelette, un glTF) : celle demandée si le modèle
/// l'a, sinon la première de sa chaîne de secours (<see cref="AnimationCatalog.FallbackOf"/>) qu'il
/// a. Chaque remplacement est consigné une fois dans <see cref="GameLog"/> en avertissement.
/// </summary>
/// <param name="owner">Le modèle, pour le journal (« humanoid », « robot »…).</param>
/// <param name="has">Vrai si le modèle a cette animation.</param>
public sealed class AnimationResolver(string owner, Func<string, bool> has)
{
    private readonly Dictionary<string, string?> _resolved = [];

    public string Owner => owner;

    /// <summary>Vrai si le modèle a lui-même <paramref name="name"/>, sans secours.</summary>
    public bool Has(string name) => has(name);

    /// <summary>L'animation à jouer pour <paramref name="wanted"/>, ou null si rien de la chaîne n'existe.</summary>
    public string? Resolve(string wanted)
    {
        if (_resolved.TryGetValue(wanted, out var known)) return known;

        string? used = null;
        for (string? candidate = wanted; candidate != null; candidate = AnimationCatalog.FallbackOf(candidate))
        {
            if (!has(candidate)) continue;
            used = candidate;
            break;
        }

        if (used != wanted)
        {
            GameLog.Warning("Animation", owner, used != null
                ? $"{owner} : animation « {wanted} » absente, remplacée par « {used} »."
                : $"{owner} : animation « {wanted} » absente, et aucune animation de secours.");
        }
        _resolved[wanted] = used;
        return used;
    }
}
