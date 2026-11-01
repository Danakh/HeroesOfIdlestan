# 09 — Planches de sprites depuis un modèle 3D

`HeroesOfIdlestan.Tool.SpriteSheet` filme un modèle 3D animé et en tire des planches de sprites
2D : une image PNG par animation (et par orientation), plus un manifeste JSON qui décrit le
découpage. On anime donc une seule fois, en 3D, et l'on obtient des sprites pour une pile 2D.

```bash
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet                       # le robot de App.Godot
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- perso.glb --out sprites/perso
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- perso.glb --anims idle,walk --directions 8 --pitch 30
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- --help
```

C'est un projet Godot autonome (Godot 4.7 .NET, le même exécutable que pour
[App.Godot](08-godot.md#lancer)) : `dotnet run` compile puis lance Godot sur le projet. Une petite
fenêtre montre la cuisson, puis se ferme toute seule. Le code de sortie vaut 0 en cas de succès, 1
si la cuisson échoue, 2 si un argument est invalide. L'outil ne référence aucun projet du Core.

## Options

| Option | Défaut | Effet |
|---|---|---|
| *modèle* | robot de `App.Godot/assets/characters/` | `.glb`, `.gltf` ou `.fbx` animé, chargé tel quel (pas d'import Godot) |
| `--out <dossier>` | `assets/sprites/<modèle>/` | Dossier de sortie, créé au besoin |
| `--anims a,b` | tous les clips sauf `RESET` | Clips à cuire, sans tenir compte de la casse ; un clip absent est signalé |
| `--loop a,b` | `idle,walk,run` | Clips en boucle, en plus de ceux que le fichier marque ainsi (glTF n'en marque aucun) |
| `--fps <n>` | 12 | Images par seconde |
| `--size <px>` | 128 | Plus grand côté d'une image, marge comprise |
| `--ppu <px>` | — | Pixels par unité de monde, à la place de `--size` |
| `--margin <px>` | 2 | Marge vide autour du personnage |
| `--directions <n>` | 1 | Nombre d'orientations, réparties sur 360° |
| `--yaw <deg>` | 90 | Rotation du modèle ; 90 = regard vers la droite |
| `--pitch <deg>` | 0 | Élévation de la caméra : 0 = profil, 30 = vue de trois quarts |
| `--columns <n>` | toutes sur une ligne | Images par ligne (on passe aussi à la ligne au-delà de 8192 px) |
| `--pixel` | — | Sans anticrénelage, pour du pixel art |

**Échelle.** Avec `--size`, chaque modèle remplit son cadre : un rat et un géant sortent à la même
taille. Pour garder entre les modèles le rapport de taille du monde 3D, cuire tous les modèles
avec le même `--ppu`.

## Fonctionnement

1. Le modèle est chargé à l'exécution (`GltfDocument`, ou `FbxDocument` pour un `.fbx`) dans un
   `SubViewport` à fond transparent, éclairé par une lumière directionnelle et une lumière ambiante.
   La caméra est orthographique : aucune perspective, la taille ne dépend pas de la profondeur.
2. **Mesure.** Une première passe filme large toutes les images de toutes les animations et
   orientations, puis relève la zone que le personnage occupe réellement.
3. **Cuisson.** La caméra cadre cette zone et la seconde passe filme les planches. Toutes les
   images ont **la même taille et le même pivot** : un sprite se dessine pivot sur la position du
   personnage, sans recalage entre deux animations.

Chaque image est posée par `AnimationPlayer.Seek`, qui ne dépend pas du temps réel : le résultat
est le même quelle que soit la vitesse de la machine.

**Images d'un clip.** Un clip en boucle de durée *d* donne `round(d × fps)` images ; la dernière
n'est pas filmée, puisqu'elle répète la première. Un clip qui ne boucle pas donne une image de
plus, sa pose finale : une mort peut ainsi se tenir sur sa dernière image.

**Orientations.** Le modèle tourne sur lui-même et la caméra ne bouge pas, si bien que l'éclairage
reste le même d'une orientation à l'autre. L'orientation *k* tourne le modèle de *k* × 360/*n*
degrés dans le sens antihoraire vu de dessus, à partir du regard vers la droite : 0 = droite,
90 = de dos, 180 = gauche, 270 = de face. Le fichier porte l'angle (`walk_045.png`) dès qu'il y a
plus d'une orientation. Avec une seule, le personnage regarde vers +X, comme le prévoit le repère
2D ([07](07-animation.md#repère)), et le miroir donne l'autre côté.

**Limites.** Un clip qui déplace la racine du modèle (marche « en avant » plutôt que sur place)
fait glisser le personnage dans son cadre et agrandit la zone commune : préférer des clips sur
place. Seul le premier `AnimationPlayer` du modèle est lu, comme dans `FighterView`.

## Sortie

```
assets/sprites/<modèle>/
  <modèle>.sprites.json
  idle.png  walk.png  look_around.png …        (une orientation)
  idle_000.png  idle_045.png …                 (plusieurs orientations)
```

Le nom de fichier est celui du clip en minuscules, chaque caractère hors lettres, chiffres, `-`
et `_` étant remplacé par `_` (`Look around` → `look_around.png`). Le manifeste garde le nom
d'origine : comme le [catalogue](07-animation.md#catalogue-et-secours), le jeu les comparera sans
tenir compte de la casse.

**Manifeste** (`"format": "hoi-spritesheet"`, `"version": 1`) :

```json
{
  "format": "hoi-spritesheet", "version": 1,
  "model": "bake3d-robot-animated.glb",
  "frameWidth": 83, "frameHeight": 129,        // taille d'une image, en px
  "pivotX": 41.04, "pivotY": 125.45,           // origine du modèle (ses pieds) dans une image
  "pixelsPerUnit": 63.18,
  "yaw": 90, "pitch": 0, "fps": 12,            // réglages de la cuisson
  "animations": [
    { "name": "Walk", "direction": 0, "image": "walk.png",
      "frames": 48, "columns": 48, "rows": 1,
      "frameDuration": 0.083333, "duration": 4, "loop": true }
  ]
}
```

L'image *i* d'une planche occupe le rectangle
`(i % columns × frameWidth, i / columns × frameHeight, frameWidth, frameHeight)` ; elle se montre
de `i × frameDuration` à `(i + 1) × frameDuration`.

`AnimationCodec.DecodeSpriteSheet` (`Core.Model`) lit le manifeste et le valide (grille, durées,
propriété inconnue refusée) ; `SpriteSheetData` en donne l'image à montrer à un instant
(`FrameAt`) et la planche la plus proche d'une orientation (`Find`). Faire évoluer le format : même
règle que pour les autres fichiers d'animation ([07](07-animation.md#fichiers)), et l'outil écrit
la nouvelle version.

## Dans le jeu (Skia)

La pile Skia dessine les combattants avec les planches du robot de la version Godot, produites par :

```bash
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- --anims "Idle,Walk,Intro,Look around" --loop "idle,walk,run,look around"
```

| Pièce | Projet | Rôle |
|---|---|---|
| `assets/sprites/bake3d-robot-animated/` | — | Planches et manifeste, versionnés |
| `SpriteSheet` | Render | Charge un jeu de planches **embarqué** dans l'assembly (`EmbeddedResource`, nom `sprites/<dossier>/<fichier>`) : bureau, web et Android l'ont sans accès au disque ; dessine une image, pivot à l'origine |
| `SpriteAnimator` | Render | Par combattant : fond en boucle, animation ponctuelle (intro, attaque, coup reçu) jusqu'au bout, mort tenue ou pose figée ; en ticks de jeu |
| `FighterRenderer` | Render | Sprites triés de haut en bas, teinte de camp, miroir des monstres et de la marche vers la gauche, chute à la renverse, corps d'un monstre tué gardé 1,2 s |
| `FighterAnimation`, `FighterCues` | Presentation | Choix du fond et détection des attaques et coups reçus, communs avec `FighterView` (Godot) |

Les animations passent par le même `AnimationResolver` que sous Godot : il manque au robot
l'attaque, le coup reçu et la mort, et le journal (bouton « Log ») le signale. Si les planches
manquent ou sont illisibles, l'erreur est consignée et les combattants redeviennent des ronds.

Les planches pèsent sur le téléchargement de la version web (1,6 Mo pour le robot, dans
`HeroesOfIdlestan.Core.Render.wasm`) : cuire seulement les clips joués, et réduire `--size` ou
`--fps` si besoin. Changer de modèle : cuire dans `assets/sprites/<modèle>/` et changer
`FighterRenderer.SpriteSheetId`. `SpriteSheetTests` vérifie que les planches livrées se lisent,
qu'elles sont embarquées et que l'écran de combat les dessine.
