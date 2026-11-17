using UnityEngine;
using UnityEngine.UI;
using static GameManager;

/// <summary>Racine independante : menu en jeu ouvert par la touche de sortie.</summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>Panneau du menu (affiche/masque).</summary>
    [SerializeField] GameObject panel;
    /// <summary>Bouton Reprendre.</summary>
    [SerializeField] Button resume;
    /// <summary>Bouton Parametres.</summary>
    [SerializeField] Button settings;
    /// <summary>Bouton Quitter vers le menu.</summary>
    [SerializeField] Button quitToMenu;
    /// <summary>Bouton Quitter vers le bureau.</summary>
    [SerializeField] Button quitToDesktop;

    /// <summary>Branche les boutons et masque le menu.</summary>
    void Awake()
    {
        if (panel == null) { Debug.LogError("[PauseMenu] Panel manquant.", this); enabled = false; return; }
        Bind(resume, () => GM.SetPaused(false));
        Bind(settings, () => { if (GM.Linker.Settings != null) GM.Linker.Settings.Open(); });
        Bind(quitToMenu, () => GM.LoadMenu());
        Bind(quitToDesktop, () => GM.QuitToDesktop());
        panel.SetActive(false);
    }

    /// <summary>Ajoute une action a un bouton (signale un bouton manquant).</summary>
    void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) { Debug.LogWarning("[PauseMenu] Un bouton n'est pas assigne.", this); return; }
        b.onClick.AddListener(() => { if (GM != null) a(); });
    }

    /// <summary>Affiche ou masque le menu.</summary>
    public void Show(bool on) { if (panel != null) panel.SetActive(on); }
}
