# Configuration de l’application Discord

Aeziol utilise le RPC local officiel de Discord. Il ne s’agit ni d’un bot, ni d’un selfbot : aucun token utilisateur n’est demandé et aucune donnée de salon n’est conservée.

## Application développeur

1. Créer une application dédiée dans le [Discord Developer Portal](https://discord.com/developers/applications).
2. Activer **Public Client**, car Aeziol est une application de bureau qui ne peut pas protéger un secret embarqué.
3. Déclarer l’URI de redirection `http://127.0.0.1/aeziol-discord-oauth`.
4. Demander à Discord l’accès aux scopes RPC `rpc` et `rpc.voice.read`.
5. Copier uniquement le **Client ID public** dans Aeziol. Ne jamais intégrer ni saisir le client secret.

Le Client ID public de l’application officielle Aeziol (`1538505326641414154`) est fourni par défaut avec l’application. Le jeton obtenu par l’utilisateur est stocké dans le Gestionnaire d’informations d’identification Windows sous `Aeziol/DiscordOAuth`.

## Vérification avant publication

Avant chaque release publique, vérifier que `rpc.voice.read` reste disponible pour l’application officielle et que l’échange du code d’autorisation fonctionne pour un client natif sans secret embarqué. Aeziol n’active aucun mode heuristique de remplacement si Discord refuse l’accès.

## Validation manuelle

- Discord fermé : état « Discord absent ».
- Discord Stable, PTB, Canary ou Development lancé : détection du ou des processus.
- Hors vocal : `GET_SELECTED_VOICE_CHANNEL` ne renvoie aucun salon.
- Entrée complète : seule la transition RPC `VOICE_CONNECTED` déclenche le routage.
- Changement/reconnexion : aucune restauration intermédiaire.
- Sortie complète ou fermeture de Discord : restauration après le délai de grâce.
- Lancer plusieurs éditions : chaque pipe `discord-ipc-0` à `discord-ipc-9` est observé indépendamment.

Les identifiants de salon et de serveur ne sont ni journalisés ni exposés au cœur applicatif.
