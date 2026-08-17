# Historique des changements

Ce fichier suit les principes de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et le versionnement sémantique.

## [Non publié]

### Ajouté

- mise à jour intégrée depuis les releases GitHub avec canaux Stable et Bêta ;
- téléchargement MSIX limité à GitHub et vérifié par SHA-256 avant installation ;
- choix explicite de la musique d’ambiance pendant le premier lancement.

### Modifié

- crédit de la musique « Onde dorée » attribué uniquement à Suno dans l’interface et la documentation.

### Prévu

- retours de la bêta publique et corrections de compatibilité Windows 11 ;
- extension progressive des règles au-delà de Discord.

## [0.9.0-beta.1] — 2026-08-17

### Ajouté

- passage audio Discord vers une sortie Windows choisie ;
- restauration transactionnelle durable et récupération après crash ;
- OAuth Discord local avec PKCE et stockage dans les identifiants Windows ;
- interface Passage, Règles et Réglages avec thèmes et animations réduites ;
- localisation anglaise, française et arabe ;
- musique d’ambiance facultative « Onde dorée » ;
- instance unique, gestion globale des erreurs et journaux censurés ;
- construction MSIX, CI et préparation automatisée des drafts GitHub.

### Sécurité

- aucune conservation des identifiants de serveur ou de salon vocal ;
- rotation et assainissement des journaux avant partage ;
- priorité systématique aux changements audio manuels de l’utilisateur.

[Non publié]: https://github.com/GaetanGrd/Aeziol/compare/v0.9.0-beta.1...HEAD
[0.9.0-beta.1]: https://github.com/GaetanGrd/Aeziol/releases/tag/v0.9.0-beta.1
