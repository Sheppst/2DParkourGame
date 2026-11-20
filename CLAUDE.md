# CLAUDE.md — 2DParkourGame (« PièParPiè »)

## Concept et objectifs
- Jeu de plateforme / parkour 2D sous Unity.
- Spec de référence : page Notion « PièParPiè » (https://app.notion.com/p/409857f6262d80338ea0cddd6f1a37ec). La relire avant de coder une nouvelle partie.
- Objectifs : contrôles du joueur (3C), caméra qui coulisse, pièges et pièces, sauvegarde automatique, menus principal et pause, paramètres.

### Boucle OCR (Objectif, Challenge, Récompense), pas encore rédigée dans Notion
- Terme de game design : un objectif donne une raison de jouer, un challenge donne de l'importance, une récompense prouve que l'effort n'était pas vain, puis un nouvel objectif relance la boucle.
- **Récompense = deckbuilding de fin de niveau** : à chaque fin de niveau, le joueur obtient l'occasion de choisir des attributs.
- Chaque attribut a un **type**, et chaque type a une **couleur**.
- **Écharpe du joueur** (rangée sous « Character » dans Notion, pas sous Récompense) : sa couleur compare les couleurs de tous les attributs. La couleur la plus présente domine, sinon les couleurs se mélangent en proportion. Le sprite de l'écharpe est blanc et teinté en code ; il est accroché à un objet vide du skin, qui suivra les animations.
- Titres vides déjà créés dans Notion : « Boucle OCR » (Objectif, Challenge, Récompense > Deckbuilding > Attributs, Types et couleurs) et « Écharpe » sous Character.

### Direction artistique (notes manuscrites de l'utilisateur)
- Pixel art de style « 32 bits », 32 × 32 pixels par bloc, 1 bloc = 1 unité Unity. Assets générés par code (menu PieParPie > Generate Art).
- Personnage : cheveux blonds courts, t-shirt rouge, cargo gris, écharpe colorée. Il tient en grande partie dans un bloc, avec des espaces vides pour un aspect humain ; jusqu'à 4 pixels de débordement autorisés sur chacun des 8 côtés.
- Sol terre et herbe, chaque tuile remplit un bloc : dessus avec une bande d'herbe de 2 px (6,25 %) qui dépasse au-dessus (et sur le côté extérieur pour les bords), milieu en terre pleine, dessous irrégulier avec de petites roches qui pendent. Toutes les variantes de voisinage, plus un Rule Tile.
- Pièce : pièce dorée classique (inspirée du style Mario, sans copier le sprite).
- Piques : triangles grisés avec un reflet pour un aspect arrondi, 2 par bloc. Bande `SpikeStrip` : nombre de piques en stock, collider découpé selon les piques réellement posées (raycast sous chaque pique ; sans support, la pique est masquée et sa zone exclue). Orientable sol, plafond, murs. Segment droit uniquement. Base enfoncée de 4 px dans la surface et affichée derrière la terre (sortingOrder -1). Outil éditeur `TrapSnapper` : 3 s après qu'un piège (SpikeStrip ou Trap) a été lâché, rotation au quart de tour, alignement sur la grille des demi-blocs et collage à la surface proche (même raycast).
- Finish : mini portail rose-violet dans une arche en bois aux bords droits, 1 bloc de large sur 4 de haut.
- Animations (pièce, portail, personnage) : plus tard.

## Façon de travailler (préférences de l'utilisateur, obligatoires)
- Langue : français.
- **Attendre un « feu vert » explicite avant de coder.** Une réponse à mes questions ne vaut pas feu vert.
- Ne rien coder hors de la demande. En cas de doute, demander plutôt que supposer.
- Si du code existant gêne une modification, demander avant de le réécrire.
- Commits et push seulement sur demande. Jusqu'ici, messages de commit en anglais (pas une consigne de l'utilisateur).
- `Test_Scene` est le bac à sable de l'utilisateur. Ne jamais y construire le vrai niveau ; s'en servir seulement pour de petites démos ou tests.
- Avant un test en Play Mode qui écrit `save.json`, prévenir l'utilisateur : le fichier est partagé avec ses parties (voir Bugs connus).
- Si la session tourne dans un worktree, `git merge --ff-only main` pour voir le travail de l'utilisateur.

### Erreurs déjà commises, à ne pas refaire
- J'ai commencé à coder alors que l'utilisateur répondait seulement à mes questions : attendre le mot « feu vert ».
- J'ai construit le niveau dans Test_Scene au lieu d'une scène de jeu séparée.
- J'ai inversé les proportions du joueur (plus large que haut) : il est plus haut que large.
- J'ai fait un niveau infranchissable (plateforme trop haute, piège trop proche) : vérifier l'atteignabilité par le calcul et par un test.
- Le texte de verrouillage voulu par l'utilisateur est « La partie ne peut être continuer dans l'état du jeu actuelle ». Dans le code, il est sans accents à cause de la règle ASCII ; il est modifiable dans l'Inspector.

## Conventions de code
- Chaque classe, struct, méthode et variable a un `/// <summary>` XML.
- Fichiers de code en ASCII uniquement : aucun accent, même dans les commentaires et les chaînes.
- Gérer toutes les erreurs possibles (vérifications de null, données corrompues, références manquantes), avec un message `[NomClasse]` dans les logs.
- Découper au maximum, puis compacter : peu de lignes, mais pas de lignes trop longues (environ 160 caractères maximum).
- **Aérer les méthodes** : une ligne vide entre chaque étape logique d'une méthode (garde, préparation, boucle, application, log). Le corps court d'une boucle va sur la ligne suivante, entre accolades sur une seule ligne. Exemple de l'utilisateur : `SpikeStrip.Prepare()`.
- **Aucune coroutine** (sauf pour d'éventuelles requêtes serveur) : minuteries faites à la main avec des `float`.
- Noms réels (`GameManager`, `PlayerManager`…). Les `Racine0/10/11/21` de `Assets/Scenes/--TEST--` ne sont que des exemples du modèle.

## Architecture « à racines »
Le modèle vient des exemples de l'utilisateur dans `Assets/Scenes/--TEST--`.
- **Racine 0 : `GameManager`.** Seul `MonoBehaviour` pilote, singleton `GM`, `DontDestroyOnLoad`, détruit tout doublon, `[DefaultExecutionOrder(-1000)]`. Il relaie Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnDisable, OnDestroy et OnApplicationQuit aux racines 1, via des méthodes `internal` (Awake, Bind, Tick, FixedTick, LateTick, Unbind…).
- **Racines 1 et 2.** Classes C# simples `[Serializable]`, exposées en `[field: SerializeField] public X Prop { get; private set; }` et instanciées dans l'Awake du parent avec `??= new()` (les valeurs de l'Inspector sont conservées). Elles accèdent à la racine 0 via `using static GameManager;` puis `GM.…`.
- **Racines indépendantes.** `MonoBehaviour` qui se gèrent seuls et renvoient leurs résultats à la branche principale.

| Racine 1 (sur le GameManager) | Racines 2 | Rôle |
|---|---|---|
| `InputManager` (`GM.Inputs`) | — | Actions de l'Input System créées en code et rangées dans des variables (MoveX, JumpPressed/Held, RunHeld, SneakHeld, InteractPressed, ExitPressed). Réassignation sauvegardée dans PlayerPrefs. |
| `PlayerManager` (`GM.Player`) | `PlayerController`, `PlayerAnimator`, `PlayerMechanism`, `PlayerDeath` | Déplacement ; animations par scale/rotation ; écrasement, interaction, détection par overlap box ; `PlayerDeath` est le point d'entrée unique de la mort (`Kill(source)`), qui refuse les doublons (déjà mort, réapparition, pause, gel, niveau fini). |
| `EntityManager` (`GM.Entities`) | — | Liste des entités, activation/désactivation sur demande ou selon la distance au joueur (objet entier), compteur `Coins`. |
| `LevelManager` (`GM.Level`) | — | Sauvegarde JSON, retour à la dernière sauvegarde, fin de niveau. Fichier `SaveSystem.cs` : `SaveData` et lecture/écriture sécurisées. |
| `CameraManager` (`GM.Cam`) | `CameraFollow`, `CameraEffects` | Coulissement de la caméra ; effets gel du temps / tremblement / flash (`FreezeSettings`, `ShakeSettings`, `FlashSettings` et `CameraEffect` qui les combine). Ajouté comme racine 1 de ma propre initiative, non prévu par la spec. |

Racines indépendantes :
- `SceneLinker` : fournit au GameManager persistant les objets de la scène (joueur, visuel, caméra, image de flash, menus, texte de fin) et dessine le gizmo du coyote time.
- `Entity` (abstraite), avec `Trap` (tue), `Coin` (ramassée), `Finish` (fin de niveau) et `SpikeStrip` (bande de piques). Le collider doit être en trigger et sur le même objet. Hook `Prepare()` appelé au début d'`Init`.
- `BlockZone` : fait son propre overlap, l'affiche en gizmo et signale au LevelManager que la sauvegarde est interdite.
- `MainMenu`, `PauseMenu`, `SettingsMenu`.
- Interface `IInteractable` : aucune implémentation pour l'instant.

Ordre dans `GameManager.Update` : Inputs → touche Échap (pause) → Player → Entities → Level → Cam. Caméra dans LateUpdate.

## Dossiers (`2DParkourGame/Assets/`)
- `Scripts/Core` : GameManager, InputManager, SceneLinker.
- `Scripts/Player`, `Scripts/Entities`, `Scripts/Level`, `Scripts/Camera`, `Scripts/UI` : voir le tableau ci-dessus.
- `Editor/PieParPieSetup.cs` : outil de setup des scènes.
- `Scenes/` :
  - `MainMenu.unity` : menu principal, première scène du build ;
  - `Game.unity` : niveau de jeu, deuxième scène du build ;
  - `Test_Scene.unity` : bac à sable de l'utilisateur, hors du build ;
  - `--TEST--/` : exemples Racine de l'utilisateur.
- `Prefabs/` : Trap, Coin, Finish (placeholders, utilisables avec le pinceau GameObject du Tile Palette).
- `Art/Placeholders/` : sprite blanc, `GroundTile`, matériau `NoFriction`.
- `TextMesh Pro/` : ressources essentielles de TMP.

## Décisions de conception (et pourquoi)
- **GameManager persistant + `SceneLinker` par scène** : le GameManager survit aux changements de scène ; il ne peut donc pas garder de références vers les objets d'une scène.
- **Joueur** :
  - `Rigidbody2D` avec gravityScale 0, la gravité passant par un `ConstantForce2D` (exigé par la spec) ;
  - `BoxCollider2D` avec un matériau sans friction, pour ne pas coller aux murs ;
  - racine aux pieds, enfant `Visual` (pivot animé, aux pieds) puis petit-enfant `Body` (sprite), pour que les déformations partent des pieds.
- **Saut** : deux modes, `JumpMode.Fixed` ou `Variable` (relâcher la touche coupe la montée), pour que l'utilisateur compare. Coyote time défini en **distance**, affiché en gizmo cyan aux pieds. Mémorisation de l'appui (jump buffer) en secondes.
- **Prudence** : comme dans Minecraft, plus lent et impossible de tomber d'un rebord, grâce à une détection du sol décalée dans le sens du mouvement.
- **Détection des entités** : overlap box à la taille du collider plus une marge. Pas de `MonoBehaviour` relais pour les collisions.
- **Mort** : `Debug.Log`, commandes bloquées, puis retour à la dernière sauvegarde après `deathDelay`.
- **S'écraser** : à l'atterrissage, si la vitesse de chute dépasse `crashSpeed` (variable réglable). Purement visuel : ni mort ni dégâts.
- **BlockZones** : c'est l'utilisateur qui les place. Elles déterminent ce qu'est une « situation bloquante » pour la sauvegarde.
- **Pièces** : désactivées, jamais détruites. Seules les pièces présentes dans la dernière sauvegarde comptent ; une mort fait réapparaître celles qui ne sont pas sauvegardées. L'identifiant d'une pièce vient de sa position de départ.
- **Sauvegarde** :
  - une seule écriture, à la première frame où le joueur est au sol, immobile, a bougé à la frame précédente, n'est pas dans une BlockZone et n'est ni mort ni arrivé ;
  - fichier JSON dans `persistentDataPath/save.json`, écrit via un fichier temporaire pour ne jamais laisser un fichier à moitié écrit.
- **Finish** : message central, retour au menu après `finishDelay`, sauvegarde marquée `Finished`. Dans le menu, Continuer est alors verrouillé avec un message ; il est masqué s'il n'y a pas de sauvegarde.
- **Caméra** :
  - limites = côtés du cadre × (1 − `reduction`), avec un easing exponentiel ;
  - retour au point de ralliement (au-dessus de la tête) après `returnDelay` d'immobilité, interrompu si le joueur bouge ;
  - le gel du temps met `Time.timeScale` à 0 ; les effets tournent en temps réel.
- **Pause** : `Time.timeScale = 0`, recalculé par `GM.RefreshTimeScale()` à partir de la pause et du gel.
- **Touches par défaut** : Q/D, Espace, E, Shift, F, Échap. Elles sont liées par caractère (`<Keyboard>/#(q)`) pour rester justes en AZERTY.
- **Paramètres** : volume (`AudioListener`, PlayerPrefs), réassignation des touches, résolution.
- **Distance des entités** : désactivation de l'objet entier.
- **Forme du joueur (spec)** : cube de base de taille 2, avec des proportions par côté (gauche 0.5, bas 1, droite 0.5, haut 0.75 de 2). Le placeholder actuel est un rectangle de 0.5 × 1, en attendant le cube d'exemple de l'utilisateur dans Test_Scene.

## Environnement
- Unity **6000.3.9f1** (Unity 6.3), URP 2D.
- Input System 1.18.0, avec `activeInputHandler` = nouveau système uniquement.
- Autres packages : uGUI 2.0 (TMP inclus), 2D Tilemap et Tilemap Extras, 2D Animation, Test Framework 1.6.
- Chemin de l'éditeur sur l'ancien PC : `G:\Softwares\Unity\6000.3.9f1\Editor\Unity.exe`.

## PieParPieSetup (menu **PieParPie > Setup Game + Main Menu**)
- Crée si absents : les assets placeholder, les prefabs, `Game.unity` (sol en tiles, joueur, entités, BlockZone, UI, SceneLinker, lumière 2D globale) et `MainMenu.unity`. Met à jour les Build Settings (MainMenu puis Game).
- N'écrase rien : une scène existante est ignorée. Pour la régénérer, supprimer `Game.unity` ou `MainMenu.unity`.
- Ne touche jamais à `Test_Scene`.
- Si TMP Essentials manque, l'outil l'importe : le relancer une fois l'import terminé.
- En batchmode, l'import TMP est asynchrone : importer le paquet avec `-importPackage "<PackageCache>/com.unity.ugui@*/Package Resources/TMP Essential Resources.unitypackage"`, puis lancer `-executeMethod PieParPieSetup.RunBatch`.
- Pièges de l'API corrigés dans l'outil :
  - ouvrir ou créer une scène décharge les assets non référencés : créer les assets **après** `NewScene` ;
  - appeler `CompositeCollider2D.GenerateGeometry()` après avoir peint les tiles, sinon le sol est sauvegardé sans collision ;
  - l'EventSystem utilise les références de la map UI de `InputSystem_Actions.inputactions`, car `AssignDefaultActions()` plante en batchmode.

## Tester
- Dans l'éditeur : lancer MainMenu (parcours complet) ou Game (directement). Réglages dans l'Inspector du GameManager.
- Niveau validé au calcul : saut d'environ 2,4 de haut, environ 2,85 unités parcourues au-dessus de 0,5 en marchant. Plateformes à 2 de haut maximum, murets à 1.
- Tests automatiques : aucun dans le repo. J'ai validé avec un script temporaire en Play Mode lancé en batchmode, depuis supprimé. Il :
  - ajoute un clavier virtuel avec `InputSystem.AddDevice<Keyboard>()` et `QueueStateEvent` ;
  - règle `InputSystem.settings.backgroundBehavior = IgnoreFocus` et `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView`, sinon les touches n'arrivent pas en batchmode ;
  - maintient Espace environ 0,4 s : en saut variable, un simple appui donne un mini-saut.

  Résultat : niveau terminé, 4 pièces sur 4, aucune mort.
- Un batchmode ne peut pas tourner sur le projet pendant qu'Unity l'a ouvert. Utiliser un worktree ou fermer Unity.

## Bugs connus et pièges
- Le `save.json` (`AppData/LocalLow/DefaultCompany/2DParkourGame/`) est commun à l'éditeur, aux tests et aux parties : un test peut écraser une partie.
- Le message de verrouillage de Continuer est écrit sans accents (règle ASCII) ; il est modifiable dans l'Inspector de `MainMenu`.
- Pas de `.gitattributes` : mes fichiers sont en fins de ligne LF, ceux de l'utilisateur en CRLF.
- Sur l'ancien PC, `git fetch` a échoué sur une erreur de certificat SSL (« unable to get local issuer certificate »).
- Deux entités à la même position ont le même identifiant de sauvegarde ; un avertissement s'affiche.

## Reste à faire (par priorité)
1. Proportions du joueur : reprendre celles du cube d'exemple que l'utilisateur placera dans Test_Scene. Mettre à jour `PW`/`PH` dans PieParPieSetup et le joueur de Game.
2. Faire tester à la main par l'utilisateur, aucun de ces points n'ayant été vérifié :
   - les animations à l'œil ;
   - le ressenti de la caméra ;
   - la prudence au bord d'une plateforme ;
   - le menu pause et Échap ;
   - les paramètres (réassignation des touches, résolution) ;
   - les touches en AZERTY réel.
3. Brancher les effets de caméra sur des événements du jeu, seulement si l'utilisateur le demande (il n'existe que l'API `GM.Cam.Effects.Play(...)`).
4. Créer de vrais objets interactifs implémentant `IInteractable`, seulement si l'utilisateur le demande.
5. Remplacer les placeholders (sprites, tiles) et vrai level design, à la main de l'utilisateur.
