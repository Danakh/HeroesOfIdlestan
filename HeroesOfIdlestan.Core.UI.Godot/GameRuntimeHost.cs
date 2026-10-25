using Godot;
using HeroesOfIdlestan.Core.Render.Godot.Runtime;

namespace HeroesOfIdlestan.Core.UI.Godot;

/// <summary>
/// Accès unique et sérialisé au <see cref="GameRuntime"/>. Sous Godot, boucle, input et HUD vivent
/// tous sur le thread principal et le serveur de rendu ne lit jamais l'état du jeu : le verrou ne
/// coûte presque rien, et il garde le contrat de la version Avalonia le jour où un thread s'ajoute
/// (sauvegarde en tâche de fond, store qui rappelle depuis son propre thread…).
///
/// <para>RÈGLE : tout accès au runtime, lectures comprises, passe par <see cref="Read{T}"/> ou
/// <see cref="Invoke"/>.</para>
/// </summary>
public sealed class GameRuntimeHost : IDisposable
{
    private readonly GameRuntime _runtime;
    private readonly object _gate = new();
    private bool _disposed;

    public GameRuntimeHost(GameRuntime runtime) => _runtime = runtime;

    public T Read<T>(Func<GameRuntime, T> read)
    {
        lock (_gate) return _disposed ? default! : read(_runtime);
    }

    public void Invoke(Action<GameRuntime> command)
    {
        lock (_gate)
        {
            if (!_disposed) command(_runtime);
        }
    }

    // ── Boucle ───────────────────────────────────────────────────────────

    public void Tick() => Invoke(r => r.Tick());

    public void Render(double deltaSeconds) => Invoke(r => r.Render(deltaSeconds));

    // ── Input ────────────────────────────────────────────────────────────

    public bool HandleInput(InputEvent input) => Read(r => r.HandleInput(input));

    // ── Cycle de vie du head ─────────────────────────────────────────────

    public void SetInBackground(bool inBackground) => Invoke(r => r.SetInBackground(inBackground));

    public void CreditStalledTime(double seconds) => Invoke(r => r.CreditStalledTime(seconds));

    // ── Lecture pour l'overlay ───────────────────────────────────────────

    public HudSnapshot GetHudSnapshot() => Read(r => r.GetHudSnapshot()) ?? HudSnapshot.Empty;

    public string Localize(string key, params object[] args) =>
        Read(r => args.Length == 0 ? r.Localizer.Get(key) : r.Localizer.GetFormatted(key, args)) ?? key;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _runtime.Dispose();
        }
    }
}
