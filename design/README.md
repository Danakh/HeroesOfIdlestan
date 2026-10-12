# HeroesOfIdlestan — design

HeroesOfIdlestan est un squelette de jeu 2D en C# (.NET 10) : simulation au tick, sauvegardes testées,
rendu SkiaSharp, interface Avalonia, sons, et trois plateformes (bureau, web, Android). Il ne
contient aucune règle de jeu : le contenu d'exemple (des producteurs qui rapportent de l'or)
n'existe que pour que tout tourne, se sauvegarde et produise des fixtures de test.

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

## Commandes

```bash
dotnet build HeroesOfIdlestan.slnx                      # Core, Render, UI, Desktop, AnimationTool, Browser, Tests
dotnet run --project HeroesOfIdlestan.Desktop
dotnet run --project HeroesOfIdlestan.Browser           # sert la page et ouvre le navigateur
dotnet publish HeroesOfIdlestan.Android -c Release      # APK (hors .slnx, workload android requis)
dotnet run --project HeroesOfIdlestan.AnimationTool    # banc d'essai des animations
dotnet test HeroesOfIdlestan.Tests
python assets/sounds/generate_sounds.py              # régénère les WAV
```

Régénérer les fixtures de sauvegarde (action volontaire, voir 03) :

```powershell
$env:BLANK_MANUAL_TESTS=1; dotnet test HeroesOfIdlestan.Tests --filter "FullyQualifiedName~Rebuild_Current_Saves"
```

## Arborescence

```
HeroesOfIdlestan.Core/       modèle + contrôleurs, aucune dépendance
HeroesOfIdlestan.Render/     rendu Skia, caméra, input, sons, GameRuntime
HeroesOfIdlestan.UI/         overlay Avalonia, GameRuntimeHost, boucle de frame
HeroesOfIdlestan.Desktop/    head bureau (+ NativeAudioService, partagé avec Android)
HeroesOfIdlestan.Browser/    head WebAssembly (+ wwwroot/)
HeroesOfIdlestan.Android/    head Android (hors .slnx)
HeroesOfIdlestan.AnimationTool/  banc d'essai des animations (Avalonia, bureau)
HeroesOfIdlestan.Tests/      tests xUnit + saves/ (fixtures)
assets/sounds/           générateur des bruitages
assets/animation/        squelettes, habillages SVG et animations (voir 07)
tools/New-Game.ps1       crée un nouveau jeu à partir du squelette
.github/workflows/       déploiement web, build APK
design/                  cette documentation
```
