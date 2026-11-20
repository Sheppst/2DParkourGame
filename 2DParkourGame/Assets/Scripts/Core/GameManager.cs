using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Racine 0 : unique MonoBehaviour pilote, singleton persistant qui relaie le cycle de vie Unity aux racines 1.</summary>
[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    /// <summary>Instance unique accessible depuis toute la hierarchie (using static GameManager).</summary>
    public static GameManager GM { get; private set; }

    /// <summary>Nom de la scene du menu principal.</summary>
    [SerializeField] string menuScene = "MainMenu";
    /// <summary>Nom de la scene de jeu.</summary>
    [SerializeField] string gameScene = "Game";

    /// <summary>Racine 1 : lecture des entrees joueur.</summary>
    [field: SerializeField] public InputManager Inputs { get; private set; }
    /// <summary>Racine 1 : gestion complete du joueur.</summary>
    [field: SerializeField] public PlayerManager Player { get; private set; }
    /// <summary>Racine 1 : gestion des entites (pieges, pieces, finish...).</summary>
    [field: SerializeField] public EntityManager Entities { get; private set; }
    /// <summary>Racine 1 : gestion du niveau et de la sauvegarde.</summary>
    [field: SerializeField] public LevelManager Level { get; private set; }
    /// <summary>Racine 1 : suivi et effets de camera.</summary>
    [field: SerializeField] public CameraManager Cam { get; private set; }

    /// <summary>Liens vers les objets de la scene courante (null hors scene de jeu).</summary>
    public SceneLinker Linker { get; private set; }
    /// <summary>Vrai si le jeu est en pause (menu echap).</summary>
    public bool IsPaused { get; private set; }
    /// <summary>Vrai si une scene de jeu est liee et prete.</summary>
    public bool InGame => Linker != null;

    /// <summary>Securise le singleton, instancie les racines 1 et relaie Awake.</summary>
    void Awake()
    {
        if (GM != null && GM != this)
        { Destroy(gameObject); return; }

        GM = this;
        DontDestroyOnLoad(gameObject);

        Inputs ??= new(); Player ??= new(); Entities ??= new(); Level ??= new(); Cam ??= new();
        Inputs.Awake(); Player.Awake(); Entities.Awake(); Level.Awake(); Cam.Awake();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>Relaie OnEnable.</summary>
    void OnEnable() { if (GM == this) Inputs?.OnEnable(); }

    /// <summary>Lie la scene de depart si sceneLoaded ne l'a pas deja fait.</summary>
    void Start() { if (GM == this && Linker == null) BindScene(); }

    /// <summary>Relaie FixedUpdate (physique).</summary>
    void FixedUpdate()
    {
        if (GM != this || !InGame) return;

        Player.FixedTick();
        Level.FixedTick();
    }

    /// <summary>Relaie Update dans l'ordre : entrees, pause, joueur, entites, niveau.</summary>
    void Update()
    {
        if (GM != this) return;

        Inputs.Tick();

        if (!InGame) return;

        if (Inputs.ExitPressed) HandleExit();

        Player.Tick(); Entities.Tick(); Level.Tick(); Cam.Tick();
    }

    /// <summary>Relaie LateUpdate (camera apres le joueur).</summary>
    void LateUpdate() { if (GM == this && InGame) Cam.LateTick(); }

    /// <summary>Relaie OnDisable.</summary>
    void OnDisable() { if (GM == this) Inputs?.OnDisable(); }

    /// <summary>Relaie OnDestroy et libere le singleton.</summary>
    void OnDestroy()
    {
        if (GM != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindScene(); Inputs?.OnDestroy();

        Time.timeScale = 1f; GM = null;
    }

    /// <summary>Relaie OnApplicationQuit.</summary>
    void OnApplicationQuit() { if (GM == this) Inputs?.OnDestroy(); }

    /// <summary>Appele par Unity a chaque chargement de scene.</summary>
    void OnSceneLoaded(Scene s, LoadSceneMode m) => BindScene();

    /// <summary>Cherche le SceneLinker de la scene et lie toutes les racines a ses objets.</summary>
    void BindScene()
    {
        UnbindScene();
        SetPaused(false);

        Linker = FindAnyObjectByType<SceneLinker>();

        if (Linker == null) return;

        if (!Linker.IsValid(out string err))
        { Debug.LogError($"[GameManager] SceneLinker invalide : {err}", Linker); Linker = null; return; }

        Player.Bind(); Entities.Bind(); Level.Bind(); Cam.Bind();
    }

    /// <summary>Delie toutes les racines de la scene precedente.</summary>
    void UnbindScene()
    {
        if (Linker == null) return;

        Cam?.Unbind(); Level?.Unbind(); Entities?.Unbind(); Player?.Unbind();
        Linker = null;
    }

    /// <summary>Touche de sortie : ferme les parametres s'ils sont ouverts, sinon bascule la pause.</summary>
    void HandleExit()
    {
        if (Level.IsFinished) return;

        if (Linker.Settings != null && Linker.Settings.IsOpen)
        { Linker.Settings.Close(); return; }

        SetPaused(!IsPaused);
    }

    /// <summary>Met en pause ou reprend le jeu et affiche le menu pause.</summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        if (Linker != null && Linker.Pause != null) Linker.Pause.Show(paused);

        RefreshTimeScale();
    }

    /// <summary>Recalcule Time.timeScale selon la pause et le gel camera.</summary>
    public void RefreshTimeScale() => Time.timeScale = IsPaused || (Cam != null && Cam.Effects.IsFrozen) ? 0f : 1f;

    /// <summary>Charge la scene du menu principal.</summary>
    public void LoadMenu() => LoadScene(menuScene);

    /// <summary>Charge la scene de jeu (la sauvegarde est appliquee au Bind si demande).</summary>
    public void LoadGame(bool continueSave) { Level.ContinueRequested = continueSave; LoadScene(gameScene); }

    /// <summary>Charge une scene apres avoir verifie qu'elle est dans les Build Settings.</summary>
    void LoadScene(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        { Debug.LogError($"[GameManager] Scene '{scene}' absente des Build Settings."); return; }

        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    /// <summary>Quitte vers le bureau (arrete le Play Mode dans l'editeur).</summary>
    public void QuitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
