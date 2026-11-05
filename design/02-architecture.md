# 02 — Architecture

Chaque couche ne dépend que de celle du dessous, et le Model ne dépend de rien : Model et Controller
se testent sans fenêtre ni GPU.

```
 HeroesOfIdlestan.App.Desktop / .App.Browser / .App.Android        HeroesOfIdlestan.App.Godot
                                    heads : point d'entrée            head Godot, scènes d'UI (res://ui/)
                                    + services de plateforme
 HeroesOfIdlestan.Tool.Animation    banc d'essai des animations
            │                                                                   │
            ▼                                                                   ▼
 HeroesOfIdlestan.Core.UI           overlay Avalonia, boucle       HeroesOfIdlestan.Core.UI.Godot
            │                                                                   │
            ▼                                                                   ▼
 HeroesOfIdlestan.Core.Render       rendu Skia, caméra, input,     HeroesOfIdlestan.Core.Render.Godot
            │                       dessin des habillages                       │
            └───────────────────────────────┬───────────────────────────────────┘
                                            ▼
 HeroesOfIdlestan.Core.Presentation GameSession (partie, sauvegarde auto, commandes), règles des
                                    bruitages, snapshots, hôte sérialisé, ViewModels — aucun moteur
            │
            ▼
 HeroesOfIdlestan.Core.Controller   GameController, simulation, systèmes, modificateurs, succès, stores,
                                    lecture des animations
            │
            ▼
 HeroesOfIdlestan.Core.Model        état persisté, entités, sauvegarde, localisation, données
                                    d'animation et pose — aucune dépendance
```

`HeroesOfIdlestan.Test.Tests` référence Model, Controller, Presentation et Render.

**Deux piles d'affichage, maintenues en parallèle** : Skia + Avalonia (`Core.Render`, `Core.UI`,
heads Desktop, Browser, Android) et Godot 3D (`Core.Render.Godot`, `Core.UI.Godot`, `App.Godot`,
voir [08 — Version Godot](08-godot.md)). Elles ne se référencent jamais l'une l'autre et ne
s'appuient que sur `Core.Presentation`. Une évolution se fait sur les deux dans le même changement.

**Presentation ou pile ?** Ce qui ne dépend pas d'un moteur va dans Presentation, une seule fois :
la partie et sa sauvegarde, les commandes du joueur, la table événement → son, les snapshots, les
ViewModels et leurs libellés. Une pile ne garde que ce qui dessine, ce qui lit l'input de son moteur
et les vues (Avalonia en code, scènes Godot). Une ligne recopiée à l'identique dans les deux piles
signale du code à remonter dans Presentation. `InvariantTests.PresentationLayer_DependsOnNoEngine`
y interdit tout paquet Skia, Avalonia ou Godot.

**Model ou Controller ?** Le Model décrit l'état et sait le persister ; il ne le fait jamais avancer.
Tout ce qui modifie l'état selon les règles du jeu (systèmes du pipeline, commandes, agrégats
runtime reconstruits au chargement, lecture d'une animation) va dans le Controller. Un type du Model
qui aurait besoin d'un type du Controller signale un découpage à revoir : la référence inverse
n'existe pas.

## Nommage des projets

Le nom d'un projet dit son rôle ; le préfixe est le nom du jeu.

| Rôle | Nom | Projets |
|---|---|---|
| Lance le jeu (head, point d'entrée) | `<Jeu>.App.<Plateforme>` | `HeroesOfIdlestan.App.Desktop`, `.App.Browser`, `.App.Android`, `.App.Godot` |
| Cœur du jeu, une couche par projet | `<Jeu>.Core.<Couche>` | `HeroesOfIdlestan.Core.Model`, `.Core.Controller`, `.Core.Presentation`, `.Core.Render`, `.Core.UI` |
| Variante d'une couche pour un autre moteur | `<Jeu>.Core.<Couche>.<Moteur>` | `HeroesOfIdlestan.Core.Render.Godot`, `.Core.UI.Godot` |
| Teste | `<Jeu>.Test.<Nom>` | `HeroesOfIdlestan.Test.Tests` |
| Outil de développement (ne lance pas le jeu) | `<Jeu>.Tool.<Nom>` | `HeroesOfIdlestan.Tool.Animation`, `.Tool.SpriteSheet` |

- Le dossier, le `.csproj` et l'assembly portent le nom du projet ; l'espace de noms racine aussi
  (`RootNamespace`), et un sous-dossier ajoute un segment (`HeroesOfIdlestan.Core.Model/State/` →
  `HeroesOfIdlestan.Core.Model.State`).
- Une nouvelle plateforme est un `App.*`, un nouveau projet de tests un `Test.*` (par exemple
  `HeroesOfIdlestan.Test.Benchmarks`), un nouvel outil un `Tool.*`. Le Core garde ses cinq
  couches : du code qui ne rentre dans aucune signale d'abord une question d'architecture. Seules
  Render et UI ont une variante par moteur (`.Godot`), qui double la couche d'origine sans en
  dépendre.
- `tools/New-Game.ps1` remplace `HeroesOfIdlestan` par le nom du jeu : la convention se transmet
  telle quelle aux jeux créés depuis ce dépôt.

## Store d'état

L'état vit dans une seule racine sérialisable, `GameState`, lue et modifiée uniquement à travers
un hôte qui pose un verrou.

| Pièce | Rôle |
|---|---|
| `GameState` | Racine unique : tout ce qui est persisté, rien d'autre |
| `GameController` | Possède l'état, construit le pipeline et les agrégats runtime, expose les commandes |
| `GameSession` | Presentation : contrôleur + sons + sauvegarde auto + commandes + snapshots |
| `GameRuntime` | Une par pile : la session + ce qui dessine (écran Skia, scène Godot) ; construit par chaque head |
| `GameHost<T>.Read` / `Invoke` | Seule porte d'entrée vers le runtime, sous verrou ; chaque pile en dérive son `GameRuntimeHost` (rendu, input) |
| `IGameHost` | Ce que voient les ViewModels : la session, sous le même verrou, sans rien savoir de la pile |
| Snapshots (`record`) | Vue immuable pour l'UI, construite en une seule prise de verrou |
| Synchro par sondage | L'UI relit les snapshots toutes les 100 ms, découplée des 60 fps |

**Pourquoi un verrou.** Avalonia dessine sur son thread de rendu ; la boucle de jeu et l'input vivent
sur le thread UI. Sous Godot tout vit sur le thread principal, mais le contrat reste le même. Le modèle reste mono-thread par conception ; c'est l'accès qui est sérialisé.
Un ViewModel qui lit le runtime sans passer par `Read` réintroduit la course (« Collection was
modified » dans un renderer, au mieux).

**Pourquoi des snapshots.** Lire dix propriétés une à une ferait dix prises de verrou et donnerait
une vue incohérente si un tick s'intercalait. Un record se compare structurellement : la vue ne se
rafraîchit que si quelque chose a changé.

**Le journal (`GameLog`).** Canal de diagnostic statique (Core.Model), hors partie et jamais
sérialisé : erreurs attrapées (systèmes, audio, sauvegarde) et avertissements (contenu manquant
remplacé, comme une animation absente). Un message répété fait monter un compteur au lieu d'être
dupliqué. Le bouton « Log » du HUD, sur les deux piles, l'affiche du plus récent au plus ancien, et
clignote quand un nouveau message n'a pas encore été vu (`HudViewModel.LogUnread`,
`LogHighlighted`). Le HUD lit `GameLog.Version` directement, sans passer par un snapshot : le journal
est déjà thread-safe et reste lisible sans partie en cours.

## Simulation

- **Horloge** (`GameClock`) : 1 tick = 10 ms. Le jeu n'avance qu'en ticks, jamais en temps réel. Le
  temps de pause et d'absence part dans une banque, que les vitesses x3/x10 dépensent.
- **Pipeline** (`SimulationPipeline`) : la liste ordonnée des `ISimulationSystem`. L'ordre de la liste
  *est* l'ordre du tick, et un test le fige. Chaque système est isolé par un try/catch : une
  exception est consignée dans `GameLog` sans priver les suivants de leur passage.
- **Cooldowns** (`TickCooldown`) : un passage du pipeline peut couvrir des milliers de ticks (saut de
  temps, `SimulateTicks` en grosses tranches). Un système périodique compte ses cycles écoulés au
  lieu d'agir une fois par passage. Un test vérifie que la production ne dépend pas de la taille
  des tranches.
- **Aléatoire** (`GamePrng`) : deux flux persistés, `WorldPrng` (génération) et `Prng` (gameplay).
  Jamais `System.Random` ni `new GamePrng()` dans un système : la partie se désynchroniserait de
  son seed, et les fixtures ne seraient plus reproductibles.
- **Modificateurs** (`ModifierAggregator`) : des fournisseurs (`IModifierProvider`) déclarent des
  `Modifier(catégorie, type, valeur, sous-catégorie)` ; le résultat vaut
  `(base + Σ additifs) × Π (1 + multiplicatifs)`, sauf remplacement. Runtime uniquement, reconstruit
  à chaque chargement.

## Rendu et input

- `IGameRenderer` + `RenderPipeline` : l'ordre d'ajout est l'ordre de dessin, du fond vers l'avant.
  Un renderer lit l'état, ne le modifie jamais.
- `Camera2D` : position monde au centre de l'écran, zoom ancré sous le curseur, cadrage.
- `InputRouter` : clic, glisser = pan, molette et pincement = zoom. Un appui qui bouge de plus de
  6 px devient un glisser et ne produit plus de clic.
- Un appui du bouton gauche **sur un personnage** le glisse dans la formation au lieu de déplacer la
  vue (voir « Réorganiser la formation » ci-dessous).
- `IScreen` : ce qui se dessine sur le canevas. Les menus vivent dans l'overlay Avalonia.

## Réorganiser la formation

Le joueur glisse un personnage vivant (bouton gauche, ou doigt) et le dépose :

- **sur un autre personnage**, n'importe où dans la formation : les deux échangent leur place ;
- **au bout d'une ligne** (cercle pointillé, en bas de la ligne), ou sur une nouvelle ligne derrière :
  seulement quand toute l'équipe est en vie, et si la pyramide reste valide ;
- **sur lui-même** : son ordre en cours est annulé.

Le dépôt ne déplace personne tout de suite : il crée un ordre (`GameState.FormationMoves`, un par
personnage, persisté) dessiné en flèche jaune. Au **prochain cycle d'action** du personnage, ce
cycle sert à se déplacer au lieu d'agir : il marche vers sa place (`MoveTicks`), comme pour une
avancée. Pendant le repos, les ordres s'exécutent à la fin du repos. Un ordre dont le personnage ou
la cible est mort disparaît.

| Pièce | Couche | Rôle |
|---|---|---|
| `FormationOrders` | Controller | Places proposées, ordres, exécution, placement des recrues, règle de la pyramide |
| `GameController.OrderFormationMove`, `CancelFormationMove`, `Recruit` | Controller | Les commandes |
| `FormationDrag` | Presentation | État du glisser (personnage, places, survol, dépôt), commun aux deux piles |
| `FormationOrderRenderer` | Render (Skia) | Flèches, places proposées, flèche qui suit le pointeur |
| `FormationOrderView` | Render.Godot | La même chose à plat sur le sol (`ImmediateMesh`), prise en coordonnées écran |

Un échange modifie de la même façon la formation du combat (`Line`, `Rank`) et celle du joueur
(`FormationLine`, `FormationRank`, reprise après une défaite) : les deux restent des pyramides. Le
recrutement (boutons du HUD, prix exponentiel selon la taille de l'équipe) place la recrue le plus
loin possible derrière sans casser la pyramide ; le joueur la déplace ensuite. Le GDD le réserve à
l'entre-deux-runs : tant que le prestige n'existe pas, il se fait en cours de run.

## Overlay

`GameView` empile la carte Skia au fond et les contrôles Avalonia par-dessus : l'ordre des enfants
décide de ce qui recouvre quoi et de qui reçoit un clic. Les vues sont construites en code (pas de
XAML) et liées à leur ViewModel par `Binding`. `ViewModelBase.SetProperty` ne notifie qu'en cas de
vrai changement.

## Menu principal

Au lancement, `GameSession` est au menu (`InMenu`) : aucune partie n'est chargée, `Tick` ne fait
rien et rien n'est sauvegardé. `MenuViewModel` (commun) propose :

| Choix | Commande de la session | Effet |
|---|---|---|
| Continuer (si une sauvegarde existe) | `ContinueGame` | Charge `autosave.sav` et crédite l'absence ; sans sauvegarde lisible, nouvelle partie |
| Nouvelle partie | `StartNewGame` | Un guerrier seul en zone 1 (recrues ensuite avec l'or) ; remplace la sauvegarde |
| Partie de debug | `StartDebugGame` | 15 guerriers contre 15 gobelins à chaque zone (`RunState.Debug`), en pyramide 5-4-3-2-1 ; jamais sauvegardée |

Le bouton Menu du HUD (`ReturnToMenu`) sauvegarde, vide le champ de bataille et rouvre le menu. Le
HUD est masqué tant que le menu est affiché. Côté Avalonia, `MenuView` est construit en code ; côté
Godot, il lie `ui/menu.tscn` (voir 08).

## Règles transformées en tests

| Règle | Test |
|---|---|
| Tout enum du Core est sérialisé par nom | `InvariantTests.EveryCoreEnum_IsSerializedByName` |
| Tout type d'entité est enregistré | `InvariantTests.EveryEntityKind_IsRegistered_WithMatchingType` |
| L'ordre de simulation est déclaré | `InvariantTests.SimulationPipeline_HasTheDeclaredOrder` |
| fr.json et en.json ont les mêmes clés | `InvariantTests.LocalizationFiles_HaveTheSameKeys` |
| Les piles Skia et Godot ne se référencent pas | `InvariantTests.GodotAndSkiaStacks_DoNotReferenceEachOther` |
| Tout son a son WAV | `SoundBankTests.EverySoundId_HasAReadableWav` |
| Les sauvegardes se relisent | voir 03 |

Une règle qui ne vit que dans un commentaire finit par être enfreinte en silence : quand une
nouvelle règle d'architecture apparaît, l'écrire comme un test.
