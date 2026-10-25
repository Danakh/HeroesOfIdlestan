using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.UI.Controls;

/// <summary>
/// Contrôle Avalonia qui expose un <see cref="SKCanvas"/> brut à la couche de rendu. Seul point de
/// contact entre Avalonia et Skia : le monde se dessine ici, l'overlay est fait de vrais contrôles
/// posés au-dessus, et l'arbitrage des clics revient à l'arbre visuel.
/// </summary>
public abstract class SkiaCanvasControl : Control
{
    /// <summary>Renseigné si le backend n'expose pas de lease SkiaSharp (diagnostic).</summary>
    public string? LeaseFailureReason { get; private set; }

    /// <summary>
    /// Dessine une frame. Le canevas est déjà dans le repère du contrôle, en unités logiques :
    /// (0,0) est son coin haut-gauche.
    /// </summary>
    protected abstract void OnRenderSkia(SKCanvas canvas, SKSize size);

    public sealed override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Un contrôle qui ne remplit rien n'est pas hit-testable : sans ce fond, aucun clic.
        context.FillRectangle(Brushes.Transparent, bounds);
        var op = new SkiaDrawOperation(bounds, this);
        context.Custom(op);
        LeaseFailureReason ??= op.FailureReason;
    }

    private sealed class SkiaDrawOperation(Rect bounds, SkiaCanvasControl owner) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;
        public string? FailureReason;

        public bool HitTest(Point p) => Bounds.Contains(p);

        // Le contenu change à chaque frame : l'opération n'est jamais réutilisable.
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
            {
                FailureReason = "ISkiaSharpApiLeaseFeature indisponible : le backend de rendu n'est pas Skia.";
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            // Canevas partagé avec Avalonia : isoler nos transformations et clips.
            int restore = canvas.Save();
            try
            {
                canvas.ClipRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height));
                owner.OnRenderSkia(canvas, new SKSize((float)Bounds.Width, (float)Bounds.Height));
            }
            finally
            {
                canvas.RestoreToCount(restore);
            }
        }

        public void Dispose() { }
    }
}
