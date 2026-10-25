using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Render.Rendering;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Renderers;

/// <summary>Fond quadrillé en coordonnées monde — rend le pan et le zoom visibles.</summary>
public sealed class GridRenderer : IGameRenderer
{
    private const float Step = 50f;
    private readonly SKPaint _line = new() { Color = new SKColor(0x2A, 0x30, 0x3A), StrokeWidth = 1, IsAntialias = false };
    private readonly SKPaint _axis = new() { Color = new SKColor(0x45, 0x50, 0x60), StrokeWidth = 2, IsAntialias = false };

    public void Render(SKCanvas canvas, FrameContext context)
    {
        var cam = context.Camera;
        var topLeft = cam.ScreenToWorld(new SKPoint(0, 0));
        var bottomRight = cam.ScreenToWorld(new SKPoint(context.CanvasSize.Width, context.CanvasSize.Height));

        canvas.Save();
        cam.ApplyTo(canvas);
        _line.StrokeWidth = 1 / cam.Zoom;
        for (float x = MathF.Floor(topLeft.X / Step) * Step; x <= bottomRight.X; x += Step)
            canvas.DrawLine(x, topLeft.Y, x, bottomRight.Y, x == 0 ? _axis : _line);
        for (float y = MathF.Floor(topLeft.Y / Step) * Step; y <= bottomRight.Y; y += Step)
            canvas.DrawLine(topLeft.X, y, bottomRight.X, y, y == 0 ? _axis : _line);
        canvas.Restore();
    }

    public void Dispose()
    {
        _line.Dispose();
        _axis.Dispose();
    }
}

/// <summary>Exemple — dessine les producteurs, avec une pulsation à chaque cycle de production.</summary>
public sealed class EntityRenderer : IGameRenderer
{
    private readonly SKPaint _fill = new() { Color = new SKColor(0xE8, 0xB8, 0x4A), IsAntialias = true };
    private readonly SKPaint _ring = new() { Color = new SKColor(0xE8, 0xB8, 0x4A, 0x80), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };

    public void Render(SKCanvas canvas, FrameContext context)
    {
        var state = context.State;
        long tick = state.Clock.CurrentTick;
        canvas.Save();
        context.Camera.ApplyTo(canvas);
        for (int i = 0; i < state.Entities.Count; i++)
        {
            if (state.Entities[i] is not ProducerEntity p) continue;
            // Phase dans le cycle de production, en ticks de jeu : la pulsation suit la simulation,
            // pause et vitesse comprises.
            float phase = (tick - p.LastProductionTick) / (float)SampleContent.ProductionCooldownTicks;
            float r = 14 + 4 * p.Level;
            canvas.DrawCircle(p.X, p.Y, r, _fill);
            canvas.DrawCircle(p.X, p.Y, r + 18 * Math.Clamp(phase, 0, 1), _ring);
        }
        canvas.Restore();
    }

    public void Dispose()
    {
        _fill.Dispose();
        _ring.Dispose();
    }
}
