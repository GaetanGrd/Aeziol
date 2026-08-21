# Aeziol

<p align="center">
  <img src="src/Aeziol.App/Assets/Brand/aeziol-cicada.svg" width="150" alt="Logo d’Aeziol">
</p>

<p align="center">
  <strong>Un passage audio local entre Discord et Windows.</strong>
</p>

<p align="center">
  <a href="README.md">Read in English</a>
</p>

[![CI](https://github.com/GaetanGrd/Aeziol/actions/workflows/ci.yml/badge.svg)](https://github.com/GaetanGrd/Aeziol/actions/workflows/ci.yml)

Aeziol est une application Windows 11 légère qui observe l’état vocal de Discord. Lorsqu’une connexion vocale commence, elle dirige la sortie audio globale de Windows vers la destination choisie. À la sortie complète du vocal, elle restaure exactement la route précédente.

> [!WARNING]
> Aeziol est actuellement en bêta. Le routage audio fonctionne au niveau du système : vérifiez la sortie sélectionnée avant une utilisation importante et conservez une manière simple de revenir aux réglages audio Windows.

## Fonctionnalités

- détection locale de Discord Stable, PTB, Canary et Development ;
- autorisation OAuth officielle avec PKCE et les scopes `rpc` et `rpc.voice.read` ;
- routage des rôles Windows Console, Multimedia et Communications ;
- restauration transactionnelle après la sortie du vocal, un redémarrage ou un crash ;
- priorité donnée aux changements manuels de l’utilisateur ;
- exclusions de périphériques et délai de stabilisation configurable ;
- interface localisée en anglais, français et arabe, avec prise en charge RTL ;
- thèmes Aeziol inspirés de l’univers d’Elgo ;
- mises à jour intégrées avec canaux Stable et Bêta, téléchargement vérifié par SHA-256 et installation confirmée par l’utilisateur ;
- journaux locaux rotatifs avec censure des chemins, identifiants et secrets.

Aeziol n’est ni un bot ni un selfbot. Il ne lit pas les messages, n’intercepte pas le trafic réseau de Discord et ne modifie aucun réglage audio interne à Discord.

## Prérequis

- Windows 11 x64, version 21H2 ou ultérieure ;
- Discord Desktop pour l’automatisation vocale ;
- aucune installation séparée de .NET pour le package autonome.

## Installation

Les paquets destinés aux utilisateurs seront attachés aux [releases GitHub](https://github.com/GaetanGrd/Aeziol/releases). Une release marquée **Draft** est une préparation réservée à la validation et ne doit pas être distribuée comme version publique.

Un MSIX public doit être signé. Vérifiez sa signature Windows ainsi que le fichier `.sha256` fourni avec la release avant installation.

Les bêtas de test auto-signées fournissent également un fichier `Aeziol-<version>-signing.cer`. Avant la première installation, importez ce certificat dans **Ordinateur local → Personnes de confiance**, puis vérifiez que son sujet est `CN=Aeziol Development`. Cette opération demande les droits administrateur. Le certificat de test ne remplace pas une signature publique reconnue pour une version stable.

## Données et confidentialité

Aeziol travaille localement. La configuration, les journaux et les paquets de mise à jour téléchargés restent dans `%LOCALAPPDATA%\Aeziol`. Les jetons Discord sont conservés par le Gestionnaire d’informations d’identification Windows sous `Aeziol/DiscordOAuth`. Aucun identifiant de serveur ou de salon vocal n’est écrit dans les journaux. La recherche de mises à jour consulte uniquement les releases publiques de ce dépôt GitHub.

Les détails sont documentés dans [Confidentialité et données locales](docs/privacy.md). Une autorisation Discord peut être révoquée depuis les réglages d’Aeziol.

## Développement

Prérequis : Windows 11 x64 et SDK .NET `10.0.101`, ou une version corrective compatible.

```powershell
dotnet restore Aeziol.slnx
dotnet build Aeziol.slnx -c Release
dotnet test --solution Aeziol.slnx -c Release --timeout 60s
```

La sonde de diagnostic ne modifie aucun réglage :

```powershell
dotnet run --project tools/Aeziol.Probe/Aeziol.Probe.csproj -- audio-list
dotnet run --project tools/Aeziol.Probe/Aeziol.Probe.csproj -- discord-processes
```

Structure principale :

```text
src/Aeziol.App                       Interface WPF et cycle de vie
src/Aeziol.Core                      Règles et transactions audio
src/Aeziol.Infrastructure.Discord    RPC local et OAuth Discord
src/Aeziol.Infrastructure.Windows    Core Audio Windows
tests/Aeziol.Tests                   Tests unitaires et visuels isolés
tools/Aeziol.Probe                   Diagnostics en lecture seule
packaging                            Construction et publication MSIX
```

Consultez [CONTRIBUTING.md](CONTRIBUTING.md) avant une contribution. Les vulnérabilités ne doivent pas être signalées dans une issue publique : suivez [SECURITY.md](SECURITY.md).

## Documentation

- [Architecture](docs/architecture.md)
- [Configuration de l’application Discord](docs/discord-setup.md)
- [Confidentialité et données locales](docs/privacy.md)
- [Ajouter une langue](docs/localization.md)
- [Construire et préparer une release](docs/releasing.md)
- [Historique des changements](CHANGELOG.md)
- [Support et diagnostic](SUPPORT.md)

## Préparer une bêta

Depuis `main`, avec un worktree propre et déjà commité :

```powershell
.\packaging\publish-beta.ps1 -Version 0.9.0-beta.1 -Publish
```

La commande restaure, compile et teste dans un dossier isolé, pousse `main`, crée le tag puis demande à GitHub Actions de produire une **draft prerelease** avec son checksum. Sans `-Publish`, elle effectue uniquement la validation locale.

La procédure complète et les secrets facultatifs de signature sont décrits dans [docs/releasing.md](docs/releasing.md).

## Crédits

Aeziol est créé par [GaetanGrd](https://github.com/GaetanGrd), avec l’aide d’outils d’intelligence artificielle. La direction, les choix techniques et la validation finale restent humains.

L’identité visuelle et une partie du vocabulaire sont inspirées de l’univers original **Elgo**. La musique d’ambiance « Onde dorée » a été générée avec **Suno**.

## Licence

Aeziol est **source-available**, mais n’est pas un logiciel open source. La modification et l’usage privés sont autorisés ; la redistribution du code, des versions modifiées et des builds dérivés est interdite sans autorisation écrite. Consultez [LICENSE.md](LICENSE.md).
