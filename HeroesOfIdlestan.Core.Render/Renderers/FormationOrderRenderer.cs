using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Battle;
using HeroesOfIdlestan.Core.Render.Rendering;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Renderers;

/// <summary>
/// La réorganisation de la formation, par-dessus les combattants : une flèche par déplacement
/// demandé (<see cref="GameState.FormationMoves"/>), du personnage à sa future place, jusqu'à ce
/// qu'il l'exécute à son prochain cycle ; pendant un glisser (<see cref="FormationDrag"/>), les
/// places proposées (cercle sur un personnage à échanger, cercle pointillé au bout d'une ligne), celle
/// sous le pointeur en surbrillance, et la flèche qui suit le pointeur.
/// </summary>
public sealed class FormationOrderRenderer(FormationDrag drag) : IGameRenderer
{
    private const float ArrowWidth = 5f;
    private const float HeadLength = 16f;
    private const float HeadWidth = 12f;
    /// <summary>La flèche part du bord du personnage et s'arrête au bord de la place visée.</summary>
    private const float EndGap = BattleLayout.FighterRadius * 0.9f;
    private const float SlotRadius = BattleLayout.FighterRadius * 1.15f;

    private readonly SKPaint _pending = ArrowPaint(new SKColor(0xF2, 0xC2, 0x30, 0xE0));
    private readonly SKPaint _preview = ArrowPaint(new SKColor(0xFF, 0xFF, 0xFF, 0xD0));
    private readonly SKPaint _slot = new()
    {
        Color = new SKColor(0x9C, 0xD8, 0xFF, 0xA0), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3,
    };
    private readonly SKPaint _freeSlot = new()
    {
        Color = new SKColor(0x9C, 0xD8, 0xFF, 0xA0), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3,
        PathEffect = SKPathEffect.CreateDash([8, 6], 0),
    };
    private readonly SKPaint _hovered = new() { Color = new SKColor(0x9C, 0xD8, 0xFF, 0x50), IsAntialias = true };

    public void Render(SKCanvas canvas, FrameContext context)
    {
        var state = context.State;
        canvas.Save();
        context.Camera.ApplyTo(canvas);

        foreach (var move in state.FormationMoves)
        {
            if (From(state, move.Hero) is not { } from || FormationOrders.TargetOf(state, move) is not { } target) continue;
            DrawArrow(canvas, from, BattleLayout.TargetPosition(target), _pending, EndGap);
        }

        if (drag.Hero is int hero && From(state, hero) is { } origin)
        {
            foreach (var target in drag.Targets)
            {
                var p = BattleLayout.TargetPosition(target);
                if (target == drag.Hovered) canvas.DrawCircle(p, SlotRadius, _hovered);
                canvas.DrawCircle(p, SlotRadius, target.SwapWith == null ? _freeSlot : _slot);
            }
            if (!drag.HoversSelf)
            {
                bool snapped = drag.Hovered != null;
                var end = snapped ? BattleLayout.TargetPosition(drag.Hovered!) : new SKPoint(drag.Pointer.X, drag.Pointer.Y);
                DrawArrow(canvas, origin, end, _preview, snapped ? EndGap : 0);
            }
        }

        canvas.Restore();
    }

    private static SKPoint? From(GameState state, int hero) =>
        hero >= 0 && hero < state.Heroes.Count
            ? BattleLayout.PositionOf(state.Heroes[hero], state.Heroes, state.HeroHoles, isHero: true)
            : null;

    /// <summary>Un trait et une pointe de <paramref name="from"/> à <paramref name="to"/>, raccourci de <paramref name="endGap"/> à l'arrivée.</summary>
    private static void DrawArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKPaint paint, float endGap)
    {
        var delta = to - from;
        float length = delta.Length;
        if (length < 2 * EndGap) return;
        var dir = new SKPoint(delta.X / length, delta.Y / length);
        var normal = new SKPoint(-dir.Y, dir.X);
        var start = from + Scale(dir, EndGap);
        var tip = to - Scale(dir, endGap);
        var neck = tip - Scale(dir, HeadLength);

        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(start, neck, paint);
        using var head = new SKPath();
        head.MoveTo(tip);
        head.LineTo(neck + Scale(normal, HeadWidth));
        head.LineTo(neck - Scale(normal, HeadWidth));
        head.Close();
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(head, paint);
    }

    private static SKPoint Scale(SKPoint v, float k) => new(v.X * k, v.Y * k);

    private static SKPaint ArrowPaint(SKColor color) => new()
    {
        Color = color, IsAntialias = true, StrokeWidth = ArrowWidth, StrokeCap = SKStrokeCap.Round,
    };

    public void Dispose()
    {
        _pending.Dispose();
        _preview.Dispose();
        _slot.Dispose();
        _freeSlot.PathEffect?.Dispose();
        _freeSlot.Dispose();
        _hovered.Dispose();
    }
}
