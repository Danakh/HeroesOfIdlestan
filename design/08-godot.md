# 08 — Version Godot (rendu 3D)

Une seconde pile de rendu, en 3D sous Godot, à côté de la pile Skia + Avalonia. Les deux sont
**maintenues en parallèle** : une évolution du rendu, de l'overlay ou des sons se fait sur les deux
dans le même changement. Elle s'appuie sur `Core.Presentation`, comme la pile Skia : la partie, les
sauvegardes, la localisation, les bruitages, les snapshots et les ViewModels sont les mêmes ; seuls
le rendu, les vues et le head changent.

```
 HeroesOfIdlestan.App.Godot          projet Godot (project.godot) : Main.cs, stockage, scènes d'UI
            │                        (ui/), modèles 3D
            ▼
 HeroesOfIdlestan.Core.UI.Godot      GameRuntimeHost, GameView (boucle), HudView (liaison de ui/hud.tscn)
            │
            ▼
 HeroesOfIdlestan.Core.Render.Godot  GameRuntime (session + scène), scène 3D du combat, caméra, sortie audio
            │
            ▼
 HeroesOfIdlestan.Core.Presentation  GameSession, sons, snapshots, GameHost, HudViewModel (communs)
            │
            ▼
 HeroesOfIdlestan.Core.Controller → HeroesOfIdlestan.Core.Model
```

Aucune référence entre les deux piles, dans un sens ou dans l'autre :
`InvariantTests.GodotAndSkiaStacks_DoNotReferenceEachOther` le vérifie. Ce qu'elles partagent vit
dans `Core.Presentation`, qui ne connaît aucun moteur (voir [02](02-architecture.md)).

## Lancer

Godot 4.7 en version **.NET** (`Godot_v4.7.2-stable_mono_win64`), même version que `Godot.NET.Sdk`
dans `HeroesOfIdlestan.App.Godot.csproj` et que `GodotSharp` dans les deux couches.

- **Éditeur** : ouvrir `HeroesOfIdlestan.App.Godot/project.godot`, puis Play (F5). L'éditeur compile
  le csproj et ses références lui-même.
- **Sans éditeur** : `dotnet run --project HeroesOfIdlestan.App.Godot` (ou Démarrer dans l'IDE)
  compile, puis lance `C:\DEV\Godot_v4.7.2\Godot_v4.7.2-stable_mono_win64_console.exe` sur le projet
  (`RunCommand` du csproj). Ailleurs : variable d'environnement `GODOT`, ou
  `-p:GodotExecutable=<chemin>`. Les arguments après `--` vont à Godot
  (`dotnet run --project HeroesOfIdlestan.App.Godot -- --quit-after 120`).

`dotnet build HeroesOfIdlestan.slnx` compile aussi la pile Godot (le SDK vient de NuGet, pas
besoin de Godot installé). Sans import préalable par Godot (`--import`, ou ouverture dans
l'éditeur), le modèle 3D est introuvable et les combattants s'affichent en capsules.

## Pièces

| Pièce | Projet | Rôle |
|---|---|---|
| `Main` | App.Godot | Seul script Godot : crée le runtime, l'hôte, la vue ; relaie `_Process`, `_UnhandledInput`, le focus |
| `GodotFileSystemService` | App.Godot | Sauvegardes dans `user://` (`%AppData%/Godot/app_userdata/HeroesOfIdlestan`), séparées du head Avalonia |
| `ui/hud.tscn` | App.Godot | Mise en page du HUD, éditée dans Godot ; sans script |
| `GameView` | UI.Godot | Scène 3D au fond, HUD dans un `CanvasLayer` ; tick + mise à jour de la scène à chaque frame, HUD sondé toutes les 100 ms |
| `HudView` | UI.Godot | Instancie `ui/hud.tscn` et la relie au `HudViewModel` commun : chaque propriété notifiée est recopiée dans son contrôle |
| `GameRuntimeHost` | UI.Godot | Dérive du `GameHost` commun (verrou, `Read` / `Invoke`) ; ajoute `Render(delta)` et l'input Godot |
| `GameRuntime` | Render.Godot | La `GameSession` commune + la scène ; `Render(delta)` met la scène à jour |
| `BattleScene` | Render.Godot | Décor, lumière, caméra, une `FighterView` par combattant ; lit l'état, ne le modifie jamais |
| `FighterView` | Render.Godot | Modèle animé, barre de vie, fente à chaque action, chute à la mort |
| `CameraRig` | Render.Godot | Orbite (glisser), zoom (molette, pincement), déplacement (bouton du milieu) |
| `GodotAudioService` | Render.Godot | Un `AudioStreamPlayer` polyphone par son ; WAV décodés par Godot |

**Pourquoi un seul script.** Godot n'attache à une scène que les scripts de l'assembly du projet
Godot. Les couches Render et UI ne dérivent donc d'aucun `Node` : ce sont des classes ordinaires
qui construisent leurs nœuds en code ou instancient une scène du head. Elles ne dépendent que du
paquet `GodotSharp`, sans générateurs de source.

## Éditer l'UI dans Godot

La mise en page de l'UI se fait dans l'éditeur ; la logique reste dans les ViewModels communs.

- Les scènes d'UI vivent dans `HeroesOfIdlestan.App.Godot/ui/` (seul un chemin `res://` passe par
  l'import) et **ne portent aucun script**.
- Chaque nœud que le code doit lier porte un **nom unique** : clic droit sur le nœud, *Access as
  Unique Name* (`%Gold`, `%Pause`…). Sa place dans l'arbre est libre : on peut regrouper, imbriquer,
  ajouter des conteneurs ou du décor sans toucher au code.
- La vue de `Core.UI.Godot` (par exemple `HudView`) instancie la scène, retrouve ces nœuds et les
  relie au ViewModel. Un nœud manquant, renommé ou d'un autre type lève au démarrage avec son nom
  (`res://ui/hud.tscn : nœud %Gold (Label) absent…`), plutôt que de laisser un HUD muet.
- Les textes saisis dans l'éditeur servent d'aperçu : ils sont remplacés par les libellés localisés
  dès la première frame.

| Nœud de `ui/hud.tscn` | Type | Lié à |
|---|---|---|
| `%Gold`, `%Zone`, `%Hero`, `%Bank` | `Label` | `GoldLabel`, `ZoneLabel`, `HeroLabel`, `BankLabel` |
| `%AutoAdvance`, `%Pause`, `%Save` | `Button` | libellé + `ToggleAutoAdvance`, `TogglePause`, `Save` |
| `%Speed1`, `%Speed3`, `%Speed10` | `Button` | `SetSpeed(1 / 3 / 10)` |
| `%Sound` | `Button` | `SoundLabel`, visible si `SoundAvailable`, `ToggleSound` |

Un nouvel élément du HUD : la propriété dans `HudViewModel` (et le snapshot si besoin), le nœud dans
`hud.tscn`, une ligne de liaison dans `HudView` — et sa contrepartie dans le `HudView` Avalonia.

**Espaces de noms.** Dans `HeroesOfIdlestan.Core.Render.Godot`, le nom `Godot` désigne d'abord ce
namespace : un type de Godot qui en croise un de .NET s'écrit `global::Godot.Environment`, et celui
de .NET `System.Environment`. Le `using Godot;` en tête de fichier, lui, fonctionne normalement.

## Modèles et animations

Les modèles vivent dans le projet Godot (`HeroesOfIdlestan.App.Godot/assets/characters/`) : seul un
chemin `res://` passe par l'import de Godot. Le glTF est importé tel quel ; ses textures extraites
et les fichiers `.import` sont versionnés, le cache `.godot/` non.

Le robot de test (`bake3d-robot-animated.glb`, CC BY 4.0, voir `CREDITS.md`) sert aux deux camps :
taille réelle et teinte bleue pour l'équipe, réduit et teinté de rouge pour les monstres.

| Situation | Animation |
|---|---|
| Arrivée de l'équipe (nouvelle partie, chargement) | `Intro`, jouée jusqu'au bout |
| Combat | `Idle`, plus une fente vers l'adversaire à chaque action |
| Repos après une victoire, avance automatique | `Walk` |
| Repos après une victoire, sans avance | `Look around` |
| Mort | pose figée, chute à la renverse ; un monstre disparaît 1,2 s plus tard |

Tout suit le **temps de jeu** : la vitesse des animations vaut celle de l'horloge (x1, x3, x10) et
tombe à 0 en pause, comme la fente et la chute.

Le système d'animation 2D (squelettes JSON + SVG, voir 07) ne sert pas ici : en 3D, les animations
viennent du glTF et de l'`AnimationPlayer` de Godot. Pour un nouveau modèle, garder les noms
d'animation de `FighterView` ou y ajouter les siens.

## Contraintes

- **Version de .NET.** `Directory.Build.props` impose `net10.0`, et le csproj Godot le redit : sans
  `TargetFramework` explicite, l'éditeur en ajoute un en `net8.0` à l'ouverture, qui ne peut plus
  référencer le Core.
- **Bruitages.** Les WAV sont embarqués par `Core.Presentation` (voir 04) et chargés par le même
  `SoundBank` que sous Skia ; seule la sortie (`GodotAudioService`) est propre à Godot.
- **Plateformes.** Godot exporte aussi vers Android ; l'export web en C# n'est pas pris en charge par
  Godot 4. Les heads Browser et Android restent sur la pile Skia + Avalonia.
