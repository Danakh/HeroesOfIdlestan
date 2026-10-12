using System.Numerics;

namespace HeroesOfIdlestan.Core.Animation;

/// <summary>Transformation locale d'un os, relative à son parent.</summary>
public readonly record struct BoneTransform(float X, float Y, float Rotation, float ScaleX, float ScaleY)
{
    public static BoneTransform FromSetup(BoneData b) => new(b.X, b.Y, b.Rotation, b.ScaleX, b.ScaleY);

    public static BoneTransform Lerp(BoneTransform a, BoneTransform b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Rotation + (b.Rotation - a.Rotation) * t,
        a.ScaleX + (b.ScaleX - a.ScaleX) * t,
        a.ScaleY + (b.ScaleY - a.ScaleY) * t);

    /// <summary>Échelle, puis rotation, puis translation (convention ligne de <see cref="Matrix3x2"/>).</summary>
    public Matrix3x2 ToMatrix() =>
        Matrix3x2.CreateScale(ScaleX, ScaleY)
        * Matrix3x2.CreateRotation(Rotation * MathF.PI / 180f)
        * Matrix3x2.CreateTranslation(X, Y);
}

/// <summary>
/// État courant d'un squelette : la transformation locale de chaque os (écrite par les animations)
/// et sa matrice monde (calculée par <see cref="UpdateWorld"/>). Pas de lien avec le rendu : un
/// renderer lit <see cref="World"/>, la simulation pourrait y lire la position d'une lame.
/// </summary>
public sealed class SkeletonPose
{
    private readonly int[] _parents;

    public SkeletonPose(SkeletonData skeleton)
    {
        Skeleton = skeleton;
        _parents = skeleton.ParentIndices();
        Local = new BoneTransform[skeleton.Bones.Count];
        World = new Matrix3x2[skeleton.Bones.Count];
        SetToSetup();
        UpdateWorld();
    }

    public SkeletonData Skeleton { get; }

    /// <summary>Transformations locales, dans l'ordre de <see cref="SkeletonData.Bones"/>.</summary>
    public BoneTransform[] Local { get; }

    /// <summary>Repère os → repère du personnage (origine aux pieds).</summary>
    public Matrix3x2[] World { get; }

    public void SetToSetup()
    {
        for (int i = 0; i < Local.Length; i++)
            Local[i] = BoneTransform.FromSetup(Skeleton.Bones[i]);
    }

    /// <summary>Recalcule <see cref="World"/>. Les parents précèdent leurs enfants : un seul passage suffit.</summary>
    public void UpdateWorld()
    {
        for (int i = 0; i < Local.Length; i++)
        {
            var local = Local[i].ToMatrix();
            World[i] = _parents[i] < 0 ? local : local * World[_parents[i]];
        }
    }
}
