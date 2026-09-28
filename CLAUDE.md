# Projet SKE — règles de travail

## Une seule appli : téléphone (Android) et PC (Windows)

Le jeu existe sur téléphone et sur PC. C'est **le même projet** (`src/ProjetSKE.App`), compilé pour deux cibles
(`net10.0-android` et `net10.0-windows10.0.19041.0`). Les deux doivent **toujours** avoir exactement les mêmes
fonctionnalités, le même contenu en ligne et le même numéro de version.

Règles, sans exception :

- **Toute fonctionnalité va dans le code commun** (`src/ProjetSKE.Core`, et `src/ProjetSKE.App` hors `Platforms/`).
  Ne jamais ajouter quelque chose « seulement pour le téléphone » ou « seulement pour le PC ».
- Le code propre à une plateforme (`#if ANDROID` / `#if WINDOWS`, dossier `Platforms/`) ne sert qu'à brancher une
  différence technique (installation d'une mise à jour, journal, taille de fenêtre...). Chaque `#if ANDROID` a son
  équivalent pour Windows (branche `#else` ou `#elif WINDOWS`) qui rend le même service.
- Le test automatique (`src/ProjetSKE.App/Dev/AutoTest.cs`) tourne sur les deux (émulateur Android et Windows).
  Chaque nouvel écran ou fonctionnalité y ajoute une étape.
- Une version n'est publiée (Release GitHub : `ProjetSKE.apk` + `ProjetSKE-Windows.zip`) que si **les deux**
  compilent et passent le test (`.github/workflows/build-apk.yml`, job `release` : `needs: [build, emulator-test, windows]`).

- Les écrans s'adaptent à la largeur : sur grand écran, le contenu reste dans une colonne centrée
  (`Ui/Responsive.cs`, appliqué à toutes les pages par `SkeApp.GoTo`). Toute interaction doit marcher au toucher
  ET à la souris (pas de geste seul : ajouter des boutons, comme les flèches du choix des héros).

## Vérifier avant de pousser

- Tests du moteur : `dotnet test tests/ProjetSKE.Core.Tests`
- Le code de l'appli se compile en local sans Android ni Windows via un projet de vérification en `net10.0`
  (tous les `.cs` de `src/ProjetSKE.App` sauf `Platforms/`). Les parties `#if ANDROID` / `#if WINDOWS` ne sont
  vérifiées que par GitHub Actions.

## Publier une mise à jour

Les pushs sont compilés et testés, mais une version n'est publiée que si le message du dernier commit contient
`[maj]` (une seule mise à jour par lot de changements), ou en lançant le workflow à la main.
