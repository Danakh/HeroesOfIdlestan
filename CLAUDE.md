# CLAUDE.md

Squelette de jeu 2D en C# (.NET 10). Toute la documentation — architecture, règles, sauvegardes,
sons, plateformes, déploiement, et comment démarrer un jeu à partir du squelette — est dans
[`design/`](design/README.md). La lire avant de modifier le code.

## Nommage des projets

Détail et exemples : [`design/02-architecture.md`](design/02-architecture.md#nommage-des-projets).

- Tout ce qui lance le jeu : `HeroesOfIdlestan.App.<Plateforme>` (`App.Desktop`, `App.Browser`, `App.Android`).
- Tout ce qui teste : `HeroesOfIdlestan.Test.<Nom>` (`Test.Tests`).
- Outils de développement, qui ne lancent pas le jeu : `HeroesOfIdlestan.Tool.<Nom>` (`Tool.Animation`).
- Le cœur, une couche par projet, exactement quatre : `HeroesOfIdlestan.Core.Model`, `.Core.Controller`,
  `.Core.Render`, `.Core.UI`. Model ne dépend de rien ; Controller de Model ; Render de Controller ;
  UI de Render.
- Dossier, `.csproj`, assembly et espace de noms racine portent le nom du projet ; un sous-dossier
  ajoute un segment d'espace de noms.

## Commandes

```bash
dotnet build HeroesOfIdlestan.slnx
dotnet run --project HeroesOfIdlestan.App.Desktop
dotnet run --project HeroesOfIdlestan.Tool.Animation   # banc d'essai des animations (design/07)
dotnet test HeroesOfIdlestan.Test.Tests
```
