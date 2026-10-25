using HeroesOfIdlestan.Core.Model.State;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Rendering;

/// <summary>Tout ce qu'un renderer peut lire pendant une frame.</summary>
public sealed class FrameContext
{
    public required GameState State { get; init; }
    public required Camera2D Camera { get; init; }
    public required SKSize CanvasSize { get; init; }

    /// <summary>Secondes réelles depuis la frame précédente.</summary>
    public float DeltaTime { get; init; }

    /// <summary>Secondes réelles depuis le démarrage — pour les animations.</summary>
    public float TotalTime { get; init; }
}

/// <summary>Un aspect du rendu (fond, entités, effets…). Ne modifie jamais l'état.</summary>
public interface IGameRenderer : IDisposable
{
    void Render(SKCanvas canvas, FrameContext context);
}

/// <summary>
/// Compose les renderers : l'ordre d'enregistrement est l'ordre de dessin, du fond vers l'avant.
/// </summary>
public sealed class RenderPipeline : IDisposable
{
    private readonly List<IGameRenderer> _renderers = [];

    public RenderPipeline Add(IGameRenderer renderer)
    {
        _renderers.Add(renderer);
        return this;
    }

    public void Render(SKCanvas canvas, FrameContext context)
    {
        for (int i = 0; i < _renderers.Count; i++)
            _renderers[i].Render(canvas, context);
    }

    public void Dispose()
    {
        foreach (var renderer in _renderers) renderer.Dispose();
        _renderers.Clear();
    }
}
