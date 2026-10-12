namespace HeroesOfIdlestan.Core.Meta;

/// <summary>Un événement de jeu montré au joueur. <see cref="Type"/> est une clé libre (et une clé de localisation).</summary>
public sealed record GameEvent(long Tick, string Type, string? Message = null);

/// <summary>
/// Journal des événements de la partie, persisté et plafonné. <see cref="Added"/> permet aux
/// couches supérieures (sons, toasts) de réagir sans que le Core les connaisse.
/// </summary>
public sealed class GameEventLog
{
    public const int MaxEntries = 200;

    public List<GameEvent> Entries { get; set; } = [];

    /// <summary>Levé à chaque ajout, pendant le tick ou la commande qui l'a produit. Runtime uniquement.</summary>
    public event Action<GameEvent>? Added;

    public void Add(GameEvent entry)
    {
        Entries.Add(entry);
        if (Entries.Count > MaxEntries) Entries.RemoveRange(0, Entries.Count - MaxEntries);
        Added?.Invoke(entry);
    }
}
