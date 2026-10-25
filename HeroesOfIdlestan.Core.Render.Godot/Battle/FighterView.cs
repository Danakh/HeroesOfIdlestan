using Godot;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Render.Godot.Battle;

/// <summary>
/// Un combattant dans la scène : le modèle animé, sa barre de vie, une fente vers l'adversaire à
/// chaque action, et la chute à sa mort.
///
/// <para>Tout suit le temps de jeu, pas le temps réel : les animations ralentissent, accélèrent ou
/// s'arrêtent avec la vitesse et la pause, comme la simulation.</para>
///
/// <para>Hiérarchie : <see cref="Root"/> (position) → pivot (chute) → modèle (orientation, échelle).
/// La barre de vie pend à <see cref="Root"/> pour rester droite quand le corps tombe.</para>
/// </summary>
internal sealed class FighterView
{
    /// <summary>Animations du modèle, nommées comme dans le glTF.</summary>
    public const string Idle = "Idle";
    public const string Walk = "Walk";
    public const string Run = "Run";
    public const string LookAround = "Look around";
    public const string Intro = "Intro";
    private static readonly string[] Loops = [Idle, Walk, Run, LookAround];

    /// <summary>Durée de la fente qui suit une action, en ticks de jeu.</summary>
    private const float LungeTicks = 20f;
    private const float LungeDistance = 0.6f;
    /// <summary>Chute ou relevé complet, en secondes de jeu.</summary>
    private const float FallSeconds = 0.4f;
    /// <summary>Un monstre tué reste au sol ce temps-là (secondes de jeu) avant de disparaître.</summary>
    private const float CorpseSeconds = 1.2f;
    private const float BlendSeconds = 0.25f;
    private const float ModelHeight = 1.9f;
    private const float MonsterScale = 0.6f;
    private const float BarWidth = 0.9f;
    private const float BarHeight = 0.09f;

    private static readonly Color HeroTint = new(0.25f, 0.5f, 0.85f, 0.12f);
    private static readonly Color MonsterTint = new(0.8f, 0.25f, 0.15f, 0.3f);
    private static readonly Color DeadTint = new(0.2f, 0.2f, 0.22f, 0.6f);

    private readonly bool _isHero;
    private readonly Node3D _pivot;
    private readonly AnimationPlayer? _animation;
    private readonly List<GeometryInstance3D> _meshes = [];
    private readonly Node3D _bar;
    private readonly MeshInstance3D _barFill;
    private readonly float _height;
    private Vector3 _home;
    private bool _wasAlive = true;
    private float _fall;
    private float _corpseTime;

    public FighterView(PackedScene? model, bool isHero, bool playIntro)
    {
        _isHero = isHero;
        float scale = isHero ? 1f : MonsterScale;
        _height = ModelHeight * scale;

        Root = new Node3D { Name = isHero ? "Hero" : "Monster" };
        _pivot = new Node3D { Name = "Pivot" };
        Root.AddChild(_pivot);

        var body = model?.Instantiate<Node3D>() ?? Placeholder();
        // Le glTF regarde vers +Z : un quart de tour l'oriente vers le camp d'en face.
        body.RotationDegrees = new Vector3(0, isHero ? 90 : -90, 0);
        body.Scale = Vector3.One * scale;
        _pivot.AddChild(body);
        Collect(body);
        SetTint(isHero ? HeroTint : MonsterTint);

        _animation = FindAnimationPlayer(body);
        if (_animation != null)
        {
            foreach (var name in Loops)
                if (_animation.HasAnimation(name)) _animation.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
            Play(playIntro ? Intro : Idle);
        }

        (_bar, _barFill) = BuildHealthBar();
        _bar.Position = new Vector3(0, _height + 0.35f, 0);
        Root.AddChild(_bar);
    }

    public Node3D Root { get; }

    /// <summary>Vrai quand un monstre retiré du combat a fini de tomber et peut être libéré.</summary>
    public bool CorpseDone => _corpseTime >= CorpseSeconds;

    /// <summary>Recale l'état visuel sur celui du combattant ; <paramref name="speed"/> vaut 0 en pause.</summary>
    public void Update(GameState state, Entity fighter, IReadOnlyList<Entity> side, float speed, float delta, Basis camera)
    {
        if (BattleLayout.PositionOf(fighter, side, _isHero) is { } home) _home = home;
        var stats = CombatSystem.Stats(state, fighter);

        var position = _home;
        if (fighter.IsAlive)
        {
            // Fente vers l'adversaire juste après une action, en ticks de jeu : elle suit pause et vitesse.
            long sinceAction = state.Clock.CurrentTick - (fighter.NextActionTick - stats.CooldownTicks);
            if (sinceAction >= 0 && sinceAction < LungeTicks)
                position.X += (_isHero ? 1 : -1) * LungeDistance * (1 - sinceAction / LungeTicks);
        }
        Root.Position = position;

        if (fighter.IsAlive != _wasAlive)
        {
            _wasAlive = fighter.IsAlive;
            SetTint(fighter.IsAlive ? (_isHero ? HeroTint : MonsterTint) : DeadTint);
        }
        UpdateFall(fighter.IsAlive, speed, delta);
        UpdateAnimation(fighter.IsAlive ? ChooseAnimation(state.Run) : null, speed);

        _bar.Visible = fighter.IsAlive;
        if (fighter.IsAlive) UpdateHealthBar(Math.Clamp(fighter.Health / (float)stats.MaxHealth, 0f, 1f), camera);
    }

    /// <summary>Monstre retiré de la liste à sa mort : il tombe là où il était, puis disparaît.</summary>
    public void UpdateCorpse(float speed, float delta)
    {
        if (_wasAlive)
        {
            _wasAlive = false;
            SetTint(DeadTint);
            _bar.Visible = false;
        }
        Root.Position = _home;
        UpdateFall(alive: false, speed, delta);
        UpdateAnimation(null, speed);
        _corpseTime += delta * speed;
    }

    public void Free()
    {
        if (GodotObject.IsInstanceValid(Root)) Root.QueueFree();
    }

    // ── Animation ────────────────────────────────────────────────────────

    private string ChooseAnimation(RunState run)
    {
        if (!_isHero) return Idle;
        if (run.Phase == RunPhase.Fighting) return Idle;
        // Repos après une victoire : en route vers la zone suivante, ou on reste sur place.
        if (run.LastOutcome == FightOutcome.Victory) return run.AutoAdvance ? Walk : LookAround;
        return Idle;
    }

    /// <param name="wanted">Animation voulue, ou null pour figer la pose (mort).</param>
    private void UpdateAnimation(string? wanted, float speed)
    {
        if (_animation == null) return;
        _animation.SpeedScale = wanted == null ? 0 : speed;
        if (wanted == null) return;
        // L'intro se joue jusqu'au bout avant de céder la place.
        if (_animation.CurrentAnimation == Intro && _animation.IsPlaying()) return;
        if (_animation.CurrentAnimation != wanted) Play(wanted);
    }

    private void Play(string name)
    {
        if (_animation != null && _animation.HasAnimation(name)) _animation.Play(name, BlendSeconds);
    }

    private void UpdateFall(bool alive, float speed, float delta)
    {
        float step = delta * speed / FallSeconds;
        _fall = Mathf.Clamp(_fall + (alive ? -step : step), 0f, 1f);
        // Chacun tombe à la renverse, loin de l'adversaire.
        float angle = Mathf.Pi / 2 * Mathf.SmoothStep(0, 1, _fall) * (_isHero ? 1 : -1);
        _pivot.Rotation = new Vector3(0, 0, angle);
    }

    // ── Habillage ────────────────────────────────────────────────────────

    private void Collect(Node node)
    {
        if (node is GeometryInstance3D geometry) _meshes.Add(geometry);
        foreach (var child in node.GetChildren()) Collect(child);
    }

    /// <summary>Teinte de camp posée par-dessus les matériaux du modèle, sans les modifier.</summary>
    private void SetTint(Color tint)
    {
        var overlay = new StandardMaterial3D
        {
            AlbedoColor = tint,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        foreach (var mesh in _meshes) mesh.MaterialOverlay = overlay;
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;
        foreach (var child in node.GetChildren())
            if (FindAnimationPlayer(child) is { } found) return found;
        return null;
    }

    /// <summary>Modèle de secours si le glTF manque : une capsule à la taille du personnage.</summary>
    private static Node3D Placeholder()
    {
        var capsule = new MeshInstance3D { Mesh = new CapsuleMesh { Height = ModelHeight, Radius = 0.35f } };
        capsule.Position = new Vector3(0, ModelHeight / 2, 0);
        var root = new Node3D { Name = "Placeholder" };
        root.AddChild(capsule);
        return root;
    }

    // ── Barre de vie ─────────────────────────────────────────────────────

    private static (Node3D Bar, MeshInstance3D Fill) BuildHealthBar()
    {
        var bar = new Node3D { Name = "HealthBar" };
        var back = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(BarWidth + 0.04f, BarHeight + 0.04f) } };
        back.MaterialOverride = BarMaterial(new Color(0.06f, 0.08f, 0.1f, 0.75f), priority: 0);
        var fill = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(BarWidth, BarHeight) } };
        fill.MaterialOverride = BarMaterial(new Color(0.36f, 0.78f, 0.36f), priority: 1);
        bar.AddChild(back);
        bar.AddChild(fill);
        return (bar, fill);
    }

    private static StandardMaterial3D BarMaterial(Color color, int priority) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        RenderPriority = priority,
    };

    private void UpdateHealthBar(float ratio, Basis camera)
    {
        // Toujours face à la caméra : Root n'est que translaté, la base locale est donc la base monde.
        _bar.Basis = camera;
        _barFill.Scale = new Vector3(Math.Max(ratio, 0.001f), 1, 1);
        _barFill.Position = new Vector3(-(1 - ratio) * BarWidth / 2, 0, 0.001f);
    }
}
