using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using static GameManager;

/// <summary>Ligne de reassignation d'une touche.</summary>
[Serializable]
public struct RebindEntry
{
    /// <summary>Nom de l'action (Move, Jump, Interact, Run, Sneak, Exit).</summary>
    public string Action;
    /// <summary>Index du binding (Move : 1 = gauche, 2 = droite ; autres : 0).</summary>
    public int BindingIndex;
    /// <summary>Bouton lancant la reassignation.</summary>
    public Button Button;
    /// <summary>Texte affichant la touche courante.</summary>
    public TMP_Text Label;
}

/// <summary>Racine independante : parametres (volume, touches, resolution).</summary>
public class SettingsMenu : MonoBehaviour
{
    /// <summary>Cle PlayerPrefs du volume.</summary>
    const string VolumeKey = "Volume";

    /// <summary>Panneau des parametres.</summary>
    [SerializeField] GameObject panel;
    /// <summary>Curseur du volume general (0 a 1).</summary>
    [SerializeField] Slider volume;
    /// <summary>Liste des resolutions (optionnelle).</summary>
    [SerializeField] TMP_Dropdown resolution;
    /// <summary>Touches reassignables.</summary>
    [SerializeField] List<RebindEntry> rebinds = new();
    /// <summary>Bouton de retour.</summary>
    [SerializeField] Button back;

    /// <summary>Vrai si le panneau est ouvert.</summary>
    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>Resolutions proposees.</summary>
    Resolution[] resolutions = Array.Empty<Resolution>();

    /// <summary>Applique le volume sauvegarde, branche les controles et masque le panneau.</summary>
    void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        if (panel == null) { Debug.LogError("[SettingsMenu] Panel manquant.", this); enabled = false; return; }
        if (volume != null) { volume.SetValueWithoutNotify(AudioListener.volume); volume.onValueChanged.AddListener(SetVolume); }
        if (back != null) back.onClick.AddListener(Close);
        foreach (RebindEntry r in rebinds) if (r.Button != null) { RebindEntry e = r; r.Button.onClick.AddListener(() => Rebind(e)); }
        SetupResolutions();
        panel.SetActive(false);
    }

    /// <summary>Ouvre le panneau et rafraichit l'affichage des touches.</summary>
    public void Open() { if (panel == null) return; panel.SetActive(true); RefreshLabels(); }

    /// <summary>Ferme le panneau (annule une reassignation en cours).</summary>
    public void Close() { if (GM != null) GM.Inputs.CancelRebind(); if (panel != null) panel.SetActive(false); }

    /// <summary>Change et sauvegarde le volume.</summary>
    void SetVolume(float v) { AudioListener.volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume); }

    /// <summary>Lance la reassignation d'une touche.</summary>
    void Rebind(RebindEntry e)
    {
        InputAction a = GM != null ? GM.Inputs.Map?.FindAction(e.Action) : null;
        if (a == null) { Debug.LogError($"[SettingsMenu] Action '{e.Action}' introuvable.", this); return; }
        if (e.Label != null) e.Label.text = "...";
        GM.Inputs.StartRebind(a, e.BindingIndex, RefreshLabels);
    }

    /// <summary>Affiche la touche courante de chaque ligne.</summary>
    void RefreshLabels()
    {
        if (GM == null || GM.Inputs.Map == null) return;
        foreach (RebindEntry r in rebinds) if (r.Label != null) r.Label.text = InputManager.KeyName(GM.Inputs.Map.FindAction(r.Action), r.BindingIndex);
    }

    /// <summary>Remplit la liste des resolutions et selectionne la courante.</summary>
    void SetupResolutions()
    {
        if (resolution == null) return;
        resolutions = Screen.resolutions;
        var options = new List<string>();
        int current = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution r = resolutions[i];
            options.Add($"{r.width} x {r.height} @ {Mathf.RoundToInt((float)r.refreshRateRatio.value)} Hz");
            if (r.width == Screen.width && r.height == Screen.height) current = i;
        }
        resolution.ClearOptions(); resolution.AddOptions(options);
        resolution.SetValueWithoutNotify(current);
        resolution.onValueChanged.AddListener(SetResolution);
    }

    /// <summary>Applique la resolution choisie.</summary>
    void SetResolution(int i)
    {
        if (i < 0 || i >= resolutions.Length) return;
        Resolution r = resolutions[i];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
    }
}
