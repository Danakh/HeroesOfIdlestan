namespace HeroesOfIdlestan.Core.Presentation.Runtime;

/// <summary>
/// Le runtime d'une pile d'affichage : une <see cref="GameSession"/> et ce que la pile dessine.
/// <see cref="IDisposable.Dispose"/> libère la vue et la session (qui sauvegarde une dernière fois).
/// </summary>
public interface IGameRuntime : IDisposable
{
    GameSession Session { get; }
}

/// <summary>
/// Ce que l'overlay voit de l'hôte, quelle que soit la pile : les ViewModels n'atteignent la session
/// que par ici, sous le verrou.
/// </summary>
public interface IGameHost
{
    T Read<T>(Func<GameSession, T> read);
    void Invoke(Action<GameSession> command);
    HudSnapshot GetHudSnapshot();
    MenuSnapshot GetMenuSnapshot();
    string Localize(string key, params object[] args);
}

/// <summary>
/// Accès unique et sérialisé au runtime. Avalonia dessine sur son thread de rendu pendant que la
/// boucle de jeu et l'input vivent sur le thread UI ; sous Godot tout vit sur le thread principal,
/// mais un thread peut s'ajouter (sauvegarde en tâche de fond, store qui rappelle depuis le sien).
/// Le modèle, lui, est mono-thread par conception : plutôt que de rendre chaque collection
/// thread-safe, on rétablit ce contrat ici.
///
/// <para>RÈGLE : tout accès au runtime, lectures comprises, passe par <see cref="Read{T}"/> ou
/// <see cref="Invoke"/>. Un accès direct depuis un ViewModel réintroduit la course
/// (« Collection was modified » dans un renderer, au mieux).</para>
///
/// <para>Chaque pile en dérive son <c>GameRuntimeHost</c>, qui ajoute le rendu et l'input propres à
/// son moteur.</para>
/// </summary>
public abstract class GameHost<TRuntime>(TRuntime runtime) : IGameHost, IDisposable
    where TRuntime : class, IGameRuntime
{
    private readonly object _gate = new();
    private bool _disposed;

    public T Read<T>(Func<TRuntime, T> read)
    {
        lock (_gate) return _disposed ? default! : read(runtime);
    }

    public void Invoke(Action<TRuntime> command)
    {
        lock (_gate)
        {
            if (!_disposed) command(runtime);
        }
    }

    T IGameHost.Read<T>(Func<GameSession, T> read) => Read(r => read(r.Session));

    void IGameHost.Invoke(Action<GameSession> command) => Invoke(r => command(r.Session));

    // ── Boucle ───────────────────────────────────────────────────────────

    public void Tick() => Invoke(r => r.Session.Tick());

    // ── Cycle de vie du head ─────────────────────────────────────────────

    public void SetInBackground(bool inBackground) => Invoke(r => r.Session.SetInBackground(inBackground));

    public void CreditStalledTime(double seconds) => Invoke(r => r.Session.CreditStalledTime(seconds));

    // ── Lecture pour l'overlay ───────────────────────────────────────────

    public HudSnapshot GetHudSnapshot() => Read(r => r.Session.GetHudSnapshot()) ?? HudSnapshot.Empty;

    public MenuSnapshot GetMenuSnapshot() => Read(r => r.Session.GetMenuSnapshot()) ?? MenuSnapshot.Empty;

    public string Localize(string key, params object[] args) =>
        Read(r => args.Length == 0 ? r.Session.Localizer.Get(key) : r.Session.Localizer.GetFormatted(key, args)) ?? key;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            runtime.Dispose();
        }
    }
}
