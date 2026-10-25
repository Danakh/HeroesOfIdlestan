using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Test.Tests.TestUtilities;

/// <summary>
/// Une étape de scénario : partir de la fixture de l'étape précédente, jouer jusqu'à
/// <see cref="Condition"/>, et produire la fixture <see cref="SaveName"/>.
/// </summary>
public sealed class ScenarioStep
{
    public required string SaveName { get; init; }

    /// <summary>Fait progresser la partie d'un pas. Appelé jusqu'à ce que <see cref="Condition"/> soit vraie.</summary>
    public required Action<GameController> Advance { get; init; }

    public required Func<GameController, bool> Condition { get; init; }

    /// <summary>Garde-fou : nombre maximal d'appels à <see cref="Advance"/> avant échec.</summary>
    public int MaxIterations { get; init; } = 10_000;
}

/// <summary>Suite d'étapes enchaînées. La première part d'une nouvelle partie au seed donné.</summary>
public sealed record Scenario(string Name, int Seed, IReadOnlyList<ScenarioStep> Steps);

public static class ScenarioRunner
{
    /// <summary>
    /// Joue l'étape <paramref name="index"/> en partant de la fixture de l'étape précédente, lue dans
    /// <paramref name="folder"/>. Vérifie la condition d'arrivée et le round-trip ; écrit la fixture
    /// produite si <paramref name="save"/> (jamais dans un dossier de release).
    /// </summary>
    public static GameController RunStep(Scenario scenario, int index, string folder, bool save)
    {
        GameController controller;
        if (index == 0)
        {
            controller = new GameController();
            controller.NewGame(scenario.Seed);
        }
        else
        {
            controller = SaveFixtures.Load(folder, scenario.Steps[index - 1].SaveName);
        }

        var step = scenario.Steps[index];
        int iterations = 0;
        while (!step.Condition(controller))
        {
            Assert.True(++iterations <= step.MaxIterations,
                $"{scenario.Name}/{step.SaveName} : condition non atteinte après {step.MaxIterations} itérations " +
                $"(tick {controller.State.Clock.CurrentTick}, or {controller.State.Gold.Value}).");
            step.Advance(controller);
        }

        if (save) SaveFixtures.Write(controller, folder, step.SaveName);
        else SaveFixtures.AssertRoundTrip(controller);
        return controller;
    }
}

/// <summary>
/// Scénarios du contenu d'exemple. À réécrire avec le vrai jeu : une étape par jalon de progression
/// (premier bâtiment, première ressource avancée, premier prestige…), pour que chaque fixture couvre
/// un morceau de modèle de plus que la précédente.
/// </summary>
public static class SampleScenarios
{
    private static void PlayOneSecond(GameController c) => c.SimulateTicks(GameClock.TicksPerSecond);

    private static void BuyOrWait(GameController c)
    {
        if (!c.TryBuyProducer()) PlayOneSecond(c);
    }

    public static readonly Scenario Main = new("Main", Seed: 1234,
    [
        new ScenarioStep
        {
            SaveName = "Main_Start",
            Advance = PlayOneSecond,
            Condition = c => c.State.Clock.CurrentTick >= 10 * GameClock.TicksPerSecond,
        },
        new ScenarioStep
        {
            SaveName = "Main_ThreeProducers",
            Advance = BuyOrWait,
            Condition = c => c.State.Entities.Count >= 3,
        },
        new ScenarioStep
        {
            SaveName = "Main_Gold1000",
            Advance = BuyOrWait,
            Condition = c => c.State.Gold >= 1000,
        },
    ]);

    public static readonly IReadOnlyList<Scenario> All = [Main];
}
