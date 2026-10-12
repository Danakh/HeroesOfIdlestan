using SkiaSharp;

namespace HeroesOfIdlestan.Render.Rendering;

public enum PointerButton { Left, Middle, Right }

/// <summary>
/// Traduit les entrées brutes de l'hôte (coordonnées écran) en gestes : clic, glisser = pan, molette
/// et pincement = zoom. Un appui qui bouge de plus de <see cref="DragThreshold"/> pixels devient un
/// glisser et ne produit plus de clic.
/// </summary>
public sealed class InputRouter(Camera2D camera)
{
    public const float DragThreshold = 6f;
    public const float WheelZoomStep = 1.15f;

    private readonly Dictionary<int, SKPoint> _pressed = [];
    private SKPoint _pressOrigin;
    private bool _dragging;

    /// <summary>Clic sans glisser, en coordonnées monde.</summary>
    public event Action<SKPoint, PointerButton>? WorldClicked;

    public void PointerPressed(float x, float y, int id, PointerButton button)
    {
        _pressed[id] = new SKPoint(x, y);
        if (_pressed.Count == 1)
        {
            _pressOrigin = new SKPoint(x, y);
            _dragging = false;
        }
    }

    public void PointerMoved(float x, float y, int id)
    {
        if (!_pressed.TryGetValue(id, out var last) || _pressed.Count != 1) return;
        var p = new SKPoint(x, y);
        if (!_dragging && SKPoint.Distance(p, _pressOrigin) > DragThreshold) _dragging = true;
        if (_dragging) camera.Pan(p.X - last.X, p.Y - last.Y);
        _pressed[id] = p;
    }

    public void PointerReleased(float x, float y, int id, PointerButton button)
    {
        bool wasClick = _pressed.Remove(id) && !_dragging && _pressed.Count == 0;
        if (wasClick) WorldClicked?.Invoke(camera.ScreenToWorld(new SKPoint(x, y)), button);
        if (_pressed.Count == 0) _dragging = false;
    }

    public void Wheel(float delta, float x, float y) =>
        camera.ZoomAt(camera.Zoom * MathF.Pow(WheelZoomStep, delta), new SKPoint(x, y));

    /// <summary>Pincement : <paramref name="scaleRatio"/> relatif à l'événement précédent, le centre du geste sert de pan.</summary>
    public void Pinch(float scaleRatio, float x, float y, float panDx, float panDy)
    {
        _dragging = true;
        camera.ZoomAt(camera.Zoom * scaleRatio, new SKPoint(x, y));
        camera.Pan(panDx, panDy);
    }

    /// <summary>Oublie les pointeurs enfoncés (capturés par un geste de l'hôte, fenêtre qui perd le focus…).</summary>
    public void Reset()
    {
        _pressed.Clear();
        _dragging = false;
    }
}
