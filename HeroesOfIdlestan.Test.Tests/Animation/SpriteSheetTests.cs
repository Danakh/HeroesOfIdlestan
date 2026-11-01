using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.Animation;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Animation;
using HeroesOfIdlestan.Core.Render.Animation;
using HeroesOfIdlestan.Core.Render.Renderers;
using HeroesOfIdlestan.Core.Render.Screens;
using SkiaSharp;

namespace HeroesOfIdlestan.Test.Tests.Animation;

/// <summary>
/// Les planches de sprites (design/09) : manifestes livrés lisibles et cohérents avec leurs images,
/// lecture des images dans le temps, et dessin des combattants par la pile Skia.
/// </summary>
public class SpriteSheetTests
{
    private static readonly string SpritesRoot = Path.Combine(RepositoryRoot(), "assets", "sprites");

    [Fact]
    public void ShippedSpriteSheets_DecodeAndMatchTheirImages()
    {
        var manifests = Directory.EnumerateFiles(SpritesRoot, "*.sprites.json", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(manifests);
        foreach (var path in manifests)
        {
            var sheet = AnimationCodec.DecodeSpriteSheet(File.ReadAllText(path));
            foreach (var clip in sheet.Animations)
            {
                var image = Path.Combine(Path.GetDirectoryName(path)!, clip.Image);
                Assert.True(File.Exists(image), $"{path} : {clip.Image} absente.");
                using var codec = SKCodec.Create(image);
                Assert.True(codec.Info.Width >= clip.Columns * sheet.FrameWidth, $"{clip.Image} trop étroite.");
                Assert.True(codec.Info.Height >= clip.Rows * sheet.FrameHeight, $"{clip.Image} trop basse.");
            }
        }
    }

    [Fact]
    public void FighterSpriteSheet_IsEmbedded_WithTheBaseAnimationsOfTheRobot()
    {
        using var sheet = SpriteSheet.LoadEmbedded(FighterRenderer.SpriteSheetId);
        Assert.NotNull(sheet);
        Assert.True(sheet.Data.Has(AnimationNames.Idle));
        Assert.True(sheet.Data.Has(AnimationNames.Walk));
        Assert.True(sheet.Data.Has(FighterAnimation.Intro));
        Assert.True(sheet.Data.Has(FighterAnimation.LookAround));
    }

    [Fact]
    public void FrameAt_LoopsOrHoldsTheLastFrame()
    {
        var clip = new SpriteClipData
        {
            Name = "walk", Image = "walk.png", Frames = 4, Columns = 4, Rows = 1,
            FrameDuration = 0.25f, Duration = 1f, Loop = true,
        };
        Assert.Equal(0, clip.FrameAt(0));
        Assert.Equal(1, clip.FrameAt(0.3));
        Assert.Equal(0, clip.FrameAt(1.1));      // boucle
        Assert.Equal(3, clip.FrameAt(1.1, loop: false)); // tenue
        Assert.Equal(0, clip.FrameAt(-1));
    }

    [Fact]
    public void Find_IgnoresCase_AndPicksTheClosestDirection()
    {
        SpriteClipData Clip(float direction) => new()
        {
            Name = "Walk", Direction = direction, Image = $"walk_{direction}.png", Frames = 1, Columns = 1, Rows = 1,
            FrameDuration = 0.1f, Duration = 0.1f,
        };
        var sheet = new SpriteSheetData
        {
            Model = "m.glb", FrameWidth = 10, FrameHeight = 10, PivotX = 5, PivotY = 10, PixelsPerUnit = 10,
            Animations = [Clip(0), Clip(90), Clip(180), Clip(270)],
        };
        Assert.Equal(0, sheet.Find("walk")!.Direction);
        Assert.Equal(270, sheet.Find("WALK", 300)!.Direction);
        Assert.Equal(0, sheet.Find("walk", 350)!.Direction);
        Assert.Null(sheet.Find("run"));
    }

    [Fact]
    public void Decode_RejectsUnknownPropertiesAndBadGrids()
    {
        const string valid = """
            { "format": "hoi-spritesheet", "version": 1, "model": "m.glb",
              "frameWidth": 10, "frameHeight": 10, "pivotX": 5, "pivotY": 10, "pixelsPerUnit": 10,
              "animations": [ { "name": "idle", "image": "idle.png", "frames": 4, "columns": 2, "rows": 2,
                                "frameDuration": 0.1, "duration": 0.4, "loop": true } ] }
            """;
        Assert.Equal(4, AnimationCodec.DecodeSpriteSheet(valid).Animations[0].Frames);
        Assert.Throws<AnimationFormatException>(() => AnimationCodec.DecodeSpriteSheet(valid.Replace("\"model\"", "\"modle\"")));
        Assert.Throws<AnimationFormatException>(() => AnimationCodec.DecodeSpriteSheet(valid.Replace("\"rows\": 2", "\"rows\": 1")));
        Assert.Throws<AnimationFormatException>(() => AnimationCodec.DecodeSpriteSheet(valid.Replace("hoi-spritesheet", "hoi-skin")));
    }

    [Fact]
    public void Background_FollowsThePhaseOfTheRun()
    {
        var game = new GameController();
        game.NewGame(1);
        var run = game.State.Run;
        Assert.Equal(AnimationNames.Idle, FighterAnimation.Background(run, isHero: false, hasLookAround: true));
        Assert.Equal(AnimationNames.Idle, FighterAnimation.Background(run, isHero: true, hasLookAround: true));
    }

    [Fact]
    public void Cues_SpotNewActionsAndLostHealth()
    {
        var game = new GameController();
        game.NewGame(1);
        var hero = game.State.Heroes[0];
        var cues = new FighterCues(hero);
        Assert.Equal((false, false), cues.Observe(hero));

        game.SimulateTicks(20 * GameClock.TicksPerSecond);
        var (acted, hurt) = cues.Observe(hero);
        Assert.True(acted || hurt, "Après 20 s de combat, le héros a agi ou a été touché.");
        Assert.Equal((false, false), cues.Observe(hero));
    }

    [Fact]
    public void GameScreen_DrawsTheFightersAsSprites()
    {
        var game = new GameController();
        game.NewDebugGame(1);
        using var screen = new GameScreen(game);
        using var bitmap = new SKBitmap(960, 540);
        using var canvas = new SKCanvas(bitmap);

        for (int frame = 0; frame < 30; frame++)
        {
            game.SimulateTicks(GameClock.TicksPerSecond / 10, chunkTicks: 10);
            canvas.Clear(SKColors.Black);
            screen.Render(canvas, new SKSize(bitmap.Width, bitmap.Height), 0.1f, frame * 0.1f);
        }

        // Pour regarder le rendu : HOI_RENDER_DUMP=<fichier.png> dotnet test --filter GameScreen_Draws
        if (Environment.GetEnvironmentVariable("HOI_RENDER_DUMP") is { Length: > 0 } dump)
        {
            using var file = File.Create(dump);
            bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
        }

        using var fighters = new FighterRenderer();
        Assert.True(fighters.UsesSprites, "Planches embarquées absentes : le jeu dessinerait des ronds.");

        // Le robot, ses teintes et les barres de vie : bien plus de pixels colorés que le sol seul.
        int colored = 0;
        for (int y = 0; y < bitmap.Height; y += 2)
        for (int x = 0; x < bitmap.Width; x += 2)
        {
            var c = bitmap.GetPixel(x, y);
            if (Math.Max(c.Red, Math.Max(c.Green, c.Blue)) - Math.Min(c.Red, Math.Min(c.Green, c.Blue)) > 40) colored++;
        }
        Assert.True(colored > 500, $"Seulement {colored} pixels colorés : les sprites ne sont pas dessinés.");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HeroesOfIdlestan.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("HeroesOfIdlestan.slnx introuvable au-dessus du dossier de test.");
    }
}
