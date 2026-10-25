using HeroesOfIdlestan.Core.Model.Animation;

namespace HeroesOfIdlestan.Tool.Animation;

/// <summary>Un squelette et tout ce qui le vise : ses habillages et ses animations.</summary>
public sealed record SkeletonEntry(SkeletonData Skeleton, List<SkinEntry> Skins, List<AnimationData> Animations);

/// <param name="Directory">Dossier du fichier d'habillage, base des chemins SVG.</param>
public sealed record SkinEntry(SkinData Skin, string Directory);

/// <summary>
/// Le contenu d'un dossier d'animation, reconnu par extension : <c>*.skeleton.json</c>,
/// <c>*.skin.json</c>, <c>*.anim.json</c>, à n'importe quelle profondeur. Habillages et animations
/// se rattachent à leur squelette par son id, pas par leur emplacement. Un fichier invalide est
/// écarté et consigné dans <see cref="Errors"/> sans empêcher le chargement des autres.
/// </summary>
public sealed class AnimationLibrary
{
    public const string SkeletonPattern = "*.skeleton.json";
    public const string SkinPattern = "*.skin.json";
    public const string AnimationPattern = "*.anim.json";

    private AnimationLibrary(string root) => Root = root;

    public string Root { get; }
    public List<SkeletonEntry> Skeletons { get; } = [];
    public List<string> Errors { get; } = [];

    public static AnimationLibrary Load(string root)
    {
        var library = new AnimationLibrary(root);
        if (!Directory.Exists(root))
        {
            library.Errors.Add($"Dossier introuvable : {root}");
            return library;
        }

        var byId = new Dictionary<string, SkeletonEntry>();
        foreach (var file in Find(root, SkeletonPattern))
        {
            library.TryLoad(file, AnimationCodec.DecodeSkeleton, skeleton =>
            {
                if (!byId.TryAdd(skeleton.Id, new SkeletonEntry(skeleton, [], [])))
                    throw new AnimationFormatException($"Id de squelette '{skeleton.Id}' déjà utilisé.");
            });
        }

        foreach (var file in Find(root, SkinPattern))
        {
            library.TryLoad(file, AnimationCodec.DecodeSkin, skin =>
            {
                var entry = Owner(byId, skin.Skeleton);
                AnimationCodec.ValidateSkin(skin, entry.Skeleton);
                entry.Skins.Add(new SkinEntry(skin, Path.GetDirectoryName(file)!));
            });
        }

        foreach (var file in Find(root, AnimationPattern))
        {
            library.TryLoad(file, AnimationCodec.DecodeAnimation, animation =>
            {
                var entry = Owner(byId, animation.Skeleton);
                AnimationCodec.ValidateAnimation(animation, entry.Skeleton);
                entry.Animations.Add(animation);
            });
        }

        foreach (var entry in byId.Values.OrderBy(e => e.Skeleton.Id, StringComparer.Ordinal))
        {
            entry.Skins.Sort((a, b) => string.CompareOrdinal(a.Skin.Id, b.Skin.Id));
            entry.Animations.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            library.Skeletons.Add(entry);
        }
        return library;
    }

    private static IEnumerable<string> Find(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).Order(StringComparer.Ordinal);

    private static SkeletonEntry Owner(Dictionary<string, SkeletonEntry> byId, string skeletonId) =>
        byId.TryGetValue(skeletonId, out var entry)
            ? entry
            : throw new AnimationFormatException($"Squelette '{skeletonId}' introuvable.");

    private void TryLoad<T>(string file, Func<string, T> decode, Action<T> register)
    {
        try
        {
            register(decode(File.ReadAllText(file)));
        }
        catch (Exception ex) when (ex is AnimationFormatException or IOException or UnauthorizedAccessException)
        {
            Errors.Add($"{Path.GetRelativePath(Root, file)} : {ex.Message}");
        }
    }
}
