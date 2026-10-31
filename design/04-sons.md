# 04 — Sons

Les bruitages passent par une seule classe, `GameAudioService` (couche Presentation, commune aux
deux piles), qui décide quoi jouer et quand ; chaque head fournit une sortie `IAudioService` qui sait
comment jouer.

```
 GameEventLog.Added ──► GameAudioService ──► IAudioService (head)
  (Core, événement)     (quoi, quand)          ├─ NativeAudioService  miniaudio : Desktop, Android
                                               ├─ BrowserAudioService Web Audio : Browser
                                               ├─ GodotAudioService   serveur audio Godot : App.Godot
                                               └─ null                muet : tests, outils
```

## Ajouter un son

1. Une valeur dans `Core.Presentation/Audio/SoundId.cs`, par exemple `BuildingCompleted`.
2. Son fichier `Core.Presentation/Resources/sounds/building_completed.wav` (le nom est la valeur en
   snake_case). Deux façons :
   - le synthétiser : une fonction `building_completed()` dans `assets/sounds/generate_sounds.py`,
     ajoutée à `SOUNDS`, puis `python assets/sounds/generate_sounds.py` ;
   - déposer un vrai enregistrement (mono ou stéréo, 16 bits ou 32 bits flottants, toute
     fréquence : il est rééchantillonné au chargement).
3. Le déclencher dans `GameAudioService.OnGameEvent` à partir du type d'événement que le Core écrit
   dans `GameEventLog` (`state.Events.Add(new GameEvent(tick, "building_completed"))`).
4. Si besoin, régler son intervalle minimum (`MinInterval`) et son volume relatif (`Mix`).

`SoundBankTests` échoue si un `SoundId` n'a pas de WAV lisible.

## Règles

- **Le Core ne connaît pas les sons.** Il écrit des événements ; la couche Presentation les traduit, pour les deux piles à la fois.
- **Sons courts** (< 1 s) avec fondu de fin : ce sont des retours d'interface. La musique demanderait
  un autre chemin (flux plutôt qu'échantillons en mémoire).
- **Garde-fous** : intervalle minimum par son (cent victoires en vitesse x10 ne font pas cent sons dans la même
  frame), silence pendant un chargement (`Suppressed`), silence en arrière-plan si le joueur le veut.
- **Jamais bloquant.** `IAudioService.Play` est appelé sous le verrou du runtime, sur le thread UI :
  il ne doit jamais attendre. `NativeAudioService` abandonne le son si son verrou de mixage est pris.
- **Jamais fatal.** Pas de carte son, pilote récalcitrant, WAV illisible : journalisé dans `GameLog`,
  le jeu continue en silence.

## Par plateforme

| Head | Sortie | Particularités |
|---|---|---|
| Desktop | `NativeAudioService` (SoundFlow / miniaudio) | Mixage maison de 12 voix ; fenêtre sans focus = arrière-plan |
| Android | `NativeAudioService`, fichier lié depuis Desktop | Muet par défaut en arrière-plan ; la sortie est mise en veille (batterie) |
| Browser | `BrowserAudioService` → `wwwroot/gameInterop.js` | Aucun son avant le premier clic ou la première touche (règle des navigateurs) ; onglet caché = arrière-plan |

## À ajouter selon le jeu

- Persister le volume et l'interrupteur (aujourd'hui runtime seulement) dans un fichier de réglages
  via `IFileSystemService`, encodé avec `SaveCodec` comme la sauvegarde.
- Des familles de sons coupables séparément (combat, notifications…), comme `SoundCategory` dans
  SettlersOfIdlestan.
