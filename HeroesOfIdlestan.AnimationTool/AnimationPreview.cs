using HeroesOfIdlestan.Core.Animation;
using HeroesOfIdlestan.Render.Animation;
using SkiaSharp;

namespace HeroesOfIdlestan.AnimationTool;

/// <summary>État de lecture vu par le panneau, pris en une seule prise de verrou.</summary>
public readonly record struct PreviewSnapshot(float Time, float Duration, bool Paused, bool Finished);

/// <summary>
/// Ce qui est montré : un lecteur, un habillage et les options d'affichage. Avalonia dessine sur son
/// thread de rendu pendant que le thread UI avance la lecture : comme <c>GameRuntimeHost</c>, chaque
/// accès passe sous verrou.
/// </summary>
public sealed class AnimationPreview : IDisposable
{
    /// <summary>Hauteur du personnage, en unités du squelette, qui doit occuper <see cref="FillRatio"/> du canevas.</summary>
    private const float ReferenceHeight = 200;
    private const float FillRatio = 0.6f;
    private const float EventDisplaySeconds = 0.6f;

    private static readonly SKColor Background = new(0x1A, 0x1D, 0x23);
    private static readonly SKColor GridColor = new(0x26, 0x2A, 0x32);
    private static readonly SKColor GroundColor = new(0x4A, 0x50, 0x5C);
    private static readonly SKColor EventColor = new(0xFF, 0xD5, 0x4F);

    private readonly object _sync = new();
    private AnimationPlayer? _player;
    private SkinRenderer? _skin;
    private string? _lastEvent;
    private float _lastEventAge;

    public bool ShowSkin { get; set; } = true;
    public bool ShowBones { get; set; }
    public bool Mirror { get; set; }

    public void SetSkeleton(SkeletonData? skeleton)
    {
        lock (_sync)
        {
            _skin?.Dispose();
            _skin = null;
            _player = skeleton is null ? null : new AnimationPlayer(skeleton);
            if (_player is not null) _player.EventFired += OnEvent;
            _lastEvent = null;
        }
    }

    /// <summary>Remplace l'habillage ; l'ancien est libéré.</summary>
    public void SetSkin(SkinRenderer? skin)
    {
        lock (_sync)
        {
            _skin?.Dispose();
            _skin = skin;
        }
    }

    public void Play(AnimationData animation) => With(p => p.Play(animation));
    public void Seek(float time) => With(p => { p.Paused = true; p.Seek(time); });
    public void SetSpeed(float speed) => With(p => p.Speed = speed);
    public void SetLoop(bool loop) => With(p => p.Loop = loop);
    public void SetMix(bool enabled) => With(p => p.MixDuration = enabled ? 0.15f : 0);

    /// <summary>Lecture ↔ pause. Une animation terminée repart du début.</summary>
    public void TogglePause() => With(p =>
    {
        if (p.IsFinished) Replay(p);
        else p.Paused = !p.Paused;
    });

    public void Restart() => With(Replay);

    /// <summary>Rejoue depuis le début en gardant le choix de boucle fait dans le panneau.</summary>
    private static void Replay(AnimationPlayer p)
    {
        if (p.Animation is not { } a) return;
        bool loop = p.Loop;
        p.Play(a);
        p.Loop = loop;
    }

    public PreviewSnapshot Snapshot()
    {
        lock (_sync)
        {
            return _player?.Animation is { } a
                ? new PreviewSnapshot(_player.Time, a.Duration, _player.Paused, _player.IsFinished)
                : default;
        }
    }

    public void Tick(float deltaSeconds)
    {
        lock (_sync)
        {
            _player?.Update(deltaSeconds);
            _lastEventAge += deltaSeconds;
        }
    }

    public void Render(SKCanvas canvas, SKSize size)
    {
        lock (_sync)
        {
            canvas.Clear(Background);
            float groundY = size.Height * 0.8f;
            float zoom = MathF.Max(0.1f, size.Height * FillRatio / ReferenceHeight);
            DrawBackdrop(canvas, size, groundY, zoom);
            if (_player is null) return;

            int restore = canvas.Save();
            canvas.Translate(size.Width / 2, groundY);
            canvas.Scale(Mirror ? -zoom : zoom, zoom);
            if (ShowSkin) _skin?.Draw(canvas, _player.Pose);
            if (ShowBones || _skin is null) SkeletonDebugRenderer.Draw(canvas, _player.Pose, 1 / zoom);
            canvas.RestoreToCount(restore);

            DrawEvent(canvas, size);
        }
    }

    public void Dispose() => SetSkin(null);

    private void With(Action<AnimationPlayer> action)
    {
        lock (_sync)
        {
            if (_player is not null) action(_player);
        }
    }

    // Appelé par le lecteur pendant Update, donc déjà sous verrou.
    private void OnEvent(AnimationEvent e)
    {
        _lastEvent = e.Name;
        _lastEventAge = 0;
    }

    private static void DrawBackdrop(SKCanvas canvas, SKSize size, float groundY, float zoom)
    {
        // Grille de 20 unités du squelette, calée sur les pieds : elle donne l'échelle des mouvements.
        using var grid = new SKPaint { Color = GridColor, StrokeWidth = 1 };
        float step = 20 * zoom;
        for (float x = size.Width / 2 % step; x < size.Width; x += step) canvas.DrawLine(x, 0, x, size.Height, grid);
        for (float y = groundY % step; y < size.Height; y += step) canvas.DrawLine(0, y, size.Width, y, grid);

        using var ground = new SKPaint { Color = GroundColor, StrokeWidth = 2 };
        canvas.DrawLine(0, groundY, size.Width, groundY, ground);
    }

    private void DrawEvent(SKCanvas canvas, SKSize size)
    {
        if (_lastEvent is null || _lastEventAge > EventDisplaySeconds) return;
        byte alpha = (byte)(255 * (1 - _lastEventAge / EventDisplaySeconds));
        using var font = new SKFont { Size = 28, Embolden = true };
        using var paint = new SKPaint { Color = EventColor.WithAlpha(alpha), IsAntialias = true };
        canvas.DrawText($"« {_lastEvent} »", size.Width / 2, 48, SKTextAlign.Center, font, paint);
    }
}
