using HeroesOfIdlestan.Core.Model.Platform;

namespace HeroesOfIdlestan.App.Desktop;

/// <summary>
/// Fichiers dans <c>%AppData%/HeroesOfIdlestan</c>. Écriture atomique (fichier temporaire puis rename) :
/// un arrêt brutal pendant la sauvegarde ne laisse jamais un fichier vide ou tronqué.
/// </summary>
public sealed class DesktopFileSystemService : IFileSystemService
{
    private readonly string _root;

    public DesktopFileSystemService(string? root = null)
    {
        _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeroesOfIdlestan");
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
