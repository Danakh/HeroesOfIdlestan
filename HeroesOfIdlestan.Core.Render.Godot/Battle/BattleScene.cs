using Godot;
using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Render.Godot.Battle;

/// <summary>
/// La scène 3D du combat : décor, lumière, caméra, et une vue par combattant. Lit l'état à chaque
/// frame, ne le modifie jamais. Construite en code : le head n'a qu'à accrocher <see cref="Root"/>.
/// </summary>
public sealed class BattleScene : IDisposable
{
    /// <summary>
    /// Modèle des combattants, dans le projet Godot (HeroesOfIdlestan.App.Godot) : seul un chemin
    /// <c>res://</c> passe par l'import de Godot.
    /// </summary>
    public const string FighterModelPath = "res://assets/characters/bake3d-robot-animated.glb";

    private readonly CameraRig _camera = new();
    private readonly PackedScene? _model;
    private readonly Dictionary<Entity, FighterView> _views = new(ReferenceEqualityComparer.Instance);
    private readonly List<FighterView> _corpses = [];
    private readonly HashSet<Entity> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly List<Entity> _gone = [];
    /// <summary>Vrai jusqu'à la première frame d'une partie : l'équipe y joue son intro.</summary>
    private bool _freshState = true;

    public BattleScene(GameController game)
    {
        Root = new Node3D { Name = "Battle" };
        _model = ResourceLoader.Exists(FighterModelPath) ? GD.Load<PackedScene>(FighterModelPath) : null;
        if (_model == null)
            GameLog.Error(nameof(BattleScene), nameof(BattleScene), $"Modèle absent : {FighterModelPath}");

        Root.AddChild(BuildEnvironment());
        Root.AddChild(BuildLight());
        Root.AddChild(BuildGround());
        Root.AddChild(_camera.Camera);

        game.StateReplaced += Clear;
    }

    public Node3D Root { get; }

    public void Update(GameState state, float delta)
    {
        float speed = state.Clock.IsPaused ? 0 : state.Clock.ActiveSpeed;
        var camera = _camera.Camera.Basis;

        _seen.Clear();
        Sync(state, state.Heroes, isHero: true, speed, delta, camera);
        Sync(state, state.Monsters, isHero: false, speed, delta, camera);
        _freshState = false;

        // Un combattant qui quitte la liste (monstre tué) tombe là où il était avant de disparaître.
        _gone.Clear();
        foreach (var entity in _views.Keys)
            if (!_seen.Contains(entity)) _gone.Add(entity);
        foreach (var entity in _gone)
        {
            _corpses.Add(_views[entity]);
            _views.Remove(entity);
        }

        for (int i = _corpses.Count - 1; i >= 0; i--)
        {
            _corpses[i].UpdateCorpse(speed, delta);
            if (!_corpses[i].CorpseDone) continue;
            _corpses[i].Free();
            _corpses.RemoveAt(i);
        }
    }

    public bool HandleInput(InputEvent input) => _camera.HandleInput(input);

    private void Sync(GameState state, IReadOnlyList<Entity> side, bool isHero, float speed, float delta, Basis camera)
    {
        foreach (var fighter in side)
        {
            _seen.Add(fighter);
            if (!_views.TryGetValue(fighter, out var view))
            {
                view = new FighterView(_model, isHero, playIntro: isHero && _freshState);
                Root.AddChild(view.Root);
                _views[fighter] = view;
            }
            view.Update(state, fighter, side, speed, delta, camera);
        }
    }

    /// <summary>Nouvelle partie ou chargement : toutes les vues repartent de zéro, sans chute.</summary>
    private void Clear()
    {
        foreach (var view in _views.Values) view.Free();
        foreach (var view in _corpses) view.Free();
        _views.Clear();
        _corpses.Clear();
        _camera.Reset();
        _freshState = true;
    }

    public void Dispose()
    {
        if (GodotObject.IsInstanceValid(Root)) Root.QueueFree();
    }

    // ── Décor ────────────────────────────────────────────────────────────

    private static WorldEnvironment BuildEnvironment() => new()
    {
        Name = "Environment",
        // global:: : dans cet espace de noms, « Godot » désigne HeroesOfIdlestan.Core.Render.Godot.
        Environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0x161A20FF),
            AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0x8090A0FF),
            AmbientLightEnergy = 0.6f,
            TonemapMode = global::Godot.Environment.ToneMapper.Filmic,
        },
    };

    private static DirectionalLight3D BuildLight() => new()
    {
        Name = "Sun",
        RotationDegrees = new Vector3(-55, 35, 0),
        LightEnergy = 1.2f,
        ShadowEnabled = true,
    };

    private static Node3D BuildGround()
    {
        var ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = BattleLayout.GroundSize },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0x222A22FF), Roughness = 0.95f },
        };
        var midline = new MeshInstance3D
        {
            Name = "Midline",
            Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.01f, BattleLayout.GroundSize.Y - 1f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0x333D33FF) },
        };
        ground.AddChild(midline);
        return ground;
    }
}
