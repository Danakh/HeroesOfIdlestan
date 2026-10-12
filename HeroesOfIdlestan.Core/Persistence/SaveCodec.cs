using System.Text;

namespace HeroesOfIdlestan.Core.Persistence;

/// <summary>
/// Encodage des fichiers persistés : JSON → XOR → Base64. Ce n'est pas un chiffrement, seulement de
/// quoi décourager l'édition à la main. Générique : sert aux sauvegardes comme aux réglages.
///
/// <para>La lecture accepte aussi le JSON brut (fichiers anciens, fixtures éditées pour un test,
/// débogage). Ajouter ici tout futur format sans jamais retirer la lecture d'un ancien : une
/// sauvegarde de joueur doit rester lisible pour toujours. <c>SaveCodecTests</c> fige chaque format.</para>
/// </summary>
public static class SaveCodec
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("HeroesOfIdlestan-save-key-change-me");

    public static string Encode(string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        XorCycle(data);
        return Convert.ToBase64String(data);
    }

    /// <summary>Renvoie le JSON contenu, ou lève <see cref="SaveCorruptedException"/>.</summary>
    public static string Decode(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var trimmed = content.Trim();
        if (trimmed.Length == 0) throw new SaveCorruptedException("Fichier de sauvegarde vide.");

        // Format 0 : JSON brut.
        if (LooksLikeJson(trimmed)) return trimmed;

        // Format 1 : JSON → XOR → Base64.
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(trimmed);
        }
        catch (FormatException ex)
        {
            throw new SaveCorruptedException("Contenu ni JSON ni Base64.", ex);
        }
        XorCycle(bytes);
        var json = Encoding.UTF8.GetString(bytes);
        return LooksLikeJson(json) ? json : throw new SaveCorruptedException("Contenu Base64 qui ne décode pas en JSON.");
    }

    private static bool LooksLikeJson(string s)
    {
        var t = s.AsSpan().TrimStart();
        return t.Length > 0 && (t[0] == '{' || t[0] == '[');
    }

    private static void XorCycle(Span<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
            data[i] ^= Key[i % Key.Length];
    }
}

public sealed class SaveCorruptedException : Exception
{
    public SaveCorruptedException(string message, Exception? inner = null) : base(message, inner) { }
}
