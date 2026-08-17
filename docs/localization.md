# Ajouter une langue

Aeziol utilise Fluent (`.ftl`) et le format communautaire version 1. Un fichier externe se place dans `%LOCALAPPDATA%\Aeziol\languages\<locale>.ftl` et commence obligatoirement par :

```ftl
# aeziol-language-format: 1
```

Chaque concept utilise une clé standard :

```ftl
destination-standard = Sortie audio pendant un vocal
```

La clé standard est obligatoire. Si elle manque dans la langue active, Aeziol utilise automatiquement l’anglais. Un fichier mal formé ou d’une version inconnue est refusé sans remplacer les ressources intégrées. Les termes techniques, diagnostics, journaux et noms de périphériques restent exacts. Les valeurs techniques sont isolées en LTR dans une interface RTL.

Les fichiers intégrés anglais, français et arabe se trouvent dans `src/Aeziol.App/Localization`. Fluent fournit les pluriels CLDR, les variables et l’isolation bidirectionnelle des interpolations.
