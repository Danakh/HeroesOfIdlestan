using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Meta;
using HeroesOfIdlestan.Core.Model.Obfuscation;

namespace HeroesOfIdlestan.Core.Model.State;

/// <summary>
/// Racine unique de l'état persisté — le « store » du jeu. Tout ce qui doit survivre à une
/// sauvegarde vit ici, et rien d'autre : les caches et agrégats dérivés sont reconstruits par
/// <c>GameController.Initialize</c>.
///
/// <para>Accès : le modèle est mono-thread. Hors des tests, l'UI et le rendu n'y touchent qu'à travers
/// <c>GameRuntimeHost.Read</c>/<c>Invoke</c>.</para>
/// </summary>
public sealed class GameState
{
    /// <summary>Version du jeu qui a écrit la sauvegarde, posée par <c>SaveService.Serialize</c>.</summary>
    public string? SavedGameVersion { get; set; }

    public GameClock Clock { get; set; } = new();

    /// <summary>PRNG réservé à la génération du monde : pour un seed donné, le monde est toujours le même.</summary>
    public GamePrng WorldPrng { get; set; } = new(1);

    /// <summary>PRNG de gameplay : tous les autres tirages.</summary>
    public GamePrng Prng { get; set; } = new(2);

    // ── Jeu ──────────────────────────────────────────────────────────────

    /// <summary>Or gagné en combat (GDD §6.3), brouillé en mémoire car lu par le joueur.</summary>
    public ObfLong Gold { get; set; }

    public RunState Run { get; set; } = new();

    /// <summary>
    /// L'équipe du joueur, morts compris : un mort sort de la formation (sa ligne se resserre) mais
    /// reste recruté. L'ordre de la liste est l'ordre « de haut en bas » de chaque ligne.
    /// </summary>
    public List<HeroEntity> Heroes { get; set; } = [];

    /// <summary>Le groupe de monstres du combat en cours ; un monstre tué en est retiré.</summary>
    public List<MonsterEntity> Monsters { get; set; } = [];

    // ── Méta ─────────────────────────────────────────────────────────────

    public GameEventLog Events { get; set; } = new();

    /// <summary>Identifiants des succès obtenus dans cette partie.</summary>
    public List<string> CompletedAchievements { get; set; } = [];
}
