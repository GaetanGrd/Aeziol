# Politique de sécurité

## Versions prises en charge

Pendant la bêta, seule la dernière version publiée reçoit des correctifs de sécurité. Les builds de développement et les anciennes bêtas ne sont pas pris en charge.

## Signaler une vulnérabilité

N’ouvrez pas d’issue publique contenant une vulnérabilité, un jeton, un journal non relu ou une méthode d’exploitation.

Utilisez de préférence un [avis de sécurité privé GitHub](https://github.com/GaetanGrd/Aeziol/security/advisories/new). Incluez :

- la version d’Aeziol et de Windows ;
- les étapes minimales de reproduction ;
- l’impact attendu ;
- une preuve de concept expurgée si elle est nécessaire ;
- les journaux uniquement après vérification manuelle de leur contenu.

Le mainteneur confirmera la réception, évaluera l’impact et coordonnera la publication du correctif. Aucun délai fixe n’est garanti pendant la bêta, mais les signalements permettant un accès non autorisé, une fuite de jeton ou une modification audio persistante sont prioritaires.

## Périmètre sensible

- stockage OAuth dans le Gestionnaire d’informations d’identification Windows ;
- échanges RPC locaux avec Discord ;
- validation des trames et des redirections OAuth ;
- fichiers de configuration, sauvegarde et transaction de restauration ;
- signature et identité des packages MSIX ;
- censure des journaux avant partage.

Les recherches doivent rester sur vos propres comptes et machines. Ne testez pas une vulnérabilité sur les utilisateurs, l’infrastructure Discord ou un système sans autorisation.
