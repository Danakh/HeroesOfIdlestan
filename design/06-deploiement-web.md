# 06 — Déploiement de la version web sur GitHub Pages

La version web se publie par le workflow `.github/workflows/deploy-web.yml` : il lance les tests,
publie le head Browser et met en ligne le bundle sur GitHub Pages, à l'adresse
`https://<compte>.github.io/<dépôt>/`.

## Mise en place (une fois)

1. **Pousser le dépôt sur GitHub.**

   ```powershell
   git remote add origin https://github.com/<compte>/<dépôt>.git
   git push -u origin main
   ```

2. **Activer Pages avec GitHub Actions comme source** : *Settings > Pages > Build and deployment >
   Source : GitHub Actions*. Sans ce réglage, l'étape *Deploy to GitHub Pages* échoue.

3. **Dépôt privé** : GitHub Pages exige un plan payant pour un dépôt privé. Sinon, rendre le dépôt
   public, ou déployer ailleurs (voir « Autres hébergeurs »).

4. **Autoriser les tags** : l'environnement `github-pages` n'accepte par défaut que les déploiements
   depuis la branche par défaut. Pour publier par tag, dans *Settings > Environments > github-pages >
   Deployment branches and tags*, ajouter une règle de tag `v*`. Sans elle, le déploiement par tag
   est refusé (le lancement manuel depuis `main` marche toujours).

## Publier

Deux déclencheurs :

- **Manuel** : onglet *Actions > Deploy web to GitHub Pages > Run workflow*.
- **Par tag de version** :

  ```powershell
  git tag v0.1.0
  git push origin v0.1.0
  ```

Le lien de la page s'affiche dans le résumé du run (étape *Deploy*). Compter 5 à 10 minutes : la
publication WebAssembly est longue.

## Ce que fait le workflow

| Étape | Rôle |
|---|---|
| Test | `dotnet test HeroesOfIdlestan.Test.Tests` : pas de mise en ligne si une sauvegarde ne se relit plus |
| Install WASM workload | `wasm-tools`, pour la publication Release |
| Publish | `dotnet publish HeroesOfIdlestan.App.Browser -c Release` ; le bundle est dans `bin/Release/net10.0-browser/browser-wasm/AppBundle` |
| Add .nojekyll | Sans lui, Jekyll ignore `_framework/` (préfixe « _ ») et la page reste noire |
| Upload / Deploy | Artefact Pages puis mise en ligne |

## Tester localement avant de publier

```powershell
dotnet publish HeroesOfIdlestan.App.Browser -c Release
cd HeroesOfIdlestan.App.Browser\bin\Release\net10.0-browser\browser-wasm\AppBundle
python -m http.server 8080
# ouvrir http://localhost:8080
```

Ne pas ouvrir `index.html` directement depuis le disque : les modules JS et le WASM exigent HTTP.

## Pièges

- **Page noire** : ouvrir la console du navigateur. Causes typiques : `.nojekyll` absent, réflexion
  JSON désactivée (voir 05), chemin absolu dans `index.html` (`/main.js` au lieu de `./main.js`).
- **Ancienne version affichée** : le navigateur garde `_framework/` en cache ; recharger sans cache
  (Ctrl+F5).
- **Sauvegardes** : elles vivent dans le localStorage du domaine `<compte>.github.io`. Changer de
  dépôt garde le domaine ; changer de compte ou de domaine personnalisé perd les parties.
- **Domaine personnalisé** : *Settings > Pages > Custom domain*. Les chemins relatifs de la page
  rendent l'opération transparente pour le jeu, mais les sauvegardes de l'ancien domaine ne suivent pas.

## Autres hébergeurs

Le contenu d'`AppBundle` est statique : tout hébergeur de fichiers convient (itch.io en jeu HTML
zippé, Netlify, Cloudflare Pages…). Servir les `.wasm` avec le type `application/wasm`.
