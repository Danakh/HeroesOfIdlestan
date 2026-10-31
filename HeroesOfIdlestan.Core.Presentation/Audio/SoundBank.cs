using System.Text;
using HeroesOfIdlestan.Core.Model.Diagnostics;

namespace HeroesOfIdlestan.Core.Presentation.Audio;

/// <summary>
/// Charge les WAV embarqués dans cet assembly et les remet au head. Un fichier manquant est
/// journalisé et ignoré : ce son reste muet, le jeu démarre quand même.
/// </summary>
public static class SoundBank
{
    private const string ResourceFolder = "Resources.sounds.";

    /// <summary>Nom du fichier d'un son : la valeur de l'enum en snake_case.</summary>
    public static string FileName(SoundId id)
    {
        var name = id.ToString();
        var builder = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                if (i > 0) builder.Append('_');
                builder.Append(char.ToLowerInvariant(name[i]));
            }
            else builder.Append(name[i]);
        }
        return builder.Append(".wav").ToString();
    }

    /// <summary>WAV embarqué d'un son, ou null s'il est absent.</summary>
    public static byte[]? Read(SoundId id)
    {
        var assembly = typeof(SoundBank).Assembly;
        var resource = $"{assembly.GetName().Name}.{ResourceFolder}{FileName(id)}";
        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream == null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Charge tous les sons dans la sortie du head. Renvoie le nombre chargé.</summary>
    public static int LoadAll(IAudioService audio)
    {
        int loaded = 0;
        foreach (var id in Enum.GetValues<SoundId>())
        {
            var wav = Read(id);
            if (wav == null)
            {
                GameLog.Error(nameof(SoundBank), nameof(LoadAll), $"Bruitage absent : {FileName(id)}");
                continue;
            }
            audio.Load(id, wav);
            loaded++;
        }
        return loaded;
    }
}
