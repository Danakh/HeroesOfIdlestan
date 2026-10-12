using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Render.Renderers;
using HeroesOfIdlestan.Render.Rendering;
using SkiaSharp;

namespace HeroesOfIdlestan.Render.Screens;

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

/// <summary>Écran du monde : caméra, input, pipeline de renderers. Garder-le mince : la logique va dans les services.</summary>
public sealed class GameScreen : IScreen
{
    private readonly GameController _game;
    private readonly Camera2D _camera = new();
    private readonly InputRouter _input;
    private readonly RenderPipeline _renderers = new RenderPipeline()
        .Add(new GridRenderer())
        .Add(new EntityRenderer());
    private bool _framed;

    public GameScreen(GameController game)
    {
        _game = game;
        _input = new InputRouter(_camera);
        _game.StateReplaced += () => _framed = false;
    }

    public Camera2D Camera => _camera;

    public void Render(SKCanvas canvas, SKSize size, float deltaTime, float totalTime)
    {
        _camera.SetViewport(size);
        if (!_framed && size.Width > 0)
        {
            _camera.Fit(SKRect.Create(-200, -200, 400, 400));
            _framed = true;
        }

        _renderers.Render(canvas, new FrameContext
        {
            State = _game.State,
            Camera = _camera,
            CanvasSize = size,
            DeltaTime = deltaTime,
            TotalTime = totalTime,
        });
    }

    public void PointerPressed(float x, float y, int id, PointerButton button) => _input.PointerPressed(x, y, id, button);
    public void PointerMoved(float x, float y, int id) => _input.PointerMoved(x, y, id);
    public void PointerReleased(float x, float y, int id, PointerButton button) => _input.PointerReleased(x, y, id, button);
    public void Wheel(float delta, float x, float y) => _input.Wheel(delta, x, y);
    public void Pinch(float scaleRatio, float x, float y, float panDx, float panDy) => _input.Pinch(scaleRatio, x, y, panDx, panDy);
    public void ResetInput() => _input.Reset();

    public void Dispose() => _renderers.Dispose();
}
