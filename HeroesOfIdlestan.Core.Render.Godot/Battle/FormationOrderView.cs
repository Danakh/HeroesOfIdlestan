using Godot;
using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Battle;

namespace HeroesOfIdlestan.Core.Render.Godot.Battle;

/// <summary>
/// La réorganisation de la formation dans la scène 3D, comme <c>FormationOrderRenderer</c> côté
/// Skia : glisser un personnage du bouton gauche (<see cref="FormationDrag"/>, en coordonnées écran)
/// au lieu de tourner la caméra, et, à plat sur le sol, une flèche par déplacement demandé, les places
/// proposées pendant un glisser (anneau sur un personnage à échanger, anneau pointillé au bout d'une
/// ligne), celle sous le pointeur en surbrillance, et la flèche qui suit le pointeur.
///
/// <para>Tout est redessiné à chaque frame dans un seul <see cref="ImmediateMesh"/>, aux couleurs
/// des sommets, par-dessus le reste (sans test de profondeur) pour rester visible sous les corps.</para>
/// </summary>
internal sealed class FormationOrderView
{
    /// <summary>Rayon de prise d'un personnage ou d'une place, en pixels autour de son buste.</summary>
    private const float PickPixels = 48f;
    /// <summary>Hauteur visée pour prendre un personnage : son buste plutôt que ses pieds.</summary>
    private const float PickHeight = 0.9f;
    private const float GroundHeight = 0.04f;
    private const float ShaftHalfWidth = 0.07f;
    private const float HeadLength = 0.4f;
    private const float HeadHalfWidth = 0.28f;
    /// <summary>La flèche part du bord du personnage et s'arrête au bord de la place visée.</summary>
    private const float EndGap = 0.5f;
    private const float SlotRadius = 0.6f;
    private const float RingWidth = 0.07f;
    private const int RingSegments = 32;

    private static readonly Color PendingColor = new(0.95f, 0.76f, 0.19f, 0.9f);
    private static readonly Color PreviewColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Color SlotColor = new(0.61f, 0.85f, 1f, 0.75f);
    private static readonly Color HoveredColor = new(0.61f, 0.85f, 1f, 0.3f);

    private readonly GameController _game;
    private readonly Camera3D _camera;
    private readonly FormationDrag _drag = new();
    private readonly ImmediateMesh _mesh = new();
    private readonly List<(Vector3 Vertex, Color Color)> _triangles = [];
    private Vector3? _pointerOnGround;

    public FormationOrderView(GameController game, Camera3D camera)
    {
        _game = game;
        _camera = camera;
        Root = new MeshInstance3D
        {
            Name = "FormationOrders",
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                VertexColorIsSrgb = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true,
            },
        };
    }

    public MeshInstance3D Root { get; }

    /// <summary>Nouvelle partie ou chargement : le glisser en cours s'arrête.</summary>
    public void Cancel() => _drag.Cancel();

    // ── Input ────────────────────────────────────────────────────────────

    /// <summary>Vrai si l'événement a servi au glisser (la caméra ne le reçoit alors pas).</summary>
    public bool HandleInput(InputEvent input)
    {
        var state = _game.State;
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when !_drag.Active:
                if (HeroAt(state, press.Position) is not { } hero
                    || !_drag.Begin(state, state.Heroes.IndexOf(hero), press.Position.X, press.Position.Y))
                    return false;
                Move(state, press.Position);
                return true;
            case InputEventMouseMotion motion when _drag.Active:
                Move(state, motion.Position);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release when _drag.Active:
                Move(state, release.Position);
                _drag.Drop(_game);
                return true;
            // Le doigt qui glisse un personnage ne tourne pas la caméra.
            case InputEventScreenDrag or InputEventScreenTouch when _drag.Active:
                return true;
        }
        return false;
    }

    private void Move(GameState state, Vector2 screen)
    {
        var hovered = FormationDrag.Nearest(_drag.Targets.Select(t =>
        {
            var p = Project(BattleLayout.TargetPosition(t));
            return (t, p.X, p.Y);
        }), screen.X, screen.Y, PickPixels);
        bool self = _drag.Hero is int hero && HeroAt(state, screen) == state.Heroes[hero];
        _drag.MoveTo(screen.X, screen.Y, hovered, self);
        _pointerOnGround = GroundUnder(screen);
    }

    /// <summary>Le personnage vivant sous le pointeur, ou null.</summary>
    private HeroEntity? HeroAt(GameState state, Vector2 screen)
    {
        var candidates = state.Heroes.Where(h => h.IsAlive)
            .Select(h => (Hero: h, Position: BattleLayout.PositionOf(h, state.Heroes, state.HeroHoles, isHero: true)))
            .Where(c => c.Position != null)
            .Select(c =>
            {
                var p = Project(c.Position!.Value);
                return (c.Hero, p.X, p.Y);
            });
        return FormationDrag.Nearest(candidates, screen.X, screen.Y, PickPixels);
    }

    /// <summary>Position à l'écran du buste d'une place ; très loin si elle est derrière la caméra.</summary>
    private Vector2 Project(Vector3 ground)
    {
        var point = ground + Vector3.Up * PickHeight;
        return _camera.IsPositionBehind(point) ? new Vector2(float.MaxValue / 4, float.MaxValue / 4) : _camera.UnprojectPosition(point);
    }

    /// <summary>Le point du sol sous le pointeur ; null si le rayon ne le rencontre pas.</summary>
    private Vector3? GroundUnder(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (direction.Y > -1e-4f) return null;
        return origin + direction * (-origin.Y / direction.Y);
    }

    // ── Dessin ───────────────────────────────────────────────────────────

    public void Update(GameState state)
    {
        _drag.Refresh(state);
        _triangles.Clear();

        foreach (var move in state.FormationMoves)
        {
            if (From(state, move.Hero) is not { } from || FormationOrders.TargetOf(state, move) is not { } target) continue;
            AddArrow(from, BattleLayout.TargetPosition(target), PendingColor, EndGap);
        }

        if (_drag.Hero is int hero && From(state, hero) is { } origin)
        {
            foreach (var target in _drag.Targets)
            {
                var p = BattleLayout.TargetPosition(target);
                if (target == _drag.Hovered) AddDisc(p, SlotRadius, HoveredColor);
                AddRing(p, SlotRadius, SlotColor, dashed: target.SwapWith == null);
            }
            if (!_drag.HoversSelf)
            {
                if (_drag.Hovered is { } hovered) AddArrow(origin, BattleLayout.TargetPosition(hovered), PreviewColor, EndGap);
                else if (_pointerOnGround is { } pointer) AddArrow(origin, pointer, PreviewColor, 0);
            }
        }

        _mesh.ClearSurfaces();
        if (_triangles.Count == 0) return;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var (vertex, color) in _triangles)
        {
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(vertex);
        }
        _mesh.SurfaceEnd();
    }

    private static Vector3? From(GameState state, int hero) =>
        hero >= 0 && hero < state.Heroes.Count
            ? BattleLayout.PositionOf(state.Heroes[hero], state.Heroes, state.HeroHoles, isHero: true)
            : null;

    /// <summary>Une flèche à plat sur le sol, raccourcie de <paramref name="endGap"/> à l'arrivée.</summary>
    private void AddArrow(Vector3 from, Vector3 to, Color color, float endGap)
    {
        var delta = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        float length = delta.Length();
        if (length < 2 * EndGap) return;
        var dir = delta / length;
        var normal = new Vector3(-dir.Z, 0, dir.X);
        var up = Vector3.Up * GroundHeight;
        var start = new Vector3(from.X, 0, from.Z) + dir * EndGap + up;
        var tip = new Vector3(to.X, 0, to.Z) - dir * endGap + up;
        var neck = tip - dir * HeadLength;

        Quad(start + normal * ShaftHalfWidth, start - normal * ShaftHalfWidth,
            neck - normal * ShaftHalfWidth, neck + normal * ShaftHalfWidth, color);
        Triangle(tip, neck + normal * HeadHalfWidth, neck - normal * HeadHalfWidth, color);
    }

    /// <summary>Un anneau à plat autour de <paramref name="center"/> ; pointillé, un segment sur deux.</summary>
    private void AddRing(Vector3 center, float radius, Color color, bool dashed)
    {
        var c = new Vector3(center.X, GroundHeight, center.Z);
        for (int i = 0; i < RingSegments; i++)
        {
            if (dashed && i % 2 == 1) continue;
            var (a, b) = (Around(i, radius), Around(i + 1, radius));
            var (ai, bi) = (Around(i, radius - RingWidth), Around(i + 1, radius - RingWidth));
            Quad(c + a, c + b, c + bi, c + ai, color);
        }
    }

    private void AddDisc(Vector3 center, float radius, Color color)
    {
        var c = new Vector3(center.X, GroundHeight, center.Z);
        for (int i = 0; i < RingSegments; i++)
            Triangle(c, c + Around(i, radius), c + Around(i + 1, radius), color);
    }

    private static Vector3 Around(int segment, float radius)
    {
        float angle = Mathf.Tau * segment / RingSegments;
        return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }

    private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        _triangles.Add((a, color));
        _triangles.Add((b, color));
        _triangles.Add((c, color));
    }
}
