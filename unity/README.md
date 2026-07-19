# Projet du PoC Unity 6

Ce dossier est un projet Unity complet ciblant **Unity 6000.0.71f1**. La scène `Assets/Scenes/Bootstrap.unity` construit automatiquement la rue, le joueur, le ballon, la caméra et les commandes tactiles. C'est cette scène que GitHub Actions compile en APK.

## 1. Ouvrir le projet localement (facultatif)

1. Ouvrez Unity Hub.
2. Choisissez **Open > Add project from disk**.
3. Sélectionnez ce dossier `unity`, et non la racine du dépôt.
4. Ouvrez `Assets/Scenes/Bootstrap.unity` puis appuyez sur Play.

Le PoC visible dans l'APK est créé par `PocBootstrap.cs`. Il ne demande ni serveur, ni clé API, ni asset payant.

## 2. Préparer l'objet de rue à peindre

1. Ajoutez dans la scène un `Plane` ou un mesh UV-mappé représentant le décor.
2. Ajoutez-lui un `MeshCollider`.
3. Laissez `Convex` désactivé.
4. Dans les réglages d'import du mesh, activez **Read/Write**.
5. Créez un Material avec le shader `StreetTurf/MaskPreview`.
6. Placez l'image autorisée dans la propriété `Base Map` du Material.
7. Affectez ce Material au `MeshRenderer` du décor.

`RaycastHit.textureCoord` exige un `MeshCollider`. Dans un build, Unity exige aussi que le mesh soit lisible ; sinon toutes les touches retourneront l'UV `(0, 0)`.

## 3. Ajouter la surface tactile de dessin

1. Créez un `Canvas` en mode `Screen Space - Overlay`.
2. Dans le Canvas, créez une `Image` transparente couvrant tout l'écran.
3. Gardez `Raycast Target` activé sur cette Image.
4. Ajoutez `SurfaceMaskPainter` à cette Image.
5. Glissez la caméra dans `Editor Camera`.
6. Glissez le `MeshRenderer` de la rue dans `Surface Renderer`.
7. Glissez son `MeshCollider` dans `Surface Collider`.
8. Placez le décor dans un layer `Paintable`, puis sélectionnez ce layer dans `Paintable Layers`.

Un mouvement du doigt peint maintenant en rouge. Le PNG réellement exporté est noir et blanc : blanc = à supprimer, noir = à conserver.

Le peintre reprend automatiquement le ratio de l'image `Base Map` et limite son plus grand côté à 2048 pixels. Lors de l'envoi, l'image originale est redimensionnée exactement comme le masque ; le serveur reçoit donc toujours deux fichiers de mêmes dimensions.

## 4. Ajouter les boutons

Créez trois boutons UI au-dessus de l'Image transparente :

- `Effacer le masque` appelle `SurfaceMaskPainter.ClearMask` ;
- `Petit pinceau` appelle `SurfaceMaskPainter.SetBrushRadiusPixels` avec `12` ;
- `Grand pinceau` appelle la même méthode avec `48`.

Les boutons doivent être placés après l'Image transparente dans la hiérarchie du Canvas afin de recevoir les touches en premier.

## 5. Placer les buts

1. Ajoutez un sol jouable avec un Collider et placez-le dans le layer `PlayableGround`.
2. Ajoutez deux prefabs de cage dans la scène.
3. Ajoutez une seconde Image transparente plein écran, désactivée au départ.
4. Ajoutez `GoalPlacementController` à cette seconde Image.
5. Affectez la caméra, le layer du sol et les deux cages.
6. Activez cette Image seulement pendant l'étape de placement.
7. Appelez `SelectGoalA` ou `SelectGoalB`, puis glissez le doigt sur le sol.

Ne laissez jamais les deux Images transparentes actives en même temps : une sert au masque, l'autre aux buts.

Pour les limites, ajoutez une troisième Image transparente avec `BoundaryPlacementController` et un `LineRenderer`. Touchez les coins dans l'ordre, puis reliez un bouton `Fermer` à `CloseBoundary`. `BuildBoundaryData` fournit le polygone envoyé au serveur. Activez une seule surface tactile d'édition à la fois.

## 6. Envoyer le Home Turf

1. Ajoutez un GameObject vide nommé `HomeTurfApi`.
2. Ajoutez-lui `HomeTurfUploadClient`.
3. Remplacez `Api Base Url` par l'adresse HTTPS du backend.
4. En développement sur un téléphone physique, utilisez l'adresse IP locale de l'ordinateur, jamais `localhost`.
5. Démarrez aussi le backend avec `STREET_TURF_PUBLIC_BASE_URL` réglé sur cette même adresse ; cette valeur construit l'URL de texture renvoyée au mobile.
6. Appelez `CreateHomeTurf` depuis votre contrôleur d'écran avec la texture originale, le peintre et le manifeste.

Le serveur refuse les sources autres que `user_capture` et `licensed_asset`.

## 7. Charger le terrain du joueur Home chez le joueur Away

1. Ajoutez `HomeTurfLoader` dans la scène de match.
2. Affectez le Renderer du décor propre, les deux cages et un Transform vide `Boundary Root`.
3. Donnez au loader le `turfId` verrouillé par le serveur de match.
4. Appelez `LoadTurf`.

Le loader vérifie le SHA-256 avant d'afficher la texture et reconstruit les murs invisibles depuis les limites du manifeste.

## 8. Ajouter le gameplay arcade

1. Ajoutez `UrbanBallController`, un `Rigidbody` et un `SphereCollider` au ballon.
2. Créez un Physic Material avec `Dynamic Friction = 0.55`, `Static Friction = 0.60`, `Bounciness = 0.38` et `Bounce Combine = Maximum`.
3. Affectez ce matériau au SphereCollider du ballon.
4. Ajoutez `ArcadePlayerController` et un `CharacterController` au joueur.
5. Ajoutez `TouchJoystick` à un joystick UI composé d'un fond et d'une poignée.
6. Affectez ce joystick au joueur.
7. Ajoutez `TouchActionButton` aux boutons Tir, Passe et Tacle.
8. Reliez `On Pressed` aux méthodes `Shoot`, `Pass` et `Tackle` du joueur.
9. Ajoutez `DynamicMatchCamera` à la caméra et affectez le joueur et le ballon.

Ces scripts valident les sensations locales. Pour un match classé, seul le serveur doit accepter les tirs, les buts, les tacles, le chronomètre et la position officielle du ballon.

## 9. Packages multijoueur recommandés

Pour un prototype amical Unity 6 :

- Multiplayer Services SDK ;
- Netcode for GameObjects ;
- Relay pour connecter les joueurs sans exposer l'adresse IP du Home.

Pour le classement, utilisez un serveur dédié chez un hébergeur interchangeable. Unity Multiplay Hosting a atteint sa date d'arrêt le 31 mars 2026 ; ne liez pas l'architecture à cet ancien service.
