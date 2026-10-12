using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Animation;

/// <summary>
/// Lecture et écriture des trois fichiers d'animation : squelette, habillage, animation. JSON lisible
/// et éditable à la main, en tête duquel <c>format</c> dit ce que contient le fichier et
/// <c>version</c> la révision du format :
///
/// <code>
/// { "format": "hoi-animation", "version": 1, "id": "walk", "skeleton": "humanoid", ... }
/// </code>
///
/// <para>Le décodage valide tout ce qui peut l'être sans le squelette (clés triées, durée positive,
/// parents déclarés…) et rassemble toutes les erreurs du fichier dans une seule
/// <see cref="AnimationFormatException"/>. Les références à un squelette se valident ensuite avec
/// <see cref="ValidateSkin"/> et <see cref="ValidateAnimation"/>. Une propriété inconnue est une
/// erreur : une faute de frappe ne doit pas passer pour une valeur par défaut.</para>
///
/// <para>Faire évoluer le format : incrémenter <see cref="CurrentVersion"/> et migrer l'ancien JSON
/// dans <see cref="Decode{T}"/> avant la désérialisation, sans jamais retirer la lecture d'une
/// version déjà écrite.</para>
/// </summary>
public static class AnimationCodec
{
    public const int CurrentVersion = 1;

    public const string SkeletonFormat = "hoi-skeleton";
    public const string SkinFormat = "hoi-skin";
    public const string AnimationFormat = "hoi-animation";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null, // les clés de dictionnaire sont des noms d'os : intouchées
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    // ── Squelette ─────────────────────────────────────────────────────────

    public static string EncodeSkeleton(SkeletonData skeleton) => Encode(SkeletonFormat, skeleton);

    public static SkeletonData DecodeSkeleton(string json)
    {
        var s = Decode<SkeletonData>(json, SkeletonFormat);
        var errors = new List<string>();
        RequireId(s.Id, errors);
        if (s.Bones.Count == 0) errors.Add("Le squelette n'a aucun os.");

        var seen = new HashSet<string>();
        foreach (var bone in s.Bones)
        {
            if (string.IsNullOrWhiteSpace(bone.Name)) { errors.Add("Os sans nom."); continue; }
            if (bone.Parent is { } parent && !seen.Contains(parent))
                errors.Add($"Os '{bone.Name}' : parent '{parent}' inconnu ou déclaré après lui.");
            if (!seen.Add(bone.Name)) errors.Add($"Os '{bone.Name}' déclaré deux fois.");
            if (!float.IsFinite(bone.X + bone.Y + bone.Rotation + bone.ScaleX + bone.ScaleY))
                errors.Add($"Os '{bone.Name}' : valeur non finie.");
        }
        ThrowIfAny(errors);
        return s;
    }

    // ── Habillage ─────────────────────────────────────────────────────────

    public static string EncodeSkin(SkinData skin) => Encode(SkinFormat, skin);

    public static SkinData DecodeSkin(string json)
    {
        var skin = Decode<SkinData>(json, SkinFormat);
        var errors = new List<string>();
        RequireId(skin.Id, errors);
        if (string.IsNullOrWhiteSpace(skin.Skeleton)) errors.Add("Squelette visé manquant.");
        foreach (var a in skin.Attachments)
            if (string.IsNullOrWhiteSpace(a.Svg)) errors.Add($"Morceau de l'os '{a.Bone}' sans SVG.");
        ThrowIfAny(errors);
        return skin;
    }

    /// <summary>Vérifie que l'habillage vise <paramref name="skeleton"/> et n'accroche que des os existants.</summary>
    public static void ValidateSkin(SkinData skin, SkeletonData skeleton)
    {
        var errors = new List<string>();
        CheckSkeletonId(skin.Skeleton, skeleton, errors);
        foreach (var a in skin.Attachments)
            if (skeleton.IndexOf(a.Bone) < 0) errors.Add($"Morceau '{a.Svg}' : os '{a.Bone}' absent du squelette.");
        ThrowIfAny(errors);
    }

    // ── Animation ─────────────────────────────────────────────────────────

    public static string EncodeAnimation(AnimationData animation) => Encode(AnimationFormat, animation);

    public static AnimationData DecodeAnimation(string json)
    {
        var a = Decode<AnimationData>(json, AnimationFormat);
        var errors = new List<string>();
        RequireId(a.Id, errors);
        if (string.IsNullOrWhiteSpace(a.Skeleton)) errors.Add("Squelette visé manquant.");
        if (!(a.Duration > 0) || !float.IsFinite(a.Duration)) errors.Add($"Durée invalide : {a.Duration}.");

        foreach (var (bone, timeline) in a.Bones)
        {
            CheckKeys(timeline.Rotate, $"{bone}.rotate", a.Duration, errors);
            CheckKeys(timeline.Translate, $"{bone}.translate", a.Duration, errors);
            CheckKeys(timeline.Scale, $"{bone}.scale", a.Duration, errors);
        }

        float previous = 0;
        foreach (var e in a.Events)
        {
            if (string.IsNullOrWhiteSpace(e.Name)) errors.Add($"Événement sans nom à {e.Time} s.");
            if (e.Time < previous || e.Time > a.Duration)
                errors.Add($"Événement '{e.Name}' : temps {e.Time} hors de [0, durée] ou non trié.");
            previous = e.Time;
        }
        ThrowIfAny(errors);
        return a;
    }

    /// <summary>Vérifie que l'animation vise <paramref name="skeleton"/> et n'anime que des os existants.</summary>
    public static void ValidateAnimation(AnimationData animation, SkeletonData skeleton)
    {
        var errors = new List<string>();
        CheckSkeletonId(animation.Skeleton, skeleton, errors);
        foreach (var bone in animation.Bones.Keys)
            if (skeleton.IndexOf(bone) < 0) errors.Add($"Os '{bone}' absent du squelette.");
        ThrowIfAny(errors);
    }

    // ── Commun ────────────────────────────────────────────────────────────

    private static string Encode<T>(string format, T model)
    {
        var body = JsonSerializer.SerializeToNode(model, Options)!.AsObject();
        // format et version en tête : c'est ce qu'on lit en premier en ouvrant le fichier.
        var root = new JsonObject { ["format"] = format, ["version"] = CurrentVersion };
        foreach (var (key, value) in body.ToList())
        {
            body.Remove(key);
            root[key] = value;
        }
        return root.ToJsonString(Options);
    }

    private static T Decode<T>(string json, string expectedFormat)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) as JsonObject ?? throw new AnimationFormatException("Le contenu n'est pas un objet JSON.");
        }
        catch (JsonException ex)
        {
            throw new AnimationFormatException($"JSON invalide : {ex.Message}", ex);
        }

        var format = root["format"]?.GetValueKind() == JsonValueKind.String ? root["format"]!.GetValue<string>() : null;
        if (format != expectedFormat)
            throw new AnimationFormatException($"Format '{format ?? "(absent)"}' alors que '{expectedFormat}' est attendu.");

        var version = root["version"]?.GetValueKind() == JsonValueKind.Number ? root["version"]!.GetValue<int>() : 0;
        if (version < 1 || version > CurrentVersion)
            throw new AnimationFormatException(
                $"Version {version} non prise en charge (version courante : {CurrentVersion}).");
        // Futures migrations : réécrire ici `root` de la version lue vers CurrentVersion.

        root.Remove("format");
        root.Remove("version");
        try
        {
            return root.Deserialize<T>(Options) ?? throw new AnimationFormatException("Contenu vide.");
        }
        catch (JsonException ex)
        {
            throw new AnimationFormatException(ex.Message, ex);
        }
    }

    private static void CheckKeys<TKey>(List<TKey>? keys, string track, float duration, List<string> errors)
        where TKey : IKeyframe
    {
        if (keys is null) return;
        if (keys.Count == 0) { errors.Add($"Piste {track} vide."); return; }
        for (int i = 0; i < keys.Count; i++)
        {
            var t = keys[i].Time;
            if (t < 0 || t > duration) errors.Add($"Piste {track} : clé à {t} s hors de [0, {duration}].");
            if (i > 0 && t <= keys[i - 1].Time) errors.Add($"Piste {track} : clés non strictement croissantes à {t} s.");
        }
    }

    private static void CheckSkeletonId(string target, SkeletonData skeleton, List<string> errors)
    {
        if (target != skeleton.Id) errors.Add($"Vise le squelette '{target}', pas '{skeleton.Id}'.");
    }

    private static void RequireId(string id, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id)) errors.Add("Id manquant.");
    }

    private static void ThrowIfAny(List<string> errors)
    {
        if (errors.Count > 0) throw new AnimationFormatException(string.Join(Environment.NewLine, errors));
    }
}

public sealed class AnimationFormatException : Exception
{
    public AnimationFormatException(string message, Exception? inner = null) : base(message, inner) { }
}
