using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Racine independante : zone bloquante (sauvegarde interdite). Cree son propre overlap et le represente par un gizmo.</summary>
public class BlockZone : MonoBehaviour
{
    /// <summary>Taille de la zone (unites monde).</summary>
    [SerializeField] Vector2 size = Vector2.one;
    /// <summary>Decalage du centre de la zone par rapport a l'objet.</summary>
    [SerializeField] Vector2 offset;
    /// <summary>Couleur du gizmo.</summary>
    [SerializeField] Color color = new(1f, 0.3f, 0.2f, 0.35f);

    /// <summary>Tampon de resultats partage (evite les allocations).</summary>
    static readonly List<Collider2D> hits = new();
    /// <summary>Filtre incluant les triggers.</summary>
    static readonly ContactFilter2D filter = new() { useTriggers = true };
    /// <summary>Vrai si le joueur est dans la zone (dernier resultat transmis).</summary>
    bool inside;

    /// <summary>Centre monde de la zone.</summary>
    Vector2 Center => (Vector2)transform.position + offset;

    /// <summary>Teste l'overlap avec le joueur et transmet le resultat au LevelManager s'il change.</summary>
    void FixedUpdate()
    {
        if (GM == null || !GM.InGame) return;

        bool now = Physics2D.OverlapBox(Center, size, 0f, filter, hits) > 0 && hits.Contains(GM.Player.Collider);

        if (now != inside)
        { inside = now; GM.Level.SetBlocked(this, now); }
    }

    /// <summary>Se retire du LevelManager quand la zone est desactivee.</summary>
    void OnDisable() { if (inside && GM != null) GM.Level.SetBlocked(this, false); inside = false; }

    /// <summary>Represente l'overlap dans l'editeur.</summary>
    void OnDrawGizmos()
    {
        Gizmos.color = color; Gizmos.DrawCube(Center, size);
        Gizmos.color = new Color(color.r, color.g, color.b, 1f); Gizmos.DrawWireCube(Center, size);
    }
}
