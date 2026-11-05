# HeroesOfIdlestan — design

HeroesOfIdlestan est un idle / autobattler 2D en C# (.NET 10), bâti sur un squelette : simulation au
tick, sauvegardes testées, rendu SkiaSharp, interface Avalonia, sons, et trois plateformes (bureau,
web, Android). Les règles du jeu sont dans le [GDD](Gamedesign.md) ; la version actuelle en implémente
la boucle minimale : un guerrier contre un rat (un monstre de plus toutes les 10 zones : rats en
ligne 1, gobelins en ligne 2 qui se battent comme des voleurs), combat → repos → zone suivante, recul et arrêt de
l'avance automatique en cas de défaite ; le recrutement de guerriers, voleurs (corps à corps en
ligne 1, couteaux à portée 2 en ligne 2) et mages (sort mono-cible portée 2-3 en lignes 2 et 3) ;
et la réorganisation de la formation par glisser-déposer (voir
[02 — Architecture](02-architecture.md#réorganiser-la-formation)).

Il est extrait de l'architecture de SettlersOfIdlestan.

## Sommaire

| Document | Pour |
|---|---|
| [01 — Démarrer un jeu](01-demarrer-un-jeu.md) | Créer un nouveau jeu à partir du squelette, étape par étape |
| [02 — Architecture](02-architecture.md) | Les couches, le store d'état, la simulation, les règles à respecter |
| [03 — Sauvegardes et tests](03-sauvegardes-et-tests.md) | Le format, les migrations, les cinq niveaux de tests de sauvegarde |
| [04 — Sons](04-sons.md) | Ajouter un bruitage, la chaîne audio par plateforme |
| [05 — Plateformes](05-plateformes.md) | Les heads Desktop, Browser et Android, et ce que chacun fournit |
| [06 — Déploiement web](06-deploiement-web.md) | Publier la version web sur GitHub Pages |
| [07 — Animation](07-animation.md) | Squelettes, habillages SVG, format des animations, outil de test |
| [08 — Version Godot](08-godot.md) | La pile de rendu 3D sous Godot, à côté de Skia + Avalonia |
| [09 — Planches de sprites](09-sprite-sheets.md) | Tirer des sprites 2D d'un modèle 3D animé |
| [Game design](Gamedesign.md) | Les règles du jeu : formation, combat, classes, progression |

## Commandes

```bash
dotnet build HeroesOfIdlestan.slnx                          # Core.*, App.Desktop, App.Browser, App.Godot, Tool.*, Test.Tests
dotnet run --project HeroesOfIdlestan.App.Desktop
dotnet run --project HeroesOfIdlestan.App.Browser           # sert la page et ouvre le navigateur
dotnet publish HeroesOfIdlestan.App.Android -c Release      # APK (hors .slnx, workload android requis)
dotnet run --project HeroesOfIdlestan.Tool.Animation        # banc d'essai des animations
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- <modèle.glb>  # planches de sprites (voir 09)
dotnet run --project HeroesOfIdlestan.App.Godot            # version Godot 3D (Godot 4.7 .NET, voir 08)
dotnet test HeroesOfIdlestan.Test.Tests
python assets/sounds/generate_sounds.py                     # régénère les WAV
```

Régénérer les fixtures de sauvegarde (action volontaire, voir 03) :

```powershell
$env:GAME_MANUAL_TESTS=1; dotnet test HeroesOfIdlestan.Test.Tests --filter "FullyQualifiedName~Rebuild_Current_Saves"
```

## Arborescence

Nommage des projets (`App.*` lance le jeu, `Core.*` en est le cœur, `Test.*` le teste, `Tool.*`
outille son développement) : voir [02 — Architecture](02-architecture.md#nommage-des-projets).

```
HeroesOfIdlestan.Core.Model/        état persisté, entités, sauvegarde, localisation, données d'animation — aucune dépendance
HeroesOfIdlestan.Core.Controller/   GameController, simulation, systèmes, modificateurs, succès, stores, lecture des animations
HeroesOfIdlestan.Core.Presentation/ GameSession, sons (règles + WAV), snapshots, hôte, ViewModels — commun aux deux piles
HeroesOfIdlestan.Core.Render/       rendu Skia, caméra, input, GameRuntime
HeroesOfIdlestan.Core.UI/           overlay Avalonia, GameRuntimeHost, boucle de frame
HeroesOfIdlestan.Core.Render.Godot/ rendu 3D Godot, caméra, sortie audio, GameRuntime (voir 08)
HeroesOfIdlestan.Core.UI.Godot/     liaison du HUD, GameRuntimeHost, boucle de frame
HeroesOfIdlestan.App.Desktop/       head bureau (+ NativeAudioService, partagé avec Android)
HeroesOfIdlestan.App.Browser/       head WebAssembly (+ wwwroot/)
HeroesOfIdlestan.App.Android/       head Android (hors .slnx)
HeroesOfIdlestan.App.Godot/         head Godot 3D : project.godot, Main.cs, ui/ (scènes d'UI), assets/characters/
HeroesOfIdlestan.Tool.Animation/    banc d'essai des animations (Avalonia, bureau)
HeroesOfIdlestan.Tool.SpriteSheet/  planches de sprites 2D tirées d'un modèle 3D animé (projet Godot, voir 09)
HeroesOfIdlestan.Test.Tests/        tests xUnit + Saves/ (fixtures)
assets/sounds/                      générateur des bruitages
assets/animation/                   squelettes, habillages SVG et animations (voir 07)
assets/sprites/                     sortie par défaut de Tool.SpriteSheet (voir 09)
tools/New-Game.ps1                  crée un nouveau jeu à partir du squelette
.github/workflows/                  déploiement web, build APK
design/                             cette documentation
```
