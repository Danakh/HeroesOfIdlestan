# 01 — Démarrer un jeu à partir du squelette

Un nouveau jeu se crée en trois temps : copier le squelette sous son nom, remplacer le contenu
d'exemple par le premier vrai système, puis fixer les identifiants de plateforme avant toute
publication.

## 1. Copier le squelette

```powershell
.\tools\New-Game.ps1 -Name MyGame -Destination C:\DEV
```

Le script :

1. copie le dépôt sans `bin/`, `obj/`, `.vs/` ni `.git/` ;
2. remplace `HeroesOfIdlestan` par `MyGame` et `heroesofidlestan` par `mygame` (noms de fichiers, dossiers,
   namespaces, identifiant Android, préfixe localStorage) ;
3. régénère les fixtures de sauvegarde (la clé de brouillage contient le nom du jeu) ;
4. compile la solution et lance les tests.

Ensuite :

```powershell
cd C:\DEV\MyGame
git init; git add .; git commit -m "Squelette MyGame"
dotnet run --project MyGame.Desktop
```

Le jeu doit tourner : un guerrier (G) qui enchaîne les rats (R), zone après zone.

## 2. Fixer ce qui ne pourra plus changer

À faire **avant la première version publiée** : après, chaque changement fait perdre leur partie
aux joueurs ou empêche les mises à jour.

| Quoi | Où | Pourquoi |
|---|---|---|
| Clé de brouillage des sauvegardes | `Core.Model/Persistence/SaveCodec.cs` (`Key`) | La changer rend toutes les sauvegardes illisibles |
| Préfixe du stockage web | `App.Browser/Services/BrowserServices.cs` (`Prefix`) | Les parties web y sont rangées |
| Identifiant Android | `App.Android/*.csproj` (`ApplicationId`) | Android refuse une mise à jour d'un autre identifiant |
| Nom de la sauvegarde | `Core.Presentation/Runtime/GameSession.cs` (`AutosaveFileName`) | Les fichiers existants ne seraient plus trouvés |
| Dossier de données bureau | `App.Desktop/DesktopFileSystemService.cs` | Idem |

Changer la clé de brouillage impose de régénérer les fixtures `current` (voir 03).

## 3. Remplacer le contenu d'exemple

Dans HeroesOfIdlestan, c'est fait : le contenu d'exemple du squelette (des producteurs qui
rapportaient de l'or) a laissé place à la boucle courte du [GDD](Gamedesign.md) — un guerrier contre
un rat, combat → repos → zone suivante. Le jeu se trouve ici :

| Fichier | Contenu |
|---|---|
| `Core.Model/Entities/Entity.cs` | `HeroEntity`, `MonsterEntity`, classes et espèces |
| `Core.Model/State/RunState.cs` | Zone, phase (combat ou repos), avance automatique |
| `Core.Controller/Content/GameContent.cs` | Statistiques, actions par ligne, récompenses, XP, succès |
| `Core.Controller/Combat/Formation.cs` | Positions en demi-cases, distances, ciblage (GDD §3.4, §4.3, §4.4) |
| `Core.Controller/Combat/CombatSystem.cs` | La boucle courte, simulée par événements |
| `Core.Controller/GameController.cs` | `NewGame` (état initial), `NewDebugGame` (15 contre 15), `SetAutoAdvance` |
| `Core.Render/Renderers/BattleRenderers.cs` | Le champ de bataille et les combattants (Skia) |
| `Core.Render.Godot/Battle/` | Le champ de bataille et les combattants (Godot 3D) |
| `Core.Presentation/Runtime/Snapshots.cs` + `Core.Presentation/ViewModels/HudViewModel.cs` | Le HUD : données et libellés, communs aux deux piles |
| `Core.UI/Views/HudView.cs` + `App.Godot/ui/hud.tscn` | Le HUD : vue Avalonia, scène Godot |
| `Core.Presentation/ViewModels/MenuViewModel.cs` + `Core.UI/Views/MenuView.cs` + `App.Godot/ui/menu.tscn` | Le menu principal (continuer, nouvelle partie, debug) |
| `Core.Presentation/Audio/GameAudioService.cs` | `OnGameEvent` : la table événement → son |
| `Test.Tests/TestUtilities/Scenarios.cs` | `GameScenarios` |

Pour un autre jeu créé depuis ce dépôt, ces fichiers sont ceux à remplacer.

Ordre conseillé, pour garder les tests verts à chaque étape :

1. **Modèle** : écrire les nouvelles classes d'état dans `GameState`, les entités dans `Entity.cs` et
   les enregistrer dans `EntityRegistry`.
2. **Systèmes** : un `ISimulationSystem` par mécanique, ajouté dans `GameController.Initialize`.
   Mettre à jour `InvariantTests.SimulationPipeline_HasTheDeclaredOrder`.
3. **Commandes** : des méthodes sur `GameController`, exposées par `GameSession`, appelées par les
   ViewModels via `IGameHost.Invoke`.
4. **Rendu**, sur les deux piles : des `IGameRenderer` ajoutés au pipeline de `GameScreen` (Skia),
   des vues 3D dans `BattleScene` (Godot).
5. **UI** : un snapshot (record) par panneau et un ViewModel qui le reçoit, dans Presentation ;
   puis une vue par pile qui s'y lie (Avalonia en code, scène Godot dans `App.Godot/ui/`).
6. **Scénarios** : réécrire `GameScenarios` avec les jalons du vrai jeu (premier bâtiment,
   première ressource avancée, premier prestige…), puis régénérer les fixtures.
7. Supprimer le contenu d'exemple devenu inutile.

## 4. Les gestes du quotidien

| Je veux… | Je fais… |
|---|---|
| Ajouter une quantité affichée au joueur | Un champ `ObfInt`/`ObfLong` dans l'état (anti-triche) |
| Ajouter un enum persisté | `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` — un test le vérifie |
| Ajouter un type d'entité | Une valeur d'`EntityKind`, une classe, une ligne dans `EntityRegistry` |
| Renommer ou déplacer un champ persisté | Une `ISaveMigration` dans `SaveService.DefaultMigrations` (voir 03) |
| Un effet qui dépend du temps | `TickCooldown.ConsumeElapsedCycles`, jamais un `if` sur l'écart |
| Un tirage aléatoire | `state.Prng` (gameplay) ou `state.WorldPrng` (génération), jamais `System.Random` |
| Un bonus (recherche, objet…) | Un `IModifierProvider` enregistré dans `ModifierAggregator` |
| Un texte | Une clé dans `fr.json` **et** `en.json` — un test le vérifie |
| Un son | Voir 04 |
| Sortir une version | Bumper `GameVersion.Current`, régénérer `current`, copier `current` en `release-X.Y` (voir 03), taguer `vX.Y.Z` (voir 06) |

## 5. Un CLAUDE.md pour le nouveau jeu

Le `CLAUDE.md` du squelette ne contient que les commandes et un renvoi vers `design/`. Au fil du
projet, y ajouter les règles propres au jeu (comment ajouter un bâtiment, une recherche…), comme
le fait SettlersOfIdlestan.
