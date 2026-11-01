namespace HeroesOfIdlestan.Core.Model.Animation;

/// <summary>
/// Manifeste d'un jeu de planches de sprites (<c>*.sprites.json</c>, <c>"format": "hoi-spritesheet"</c>),
/// écrit par <c>HeroesOfIdlestan.Tool.SpriteSheet</c> à partir d'un modèle 3D animé. Toutes les
/// images de toutes les planches ont la même taille et le même pivot. Voir design/09-sprite-sheets.md.
/// </summary>
public sealed class SpriteSheetData
{
    /// <summary>Fichier du modèle 3D d'origine (« bake3d-robot-animated.glb »).</summary>
    public required string Model { get; init; }

    /// <summary>Taille d'une image, en pixels.</summary>
    public required int FrameWidth { get; init; }
    public required int FrameHeight { get; init; }

    /// <summary>Origine du modèle (ses pieds) dans une image, en pixels depuis le coin haut gauche.</summary>
    public required float PivotX { get; init; }
    public required float PivotY { get; init; }

    /// <summary>Pixels par unité du monde 3D d'origine.</summary>
    public required float PixelsPerUnit { get; init; }

    /// <summary>Réglages de la cuisson : rotation du modèle, élévation de la caméra, images par seconde.</summary>
    public float Yaw { get; init; }
    public float Pitch { get; init; }
    public float Fps { get; init; }

    public required List<SpriteClipData> Animations { get; init; }

    /// <summary>
    /// La planche de l'animation <paramref name="name"/> (sans tenir compte de la casse, comme les clips
    /// glTF), dans l'orientation la plus proche de <paramref name="direction"/> degrés ; null si absente.
    /// </summary>
    public SpriteClipData? Find(string name, float direction = 0)
    {
        SpriteClipData? best = null;
        float bestGap = float.MaxValue;
        foreach (var clip in Animations)
        {
            if (!string.Equals(clip.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            float gap = MathF.Abs(((clip.Direction - direction) % 360 + 540) % 360 - 180);
            if (gap < bestGap) (best, bestGap) = (clip, gap);
        }
        return best;
    }

    /// <summary>Vrai si le jeu a l'animation <paramref name="name"/>, dans au moins une orientation.</summary>
    public bool Has(string name) => Find(name) is not null;
}

/// <summary>Une planche : les images d'une animation dans une orientation, rangées en grille.</summary>
public sealed class SpriteClipData
{
    /// <summary>Nom du clip d'origine (« Idle », « Look around »).</summary>
    public required string Name { get; init; }

    /// <summary>Orientation en degrés : 0 = vers la droite, 90 = de dos, 180 = gauche, 270 = de face.</summary>
    public float Direction { get; init; }

    /// <summary>Fichier PNG, relatif au manifeste.</summary>
    public required string Image { get; init; }

    public required int Frames { get; init; }
    public required int Columns { get; init; }
    public required int Rows { get; init; }

    /// <summary>Durée d'affichage d'une image, en secondes.</summary>
    public required float FrameDuration { get; init; }

    /// <summary>Durée du clip, en secondes.</summary>
    public required float Duration { get; init; }

    /// <summary>
    /// Clip en boucle : sa dernière image (identique à la première) n'est pas dans la planche. Un clip
    /// simple a une image de plus, sa pose finale.
    /// </summary>
    public bool Loop { get; init; }

    /// <summary>
    /// L'image à montrer <paramref name="seconds"/> après le début du clip. En boucle si
    /// <paramref name="loop"/> (par défaut, selon <see cref="Loop"/>) ; sinon la dernière image est tenue.
    /// </summary>
    public int FrameAt(double seconds, bool? loop = null)
    {
        int index = (int)Math.Floor(Math.Max(0, seconds) / FrameDuration);
        return (loop ?? Loop) ? index % Frames : Math.Min(index, Frames - 1);
    }

    /// <summary>Vrai une fois le clip joué jusqu'au bout (pour un clip qu'on ne fait pas boucler).</summary>
    public bool IsFinished(double seconds) => seconds >= Duration;

    /// <summary>Coin haut gauche de l'image <paramref name="frame"/> dans la planche, en pixels.</summary>
    public (int X, int Y) FrameOrigin(int frame, SpriteSheetData sheet) =>
        (frame % Columns * sheet.FrameWidth, frame / Columns * sheet.FrameHeight);
}
