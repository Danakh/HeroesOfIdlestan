using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.Persistence;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Test.Tests.TestUtilities;

/// <summary>
/// Accès aux fixtures <c>HeroesOfIdlestan.Test.Tests/saves/{dossier}/{nom}.sav</c>, lues et écrites dans les
/// sources (pas dans bin/) pour être versionnées avec le code.
///
/// <list type="bullet">
/// <item><c>current</c> : régénéré à la demande par <c>SaveFixtureGeneratorTests</c>, toujours
/// estampillé <see cref="GameVersion.Current"/>.</item>
/// <item><c>release-X.Y</c> : copie figée de <c>current</c> au moment d'une sortie. Lecture seule, à
/// vie — toute écriture lève.</item>
/// </list>
/// </summary>
public static class SaveFixtures
{
    public const string CurrentFolder = "current";
    public const string ReleasePrefix = "release-";
    public const string Extension = ".sav";

    public const string RegenerateCommand =
        "$env:BLANK_MANUAL_TESTS=1; dotnet test HeroesOfIdlestan.Test.Tests --filter \"FullyQualifiedName~Rebuild_Current_Saves\"";

    public static string SavesRoot { get; } = FindSavesRoot();

    private static string FindSavesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HeroesOfIdlestan.slnx")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("HeroesOfIdlestan.slnx introuvable au-dessus du dossier de test.");
        return Path.Combine(dir.FullName, "HeroesOfIdlestan.Test.Tests", "saves");
    }

    public static string PathOf(string folder, string name) => Path.Combine(SavesRoot, folder, name + Extension);

    public static bool Exists(string folder, string name) => File.Exists(PathOf(folder, name));

    public static IEnumerable<string> ReleaseFolders() =>
        Directory.Exists(SavesRoot)
            ? Directory.GetDirectories(SavesRoot, ReleasePrefix + "*").Select(Path.GetFileName).OfType<string>().Order()
            : [];

    public static IEnumerable<string> Names(string folder)
    {
        var dir = Path.Combine(SavesRoot, folder);
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*" + Extension).Select(Path.GetFileNameWithoutExtension).OfType<string>().Order()
            : [];
    }

    /// <summary>
    /// Charge une fixture dans un contrôleur neuf. Pour <c>current</c>, exige aussi que la version
    /// estampillée soit <see cref="GameVersion.Current"/> : sinon les tests tourneraient sur des
    /// sauvegardes produites par une ancienne logique de jeu sans que personne ne le voie.
    /// </summary>
    public static GameController Load(string folder, string name)
    {
        var path = PathOf(folder, name);
        Assert.True(File.Exists(path),
            $"Fixture absente : {path}. Pour '{CurrentFolder}', la régénérer : {RegenerateCommand}");

        var controller = new GameController();
        controller.Load(File.ReadAllText(path));

        if (folder == CurrentFolder)
        {
            var saved = controller.State.SavedGameVersion;
            Assert.True(saved == GameVersion.Current,
                $"{folder}/{name} a été écrite en version '{saved ?? "(aucune)"}', la version courante est " +
                $"'{GameVersion.Current}'. Régénérer : {RegenerateCommand}");
        }
        return controller;
    }

    /// <summary>Écrit une fixture (atomiquement), après avoir vérifié son round-trip.</summary>
    public static void Write(GameController controller, string folder, string name)
    {
        if (folder.StartsWith(ReleasePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Écriture refusée dans '{folder}' : les fixtures de release sont figées.");

        var content = AssertRoundTrip(controller);
        var path = PathOf(folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Supprime de <paramref name="folder"/> les fixtures absentes de <paramref name="keep"/>. À appeler
    /// après une régénération complète, jamais avant : chaque fixture régénérée est remplacée
    /// atomiquement et reste lisible pendant toute la reconstruction.
    /// </summary>
    public static void Prune(string folder, IEnumerable<string> keep)
    {
        var keepSet = keep.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Names(folder).Where(n => !keepSet.Contains(n)).ToList())
            File.Delete(PathOf(folder, name));
    }

    /// <summary>
    /// Export → import → export doit redonner exactement le même JSON. Plus strict qu'une comparaison
    /// de quelques comptes : une propriété perdue ou mal relue à l'aller-retour fait échouer le test.
    /// Renvoie l'export encodé.
    /// </summary>
    public static string AssertRoundTrip(GameController controller)
    {
        var service = new SaveService();
        var exported = controller.Export();
        var json1 = SaveCodec.Decode(exported);

        var reloaded = new GameController();
        reloaded.Load(exported);
        var json2 = service.Serialize(reloaded.State);

        Assert.Equal(json1, json2);
        return exported;
    }
}
