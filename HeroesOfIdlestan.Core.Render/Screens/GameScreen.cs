using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Combat;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Presentation.Battle;
using HeroesOfIdlestan.Core.Render.Renderers;
using HeroesOfIdlestan.Core.Render.Rendering;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Screens;

/// <summary>
/// Un écran du runtime : ce qu'il dessine sur le canevas et comment il réagit aux entrées.
/// L'écran-titre et les menus vivent dans l'overlay Avalonia ; un écran Skia n'existe que pour ce
/// qui se dessine (le monde, une carte de progression…).
/// </summary>
public interface IScreen : IDisposable
{
    void Render(SKCanvas canvas, SKSize size, float deltaTime, float totalTime);
    void PointerPressed(float x, float y, int id, PointerButton button);
    void PointerMoved(float x, float y, int id);
    void PointerReleased(float x, float y, int id, PointerButton button);
    void Wheel(float delta, float x, float y);
    void Pinch(float scaleRatio, float x, float y, float panDx, float panDy);

    /// <summary>Oublie les pointeurs enfoncés dont l'hôte ne transmettra pas le relâchement.</summary>
    void ResetInput();
}

/// <summary>
/// Écran du combat : caméra, input, pipeline de renderers. Garder-le mince : la logique va dans les services.
///
/// <para>Un appui du bouton gauche sur un personnage le glisse dans la formation
/// (<see cref="FormationDrag"/>) au lieu de déplacer la vue ; ailleurs, l'input va à la caméra.</para>
/// </summary>
public sealed class GameScreen : IScreen
{
    /// <summary>Rayon de prise d'un personnage ou d'une place, en unités monde.</summary>
    private const float PickRadius = BattleLayout.FighterRadius * 1.4f;

    private readonly GameController _game;
    private readonly Camera2D _camera = new();
    private readonly InputRouter _input;
    private readonly FighterRenderer _fighters = new();
    private readonly FormationDrag _drag = new();
    private readonly RenderPipeline _renderers;
    private int _dragPointer;
    private bool _framed;

    public GameScreen(GameController game)
    {
        _game = game;
        _input = new InputRouter(_camera);
        _renderers = new RenderPipeline().Add(new GroundRenderer()).Add(_fighters).Add(new FormationOrderRenderer(_drag));
        _game.StateReplaced += () =>
        {
            _framed = false;
            _fighters.Reset();
            _drag.Cancel();
        };
    }

    public Camera2D Camera => _camera;

    public void Render(SKCanvas canvas, SKSize size, float deltaTime, float totalTime)
    {
        _camera.SetViewport(size);
        if (!_framed && size.Width > 0)
        {
            _camera.Fit(BattleLayout.BoundsFor(_game.State));
            _framed = true;
        }

        _drag.Refresh(_game.State);
        _renderers.Render(canvas, new FrameContext
        {
            State = _game.State,
            Camera = _camera,
            CanvasSize = size,
            DeltaTime = deltaTime,
            TotalTime = totalTime,
        });
    }

    public void PointerPressed(float x, float y, int id, PointerButton button)
    {
        var world = _camera.ScreenToWorld(new SKPoint(x, y));
        if (button == PointerButton.Left && !_drag.Active && HeroAt(world) is { } hero
            && _drag.Begin(_game.State, _game.State.Heroes.IndexOf(hero), world.X, world.Y))
        {
            _dragPointer = id;
            _input.Reset();
            return;
        }
        _input.PointerPressed(x, y, id, button);
    }

    public void PointerMoved(float x, float y, int id)
    {
        if (_drag.Active && id == _dragPointer)
        {
            var world = _camera.ScreenToWorld(new SKPoint(x, y));
            var self = _drag.Hero is int hero ? HeroAt(world) == _game.State.Heroes[hero] : false;
            _drag.MoveTo(world.X, world.Y, TargetAt(world), self);
            return;
        }
        _input.PointerMoved(x, y, id);
    }

    public void PointerReleased(float x, float y, int id, PointerButton button)
    {
        if (_drag.Active && id == _dragPointer)
        {
            PointerMoved(x, y, id);
            _drag.Drop(_game);
            return;
        }
        _input.PointerReleased(x, y, id, button);
    }

    public void Wheel(float delta, float x, float y) => _input.Wheel(delta, x, y);

    public void Pinch(float scaleRatio, float x, float y, float panDx, float panDy)
    {
        _drag.Cancel();
        _input.Pinch(scaleRatio, x, y, panDx, panDy);
    }

    public void ResetInput()
    {
        _drag.Cancel();
        _input.Reset();
    }

    /// <summary>Le personnage vivant sous le point monde <paramref name="world"/>, ou null.</summary>
    private HeroEntity? HeroAt(SKPoint world)
    {
        var state = _game.State;
        var candidates = state.Heroes.Where(h => h.IsAlive)
            .Select(h => (Hero: h, Position: BattleLayout.PositionOf(h, state.Heroes, state.HeroHoles, isHero: true)))
            .Where(c => c.Position != null)
            .Select(c => (c.Hero, c.Position!.Value.X, c.Position!.Value.Y));
        return FormationDrag.Nearest(candidates, world.X, world.Y, PickRadius);
    }

    /// <summary>La place proposée sous le point monde <paramref name="world"/>, ou null.</summary>
    private FormationTarget? TargetAt(SKPoint world) =>
        FormationDrag.Nearest(_drag.Targets.Select(t =>
        {
            var p = BattleLayout.TargetPosition(t);
            return (t, p.X, p.Y);
        }), world.X, world.Y, PickRadius);

    public void Dispose() => _renderers.Dispose();
}
