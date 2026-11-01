using HeroesOfIdlestan.Core.Model.Animation;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Animation;

/// <summary>
/// Un jeu de planches de sprites chargé : le manifeste (<see cref="SpriteSheetData"/>) et l'image de
/// chaque planche. Les planches sont produites par Tool.SpriteSheet dans <c>assets/sprites/</c> et
/// embarquées dans cet assembly (voir le csproj) : le bureau, le web et Android les ont sans accès
/// au disque. Voir design/09-sprite-sheets.md.
/// </summary>
public sealed class SpriteSheet : IDisposable
{
    /// <summary>Préfixe des ressources embarquées : <c>sprites/&lt;id&gt;/&lt;fichier&gt;</c>.</summary>
    private const string ResourcePrefix = "sprites/";

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private readonly Dictionary<SpriteClipData, SKImage> _images = new(ReferenceEqualityComparer.Instance);

    public SpriteSheet(string id, SpriteSheetData data, Func<string, Stream?> openImage)
    {
        Id = id;
        Data = data;
        try
        {
            foreach (var clip in data.Animations)
            {
                using var stream = openImage(clip.Image) ?? throw new FileNotFoundException($"Planche introuvable : {clip.Image}", clip.Image);
                var image = SKImage.FromEncodedData(stream) ?? throw new AnimationFormatException($"Planche illisible : {clip.Image}");
                _images[clip] = image;
                if (image.Width < clip.Columns * data.FrameWidth || image.Height < clip.Rows * data.FrameHeight)
                    throw new AnimationFormatException($"Planche {clip.Image} : {image.Width}×{image.Height} px, trop petite pour sa grille.");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Le nom du jeu de planches (dossier sous <c>assets/sprites/</c>), pour le journal.</summary>
    public string Id { get; }

    public SpriteSheetData Data { get; }

    /// <summary>
    /// Charge le jeu embarqué <paramref name="id"/> (<c>assets/sprites/&lt;id&gt;/&lt;id&gt;.sprites.json</c>).
    /// Absent ou illisible : consigné dans <see cref="GameLog"/>, et null.
    /// </summary>
    public static SpriteSheet? LoadEmbedded(string id)
    {
        var assembly = typeof(SpriteSheet).Assembly;
        // Les noms viennent de LogicalName (voir le csproj), avec le séparateur de la machine de build.
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(n => n.Replace('\\', '/'), StringComparer.Ordinal);
        Stream? Open(string file) =>
            resources.TryGetValue($"{ResourcePrefix}{id}/{file.Replace('\\', '/')}", out var name)
                ? assembly.GetManifestResourceStream(name)
                : null;

        try
        {
            using var manifest = Open($"{id}.sprites.json")
                                 ?? throw new FileNotFoundException($"Planches « {id} » absentes des ressources.");
            using var reader = new StreamReader(manifest);
            var data = AnimationCodec.DecodeSpriteSheet(reader.ReadToEnd());
            return new SpriteSheet(id, data, Open);
        }
        catch (Exception ex)
        {
            GameLog.Error(nameof(SpriteSheet), id, ex);
            return null;
        }
    }

    /// <summary>
    /// Dessine l'image <paramref name="frame"/> de <paramref name="clip"/>, pivot (les pieds) en
    /// (0, 0) du canevas courant, <paramref name="scale"/> unités du canevas par pixel de planche.
    /// Le personnage regarde vers +X ; le miroir se fait par le canevas.
    /// </summary>
    public void Draw(SKCanvas canvas, SpriteClipData clip, int frame, float scale, SKPaint? paint = null)
    {
        if (!_images.TryGetValue(clip, out var image)) return;
        var (x, y) = clip.FrameOrigin(Math.Clamp(frame, 0, clip.Frames - 1), Data);
        var source = SKRect.Create(x, y, Data.FrameWidth, Data.FrameHeight);
        var target = SKRect.Create(
            -Data.PivotX * scale, -Data.PivotY * scale,
            Data.FrameWidth * scale, Data.FrameHeight * scale);
        canvas.DrawImage(image, source, target, Sampling, paint);
    }

    public void Dispose()
    {
        foreach (var image in _images.Values) image.Dispose();
        _images.Clear();
    }
}
