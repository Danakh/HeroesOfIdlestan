using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Controller.Content;
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
                $"(tick {controller.State.Clock.CurrentTick}, zone {controller.State.Run.Zone}).");
            step.Advance(controller);
        }

        if (save) SaveFixtures.Write(controller, folder, step.SaveName);
        else SaveFixtures.AssertRoundTrip(controller);
        return controller;
    }
}

/// <summary>
/// Scénarios du jeu, une étape par jalon de progression : chaque fixture couvre un morceau de modèle
/// de plus que la précédente (premiers combats, avance automatique, défaite et recul, niveau maximum).
/// </summary>
public static class GameScenarios
{
    private static void PlayOneSecond(GameController c) => c.SimulateTicks(GameClock.TicksPerSecond);

    /// <summary>Joue une minute puis relance l'avance automatique, comme un joueur qui repasse de temps en temps.</summary>
    private static void PlayAMinuteThenPush(GameController c)
    {
        c.SimulateTicks(60 * GameClock.TicksPerSecond);
        c.SetAutoAdvance(true);
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
            SaveName = "Main_Zone5",
            Advance = PlayOneSecond,
            Condition = c => c.State.Run.HighestZone >= 5,
        },
        new ScenarioStep
        {
            SaveName = "Main_FirstDefeat",
            Advance = PlayOneSecond,
            Condition = c => !c.State.Run.AutoAdvance,
        },
        new ScenarioStep
        {
            SaveName = "Main_MaxLevel",
            Advance = PlayAMinuteThenPush,
            Condition = c => c.State.Heroes[0].Level >= GameContent.MaxLevel,
        },
    ]);

    public static readonly IReadOnlyList<Scenario> All = [Main];
}
