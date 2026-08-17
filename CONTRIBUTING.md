# Contribuer à Aeziol

Merci de vouloir améliorer Aeziol. Le projet touche à la sortie audio globale de Windows : une modification apparemment mineure peut interrompre le son ou empêcher une restauration. Les changements doivent donc rester petits, explicables et testés.

## Avant de commencer

- Utilisez une issue existante ou ouvrez-en une pour décrire le problème et le résultat attendu.
- Pour une vulnérabilité, n’ouvrez pas d’issue publique : consultez [SECURITY.md](SECURITY.md).
- Pour une modification importante de l’interface ou du moteur de routage, attendez l’accord du mainteneur avant une grosse implémentation.

## Environnement

- Windows 11 x64 ;
- SDK .NET défini dans `global.json` ;
- Discord Desktop uniquement si le changement concerne l’intégration réelle.

```powershell
git clone https://github.com/GaetanGrd/Aeziol.git
cd Aeziol
dotnet restore Aeziol.slnx
dotnet build Aeziol.slnx -c Release
dotnet test --solution Aeziol.slnx -c Release --timeout 60s
```

Les tests ne doivent pas ouvrir de fenêtre visible ni modifier la sortie audio de la machine. Utilisez les doubles de test existants pour le cœur transactionnel et les fenêtres WPF hors écran pour les contrôles visuels.

## Principes du projet

- Une action manuelle de l’utilisateur gagne toujours sur l’automatisation.
- Une transaction de restauration ne doit jamais être supprimée uniquement parce qu’une opération a échoué.
- La configuration locale doit survivre aux mises à jour et aux retours de version compatibles.
- Aucun jeton, chemin utilisateur, identifiant Discord ou détail réseau sensible ne doit apparaître dans les journaux.
- Les changements visuels doivent rester lisibles dans toutes les palettes et avec les animations réduites.
- Toute chaîne visible doit être ajoutée aux ressources anglaises, françaises et arabes.

## Pull request

1. Créez une branche dédiée.
2. Ajoutez ou adaptez les tests couvrant le comportement modifié.
3. Exécutez la compilation Release et tous les tests.
4. Décrivez le comportement avant/après, les risques audio et la validation manuelle effectuée.
5. Joignez des captures pour les changements d’interface.

Une pull request ne doit pas contenir de fichiers de configuration locale, de journaux, de certificats, de packages ou d’artefacts de compilation.

En soumettant une contribution, vous acceptez la concession décrite dans la section « Contributions » de [LICENSE.md](LICENSE.md). Elle permet à GaetanGrd d’intégrer et de distribuer officiellement votre travail sans ouvrir un droit général de redistribution aux autres utilisateurs.

## Traductions

Le format Fluent et le mécanisme de repli sont décrits dans [docs/localization.md](docs/localization.md). Une traduction communautaire peut être proposée sans modifier le registre technique des diagnostics.
