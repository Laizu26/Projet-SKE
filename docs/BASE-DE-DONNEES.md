# Base de données en ligne du mode développeur

Le contenu du jeu (PJ, PNJ, dialogues, quêtes, objets, monstres, lieux…) peut être partagé entre tous les téléphones grâce à **Firebase Firestore**, la même technologie que *Service Impérial*.

## Fonctionnement

- Tout le contenu tient dans **un seul document** Firestore : `projet-ske/contenu` par défaut. Il contient :
  - `json` : le contenu complet, au même format que l'export de l'éditeur ;
  - `revision` : un numéro qui augmente à chaque publication ;
  - `updatedBy` et `updatedAt` : qui a publié, et quand.
- **Au lancement de l'app**, si la synchronisation est activée, la dernière version en ligne est récupérée et le jeu l'utilise.
- **Dans le mode développeur :**
  - « Enregistrer » publie automatiquement ;
  - « Publier » et « Récupérer » sont aussi disponibles à la main.
- **Conflits :** si quelqu'un a publié entre-temps, la publication est refusée et rien n'est écrasé. Tu choisis alors :
  - « Récupérer la leur », qui fait perdre tes modifications non publiées ;
  - ou « Écraser avec la mienne ».
- Le code (`src/ProjetSKE.Core/Cloud/ContentRepository.cs`) passe par une interface `IContentRepository`. Une autre base (Supabase, serveur maison…) pourra la remplacer sans toucher au reste.

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
       match /projet-ske/{doc} {
         allow read: if true;
         allow write: if request.auth != null;
       }
     }
   }
   ```

   Si le projet contient déjà d'autres règles (celles de *Service Impérial*), ajoute seulement le bloc `match /projet-ske/{doc}` à côté des règles existantes.
5. **Dans l'app :** ouvre *Mode développeur*, puis « Configurer la base ».
   - Renseigne ton nom, puis touche « Tester ».
   - « Publier » envoie ensuite le contenu une première fois.

Les réglages restent sur le téléphone : ils ne sont jamais écrits dans le dépôt.

## Pistes pour la suite

- Écoute en direct des changements (aujourd'hui, la mise à jour se fait au lancement ou avec « Récupérer »).
- Historique des révisions, pour revenir à une version précédente.
- Droits d'édition par personne, avec connexion par e-mail au lieu de la connexion anonyme.
