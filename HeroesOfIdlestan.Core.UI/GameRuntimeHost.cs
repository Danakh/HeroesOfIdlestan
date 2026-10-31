using HeroesOfIdlestan.Core.Presentation.Runtime;
using HeroesOfIdlestan.Core.Render.Rendering;
using HeroesOfIdlestan.Core.Render.Runtime;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.UI;

/// <summary>
/// Hôte du runtime Skia : le verrou et la boucle communs (<see cref="GameHost{TRuntime}"/>), plus le
/// rendu sur canevas et l'input pointeur d'Avalonia. Avalonia dessine sur son thread de rendu pendant
/// que la boucle et l'input vivent sur le thread UI : c'est ici que le verrou sert vraiment.
/// </summary>
public sealed class GameRuntimeHost(GameRuntime runtime) : GameHost<GameRuntime>(runtime)
{
    public void Render(SKCanvas canvas, SKSize size) => Invoke(r => r.Render(canvas, size));

    // ── Input ────────────────────────────────────────────────────────────

    public void PointerPressed(float x, float y, int id, PointerButton b) => Invoke(r => r.Screen.PointerPressed(x, y, id, b));
    public void PointerMoved(float x, float y, int id) => Invoke(r => r.Screen.PointerMoved(x, y, id));
    public void PointerReleased(float x, float y, int id, PointerButton b) => Invoke(r => r.Screen.PointerReleased(x, y, id, b));
    public void Wheel(float delta, float x, float y) => Invoke(r => r.Screen.Wheel(delta, x, y));
    public void Pinch(float ratio, float x, float y, float dx, float dy) => Invoke(r => r.Screen.Pinch(ratio, x, y, dx, dy));
}
