namespace HeroesOfIdlestan.Core.Diagnostics;

/// <summary>
/// Journal des erreurs attrapées par les gardes de la simulation. Remplace
/// <c>Debug.WriteLine</c>, supprimé à la compilation en Release : un système qui lèverait à chaque
/// tick s'arrêterait alors sans laisser de trace dans le build livré.
///
/// <para>Dédupliqué par (source, opération, type, message) : une erreur répétée à chaque tick fait
/// monter un compteur au lieu de remplir la mémoire. Statique à dessein : c'est un canal de
/// diagnostic, jamais sérialisé ni lu par la simulation.</para>
/// </summary>
public static class GameLog
{
    public const int MaxDistinctErrors = 50;

    public sealed class ErrorEntry
    {
        public required string Source { get; init; }
        public required string Operation { get; init; }
        public required string ExceptionType { get; init; }
        public required string Message { get; init; }
        public required string StackTrace { get; init; }
        public int Count { get; internal set; } = 1;
    }

    private static readonly object Gate = new();
    private static readonly List<ErrorEntry> Entries = [];

    /// <summary>Levé à la première occurrence d'une erreur distincte (pour un toast, un rapport…).</summary>
    public static event Action<ErrorEntry>? OnFirstOccurrence;

    public static IReadOnlyList<ErrorEntry> Snapshot()
    {
        lock (Gate) return Entries.ToList();
    }

    public static void Error(string source, string operation, Exception ex)
        => Record(source, operation, ex.GetType().FullName ?? ex.GetType().Name, ex.Message, ex.StackTrace ?? "");

    /// <summary>Anomalie sans exception (ressource absente, format refusé…).</summary>
    public static void Error(string source, string operation, string message)
        => Record(source, operation, "", message, "");

    private static void Record(string source, string operation, string type, string message, string stackTrace)
    {
        ErrorEntry? added = null;
        lock (Gate)
        {
            var existing = Entries.Find(e => e.Source == source && e.Operation == operation
                                             && e.ExceptionType == type && e.Message == message);
            if (existing != null)
            {
                existing.Count++;
                return;
            }

            added = new ErrorEntry
            {
                Source = source,
                Operation = operation,
                ExceptionType = type,
                Message = message,
                StackTrace = stackTrace,
            };
            Entries.Add(added);
            if (Entries.Count > MaxDistinctErrors) Entries.RemoveAt(0);
        }
        OnFirstOccurrence?.Invoke(added);
    }

    public static void Clear()
    {
        lock (Gate) Entries.Clear();
    }
}
