using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static GameManager;

/// <summary>Racine independante : menu principal (nouvelle partie, continuer, parametres, quitter).</summary>
public class MainMenu : MonoBehaviour
{
    /// <summary>Bouton Nouvelle partie.</summary>
    [SerializeField] Button newGame;
    /// <summary>Bouton Continuer (masque sans sauvegarde, verrouille si la partie est terminee).</summary>
    [SerializeField] Button continueGame;
    /// <summary>Bouton Parametres.</summary>
    [SerializeField] Button settings;
    /// <summary>Bouton Quitter.</summary>
    [SerializeField] Button quit;
    /// <summary>Menu des parametres.</summary>
    [SerializeField] SettingsMenu settingsMenu;
    /// <summary>Texte affiche quand Continuer est verrouille.</summary>
    [SerializeField] TMP_Text lockedText;
    /// <summary>Message de verrouillage.</summary>
    [SerializeField] string lockedMessage = "La partie ne peut etre continuee dans l'etat du jeu actuel";

    /// <summary>Verifie le GameManager, branche les boutons et regle Continuer selon la sauvegarde.</summary>
    void Start()
    {
        if (GM == null)
        { Debug.LogError("[MainMenu] Aucun GameManager dans la scene.", this); return; }

        Bind(newGame, () => { SaveSystem.Delete(); GM.LoadGame(false); });
        Bind(continueGame, () => GM.LoadGame(true));
        Bind(settings, () => { if (settingsMenu != null) settingsMenu.Open(); });
        Bind(quit, () => GM.QuitToDesktop());

        RefreshContinue();
    }

    /// <summary>Continuer : masque sans sauvegarde valide, verrouille avec message si la partie est terminee.</summary>
    void RefreshContinue()
    {
        bool has = SaveSystem.TryLoad(out SaveData s), locked = has && s.Finished;

        if (continueGame != null)
        { continueGame.gameObject.SetActive(has); continueGame.interactable = !locked; }

        if (lockedText != null)
        { lockedText.text = lockedMessage; lockedText.gameObject.SetActive(locked); }
    }

    /// <summary>Ajoute une action a un bouton (signale un bouton manquant).</summary>
    void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null)
        { Debug.LogWarning("[MainMenu] Un bouton n'est pas assigne.", this); return; }

        b.onClick.AddListener(a);
    }
}
