using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Animation;
using HeroesOfIdlestan.Core.Presentation.Battle;
using HeroesOfIdlestan.Core.Render.Animation;
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
    /// (5 combattants de front en partie de debug), plus une place côté équipe : celle proposée au
    /// bout d'une ligne pendant un glisser-déposer.
    /// </summary>
    public static SKRect BoundsFor(GameState state)
    {
        int widest = Math.Max(Formation.WidestLine(state.Heroes, state.HeroHoles) + 1, Formation.WidestLine(state.Monsters, state.MonsterHoles));
        float halfHeight = Math.Max(-Bounds.Top, (widest - 1) * HalfSlot + FighterRadius + 40f);
        return SKRect.Create(Bounds.Left, -halfHeight, Bounds.Width, 2 * halfHeight);
    }

    /// <summary>Position d'un combattant ; null s'il ne fait pas (ou plus) partie de la formation.</summary>
    public static SKPoint? PositionOf(Entity fighter, IReadOnlyList<Entity> side, IReadOnlyList<FormationHole> holes, bool isHero)
    {
        // Les places de la ligne, trous réservés compris : un mort reste dessiné là où il est tombé.
        if (Formation.Place(fighter, side, holes) is not (int index, int count)) return null;
        return SlotPosition(fighter.Line, index, count, isHero);
    }

    /// <summary>Position de la place <paramref name="index"/> parmi <paramref name="count"/> d'une ligne.</summary>
    public static SKPoint SlotPosition(int line, int index, int count, bool isHero)
    {
        float x = FrontOffset + (line - 1) * LineSpacing;
        float y = Formation.HalfPosition(index, count) * HalfSlot;
        return new SKPoint(isHero ? -x : x, y);
    }

    /// <summary>Position d'une place proposée au glisser-déposer.</summary>
    public static SKPoint TargetPosition(FormationTarget target) =>
        SlotPosition(target.Line, target.Index, target.Count, isHero: true);
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
/// Les combattants : le robot en planches de sprites (<see cref="SpriteSheet"/>, tirées du modèle 3D
/// de la version Godot par Tool.SpriteSheet), sa barre de vie, une courte fente vers l'adversaire à
/// chaque action, la marche quand sa place change (<see cref="FighterWalk"/>), et la chute à sa mort.
///
/// <para>Mêmes règles que <c>FighterView</c> côté Godot : animations du catalogue avec secours
/// (<see cref="AnimationResolver"/>), fond choisi par <see cref="FighterAnimation"/>, intro de
/// l'équipe en début de partie, attaque à chaque action, coup reçu quand la vie baisse ; teinte de
/// camp (bleue pour l'équipe, rouge pour les monstres, verte pour les gobelins), monstres réduits et
/// tournés vers la gauche ; sans animation de mort, la pose se fige et le corps tombe à la renverse,
/// et un monstre retiré du combat disparaît peu après.</para>
///
/// <para>Teinte des héros selon la classe : bleue pour le guerrier, ambre pour le voleur, violette
/// pour le mage.</para>
///
/// <para>Si les planches manquent, chaque combattant est un rond marqué de son initiale (G : guerrier,
/// V : voleur, M : mage, R : rat, G sur fond vert : gobelin), qui se dandine en marchant.</para>
/// </summary>
public sealed class FighterRenderer : IGameRenderer
{
    /// <summary>Jeu de planches des combattants, dans <c>assets/sprites/</c>.</summary>
    public const string SpriteSheetId = "bake3d-robot-animated";

    /// <summary>Durée de la fente qui suit une action, en ticks de jeu.</summary>
    private const float LungeTicks = 20f;
    private const float LungeDistance = 14f;
    /// <summary>Dandinement de la marche des ronds : écart latéral et gonflement à chaque pas.</summary>
    private const float SwayDistance = 4f;
    private const float StepSwell = 0.08f;
    /// <summary>Hauteur d'un héros dessiné, en unités monde ; les monstres sont réduits comme sous Godot.</summary>
    private const float HeroHeight = 72f;
    private const float MonsterScale = 0.6f;
    /// <summary>Chute ou relevé complet, en ticks de jeu.</summary>
    private const float FallTicks = 0.4f * GameClock.TicksPerSecond;
    /// <summary>Un monstre tué reste au sol ce temps-là (ticks de jeu) avant de disparaître.</summary>
    private const long CorpseTicks = 6 * GameClock.TicksPerSecond / 5;

    private static readonly SKColor HeroTint = new(0x40, 0x80, 0xD9, 0x1F);
    private static readonly SKColor RogueTint = new(0xD9, 0xA6, 0x26, 0x47);
    private static readonly SKColor MageTint = new(0x99, 0x4D, 0xE6, 0x52);
    private static readonly SKColor MonsterTint = new(0xCC, 0x40, 0x26, 0x4D);
    private static readonly SKColor GoblinTint = new(0x4D, 0xB3, 0x33, 0x59);
    private static readonly SKColor DeadTint = new(0x33, 0x33, 0x38, 0x99);

    /// <summary>Ce que le renderer retient d'un combattant d'une frame à l'autre.</summary>
    private sealed class Fighter(Entity entity, bool isHero, SpriteAnimator? animator)
    {
        public readonly Entity Entity = entity;
        public readonly bool IsHero = isHero;
        public readonly FighterWalk Walk = new();
        public readonly FighterCues Cues = new(entity);
        public readonly SpriteAnimator? Animator = animator;
        public bool WasAlive = entity.IsAlive;
        public float Fall;
        public long LastTick;
        /// <summary>Tick où le combattant a quitté la liste (monstre tué) ; null tant qu'il y est.</summary>
        public long? GoneTick;
        public bool FacingRight = isHero;
        public SKPoint Home;
    }

    private readonly SpriteSheet? _sheet;
    private readonly AnimationResolver? _resolver;
    private readonly Dictionary<Entity, Fighter> _fighters = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Entity> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly List<Entity> _gone = [];
    private readonly List<(float Y, Fighter Fighter)> _drawOrder = [];
    /// <summary>Vrai jusqu'à la première frame d'une partie : l'équipe y joue son intro.</summary>
    private bool _freshState = true;
    private volatile bool _resetPending;

    private readonly SKPaint _heroFill = new() { Color = new SKColor(0x3F, 0x7C, 0xD6), IsAntialias = true };
    private readonly SKPaint _rogueFill = new() { Color = new SKColor(0xB8, 0x8A, 0x22), IsAntialias = true };
    private readonly SKPaint _mageFill = new() { Color = new SKColor(0x7E, 0x48, 0xC4), IsAntialias = true };
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
    private readonly SKPaint _shadow = new() { Color = new SKColor(0, 0, 0, 0x50), IsAntialias = true };
    private readonly SKPaint _heroSprite = TintPaint(HeroTint);
    private readonly SKPaint _rogueSprite = TintPaint(RogueTint);
    private readonly SKPaint _mageSprite = TintPaint(MageTint);
    private readonly SKPaint _monsterSprite = TintPaint(MonsterTint);
    private readonly SKPaint _goblinSprite = TintPaint(GoblinTint);
    private readonly SKPaint _deadSprite = TintPaint(DeadTint);

    /// <param name="sheet">Planches des combattants ; null pour dessiner des ronds.</param>
    public FighterRenderer(SpriteSheet? sheet)
    {
        _sheet = sheet;
        if (sheet != null) _resolver = new AnimationResolver(sheet.Id, sheet.Data.Has);
    }

    /// <summary>Planches embarquées <see cref="SpriteSheetId"/>, ou des ronds si elles manquent.</summary>
    public FighterRenderer() : this(SpriteSheet.LoadEmbedded(SpriteSheetId)) { }

    /// <summary>Vrai si les combattants sont dessinés en sprites plutôt qu'en ronds.</summary>
    public bool UsesSprites => _sheet != null;

    /// <summary>Nouvelle partie ou chargement : tout repart de zéro à la frame suivante, sans chute.</summary>
    public void Reset() => _resetPending = true;

    public void Render(SKCanvas canvas, FrameContext context)
    {
        if (_resetPending)
        {
            _resetPending = false;
            _fighters.Clear();
            _freshState = true;
        }

        var state = context.State;
        long now = state.Clock.CurrentTick;

        _seen.Clear();
        _drawOrder.Clear();
        Sync(state.Heroes, state.HeroHoles, isHero: true, now);
        Sync(state.Monsters, state.MonsterHoles, isHero: false, now);
        _freshState = false;

        // Un combattant qui quitte la liste (monstre tué) tombe là où il était avant de disparaître ;
        // sans planches, il disparaît aussitôt.
        _gone.Clear();
        foreach (var (entity, fighter) in _fighters)
        {
            if (_seen.Contains(entity)) continue;
            fighter.GoneTick ??= now;
            if (_sheet == null || fighter.IsHero || now - fighter.GoneTick >= CorpseTicks || now < fighter.GoneTick)
                _gone.Add(entity);
            else
                _drawOrder.Add((fighter.Home.Y, fighter));
        }
        foreach (var entity in _gone) _fighters.Remove(entity);

        // Du fond vers l'avant : qui est plus bas à l'écran passe devant.
        _drawOrder.Sort((a, b) => a.Y.CompareTo(b.Y));
        canvas.Save();
        context.Camera.ApplyTo(canvas);
        foreach (var (_, fighter) in _drawOrder) Draw(canvas, state, fighter, now);
        canvas.Restore();
    }

    private void Sync(IReadOnlyList<Entity> side, IReadOnlyList<FormationHole> holes, bool isHero, long now)
    {
        foreach (var entity in side)
        {
            _seen.Add(entity);
            var place = BattleLayout.PositionOf(entity, side, holes, isHero);
            if (!_fighters.TryGetValue(entity, out var fighter))
            {
                if (place is null) continue;
                var animator = _sheet != null ? new SpriteAnimator(_sheet, _resolver!, playIntro: isHero && _freshState, now) : null;
                _fighters[entity] = fighter = new Fighter(entity, isHero, animator) { LastTick = now };
            }
            fighter.GoneTick = null;

            // Un vivant marche vers sa place ; un mort reste là où il est tombé. La première fois (vue
            // neuve, même d'un mort), il y est posé.
            if (place is { } target && (entity.IsAlive || !fighter.Walk.IsPlaced))
                fighter.Walk.Target(target.X, target.Y, now);
            else if (!entity.IsAlive)
                fighter.Walk.Halt(now);
            var (x, y) = fighter.Walk.PositionAt(now);
            fighter.Home = new SKPoint(x, y);
            _drawOrder.Add((y, fighter));
        }
    }

    private void Draw(SKCanvas canvas, GameState state, Fighter fighter, long now)
    {
        var entity = fighter.Entity;
        bool alive = entity.IsAlive && fighter.GoneTick == null;
        bool walking = alive && fighter.Walk.IsWalking(now);
        var position = fighter.Home;

        // Fente vers l'adversaire juste après une action, en ticks de jeu : elle suit pause et vitesse.
        // Qui n'a rien pu faire (aucune action depuis sa ligne, personne à portée) reste au repos.
        if (alive && entity.LastActionTick is { } lastAction)
        {
            long sinceAction = now - lastAction;
            if (sinceAction >= 0 && sinceAction < LungeTicks)
                position.X += (fighter.IsHero ? 1 : -1) * LungeDistance * (1 - sinceAction / LungeTicks);
        }

        float ratio = Math.Clamp(entity.Health / (float)CombatSystem.Stats(state, entity).MaxHealth, 0f, 1f);
        if (fighter.Animator is { } animator) DrawSprite(canvas, state, fighter, animator, position, alive, walking, ratio, now);
        else DrawCircle(canvas, fighter, position, alive, walking, ratio, now);
        fighter.LastTick = now;
    }

    // ── Planches ────────────────────────────────────────────────────────

    private void DrawSprite(SKCanvas canvas, GameState state, Fighter fighter, SpriteAnimator animator,
        SKPoint position, bool alive, bool walking, float healthRatio, long now)
    {
        var entity = fighter.Entity;
        var sheet = _sheet!;

        if (alive != fighter.WasAlive)
        {
            fighter.WasAlive = alive;
            if (alive) animator.Revive();
            else animator.Die(now);
        }
        if (alive)
        {
            var (acted, hurt) = fighter.Cues.Observe(entity);
            // L'attaque l'emporte : un coup reçu n'interrompt pas un coup porté.
            if (!walking && acted) animator.StartOneShot(AnimationCatalog.AttackFor(entity), now);
            else if (!walking && hurt && !animator.InOneShot) animator.StartOneShot(AnimationNames.Hurt, now);
        }

        // En marche, le corps regarde où il va ; arrivé, il refait face à l'adversaire.
        var (hx, _) = fighter.Walk.Heading;
        fighter.FacingRight = walking && MathF.Abs(hx) > 0.5f ? hx > 0 : fighter.IsHero;

        // Chute à la renverse quand il n'y a pas d'animation de mort ; relevé dans l'autre sens.
        float step = Math.Max(0, now - fighter.LastTick) / FallTicks;
        bool standing = alive || animator.HasDeathClip;
        fighter.Fall = Math.Clamp(fighter.Fall + (standing ? -step : step), 0f, 1f);

        string background = walking
            ? AnimationNames.Walk
            : FighterAnimation.Background(state.Run, fighter.IsHero, sheet.Data.Has(FighterAnimation.LookAround));
        if (animator.Frame(background, now) is not var (clip, frame)) return;

        float height = HeroHeight * (fighter.IsHero ? 1f : MonsterScale);
        float scale = height / sheet.Data.FrameHeight;
        // Les pieds un peu sous le centre de la case : le corps est centré sur sa place.
        var feet = new SKPoint(position.X, position.Y + height * 0.5f);

        canvas.DrawOval(feet, new SKSize(height * 0.22f * (1 - fighter.Fall * 0.3f), height * 0.06f), _shadow);

        canvas.Save();
        canvas.Translate(feet);
        float fall = 90f * SmoothStep(fighter.Fall);
        canvas.RotateDegrees(fighter.IsHero ? -fall : fall); // loin de l'adversaire
        if (!fighter.FacingRight) canvas.Scale(-1, 1);
        var paint = entity switch
        {
            _ when !alive => _deadSprite,
            HeroEntity { Class: HeroClass.Rogue } => _rogueSprite,
            HeroEntity { Class: HeroClass.Mage } => _mageSprite,
            HeroEntity => _heroSprite,
            MonsterEntity { Species: MonsterSpecies.Goblin } => _goblinSprite,
            _ => _monsterSprite,
        };
        sheet.Draw(canvas, clip, frame, scale, paint);
        canvas.Restore();

        if (alive) DrawHealthBar(canvas, new SKPoint(feet.X, feet.Y - height - 10), height * 0.6f, healthRatio);
    }

    private static float SmoothStep(float t) => t * t * (3 - 2 * t);

    /// <summary>Teinte de camp posée par-dessus l'image, sans toucher à sa transparence.</summary>
    private static SKPaint TintPaint(SKColor tint) => new()
    {
        IsAntialias = true,
        ColorFilter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.SrcATop),
    };

    private void DrawHealthBar(SKCanvas canvas, SKPoint center, float width, float ratio)
    {
        const float height = 6;
        var bar = SKRect.Create(center.X - width / 2, center.Y - height / 2, width, height);
        canvas.DrawRect(bar, _barBack);
        canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, width * ratio, height), _barHealth);
    }

    // ── Ronds (sans planches) ───────────────────────────────────────────

    private void DrawCircle(SKCanvas canvas, Fighter fighter, SKPoint position, bool alive, bool walking, float healthRatio, long now)
    {
        var entity = fighter.Entity;
        float r = BattleLayout.FighterRadius;

        if (walking)
        {
            // Un pas = un balancement d'un côté puis de l'autre, perpendiculaire à la marche.
            var (hx, hy) = fighter.Walk.Heading;
            float length = MathF.Sqrt(hx * hx + hy * hy);
            float swing = MathF.Sin(2 * MathF.PI * fighter.Walk.StepPhase(now));
            position.X += -hy / length * SwayDistance * swing;
            position.Y += hx / length * SwayDistance * swing;
            r *= 1 + StepSwell * MathF.Abs(swing);
        }

        var fill = entity switch
        {
            _ when !alive => _deadFill,
            HeroEntity { Class: HeroClass.Rogue } => _rogueFill,
            HeroEntity { Class: HeroClass.Mage } => _mageFill,
            HeroEntity => _heroFill,
            MonsterEntity { Species: MonsterSpecies.Goblin } => _goblinFill,
            _ => _monsterFill,
        };
        canvas.DrawCircle(position, r, fill);
        canvas.DrawCircle(position, r, _outline);
        DrawLetter(canvas, Initial(entity), position, r * 0.5f);

        if (alive) DrawHealthBar(canvas, new SKPoint(position.X, position.Y - r - 11), r * 2, healthRatio);
    }

    private static char Initial(Entity fighter) => fighter switch
    {
        HeroEntity { Class: HeroClass.Warrior } => 'G',
        HeroEntity { Class: HeroClass.Rogue } => 'V',
        HeroEntity { Class: HeroClass.Mage } => 'M',
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

        var v = new SKPath();
        v.MoveTo(-0.65f, -1f);
        v.LineTo(0f, 1f);
        v.LineTo(0.65f, -1f);

        var m = new SKPath();
        m.MoveTo(-0.7f, 1f);
        m.LineTo(-0.7f, -1f);
        m.LineTo(0f, 0.2f);
        m.LineTo(0.7f, -1f);
        m.LineTo(0.7f, 1f);

        return new Dictionary<char, SKPath> { ['G'] = g, ['R'] = r, ['V'] = v, ['M'] = m };
    }

    public void Dispose()
    {
        _sheet?.Dispose();
        _shadow.Dispose();
        foreach (var paint in new[] { _heroSprite, _rogueSprite, _mageSprite, _monsterSprite, _goblinSprite, _deadSprite })
        {
            paint.ColorFilter?.Dispose();
            paint.Dispose();
        }
        _heroFill.Dispose();
        _rogueFill.Dispose();
        _mageFill.Dispose();
        _monsterFill.Dispose();
        _goblinFill.Dispose();
        _deadFill.Dispose();
        _outline.Dispose();
        _letter.Dispose();
        _barBack.Dispose();
        _barHealth.Dispose();
    }
}
