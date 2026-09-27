# Clé de signature de l'APK

`projetske.keystore` signe **toutes** les versions du jeu (alias `projetske`). C'est ce qui permet d'installer une nouvelle version par-dessus l'ancienne en gardant les sauvegardes.

- **Ne jamais supprimer ni régénérer ce fichier.** Avec une autre clé, Android refuse la mise à jour et il faut désinstaller le jeu, ce qui efface les sauvegardes.
- Garde-en une copie de sécurité ailleurs (clé USB, cloud perso).
- Empreinte SHA-256 : `74:3A:24:04:ED:35:36:55:FF:25:BE:77:AC:C5:18:D2:96:05:55:F2:2E:99:E8:0B:67:4D:E6:37:F9:21:2C:54`. Utile si Firebase la demande pour l'app Android.
- Le mot de passe est dans `src/ProjetSKE.App/ProjetSKE.App.csproj`, ce qui passe car le dépôt est **privé**. Si le dépôt devient public :
  1. crée un secret GitHub `SKE_KEYSTORE_PASS` ;
  2. retire le mot de passe du `.csproj` ;
  3. passe `-p:AndroidSigningStorePass=${{ secrets.SKE_KEYSTORE_PASS }} -p:AndroidSigningKeyPass=${{ secrets.SKE_KEYSTORE_PASS }}` à `dotnet publish` dans le workflow.
