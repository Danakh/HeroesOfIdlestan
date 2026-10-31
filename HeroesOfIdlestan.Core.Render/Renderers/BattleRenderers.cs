using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Render.Rendering;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Renderers;

/// <summary>
/// Placement des combattants en coordonnées monde. L'équipe est à gauche, les monstres à droite ;
/// la ligne 1 de chaque camp fait face à l'autre au centre, les lignes suivantes s'éloignent, et
/// chaque ligne se lit de haut en bas.
/// </summary>
public static class BattleLayout
{
    /// <summary>Distance entre le centre du champ de bataille et la ligne 1 de chaque camp.</summary>
    public const float FrontOffset = 60f;
    public const float LineSpacing = 90f;
    /// <summary>Écart vertical pour une demi-case (deux voisins d'une même ligne sont à 2 demi-cases).</summary>
    public const float HalfSlot = 40f;
    public const float FighterRadius = 26f;

    /// <summary>Champ de bataille minimal : trois lignes par camp, quelques combattants par ligne.</summary>
    public static readonly SKRect Bounds = SKRect.Create(-260, -160, 520, 320);

    /// <summary>
    /// Le champ de bataille, agrandi en hauteur pour tenir la ligne la plus longue des deux camps
    /// (15 combattants de front en partie de debug).
    /// </summary>
    public static SKRect BoundsFor(GameState state)
    {
        int widest = Math.Max(WidestLine(state.Heroes), WidestLine(state.Monsters));
        float halfHeight = Math.Max(-Bounds.Top, (widest - 1) * HalfSlot + FighterRadius + 40f);
        return SKRect.Create(Bounds.Left, -halfHeight, Bounds.Width, 2 * halfHeight);
    }

    private static int WidestLine(IEnumerable<Entity> side) =>
        side.GroupBy(f => f.Line).Select(g => g.Count()).DefaultIfEmpty(0).Max();

    /// <summary>Position d'un combattant ; null s'il ne fait pas (ou plus) partie de la formation.</summary>
    public static SKPoint? PositionOf(Entity fighter, IReadOnlyList<Entity> side, bool isHero)
    {
        // Les vivants se resserrent ; un mort reste dessiné là où il est tombé, parmi toute sa ligne.
        var line = fighter.IsAlive
            ? Formation.LineOf(side, fighter.Line)
            : side.Where(f => f.Line == fighter.Line).ToList();
        int index = line.IndexOf(fighter);
        if (index < 0) return null;

        float x = FrontOffset + (fighter.Line - 1) * LineSpacing;
        float y = Formation.HalfPosition(index, line.Count) * HalfSlot;
        return new SKPoint(isHero ? -x : x, y);
    }
}

/// <summary>Le sol du champ de bataille, en coordonnées monde — rend aussi le pan et le zoom visibles.</summary>
public sealed class GroundRenderer : IGameRenderer
{
    private readonly SKPaint _ground = new() { Color = new SKColor(0x22, 0x2A, 0x22), IsAntialias = true };
    private readonly SKPaint _midline = new() { Color = new SKColor(0x33, 0x3D, 0x33), StrokeWidth = 2, IsAntialias = true };

    public void Render(SKCanvas canvas, FrameContext context)
    {
        var bounds = BattleLayout.BoundsFor(context.State);
        canvas.Save();
        context.Camera.ApplyTo(canvas);
        canvas.DrawRoundRect(bounds, 24, 24, _ground);
        canvas.DrawLine(0, bounds.Top + 20, 0, bounds.Bottom - 20, _midline);
        canvas.Restore();
    }

    public void Dispose()
    {
        _ground.Dispose();
        _midline.Dispose();
    }
}

/// <summary>
/// Les combattants : un rond marqué de l'initiale de sa classe ou de son espèce (G : guerrier,
/// R : rat, G sur fond vert : gobelin), sa barre de vie, et une courte fente vers l'adversaire à
/// chaque action.
/// </summary>
public sealed class FighterRenderer : IGameRenderer
{
    /// <summary>Durée de la fente qui suit une action, en ticks de jeu.</summary>
    private const float LungeTicks = 20f;
    private const float LungeDistance = 14f;

    private readonly SKPaint _heroFill = new() { Color = new SKColor(0x3F, 0x7C, 0xD6), IsAntialias = true };
    private readonly SKPaint _monsterFill = new() { Color = new SKColor(0xA8, 0x55, 0x3A), IsAntialias = true };
    private readonly SKPaint _goblinFill = new() { Color = new SKColor(0x4E, 0x8A, 0x3A), IsAntialias = true };
    private readonly SKPaint _deadFill = new() { Color = new SKColor(0x55, 0x5A, 0x60, 0x90), IsAntialias = true };
    private readonly SKPaint _outline = new() { Color = new SKColor(0x10, 0x14, 0x1A), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
    private readonly SKPaint _letter = new()
    {
        Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    private readonly SKPaint _barBack = new() { Color = new SKColor(0x10, 0x14, 0x1A, 0xC0), IsAntialias = true };
    private readonly SKPaint _barHealth = new() { Color = new SKColor(0x5C, 0xC8, 0x5C), IsAntialias = true };

    public void Render(SKCanvas canvas, FrameContext context)
    {
        var state = context.State;
        canvas.Save();
        context.Camera.ApplyTo(canvas);

        for (int i = 0; i < state.Heroes.Count; i++)
            Draw(canvas, context, state.Heroes[i], state.Heroes, isHero: true);
        for (int i = 0; i < state.Monsters.Count; i++)
            Draw(canvas, context, state.Monsters[i], state.Monsters, isHero: false);

        canvas.Restore();
    }

    private void Draw(SKCanvas canvas, FrameContext context, Entity fighter, IReadOnlyList<Entity> side, bool isHero)
    {
        if (BattleLayout.PositionOf(fighter, side, isHero) is not { } position) return;

        var state = context.State;
        var stats = CombatSystem.Stats(state, fighter);
        float r = BattleLayout.FighterRadius;

        if (fighter.IsAlive)
        {
            // Fente vers l'adversaire juste après une action, en ticks de jeu : elle suit pause et vitesse.
            long sinceAction = state.Clock.CurrentTick - (fighter.NextActionTick - stats.CooldownTicks);
            if (sinceAction >= 0 && sinceAction < LungeTicks)
                position.X += (isHero ? 1 : -1) * LungeDistance * (1 - sinceAction / LungeTicks);
        }

        var fill = fighter switch
        {
            { IsAlive: false } => _deadFill,
            HeroEntity => _heroFill,
            MonsterEntity { Species: MonsterSpecies.Goblin } => _goblinFill,
            _ => _monsterFill,
        };
        canvas.DrawCircle(position, r, fill);
        canvas.DrawCircle(position, r, _outline);
        DrawLetter(canvas, Initial(fighter), position, r * 0.5f);

        if (!fighter.IsAlive) return;
        float width = r * 2, height = 6;
        var bar = SKRect.Create(position.X - r, position.Y - r - 14, width, height);
        float ratio = Math.Clamp(fighter.Health / (float)stats.MaxHealth, 0f, 1f);
        canvas.DrawRect(bar, _barBack);
        canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, width * ratio, height), _barHealth);
    }

    private static char Initial(Entity fighter) => fighter switch
    {
        HeroEntity { Class: HeroClass.Warrior } => 'G',
        MonsterEntity { Species: MonsterSpecies.Rat } => 'R',
        MonsterEntity { Species: MonsterSpecies.Goblin } => 'G',
        _ => '?',
    };

    /// <summary>
    /// Lettre tracée en vecteurs plutôt qu'en texte : le navigateur n'a pas de police système pour
    /// Skia, et un tracé ne dépend d'aucune.
    /// </summary>
    private void DrawLetter(SKCanvas canvas, char letter, SKPoint center, float halfHeight)
    {
        if (!LetterPaths.TryGetValue(letter, out var path)) return;
        canvas.Save();
        canvas.Translate(center);
        canvas.Scale(halfHeight);
        _letter.StrokeWidth = 0.32f;
        canvas.DrawPath(path, _letter);
        canvas.Restore();
    }

    /// <summary>Lettres dans un carré de −1 à 1.</summary>
    private static readonly Dictionary<char, SKPath> LetterPaths = BuildLetters();

    private static Dictionary<char, SKPath> BuildLetters()
    {
        var g = new SKPath();
        g.AddArc(SKRect.Create(-0.75f, -1f, 1.5f, 2f), -40, -280);
        g.LineTo(0.62f, 0.15f);
        g.LineTo(0.1f, 0.15f);

        var r = new SKPath();
        r.MoveTo(-0.55f, 1f);
        r.LineTo(-0.55f, -1f);
        r.LineTo(0.05f, -1f);
        r.ArcTo(SKRect.Create(-0.45f, -1f, 1f, 1f), -90, 180, false);
        r.LineTo(-0.55f, 0f);
        r.MoveTo(0f, 0f);
        r.LineTo(0.6f, 1f);

        return new Dictionary<char, SKPath> { ['G'] = g, ['R'] = r };
    }

    public void Dispose()
    {
        _heroFill.Dispose();
        _monsterFill.Dispose();
        _goblinFill.Dispose();
        _deadFill.Dispose();
        _outline.Dispose();
        _letter.Dispose();
        _barBack.Dispose();
        _barHealth.Dispose();
    }
}
