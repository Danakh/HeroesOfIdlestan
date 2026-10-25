using HeroesOfIdlestan.Core.Model.Animation;

namespace HeroesOfIdlestan.Core.Controller.Animation;

/// <summary>
/// Joue une animation sur un squelette : avance le temps, boucle ou s'arrête à la fin, émet les
/// événements franchis et fond la pose précédente dans la nouvelle lors d'un changement
/// (<see cref="MixDuration"/>), pour qu'un passage de la marche à l'attaque ne saute pas.
/// </summary>
public sealed class AnimationPlayer
{
    private AnimationData? _previous;
    private float _previousTime;
    private float _mixElapsed;
    private bool _startPending;

    public AnimationPlayer(SkeletonData skeleton) => Pose = new SkeletonPose(skeleton);

    public SkeletonPose Pose { get; }
    public AnimationData? Animation { get; private set; }

    /// <summary>Temps de lecture, en secondes, dans [0, durée].</summary>
    public float Time { get; private set; }

    /// <summary>Repris de <see cref="AnimationData.Loop"/> à chaque <see cref="Play"/>, modifiable ensuite.</summary>
    public bool Loop { get; set; }

    public float Speed { get; set; } = 1;
    public bool Paused { get; set; }

    /// <summary>Durée du fondu entre deux animations, en secondes ; 0 pour un changement sec.</summary>
    public float MixDuration { get; set; } = 0.15f;

    public bool IsFinished => Animation is { } a && !Loop && Time >= a.Duration;

    /// <summary>Émis pendant <see cref="Update"/> pour chaque événement franchi, dans l'ordre.</summary>
    public event Action<AnimationEvent>? EventFired;

    public void Play(AnimationData animation)
    {
        if (animation.Skeleton != Pose.Skeleton.Id)
            throw new ArgumentException($"L'animation '{animation.Id}' vise le squelette '{animation.Skeleton}'.");

        if (Animation is not null && MixDuration > 0)
        {
            _previous = Animation;
            _previousTime = Time;
            _mixElapsed = 0;
        }
        Animation = animation;
        Loop = animation.Loop;
        Time = 0;
        Paused = false;
        _startPending = true;
        ApplyPose();
    }

    /// <summary>Avance de <paramref name="deltaSeconds"/> (temps réel, avant <see cref="Speed"/>) et recalcule la pose.</summary>
    public void Update(float deltaSeconds)
    {
        if (Animation is null) return;
        if (!Paused) Advance(deltaSeconds * Speed);

        if (_previous is not null)
        {
            _mixElapsed += deltaSeconds;
            if (_mixElapsed >= MixDuration) _previous = null;
        }
        ApplyPose();
    }

    /// <summary>Place la lecture à <paramref name="time"/> sans émettre d'événement ni de fondu (scrubbing).</summary>
    public void Seek(float time)
    {
        if (Animation is null) return;
        Time = Math.Clamp(time, 0, Animation.Duration);
        _previous = null;
        _startPending = false;
        ApplyPose();
    }

    private void Advance(float delta)
    {
        var anim = Animation!;
        // Les événements à t = 0 partent au premier pas après Play, pas jamais.
        float from = _startPending ? -1 : Time;
        _startPending = false;
        float to = Time + delta;

        if (to < anim.Duration)
        {
            FireEvents(from, to);
            Time = to;
        }
        else if (Loop)
        {
            FireEvents(from, anim.Duration);
            Time = to % anim.Duration;
            FireEvents(-1, Time);
        }
        else
        {
            FireEvents(from, anim.Duration);
            Time = anim.Duration;
        }
    }

    /// <summary>Événements dans ]<paramref name="fromExclusive"/>, <paramref name="toInclusive"/>].</summary>
    private void FireEvents(float fromExclusive, float toInclusive)
    {
        if (EventFired is null) return;
        foreach (var e in Animation!.Events)
            if (e.Time > fromExclusive && e.Time <= toInclusive)
                EventFired(e);
    }

    private void ApplyPose()
    {
        if (Animation is null) return;
        if (_previous is not null)
        {
            AnimationSampler.Apply(_previous, Pose, _previousTime);
            AnimationSampler.Apply(Animation, Pose, Time, _mixElapsed / MixDuration);
        }
        else
        {
            AnimationSampler.Apply(Animation, Pose, Time);
        }
        Pose.UpdateWorld();
    }
}
