using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using SkiaSharp;
using RenderButton = HeroesOfIdlestan.Render.Rendering.PointerButton;

namespace HeroesOfIdlestan.UI.Controls;

/// <summary>
/// Hôte Avalonia du monde : boucle de frame (Tick puis invalidation) et relais de l'input vers le
/// runtime. Tout passe par <see cref="GameRuntimeHost"/>.
/// </summary>
public sealed class GameRuntimeControl : SkiaCanvasControl
{
    /// <summary>Cadence de la boucle : Avalonia n'expose pas de RequestAnimationFrame, on pilote un timer à priorité Render.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly GameRuntimeHost _host;
    private IDisposable? _loop;

    public GameRuntimeControl(GameRuntimeHost host)
    {
        _host = host;
        Focusable = true;
        GestureRecognizers.Add(new PinchGestureRecognizer());
        Pinch += OnPinch;
        PinchEnded += OnPinchEnded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loop = DispatcherTimer.Run(OnFrame, FrameInterval, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop?.Dispose();
        _loop = null;
    }

    private bool OnFrame()
    {
        // Le tick tourne même quand rien n'est dessiné (fenêtre masquée) : le temps de jeu ne dérive pas.
        _host.Tick();
        InvalidateVisual();
        return true;
    }

    protected override void OnRenderSkia(SKCanvas canvas, SKSize size) => _host.Render(canvas, size);

    // ── Input : les coordonnées Avalonia sont déjà celles du canevas ─────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        _host.PointerPressed((float)p.X, (float)p.Y, e.Pointer.Id, ToButton(e.GetCurrentPoint(this).Properties));
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _host.PointerMoved((float)p.X, (float)p.Y, e.Pointer.Id);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);
        var button = e.InitialPressMouseButton switch
        {
            MouseButton.Right => RenderButton.Right,
            MouseButton.Middle => RenderButton.Middle,
            _ => RenderButton.Left,
        };
        _host.PointerReleased((float)p.X, (float)p.Y, e.Pointer.Id, button);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this);
        _host.Wheel((float)e.Delta.Y, (float)p.X, (float)p.Y);
        e.Handled = true;
    }

    // Pincement : Avalonia fournit une échelle cumulée depuis le début du geste ; le runtime attend un
    // rapport relatif à l'événement précédent et le déplacement du centre comme pan.
    private Point? _lastPinchOrigin;
    private double _lastPinchScale = 1;

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        var origin = e.ScaleOrigin;
        if (_lastPinchOrigin is { } last && _lastPinchScale > 0)
        {
            _host.Pinch((float)(e.Scale / _lastPinchScale), (float)origin.X, (float)origin.Y,
                (float)(origin.X - last.X), (float)(origin.Y - last.Y));
        }
        else
        {
            // Premier événement du geste : le recognizer vient de capturer les pointeurs, leur
            // relâchement ne nous parviendra jamais. Sans ceci la carte resterait accrochée au doigt.
            _host.Invoke(r => r.Screen.ResetInput());
        }
        _lastPinchScale = e.Scale;
        _lastPinchOrigin = origin;
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _lastPinchOrigin = null;
        _lastPinchScale = 1;
        e.Handled = true;
    }

    private static RenderButton ToButton(PointerPointProperties props) =>
        props.IsRightButtonPressed ? RenderButton.Right
        : props.IsMiddleButtonPressed ? RenderButton.Middle
        : RenderButton.Left;
}
