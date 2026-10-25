using System.Diagnostics;
using Avalonia;
using Avalonia.Threading;
using HeroesOfIdlestan.Core.UI.Controls;
using SkiaSharp;

namespace HeroesOfIdlestan.Tool.Animation;

/// <summary>Canevas de l'aperçu : boucle de frame (avance puis invalidation), comme <c>GameRuntimeControl</c>.</summary>
public sealed class AnimationCanvas(AnimationPreview preview) : SkiaCanvasControl
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly Stopwatch _clock = new();
    private IDisposable? _loop;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock.Restart();
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
        // Temps réellement écoulé : le timer ne garantit pas ses 16 ms. Plafonné pour qu'un gel
        // (débogueur, déplacement de fenêtre) ne fasse pas sauter la moitié d'une animation.
        float dt = MathF.Min((float)_clock.Elapsed.TotalSeconds, 0.1f);
        _clock.Restart();
        preview.Tick(dt);
        InvalidateVisual();
        return true;
    }

    protected override void OnRenderSkia(SKCanvas canvas, SKSize size) => preview.Render(canvas, size);
}
