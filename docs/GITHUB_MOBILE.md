# Obtenir l'APK avec GitHub depuis un téléphone

Le code et le workflow sont déjà dans le dépôt. Vous n'avez rien à compiler sur le téléphone. GitHub loue une machine temporaire qui lance Unity et place l'APK dans la page Actions.

## Importer l'archive complète dans le dépôt vide

Si les fichiers ne sont pas encore visibles dans le dépôt, téléchargez d'abord l'archive `Street-Turf-Global-Football-PoC.zip` fournie avec le projet.

1. Sur la page vide du dépôt, touchez le lien **creating a new file**.
2. Nommez ce premier fichier `README.md`, écrivez `# Street Turf`, puis validez avec **Commit changes**.
3. Revenez au dépôt, ouvrez **Code > Codespaces**, puis créez un Codespace sur `main`.
4. Dans l'explorateur de fichiers du Codespace, ouvrez le menu `…`, choisissez **Upload**, puis sélectionnez le ZIP téléchargé sur le téléphone.
5. Ouvrez le terminal du Codespace et exécutez séparément :

```bash
unzip -o Street-Turf-Global-Football-PoC.zip
git add .
git commit -m "feat: add Street Turf Unity PoC and Android CI"
git push origin main
```

Le ZIP est ignoré par Git et ne sera pas ajouté au dépôt. Le dernier ordre déclenche automatiquement le workflow Android.

## Ce qui peut être fait sur mobile

Depuis Chrome ou Safari, activez au besoin l'option **Version pour ordinateur**, puis ouvrez le dépôt GitHub.

Vous pouvez depuis le téléphone :

1. ajouter les secrets Unity ;
2. relancer le workflow ;
3. suivre sa progression ;
4. télécharger l'APK final.

## Prérequis unique qui demande un ordinateur

Unity exige un fichier de licence `.ulf`. Pour une licence Personal, ce fichier doit être créé légalement une fois avec **Unity Hub sur Windows, macOS ou Linux**. Il ne peut pas être généré uniquement depuis un téléphone.

Sur cet ordinateur :

1. installez Unity Hub ;
2. connectez-vous avec votre compte Unity ;
3. installez **Unity 6000.0.71f1** avec **Android Build Support**, le SDK/NDK et OpenJDK ;
4. dans Unity Hub, ouvrez **Preferences > Licenses > Add > Get a free personal license** ;
5. récupérez le contenu du fichier `Unity_lic.ulf` créé par Unity.

Gardez ce fichier privé. Ne l'ajoutez jamais dans le code du dépôt.

## Ajouter les trois secrets depuis le téléphone

Dans le dépôt GitHub :

1. ouvrez **Settings** ;
2. ouvrez **Secrets and variables > Actions** ;
3. touchez **New repository secret** ;
4. créez `UNITY_EMAIL` avec l'adresse du compte Unity ;
5. créez `UNITY_PASSWORD` avec le mot de passe du compte Unity ;
6. créez `UNITY_LICENSE` et collez tout le contenu du fichier `.ulf`, de la première à la dernière ligne.

Les noms doivent être écrits exactement comme ci-dessus.

## Lancer ou relancer la compilation

1. revenez à la page principale du dépôt ;
2. ouvrez **Actions** ;
3. choisissez **Build Android APK** ;
4. touchez **Run workflow**, sélectionnez `main`, puis confirmez ;
5. attendez que les deux étapes deviennent vertes : `Test Python backend`, puis `Build test APK`.

La première compilation Unity peut prendre plusieurs dizaines de minutes.

## Télécharger l'APK

1. ouvrez l'exécution verte du workflow ;
2. descendez jusqu'à **Artifacts** ;
3. touchez `StreetTurf-Android-APK` ;
4. décompressez le fichier ZIP téléchargé ;
5. ouvrez le fichier `.apk` sur Android et autorisez l'installation depuis cette source si le téléphone le demande.

L'APK Android ne peut pas être installé sur un iPhone. Pour iOS, Unity doit générer un projet Xcode qui devra ensuite être signé sur un Mac avec un compte Apple Developer.

## Diagnostic rapide

| Message rouge | Solution |
| --- | --- |
| `Add UNITY_LICENSE...` | Un ou plusieurs des trois secrets manquent. |
| `License is not active` | Régénérez la licence dans Unity Hub avec le même compte. |
| Les tests Python échouent | Ouvrez la ligne rouge pour lire le test concerné ; la compilation Unity n'a pas encore commencé. |
| `No files were found` | La compilation Unity n'a pas produit l'APK ; ouvrez les logs de l'étape Unity. |
