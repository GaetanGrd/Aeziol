# Confidentialité et données locales

Aeziol est conçu comme un outil local. Il n’exploite aucun service de télémétrie Aeziol et n’envoie pas de rapport de crash automatiquement.

## Données enregistrées

| Emplacement | Contenu | Suppression |
|---|---|---|
| `%LOCALAPPDATA%\Aeziol\settings.json` | préférences, chemins techniques et identifiants de sorties audio | réinitialisation depuis l’application ou suppression manuelle après fermeture |
| `%LOCALAPPDATA%\Aeziol\settings.json.backup` | génération précédente des réglages | suppression manuelle après fermeture |
| `%LOCALAPPDATA%\Aeziol\route-transaction.json` | instantané temporaire nécessaire à une restauration audio | supprimé par Aeziol uniquement après résolution de la transaction |
| `%LOCALAPPDATA%\Aeziol\logs` | événements techniques rotatifs | bouton de l’application ou suppression manuelle |
| `%LOCALAPPDATA%\Aeziol\updates` | dernier MSIX téléchargé et vérifié à la demande | suppression manuelle après fermeture |
| Gestionnaire d’informations d’identification Windows, `Aeziol/DiscordOAuth` | jetons OAuth Discord | action Révoquer/oubli local dans les réglages |

Les journaux sont limités et rotatifs. Ils passent par un assainissement qui masque notamment les jetons, adresses, chemins utilisateur et identifiants courants. Cette protection ne remplace pas une relecture humaine avant publication.

## Données Discord

Aeziol demande les scopes `rpc` et `rpc.voice.read` afin de connaître l’état de la connexion vocale locale. Les identifiants de serveur et de salon ne sont ni transmis au cœur de routage ni écrits dans les journaux. Aeziol ne lit pas les messages et ne peut pas en envoyer au nom de l’utilisateur.

L’échange OAuth avec Discord est la seule communication réseau nécessaire à l’autorisation et au renouvellement du jeton. Les événements vocaux sont reçus par le RPC local de Discord.

## Mises à jour

Aeziol consulte l’API publique de GitHub au démarrage et lors d’une recherche manuelle afin de comparer les releases du canal choisi. Aucun jeton GitHub ni donnée de configuration n’est envoyé. Un MSIX est téléchargé depuis GitHub uniquement après une action de l’utilisateur, puis vérifié avec le checksum SHA-256 publié dans la même release.

## Musique d’ambiance

« Onde dorée » est embarquée dans l’application et lue localement. Sa lecture ne contacte ni Suno ni un service Aeziol.

## Désinstallation

La désinstallation du MSIX peut ne pas supprimer toutes les données placées dans `%LOCALAPPDATA%\Aeziol` ou le Gestionnaire d’informations d’identification Windows. Pour un effacement complet, révoquez Discord depuis Aeziol, fermez l’application, puis supprimez le dossier local restant.
