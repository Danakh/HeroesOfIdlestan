namespace HeroesOfIdlestan.Core.Model.Diagnostics;

/// <summary>
/// Journal de diagnostic : erreurs attrapées par les gardes de la simulation, avertissements (une
/// animation absente remplacée par une autre…). Remplace <c>Debug.WriteLine</c>, supprimé à la
/// compilation en Release : un système qui lèverait à chaque tick s'arrêterait alors sans laisser de
/// trace dans le build livré. Le bouton « Log » du HUD l'affiche.
///
/// <para>Dédupliqué par (niveau, source, opération, type, message) : un message répété à chaque tick
/// fait monter un compteur au lieu de remplir la mémoire. Statique à dessein : c'est un canal de
/// diagnostic, jamais sérialisé ni lu par la simulation.</para>
/// </summary>
public static class GameLog
{
    public const int MaxDistinctEntries = 50;

    public enum Level { Warning, Error }

    public sealed class Entry
    {
        public required Level Level { get; init; }
        public required string Source { get; init; }
        public required string Operation { get; init; }
        public required string ExceptionType { get; init; }
        public required string Message { get; init; }
        public required string StackTrace { get; init; }
        /// <summary>Heure locale de la première occurrence.</summary>
        public required DateTime Time { get; init; }
        public int Count { get; internal set; } = 1;
    }

    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = [];
    private static long _version;

    /// <summary>Levé à la première occurrence d'un message distinct (pour un toast, un rapport…).</summary>
    public static event Action<Entry>? OnFirstOccurrence;

    /// <summary>
    /// Augmente à chaque nouveau message distinct (pas à ses répétitions) : une vue qui retient la
    /// dernière valeur vue sait s'il y a du nouveau, sans copier le journal.
    /// </summary>
    public static long Version
    {
        get { lock (Gate) return _version; }
    }

    public static IReadOnlyList<Entry> Snapshot()
    {
        lock (Gate) return Entries.ToList();
    }

    public static void Error(string source, string operation, Exception ex)
        => Record(Level.Error, source, operation, ex.GetType().FullName ?? ex.GetType().Name, ex.Message, ex.StackTrace ?? "");

    /// <summary>Anomalie sans exception (ressource absente, format refusé…).</summary>
    public static void Error(string source, string operation, string message)
        => Record(Level.Error, source, operation, "", message, "");

    /// <summary>Anomalie que le jeu contourne (contenu manquant remplacé par un autre…).</summary>
    public static void Warning(string source, string operation, string message)
        => Record(Level.Warning, source, operation, "", message, "");

    private static void Record(Level level, string source, string operation, string type, string message, string stackTrace)
    {
        Entry? added = null;
        lock (Gate)
        {
            var existing = Entries.Find(e => e.Level == level && e.Source == source && e.Operation == operation
                                             && e.ExceptionType == type && e.Message == message);
            if (existing != null)
            {
                existing.Count++;
                return;
            }

            added = new Entry
            {
                Level = level,
                Source = source,
                Operation = operation,
                ExceptionType = type,
                Message = message,
                StackTrace = stackTrace,
                Time = DateTime.Now,
            };
            Entries.Add(added);
            if (Entries.Count > MaxDistinctEntries) Entries.RemoveAt(0);
            _version++;
        }
        OnFirstOccurrence?.Invoke(added);
    }

    /// <summary>Vide le journal. <see cref="Version"/> ne recule pas, pour qu'un message suivant compte comme nouveau.</summary>
    public static void Clear()
    {
        lock (Gate) Entries.Clear();
    }
}
