using HeroesOfIdlestan.Render.Rendering;
using HeroesOfIdlestan.Render.Runtime;
using SkiaSharp;

namespace HeroesOfIdlestan.UI;

/// <summary>
/// Accès unique et sérialisé au <see cref="GameRuntime"/>. Avalonia dessine sur son thread de rendu
/// pendant que la boucle de jeu et l'input vivent sur le thread UI ; le modèle, lui, est mono-thread
/// par conception. Plutôt que de rendre chaque collection thread-safe, on rétablit ce contrat ici.
///
/// <para>RÈGLE : tout accès au runtime, lectures comprises, passe par <see cref="Read{T}"/> ou
/// <see cref="Invoke"/>. Un accès direct depuis un ViewModel réintroduit la course
/// (« Collection was modified » dans un renderer, au mieux).</para>
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

    public void Render(SKCanvas canvas, SKSize size) => Invoke(r => r.Render(canvas, size));

    // ── Input ────────────────────────────────────────────────────────────

    public void PointerPressed(float x, float y, int id, PointerButton b) => Invoke(r => r.Screen.PointerPressed(x, y, id, b));
    public void PointerMoved(float x, float y, int id) => Invoke(r => r.Screen.PointerMoved(x, y, id));
    public void PointerReleased(float x, float y, int id, PointerButton b) => Invoke(r => r.Screen.PointerReleased(x, y, id, b));
    public void Wheel(float delta, float x, float y) => Invoke(r => r.Screen.Wheel(delta, x, y));
    public void Pinch(float ratio, float x, float y, float dx, float dy) => Invoke(r => r.Screen.Pinch(ratio, x, y, dx, dy));

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
