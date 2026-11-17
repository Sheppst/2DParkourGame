using System.IO;
using System.Linq;
using TMPro;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>Outil editeur : cree la scene Game (joueur, sol, entites, UI) et la scene MainMenu, puis les ajoute aux Build Settings. Ne touche pas a Test_Scene.</summary>
public static class PieParPieSetup
{
    /// <summary>Chemin de la scene de jeu.</summary>
    const string GameScene = "Assets/Scenes/Game.unity";
    /// <summary>Chemin de la scene du menu principal.</summary>
    const string MenuScene = "Assets/Scenes/MainMenu.unity";
    /// <summary>Dossier des assets placeholder.</summary>
    const string ArtDir = "Assets/Art/Placeholders";
    /// <summary>Dossier des prefabs.</summary>
    const string PrefabDir = "Assets/Prefabs";
    /// <summary>Paquet des ressources essentielles de TextMeshPro.</summary>
    const string TmpPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    /// <summary>Asset d'actions contenant la map UI.</summary>
    const string UiActions = "Assets/InputSystem_Actions.inputactions";
    /// <summary>Largeur du joueur placeholder (provisoire, plus haut que large).</summary>
    const float PW = 0.5f;
    /// <summary>Hauteur du joueur placeholder (tient dans une case).</summary>
    const float PH = 1f;

    /// <summary>Sprite blanc 1x1 unite.</summary>
    static Sprite white;
    /// <summary>Ressources des controles TMP.</summary>
    static TMP_DefaultControls.Resources tmpRes;
    /// <summary>Ressources des controles UGUI.</summary>
    static DefaultControls.Resources uiRes;

    /// <summary>Entree du menu Unity.</summary>
    [MenuItem("PieParPie/Setup Game + Main Menu")]
    public static void Run()
    {
        if (!EnsureTmp()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("PieParPie Setup", "Creer les scenes Game et MainMenu (si absentes) ?", "Oui", "Annuler")) return;
        Setup();
    }

    /// <summary>Entree batchmode (-executeMethod PieParPieSetup.RunBatch).</summary>
    public static void RunBatch() { if (EnsureTmp()) Setup(); }

    /// <summary>Verifie les ressources TMP ; les importe si absentes (relancer l'outil ensuite).</summary>
    static bool EnsureTmp()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
        AssetDatabase.ImportPackage(TmpPackage, false);
        Debug.LogWarning("[PieParPieSetup] TMP Essentials importees : relance l'outil une fois l'import termine.");
        return false;
    }

    /// <summary>Execute tout le setup.</summary>
    static void Setup()
    {
        if (File.Exists(GameScene)) Debug.LogWarning("[PieParPieSetup] Game existe deja : ignoree.");
        else if (!CreateGameScene()) return;
        CreateMenuScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScene, true), new EditorBuildSettingsScene(GameScene, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[PieParPieSetup] Termine : Game et MainMenu prets, Build Settings mis a jour.");
    }

    // ---------- Assets ----------

    /// <summary>Charge le sprite blanc et les ressources UI integrees.</summary>
    static void LoadResources()
    {
        Directory.CreateDirectory(ArtDir); Directory.CreateDirectory(PrefabDir);
        string path = ArtDir + "/White.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(4, 4); tex.SetPixels(new Color[16].Fill(Color.white)); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spritePixelsPerUnit = 4; imp.filterMode = FilterMode.Point;
            imp.SaveAndReimport();
        }
        white = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Sprite S(string n) => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + n);
        tmpRes = new TMP_DefaultControls.Resources { standard = S("UISprite.psd"), background = S("Background.psd"), inputField = S("InputFieldBackground.psd"),
            knob = S("Knob.psd"), checkmark = S("Checkmark.psd"), dropdown = S("DropdownArrow.psd"), mask = S("UIMask.psd") };
        uiRes = new DefaultControls.Resources { standard = tmpRes.standard, background = tmpRes.background, inputField = tmpRes.inputField,
            knob = tmpRes.knob, checkmark = tmpRes.checkmark, dropdown = tmpRes.dropdown, mask = tmpRes.mask };
    }

    /// <summary>Remplit un tableau avec une valeur (aide).</summary>
    static T[] Fill<T>(this T[] a, T v) { for (int i = 0; i < a.Length; i++) a[i] = v; return a; }

    /// <summary>Cree ou charge la tile de sol.</summary>
    static Tile GroundTile()
    {
        string path = ArtDir + "/GroundTile.asset";
        var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (t != null) return t;
        t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = white; t.color = new Color(0.35f, 0.35f, 0.4f); t.colliderType = Tile.ColliderType.Grid;
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    /// <summary>Cree ou charge un materiau physique sans friction (evite de coller aux murs).</summary>
    static PhysicsMaterial2D Slippery()
    {
        string path = ArtDir + "/NoFriction.physicsMaterial2D";
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (m != null) return m;
        m = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// <summary>Cree ou charge le prefab placeholder d'une entite (sprite colore + collider trigger).</summary>
    static GameObject EntityPrefab<T>(string name, Color color, Vector2 size) where T : Entity
    {
        string path = $"{PrefabDir}/{name}.prefab";
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p != null) return p;
        var go = new GameObject(name);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = white; sr.color = color;
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<T>();
        p = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return p;
    }

    // ---------- Scene de jeu ----------

    /// <summary>Cree la scene Game : GameManager, lumiere 2D, sol, joueur, entites, zone bloquante, UI et SceneLinker.</summary>
    static bool CreateGameScene()
    {
        Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // avant les assets : changer de scene decharge les assets non references
        LoadResources();
        Tile ground = GroundTile();
        PhysicsMaterial2D slip = Slippery();
        GameObject trap = EntityPrefab<Trap>("Trap", new Color(0.9f, 0.2f, 0.2f), new Vector2(1f, 0.5f));
        GameObject coin = EntityPrefab<Coin>("Coin", new Color(1f, 0.85f, 0.1f), new Vector2(0.4f, 0.4f));
        GameObject finish = EntityPrefab<Finish>("Finish", new Color(0.2f, 0.9f, 0.3f), new Vector2(0.5f, 2f));
        EnsureGameManager();
        Camera cam = MainCamera();
        new GameObject("Global Light 2D").AddComponent<UnityEngine.Rendering.Universal.Light2D>().lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;

        // Sol en tile painting
        var grid = new GameObject("Grid", typeof(Grid));
        var tmGo = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(Rigidbody2D), typeof(CompositeCollider2D));
        tmGo.transform.SetParent(grid.transform, false);
        tmGo.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        tmGo.GetComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        Tilemap tm = tmGo.GetComponent<Tilemap>();
        for (int x = -12; x <= 40; x++) tm.SetTile(new Vector3Int(x, -1), ground);
        for (int y = 0; y < 6; y++) { tm.SetTile(new Vector3Int(-12, y), ground); tm.SetTile(new Vector3Int(40, y), ground); }
        // Saut actuel ~2.4 de haut : plateformes a 2 de haut maximum, murets a 1
        for (int x = 5; x <= 8; x++) tm.SetTile(new Vector3Int(x, 1), ground);
        for (int x = 18; x <= 21; x++) tm.SetTile(new Vector3Int(x, 1), ground);
        tm.SetTile(new Vector3Int(30, 0), ground);
        tmGo.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        tmGo.GetComponent<CompositeCollider2D>().GenerateGeometry(); // sinon le composite est sauve vide (le joueur traverse le sol)
        // Joueur : racine aux pieds, pivot visuel aux pieds, sprite au-dessus
        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;
        var rb = player.AddComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.freezeRotation = true;
        var col = player.AddComponent<BoxCollider2D>(); col.size = new Vector2(PW, PH); col.offset = new Vector2(0f, PH * 0.5f); col.sharedMaterial = slip;
        player.AddComponent<ConstantForce2D>();
        var visual = new GameObject("Visual").transform; visual.SetParent(player.transform, false);
        var body = new GameObject("Body"); body.transform.SetParent(visual, false);
        body.transform.localPosition = new Vector3(0f, PH * 0.5f); body.transform.localScale = new Vector3(PW, PH, 1f);
        var sr = body.AddComponent<SpriteRenderer>(); sr.sprite = white; sr.color = new Color(0.3f, 0.6f, 1f); sr.sortingOrder = 10;

        // Entites et zone bloquante
        var entities = new GameObject("Entities").transform;
        Place(coin, entities, 3f, 0.5f); Place(coin, entities, 6.5f, 2.5f); Place(coin, entities, 19.5f, 2.5f); Place(coin, entities, 24f, 0.5f);
        Place(trap, entities, 12.5f, 0.25f); Place(trap, entities, 25.5f, 0.25f);
        Place(finish, entities, 35f, 1f);
        var zone = new GameObject("BlockZone").AddComponent<BlockZone>();
        zone.transform.position = new Vector3(16f, 1f);
        Set(zone, "size", new Vector2(3f, 2f));

        // UI : flash, message de fin, pause, parametres
        RectTransform canvas = Canvas("Canvas");
        EnsureEventSystem();
        var flash = Stretch(new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(), canvas);
        var flashImg = flash.GetComponent<Image>(); flashImg.color = new Color(1f, 1f, 1f, 0f); flashImg.raycastTarget = false; flashImg.enabled = false;
        TMP_Text finishText = Text(canvas, "Niveau termine !", 72, new Vector2(1200f, 200f));
        finishText.name = "FinishText"; finishText.gameObject.SetActive(false);
        SettingsMenu settings = Settings(canvas);
        PauseMenu pause = Pause(canvas);
        settings.transform.SetAsLastSibling();

        // SceneLinker
        var link = new GameObject("SceneLinker").AddComponent<SceneLinker>();
        SetAuto(link, "PlayerRoot", player.transform); SetAuto(link, "PlayerVisual", visual); SetAuto(link, "MainCamera", cam);
        SetAuto(link, "FlashImage", flashImg); SetAuto(link, "Pause", pause); SetAuto(link, "Settings", settings); SetAuto(link, "FinishText", finishText);

        return EditorSceneManager.SaveScene(s, GameScene);
    }

    /// <summary>Instancie un prefab a une position.</summary>
    static void Place(GameObject prefab, Transform parent, float x, float y)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = new Vector3(x, y);
    }

    /// <summary>Cree un GameManager s'il n'y en a pas.</summary>
    static void EnsureGameManager() { if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("GameManager").AddComponent<GameManager>(); }

    /// <summary>Recupere ou cree la camera orthographique.</summary>
    static Camera MainCamera()
    {
        Camera c = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (c == null) { c = new GameObject("Main Camera").AddComponent<Camera>(); c.tag = "MainCamera"; }
        c.orthographic = true; c.orthographicSize = 5f; c.transform.position = new Vector3(0f, 1f, -10f);
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
        return c;
    }

    // ---------- Scene menu ----------

    /// <summary>Cree la scene MainMenu (camera independante, GameManager, menu et parametres).</summary>
    static void CreateMenuScene()
    {
        if (File.Exists(MenuScene)) { Debug.LogWarning("[PieParPieSetup] MainMenu existe deja : ignoree."); return; }
        Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadResources();
        Camera c = new GameObject("Main Camera").AddComponent<Camera>();
        c.tag = "MainCamera"; c.orthographic = true; c.transform.position = new Vector3(0f, 0f, -10f);
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        EnsureGameManager();
        RectTransform canvas = Canvas("Canvas");
        EnsureEventSystem();
        RectTransform col = Column(canvas, "Menu", new Vector2(500f, 520f));
        Text(col, "PieParPie", 64, new Vector2(500f, 110f));
        Button newGame = Btn(col, "Nouvelle partie"), cont = Btn(col, "Continuer"), set = Btn(col, "Parametres"), quit = Btn(col, "Quitter");
        TMP_Text locked = Text(canvas, "", 28, new Vector2(1000f, 60f));
        locked.name = "LockedText"; locked.rectTransform.anchoredPosition = new Vector2(0f, -320f); locked.color = new Color(1f, 0.5f, 0.4f);
        SettingsMenu settings = Settings(canvas);
        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        Set(menu, "newGame", newGame); Set(menu, "continueGame", cont); Set(menu, "settings", set); Set(menu, "quit", quit);
        Set(menu, "settingsMenu", settings); Set(menu, "lockedText", locked);
        EditorSceneManager.SaveScene(s, MenuScene);
    }

    // ---------- UI ----------

    /// <summary>Cree un Canvas plein ecran (1920x1080).</summary>
    static RectTransform Canvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = go.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920f, 1080f); cs.matchWidthOrHeight = 0.5f;
        return go.GetComponent<RectTransform>();
    }

    /// <summary>Cree l'EventSystem avec le module UI de l'Input System, branche sur la map UI de InputSystem_Actions.</summary>
    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var m = new GameObject("EventSystem", typeof(EventSystem)).AddComponent<InputSystemUIInputModule>();
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActions);
        if (asset == null) { Debug.LogError($"[PieParPieSetup] {UiActions} introuvable : module UI sans actions."); return; }
        InputActionReference[] refs = AssetDatabase.LoadAllAssetsAtPath(UiActions).OfType<InputActionReference>().ToArray();
        InputActionReference R(string n) => refs.FirstOrDefault(r => r.action != null && r.action.actionMap?.name == "UI" && r.action.name == n);
        m.actionsAsset = asset;
        m.point = R("Point"); m.move = R("Navigate"); m.submit = R("Submit"); m.cancel = R("Cancel"); m.leftClick = R("Click");
        m.rightClick = R("RightClick"); m.middleClick = R("MiddleClick"); m.scrollWheel = R("ScrollWheel");
        m.trackedDevicePosition = R("TrackedDevicePosition"); m.trackedDeviceOrientation = R("TrackedDeviceOrientation");
    }

    /// <summary>Etire un RectTransform sur tout son parent.</summary>
    static RectTransform Stretch(RectTransform r, Transform parent)
    {
        r.SetParent(parent, false); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    /// <summary>Panneau plein ecran sombre (masque cliquable).</summary>
    static RectTransform Overlay(Transform parent, string name, float alpha)
    {
        var r = Stretch(new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(), parent);
        r.GetComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
        return r;
    }

    /// <summary>Colonne centree avec disposition verticale automatique.</summary>
    static RectTransform Column(Transform parent, string name, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.sizeDelta = size;
        var v = r.GetComponent<VerticalLayoutGroup>();
        v.spacing = 14f; v.childAlignment = TextAnchor.MiddleCenter; v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false;
        return r;
    }

    /// <summary>Ligne avec disposition horizontale automatique.</summary>
    static RectTransform Row(Transform parent, string name)
    {
        var r = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.GetComponent<LayoutElement>().preferredHeight = 60f;
        var h = r.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14f; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = true;
        return r;
    }

    /// <summary>Texte centre.</summary>
    static TMP_Text Text(Transform parent, string text, float size, Vector2 box)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false); t.rectTransform.sizeDelta = box;
        t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false;
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = box.y;
        return t;
    }

    /// <summary>Bouton avec libelle.</summary>
    static Button Btn(Transform parent, string label)
    {
        GameObject go = TMP_DefaultControls.CreateButton(tmpRes);
        go.name = label; go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 60f;
        var t = go.GetComponentInChildren<TMP_Text>(); t.text = label; t.fontSize = 28;
        return go.GetComponent<Button>();
    }

    /// <summary>Menu pause : Reprendre, Parametres, et Quitter vers le menu / le bureau cote a cote.</summary>
    static PauseMenu Pause(Transform canvas)
    {
        var root = new GameObject("PauseMenu", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root, canvas);
        RectTransform panel = Overlay(root, "Panel", 0.6f);
        RectTransform col = Column(panel, "Column", new Vector2(640f, 380f));
        Text(col, "Pause", 56, new Vector2(640f, 90f));
        Button resume = Btn(col, "Reprendre"), settings = Btn(col, "Parametres");
        RectTransform row = Row(col, "Quit");
        Button toMenu = Btn(row, "Quitter vers le menu"), toDesktop = Btn(row, "Quitter vers le bureau");
        var p = root.gameObject.AddComponent<PauseMenu>();
        Set(p, "panel", panel.gameObject); Set(p, "resume", resume); Set(p, "settings", settings);
        Set(p, "quitToMenu", toMenu); Set(p, "quitToDesktop", toDesktop);
        return p;
    }

    /// <summary>Menu parametres : volume, resolution, touches reassignables, retour.</summary>
    static SettingsMenu Settings(Transform canvas)
    {
        var root = new GameObject("SettingsMenu", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root, canvas);
        RectTransform panel = Overlay(root, "Panel", 0.85f);
        RectTransform col = Column(panel, "Column", new Vector2(760f, 900f));
        Text(col, "Parametres", 52, new Vector2(760f, 80f));
        RectTransform vRow = Row(col, "Volume");
        Text(vRow, "Volume", 28, new Vector2(300f, 60f));
        GameObject slider = DefaultControls.CreateSlider(uiRes); slider.transform.SetParent(vRow, false);
        RectTransform rRow = Row(col, "Resolution");
        Text(rRow, "Resolution", 28, new Vector2(300f, 60f));
        GameObject drop = TMP_DefaultControls.CreateDropdown(tmpRes); drop.transform.SetParent(rRow, false);
        (string action, int index, string label)[] keys = { ("Move", 1, "Gauche"), ("Move", 2, "Droite"), ("Jump", 0, "Sauter"), ("Interact", 0, "Interagir"),
            ("Run", 0, "Courir"), ("Sneak", 0, "Prudence"), ("Exit", 0, "Sortir") };
        var sm = root.gameObject.AddComponent<SettingsMenu>();
        var so = new SerializedObject(sm);
        SerializedProperty list = so.FindProperty("rebinds");
        list.arraySize = keys.Length;
        for (int i = 0; i < keys.Length; i++)
        {
            RectTransform row = Row(col, keys[i].label);
            Text(row, keys[i].label, 28, new Vector2(300f, 60f));
            Button b = Btn(row, "-");
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Action").stringValue = keys[i].action;
            e.FindPropertyRelative("BindingIndex").intValue = keys[i].index;
            e.FindPropertyRelative("Button").objectReferenceValue = b;
            e.FindPropertyRelative("Label").objectReferenceValue = b.GetComponentInChildren<TMP_Text>();
        }
        Button back = Btn(col, "Retour");
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("volume").objectReferenceValue = slider.GetComponent<Slider>();
        so.FindProperty("resolution").objectReferenceValue = drop.GetComponent<TMP_Dropdown>();
        so.FindProperty("back").objectReferenceValue = back;
        so.ApplyModifiedPropertiesWithoutUndo();
        return sm;
    }

    // ---------- Serialisation ----------

    /// <summary>Assigne un champ serialise prive.</summary>
    static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[PieParPieSetup] Champ '{field}' introuvable sur {target.GetType().Name}."); return; }
        if (value is Object o) p.objectReferenceValue = o; else if (value is Vector2 v) p.vector2Value = v;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Assigne une auto-propriete [field: SerializeField].</summary>
    static void SetAuto(Object target, string property, Object value) => Set(target, $"<{property}>k__BackingField", value);
}
