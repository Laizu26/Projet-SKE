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
| **Sélection** | Choix du héros en cartes qu'on fait glisser de gauche à droite ; son **départ** (origine, lieu, équipement, compagnons, monde de départ) dépend de lui et est annoncé sur sa carte |
| **Camp** | Un cercle autour d'un feu animé, avec six parties : **Équipe** (les combattants assis autour du feu ; fiche : stats, niveau, XP, karma, compétences, et l'équipement en « poupée » : tête, corps, mains, jambes, pieds, accessoire, arme, bouclier, relique), **Persos** (les habitants répartis en cercles par grade, le chef au centre ; vue liste possible), **Gestion** (hiérarchie, qui fait quelle tâche, journal du camp), **Ressources** (trésor, stocks du camp consommés chaque jour, valeurs du scénario), **Sac** et **Lieux** (bâtiments à construire avec des ressources et de l'or). Re-toucher l'onglet ramène autour du feu |
| **Carte** | Vue du lieu actuel (ville : auberge, boutique, habitants ; nature : explorer, combat fixe), puis vue du pays avec la liste des destinations |
| **Quêtes** | Onglet masqué par défaut (activable dans Monde et textes) : quêtes en cours et terminées. Les quêtes avancent quand même, avec des messages à l'écran |
| **Encyclo** | Personnages (PJ et PNJ), monstres, lieux, armes et reliques rencontrés (les autres restent invisibles) |
| **Shop** | Uniquement en ville : achat et revente (à moitié prix) |
| **Journal** | Page blanche où l'on écrit librement, sauvegardée avec la partie |
| **Menu** | Sauvegarde, paramètres (dont la vitesse du texte : lente, normale, rapide, instantanée), retour au titre |
| **Combat** | Par-dessus tout : l'ennemi en haut, la narration du combat (avec les répliques des combattants) au milieu, l'équipe en bas, puis Attaque / Défense / Objet / Fuite. Tour par tour selon la vitesse ; Défense divise les dégâts reçus par deux jusqu'au tour suivant |
| **Écran fissuré** | Partout dans le jeu : sous 30 %, 15 % puis 5 % des PV du héros, l'écran se fissure un peu plus (secousse et vibration) ; un soin efface les fissures. À la défaite, l'écran éclate en morceaux qui tombent, puis l'écran de fin (game over ou réveil à l'auberge) |
| **Histoire** | L'écran s'assombrit, le portrait de celui qui parle apparaît, puis la boîte de dialogue ; la **narration** (le récit, sans personnage) s'affiche à part : texte centré en italique sur un bandeau sombre, sans nom, avec une illustration possible ; choix de réponse (certains grisés avec la raison) |
| **Horloge** | En haut à droite : heure, moment de la journée, jour. Le temps passe en voyageant, en parlant, en combattant, à l'auberge |

### Paramètres modifiables (Menu)

- **Combats en voyage** : aucun, aléatoires, fixes (histoire), ou les deux.
- **Défaite** : retour à la dernière ville (avec une perte d'or) ou game over.
- **Fuite** : toujours réussie, selon la vitesse, ou impossible. Les boss empêchent toujours la fuite.

### Reliques

Une relique est un **objet unique**. Elle peut être **équipable**, pour un bonus permanent, ou servir d'**objet de quête**, sans effet en combat. Ce choix se règle avec `RelicUsage` dans le contenu.

## Mises à jour dans l'application

Chaque push est compilé et testé, mais une nouvelle version n'est publiée en **Release** GitHub (`v<numéro>`, avec `ProjetSKE.apk`) qu'à la fin d'un lot de changements : commit dont le message contient `[maj]`, ou lancement manuel (Actions › Compiler l'APK › Run workflow). Le téléphone ne reçoit donc qu'une mise à jour par lot, qui contient tout.
L'application compare son numéro de compilation à la dernière Release :

- sur l'écran titre, une carte « Mise à jour disponible » apparaît, avec un bouton **Installer la mise à jour** ;
- dans **Menu → Version du jeu**, on peut vérifier et installer à la main.

L'APK est téléchargé puis l'installateur d'Android s'ouvre (la première fois, Android demande d'autoriser l'application à installer des applis). Les parties, le contenu et le brouillon du mode développeur sont gardés.

Réglages GitHub nécessaires (une seule fois) :

- dépôt **public** : l'application lit les Releases sans identifiant ;
- **Settings → Actions → General → Workflow permissions → Read and write permissions** : pour que la compilation publie les Releases ;
- secret **`SKE_KEYSTORE_PASS`** (Settings → Secrets and variables → Actions) : mot de passe de la clé de signature (voir `signing/LISEZMOI.md`).

## Mode développeur

Sur l'écran titre, touche **Développeur** puis entre le code **1234**.

### Éditeur (tout le contenu est modifiable)

| Catégorie | Ce qu'on règle |
|---|---|
| **PJ** | Nom, classe, description, portrait, stats de base et gain par niveau, compétences, équipement de départ, karma et amitié de départ, répliques de combat, « proposé au départ », son départ de partie, ses **compagnons au départ** et ses **effets au lancement** (quand on joue ce héros) |
| **PNJ** | Nom, portrait, lieu habituel, emploi du temps (placements sous conditions : heure, jour, flag…), amitié de départ, vie au camp et grade, dialogue par défaut, dialogues selon la situation (y compris selon le PJ qui parle), conditions d'apparition |
| **Histoire** | Dialogues en **formulaire** ou en **texte** : répliques ou **narration** (case « Narration », ou `- texte`, `* texte`, `Narration: texte` en mode texte), répliques jouées **seulement si** une condition passe (sinon sautées, ou « sinon, aller à »), variantes selon la situation, aiguillages, sauts vers un autre dialogue (les histoires se croisent), choix (cachés ou grisés, ou **choix-narration** : une action décrite plutôt qu'une réplique, `> * texte`), conditions, effets |
| **Quêtes** | Quête simple (objectifs dans l'ordre, récompenses) ou **quête à étapes** : chaque étape a son récit, ses objectifs, des effets sur le monde en y entrant, et des **chemins sous conditions** vers d'autres étapes, jusqu'à plusieurs **fins** (réussies ou échouées), ou **quête en parties** : plusieurs parties en parallèle, chacune avec son état (pas commencée, en cours, terminée, échouée), ses conditions de départ et d'échec, ses objectifs et ses effets ; conditions « Partie de quête : ... » pour lancer la suite selon l'état de chaque partie. Démarrage par un effet ou automatique sous condition |
| **Objets / Reliques** | Type, prix, soins, bonus d'équipement, objet unique, relique équipable ou de quête |
| **Monstres** | Stats, compétences, XP, or, butin (avec probabilités), boss, portrait, répliques de combat |
| **Compétences** | Physique / magique / soin / effets seuls / résurrection, cible, coût en PM et en PV, recharge, puissance et montant fixe, élément, nombre de coups, précision, critiques, vol de vie, **effets durables** (poison, régénération, étourdissement, bonus ou malus de stat, bouclier, purification) avec durée et chance, texte du journal. Monstres et PJ ont des **faiblesses et résistances** aux éléments |
| **Lieux et carte** | Type, liens entre lieux, lieu secret (visible sous condition), durée du voyage, boutique, auberge, rencontres aléatoires, combat fixe (dialogues avant / après victoire / après défaite, répliques), accès sous condition, dialogue de première visite |
| **Départs** | Plusieurs départs possibles : nom, description, lieu, or, objets, compagnons, date, dialogue d'introduction, effets au lancement (flags, variables, karma, quêtes…) |
| **Équilibrage** | XP par niveau, niveau max, prix de revente, formules de dégâts et de soin |
| **Monde et textes** | Nom du pays, choix du PJ qui parle, écran fissuré (seuils, force, secousse, éclatement, texte du game over), et **tous les textes de l'interface** (onglets, monnaie, PV/PM, actions de combat, types de lieu…) |
| **Temps et calendrier** | Heure et jour de départ, heures par jour, jours de la semaine, mois, moments de la journée, durées (voyage, exploration, combat, discussion, réveil à l'auberge) |
| **Karma / Amitié** | Nom, valeur de départ, minimum, maximum, visible ou caché, paliers nommés (« Vertueux », « Ami »…) |
| **Campement** | Titre du chef, grades (niveau, nombre de places), tâches (durée, grade minimum, places, conditions, résultats tirés au sort avec texte et effets), ressources (stock de départ, max, consommation par habitant et par jour, amitié perdue en cas de pénurie), lieux à construire (coût en or et en ressources, conditions, effets une fois construit, déjà construit au départ) |
| **Variables** | Valeurs libres du scénario (réputation, dette…), avec départ, minimum, maximum, affichage au joueur |
| **Banque d'images** | Images par **lien https** (rien n'est stocké), cadrées une fois (glisser + zoom), utilisées comme portraits |

- **Effets** possibles : flags, recruter / renvoyer un PJ, objets, or, XP, combat, quêtes, soin, téléportation, variables (fixer / ajouter), karma et amitié (fixer / ajouter, pour celui qui parle, le héros, toute l'équipe ou un PJ), faire passer le temps ou attendre une heure, afficher un message, déplacer un PNJ, révéler / cacher un lieu, camp (rejoindre, quitter, grade, tâche, ressource, construire un lieu), quête : aller à une étape, échouer.
- **Conditions** possibles : flags, quêtes, objets, équipe, or, niveau, variables, karma, amitié (envers l'équipe ou un PJ), taille de l'équipe, qui parle, heure, jour, moment, jour de la semaine, mois, lieu actuel ou visité, PNJ rencontré, hasard, camp (membre, grade, tâche, stock d'une ressource, lieu construit), quête à l'étape X, étape déjà passée, quête finie par telle fin, quête échouée. Chaque condition peut être **inversée** (« sauf si »), et les groupes **« au moins une de »** / **« toutes »** permettent n'importe quelle logique.
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
