using Godot;
using HeroesOfIdlestan.Core.Model.Platform;

namespace HeroesOfIdlestan.App.Godot;

/// <summary>
/// Fichiers dans le dossier <c>user://</c> de Godot (<c>%AppData%/Godot/app_userdata/HeroesOfIdlestan</c>
/// sous Windows) : séparé des sauvegardes du head Avalonia, pour que les deux ne s'écrasent pas.
/// Écriture atomique (fichier temporaire puis rename) : un arrêt brutal pendant la sauvegarde ne
/// laisse jamais un fichier vide ou tronqué.
/// </summary>
public sealed class GodotFileSystemService : IFileSystemService
{
    private readonly string _root;

    public GodotFileSystemService(string? root = null)
    {
        _root = root ?? ProjectSettings.GlobalizePath("user://");
        Directory.CreateDirectory(_root);
    }

    public void WriteText(string name, string content)
    {
        var path = Path.Combine(_root, name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    public string? ReadText(string name)
    {
        var path = Path.Combine(_root, name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Delete(string name)
    {
        var path = Path.Combine(_root, name);
        if (File.Exists(path)) File.Delete(path);
    }
}
