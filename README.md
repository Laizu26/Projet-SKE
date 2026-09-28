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

## Direction artistique

Reprise de *Service Impérial* : fond parchemin, bandeaux en pierre sombre soulignés d'un filet d'or, cartes blanches avec bandeau de titre, petits libellés en capitales espacées, titres à empattements et boutons pierre / texte or. Les icônes viennent de [Lucide](https://lucide.dev) (police `Resources/Fonts/lucide.ttf`, licence ISC). Toute la palette et les composants sont dans `src/ProjetSKE.App/Ui/UiKit.cs`.

## Ce qui est en place

| Écran | Contenu |
|---|---|
| **Titre** | Nouvelle partie, charger une partie, 3 emplacements de sauvegarde |
| **Sélection** | Choix du personnage de départ (1 héros pour l'instant, on peut en ajouter d'autres) |
| **Camp** | Titulaires et réserve, fiche de chaque personnage (stats, niveau, XP, karma, compétences), équipement, sac commun, et **le camp** : hiérarchie (grades), tâches (rondes, chasse…) et journal du camp |
| **Carte** | Vue du lieu actuel (ville : auberge, boutique, habitants ; nature : explorer, combat fixe), puis vue du pays avec la liste des destinations |
| **Quêtes** | Quêtes en cours (objectif actuel, compteur) et quêtes terminées |
| **Encyclo** | Personnages (PJ et PNJ), monstres, lieux, armes et reliques rencontrés (les autres restent invisibles) |
| **Shop** | Uniquement en ville : achat et revente (à moitié prix) |
| **Journal** | Page blanche où l'on écrit librement, sauvegardée avec la partie |
| **Menu** | Sauvegarde, paramètres, retour au titre |
| **Combat** | Par-dessus tout : l'ennemi en haut, la narration du combat (avec les répliques des combattants) au milieu, l'équipe en bas, puis Attaque / Défense / Objet / Fuite. Tour par tour selon la vitesse ; Défense divise les dégâts reçus par deux jusqu'au tour suivant |
| **Histoire** | L'écran s'assombrit, le portrait de celui qui parle apparaît, puis la boîte de dialogue ; choix de réponse (certains grisés avec la raison) |
| **Horloge** | En haut à droite : heure, moment de la journée, jour. Le temps passe en voyageant, en parlant, en combattant, à l'auberge |

### Paramètres modifiables (Menu)

- **Combats en voyage** : aucun, aléatoires, fixes (histoire), ou les deux.
- **Défaite** : retour à la dernière ville (avec une perte d'or) ou game over.
- **Fuite** : toujours réussie, selon la vitesse, ou impossible. Les boss empêchent toujours la fuite.

### Reliques

Une relique est un **objet unique**. Elle peut être **équipable**, pour un bonus permanent, ou servir d'**objet de quête**, sans effet en combat. Ce choix se règle avec `RelicUsage` dans le contenu.

## Mode développeur

Sur l'écran titre, touche **Développeur** puis entre le code **1234**.

### Éditeur (tout le contenu est modifiable)

| Catégorie | Ce qu'on règle |
|---|---|
| **PJ** | Nom, classe, description, portrait, stats de base et gain par niveau, compétences, équipement de départ, karma et amitié de départ, répliques de combat, « proposé au départ » |
| **PNJ** | Nom, portrait, lieu habituel, emploi du temps (placements sous conditions : heure, jour, flag…), amitié de départ, vie au camp et grade, dialogue par défaut, dialogues selon la situation (y compris selon le PJ qui parle), conditions d'apparition |
| **Histoire** | Dialogues en **formulaire** ou en **texte** : répliques, variantes selon la situation, aiguillages, sauts vers un autre dialogue (les histoires se croisent), choix (cachés ou grisés), conditions, effets |
| **Quêtes** | Objectifs dans l'ordre (parler à un PNJ, vaincre des monstres, aller à un lieu, apporter un objet) et récompenses |
| **Objets / Reliques** | Type, prix, soins, bonus d'équipement, objet unique, relique équipable ou de quête |
| **Monstres** | Stats, compétences, XP, or, butin (avec probabilités), boss, portrait, répliques de combat |
| **Compétences** | Physique / magique / soin, cible, coût en PM, puissance |
| **Lieux et carte** | Type, liens entre lieux, lieu secret (visible sous condition), durée du voyage, boutique, auberge, rencontres aléatoires, combat fixe (dialogues avant / après victoire / après défaite, répliques), accès sous condition, dialogue de première visite |
| **Départ** | Lieu, or, objets, dialogue d'introduction, héros proposés |
| **Équilibrage** | XP par niveau, niveau max, prix de revente, formules de dégâts et de soin |
| **Monde et textes** | Nom du pays, choix du PJ qui parle, et **tous les textes de l'interface** (onglets, monnaie, PV/PM, actions de combat, types de lieu…) |
| **Temps et calendrier** | Heure et jour de départ, heures par jour, jours de la semaine, mois, moments de la journée, durées (voyage, exploration, combat, discussion, réveil à l'auberge) |
| **Karma / Amitié** | Nom, valeur de départ, minimum, maximum, visible ou caché, paliers nommés (« Vertueux », « Ami »…) |
| **Campement** | Titre du chef, grades (niveau, nombre de places), tâches (durée, grade minimum, places, conditions, résultats tirés au sort avec texte et effets) |
| **Variables** | Valeurs libres du scénario (réputation, dette…), avec départ, minimum, maximum, affichage au joueur |
| **Banque d'images** | Images par **lien https** (rien n'est stocké), cadrées une fois (glisser + zoom), utilisées comme portraits |

- **Effets** possibles : flags, recruter / renvoyer un PJ, objets, or, XP, combat, quêtes, soin, téléportation, variables (fixer / ajouter), karma et amitié (fixer / ajouter, pour celui qui parle, le héros, toute l'équipe ou un PJ), faire passer le temps ou attendre une heure, afficher un message, déplacer un PNJ, révéler / cacher un lieu, camp (rejoindre, quitter, grade, tâche).
- **Conditions** possibles : flags, quêtes, objets, équipe, or, niveau, variables, karma, amitié (envers l'équipe ou un PJ), taille de l'équipe, qui parle, heure, jour, moment, jour de la semaine, mois, lieu actuel ou visité, PNJ rencontré, hasard, camp (membre, grade, tâche). Chaque condition peut être **inversée** (« sauf si »), et les groupes **« au moins une de »** / **« toutes »** permettent n'importe quelle logique.
- **Balises dans les textes** : `%pj%` (qui parle), `%heros%`, `%pays%`, `%monnaie%`, `%heure%`, `%date%`, `%periode%`, `%lieu%`, `%or%`, `%karma%`, `%var:id%`, `%amitie:pnj%`, `%nom:id%`, `%membre%` (camp).

Boutons de l'accueil de l'éditeur :

- **Vérifier** : liste les références cassées (un PNJ dont le dialogue n'existe plus, par exemple).
- **Enregistrer** : le jeu utilise alors ce contenu sur le téléphone.
- **Tester** : lance une partie de test avec le brouillon. Elle n'est jamais sauvegardée.
- **Exporter le fichier** / **Copier le texte** : pour envoyer ton contenu. Une fois déposé dans `src/ProjetSKE.Core/Data/content.json` (en ressource embarquée), il devient le contenu officiel de l'APK.
- **Importer un fichier** / **Coller le texte** : pour charger un contenu.
- **Revenir au contenu d'origine** : efface le contenu de l'éditeur.

Les anciennes sauvegardes restent jouables : tout ce qui a été supprimé du contenu est retiré de la partie au chargement.

### Base de données partagée

Le contenu peut être partagé entre tous les téléphones grâce à une base Firebase Firestore : chaque appareil récupère la dernière version au lancement, et « Enregistrer » la publie. Pour la mise en place, voir [docs/BASE-DE-DONNEES.md](docs/BASE-DE-DONNEES.md).

### Écrire un dialogue en texte

```
- Sur la place, une jeune femme fait danser des flammes.
Lyra: Un aventurier ! Tu m'emmènes ?
> Rejoins-moi. -> oui [recrute lyra]
> Non merci. -> non
> Je t'offre 50 or. -> oui {or 50} [payer 50] [recrute lyra]

@oui
Lyra: Génial !

@non
Lyra: Tant pis.
```

- `Nom: texte` : une réplique ; `- texte` : de la narration.
- `> choix -> étiquette` : un choix de réponse ; `@étiquette` : le début d'un bloc ; `-> étiquette` : un saut (`-> fin` pour terminer).
- `[effet]` : un effet, par exemple `[flag x]`, `[recrute id]`, `[objet id 2]`, `[or 50]`, `[xp 30]`, `[combat loup,loup]`, `[quete id]`, `[soin]` ou `[teleport lieu]`.
- `{condition}` : une condition sur un choix, par exemple `{flag x}`, `{quete_active id}`, `{karma >= 20}`, `{amitie pnj > 50}`, `{parle lyra}`, `{heure 20 6}`, `{!flag x}` (sauf si) ou `{flag a | flag b}` (l'un ou l'autre).
- `>~ choix {condition} ((raison))` : un choix affiché grisé, avec la raison, quand la condition manque.
- `~ {condition} Nom: texte` : une autre version de la réplique ; `? {condition} -> étiquette` : un aiguillage ; `-> dialogue:étiquette` : continuer dans un autre dialogue.

L'aide complète est affichée dans l'éditeur.

### Outils de test en partie

Une fois le mode développeur déverrouillé, **Menu → Outils de test** permet de :

- se téléporter ;
- ajouter de l'or ou de l'XP, soigner l'équipe ;
- donner des objets, recruter un PJ ;
- lancer un combat ou un dialogue ;
- démarrer, terminer ou oublier une quête ;
- poser ou retirer des flags ;
- faire passer le temps (+1 h, +6 h, +1 jour), changer le karma de chaque PJ et les variables.

## Organisation du code

```
src/ProjetSKE.Core/          Moteur du jeu (C# pur, testable, sans interface)
  Models/                    Définitions : compétences, objets, personnages, monstres, lieux, dialogues
  Data/SampleContent.cs      Contenu d'exemple (utilisé tant qu'il n'y a pas de Data/content.json)
  Data/DialogueScript.cs     Format texte des dialogues
  State/GameState.cs         Ce qui est sauvegardé
  Systems/                   Règles : partie, combat, dialogues, sauvegarde
src/ProjetSKE.App/           Application Android (.NET MAUI)
  Pages/                     Titre, emplacements, sélection, écran de jeu
  Views/                     Onglets, combat, dialogue
  Dev/                       Mode développeur : éditeur et outils de test
  Ui/UiKit.cs                Couleurs et briques d'interface
tests/ProjetSKE.Core.Tests/  Tests automatiques du moteur
```

### Ajouter du contenu

Le plus simple est d'utiliser le **mode développeur**, directement sur le téléphone. Pour intégrer un contenu exporté à l'APK officiel, dépose le fichier dans `src/ProjetSKE.Core/Data/content.json` : il remplace alors automatiquement le contenu d'exemple.

Le test `Content_JsonRoundTrip` vérifie que le contenu d'exemple est valide et s'exporte correctement.
