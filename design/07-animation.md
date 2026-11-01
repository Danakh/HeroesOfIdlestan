# 07 — Animation squelettale

Personnages animés par un squelette d'os, habillés de morceaux SVG. Le moteur vit dans le jeu :
`Core.Model/Animation` pour les données et la pose, `Core.Controller/Animation` pour la lecture
(`AnimationPlayer`, `AnimationSampler`), `Core.Render/Animation` pour le dessin.
`HeroesOfIdlestan.Tool.Animation` sert de banc d'essai.

```bash
dotnet run --project HeroesOfIdlestan.Tool.Animation              # lit assets/animation/
dotnet run --project HeroesOfIdlestan.Tool.Animation -- <dossier> # ou un autre dossier
python assets/animation/generate_warrior_placeholder.py          # régénère les SVG provisoires
```

L'outil permet de choisir un squelette, puis un de ses habillages, et de jouer ses animations :
lecture et pause, rejouer, timeline pour se placer à un instant précis, vitesse, boucle, fondu,
affichage des os, miroir. Les événements (`hit`, `step`) s'affichent quand la lecture les franchit.
**Recharger les fichiers** relit le dossier sans recompiler : on retouche un JSON ou un SVG, puis
on recharge.

## Pièces

| Pièce | Rôle |
|---|---|
| `SkeletonData` | Hiérarchie d'os et pose de repos |
| `SkinData` | SVG accrochés aux os, dans l'ordre de dessin |
| `AnimationData` | Pistes de clés par os, en décalage par rapport au repos, et événements |
| `AnimationCodec` | Lecture/écriture JSON, validation, versions du format |
| `AnimationSampler` | Évalue une animation à un instant donné dans une `SkeletonPose` |
| `AnimationPlayer` | Temps, boucle, vitesse, événements, fondu entre deux animations |
| `SkinRenderer` (Render) | Dessine un habillage sur une pose (Svg.Skia) |

`SkeletonPose.World` donne la matrice de chaque os. Le jeu peut s'en servir hors rendu, par exemple
pour connaître la position de la lame au moment de `hit`.

## Repère

Y vers le bas, origine aux pieds, 1 unité = 1 px à l'échelle 1. Angles en degrés, positifs dans le
sens horaire. Un os pointe le long de son axe X local. Le personnage regarde vers +X ; le miroir
s'obtient en dessinant avec une échelle X négative.

## Fichiers

Reconnus par leur extension, à n'importe quelle profondeur. Habillages et animations se
rattachent à leur squelette par son `id`, pas par le dossier où ils se trouvent.
Chaque fichier commence par `format` et `version`. Les commentaires `//` sont acceptés. Une
propriété inconnue est refusée.

```
assets/animation/humanoid/
  humanoid.skeleton.json
  skins/warrior/warrior.skin.json + parts/*.svg
  animations/idle, walk, attack, hurt, death, attack_melee, attack_bow, attack_magic (.anim.json)
```

**Squelette** (`*.skeleton.json`, `"format": "hoi-skeleton"`) : une liste d'os, et chaque parent
est déclaré avant ses enfants. Pour chaque os : `name`, `parent`, `x`, `y`, `rotation`, `scaleX`,
`scaleY` (relatifs au parent) et `length`, qui ne sert qu'à l'affichage.

**Habillage** (`*.skin.json`, `"format": "hoi-skin"`) : `skeleton` et la liste `attachments`
(`bone`, `svg`), du fond vers l'avant. Le chemin `svg` est relatif au fichier d'habillage.

**Animation** (`*.anim.json`, `"format": "hoi-animation"`) : `skeleton`, `duration` (s), `loop`,
`bones` puis, par nom d'os, des pistes facultatives :

```json
"thigh_front": {
  "rotate":    [ { "time": 0, "angle": -25, "curve": "Smooth" }, { "time": 0.4, "angle": 22 } ],
  "translate": [ { "time": 0, "x": 0, "y": 7 } ],
  "scale":     [ { "time": 0, "x": 1, "y": 1 } ]
}
```

- Les valeurs sont des **décalages** par rapport au repos : la rotation et la translation
  s'ajoutent, l'échelle se multiplie. Un os sans piste reste au repos. Une animation reste ainsi
  valable si l'on retouche la pose de repos.
- La `curve` d'une clé régit le trajet jusqu'à la clé suivante : `Linear` (défaut), `Step` ou
  `Smooth`.
- Avant la première clé et après la dernière, la valeur est tenue. Pour qu'une boucle soit propre,
  sa dernière clé (à `time = duration`) répète la première.
- `events` : `[{ "time": 0.4, "name": "hit" }]`, triés par temps.

Faire évoluer le format : incrémenter `AnimationCodec.CurrentVersion` et migrer l'ancien JSON dans
`Decode`, sans jamais retirer la lecture d'une version existante. `AnimationTests` vérifie que
tous les fichiers livrés se lisent.

## Catalogue et secours

Le jeu demande ses animations par nom, avec les mêmes noms sur les deux piles : id d'un
`*.anim.json` côté Skia, nom de clip glTF côté Godot (comparé sans tenir compte de la casse, donc
`Idle` convient pour `idle`). Les noms et leurs secours sont dans `Core.Presentation/Animation/
AnimationCatalog.cs`.

| Animation | Boucle | Événement | Usage | Secours |
|---|---|---|---|---|
| `idle` | oui | — | En garde, et quand le personnage n'a rien à faire depuis sa ligne | — |
| `walk` | oui | `step` | Changement de place, zone suivante | `idle` |
| `attack` | non | `hit` | Attaque de base, valable pour toute arme ; celle des monstres | `idle` |
| `hurt` | non | — | Coup reçu | `idle` |
| `death` | non | — | Chute, dernière image tenue | `idle` |
| `attack_melee` | non | `hit` | Corps à corps (guerrier) | `attack` |
| `attack_bow` | non | `hit` (au lâcher) | Arc | `attack` |
| `attack_magic` | non | `hit` | Sort | `attack` |

Les cinq premières sont la base que tout squelette devrait avoir. Les variantes d'attaque se
choisissent selon le combattant (`AnimationCatalog.AttackFor`).

**Secours.** Un `AnimationResolver` par modèle trouve l'animation à jouer : celle demandée si le
modèle l'a, sinon la suivante de sa chaîne (`attack_bow` → `attack` → `idle`). Chaque
remplacement est consigné une fois dans `GameLog` en avertissement, que le bouton « Log » du HUD
affiche : un modèle incomplet reste jouable, et ce qui manque se voit. Une animation rabattue sur
`idle` n'est pas jouée en plus : le personnage garde son animation de fond.

Côté Godot, `FighterView` passe par ce résolveur. Le robot de test n'a ni attaque, ni coup reçu, ni
mort : le journal le signale. Sans clip `death`, la pose se fige et le corps tombe à la renverse.
Côté Skia, le jeu dessine encore les combattants en ronds et ne joue pas d'animation squelettale :
le catalogue servira quand les habillages y seront branchés.

`AnimationCatalogTests` vérifie que `humanoid` a tout le catalogue.

## SVG : consigne pour Claude Design

Le moteur n'a besoin d'aucun pivot. Chaque morceau est dessiné **à sa place dans le personnage en
pose de repos** :

1. Dessiner le personnage entier, de profil, regard vers la droite, dans la pose de repos du
   squelette. Lancer l'outil avec « Os » coché et la timeline à 0 pour la voir.
2. Découper le dessin en un SVG par os listé dans l'habillage (torse, tête, haut et bas de chaque
   bras et de chaque jambe, mains, pieds, arme).
3. Donner à tous les SVG **la même viewBox** : `-60 -200 160 205` pour `humanoid`. Avec cette
   viewBox, le point (0, 0) est entre les pieds, au sol, et l'axe Y descend. Superposés, les
   morceaux redonnent le personnage entier.
4. Faire déborder chaque membre d'un peu au-delà de son articulation (épaule, coude, hanche,
   genou), sous le morceau voisin, pour qu'aucun trou n'apparaisse quand l'os tourne.

Les morceaux « back » (côté éloigné) sont dessinés derrière le torse. Le plus simple est de leur
donner une teinte plus sombre. Pour remplacer un morceau provisoire, déposer le nouveau SVG dans
`parts/` et retirer son entrée de `PARTS` dans `generate_warrior_placeholder.py`.

## Ajouter du contenu

- **Habillage** : un dossier sous `skins/` avec un `*.skin.json` qui vise `humanoid`. Toutes les
  animations du squelette s'appliquent sans autre travail.
- **Animation** : un `*.anim.json` qui vise `humanoid`.
- **Squelette** : un nouveau `*.skeleton.json` avec ses propres habillages et animations. Les os
  et les noms sont libres.
