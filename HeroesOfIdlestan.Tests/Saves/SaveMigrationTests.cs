using System.Text.Json.Nodes;
using HeroesOfIdlestan.Core.Persistence;

namespace HeroesOfIdlestan.Tests.Saves;

/// <summary>Mécanique des migrations : quelles sauvegardes elles touchent, et dans quel ordre.</summary>
public class SaveMigrationTests
{
    /// <summary>Exemple type : en 0.2.0, la ressource « Money » a été renommée « Gold ».</summary>
    private sealed class RenameMoneyToGold : ISaveMigration
    {
        public string IntroducedIn => "0.2.0";

        public void Apply(JsonObject root)
        {
            if (root["Money"] is { } money && !root.ContainsKey("Gold"))
            {
                root.Remove("Money");
                root["Gold"] = money;
            }
        }
    }

    /// <summary>Consigne son passage, pour vérifier l'ordre de la chaîne.</summary>
    private sealed class Recorder(string version, List<string> log) : ISaveMigration
    {
        public string IntroducedIn => version;
        public void Apply(JsonObject root) => log.Add(version);
    }

    [Fact]
    public void Migration_AppliesToOlderSave()
    {
        var service = new SaveService([new RenameMoneyToGold()]);
        var state = service.Deserialize("""{"SavedGameVersion":"0.1.0","Money":17}""");
        Assert.Equal<long>(17, state.Gold);
    }

    [Fact]
    public void Migration_AppliesToUnversionedSave()
    {
        var service = new SaveService([new RenameMoneyToGold()]);
        var state = service.Deserialize("""{"Money":5}""");
        Assert.Equal<long>(5, state.Gold);
    }

    [Fact]
    public void Migration_SkipsSaveAlreadyAtItsVersion()
    {
        var log = new List<string>();
        var service = new SaveService([new Recorder("0.2.0", log)]);
        service.Deserialize("""{"SavedGameVersion":"0.2.0"}""");
        service.Deserialize("""{"SavedGameVersion":"0.3.1"}""");
        Assert.Empty(log);
    }

    [Fact]
    public void Migrations_RunInDeclaredOrder_FromTheSaveVersionOn()
    {
        var log = new List<string>();
        var service = new SaveService([new Recorder("0.2.0", log), new Recorder("0.3.0", log), new Recorder("0.4.0", log)]);

        service.Deserialize("""{"SavedGameVersion":"0.2.5"}""");

        Assert.Equal(["0.3.0", "0.4.0"], log);
    }
}
