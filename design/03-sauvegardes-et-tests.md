# 03 — Sauvegardes et tests

Une sauvegarde de joueur qui ne se relit plus, c'est une partie perdue : le défaut le plus grave
qu'un jeu idle puisse livrer. Les sauvegardes sont donc couvertes par cinq niveaux de tests, et
toute modification du modèle persisté passe par eux.

## Format

`GameState` → JSON (System.Text.Json) → XOR → Base64, estampillé de `GameVersion.Current`.

- `SaveCodec` : encodage. La lecture accepte aussi le JSON brut (débogage, fixtures écrites à la
  main). Ajouter un format sans jamais retirer la lecture d'un ancien.
- `SaveService` : estampille, migrations, polymorphisme. `Serialize` ne touche ni à l'horloge ni à
  `LastSaveTime` : c'est ce qui permet d'exiger un round-trip identique octet pour octet.
- `PolymorphicRegistry` : table unique discriminant → type, écrite `"$kind": "Hero"`.
- Une sauvegarde illisible lève `SaveCorruptedException`. Au démarrage, `GameSession` la met de côté
  (`autosave.corrupted.sav`) au lieu de l'écraser.

## Migrations

Renommer, déplacer ou retyper un champ persisté demande une migration :

```csharp
public sealed class RenameMoneyToGold : ISaveMigration
{
    public string IntroducedIn => "0.2.0";   // version qui a introduit le changement

    public void Apply(JsonObject root)
    {
        if (root["Money"] is { } money && !root.ContainsKey("Gold"))
        {
            root.Remove("Money");
            root["Gold"] = money;
        }
    }
}
```

L'ajouter à `SaveService.DefaultMigrations`. Elle s'applique aux sauvegardes estampillées d'une
version strictement inférieure (et à celles sans version), dans l'ordre de la liste. Une migration
ne se retire jamais. Ajouter un champ avec une valeur par défaut ne demande rien : le JSON absent
garde la valeur du constructeur.

## Les cinq niveaux de tests

| Niveau | Fichier | Ce qu'il prouve |
|---|---|---|
| 1. Round-trip | `Saves/SaveRoundTripTests.cs` | Export → import → export redonne le même JSON ; une partie rechargée continue exactement comme l'originale |
| 2. Codec | `Saves/SaveCodecTests.cs`, `SaveMigrationTests.cs` | Chaque format jamais écrit reste lisible ; une save abîmée lève une erreur explicite ; les migrations s'appliquent au bon moment |
| 3. Fixtures `current` | `Saves/SaveFixtureTests.cs` | Les sauvegardes des scénarios se relisent avec le code actuel, et la partie reprend depuis chacune |
| 4. Fixtures `release-X.Y` | `Saves/SaveFixtureTests.cs` | Les sauvegardes des versions livrées se chargent et tournent une minute sans erreur |
| 5. Invariants | `Invariants/InvariantTests.cs` | Enums par nom, registre complet, ordre du pipeline, clés de traduction |

### Scénarios et fixtures `current`

Un scénario (`Test.Tests/TestUtilities/Scenarios.cs`) est une suite d'étapes nommées. L'étape N charge
la fixture de l'étape N−1, joue jusqu'à sa condition, vérifie le round-trip et produit la fixture N.
Chaque fixture est aussi un point de reprise pour déboguer : la copier dans le dossier de données
du bureau sous le nom `autosave.sav` pour l'ouvrir en jeu.

Les fixtures sont lues et écrites dans `HeroesOfIdlestan.Test.Tests/Saves/` (les sources, pas `bin/`), pour
être versionnées avec le code.

**Régénérer `current`** — action volontaire, jamais déclenchée par « lancer tous les tests » :

```powershell
$env:GAME_MANUAL_TESTS=1; dotnet test HeroesOfIdlestan.Test.Tests --filter "FullyQualifiedName~Rebuild_Current_Saves"
```

À faire quand `GameVersion.Current` change, quand un scénario change, ou quand une règle de jeu
change ce que les scénarios produisent. Une fixture dont la version estampillée diffère de
`GameVersion.Current` fait échouer les tests, avec cette commande dans le message. La génération
est déterministe : deux régénérations sans changement de code donnent des fichiers identiques.

### Fixtures `release-X.Y`

À chaque sortie de version :

```powershell
Copy-Item -Recurse HeroesOfIdlestan.Test.Tests\Saves\current HeroesOfIdlestan.Test.Tests\Saves\release-0.1
```

Ces dossiers sont **figés à vie** : `SaveFixtures.Write` lève sur tout dossier `release-*`.
**Ne jamais modifier une fixture de release pour faire passer un test** : c'est le code qui doit
continuer à lire les sauvegardes des joueurs, au besoin via une migration.

## Écrire un nouveau test de sauvegarde

- Un nouveau champ persisté : rien à faire de plus, le round-trip le couvre s'il est atteint par
  un scénario. Sinon, enrichir un scénario.
- Un nouveau format d'encodage : un cas dans `SaveCodecTests` avec un échantillon littéral de
  l'ancien format.
- Une migration : un test dans `SaveMigrationTests` partant du JSON d'avant.
