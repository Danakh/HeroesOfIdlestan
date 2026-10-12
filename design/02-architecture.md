# 02 — Architecture

Chaque couche ne dépend que de celle du dessous, et le Core ne dépend de rien : c'est ce qui le rend
testable sans fenêtre ni GPU.

```
 Desktop / Browser / Android   heads : point d'entrée + services de plateforme
            │
            ▼
      HeroesOfIdlestan.UI          overlay Avalonia, GameRuntimeHost, boucle de frame
            │
            ▼
    HeroesOfIdlestan.Render        rendu Skia, caméra, input, sons, GameRuntime
            │
            ▼
     HeroesOfIdlestan.Core         état, simulation, sauvegarde — aucune dépendance
```

`HeroesOfIdlestan.Tests` référence Core et Render.

## Store d'état

L'état vit dans une seule racine sérialisable, `GameState`, lue et modifiée uniquement à travers
un hôte qui pose un verrou.

| Pièce | Rôle |
|---|---|
| `GameState` | Racine unique : tout ce qui est persisté, rien d'autre |
| `GameController` | Possède l'état, construit le pipeline et les agrégats runtime, expose les commandes |
| `GameRuntime` | Côté Render : contrôleur + écran + sons + sauvegarde auto ; construit par chaque head |
| `GameRuntimeHost.Read` / `Invoke` | Seule porte d'entrée vers le runtime, sous verrou |
| Snapshots (`record`) | Vue immuable pour l'UI, construite en une seule prise de verrou |
| Synchro par sondage | L'UI relit les snapshots toutes les 100 ms, découplée des 60 fps |

**Pourquoi un verrou.** Avalonia dessine sur son thread de rendu ; la boucle de jeu et l'input vivent
sur le thread UI. Le modèle reste mono-thread par conception ; c'est l'accès qui est sérialisé.
Un ViewModel qui lit le runtime sans passer par `Read` réintroduit la course (« Collection was
modified » dans un renderer, au mieux).

**Pourquoi des snapshots.** Lire dix propriétés une à une ferait dix prises de verrou et donnerait
une vue incohérente si un tick s'intercalait. Un record se compare structurellement : la vue ne se
rafraîchit que si quelque chose a changé.

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
- `IScreen` : ce qui se dessine sur le canevas. Les menus vivent dans l'overlay Avalonia.

## Overlay

`GameView` empile la carte Skia au fond et les contrôles Avalonia par-dessus : l'ordre des enfants
décide de ce qui recouvre quoi et de qui reçoit un clic. Les vues sont construites en code (pas de
XAML) et liées à leur ViewModel par `Binding`. `ViewModelBase.SetProperty` ne notifie qu'en cas de
vrai changement.

## Règles transformées en tests

| Règle | Test |
|---|---|
| Tout enum du Core est sérialisé par nom | `InvariantTests.EveryCoreEnum_IsSerializedByName` |
| Tout type d'entité est enregistré | `InvariantTests.EveryEntityKind_IsRegistered_WithMatchingType` |
| L'ordre de simulation est déclaré | `InvariantTests.SimulationPipeline_HasTheDeclaredOrder` |
| fr.json et en.json ont les mêmes clés | `InvariantTests.LocalizationFiles_HaveTheSameKeys` |
| Tout son a son WAV | `SoundBankTests.EverySoundId_HasAReadableWav` |
| Les sauvegardes se relisent | voir 03 |

Une règle qui ne vit que dans un commentaire finit par être enfreinte en silence : quand une
nouvelle règle d'architecture apparaît, l'écrire comme un test.
