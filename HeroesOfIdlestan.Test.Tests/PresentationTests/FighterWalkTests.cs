using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Presentation.Battle;

namespace HeroesOfIdlestan.Test.Tests.PresentationTests;

/// <summary>La marche affichée d'un combattant vers sa nouvelle place.</summary>
public class FighterWalkTests
{
    [Fact]
    public void TheFirstPlace_IsTakenWithoutWalking()
    {
        var walk = new FighterWalk();
        walk.Target(3, 4, now: 50);
        Assert.False(walk.IsWalking(50));
        Assert.Equal((3f, 4f), walk.PositionAt(50));
    }

    [Fact]
    public void ANewPlace_IsReachedInMoveTicks()
    {
        var walk = new FighterWalk();
        walk.Target(0, 0, now: 0);
        walk.Target(10, 0, now: 100);

        Assert.True(walk.IsWalking(100));
        Assert.Equal((5f, 0f), walk.PositionAt(100 + GameContent.MoveTicks / 2));
        Assert.False(walk.IsWalking(100 + GameContent.MoveTicks));
        Assert.Equal((10f, 0f), walk.PositionAt(100 + GameContent.MoveTicks));
    }

    [Fact]
    public void ARetarget_StartsFromWhereItIsDrawn()
    {
        var walk = new FighterWalk();
        walk.Target(0, 0, now: 0);
        walk.Target(10, 0, now: 0);
        long half = GameContent.MoveTicks / 2;
        walk.Target(5, 10, now: half);        // à mi-chemin : (5, 0)
        Assert.Equal((5f, 0f), walk.PositionAt(half));
        Assert.Equal((5f, 10f), walk.PositionAt(half + GameContent.MoveTicks));
    }

    [Fact]
    public void Halt_StopsWhereItIs()
    {
        var walk = new FighterWalk();
        walk.Target(0, 0, now: 0);
        walk.Target(10, 0, now: 0);
        walk.Halt(GameContent.MoveTicks / 2);
        Assert.False(walk.IsWalking(GameContent.MoveTicks / 2));
        Assert.Equal((5f, 0f), walk.PositionAt(10 * GameContent.MoveTicks));
    }
}
