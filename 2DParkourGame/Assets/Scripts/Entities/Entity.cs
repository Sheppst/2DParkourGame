using UnityEngine;

/// <summary>Racine independante de base : toute entite autre que le joueur, geree par l'EntityManager. Le collider doit etre sur le meme objet.</summary>
public abstract class Entity : MonoBehaviour
{
    /// <summary>Vrai si l'entite est consommee (ex : piece ramassee) : l'activation par distance l'ignore.</summary>
    public bool Consumed { get; private set; }
    /// <summary>Identifiant stable base sur la position de depart (sert a la sauvegarde).</summary>
    public string Id { get; private set; }

    /// <summary>Renderers de l'entite (activation des graphismes).</summary>
    Renderer[] renderers;
    /// <summary>Colliders de l'entite (activation de la fonction).</summary>
    Collider2D[] colliders;

    /// <summary>Prepare l'entite (appele par l'EntityManager, meme si l'objet est desactive).</summary>
    public void Init()
    {
        Vector3 p = transform.position;
        Id = $"{GetType().Name}_{Mathf.RoundToInt(p.x * 100f)}_{Mathf.RoundToInt(p.y * 100f)}";
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        if (colliders.Length == 0) Debug.LogWarning($"[Entity] {name} n'a pas de Collider2D : le joueur ne pourra pas la toucher.", this);
    }

    /// <summary>Appele par le joueur quand son overlap box touche l'entite.</summary>
    public abstract void OnPlayerTouch();

    /// <summary>Active/desactive la fonction (colliders + script).</summary>
    public void SetFunction(bool on) { enabled = on; if (colliders != null) foreach (Collider2D c in colliders) c.enabled = on; }

    /// <summary>Active/desactive les graphismes.</summary>
    public void SetGraphics(bool on) { if (renderers != null) foreach (Renderer r in renderers) r.enabled = on; }

    /// <summary>Marque l'entite consommee ou non et l'affiche en consequence.</summary>
    public void SetConsumed(bool consumed) { Consumed = consumed; gameObject.SetActive(!consumed); }
}
