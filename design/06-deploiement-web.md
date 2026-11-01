# 06 — Déploiement de la version web sur GitHub Pages

La version web se publie par le workflow `.github/workflows/deploy-web.yml` : il lance les tests,
publie le head Browser et met en ligne le bundle sur GitHub Pages, à l'adresse
`https://<compte>.github.io/<dépôt>/`.

## Mise en place (une fois)

1. **Pousser le dépôt sur GitHub.**

   ```powershell
   git remote add origin https://github.com/<compte>/<dépôt>.git
   git push -u origin master
   ```

2. **Activer Pages avec GitHub Actions comme source** : *Settings > Pages > Build and deployment >
   Source : GitHub Actions*. Sans ce réglage, l'étape *Deploy to GitHub Pages* échoue.

3. **Dépôt privé** : GitHub Pages exige un plan payant pour un dépôt privé. Sinon, rendre le dépôt
   public, ou déployer ailleurs (voir « Autres hébergeurs »).

4. **Branche autorisée** : l'environnement `github-pages` n'accepte par défaut que les déploiements
   depuis la branche par défaut (`master`), ce qui suffit tant qu'on publie à chaque push. Pour
   publier par tag, voir plus bas.

## Publier

Deux déclencheurs, le temps du prototype :

- **À chaque push sur `master`** : chaque commit poussé est mis en ligne. Deux pushes rapprochés
  font deux déploiements à la suite (`concurrency` n'en coupe aucun).
- **Manuel** : onglet *Actions > Deploy web to GitHub Pages > Run workflow*.

**Plus tard, publier seulement les versions** : dans `deploy-web.yml`, remplacer
`branches: [ master ]` par `tags: [ 'v*' ]`, puis dans *Settings > Environments > github-pages >
Deployment branches and tags*, ajouter une règle de **tag** `v*` (sans elle, le déploiement par tag
est refusé). On publie alors par :

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
| Upload / Deploy | Artefact Pages puis mise en ligne |

Pas de `.nojekyll` : un artefact déployé par Actions est servi tel quel, sans Jekyll, et
`_framework/` est donc publié. (`upload-pages-artifact` ignore de toute façon les fichiers cachés.)
Les actions sont en versions Node 24 (`checkout@v7`, `setup-dotnet@v6`, `configure-pages@v6`,
`upload-pages-artifact@v5`, `deploy-pages@v5`).

## Tester localement avant de publier

```powershell
dotnet publish HeroesOfIdlestan.App.Browser -c Release
cd HeroesOfIdlestan.App.Browser\bin\Release\net10.0-browser\browser-wasm\AppBundle
python -m http.server 8080
# ouvrir http://localhost:8080
```

Ne pas ouvrir `index.html` directement depuis le disque : les modules JS et le WASM exigent HTTP.

## Pièges

- **Page noire** : ouvrir la console du navigateur. Causes typiques : réflexion
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
