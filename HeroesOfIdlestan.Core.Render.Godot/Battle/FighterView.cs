using Godot;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Animation;
using HeroesOfIdlestan.Core.Presentation.Battle;

namespace HeroesOfIdlestan.Core.Render.Godot.Battle;

/// <summary>
/// Un combattant dans la scène : le modèle animé, sa barre de vie, une fente vers l'adversaire à
/// chaque action, la marche quand sa place change (<see cref="FighterWalk"/> : animation de marche,
/// tourné dans le sens de la marche), et la chute à sa mort.
///
/// <para>Animations : celles du catalogue (<see cref="AnimationNames"/>) passent par un
/// <see cref="AnimationResolver"/>, qui remplace une animation absente du glTF par son secours et le
/// signale dans le journal. Attaque à chaque action, coup reçu quand la vie baisse, mort tenue sur sa
/// dernière image. Sans clip de mort, la pose se fige et le corps tombe à la renverse.</para>
///
/// <para>Tout suit le temps de jeu, pas le temps réel : les animations ralentissent, accélèrent ou
/// s'arrêtent avec la vitesse et la pause, comme la simulation.</para>
///
/// <para>Hiérarchie : <see cref="Root"/> (position) → pivot (chute) → modèle (orientation, échelle).
/// La barre de vie pend à <see cref="Root"/> pour rester droite quand le corps tombe.</para>
/// </summary>
internal sealed class FighterView
{
    /// <summary>
    /// Animations propres au modèle de test, hors catalogue : jouées si elles existent, sans
    /// secours ni avertissement.
    /// </summary>
    public const string LookAround = "Look around";
    public const string Intro = "Intro";
    private static readonly string[] Loops = [AnimationNames.Idle, AnimationNames.Walk, "Run", LookAround];

    /// <summary>Durée de la fente qui suit une action, en ticks de jeu.</summary>
    private const float LungeTicks = 20f;
    private const float LungeDistance = 0.6f;
    /// <summary>Chute ou relevé complet, en secondes de jeu.</summary>
    private const float FallSeconds = 0.4f;
    /// <summary>Un monstre tué reste au sol ce temps-là (secondes de jeu) avant de disparaître.</summary>
    private const float CorpseSeconds = 1.2f;
    private const float BlendSeconds = 0.25f;
    /// <summary>Vitesse à laquelle le corps se tourne vers sa direction (marche ou adversaire), par seconde de jeu.</summary>
    private const float TurnRate = 12f;
    private const float ModelHeight = 1.9f;
    private const float MonsterScale = 0.6f;
    private const float BarWidth = 0.9f;
    private const float BarHeight = 0.09f;

    private static readonly Color HeroTint = new(0.25f, 0.5f, 0.85f, 0.12f);
    private static readonly Color MonsterTint = new(0.8f, 0.25f, 0.15f, 0.3f);
    private static readonly Color GoblinTint = new(0.3f, 0.7f, 0.2f, 0.35f);
    private static readonly Color DeadTint = new(0.2f, 0.2f, 0.22f, 0.6f);

    private readonly bool _isHero;
    private readonly Color _aliveTint;
    private readonly Node3D _pivot;
    private readonly AnimationPlayer? _animation;
    /// <summary>Nom du clip glTF, retrouvé sans tenir compte de la casse (« Idle » pour idle).</summary>
    private readonly Dictionary<string, string> _clips = new(StringComparer.OrdinalIgnoreCase);
    private readonly AnimationResolver? _resolver;
    /// <summary>Attaque ou coup reçu en cours : joué jusqu'au bout avant de revenir au fond.</summary>
    private string? _oneShot;
    private long? _lastActionSeen;
    private long _lastHealth;
    private bool _deathStarted;
    private readonly List<GeometryInstance3D> _meshes = [];
    private readonly Node3D _bar;
    private readonly MeshInstance3D _barFill;
    private readonly float _height;
    private readonly Node3D _body;
    /// <summary>Orientation face au camp d'en face (le glTF regarde vers +Z).</summary>
    private readonly float _facingYaw;
    private readonly FighterWalk _walk = new();
    /// <summary>Dernière position affichée, hors fente : là où le corps tombe.</summary>
    private Vector3 _home;
    private bool _wasAlive = true;
    private float _fall;
    private float _corpseTime;

    public FighterView(PackedScene? model, Entity fighter, bool playIntro)
    {
        bool isHero = fighter is HeroEntity;
        _isHero = isHero;
        _aliveTint = fighter switch
        {
            HeroEntity => HeroTint,
            MonsterEntity { Species: MonsterSpecies.Goblin } => GoblinTint,
            _ => MonsterTint,
        };
        float scale = isHero ? 1f : MonsterScale;
        _height = ModelHeight * scale;

        Root = new Node3D { Name = isHero ? "Hero" : "Monster" };
        _pivot = new Node3D { Name = "Pivot" };
        Root.AddChild(_pivot);

        var body = model?.Instantiate<Node3D>() ?? Placeholder();
        _body = body;
        // Le glTF regarde vers +Z : un quart de tour l'oriente vers le camp d'en face.
        _facingYaw = Mathf.DegToRad(isHero ? 90 : -90);
        body.Rotation = new Vector3(0, _facingYaw, 0);
        body.Scale = Vector3.One * scale;
        _pivot.AddChild(body);
        Collect(body);
        SetTint(_aliveTint);

        _lastActionSeen = fighter.LastActionTick;
        _lastHealth = fighter.Health;
        _animation = FindAnimationPlayer(body);
        if (_animation != null)
        {
            foreach (var clip in _animation.GetAnimationList()) _clips.TryAdd(clip, clip);
            string owner = model != null ? System.IO.Path.GetFileNameWithoutExtension(model.ResourcePath) : "placeholder";
            _resolver = new AnimationResolver(owner, _clips.ContainsKey);
            foreach (var name in Loops)
                if (_clips.TryGetValue(name, out var clip)) _animation.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
            if (playIntro && _clips.ContainsKey(Intro)) Play(Intro);
            else if (Resolve(AnimationNames.Idle) is { } idle) Play(idle);
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
        long now = state.Clock.CurrentTick;
        var place = BattleLayout.PositionOf(fighter, side, _isHero);
        // Un vivant marche vers sa place ; un mort reste là où il est tombé. La première fois (vue
        // neuve, même d'un mort), il y est posé.
        if (place is { } target && (fighter.IsAlive || !_walk.IsPlaced))
            _walk.Target(target.X, target.Z, now);
        else if (!fighter.IsAlive)
            _walk.Halt(now);
        var (x, z) = _walk.PositionAt(now);
        _home = new Vector3(x, 0, z);
        bool walking = fighter.IsAlive && _walk.IsWalking(now);
        UpdateFacing(walking, speed, delta);
        var stats = CombatSystem.Stats(state, fighter);

        var position = _home;
        // Fente vers l'adversaire juste après une action, en ticks de jeu : elle suit pause et vitesse.
        // Qui n'a rien pu faire (aucune action depuis sa ligne, personne à portée) reste au repos.
        if (fighter.IsAlive && fighter.LastActionTick is { } lastAction)
        {
            long sinceAction = state.Clock.CurrentTick - lastAction;
            if (sinceAction >= 0 && sinceAction < LungeTicks)
                position.X += (_isHero ? 1 : -1) * LungeDistance * (1 - sinceAction / LungeTicks);
        }
        Root.Position = position;

        if (fighter.IsAlive != _wasAlive)
        {
            _wasAlive = fighter.IsAlive;
            SetTint(fighter.IsAlive ? _aliveTint : DeadTint);
            // Relevé (retour en place après une défaite) : sa prochaine mort se rejouera.
            if (fighter.IsAlive) _deathStarted = false;
        }
        if (fighter.IsAlive) DetectOneShots(fighter, walking);
        UpdateFall(fighter.IsAlive || HasDeathClip, speed, delta);
        if (fighter.IsAlive) UpdateAnimation(walking ? AnimationNames.Walk : ChooseAnimation(state.Run), speed);
        else UpdateDeath(speed);

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
        UpdateFall(alive: HasDeathClip, speed, delta);
        UpdateDeath(speed);
        _corpseTime += delta * speed;
    }

    public void Free()
    {
        if (GodotObject.IsInstanceValid(Root)) Root.QueueFree();
    }

    // ── Animation ────────────────────────────────────────────────────────

    /// <summary>L'animation de fond, hors attaque et coup reçu : un nom du catalogue, ou <see cref="LookAround"/>.</summary>
    private string ChooseAnimation(RunState run)
    {
        if (!_isHero) return AnimationNames.Idle;
        if (run.Phase == RunPhase.Fighting) return AnimationNames.Idle;
        // Repos après une victoire : en route vers la zone suivante, ou on reste sur place.
        if (run.LastOutcome == FightOutcome.Victory)
            return run.AutoAdvance ? AnimationNames.Walk : _clips.ContainsKey(LookAround) ? LookAround : AnimationNames.Idle;
        return AnimationNames.Idle;
    }

    /// <summary>Lance l'attaque à chaque nouvelle action, le coup reçu quand la vie baisse.</summary>
    private void DetectOneShots(Entity fighter, bool walking)
    {
        long health = fighter.Health;
        bool acted = fighter.LastActionTick is { } action && action != _lastActionSeen;
        bool hurt = health < _lastHealth;
        _lastActionSeen = fighter.LastActionTick;
        _lastHealth = health;
        if (walking || _animation == null) return;

        // L'attaque l'emporte : un coup reçu n'interrompt pas un coup porté.
        if (acted) StartOneShot(AnimationCatalog.AttackFor(fighter));
        else if (hurt && _oneShot == null) StartOneShot(AnimationNames.Hurt);
    }

    private void StartOneShot(string name)
    {
        var clip = Resolve(name);
        // Rabattue sur l'attente (ou sur rien), l'animation n'apporte rien : on garde le fond.
        if (clip == null || clip == Resolve(AnimationNames.Idle)) return;
        _oneShot = clip;
        if (_animation!.CurrentAnimation == clip) _animation.Seek(0, update: true);
        else _animation.Play(clip, BlendSeconds);
    }

    /// <param name="wanted">Nom du catalogue (résolu ici, avec secours) ou <see cref="LookAround"/>.</param>
    private void UpdateAnimation(string wanted, float speed)
    {
        if (_animation == null) return;
        _animation.SpeedScale = speed;
        // L'intro, puis une attaque ou un coup reçu, se jouent jusqu'au bout avant de céder la place.
        if (_animation.IsPlaying() && _animation.CurrentAnimation == Intro) return;
        if (_oneShot != null)
        {
            if (_animation.IsPlaying() && _animation.CurrentAnimation == _oneShot) return;
            _oneShot = null;
        }
        var clip = wanted == LookAround ? LookAround : Resolve(wanted);
        if (clip != null && _animation.CurrentAnimation != clip) Play(clip);
    }

    /// <summary>Joue la mort une fois et tient sa dernière image ; sans clip de mort, fige la pose.</summary>
    private void UpdateDeath(float speed)
    {
        if (_animation == null) return;
        if (!_deathStarted)
        {
            _deathStarted = true;
            _oneShot = null;
            // Résolue même sans clip, pour que le remplacement figure au journal.
            var clip = Resolve(AnimationNames.Death);
            if (HasDeathClip && clip != null) _animation.Play(clip, BlendSeconds);
        }
        _animation.SpeedScale = HasDeathClip ? speed : 0;
    }

    /// <summary>Le modèle a sa propre mort : elle remplace la chute à la renverse.</summary>
    private bool HasDeathClip => _resolver?.Has(AnimationNames.Death) == true;

    /// <summary>Clip glTF de l'animation <paramref name="name"/> du catalogue, ou de son secours.</summary>
    private string? Resolve(string name) =>
        _resolver?.Resolve(name) is { } resolved && _clips.TryGetValue(resolved, out var clip) ? clip : null;

    private void Play(string clip)
    {
        if (_animation != null && _animation.HasAnimation(clip)) _animation.Play(clip, BlendSeconds);
    }

    /// <summary>En marche, le corps se tourne vers où il va ; arrivé, il refait face à l'adversaire.</summary>
    private void UpdateFacing(bool walking, float speed, float delta)
    {
        float wanted = _facingYaw;
        if (walking)
        {
            var (hx, hz) = _walk.Heading;
            wanted = Mathf.Atan2(hx, hz);
        }
        float weight = Mathf.Clamp(delta * speed * TurnRate, 0f, 1f);
        _body.Rotation = new Vector3(0, Mathf.LerpAngle(_body.Rotation.Y, wanted, weight), 0);
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
