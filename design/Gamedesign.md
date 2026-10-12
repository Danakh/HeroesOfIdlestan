# Game Design Document — AutoBattler (titre provisoire)

> Version 0.9 — 2026-09-28

---

## 1. Vision

**Genre :** Idle / Autobattler
**Univers :** Médiéval-fantastique classique
**Plateformes :** PC + Mobile
**Référence principale :** *Idle Champions of the Forgotten Realms*

Le joueur dirige une équipe d'aventuriers qui enchaîne automatiquement des combats contre des groupes de monstres. Le cœur du gameplay n'est pas l'action en temps réel mais **la construction de la formation** : la place de chaque personnage détermine ce qu'il fait au combat.

### Piliers
1. **La formation en pyramide** : c'est la décision principale du joueur.
2. **Des classes qui changent de rôle selon la ligne** : un même personnage n'agit pas de la même façon devant ou derrière, et change de style s'il se déplace en plein combat.
3. **La progression idle** : le jeu avance tout seul, et le joueur revient pour optimiser, faire un prestige et réorganiser son équipe.

---

## 2. Boucles de jeu

### Boucle courte (une zone)
```
Combat automatique  →  Repos (soins)  →  Zone suivante  →  ...
```
- L'équipe affronte un groupe de monstres.
- Après la victoire, l'équipe se repose un court moment (soins de base, puis résurrection plus tard dans la progression) et passe à la **zone suivante**.
- **Défaite** (toute l'équipe est morte) : l'équipe recule d'une zone.

### Avance automatique
- Un bouton **« Avance automatique »** active ou désactive le passage automatique à la zone suivante.
- **Activé** : après chaque victoire, l'équipe passe à la zone suivante.
- **Désactivé** : l'équipe reste sur la zone en cours et la rejoue en boucle (farm).
- **À chaque défaite, l'avance automatique se désactive** : l'équipe recule d'une zone et y combat en boucle, au lieu de retenter sans fin le combat perdu. Le joueur la réactive quand il pense que son équipe est prête.

### Boucle moyenne (un run)
- Un run **commence toujours en zone 1**.
- Chaque groupe de monstres vaincu fait passer à la zone suivante, et **chaque zone est un peu plus difficile que la précédente**.
- **Toutes les 25 zones**, il y a un **village à libérer** en battant un boss (voir §6.2).
- Pendant le run, l'équipe gagne de l'**or** et de l'**XP**.
- **La formation ne peut pas être réorganisée pendant un run.**

### Boucle longue (méta / prestige)
- Le joueur décide lui-même de **terminer son run** quand il estime avoir assez progressé. Il gagne alors des **points de prestige**, en fonction des **villages libérés**.
- Entre deux runs, il peut :
  - **recruter** de nouveaux personnages et faire d'autres **achats** (avec l'or, voir §6.4),
  - réorganiser sa formation,
  - choisir l'**évolution** des personnages qui ont atteint le niveau maximum de leur classe (voir §5.4),
  - dépenser ses points de prestige dans un ou plusieurs **arbres de talents**.
- Sur la **carte du monde**, chaque run permet de tenter de valider une étape pour débloquer la suivante (voir §6.1). On peut aussi **rejouer une étape** déjà validée pour accumuler de l'or et des points de prestige.

---

## 3. La formation en pyramide

### 3.1 Règle fondamentale
L'équipe est disposée en **lignes**. La ligne 1 est au front, les suivantes sont de plus en plus en arrière. Chaque ligne est **centrée**. Selon le nombre de personnages, deux lignes voisines sont soit en quinconce, soit alignées (voir §3.4).

> **Chaque ligne doit contenir strictement plus de personnages que la ligne située derrière elle.**

| Formation | Valide ? |
|-----------|----------|
| 1         | ✅ |
| 2-1       | ✅ |
| 3-2-1     | ✅ |
| 4-2-1     | ✅ |
| 2-2       | ❌ |
| 3-1-1     | ❌ |

- La taille de l'équipe et le nombre de lignes **n'ont pas de maximum**.
- Au début du jeu, le joueur n'a **qu'un seul personnage**. L'équipe grandit en **recrutant** de nouveaux personnages avec de l'or.
- Il n'y a **pas d'emplacements à débloquer** : la seule limite est le nombre de recrues, et le joueur choisit librement la forme de sa pyramide.
  > Exemple : avec 6 recrues, on peut faire `3-2-1` (et accéder à la ligne 3) ou bien `4-2`.

### 3.1 bis Zone des non-combattants
- Derrière la formation, une **zone de non-combattants** suit l'équipe.
- On y place des éléments achetés qui ne combattent pas mais donnent des **bonus passifs**, par exemple des familiers ou une charrette (voir §6.4).
- Sa **capacité est limitée**, et on peut l'**agrandir avec de l'or**.
- Les monstres **ne peuvent pas** attaquer les non-combattants.

### 3.2 Mort d'un personnage : resserrement et cascade
Ces règles s'appliquent **en temps réel pendant le combat**.

**Étape 1 : resserrement.** Quand un personnage meurt, sa ligne se **resserre** : les personnages de la ligne se décalent pour boucher le trou, et les lignes de derrière se recalent pour rester en quinconce.

**Étape 2 : avancée, si nécessaire.** Si la règle de la pyramide est toujours respectée après le resserrement, **personne n'avance**.
> Exemple : `4-2-1` → un personnage meurt en ligne 1 → `3-2-1`, valide, personne n'avance.

Il ne faut faire avancer quelqu'un que si la ligne touchée a maintenant **autant de personnages ou moins** que la ligne derrière. Comme la formation est resserrée, il y a alors forcément **un personnage derrière le trou** (ou **deux** si le trou est au centre). C'est l'un d'eux qui avance.

**Choix du personnage qui avance** (quand deux sont possibles) :
1. **Affinité** : le personnage dont la **ligne préférée** correspond à la ligne à combler est prioritaire (voir §3.3).
2. En cas d'égalité, **ordre fixe** : de haut en bas.

**Étape 3 : cascade.** L'avancée d'un personnage peut à son tour rendre la ligne de derrière invalide. On répète alors les étapes 1 et 2 ligne par ligne, jusqu'à ce que toute la formation soit valide.

> Exemple : `3-2-1` → un personnage meurt en ligne 1 → `2-2-1` (invalide)
> → un personnage de la ligne 2 avance → `3-1-1` (invalide)
> → le personnage de la ligne 3 avance → `3-2` (valide)

**Recul :** un personnage ne peut pas revenir en arrière dans la version de base (par exemple après une résurrection). Ce sera envisagé plus tard.

### 3.3 Ligne préférée
- La ligne préférée est **configurée par le joueur pour chaque personnage** au moment où il construit sa formation. Elle n'est pas fixée par la classe.
- Exemple : une voleuse peut être configurée pour la ligne 1 (corps à corps) ou pour la ligne 2 (lancer de couteaux).
- La ligne préférée sert à départager les personnages lors d'une avancée (§3.2).
- Un personnage **change de style de combat immédiatement** s'il change de ligne en plein combat.

### 3.4 Géométrie : voisins et distances
Comme chaque ligne est centrée, la disposition de deux lignes voisines dépend de la **parité de la différence** entre leurs effectifs :

- **Différence impaire** (par exemple 3 et 2) : les lignes sont **en quinconce**. Chaque personnage a **deux voisins** dans la ligne d'à côté, à distance 1.
- **Différence paire** (par exemple 4 et 2) : les lignes sont **alignées**. Chaque personnage a **un seul voisin** juste devant ou derrière lui, à distance 1. Les diagonales sont à distance 2.

```
Quinconce (3-2)            Alignées (4-2)

 A   B   C                  A   B   C   D
   D   E                       E   F

D est à distance 1         E est à distance 1 de B seulement
de A et de B.              (A et C sont à distance 2).
```

**Distance entre alliés**
- Sur une même ligne, les personnages côte à côte sont à **distance 1**.
- Entre deux lignes voisines : voir ci-dessus (1 ou 2 voisins à distance 1, selon la parité).

> **Formule proposée (à confirmer).** On numérote les positions en demi-cases : dans une ligne de *n* personnages, les positions sont −(n−1), −(n−3), …, n−3, n−1. Alors :
> **Distance = écart de lignes + ⌊ écart de positions ÷ 2 ⌋**
> Cette formule redonne tous les cas ci-dessus. Par exemple, en 4-2, E (position −1) et A (position −3) : 1 + ⌊2 ÷ 2⌋ = 2.

**« En face »**
- Même principe entre la ligne 1 de l'équipe et la ligne 1 adverse. Selon la parité de la différence d'effectifs, un personnage a **1 ou 2 adversaires en face** de lui.
- Cette notion sert au ciblage (§4.4), au bonus de l'Assassin et à la restriction de soin du Prêtre.

---

## 4. Combat

### 4.1 Système de temps
- Le combat se déroule en **temps réel**, avec un **temps simulé par ticks**.
- Chaque action (attaque, sort, soin…) a une durée ou une fréquence exprimée en ticks.
- Ce modèle déterministe permet aussi d'**accélérer le temps** (voir §7, temps hors ligne).

### 4.2 Monstres
- Les monstres ont eux aussi une **formation en pyramide**, soumise aux mêmes règles.
- Leur ciblage **dépend du type de monstre**. Par exemple, un combattant au corps à corps frappe la première ligne, alors qu'un archer ou un lanceur de sorts peut viser l'arrière.

### 4.3 Portées
La distance entre un attaquant et sa cible se calcule en deux temps.

**1. Distance de base (entre lignes)**

> **Distance de base = ligne de l'attaquant + ligne de la cible − 1**

Cette distance s'applique à l'adversaire (ou aux deux adversaires) **le plus en face** de l'attaquant sur la ligne visée (§3.4), quelle que soit la position de l'attaquant dans sa ligne.

| Attaquant en… | Cible en ligne 1 | Cible en ligne 2 | Cible en ligne 3 | Cible en ligne 4 |
|---------------|------------------|------------------|------------------|------------------|
| Ligne 1       | 1                | 2                | 3                | 4                |
| Ligne 2       | 2                | 3                | 4                | 5                |
| Ligne 3       | 3                | 4                | 5                | 6                |

**2. Écart latéral**

Pour un autre adversaire de cette même ligne, on **ajoute** sa distance à l'adversaire le plus en face (1 par personnage d'écart sur la ligne).

> **Distance = distance de base + écart latéral entre la cible et l'adversaire le plus en face**

**La distance n'est pas réciproque.** Exemple : un monstre seul contre une ligne 1 de 5 personnages.

```
Équipe, ligne 1 :   A   B   C   D   E
Monstre, ligne 1 :          M
```

- **Assassin en E → M :** M est le seul adversaire, donc le plus en face de E. Distance **1**, E peut attaquer.
- **M → C** (au centre, en face) : distance **1**.
- **M → B ou D** : 1 + 1 = **2**.
- **M → A ou E** (extrémités) : 1 + 2 = **3**. Un monstre au corps à corps ne peut pas les toucher.

*Justification :* l'assassin est libre d'aller frapper dans le dos puis de se replier dans sa formation, alors que le monstre isolé est bloqué au corps à corps par ceux qui sont en face de lui.

**Portées des actions**
- Chaque action a une **portée minimale et une portée maximale**. Par exemple, une portée **2-3** peut toucher une cible à distance 2 ou 3, mais pas à distance 1.
- Chaque action a aussi une liste de **lignes depuis lesquelles elle peut être utilisée**. Ces restrictions se décident **action par action**. Par exemple, l'arc de l'Archer est inutilisable en ligne 1, alors que la lance du Lancier fonctionne depuis la ligne 1.
- Une portée **1** correspond au **corps à corps** : depuis la ligne 1, sur la ligne 1 adverse.
- **Zone :** certaines actions touchent plusieurs cibles (une cible et ses voisins, toute une ligne…).

### 4.4 Ciblage
**Ciblage automatique**

Chaque personnage choisit seul sa cible, parmi celles à sa portée :
1. Il vise d'abord la **distance la plus grande** possible (sa portée maximale).
2. Sur cette ligne, il vise l'adversaire **le plus en face** de lui. S'il n'y a personne juste en face, il vise **le plus proche d'en face**.
3. En cas d'égalité, il applique l'**ordre de haut en bas**, comme pour la cascade (§3.2).

> Exemple : un Archer en ligne 2 peut toucher la ligne 1 adverse (distance 2) ou la ligne 2 adverse (distance 3). Par défaut, il vise la ligne 2.

**Cible prioritaire**
- Pendant un run, le joueur peut **cliquer sur un adversaire**. Celui-ci devient la **cible prioritaire de toute l'équipe**.
- Chaque personnage qui a cette cible à sa portée l'attaque. Les autres gardent leur ciblage automatique *(à confirmer)*.

> La notion d'adversaire « en face » est définie au §3.4.

---

## 5. Classes

### 5.1 Principe
Il y a **4 classes de base**. Le joueur peut avoir **plusieurs personnages de la même classe**, et il n'y a pas de héros unique. Chaque classe se comporte différemment selon la ligne où elle se trouve.

> **Règle générale :** si une classe n'a aucune action possible depuis la ligne où elle se trouve, elle **ne fait rien**.

Chaque classe a aussi une **résistance** (capacité à encaisser les coups) : faible, moyenne, bonne ou très bonne.

### 5.2 Classes de base

| Classe       | Ligne 1 | Ligne 2 | Ligne 3 | Ligne 4+ |
|--------------|---------|---------|---------|----------|
| **Guerrier** | Corps à corps | — | — | — |
| **Voleur**   | Corps à corps | Lancer de couteaux (distance) | — | — |
| **Prêtre**   | Corps à corps | Soins / buffs à portée 1 | Soins / buffs à portée 1 | — |
| **Mage**     | — | Sorts mono-cible | **Rituels** | — |

*« — » : aucune action, le personnage ne fait rien. Les lignes 3 et 4+ du voleur et la ligne 4+ du prêtre et du mage restent à confirmer.*

- **Prêtre :** il **ne peut pas soigner s'il a un adversaire en face de lui** (donc en pratique en ligne 1 au contact).
- **Rituels (mage) :** ce sont des effets **maintenus** (canalisés), par opposition aux attaques mono-cible ponctuelles. Ce sont principalement des sorts de zone ou des buffs importants.

### 5.3 Évolutions
Chaque classe a deux évolutions, et la liste pourra être enrichie plus tard. Une évolution **garde les actions de sa classe de base** et y ajoute les siennes (voir §5.4).

| Classe de base | Évolution | Résistance | Portée | Particularités |
|----------------|-----------|------------|--------|----------------|
| Guerrier | **Tank** | Très bonne | 1 | Encaisse le mieux de toute l'équipe. |
| Guerrier | **Lancier** | Moyenne | 1-2 | Frappe à la lance. Depuis la ligne 1, touche les lignes 1 et 2 adverses. Depuis la ligne 2, touche la ligne 1 adverse. |
| Voleur | **Assassin** | Faible | 1-2 | **Bonus de dégâts en ligne 1 s'il n'a personne en face de lui.** Il faut donc le placer à une **extrémité de la ligne 1**. |
| Voleur | **Archer** | Moyenne | 2-3 | **Arc inutilisable en ligne 1** : il s'y bat comme un voleur. En ligne 2, il tire par défaut sur la ligne 2 adverse. En ligne 3, il tire sur la ligne 1 adverse. Au-delà, pas d'action. |
| Prêtre | **Paladin** | Bonne | 1 | Peut **soigner ses voisins (portée 1) même en ligne 1**, avec un adversaire en face (contrairement au prêtre). |
| Prêtre | **Soigneur** | Faible | Soin à portée 2 | **Ne peut pas soigner s'il a un adversaire au corps à corps.** |
| Mage | **Feu** | Faible | Mono-cible 2-3 | **Rituel « Mur de feu » :** touche **toute la ligne 1 adverse**, avec des dégâts sur la durée. |
| Mage | **Glace** | Faible | Mono-cible 2-4 | L'attaque mono-cible **ralentit la vitesse d'attaque** de la cible. **Rituel de zone (portée 4) :** touche une cible et ses voisins, et les **immobilise complètement** pendant une courte durée. |

### 5.4 Progression d'un personnage et évolution
La classe de départ est **choisie au recrutement**. Les évolutions se débloquent ensuite **avec l'expérience** :

1. Pendant un run, le personnage gagne de l'XP et monte de niveau dans sa classe.
2. La **première fois** qu'il atteint le **niveau maximum** de sa classe, il **cesse de progresser** pour le reste du run.
3. Entre deux runs, pendant la mise à jour de la formation, le joueur peut lui **choisir une évolution** (par exemple Voleur → Archer).
4. Au run suivant, le personnage **repart dans sa classe de base au niveau 1**. Quand il en atteint le niveau maximum, il **passe dans sa classe évoluée**, au niveau 1, et continue à progresser.

**Les statistiques s'additionnent :** en passant Archer, le personnage garde les statistiques gagnées en tant que Voleur, et celles de l'Archer s'y ajoutent.

**Les actions aussi :** le personnage **garde les actions de sa classe de base**. Aux lignes où la classe évoluée a sa propre action, celle-ci **remplace** l'action de base. Aux autres lignes, c'est l'action de base qui s'applique.

**Changement en plein run :** le passage à la classe évoluée se fait pendant le run, et le comportement du personnage change tout de suite. Le joueur doit donc placer le personnage **sur une ligne adaptée à son futur rôle**, en acceptant qu'il soit moins efficace en début de run.

- **Niveau maximum :** **niveau 20**, pour toutes les classes et toutes les évolutions.

> Exemple sur plusieurs runs :
> - **Run 1** : Voleur niv. 1 → Voleur niv. max → plus de progression.
> - **Entre deux runs** : le joueur choisit l'évolution Archer.
> - **Run 2** : Voleur niv. 1 → Voleur niv. max → **Archer niv. 1** → Archer niv. …

---

## 6. Progression

### 6.1 Carte du monde
- La carte est un **chemin linéaire d'étapes**.
- Chaque étape fixe une **zone à atteindre**, par exemple la zone 75.
- Pour **valider une étape** et débloquer la suivante, il faut faire un run qui atteint cette zone.
  > Exemple : une étape à 75 est validée quand le troisième village (zone 75) est libéré.

### 6.2 Zones et villages
- Un run commence toujours en **zone 1**, et chaque groupe de monstres vaincu fait passer à la zone suivante.
- La difficulté **augmente à chaque zone**.
- **Toutes les 25 zones** (25, 50, 75…), l'équipe doit **libérer un village** en battant un **boss**. Il peut s'agir :
  - d'un gros monstre unique,
  - ou d'un groupe de monstres qui reçoit des renforts en cours de combat, etc.
- Les villages libérés renforcent la **méta-progression**.
- Une étape peut être **rejouée** autant de fois que voulu pour accumuler de l'or et des points de prestige.

### 6.3 Ressources
| Ressource | Source | Usage |
|-----------|--------|-------|
| **Or** | Combats | **Recrutement** de personnages, autres **achats** (§6.4) |
| **XP** | Combats | Montée de niveau des personnages |
| **Points de prestige** | Fin de run, selon les **villages libérés** | **Arbre(s) de talents** |

### 6.4 Achats avec l'or
**Recrutement**
- Le recrutement se fait **uniquement entre deux runs**.
- Le joueur **choisit la classe** de base de la recrue.
- Le prix d'une recrue dépend **uniquement du nombre de personnages déjà recrutés**.
- Ce prix augmente de façon **exponentielle**. Il n'y a pas de limite au nombre de recrues, c'est le coût qui freine la progression.
- *Les valeurs précises seront fixées plus tard, pendant l'équilibrage.*

**Autres achats (pistes)**
Ces éléments vont dans la **zone des non-combattants** (§3.1 bis) :
- **Familiers** : bonus passifs.
- **Charrette** : permet de porter plus, ce qui donne un bonus d'or.
- *D'autres achats restent à imaginer.*

### 6.5 Prestige
- Le run se termine **à l'initiative du joueur**.
- Il rapporte des **points de prestige**, calculés selon les **villages libérés**. Chaque village rapporte **de plus en plus de points** à mesure qu'on avance. Libérer à nouveau un village lors d'un autre run rapporte aussi des points.
- Les points se dépensent dans un ou plusieurs **arbres de talents**.
- Entre deux runs : recrutement, achats, réorganisation de la formation, choix des évolutions.

**Ce qui est conservé ou remis à zéro au prestige :**

| Élément | Au prestige |
|---------|-------------|
| Or | ✅ Conservé |
| Personnages recrutés | ✅ Conservés |
| Achats (familiers, charrette…) | ✅ Conservés |
| Talents | ✅ Conservés |
| Niveaux et XP des personnages | 🔄 Remis au niveau 1 |
| Zone atteinte | 🔄 Retour en zone 1 |

---

## 7. Aspect idle

- **Temps hors ligne :** le temps passé hors du jeu est **accumulé**. Le joueur peut ensuite le dépenser pour **accélérer le temps en jeu** (grâce à la simulation par ticks).
- Les combats s'enchaînent automatiquement, sans intervention du joueur.

---

## 8. Questions ouvertes

### Formation
- [ ] Que veut dire concrètement « de haut en bas » dans l'ordre fixe, une fois la formation affichée à l'écran ?
- [ ] Lors du resserrement d'une ligne, dans quel sens se décalent les personnages (vers le centre, vers le haut) ?

### Classes
- [ ] Que fait le voleur en ligne 3 ?
- [ ] Que font les classes en ligne 4 et au-delà ?
- [ ] Le prêtre soigne/buffe « autour de lui » : quelle est la portée exacte (cases adjacentes, sa ligne, la ligne de devant) ?
- [ ] Depuis quelles lignes le Soigneur peut-il agir ? Que fait-il en ligne 3+ ?
- [ ] Le Prêtre et le Paladin donnent des buffs : lesquels ?
- [ ] Depuis quelle ligne se lancent les rituels Feu et Glace (ligne 3 seulement ?) Le mur de feu est-il toujours sur la ligne 1 adverse, quelle que soit la ligne du mage ?
- [ ] Le choix d'une évolution est-il définitif, ou peut-on en changer entre deux runs ?

### Combat
- [ ] Quelles statistiques auront les personnages (PV, attaque, vitesse, armure…) ?
- [ ] Combien de ticks par seconde en vitesse normale ?
- [ ] Valider la formule de distance entre alliés (§3.4), en particulier pour deux lignes non voisines (ligne 1 et ligne 3).
- [ ] Écart latéral quand les lignes sont en quinconce et qu'il y a **deux** adversaires en face : l'écart se compte-t-il depuis le plus proche des deux ?
- [ ] Cible prioritaire : les personnages qui ne peuvent pas l'atteindre gardent-ils leur ciblage automatique ? La cible prioritaire reste-t-elle active jusqu'à sa mort ?
- [ ] Les soins ont-ils aussi une règle de ciblage (l'allié le plus blessé, le plus proche…) ?

### Progression
- [ ] Formules d'équilibrage : prix des recrues, progression des points de prestige par village, difficulté par zone.
- [ ] Zone des non-combattants : capacité de départ et coût des agrandissements.
- [ ] Quels sont les bonus exacts des villages libérés ? Sont-ils permanents ?
- [ ] Combien d'arbres de talents, avec quelles thématiques (par classe, général, économie) ?
- [ ] Le temps hors ligne accumulé a-t-il un plafond ?

---

## 9. Évolutions envisagées (hors périmètre initial)
- Résurrection pendant le repos.
- Recul d'un personnage dans la formation (par exemple après une résurrection).
- Nouvelles classes et nouvelles évolutions.
- Évolutions d'évolutions.
- **Biclassage.**
