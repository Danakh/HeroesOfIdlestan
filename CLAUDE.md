# CLAUDE.md

Squelette de jeu 2D en C# (.NET 10). Toute la documentation — architecture, règles, sauvegardes,
sons, plateformes, déploiement, et comment démarrer un jeu à partir du squelette — est dans
[`design/`](design/README.md). La lire avant de modifier le code.

## Nommage des projets

Détail et exemples : [`design/02-architecture.md`](design/02-architecture.md#nommage-des-projets).

- Tout ce qui lance le jeu : `HeroesOfIdlestan.App.<Plateforme>` (`App.Desktop`, `App.Browser`, `App.Android`,
  `App.Godot`).
- Tout ce qui teste : `HeroesOfIdlestan.Test.<Nom>` (`Test.Tests`).
- Outils de développement, qui ne lancent pas le jeu : `HeroesOfIdlestan.Tool.<Nom>` (`Tool.Animation`).
- Le cœur, une couche par projet, exactement quatre : `HeroesOfIdlestan.Core.Model`, `.Core.Controller`,
  `.Core.Render`, `.Core.UI`. Model ne dépend de rien ; Controller de Model ; Render de Controller ;
  UI de Render.
- Variante Godot (3D) de Render et UI : `HeroesOfIdlestan.Core.Render.Godot` (de Controller),
  `.Core.UI.Godot` (de Render.Godot), lancées par `App.Godot`. Aucune référence entre la pile Godot
  et la pile Skia/Avalonia ([`design/08-godot.md`](design/08-godot.md)).
- Dossier, `.csproj`, assembly et espace de noms racine portent le nom du projet ; un sous-dossier
  ajoute un segment d'espace de noms.

## Commandes

```bash
dotnet build HeroesOfIdlestan.slnx
dotnet run --project HeroesOfIdlestan.App.Desktop
dotnet run --project HeroesOfIdlestan.Tool.Animation   # banc d'essai des animations (design/07)
dotnet test HeroesOfIdlestan.Test.Tests
godot --path HeroesOfIdlestan.App.Godot                # version Godot 3D, Godot 4.7 .NET (design/08)
```
