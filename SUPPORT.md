# Support et diagnostic

Aeziol est en bêta. Avant d’ouvrir une issue, utilisez la dernière version disponible et vérifiez les points suivants.

## Discord n’est pas détecté

1. Vérifiez que Discord Desktop est lancé.
2. Dans **Réglages → Discord**, utilisez la recherche automatique.
3. Si nécessaire, sélectionnez manuellement `Discord.exe`.
4. Révoquez puis autorisez de nouveau Discord uniquement si l’état OAuth l’exige.

## La sortie audio ne revient pas

- Ne supprimez pas `%LOCALAPPDATA%\Aeziol\route-transaction.json` manuellement.
- Utilisez **Forcer la restauration** lorsqu’Aeziol indique qu’une restauration est en attente.
- Si vous avez changé manuellement la sortie Windows, Aeziol peut abandonner sa transaction afin de respecter votre choix.

## Réglages perdus ou refusés

Les réglages sont dans `%LOCALAPPDATA%\Aeziol\settings.json` et la génération précédente dans `settings.json.backup`. Aeziol conserve un fichier utilisant un schéma plus récent au lieu de l’écraser.

## Journaux

Le bouton **Ouvrir le dossier des journaux** mène à `%LOCALAPPDATA%\Aeziol\logs`. Aeziol censure automatiquement les secrets, chemins personnels et identifiants courants, mais relisez toujours un journal avant de le joindre publiquement.

## Ouvrir une issue

Utilisez le modèle de bug et fournissez :

- la version d’Aeziol et de Windows ;
- l’édition de Discord ;
- le résultat attendu et le résultat observé ;
- les étapes de reproduction ;
- une capture sans donnée privée ;
- un extrait de journal expurgé si utile.

Pour une vulnérabilité, suivez exclusivement [SECURITY.md](SECURITY.md).
