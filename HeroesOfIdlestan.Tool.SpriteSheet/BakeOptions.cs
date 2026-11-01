using System.Globalization;

namespace HeroesOfIdlestan.Tool.SpriteSheet;

/// <summary>
/// Réglages d'une cuisson, lus sur la ligne de commande (arguments donnés à l'outil après
/// <c>--</c>). Voir <see cref="Usage"/> et design/09-sprite-sheets.md.
/// </summary>
public sealed class BakeOptions
{
    public const string Usage = """
        Usage : dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- [modèle] [options]

          modèle              .glb, .gltf ou .fbx animé (défaut : le robot de App.Godot)
          --out <dossier>     dossier de sortie (défaut : assets/sprites/<modèle>/)
          --anims a,b,c       clips à cuire, sans tenir compte de la casse (défaut : tous sauf RESET)
          --loop a,b,c        clips en boucle, en plus de ceux que le fichier marque ainsi
                              (glTF n'en marque aucun) ; défaut : idle,walk,run
          --fps <n>           images par seconde (défaut : 12)
          --size <px>         plus grand côté d'une image, marge comprise (défaut : 128)
          --ppu <px>          pixels par unité de monde, à la place de --size : même échelle
                              pour tous les modèles cuits avec la même valeur
          --margin <px>       marge vide autour du personnage (défaut : 2)
          --directions <n>    nombre d'orientations, réparties sur 360° (défaut : 1)
          --yaw <deg>         rotation du modèle ; 90 = regard vers la droite (défaut : 90)
          --pitch <deg>       élévation de la caméra ; 0 = profil, 30 = vue de trois quarts (défaut : 0)
          --columns <n>       images par ligne dans une planche (défaut : toutes sur une ligne)
          --pixel             sans anticrénelage, pour du pixel art
          --help              cette aide
        """;

    public string ModelPath { get; private set; } = "";
    public string OutputDirectory { get; private set; } = "";
    public IReadOnlyList<string> Animations { get; private set; } = [];
    public IReadOnlyList<string> Loops { get; private set; } = ["idle", "walk", "run"];
    public double Fps { get; private set; } = 12;
    public int Size { get; private set; } = 128;
    public double? PixelsPerUnit { get; private set; }
    public int Margin { get; private set; } = 2;
    public int Directions { get; private set; } = 1;
    public double Yaw { get; private set; } = 90;
    public double Pitch { get; private set; }
    public int Columns { get; private set; }
    public bool PixelArt { get; private set; }
    public bool Help { get; private set; }

    /// <summary>
    /// Lit les arguments. <paramref name="repositoryRoot"/> sert aux valeurs par défaut (modèle et
    /// dossier de sortie) ; les chemins donnés se lisent depuis <paramref name="currentDirectory"/>.
    /// Lève <see cref="ArgumentException"/> sur un argument invalide.
    /// </summary>
    public static BakeOptions Parse(IReadOnlyList<string> args, string repositoryRoot, string currentDirectory)
    {
        var options = new BakeOptions();
        string? model = null, output = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string Value() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"{arg} attend une valeur.");

            switch (arg)
            {
                case "--help" or "-h": options.Help = true; break;
                case "--out": output = Value(); break;
                case "--anims":
                    options.Animations = Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--loop":
                    options.Loops = Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--fps": options.Fps = Positive(arg, Number(arg, Value())); break;
                case "--size": options.Size = (int)Positive(arg, Integer(arg, Value())); break;
                case "--ppu": options.PixelsPerUnit = Positive(arg, Number(arg, Value())); break;
                case "--margin": options.Margin = Math.Max(0, Integer(arg, Value())); break;
                case "--directions": options.Directions = (int)Positive(arg, Integer(arg, Value())); break;
                case "--yaw": options.Yaw = Number(arg, Value()); break;
                case "--pitch": options.Pitch = Math.Clamp(Number(arg, Value()), -89, 89); break;
                case "--columns": options.Columns = Math.Max(0, Integer(arg, Value())); break;
                case "--pixel": options.PixelArt = true; break;
                default:
                    if (arg.StartsWith('-')) throw new ArgumentException($"Option inconnue : {arg}");
                    if (model is not null) throw new ArgumentException($"Un seul modèle à la fois ({model}, {arg}).");
                    model = arg;
                    break;
            }
        }

        if (options.Size <= 2 * options.Margin) throw new ArgumentException("--size doit dépasser deux fois --margin.");

        options.ModelPath = model is null
            ? Path.Combine(repositoryRoot, "HeroesOfIdlestan.App.Godot", "assets", "characters", "bake3d-robot-animated.glb")
            : Path.GetFullPath(model, currentDirectory);
        options.OutputDirectory = output is null
            ? Path.Combine(repositoryRoot, "assets", "sprites", Path.GetFileNameWithoutExtension(options.ModelPath))
            : Path.GetFullPath(output, currentDirectory);
        return options;
    }

    private static double Number(string option, string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new ArgumentException($"{option} : nombre attendu, pas « {text} ».");

    private static int Integer(string option, string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new ArgumentException($"{option} : entier attendu, pas « {text} ».");

    private static double Positive(string option, double value) =>
        value > 0 ? value : throw new ArgumentException($"{option} doit être positif.");
}
