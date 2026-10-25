# 08 — Version Godot (rendu 3D)

Une seconde pile de rendu, en 3D sous Godot, à côté de la pile Skia + Avalonia. Elle ne s'appuie
que sur `Core.Model` et `Core.Controller` : la simulation, les sauvegardes et la localisation sont
les mêmes, seuls le rendu, l'overlay et le head changent. Le but est de pouvoir remplacer Skia en
entier ; en attendant, les deux piles coexistent.

```
 HeroesOfIdlestan.App.Godot          projet Godot (project.godot) : Main.cs, stockage, modèles 3D
            │
            ▼
 HeroesOfIdlestan.Core.UI.Godot      GameRuntimeHost, HudViewModel, HUD en contrôles Godot, boucle
            │
            ▼
 HeroesOfIdlestan.Core.Render.Godot  GameRuntime, scène 3D du combat, caméra, sons
            │
            ▼
 HeroesOfIdlestan.Core.Controller → HeroesOfIdlestan.Core.Model
```

Aucune référence entre les deux piles, dans un sens ou dans l'autre :
`InvariantTests.GodotAndSkiaStacks_DoNotReferenceEachOther` le vérifie. Le code commun aux deux
(snapshot du HUD, ViewModels, règles des bruitages, sauvegarde automatique) est donc recopié, pas
partagé : il disparaîtra avec la pile Skia.

## Lancer

Godot 4.7 en version **.NET** (`Godot_v4.7.2-stable_mono_win64`), même version que `Godot.NET.Sdk`
dans `HeroesOfIdlestan.App.Godot.csproj` et que `GodotSharp` dans les deux couches.

- **Éditeur** : ouvrir `HeroesOfIdlestan.App.Godot/project.godot`, puis Play (F5). L'éditeur compile
  le csproj et ses références lui-même.
- **Sans éditeur** : `dotnet build HeroesOfIdlestan.App.Godot`, puis
  `Godot_v4.7.2-stable_mono_win64.exe --path HeroesOfIdlestan.App.Godot`.

`dotnet build HeroesOfIdlestan.slnx` compile aussi la pile Godot (le SDK vient de NuGet, pas
besoin de Godot installé). Sans import préalable par Godot (`--import`, ou ouverture dans
l'éditeur), le modèle 3D est introuvable et les combattants s'affichent en capsules.

## Pièces

| Pièce | Projet | Rôle |
|---|---|---|
| `Main` | App.Godot | Seul script Godot : crée le runtime, l'hôte, la vue ; relaie `_Process`, `_UnhandledInput`, le focus |
| `GodotFileSystemService` | App.Godot | Sauvegardes dans `user://` (`%AppData%/Godot/app_userdata/HeroesOfIdlestan`), séparées du head Avalonia |
| `GameView` | UI.Godot | Scène 3D au fond, HUD dans un `CanvasLayer` ; tick + mise à jour de la scène à chaque frame, HUD sondé toutes les 100 ms |
| `HudView` | UI.Godot | Barre du haut construite en code ; recopie chaque propriété notifiée du ViewModel dans son contrôle |
| `GameRuntimeHost` | UI.Godot | Même contrat qu'avec Avalonia : tout accès au runtime passe par `Read` / `Invoke` |
| `GameRuntime` | Render.Godot | Contrôleur, scène, sons, sauvegarde auto ; `Render(delta)` met la scène à jour |
| `BattleScene` | Render.Godot | Décor, lumière, caméra, une `FighterView` par combattant ; lit l'état, ne le modifie jamais |
| `FighterView` | Render.Godot | Modèle animé, barre de vie, fente à chaque action, chute à la mort |
| `CameraRig` | Render.Godot | Orbite (glisser), zoom (molette, pincement), déplacement (bouton du milieu) |
| `GodotAudioService` | Render.Godot | Un `AudioStreamPlayer` polyphone par son ; WAV décodés par Godot |

**Pourquoi un seul script.** Godot n'attache à une scène que les scripts de l'assembly du projet
Godot. Les couches Render et UI ne dérivent donc d'aucun `Node` : ce sont des classes ordinaires
qui construisent leurs nœuds en code, comme l'overlay Avalonia est construit sans XAML. Elles ne
dépendent que du paquet `GodotSharp`, sans générateurs de source.

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
- **Bruitages.** Les WAV restent produits dans `HeroesOfIdlestan.Core.Render/Resources/sounds/`
  (voir 04) ; Core.Render.Godot les embarque par lien de fichier, sans référence de projet.
- **Plateformes.** Godot exporte aussi vers Android ; l'export web en C# n'est pas pris en charge par
  Godot 4. Les heads Browser et Android restent sur la pile Skia + Avalonia.
