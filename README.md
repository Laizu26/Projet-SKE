# Projet SKE — Chroniques de Valdor

RPG mobile Android en C# (.NET MAUI). L'interface est volontairement minimaliste, dans le style des vieux FC Manager : du texte, des listes, des barres et des boutons, sans graphismes.

## Récupérer l'APK (sans rien installer)

À chaque modification poussée sur GitHub, l'APK est compilé automatiquement.

1. Sur GitHub, ouvre l'onglet **Actions**, puis clique sur la dernière exécution de **Compiler l'APK** (coche verte).
2. En bas de la page, dans **Artifacts**, télécharge **ProjetSKE-apk** (un .zip qui contient l'APK).
3. Copie l'APK sur ton téléphone, ouvre-le et autorise l'installation d'applications de sources inconnues.

## Compiler soi-même (Visual Studio)

Visual Studio remplace Android Studio pour le C#.

1. Installe **Visual Studio 2026 Community** (gratuit) avec la charge de travail **Développement d'applications multiplateformes .NET (.NET MAUI)**.
2. Ouvre `ProjetSKE.sln`.
3. Choisis le projet **ProjetSKE.App**, puis un émulateur Android ou ton téléphone branché en USB (avec le mode développeur activé).
4. Clique sur ▶ pour lancer le jeu. Pour obtenir un APK : clic droit sur ProjetSKE.App, puis **Publier**.

En ligne de commande, avec le SDK .NET 10 installé :

```
dotnet workload install maui-android
dotnet publish src/ProjetSKE.App/ProjetSKE.App.csproj -f net10.0-android -c Release -o out
dotnet test tests/ProjetSKE.Core.Tests
```

## Ce qui est en place

| Écran | Contenu |
|---|---|
| **Titre** | Nouvelle partie, charger une partie, 3 emplacements de sauvegarde |
| **Sélection** | Choix du personnage de départ (1 héros pour l'instant, on peut en ajouter d'autres) |
| **Camp** | Titulaires et réserve, fiche de chaque personnage (stats, niveau, XP, compétences), équipement (arme, armure, relique), sac commun |
| **Carte** | Vue du lieu actuel (ville : auberge, boutique, habitants ; nature : explorer, combat fixe), puis vue du pays avec la liste des destinations |
| **Encyclo** | Personnages, monstres, lieux, armes et reliques rencontrés (les autres restent invisibles) |
| **Shop** | Uniquement en ville : achat et revente (à moitié prix) |
| **Journal** | Page blanche où l'on écrit librement, sauvegardée avec la partie |
| **Menu** | Sauvegarde, paramètres, retour au titre |
| **Combat** | Par-dessus tout : barres de vie en haut, déroulé du combat au milieu, Attaque / Objet / Fuite en bas. Tour par tour selon la vitesse |
| **Histoire** | Dialogues par-dessus tout, avec choix de réponse (recruter un personnage, recevoir de l'or, lancer un combat…) |

### Paramètres modifiables (Menu)

- **Combats en voyage** : aucun, aléatoires, fixes (histoire), ou les deux.
- **Défaite** : retour à la dernière ville (avec une perte d'or) ou game over.
- **Fuite** : toujours réussie, selon la vitesse, ou impossible. Les boss empêchent toujours la fuite.

### Reliques

Une relique est un **objet unique**. Elle peut être **équipable**, pour un bonus permanent, ou servir d'**objet de quête**, sans effet en combat. Ce choix se règle avec `RelicUsage` dans le contenu.

## Organisation du code

```
src/ProjetSKE.Core/          Moteur du jeu (C# pur, testable, sans interface)
  Models/                    Définitions : compétences, objets, personnages, monstres, lieux, dialogues
  Data/SampleContent.cs      ← TOUT LE CONTENU DU JEU (à modifier pour ajouter des éléments)
  State/GameState.cs         Ce qui est sauvegardé
  Systems/                   Règles : partie, combat, dialogues, sauvegarde
src/ProjetSKE.App/           Application Android (.NET MAUI)
  Pages/                     Titre, emplacements, sélection, écran de jeu
  Views/                     Onglets, combat, dialogue
  Ui/UiKit.cs                Couleurs et briques d'interface
tests/ProjetSKE.Core.Tests/  Tests automatiques du moteur
```

### Ajouter du contenu

Tout se passe dans `src/ProjetSKE.Core/Data/SampleContent.cs` :

- **Un personnage recrutable** : ajoute-le à `Characters()`, puis crée un dialogue avec le choix `Recruit("id")`. Pour qu'il soit proposé au départ, mets `IsStarter = true`.
- **Un monstre** : ajoute-le à `Monsters()`, puis place-le dans les `RandomEncounters` d'un lieu.
- **Un lieu** : ajoute-le à `Locations()` et relie-le aux autres avec `ConnectedIds`, dans les deux sens.
- **Un objet** : ajoute-le à `Items()`. Pour qu'il soit vendu dans une ville, ajoute-le aux `ShopItemIds` de cette ville.

Le test `SampleContent_IsValid` vérifie que toutes les références existent (par exemple, qu'un monstre placé dans un lieu est bien défini).
