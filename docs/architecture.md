# Architecture

Aeziol sépare l’observation, la décision et l’effet système afin que le cœur transactionnel reste testable sans Discord ni modification audio réelle.

## Projets

- `Aeziol.App` : cycle de vie WPF, paramètres, localisation, musique, notifications et composition des services ;
- `Aeziol.Core` : règles, états vocaux, orchestration du routage et journal de transaction ;
- `Aeziol.Infrastructure.Discord` : découverte des processus et pipes RPC, OAuth/PKCE et stockage sécurisé ;
- `Aeziol.Infrastructure.Windows` : inventaire Core Audio et changement des rôles de sortie Windows ;
- `Aeziol.Tests` : tests du cœur, de la persistance, de Discord et des composants WPF ;
- `Aeziol.Probe` : observations en lecture seule pour le diagnostic local.

## Parcours audio

1. L’observateur Discord traduit les événements RPC en présence vocale sans exposer les identifiants de salon au cœur.
2. Le coordinateur agrège les sources actives et déclenche une règle seulement lors d’une connexion complète.
3. L’orchestrateur capture les trois sorties Windows actuelles et écrit une transaction durable.
4. Le contrôleur applique la destination puis vérifie le résultat.
5. À la fin de toutes les présences, le délai de stabilisation expire et l’instantané est restauré.
6. Si l’utilisateur change manuellement la sortie, son choix gagne et Aeziol abandonne proprement la transaction.

Le fichier de transaction ne doit être effacé qu’après restauration confirmée ou abandon explicite et sûr. Une erreur ne constitue jamais à elle seule une preuve que la transaction peut être supprimée.

## Processus et sûreté

Aeziol n’autorise qu’une instance normale. Un second lancement signale la première instance et réactive sa fenêtre. Les previews UI utilisées par les tests sont explicitement exemptées.

Les exceptions globales sont journalisées après assainissement, puis l’application demande une fermeture contrôlée afin de laisser le runtime résoudre la restauration en attente.

## Persistance

Les paramètres utilisent un schéma versionné, une sauvegarde de la génération précédente et des écritures atomiques. Une version d’Aeziol plus ancienne refuse d’écraser un schéma plus récent.

## Frontières techniques

Le changement de sortie globale repose sur `IPolicyConfig`, une interface Windows non documentée. Toute évolution de Windows 11 doit donc être validée sur une machine réelle. Les tests automatisés ne doivent jamais appeler ce contrôleur contre la sortie audio du poste de développement.
