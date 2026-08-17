# Audit produit, UX et réactivité — Aeziol

> [!NOTE]
> Ce document est un instantané historique ayant guidé la refonte du 16 août 2026. Les écrans, valeurs et nombres de tests qu’il décrit peuvent avoir évolué ; le code, le README et le changelog constituent les références actuelles.

Date de l'audit : 16 août 2026
Portée : état réel du MVP .NET 10 / WPF, parcours actuels, robustesse, réactivité, besoins futurs et architecture de la refonte.
Contrainte créative : les notes d'Elgo ont uniquement servi de référence en lecture seule.

## 1. Conclusion exécutive

Le cœur du MVP est sain : il distingue les états vocaux utiles, applique une route Windows pour les trois rôles audio, vérifie le résultat, restaure la route exacte et abandonne sa transaction si l'utilisateur choisit manuellement une autre sortie. Le RPC Discord officiel fonctionne avec OAuth et PKCE.

En revanche, l'application visible ne traduit pas cette qualité : la fenêtre actuelle est un long formulaire de paramètres, elle mélange l'action quotidienne, la configuration rare et le diagnostic, exige un bouton Enregistrer, n'indique pas clairement si Discord est autorisé et ne permet pas de révoquer l'autorisation. Le moteur contient aussi des bases pour les règles futures, mais elles ne sont pas reliées au runtime ni à l'interface.

La refonte doit donc conserver le cœur transactionnel et reconstruire entièrement la couche applicative et visuelle autour de trois espaces seulement : **Passage**, **Règles**, **Réglages**. L'écran Passage doit répondre en moins d'une seconde à la question « que va faire Aeziol maintenant ? ».

## 2. Inventaire fonctionnel réel

| Domaine | État actuel | Verdict |
|---|---|---|
| Discord Stable, PTB, Canary, Development | Détection des quatre éditions et de plusieurs processus | Présent |
| État vocal réel | RPC local, sélection initiale puis événements `VOICE_CHANNEL_SELECT` et `VOICE_CONNECTION_STATUS` | Présent |
| OAuth desktop | Client public, PKCE, jeton et renouvellement dans le Gestionnaire d'informations d'identification Windows | Présent |
| Autorisation Discord | Bouton uniquement quand le runtime estime qu'elle manque | Partiel : aucun état explicite, aucune révocation |
| Entrée en vocal | Seul l'état réellement connecté active la route | Présent |
| Changement de salon / reconnexion | Continuité de session sans restauration intermédiaire | Présent et testé |
| Sortie du vocal | Délai de grâce configurable, 4 s par défaut, puis restauration | Présent |
| Routage Windows | Console, Multimedia et Communications, capture exacte par rôle | Présent |
| Transaction | Journal durable, application, vérification, rollback, récupération après crash | Présent et testé |
| Choix manuel de l'utilisateur | Une modification manuelle gagne et abandonne la transaction Aeziol | Présent et testé |
| Périphériques exclus | Liste d'identifiants d'endpoints exacts | Présent mais fragile après réinstallation |
| Disparition d'un périphérique | Contrôlée au déclenchement et à la restauration | Partiel : pas de réaction immédiate aux événements ajout/retrait/état |
| Plusieurs règles / priorités | Modèle et moteur de conflit isolés | Préparé mais non branché au runtime |
| Applications autres que Discord | Interfaces de cœur séparées | Architecture favorable, aucun connecteur concret |
| Zone de notification | Ouvrir / quitter, double-clic | Présent, avec icône Windows générique |
| Démarrage Windows | StartupTask MSIX ou clé utilisateur hors package | Présent |
| Localisation | Français, anglais, arabe, LTR/RTL, fichiers communautaires, repli standard/fantastique | Présent |
| Journaux | JSONL, rotation à 1 Mio, cinq archives | Présent ; ouverture du dossier seulement, pas d'export guidé |
| Notifications utilisateur | État dans la fenêtre et MessageBox | Minimal, aucun modèle cohérent de notification |

## 3. Audit des états et des cas limites

### États correctement couverts

- Discord absent, lancé hors vocal, connexion, connecté, changement de salon, reconnexion et déconnexion.
- Plusieurs sessions RPC : la sortie n'est restaurée qu'après la fin de toutes les présences actives.
- Entrées/sorties rapides : le délai de grâce annule une restauration prématurée.
- Échec du basculement : restauration de l'instantané initial et conservation du journal si le rollback échoue.
- Crash d'Aeziol : proposition de récupération seulement si la route actuelle correspond toujours à la cible et que les sources existent encore.
- Changement manuel : Aeziol ne combat pas l'utilisateur et ne restaure pas ensuite par surprise.

### Lacunes à corriger

1. Le retrait, l'ajout et le changement d'état d'un endpoint sont ignorés par l'observateur Windows ; seule la sortie par défaut est écoutée.
2. La liste visible des sorties est chargée une fois à l'ouverture et ne se met pas à jour à chaud.
3. Un endpoint réinstallé peut recevoir un nouvel identifiant. Le `ContainerId` est déjà lu mais n'est pas utilisé pour retrouver une destination ou une exclusion.
4. La destination devenue indisponible pendant une session n'entraîne ni état produit clair ni parcours de réparation.
5. Le moteur de priorités n'est jamais appelé : le runtime fabrique une règle Discord fixe à chaque activation.
6. Le statut « autorisé » n'est pas un état applicatif de premier ordre.
7. Une erreur d'authentification RPC avant la création d'une session efface parfois le jeton trop largement ; il faut distinguer jeton rejeté, pipe indisponible et panne transitoire.
8. La fermeture depuis la fenêtre peut effectuer des opérations asynchrones avec `GetResult()` sur le thread UI.

## 4. Audit de réactivité

### Observations mesurées

- Package MSIX actuel : **78,42 Mio**.
- Publication autonome non compressée : **197,51 Mio**, 484 fichiers.
- Sur cette machine, la sonde du moniteur Discord a observé le mode de repli **LowFrequencyScan**, Discord Stable actif avec six processus.
- Le journal réel confirme un basculement puis une restauration fonctionnels après l'autorisation OAuth.
- Les 41 tests existants couvrent surtout le cœur transactionnel, les transitions vocales, OAuth/PKCE, le framing RPC, les processus, les règles, les paramètres et la localisation.

### Goulots identifiés dans le code

| Cause | Impact utilisateur | Correction retenue |
|---|---|---|
| La fenêtre est créée après l'initialisation du runtime | Impression de lancement lent, aucun retour immédiat | Afficher la coquille tout de suite, initialiser en arrière-plan avec un état calme |
| Réconciliation Discord toutes les 2 s | Réveils CPU permanents et délai possible au démarrage/redémarrage | Boucle réveillable par événement, timer seulement comme filet de sécurité |
| Les événements du moniteur de processus ne réveillent pas l'observateur | Le mécanisme événementiel existe mais n'améliore pas la réaction | Abonner `ProcessChanged` et déclencher une réconciliation coalescée immédiate |
| Repli processus toutes les 2 s | CPU inutile quand WMI n'est pas disponible | Repli adaptatif lent au repos, immédiat après changement connu |
| Jusqu'à dix pipes testés séquentiellement, 300 ms chacun | Premier raccordement potentiellement long | Découverte concurrente bornée, mémorisation du dernier pipe valide |
| Reconnexion : délai fixe de 10 s | Discord revenu mais Aeziol paraît bloqué | backoff court 250 ms → 1 s → 2 s → 5 s, réinitialisé par événement processus |
| Échec d'autorisation : attente de 2 min | Réparation incompréhensiblement lente | différencier auth absente/invalide/réseau ; action utilisateur réessaie immédiatement |
| Énumération Core Audio synchrone appelée depuis `Loaded` | Micro-blocage de l'UI | service d'endpoints sur thread dédié + cache mis à jour par événements |
| Toute la page dépend d'Enregistrer | L'action simple semble lourde | autosave débouncé et retour d'état local immédiat |
| Écriture durable en trois phases avant/après routage | Quelques E/S nécessaires sur le chemin critique | conserver la sûreté ; mesurer, puis réduire seulement les écritures redondantes |
| Délai de sortie de 4 s | Restauration volontairement différée | le présenter comme « stabilisation » et ne pas le confondre avec une lenteur |

### Budgets d'acceptation après refonte

- Fenêtre visible et interactive : **< 400 ms** sur la machine de référence.
- État local initial (paramètres + cache endpoints) : **< 250 ms** après affichage.
- Entrée en vocal, session RPC déjà établie : routage commencé **< 250 ms** après l'événement.
- Retour visuel d'un événement RPC : **< 100 ms**.
- Discord démarré après Aeziol : tentative de raccordement **< 500 ms** avec événements Windows, **< 3 s** en repli.
- CPU au repos : **< 0,2 %** moyen sur 5 minutes.
- Mémoire privée au repos : cible **< 90 Mio**, plafond **120 Mio**.
- Pas plus d'un réveil périodique toutes les **30 s** lorsqu'une session RPC stable est ouverte ; idéalement aucun.
- Package : ne pas dépasser le volume actuel ; viser **< 75 Mio** sans sacrifier l'autonomie .NET.

## 5. Audit de l'interface actuelle

### Problèmes structurels

- Une fenêtre de 760 × 860 px avec défilement transforme un utilitaire rapide en page de configuration.
- Le statut, la destination, les exclusions, la langue, le registre, le Client ID, le démarrage, la fermeture et les logs ont tous le même poids visuel.
- Le Client ID officiel est exposé comme un réglage ordinaire alors qu'il ne doit pas être modifié dans le parcours normal.
- Les exclusions sont une nuée de cases sans contexte ; elles devraient appartenir à une règle.
- Le bouton Enregistrer retarde une action qui devrait sembler instantanée.
- Les cartes imbriquées, pastilles décoratives, lettres de remplacement et ellipses « ••• » donnent un aspect de tableau de bord générique.
- L'utilisateur ne voit pas clairement : la source actuelle, la sortie actuelle de Windows, la sortie cible, ce qu'Aeziol a changé, ni ce qu'il restaurera.
- Autoriser Discord est présenté comme un incident, pas comme une relation maîtrisée pouvant être retirée.

### Ce qu'il faut conserver

- Le noir chaud, l'or et le contraste élevé.
- La concision des états et le détail technique réservé aux journaux.
- Les deux registres rédactionnels et le RTL.
- Une petite fenêtre, la zone de notification et l'absence de chrome superflu.

## 6. Besoins actuels et futurs traduits en architecture UX

### Espace 1 — Passage

Écran quotidien et écran d'ouverture. Il contient uniquement :

- l'interrupteur global Aeziol ;
- l'état Discord / vocal ;
- une représentation directe **Discord → passage lumineux → sortie cible** ;
- le sélecteur de destination ;
- la sortie Windows qui sera restaurée, seulement lorsqu'une transaction existe ;
- une action contextuelle unique : connecter Discord, réparer une cible absente ou ouvrir le détail utile.

Il ne doit pas défiler à sa taille nominale.

### Espace 2 — Règles

Le MVP affiche une règle « Vocal Discord » réellement éditable. La structure accepte ensuite plusieurs règles sans refaire l'application :

- déclencheur / application ;
- destination ;
- exclusions liées à cette règle ;
- priorité ;
- activation ;
- état de conflit explicite.

Au MVP, les contrôles multi-règles non disponibles ne sont pas simulés : la structure est prête, mais l'interface ne promet pas une fonction absente.

### Espace 3 — Réglages

Configuration peu fréquente :

- connexion Discord, état de l'autorisation, **Révoquer l'autorisation** ;
- démarrage Windows et comportement de fermeture ;
- langue et registre ;
- délai de stabilisation avancé ;
- journaux et diagnostic ;
- informations/version.

### Navigation retenue

Une bande verticale très fine à gauche porte le symbole Aeziol puis trois destinations. Le contenu reste sur un seul niveau. Sur une fenêtre étroite ou en RTL, la bande change de côté. Pas de menu hamburger, pas de sous-navigation permanente, pas de mosaïque de cartes.

## 7. Identité visuelle issue d'Aeziol

Référence canonique retenue, sans recopier la fiche : Aeziol est une cigale Lumalis consciente, joyeuse et expressive, dont le corps paraît fait de lumière dorée. Elle choisit des sources sonores, en reproduit fidèlement le son, crée plusieurs projections réceptrices et les déplace comme les extensions d'une même conscience. Elle ne transporte pas la magie du son.

Traduction visuelle :

- une cigale immédiatement reconnaissable, pas un glyphe abstrait ;
- un noyau lumineux central et deux ailes comme deux voies de circulation ;
- de petites projections seulement quand elles expliquent une source ou une destination ;
- une ligne continue et calme, jamais un rayon agressif ;
- asymétrie légère dans les compositions, symétrie stricte dans l'icône ;
- profondeur obtenue par lumière et opacité, pas par une accumulation de cadres.

Palette de départ :

- fond : `#090A0C` ;
- surface : `#101216` ;
- texte : `#F4F0E7` ;
- texte secondaire : `#AAA69E` ;
- or principal : `#DEBD68` ;
- or lumineux : `#F4D98A` ;
- succès discret : `#86C7A5` ;
- danger : `#E08A82`.

Le mouvement doit avoir une fonction : progression le long du passage pendant un changement, respiration très lente quand Aeziol veille, retour inverse lors de la restauration. Toute animation respecte la préférence Windows de réduction des animations.

## 8. Logo et déclinaisons

Le dessin maître sera un SVG carré, géométrie simple, traits arrondis et silhouette lisible sans texte. Déclinaisons obligatoires :

1. symbole couleur sur fond sombre ;
2. symbole monochrome ;
3. version trait seul pour l'interface ;
4. PNG transparent 1024 px pour Discord ;
5. ICO multi-tailles 16, 20, 24, 32, 48 et 256 px pour l'exécutable, la barre des tâches et la zone de notification ;
6. rasterisations MSIX adaptées, générées depuis la même source.

Les détails secondaires disparaîtront aux petites tailles plutôt que d'être simplement réduits.

## 9. Révocation Discord

Le parcours doit distinguer deux actions :

- **Révoquer** : appeler le point de terminaison OAuth de révocation avec le jeton, fermer les sessions RPC, effacer le credential local et repasser à l'état « Discord non connecté » ;
- **Oublier localement** : secours explicite si Discord refuse ou si le réseau est indisponible, qui efface seulement le credential sur ce PC et explique que l'autorisation distante peut rester visible dans Discord.

La révocation est destructive mais réversible par une nouvelle autorisation ; elle demande une confirmation courte. Aucun jeton ne doit apparaître dans l'UI, les logs ou les exceptions.

## 10. Architecture applicative cible

La refonte ne remplace pas le cœur transactionnel ; elle remplace le couplage actuel entre fenêtre, paramètres et runtime.

- `AppState`: instantané observable et immuable de l'état produit.
- `DiscordConnectionService`: absent / raccordement / autorisation requise / prêt / erreur, avec réveil événementiel.
- `AudioEndpointCatalog`: cache, événements d'ajout/retrait/état/défaut et résolution par identifiant + identité matérielle de secours.
- `RuleService`: source unique des règles et appel réel au moteur de priorités.
- `RoutingSessionService`: façade du transactionnel existant, expose source, cible, restauration et résultat.
- `SettingsService`: écritures atomiques auto-enregistrées et débouncées.
- `MainViewModel`: projection UI sans logique Core Audio ni RPC.
- vues `Passage`, `Rules`, `Settings`, avec composants partagés accessibles.

Les services ne publient pas directement sur le thread UI. Les événements rapides sont coalescés, mais aucune transition sémantique n'est perdue.

## 11. Priorités d'implémentation

1. Ajouter les états produit et mesures de latence sans toucher au comportement audio sûr.
2. Rendre la supervision Discord événementielle et fiabiliser la classification des erreurs OAuth/RPC.
3. Ajouter le catalogue d'endpoints à chaud et l'identité de secours.
4. Ajouter la révocation Discord et ses tests HTTP / stockage / runtime.
5. Introduire l'état observable et les ViewModels.
6. Créer le logo maître et ses exports.
7. Supprimer les trois fenêtres visuelles actuelles et reconstruire Passage, Règles, Réglages et l'accueil initial.
8. Valider clavier, lecteur d'écran, contrastes, 100–200 %, LTR/RTL et textes longs.
9. Mesurer démarrage, CPU, mémoire, latence vocale et taille avant/après.
10. Recréer le MSIX puis personnaliser l'application Discord avec le même logo.

## 12. Critères de sortie

La refonte est livrable seulement si :

- l'écran principal est compréhensible sans ouvrir Réglages ;
- la fenêtre nominale ne défile pas ;
- une destination se change sans bouton Enregistrer ;
- les endpoints apparaissent/disparaissent sans redémarrer ;
- l'autorisation Discord peut être révoquée depuis l'application ;
- le premier lancement n'affiche jamais deux demandes d'autorisation pour une même connexion valide ;
- l'entrée et la sortie d'un vocal respectent les budgets de réactivité ;
- la modification manuelle de Windows gagne toujours ;
- les scénarios transactionnels existants restent verts ;
- le français, l'anglais et l'arabe sont testés, y compris le RTL ;
- le logo est lisible aux tailles Windows requises ;
- l'icône est cohérente dans la fenêtre, la barre des tâches, le tray, le MSIX et Discord ;
- aucun secret, jeton, identifiant de salon ou de serveur n'est journalisé.
