using HeroesOfIdlestan.Core.Platform;

namespace HeroesOfIdlestan.Android;

/// <summary>
/// Stockage interne de l'application (<c>LocalApplicationData/saves</c>) : privé, sans permission,
/// effacé à la désinstallation. Ne pas reprendre le service du bureau : <c>%AppData%</c> n'a pas de
/// sens ici. Écriture atomique, comme sur le bureau.
/// </summary>
public sealed class AndroidFileSystemService : IFileSystemService
{
    private static string Root
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "saves");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public void WriteText(string name, string content)
    {
        var path = Path.Combine(Root, name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    public string? ReadText(string name)
    {
        var path = Path.Combine(Root, name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Delete(string name)
    {
        var path = Path.Combine(Root, name);
        if (File.Exists(path)) File.Delete(path);
    }
}
