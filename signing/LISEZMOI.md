# Clé de signature de l'APK

`projetske.keystore` signe **toutes** les versions du jeu (alias `projetske`). C'est ce qui permet d'installer une nouvelle version par-dessus l'ancienne (et les mises à jour dans l'application) en gardant les sauvegardes.

- **Ne jamais supprimer ni régénérer ce fichier.** Avec une autre clé, Android refuse la mise à jour et il faut désinstaller le jeu, ce qui efface les sauvegardes.
- Garde-en une copie de sécurité ailleurs, avec son mot de passe.
- Le **mot de passe n'est pas dans le dépôt** : il est dans le secret GitHub `SKE_KEYSTORE_PASS`
  (Settings → Secrets and variables → Actions). Sans ce secret, la compilation GitHub s'arrête avec un message clair.
- Compilation locale (Visual Studio) : ajouter `-p:SkeKeystorePass=<mot de passe>` pour signer avec cette clé ;
  sans lui, l'APK est signé avec la clé de débogage (utile pour tester, mais il ne s'installe pas par-dessus la version officielle).
- Empreinte SHA-256 : `92:D9:A3:3E:7B:01:F8:74:FB:02:CA:82:72:0C:E4:FE:D5:43:D7:FE:D2:81:51:B9:42:16:A1:54:8F:7C:39:69`.
  Utile si Firebase la demande pour l'app Android.

Historique : une première clé a servi jusqu'à la compilation 22 ; son mot de passe figurait dans l'historique du dépôt,
elle a donc été remplacée avant de rendre le dépôt public.
