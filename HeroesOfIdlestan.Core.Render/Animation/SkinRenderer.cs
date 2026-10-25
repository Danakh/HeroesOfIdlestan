using System.Globalization;
using System.Numerics;
using System.Xml;
using HeroesOfIdlestan.Core.Model.Animation;
using SkiaSharp;
using Svg.Skia;

namespace HeroesOfIdlestan.Core.Render.Animation;

/// <summary>
/// Dessine un habillage sur une pose. Chaque SVG est dessiné dans le repère du personnage en pose de
/// repos (voir <see cref="SkinData"/>) : pour le faire suivre son os, on le ramène dans le repère de
/// l'os au repos (inverse de la matrice monde de repos) puis on applique la matrice monde courante.
/// Ces inverses ne dépendent que du squelette et sont calculées une fois au chargement.
/// </summary>
public sealed class SkinRenderer : IDisposable
{
    /// <param name="ToBone">Repère de l'image enregistrée → repère de l'os.</param>
    private readonly record struct Part(int Bone, SKSvg Svg, SKPicture Picture, Matrix3x2 ToBone);

    private readonly List<Part> _parts = [];

    /// <param name="openSvg">Ouvre un SVG à partir de son chemin <see cref="AttachmentData.Svg"/> ; null s'il n'existe pas.</param>
    public SkinRenderer(SkeletonData skeleton, SkinData skin, Func<string, Stream?> openSvg)
    {
        AnimationCodec.ValidateSkin(skin, skeleton);
        Skin = skin;
        var setup = new SkeletonPose(skeleton);

        try
        {
            foreach (var a in skin.Attachments)
            {
                int bone = skeleton.IndexOf(a.Bone);
                if (!Matrix3x2.Invert(setup.World[bone], out var fromSetup))
                    throw new AnimationFormatException($"Os '{a.Bone}' d'échelle nulle au repos : '{a.Svg}' ne peut pas le suivre.");

                using var source = openSvg(a.Svg) ?? throw new FileNotFoundException($"SVG introuvable : {a.Svg}", a.Svg);
                using var stream = new MemoryStream();
                source.CopyTo(stream);

                stream.Position = 0;
                var svg = new SKSvg();
                if (svg.Load(stream) is not { } picture)
                {
                    svg.Dispose();
                    throw new AnimationFormatException($"SVG illisible : {a.Svg}");
                }
                stream.Position = 0;
                _parts.Add(new Part(bone, svg, picture, PictureToCharacter(stream, picture) * fromSetup));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public SkinData Skin { get; }

    /// <summary>
    /// Svg.Skia enregistre l'image dans un rectangle à la taille de width × height, pas forcément dans
    /// les coordonnées de la viewBox : on y ramène le dessin, car ce sont celles du personnage.
    /// </summary>
    private static Matrix3x2 PictureToCharacter(Stream svgStream, SKPicture picture)
    {
        using var reader = XmlReader.Create(svgStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        reader.MoveToContent();
        var viewBox = reader.GetAttribute("viewBox")?
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => float.Parse(v, CultureInfo.InvariantCulture))
            .ToArray();
        var cull = picture.CullRect;
        if (viewBox is not [var x, var y, var w, var h] || cull.Width <= 0 || cull.Height <= 0)
            return Matrix3x2.Identity;
        // Rectangle de l'image → rectangle de la viewBox, quelle que soit l'origine retenue par Svg.Skia.
        return Matrix3x2.CreateTranslation(-cull.Left, -cull.Top)
            * Matrix3x2.CreateScale(w / cull.Width, h / cull.Height)
            * Matrix3x2.CreateTranslation(x, y);
    }

    /// <summary>Dessine dans le repère courant du canevas, qui doit être celui du personnage (origine aux pieds).</summary>
    public void Draw(SKCanvas canvas, SkeletonPose pose)
    {
        foreach (var part in _parts)
        {
            var m = part.ToBone * pose.World[part.Bone];
            int restore = canvas.Save();
            canvas.Concat(ToSkia(m));
            // Svg.Skia enregistre chaque opacité comme un SaveLayer sans bornes : sans ce clip, chaque
            // morceau semi-transparent recomposerait un calque de la taille de l'écran.
            canvas.ClipRect(part.Picture.CullRect);
            canvas.DrawPicture(part.Picture);
            canvas.RestoreToCount(restore);
        }
    }

    /// <summary><see cref="Matrix3x2"/> (vecteur ligne) vers <see cref="SKMatrix"/> (vecteur colonne).</summary>
    public static SKMatrix ToSkia(Matrix3x2 m) => new(m.M11, m.M21, m.M31, m.M12, m.M22, m.M32, 0, 0, 1);

    public void Dispose()
    {
        foreach (var part in _parts) part.Svg.Dispose();
        _parts.Clear();
    }
}

/// <summary>Affichage de débogage des os : un segment par os, un point à chaque articulation.</summary>
public static class SkeletonDebugRenderer
{
    private static readonly SKColor BoneColor = new(0x4F, 0xC3, 0xF7);
    private static readonly SKColor JointColor = new(0xFF, 0xD5, 0x4F);

    /// <param name="pixel">Taille d'un pixel écran dans le repère courant, pour garder des traits fins quel que soit le zoom.</param>
    public static void Draw(SKCanvas canvas, SkeletonPose pose, float pixel)
    {
        using var bonePaint = new SKPaint { Color = BoneColor, StrokeWidth = 2 * pixel, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        using var jointPaint = new SKPaint { Color = JointColor, IsAntialias = true };

        var bones = pose.Skeleton.Bones;
        for (int i = 0; i < bones.Count; i++)
        {
            var origin = Vector2.Transform(Vector2.Zero, pose.World[i]);
            if (bones[i].Length > 0)
            {
                var tip = Vector2.Transform(new Vector2(bones[i].Length, 0), pose.World[i]);
                canvas.DrawLine(origin.X, origin.Y, tip.X, tip.Y, bonePaint);
            }
            canvas.DrawCircle(origin.X, origin.Y, 3 * pixel, jointPaint);
        }
    }
}
