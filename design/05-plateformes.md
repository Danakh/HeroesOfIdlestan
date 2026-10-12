# 05 — Plateformes

Un head ne fait que trois choses : construire un `GameRuntime` avec ses services, l'envelopper dans
un `GameRuntimeHost`, et afficher `GameView`. Tout le reste est partagé.

| | Desktop | Browser | Android |
|---|---|---|---|
| Projet | `HeroesOfIdlestan.Desktop` | `HeroesOfIdlestan.Browser` | `HeroesOfIdlestan.Android` |
| Dans `.slnx` | oui | oui | **non** (workload android requis) |
| Stockage | Fichiers `%AppData%/HeroesOfIdlestan` | localStorage (`heroesofidlestan_*`) | Stockage interne de l'app |
| Son | `NativeAudioService` | `BrowserAudioService` | `NativeAudioService` (fichier lié) |
| Arrière-plan | Fenêtre sans focus | Onglet caché | Activité en pause |
| Temps perdu | — | Onglet bridé : durée cachée versée à la banque | Processus gelé : sentinelle d'1 s |
| Lancement | `dotnet run --project HeroesOfIdlestan.Desktop` | `dotnet run --project HeroesOfIdlestan.Browser` | `dotnet build -t:Run -f net10.0-android` sur un appareil |
| Livraison | `dotnet publish -c Release -r win-x64` | GitHub Pages (voir 06) | APK (workflow `build-android.yml`) |

## Desktop

`Program.cs` contient `Program`, `App` et `MainWindow`. La fermeture de la fenêtre libère l'hôte,
qui sauvegarde une dernière fois. Pour Linux et macOS, publier avec `-r linux-x64` / `-r osx-arm64`.

## Browser (WebAssembly)

- `Program.Main` charge d'abord le module d'interop (`wwwroot/blankInterop.js`), puis démarre
  Avalonia dans `<div id="out">`.
- Le stockage est **synchrone** (localStorage) : le WASM n'a qu'un thread, une promesse attendue en
  bloquant gèlerait la page.
- `JsonSerializerIsReflectionEnabledByDefault=true` et `PublishTrimmed=false` sont indispensables :
  sans eux, la sauvegarde ne se charge pas (page noire) ou se recharge incomplète sans erreur.
- Quota localStorage ≈ 5 Mo par domaine, partagé par tous les jeux d'un même `github.io` : d'où le
  préfixe des clés, et une sauvegarde à garder bien en dessous.
- Tout chemin de la page est relatif : elle doit marcher dans un sous-dossier (GitHub Pages).

Workload utile pour publier : `dotnet workload install wasm-tools`.

## Android

- Exige `dotnet workload install android` et un JDK 21. Le projet est hors de la solution pour que
  `dotnet build HeroesOfIdlestan.slnx` marche sans.
- `ApplicationId` (`com.example.heroesofidlestan`) est à changer avant toute publication — il ne peut plus
  changer ensuite.
- `versionName` est lu dans `GameVersion.cs` ; `versionCode` vaut 1 en local et le numéro de run en CI.
- Même contrainte que le web sur la réflexion JSON : trimming et AOT désactivés.
- Plein écran immersif ; rotation et redimensionnement ne recréent pas l'activité.
- Pas d'icône fournie : ajouter un PNG en `Resources/mipmap/appicon.png` (`AndroidResource`) et
  `Icon = "@mipmap/appicon"` sur l'activité et l'application.

Build local :

```powershell
dotnet publish HeroesOfIdlestan.Android -c Release
adb install -r HeroesOfIdlestan.Android\bin\Release\net10.0-android\publish\com.example.heroesofidlestan-Signed.apk
```

### Signature

Sans keystore, l'APK est signé avec la clé de debug de la machine : il s'installe, mais un APK signé
ailleurs (la CI comprise) refusera de s'installer par-dessus. Pour une clé de publication :

```powershell
keytool -genkeypair -v -keystore release.keystore -alias blank -keyalg RSA -keysize 2048 -validity 10000
```

Dans GitHub (*Settings > Secrets and variables > Actions*), créer `ANDROID_KEYSTORE_BASE64` (le
fichier en Base64 : `[Convert]::ToBase64String([IO.File]::ReadAllBytes("release.keystore"))`),
`ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS` et `ANDROID_KEY_PASSWORD`. Le workflow
*Build Android APK* (lancement manuel) les utilise. **Ne jamais committer le keystore.**

## Ajouter une plateforme

Un nouveau head référence `HeroesOfIdlestan.UI`, implémente `IFileSystemService` (et `IAudioService` s'il
a du son), construit `GameRuntime`, et signale l'arrière-plan (`SetInBackground`) et le temps perdu
(`CreditStalledTime`) à l'hôte.
