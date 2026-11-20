using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Racine 1 : cree les actions de l'Input System et range leurs valeurs dans des variables lisibles par toute la hierarchie.</summary>
[Serializable]
public class InputManager
{
    /// <summary>Cle PlayerPrefs des touches reassignees.</summary>
    const string RebindKey = "InputRebinds";

    /// <summary>Ensemble des actions du joueur.</summary>
    public InputActionMap Map { get; private set; }
    /// <summary>Action de deplacement horizontal (composite : index 1 = gauche, 2 = droite).</summary>
    public InputAction Move { get; private set; }
    /// <summary>Action de saut.</summary>
    public InputAction Jump { get; private set; }
    /// <summary>Action d'interaction.</summary>
    public InputAction Interact { get; private set; }
    /// <summary>Action de course.</summary>
    public InputAction Run { get; private set; }
    /// <summary>Action de deplacement prudent.</summary>
    public InputAction Sneak { get; private set; }
    /// <summary>Action de sortie (menu, dialogue...).</summary>
    public InputAction Exit { get; private set; }

    /// <summary>Axe horizontal (-1 gauche, 1 droite).</summary>
    public float MoveX { get; private set; }
    /// <summary>Saut presse cette frame.</summary>
    public bool JumpPressed { get; private set; }
    /// <summary>Saut maintenu.</summary>
    public bool JumpHeld { get; private set; }
    /// <summary>Interaction pressee cette frame.</summary>
    public bool InteractPressed { get; private set; }
    /// <summary>Course maintenue.</summary>
    public bool RunHeld { get; private set; }
    /// <summary>Deplacement prudent maintenu.</summary>
    public bool SneakHeld { get; private set; }
    /// <summary>Sortie pressee cette frame.</summary>
    public bool ExitPressed { get; private set; }
    /// <summary>Vrai pendant une reassignation de touche (les valeurs sont alors neutralisees).</summary>
    public bool IsRebinding => rebind != null;

    /// <summary>Operation de reassignation en cours.</summary>
    InputActionRebindingExtensions.RebindingOperation rebind;

    /// <summary>Cree les actions et leurs touches par defaut (par caractere : Q/D/E/F restent justes en AZERTY).</summary>
    internal void Awake()
    {
        Map = new InputActionMap("Player");
        Move = Map.AddAction("Move", InputActionType.Value);
        Move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/#(q)").With("Positive", "<Keyboard>/#(d)");
        Jump = Map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
        Interact = Map.AddAction("Interact", InputActionType.Button, "<Keyboard>/#(e)");
        Run = Map.AddAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
        Sneak = Map.AddAction("Sneak", InputActionType.Button, "<Keyboard>/#(f)");
        Exit = Map.AddAction("Exit", InputActionType.Button, "<Keyboard>/escape");

        LoadRebinds();
    }

    /// <summary>Active les actions.</summary>
    internal void OnEnable() => Map?.Enable();

    /// <summary>Lit les actions et met a jour les variables (une fois par frame, avant tout le reste).</summary>
    internal void Tick()
    {
        if (Map == null) return;

        bool on = !IsRebinding;
        MoveX = on ? Move.ReadValue<float>() : 0f;
        JumpPressed = on && Jump.WasPressedThisFrame(); JumpHeld = on && Jump.IsPressed();
        InteractPressed = on && Interact.WasPressedThisFrame();
        RunHeld = on && Run.IsPressed(); SneakHeld = on && Sneak.IsPressed();
        ExitPressed = on && Exit.WasPressedThisFrame();
    }

    /// <summary>Desactive les actions.</summary>
    internal void OnDisable() => Map?.Disable();

    /// <summary>Libere les ressources (idempotent).</summary>
    internal void OnDestroy()
    {
        CancelRebind();
        Map?.Disable(); Map?.Dispose(); Map = null;
    }

    /// <summary>Lance la reassignation interactive d'une touche puis sauvegarde ; onDone est appele a la fin.</summary>
    public void StartRebind(InputAction action, int bindingIndex, Action onDone)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        { Debug.LogError("[InputManager] Reassignation invalide."); return; }

        CancelRebind();
        action.Disable();

        rebind = action.PerformInteractiveRebinding(bindingIndex).WithControlsExcluding("<Mouse>").WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(_ => EndRebind(action, onDone)).OnCancel(_ => EndRebind(action, onDone)).Start();
    }

    /// <summary>Termine une reassignation : libere l'operation, reactive l'action et sauvegarde.</summary>
    void EndRebind(InputAction action, Action onDone)
    {
        rebind?.Dispose(); rebind = null;
        action.Enable(); SaveRebinds();

        onDone?.Invoke();
    }

    /// <summary>Annule une reassignation en cours.</summary>
    public void CancelRebind() { rebind?.Cancel(); rebind?.Dispose(); rebind = null; }

    /// <summary>Texte lisible de la touche d'un binding.</summary>
    public static string KeyName(InputAction action, int bindingIndex) =>
        action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count ? "-" : action.GetBindingDisplayString(bindingIndex);

    /// <summary>Sauvegarde les reassignations dans les PlayerPrefs.</summary>
    void SaveRebinds() { PlayerPrefs.SetString(RebindKey, Map.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); }

    /// <summary>Charge les reassignations sauvegardees (ignore une donnee corrompue).</summary>
    void LoadRebinds()
    {
        string json = PlayerPrefs.GetString(RebindKey, "");

        if (string.IsNullOrEmpty(json)) return;

        try
        { Map.LoadBindingOverridesFromJson(json); }
        catch (Exception e)
        { Debug.LogWarning($"[InputManager] Reassignations corrompues ignorees : {e.Message}"); PlayerPrefs.DeleteKey(RebindKey); }
    }
}
