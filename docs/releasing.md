# Construire et préparer une release

## Validation locale

Depuis `main`, avec un worktree propre et au moins un commit :

```powershell
.\packaging\publish-beta.ps1 -Version 0.9.0-beta.1
```

La compilation et les tests utilisent `.aez-local\publish-validation`. Ils n’écrasent donc pas les DLL d’une instance Aeziol déjà ouverte.

## Draft GitHub

```powershell
.\packaging\publish-beta.ps1 -Version 0.9.0-beta.1 -Publish
```

La commande :

1. valide le format et la révision bêta ;
2. exige `main` et un worktree propre ;
3. restaure, compile et exécute tous les tests ;
4. pousse `main` ;
5. crée et pousse le tag annoté ;
6. déclenche `.github/workflows/release.yml` ;
7. construit un MSIX autonome et son checksum SHA-256 ;
8. crée une GitHub Release en **draft** et **prerelease**.

Une draft n’est pas visible comme release publique. Elle doit être testée, relue et signée avant publication manuelle.

Le système de mise à jour intégré ne voit que les releases publiées. Il attend exactement les deux artefacts suivants, où `<version>` correspond au tag sans le préfixe `v` :

- `Aeziol-<version>-x64.msix` ;
- `Aeziol-<version>-x64.msix.sha256`.

Une release stable doit utiliser un tag `vX.Y.Z` et ne pas être marquée prerelease. Une bêta utilise `vX.Y.Z-beta.N` et doit être marquée prerelease. Toute incohérence est ignorée par le client.

## Signature MSIX

Le workflow signe le package lorsque ces secrets GitHub sont présents :

- `AEZIOL_SIGNING_CERTIFICATE_BASE64` : PFX encodé en Base64 ;
- `AEZIOL_SIGNING_CERTIFICATE_PASSWORD` : mot de passe du PFX, si nécessaire ;
- `AEZIOL_SIGNING_PUBLISHER` : sujet exact du certificat, par exemple `CN=GaetanGrd`.

Si le certificat et l’éditeur sont tous deux absents, le workflow produit une draft non signée destinée uniquement à la validation. Une configuration partielle provoque un échec explicite.

L’identité `GaetanGrd.Aeziol` et l’éditeur du certificat doivent rester identiques entre les versions pour que Windows reconnaisse une mise à jour et préserve les données utilisateur.

## Versionnement

- tag public : `v0.9.0-beta.1` ;
- version affichée : `0.9.0-beta.1` ;
- version MSIX : `0.9.0.1`.

Les révisions bêta acceptées vont de 1 à 65534. Une version stable utilise la révision MSIX 65535 afin de rester supérieure aux bêtas du même triplet sémantique.

## Vérification avant publication

- les checks CI sont verts ;
- le MSIX est signé par l’éditeur attendu ;
- le SHA-256 correspond ;
- installation propre et mise à jour depuis la bêta précédente testées ;
- détection testée depuis les canaux Stable et Bêta, puis téléchargement et ouverture de l’installateur validés ;
- première autorisation Discord, entrée/sortie de vocal et restauration après redémarrage testées ;
- aucun secret ni chemin local dans les notes ou artefacts ;
- changelog et numéro de version à jour.
