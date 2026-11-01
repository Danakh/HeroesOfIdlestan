using System.Globalization;
using System.Text.Json;
using Godot;

namespace HeroesOfIdlestan.Tool.SpriteSheet;

/// <summary>
/// Cuit les animations d'un modèle 3D en planches de sprites 2D.
/// <para>
/// Le modèle est chargé à l'exécution (pas d'import Godot), posé dans un <see cref="SubViewport"/>
/// transparent et filmé par une caméra orthographique. Deux passes : la première filme large et
/// mesure la zone que le personnage occupe sur toutes les images de toutes les animations et
/// orientations ; la seconde cadre cette zone, identique pour toutes les planches, de sorte que le
/// pivot (l'origine du modèle, ses pieds) tombe au même pixel partout.
/// </para>
/// </summary>
public sealed class SpriteBaker
{
    public const string ManifestFormat = "hoi-spritesheet";
    public const int ManifestVersion = 1;

    /// <summary>Côté de l'image de la passe de mesure, en pixels.</summary>
    private const int MeasureResolution = 512;

    /// <summary>Largeur maximale d'une planche avant de passer à la ligne.</summary>
    private const int MaxSheetWidth = 8192;

    private readonly Node _host;
    private readonly BakeOptions _options;
    private readonly Action<string> _status;

    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private Node3D _pivot = null!;
    private AnimationPlayer _player = null!;

    public SpriteBaker(Node host, BakeOptions options, Action<string> status)
    {
        _host = host;
        _options = options;
        _status = status;
    }

    /// <summary>La vue filmée, pour l'afficher pendant la cuisson.</summary>
    public Texture2D Preview => _viewport.GetTexture();

    /// <summary>Monte la scène et charge le modèle. Lève si le modèle est illisible ou sans animation.</summary>
    public void Setup()
    {
        _viewport = new SubViewport
        {
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = _options.PixelArt ? Viewport.Msaa.Disabled : Viewport.Msaa.Msaa8X,
            Size = new Vector2I(MeasureResolution, MeasureResolution),
        };
        _host.AddChild(_viewport);

        _viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.6f,
            },
        });

        // La caméra et la lumière tournent ensemble autour du personnage (élévation) : l'éclairage
        // est le même sur toutes les orientations, puisque c'est le modèle qui pivote.
        var rig = new Node3D { RotationDegrees = new Vector3(-(float)_options.Pitch, 0, 0) };
        _viewport.AddChild(rig);
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Current = true,
        };
        rig.AddChild(_camera);
        rig.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-35, 30, 0),
            LightEnergy = 1.1f,
        });

        _pivot = new Node3D();
        _viewport.AddChild(_pivot);
        var model = LoadModel(_options.ModelPath);
        _pivot.AddChild(model);

        _player = FindAnimationPlayer(model)
                  ?? throw new InvalidOperationException($"{_options.ModelPath} : aucune animation.");
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;

        // La caméra vise l'axe vertical du modèle, au milieu de sa hauteur : le pivot (autour de Y)
        // le garde alors centré quelle que soit l'orientation.
        Aabb bounds = BoundsOf(model);
        float extent = Math.Max(bounds.Size.Length(), 0.01f);
        rig.Position = new Vector3(0, bounds.GetCenter().Y, 0);
        _camera.Position = new Vector3(0, 0, extent * 4);
        _camera.Near = extent * 0.01f;
        _camera.Far = extent * 8;
        _camera.Size = extent * 3; // passe de mesure : large, l'animation peut sortir du repos
    }

    /// <summary>Les deux passes et l'écriture des fichiers. Rend le code de sortie du processus.</summary>
    public async Task<int> RunAsync()
    {
        var clips = SelectClips();
        if (clips.Count == 0) return 1;

        // Passe 1 : la zone occupée, en unités du plan de la caméra (x à droite, y en haut).
        _status("Mesure…");
        float wpp = _camera.Size / MeasureResolution;
        Rect2? used = null;
        await ForEachFrame(clips, (image, _, _, _) =>
        {
            Rect2I r = image.GetUsedRect();
            if (r.Size.X == 0 || r.Size.Y == 0) return;
            // Un pixel de sécurité de chaque côté : la mesure est au pixel près de cette passe.
            var area = new Rect2(
                (r.Position.X - 1 - MeasureResolution / 2f) * wpp,
                (MeasureResolution / 2f - r.End.Y - 1) * wpp,
                (r.Size.X + 2) * wpp,
                (r.Size.Y + 2) * wpp);
            used = used is { } u ? u.Merge(area) : area;
        });
        if (used is not { } content)
        {
            GD.PrintErr("Le modèle n'apparaît sur aucune image (échelle, position ?).");
            return 1;
        }

        // Cadrage final : même taille d'image et même pivot pour toutes les planches.
        int margin = _options.Margin;
        double pixel = _options.PixelsPerUnit is { } ppu
            ? 1 / ppu
            : Math.Max(content.Size.X, content.Size.Y) / (_options.Size - 2 * margin);
        int width = (int)Math.Ceiling(content.Size.X / pixel) + 2 * margin;
        int height = (int)Math.Ceiling(content.Size.Y / pixel) + 2 * margin;
        Vector2 center = content.GetCenter();
        _camera.Size = (float)(height * pixel);
        _camera.HOffset = center.X;
        _camera.VOffset = center.Y;
        _viewport.Size = new Vector2I(width, height);
        await NextFrame();
        Vector2 pivot = _camera.UnprojectPosition(Vector3.Zero);

        // Passe 2 : les planches.
        Directory.CreateDirectory(_options.OutputDirectory);
        var entries = new List<ManifestAnimation>();
        var frames = new List<Image>();
        bool clipped = false;
        string? current = null;
        int total = clips.Count * _options.Directions, done = 0;

        await ForEachFrame(clips, (image, clip, direction, last) =>
        {
            string key = clip.Name + "/" + direction;
            if (current != key) { frames.Clear(); current = key; }

            Rect2I r = image.GetUsedRect();
            if (r.Size.X > 0 && (r.Position.X == 0 || r.Position.Y == 0 || r.End.X == width || r.End.Y == height))
                clipped = true;
            frames.Add(image);
            if (!last) return;

            entries.Add(WriteSheet(clip, direction, frames, width, height));
            _status($"Planches : {++done} / {total}");
        });

        WriteManifest(entries, width, height, pivot, pixel);
        if (clipped) GD.PushWarning("Une image touche le bord de son cadre : augmenter --margin.");
        GD.Print($"{entries.Count} planche(s) de {width}×{height} px, pivot ({pivot.X:0.##}, {pivot.Y:0.##}) → {_options.OutputDirectory}");
        return 0;
    }

    private sealed record Clip(string Name, Animation Animation, int Frames, double FrameDuration, bool Loop);

    /// <summary>Les clips demandés, avec leur nombre d'images. Signale ceux qui manquent.</summary>
    private List<Clip> SelectClips()
    {
        var names = _player.GetAnimationList().Where(n => n != "RESET").ToList();
        GD.Print($"Clips du modèle : {string.Join(", ", names)}");

        if (_options.Animations.Count > 0)
        {
            foreach (var missing in _options.Animations.Where(a => !names.Contains(a, StringComparer.OrdinalIgnoreCase)))
                GD.PrintErr($"Clip absent du modèle : {missing}");
            names = names.Where(n => _options.Animations.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        if (names.Count == 0) GD.PrintErr("Aucun clip à cuire.");

        return names.Select(name =>
        {
            var animation = _player.GetAnimation(name);
            bool loop = animation.LoopMode != Animation.LoopModeEnum.None
                        || _options.Loops.Contains(name, StringComparer.OrdinalIgnoreCase);
            double duration = animation.Length;
            int steps = Math.Max(1, (int)Math.Round(duration * _options.Fps));
            // Une boucle n'inclut pas sa dernière image, qui répète la première ; un clip simple, si :
            // la pose finale (mort) doit pouvoir être tenue.
            return loop
                ? new Clip(name, animation, steps, duration / steps, true)
                : new Clip(name, animation, steps + 1, duration / steps, false);
        }).ToList();
    }

    /// <summary>
    /// Pose et filme chaque image de chaque clip, pour chaque orientation, dans cet ordre.
    /// <c>last</c> marque la dernière image d'un clip dans une orientation.
    /// </summary>
    private async Task ForEachFrame(List<Clip> clips, Action<Image, Clip, int, bool> onFrame)
    {
        for (int direction = 0; direction < _options.Directions; direction++)
        {
            _pivot.RotationDegrees = new Vector3(0, (float)(_options.Yaw + direction * DirectionStep), 0);
            foreach (var clip in clips)
            {
                _player.Play(clip.Name);
                for (int i = 0; i < clip.Frames; i++)
                {
                    _player.Seek(Math.Min(i * clip.FrameDuration, clip.Animation.Length), update: true);
                    await NextFrame();
                    var image = _viewport.GetTexture().GetImage();
                    if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
                    onFrame(image, clip, direction, i == clip.Frames - 1);
                }
            }
        }
    }

    private double DirectionStep => 360.0 / _options.Directions;

    /// <summary>Attend que la pose posée soit appliquée au squelette, puis dessinée.</summary>
    private async Task NextFrame()
    {
        await _host.ToSignal(_host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await _host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private ManifestAnimation WriteSheet(Clip clip, int direction, List<Image> frames, int width, int height)
    {
        int columns = _options.Columns > 0 ? _options.Columns : frames.Count;
        columns = Math.Clamp(columns, 1, Math.Max(1, MaxSheetWidth / width));
        int rows = (frames.Count + columns - 1) / columns;

        var sheet = Image.CreateEmpty(columns * width, rows * height, false, Image.Format.Rgba8);
        for (int i = 0; i < frames.Count; i++)
            sheet.BlitRect(frames[i], new Rect2I(0, 0, width, height), new Vector2I(i % columns * width, i / columns * height));

        double angle = direction * DirectionStep;
        string file = FileName(clip.Name) + (_options.Directions > 1 ? "_" + ((int)Math.Round(angle)).ToString("000", CultureInfo.InvariantCulture) : "") + ".png";
        var error = sheet.SavePng(Path.Combine(_options.OutputDirectory, file));
        if (error != Error.Ok) throw new IOException($"{file} : écriture impossible ({error}).");

        return new ManifestAnimation(clip.Name, angle, file, frames.Count, columns, rows,
            Math.Round(clip.FrameDuration, 6), Math.Round(clip.Animation.Length, 6), clip.Loop);
    }

    private void WriteManifest(List<ManifestAnimation> animations, int width, int height, Vector2 pivot, double pixel)
    {
        var manifest = new Manifest(
            ManifestFormat, ManifestVersion,
            Path.GetFileName(_options.ModelPath),
            width, height,
            Math.Round(pivot.X, 2), Math.Round(pivot.Y, 2),
            Math.Round(1 / pixel, 4),
            _options.Yaw, _options.Pitch, _options.Fps,
            animations);
        string path = Path.Combine(_options.OutputDirectory,
            Path.GetFileNameWithoutExtension(_options.ModelPath) + ".sprites.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions) + "\n");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record Manifest(
        string Format, int Version, string Model,
        int FrameWidth, int FrameHeight, double PivotX, double PivotY, double PixelsPerUnit,
        double Yaw, double Pitch, double Fps,
        List<ManifestAnimation> Animations);

    private sealed record ManifestAnimation(
        string Name, double Direction, string Image, int Frames, int Columns, int Rows,
        double FrameDuration, double Duration, bool Loop);

    /// <summary>« Look around » → « look_around ».</summary>
    private static string FileName(string clip)
    {
        var chars = clip.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return new string(chars);
    }

    /// <summary>Charge un glTF/GLB ou un FBX à l'exécution, sans passer par l'import de Godot.</summary>
    private static Node3D LoadModel(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Modèle introuvable : {path}");

        bool fbx = Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase);
        GltfDocument document = fbx ? new FbxDocument() : new GltfDocument();
        GltfState state = fbx ? new FbxState() : new GltfState();
        var error = document.AppendFromFile(path, state);
        if (error != Error.Ok) throw new InvalidOperationException($"{path} : lecture impossible ({error}).");

        return document.GenerateScene(state) as Node3D
               ?? throw new InvalidOperationException($"{path} : scène vide.");
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;
        foreach (var child in node.GetChildren())
            if (FindAnimationPlayer(child) is { } found) return found;
        return null;
    }

    /// <summary>Boîte englobante de tous les maillages, dans le repère du modèle, au repos.</summary>
    private static Aabb BoundsOf(Node3D root)
    {
        Aabb? bounds = null;
        Visit(root);
        return bounds ?? new Aabb(Vector3.Zero, Vector3.One);

        void Visit(Node node)
        {
            if (node is VisualInstance3D visual)
            {
                Aabb box = visual.GlobalTransform * visual.GetAabb();
                bounds = bounds is { } b ? b.Merge(box) : box;
            }
            foreach (var child in node.GetChildren()) Visit(child);
        }
    }
}
