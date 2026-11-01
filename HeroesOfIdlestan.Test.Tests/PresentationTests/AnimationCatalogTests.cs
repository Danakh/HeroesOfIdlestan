using HeroesOfIdlestan.Core.Model.Animation;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Presentation.Animation;

namespace HeroesOfIdlestan.Test.Tests.PresentationTests;

/// <summary>
/// Le catalogue d'animations et ses secours : une animation absente est remplacée par la suivante
/// de sa chaîne, et le remplacement est signalé une fois dans le journal.
/// </summary>
[Collection(GameLogCollection.Name)]
public class AnimationCatalogTests
{
    private static List<GameLog.Entry> WarningsAbout(string owner) =>
        GameLog.Snapshot().Where(e => e.Level == GameLog.Level.Warning && e.Operation == owner).ToList();

    [Fact]
    public void EveryChain_EndsOnIdle()
    {
        foreach (var name in AnimationCatalog.All)
        {
            string current = name;
            for (int guard = 0; AnimationCatalog.FallbackOf(current) is { } next; guard++)
            {
                Assert.True(guard < 10, $"Chaîne de secours sans fin depuis {name}");
                current = next;
            }
            Assert.Equal(AnimationNames.Idle, current);
        }
    }

    [Theory]
    [InlineData(AnimationNames.AttackBow, AnimationNames.Attack)]
    [InlineData(AnimationNames.AttackMagic, AnimationNames.Attack)]
    [InlineData(AnimationNames.AttackMelee, AnimationNames.Attack)]
    [InlineData(AnimationNames.Hurt, AnimationNames.Idle)]
    [InlineData(AnimationNames.Death, AnimationNames.Idle)]
    [InlineData(AnimationNames.Walk, AnimationNames.Idle)]
    public void Fallback_IsTheNextOfTheChain(string name, string expected) =>
        Assert.Equal(expected, AnimationCatalog.FallbackOf(name));

    [Fact]
    public void PresentAnimation_IsUsedAsIs_WithoutWarning()
    {
        string owner = "complete-" + Guid.NewGuid();
        var resolver = new AnimationResolver(owner, _ => true);

        Assert.Equal(AnimationNames.AttackBow, resolver.Resolve(AnimationNames.AttackBow));
        Assert.Empty(WarningsAbout(owner));
    }

    [Fact]
    public void MissingVariant_FallsBackToBaseAttack_ThenToIdle()
    {
        string owner = "partial-" + Guid.NewGuid();
        var withAttack = new AnimationResolver(owner, n => n is AnimationNames.Idle or AnimationNames.Attack);
        Assert.Equal(AnimationNames.Attack, withAttack.Resolve(AnimationNames.AttackBow));

        var idleOnly = new AnimationResolver(owner + "-idle", n => n == AnimationNames.Idle);
        Assert.Equal(AnimationNames.Idle, idleOnly.Resolve(AnimationNames.AttackBow));
    }

    [Fact]
    public void Replacement_IsLoggedOnce_AsAWarning()
    {
        string owner = "robot-" + Guid.NewGuid();
        var resolver = new AnimationResolver(owner, n => n == AnimationNames.Idle);

        resolver.Resolve(AnimationNames.Hurt);
        resolver.Resolve(AnimationNames.Hurt);

        var warning = Assert.Single(WarningsAbout(owner));
        Assert.Equal(1, warning.Count);
        Assert.Contains("hurt", warning.Message);
        Assert.Contains("idle", warning.Message);
    }

    [Fact]
    public void NothingInTheChain_GivesNull_AndIsLogged()
    {
        string owner = "empty-" + Guid.NewGuid();
        var resolver = new AnimationResolver(owner, _ => false);

        Assert.Null(resolver.Resolve(AnimationNames.Walk));
        Assert.Single(WarningsAbout(owner));
    }

    [Fact]
    public void Warrior_AttacksInMelee_MonstersWithTheBaseAttack()
    {
        Assert.Equal(AnimationNames.AttackMelee, AnimationCatalog.AttackFor(new HeroEntity { Class = HeroClass.Warrior }));
        Assert.Equal(AnimationNames.Attack, AnimationCatalog.AttackFor(new MonsterEntity { Species = MonsterSpecies.Rat }));
    }

    [Fact]
    public void Humanoid_HasTheWholeCatalogue()
    {
        var ids = Directory.EnumerateFiles(AssetsRoot(), "*.anim.json", SearchOption.AllDirectories)
            .Select(f => AnimationCodec.DecodeAnimation(File.ReadAllText(f)))
            .Where(a => a.Skeleton == "humanoid")
            .Select(a => a.Id)
            .ToHashSet();

        Assert.Subset(ids, AnimationCatalog.All.ToHashSet());
    }

    private static string AssetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HeroesOfIdlestan.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "assets", "animation");
    }
}

/// <summary>
/// Les tests qui observent le <see cref="GameLog"/> statique : sans parallélisme, pour qu'un message
/// consigné par un autre test ne change pas ce qu'ils voient.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GameLogCollection
{
    public const string Name = "GameLog";
}
