# CLAUDE.md

Squelette de jeu 2D en C# (.NET 10). Toute la documentation — architecture, règles, sauvegardes,
sons, plateformes, déploiement, et comment démarrer un jeu à partir du squelette — est dans
[`design/`](design/README.md). La lire avant de modifier le code.

**Deux piles maintenues en parallèle** : Skia + Avalonia (`Core.Render`, `Core.UI`) et Godot
(`Core.Render.Godot`, `Core.UI.Godot`, scènes d'UI dans `App.Godot/ui/`). Ce qui ne dépend d'aucun
moteur (partie, sauvegarde, commandes, sons, snapshots, ViewModels) va dans `Core.Presentation`, une
seule fois. Toute évolution du rendu ou des vues se fait sur **les deux** piles dans le même
changement, sauf mention contraire.

## Nommage des projets

Détail et exemples : [`design/02-architecture.md`](design/02-architecture.md#nommage-des-projets).

- Tout ce qui lance le jeu : `HeroesOfIdlestan.App.<Plateforme>` (`App.Desktop`, `App.Browser`, `App.Android`,
  `App.Godot`).
- Tout ce qui teste : `HeroesOfIdlestan.Test.<Nom>` (`Test.Tests`).
- Outils de développement, qui ne lancent pas le jeu : `HeroesOfIdlestan.Tool.<Nom>` (`Tool.Animation`, `Tool.SpriteSheet`).
- Le cœur, une couche par projet, exactement cinq : `HeroesOfIdlestan.Core.Model`, `.Core.Controller`,
  `.Core.Presentation`, `.Core.Render`, `.Core.UI`. Model ne dépend de rien ; Controller de Model ;
  Presentation de Controller (et d'aucun moteur) ; Render de Presentation ; UI de Render.
- Variante Godot (3D) de Render et UI : `HeroesOfIdlestan.Core.Render.Godot` (de Presentation),
  `.Core.UI.Godot` (de Render.Godot), lancées par `App.Godot`. Aucune référence entre la pile Godot
  et la pile Skia/Avalonia ([`design/08-godot.md`](design/08-godot.md)).
- Dossier, `.csproj`, assembly et espace de noms racine portent le nom du projet ; un sous-dossier
  ajoute un segment d'espace de noms.

## Commandes

```bash
dotnet build HeroesOfIdlestan.slnx
dotnet run --project HeroesOfIdlestan.App.Desktop
dotnet run --project HeroesOfIdlestan.Tool.Animation   # banc d'essai des animations (design/07)
dotnet run --project HeroesOfIdlestan.Tool.SpriteSheet -- <modèle.glb>  # sprites 2D d'un modèle 3D (design/09)
dotnet test HeroesOfIdlestan.Test.Tests
dotnet run --project HeroesOfIdlestan.App.Godot        # version Godot 3D, Godot 4.7 .NET (design/08)
```
