namespace HeroesOfIdlestan.Core.Model.Platform;

/// <summary>
/// Stockage du head : fichiers sur le bureau, localStorage dans le navigateur, mémoire dans les tests.
/// Le Core dit <i>quoi</i> écrire, le head dit <i>où</i>.
/// </summary>
public interface IFileSystemService
{
    void WriteText(string name, string content);

    /// <summary>Contenu du fichier, ou null s'il n'existe pas.</summary>
    string? ReadText(string name);

    void Delete(string name);
}

/// <summary>Implémentation en mémoire, pour les tests et les outils headless.</summary>
public sealed class InMemoryFileSystem : IFileSystemService
{
    private readonly Dictionary<string, string> _files = [];

    public void WriteText(string name, string content) => _files[name] = content;

    public string? ReadText(string name) => _files.GetValueOrDefault(name);

    public void Delete(string name) => _files.Remove(name);
}
