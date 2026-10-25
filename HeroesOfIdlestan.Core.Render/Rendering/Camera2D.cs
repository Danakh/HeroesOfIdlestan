using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Rendering;

/// <summary>
/// Caméra 2D : <see cref="Position"/> est le point du monde au centre de l'écran.
/// Pan en pixels écran, zoom ancré sous le curseur, cadrage sur des bornes.
/// </summary>
public sealed class Camera2D
{
    public const float MinZoom = 0.2f;
    public const float MaxZoom = 5f;

    public SKPoint Position { get; private set; }
    public float Zoom { get; private set; } = 1f;
    public SKSize ViewportSize { get; private set; }

    public void SetViewport(SKSize size) => ViewportSize = size;

    public SKPoint WorldToScreen(SKPoint world) => new(
        (world.X - Position.X) * Zoom + ViewportSize.Width / 2,
        (world.Y - Position.Y) * Zoom + ViewportSize.Height / 2);

    public SKPoint ScreenToWorld(SKPoint screen) => new(
        (screen.X - ViewportSize.Width / 2) / Zoom + Position.X,
        (screen.Y - ViewportSize.Height / 2) / Zoom + Position.Y);

    /// <summary>Applique la transformation monde → écran au canevas (à encadrer de Save/Restore).</summary>
    public void ApplyTo(SKCanvas canvas)
    {
        canvas.Translate(ViewportSize.Width / 2, ViewportSize.Height / 2);
        canvas.Scale(Zoom);
        canvas.Translate(-Position.X, -Position.Y);
    }

    public void CenterOn(SKPoint world) => Position = world;

    public void Pan(float screenDx, float screenDy) =>
        Position = new SKPoint(Position.X - screenDx / Zoom, Position.Y - screenDy / Zoom);

    /// <summary>Zoome en gardant fixe le point du monde situé sous <paramref name="screenAnchor"/>.</summary>
    public void ZoomAt(float newZoom, SKPoint screenAnchor)
    {
        var before = ScreenToWorld(screenAnchor);
        Zoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        var after = ScreenToWorld(screenAnchor);
        Position = new SKPoint(Position.X + before.X - after.X, Position.Y + before.Y - after.Y);
    }

    /// <summary>Cadre des bornes monde dans la vue, avec une marge en fraction de la vue.</summary>
    public void Fit(SKRect worldBounds, float margin = 0.1f)
    {
        if (worldBounds.Width <= 0 || worldBounds.Height <= 0 || ViewportSize.Width <= 0) return;
        float usable = 1 - 2 * margin;
        Zoom = Math.Clamp(Math.Min(ViewportSize.Width * usable / worldBounds.Width,
                                   ViewportSize.Height * usable / worldBounds.Height), MinZoom, MaxZoom);
        Position = new SKPoint(worldBounds.MidX, worldBounds.MidY);
    }
}
