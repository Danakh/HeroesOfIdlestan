using System.Numerics;
using HeroesOfIdlestan.Core.Animation;
using HeroesOfIdlestan.Render.Animation;
using SkiaSharp;

namespace HeroesOfIdlestan.Tests.Animation;

/// <summary>
/// Le système d'animation : format des fichiers, échantillonnage, lecteur, et les données livrées dans
/// assets/animation/ (toutes doivent se lire et se rattacher à leur squelette).
/// </summary>
public class AnimationTests
{
    private static readonly string AssetsRoot = FindAssetsRoot();

    private static SkeletonData Arm() => new()
    {
        Id = "arm",
        Bones =
        [
            new BoneData { Name = "root" },
            new BoneData { Name = "upper", Parent = "root", Rotation = 90, Length = 10 },
            new BoneData { Name = "lower", Parent = "upper", X = 10, Length = 10 },
        ],
    };

    private static AnimationData Swing(bool loop = false) => new()
    {
        Id = "swing",
        Skeleton = "arm",
        Duration = 1,
        Loop = loop,
        Bones =
        {
            ["upper"] = new BoneTimeline
            {
                Rotate = [new RotateKey(0, 0), new RotateKey(0.5f, 90, Curve.Step), new RotateKey(1, 0)],
                Translate = [new VectorKey(0, 0, 0, Curve.Smooth), new VectorKey(1, 10, -10)],
            },
        },
        Events = [new AnimationEvent(0, "start"), new AnimationEvent(0.75f, "hit")],
    };

    // ── Format ────────────────────────────────────────────────────────────

    [Fact]
    public void Skeleton_RoundTrips()
    {
        var json = AnimationCodec.EncodeSkeleton(Arm());
        Assert.StartsWith("{\n  \"format\": \"hoi-skeleton\",\n  \"version\": 1,", json.ReplaceLineEndings("\n"));

        var back = AnimationCodec.DecodeSkeleton(json);
        Assert.Equal(["root", "upper", "lower"], back.Bones.Select(b => b.Name));
        Assert.Equal(90, back.Bones[1].Rotation);
        Assert.Equal(1, back.Bones[2].ScaleX);
        Assert.Equal(AnimationCodec.EncodeSkeleton(back), json);
    }

    [Fact]
    public void Animation_RoundTrips()
    {
        var json = AnimationCodec.EncodeAnimation(Swing());
        var back = AnimationCodec.DecodeAnimation(json);

        Assert.Equal(Swing().Bones["upper"].Rotate, back.Bones["upper"].Rotate);
        Assert.Equal(Swing().Events, back.Events);
        Assert.Equal(AnimationCodec.EncodeAnimation(back), json);
        Assert.DoesNotContain("\"Linear\"", json); // courbe par défaut omise
    }

    [Fact]
    public void Skin_RoundTrips()
    {
        var skin = new SkinData { Id = "s", Skeleton = "arm", Attachments = [new AttachmentData { Bone = "upper", Svg = "u.svg" }] };
        var back = AnimationCodec.DecodeSkin(AnimationCodec.EncodeSkin(skin));
        Assert.Equal("u.svg", Assert.Single(back.Attachments).Svg);
    }

    [Theory]
    [InlineData("""{ "format": "hoi-skin", "version": 1, "id": "a", "skeleton": "arm", "duration": 1 }""", "hoi-animation")]
    [InlineData("""{ "format": "hoi-animation", "version": 99, "id": "a", "skeleton": "arm", "duration": 1 }""", "99")]
    [InlineData("""{ "format": "hoi-animation", "version": 1, "id": "a", "skeleton": "arm", "duration": 1, "lop": true }""", "lop")]
    [InlineData("""{ "format": "hoi-animation", "version": 1, "id": "a", "skeleton": "arm", "duration": 0 }""", "Durée")]
    [InlineData("""{ "format": "hoi-animation", "version": 1, "id": "a", "skeleton": "arm", "duration": 1, "bones": { "upper": { "rotate": [ { "time": 0.5, "angle": 0 }, { "time": 0.2, "angle": 1 } ] } } }""", "croissantes")]
    [InlineData("""{ "format": "hoi-animation", "version": 1, "id": "a", "skeleton": "arm", "duration": 1, "bones": { "upper": { "rotate": [ { "time": 0, "angle": 0, "curve": "Bouncy" } ] } } }""", "")]
    [InlineData("""{ "format": "hoi-animation", "version": 1, "id": "a", "skeleton": "arm", "duration": 1, "events": [ { "time": 2, "name": "late" } ] }""", "late")]
    [InlineData("not json", "JSON")]
    public void InvalidAnimation_IsRejected_WithAnExplicitMessage(string json, string expected)
    {
        var ex = Assert.Throws<AnimationFormatException>(() => AnimationCodec.DecodeAnimation(json));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Skeleton_ParentDeclaredAfterChild_IsRejected()
    {
        const string json = """
            { "format": "hoi-skeleton", "version": 1, "id": "x",
              "bones": [ { "name": "child", "parent": "root" }, { "name": "root" } ] }
            """;
        var ex = Assert.Throws<AnimationFormatException>(() => AnimationCodec.DecodeSkeleton(json));
        Assert.Contains("child", ex.Message);
    }

    [Fact]
    public void Animation_OnUnknownBone_FailsSkeletonValidation()
    {
        var anim = new AnimationData { Id = "a", Skeleton = "arm", Duration = 1, Bones = { ["tail"] = new BoneTimeline() } };
        var ex = Assert.Throws<AnimationFormatException>(() => AnimationCodec.ValidateAnimation(anim, Arm()));
        Assert.Contains("tail", ex.Message);
    }

    // ── Échantillonnage et pose ───────────────────────────────────────────

    [Theory]
    [InlineData(-1, 0)]     // avant la première clé : première valeur
    [InlineData(0.25, 45)]  // linéaire
    [InlineData(0.7, 90)]   // Step : garde la valeur jusqu'à la clé suivante
    [InlineData(5, 0)]      // après la dernière clé : dernière valeur
    public void Rotate_IsSampledWithTheCurveOfTheStartingKey(float time, float expected)
    {
        Assert.Equal(expected, AnimationSampler.SampleRotate(Swing().Bones["upper"].Rotate!, time), 3);
    }

    [Fact]
    public void Smooth_EasesInAndOut()
    {
        var keys = Swing().Bones["upper"].Translate!;
        Assert.Equal(5, AnimationSampler.SampleVector(keys, 0.5f).X, 3);
        Assert.True(AnimationSampler.SampleVector(keys, 0.1f).X < 1); // linéaire donnerait 1
    }

    [Fact]
    public void Pose_ChainsBoneTransforms()
    {
        var pose = new SkeletonPose(Arm());
        var elbow = Vector2.Transform(Vector2.Zero, pose.World[2]);
        Assert.Equal(0, elbow.X, 3);
        Assert.Equal(10, elbow.Y, 3); // « upper » pointe vers le bas (90°, Y vers le bas)

        AnimationSampler.Apply(Swing(), pose, 0.5f);
        pose.UpdateWorld();
        // Rotation +90 (soit 180° : vers -X) et translation de (5, -5) au temps 0,5 (Smooth à mi-course).
        elbow = Vector2.Transform(Vector2.Zero, pose.World[2]);
        Assert.Equal(-5, elbow.X, 3);
        Assert.Equal(-5, elbow.Y, 3);
    }

    // ── Lecteur ───────────────────────────────────────────────────────────

    [Fact]
    public void Player_Loops_AndFiresEachEventOncePerCycle()
    {
        var player = new AnimationPlayer(Arm()) { MixDuration = 0 };
        var fired = new List<string>();
        player.EventFired += e => fired.Add(e.Name);
        player.Play(Swing(loop: true));

        for (int i = 0; i < 25; i++) player.Update(0.1f); // 2,5 cycles

        Assert.Equal(["start", "hit", "start", "hit", "start"], fired);
        Assert.Equal(0.5f, player.Time, 3);
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void Player_WithoutLoop_StopsAtTheEnd()
    {
        var player = new AnimationPlayer(Arm());
        player.Play(Swing());
        player.Update(3);
        Assert.True(player.IsFinished);
        Assert.Equal(1, player.Time);
    }

    [Fact]
    public void Player_CrossFades_FromThePreviousAnimation()
    {
        var player = new AnimationPlayer(Arm()) { MixDuration = 0.2f };
        player.Play(Swing());
        player.Seek(0.6f); // upper : +90 (Step)
        var still = new AnimationData { Id = "still", Skeleton = "arm", Duration = 1 };

        player.Play(still);
        player.Update(0.1f);
        Assert.Equal(90 + 45, player.Pose.Local[1].Rotation, 3); // à mi-fondu entre 180° et 90°

        player.Update(0.2f);
        Assert.Equal(90, player.Pose.Local[1].Rotation, 3);
    }

    // ── Données livrées ───────────────────────────────────────────────────

    [Fact]
    public void ShippedFiles_AreValid_AndTargetAnExistingSkeleton()
    {
        var skeletons = Files("*.skeleton.json").Select(AnimationCodec.DecodeSkeleton).ToDictionary(s => s.Id);
        Assert.Contains("humanoid", skeletons.Keys);

        foreach (var skin in Files("*.skin.json").Select(AnimationCodec.DecodeSkin))
            AnimationCodec.ValidateSkin(skin, skeletons[skin.Skeleton]);
        var animations = Files("*.anim.json").Select(AnimationCodec.DecodeAnimation).ToList();
        foreach (var a in animations)
            AnimationCodec.ValidateAnimation(a, skeletons[a.Skeleton]);

        Assert.Equal(["attack", "walk"], animations.Select(a => a.Id).Order());
    }

    [Fact]
    public void WarriorSkin_DrawsEachPartOnItsBone()
    {
        var root = Path.Combine(AssetsRoot, "humanoid");
        var skeleton = AnimationCodec.DecodeSkeleton(File.ReadAllText(Path.Combine(root, "humanoid.skeleton.json")));
        var skinDir = Path.Combine(root, "skins", "warrior");
        var skin = AnimationCodec.DecodeSkin(File.ReadAllText(Path.Combine(skinDir, "warrior.skin.json")));
        using var renderer = new SkinRenderer(skeleton, skin, p => File.OpenRead(Path.Combine(skinDir, p)));

        using var bitmap = new SKBitmap(200, 220);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(100, 210); // pieds en (100, 210)
            renderer.Draw(canvas, new SkeletonPose(skeleton));
        }

        // Au repos, chaque SVG retombe là où il a été dessiné : torse, tête, pieds au sol, rien au-dessus.
        Assert.NotEqual(0, bitmap.GetPixel(100, 210 - 120).Alpha);
        Assert.NotEqual(0, bitmap.GetPixel(100 + 2, 210 - 169).Alpha);
        Assert.NotEqual(0, bitmap.GetPixel(100 + 5, 210 - 2).Alpha);
        Assert.Equal(0, bitmap.GetPixel(100 - 50, 210 - 200).Alpha);
    }

    private static IEnumerable<string> Files(string pattern) =>
        Directory.EnumerateFiles(AssetsRoot, pattern, SearchOption.AllDirectories).Select(File.ReadAllText);

    private static string FindAssetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HeroesOfIdlestan.slnx")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("HeroesOfIdlestan.slnx introuvable au-dessus du dossier de test.");
        return Path.Combine(dir.FullName, "assets", "animation");
    }
}
