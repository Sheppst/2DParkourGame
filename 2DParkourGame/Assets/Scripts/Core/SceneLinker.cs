using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Racine independante : expose au GameManager les objets d'une scene de jeu (le GameManager persiste entre les scenes).</summary>
public class SceneLinker : MonoBehaviour
{
    /// <summary>Transform racine du joueur (porte Rigidbody2D, BoxCollider2D, ConstantForce2D).</summary>
    [field: SerializeField] public Transform PlayerRoot { get; private set; }
    /// <summary>Pivot visuel du joueur, place a ses pieds, anime en scale/rotation.</summary>
    [field: SerializeField] public Transform PlayerVisual { get; private set; }
    /// <summary>Camera de la scene de jeu.</summary>
    [field: SerializeField] public Camera MainCamera { get; private set; }
    /// <summary>Image plein ecran utilisee pour l'effet de flash (optionnelle).</summary>
    [field: SerializeField] public Image FlashImage { get; private set; }
    /// <summary>Menu pause (optionnel).</summary>
    [field: SerializeField] public PauseMenu Pause { get; private set; }
    /// <summary>Menu parametres (optionnel).</summary>
    [field: SerializeField] public SettingsMenu Settings { get; private set; }
    /// <summary>Texte central annoncant la fin du niveau (optionnel).</summary>
    [field: SerializeField] public TMP_Text FinishText { get; private set; }

    /// <summary>Rigidbody2D du joueur (recupere a la validation).</summary>
    public Rigidbody2D PlayerBody { get; private set; }
    /// <summary>Collider physique du joueur.</summary>
    public BoxCollider2D PlayerCollider { get; private set; }
    /// <summary>Force constante servant de gravite au joueur.</summary>
    public ConstantForce2D PlayerGravity { get; private set; }

    /// <summary>Verifie les references obligatoires et recupere les composants du joueur.</summary>
    public bool IsValid(out string error)
    {
        error = PlayerRoot == null ? "PlayerRoot manquant" : PlayerVisual == null ? "PlayerVisual manquant"
              : MainCamera == null ? "MainCamera manquante" : null;

        if (error != null) return false;

        PlayerBody = PlayerRoot.GetComponent<Rigidbody2D>();
        PlayerCollider = PlayerRoot.GetComponent<BoxCollider2D>();
        PlayerGravity = PlayerRoot.GetComponent<ConstantForce2D>();

        error = PlayerBody == null ? "Rigidbody2D manquant sur le joueur" : PlayerCollider == null ? "BoxCollider2D manquant sur le joueur"
              : PlayerGravity == null ? "ConstantForce2D manquant sur le joueur" : null;

        return error == null;
    }

    /// <summary>Retourne toutes les entites de la scene, y compris desactivees.</summary>
    public List<Entity> FindEntities() => new(FindObjectsByType<Entity>(FindObjectsInactive.Include, FindObjectsSortMode.None));

    /// <summary>Dessine la distance coyote aux pieds du joueur (fonctionne aussi hors Play Mode).</summary>
    void OnDrawGizmos()
    {
        GameManager gm = GameManager.GM != null ? GameManager.GM : FindAnyObjectByType<GameManager>();

        if (gm == null || gm.Player == null || gm.Player.Controller == null || PlayerRoot == null) return;

        if (PlayerRoot.TryGetComponent(out BoxCollider2D c)) gm.Player.Controller.DrawGizmos(c.bounds);
    }
}
