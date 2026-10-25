using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Test.Tests.TestUtilities;

namespace HeroesOfIdlestan.Test.Tests.Saves;

/// <summary>
/// Niveau 3 — fixtures <c>saves/current/</c>. Chaque étape de scénario repart de la fixture de
/// l'étape précédente : la chaîne prouve qu'une partie sauvegardée à chaque jalon se recharge et
/// progresse avec le code actuel.
/// </summary>
[Collection(SaveFixtureCollection.Name)]
public class CurrentSaveFixtureTests
{
    public static TheoryData<string, int> Steps()
    {
        var data = new TheoryData<string, int>();
        foreach (var scenario in SampleScenarios.All)
            for (int i = 0; i < scenario.Steps.Count; i++)
                data.Add(scenario.Name, i);
        return data;
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public void Step_ReplaysFromPreviousFixture(string scenarioName, int index)
    {
        var scenario = SampleScenarios.All.Single(s => s.Name == scenarioName);
        ScenarioRunner.RunStep(scenario, index, SaveFixtures.CurrentFolder, save: false);
    }

    [Fact]
    public void EveryExpectedFixture_Exists_AtCurrentVersion()
    {
        var expected = SampleScenarios.All.SelectMany(s => s.Steps).Select(s => s.SaveName).ToList();
        foreach (var name in expected)
            SaveFixtures.Load(SaveFixtures.CurrentFolder, name); // vérifie présence + version

        var stale = SaveFixtures.Names(SaveFixtures.CurrentFolder).Except(expected).ToList();
        Assert.True(stale.Count == 0,
            $"Fixtures sans étape correspondante dans current/ : {string.Join(", ", stale)}. Régénérer : {SaveFixtures.RegenerateCommand}");
    }
}

/// <summary>
/// Niveau 4 — fixtures <c>saves/release-X.Y/</c> : des sauvegardes réelles de versions livrées.
/// Chacune doit se charger, faire son round-trip, et tourner une minute de jeu sans erreur.
/// Tant qu'aucune version n'est sortie, il n'y a rien à vérifier et le test passe.
/// </summary>
[Collection(SaveFixtureCollection.Name)]
public class ReleaseSaveFixtureTests
{
    [Fact]
    public void EveryReleaseFixture_LoadsAndKeepsPlaying()
    {
        var failures = new List<string>();
        foreach (var folder in SaveFixtures.ReleaseFolders())
        {
            foreach (var name in SaveFixtures.Names(folder))
            {
                try
                {
                    GameLog.Clear();
                    var c = SaveFixtures.Load(folder, name);
                    SaveFixtures.AssertRoundTrip(c);
                    c.SimulateTicks(60 * GameClock.TicksPerSecond);
                    if (GameLog.Snapshot().Count > 0)
                        failures.Add($"{folder}/{name} : erreur pendant la simulation — {GameLog.Snapshot()[0].Message}");
                }
                catch (Exception ex)
                {
                    failures.Add($"{folder}/{name} : {ex.GetType().Name} — {ex.Message}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ReleaseFolders_AreReadOnly()
    {
        var c = new HeroesOfIdlestan.Core.Controller.GameController();
        c.NewGame(seed: 1);
        Assert.Throws<InvalidOperationException>(() => SaveFixtures.Write(c, "release-9.9", "Forbidden"));
        Assert.False(SaveFixtures.Exists("release-9.9", "Forbidden"));
    }
}

/// <summary>
/// Régénération de <c>saves/current/</c> — action volontaire, jamais déclenchée par « lancer tous les
/// tests ». À relancer après tout changement de <see cref="GameVersion.Current"/> ou de logique qui
/// modifie les sauvegardes produites par les scénarios.
/// </summary>
[Collection(SaveFixtureCollection.Name)]
public class SaveFixtureGeneratorTests
{
    [ManualFact]
    public void Rebuild_Current_Saves()
    {
        foreach (var scenario in SampleScenarios.All)
            for (int i = 0; i < scenario.Steps.Count; i++)
                ScenarioRunner.RunStep(scenario, i, SaveFixtures.CurrentFolder, save: true);

        // Le ménage vient après : chaque fixture a été remplacée atomiquement pendant la boucle.
        SaveFixtures.Prune(SaveFixtures.CurrentFolder,
            SampleScenarios.All.SelectMany(s => s.Steps).Select(s => s.SaveName));
    }
}

/// <summary>Les tests qui lisent ou écrivent les fixtures ne tournent pas en parallèle entre eux.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SaveFixtureCollection
{
    public const string Name = "SaveFixtures";
}
