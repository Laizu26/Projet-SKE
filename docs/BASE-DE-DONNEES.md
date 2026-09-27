# Base de données en ligne du mode développeur

Le contenu du jeu (PJ, PNJ, dialogues, quêtes, objets, monstres, lieux…) peut être partagé entre tous les téléphones grâce à **Firebase Firestore**, la même technologie que *Service Impérial*.

## Fonctionnement

**Stockage en ligne**
- Tout le contenu tient dans **un seul document** Firestore : `projet-ske/contenu`. Il contient :
  - `json` : le contenu complet ;
  - `revision` : un numéro qui augmente à chaque publication ;
  - `updatedBy` et `updatedAt` : qui a publié, et quand.
- **Chaque révision publiée est aussi archivée** dans `projet-ske/contenu/historique/r000012` (etc.) : aucune version n'est jamais perdue.

**Synchronisation automatique**
- Elle a lieu au lancement, puis toutes les 45 secondes, et à chaque « Enregistrer ».
- Le bouton « Synchroniser » du mode développeur la déclenche à la main.

**Pas d'écrasement : fusion élément par élément**

Chaque PJ, PNJ, dialogue, quête, objet, monstre, compétence et lieu est comparé séparément entre trois versions : la dernière version commune, la tienne et celle en ligne.

| Situation | Résultat |
|---|---|
| Modifié d'un seul côté | Cette version est prise |
| Deux personnes modifient des éléments différents | Tout est gardé |
| Même élément modifié des deux côtés | La version en ligne est gardée, la tienne est **mise de côté** (menu « Conflits » : « Remettre ma version » ou « Garder l'autre ») |
| Supprimé d'un côté mais modifié de l'autre | L'élément est **gardé** |
| Quelqu'un publie pendant ta publication | Refus automatique (précondition Firestore), nouvelle fusion, nouvel essai |

**Protections supplémentaires**
- **Copie locale** du contenu avant chaque changement : les 20 dernières sont gardées sur le téléphone.
- **Brouillon de l'éditeur** : « Enregistrer » le fusionne avec ce qui a été reçu entre-temps. Tu n'annules donc jamais une modification publiée par un autre pendant que tu éditais.
- **Écran d'édition ouvert** : tant qu'il y a des modifications en cours ou qu'un écran d'édition est ouvert, la synchronisation ne remplace pas le brouillon.
- **« Revenir au contenu d'origine »** est masqué quand la synchronisation est active, pour ne pas effacer le travail de tout le monde.

## Projet utilisé

Le jeu est pré-configuré sur le projet Firebase **`projet-ske-597e2`** : l'ID et la clé API sont dans `src/ProjetSKE.App/Dev/CloudSync.cs`. Tous les téléphones sont donc connectés sans rien saisir, et la synchronisation est activée par défaut. On peut changer de projet depuis *Mode développeur → Configurer la base*.

La clé API Firebase n'est pas un secret : elle sert à identifier le projet. La sécurité repose sur les règles Firestore (lecture pour tous, écriture réservée aux appareils connectés) et sur la connexion anonyme. Dans Google Cloud, cette clé ne doit **pas** être restreinte aux « applications Android », car le jeu appelle Firestore directement.

## Mise en place

1. **Projet Firebase :** `projet-ske-597e2` est déjà créé, avec l'app Android `com.laizu.projetske`.
2. **Créer la base Firestore :** menu *Firestore Database*, puis « Créer une base de données ».
3. **Activer la connexion anonyme :** menu *Authentication*, puis « Méthode de connexion », puis « Anonyme ». L'app s'en sert pour ne pas laisser la base ouverte à tous.
4. **Règles de sécurité :** dans *Firestore Database*, onglet « Règles », ajoute :

   ```
   rules_version = '2';
   service cloud.firestore {
     match /databases/{database}/documents {
       match /projet-ske/{document=**} {
         allow read: if true;
         allow write: if request.auth != null;
       }
     }
   }
   ```

   `{document=**}` couvre aussi l'historique des révisions.
5. **Dans l'app :** ouvre *Mode développeur*, puis « Configurer la base ».
   - Renseigne ton nom, puis touche « Tester ».
   - La première synchronisation publie ensuite le contenu une première fois.

Les réglages restent sur le téléphone : ils ne sont jamais écrits dans le dépôt.

## Pistes pour la suite

- Écran pour parcourir l'historique en ligne et revenir à une révision précédente.
- Droits d'édition par personne, avec connexion par e-mail au lieu de la connexion anonyme.
