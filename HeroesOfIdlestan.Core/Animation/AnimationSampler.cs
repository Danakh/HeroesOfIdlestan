namespace HeroesOfIdlestan.Core.Animation;

/// <summary>Évalue une <see cref="AnimationData"/> à un instant donné et l'écrit dans une pose.</summary>
public static class AnimationSampler
{
    /// <summary>
    /// Écrit dans <paramref name="pose"/> la pose de l'animation à <paramref name="time"/>, mélangée à
    /// la pose déjà présente selon <paramref name="alpha"/> (1 = remplace). Tous les os sont écrits :
    /// un os sans piste revient vers sa pose de repos. N'appelle pas <see cref="SkeletonPose.UpdateWorld"/>.
    /// </summary>
    public static void Apply(AnimationData animation, SkeletonPose pose, float time, float alpha = 1)
    {
        var bones = pose.Skeleton.Bones;
        for (int i = 0; i < bones.Count; i++)
        {
            var target = BoneTransform.FromSetup(bones[i]);
            if (animation.Bones.TryGetValue(bones[i].Name, out var timeline))
            {
                if (timeline.Rotate is { } rotate)
                    target = target with { Rotation = target.Rotation + SampleRotate(rotate, time) };
                if (timeline.Translate is { } translate)
                {
                    var (x, y) = SampleVector(translate, time);
                    target = target with { X = target.X + x, Y = target.Y + y };
                }
                if (timeline.Scale is { } scale)
                {
                    var (x, y) = SampleVector(scale, time);
                    target = target with { ScaleX = target.ScaleX * x, ScaleY = target.ScaleY * y };
                }
            }
            pose.Local[i] = alpha >= 1 ? target : BoneTransform.Lerp(pose.Local[i], target, alpha);
        }
    }

    public static float SampleRotate(List<RotateKey> keys, float time)
    {
        var (a, b, f) = Segment(keys, time);
        return a.Angle + (b.Angle - a.Angle) * f;
    }

    public static (float X, float Y) SampleVector(List<VectorKey> keys, float time)
    {
        var (a, b, f) = Segment(keys, time);
        return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
    }

    /// <summary>Les deux clés qui encadrent <paramref name="time"/> et l'avancement de l'une à l'autre, courbe appliquée.</summary>
    private static (TKey From, TKey To, float Factor) Segment<TKey>(List<TKey> keys, float time) where TKey : IKeyframe
    {
        if (time <= keys[0].Time) return (keys[0], keys[0], 0);
        int last = keys.Count - 1;
        if (time >= keys[last].Time) return (keys[last], keys[last], 0);

        // Dernière clé dont le temps est <= time.
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (keys[mid].Time <= time) lo = mid; else hi = mid;
        }

        var from = keys[lo];
        var to = keys[lo + 1];
        float u = (time - from.Time) / (to.Time - from.Time);
        float f = from.Curve switch
        {
            Curve.Step => 0,
            Curve.Smooth => u * u * (3 - 2 * u),
            _ => u,
        };
        return (from, to, f);
    }
}
