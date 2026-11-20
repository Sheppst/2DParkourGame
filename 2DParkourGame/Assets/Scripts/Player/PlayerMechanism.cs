using System;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Racine 2 : reactions du joueur avec l'environnement (overlap box, chute, interaction). La mort est geree par PlayerDeath.</summary>
[Serializable]
public class PlayerMechanism
{
    /// <summary>Marge ajoutee a la taille du collider pour la detection (overlap box).</summary>
    [SerializeField] Vector2 detectPadding = new(0.02f, 0.02f);
    /// <summary>Vitesse de chute a partir de laquelle l'atterrissage devient un ecrasement (purement visuel).</summary>
    [SerializeField, Min(0f)] float crashSpeed = 20f;

    /// <summary>Objet interactif actuellement au contact (null si aucun).</summary>
    public IInteractable Interactable { get; private set; }
    /// <summary>Evenement : atterrissage normal.</summary>
    public bool JustLanded { get; private set; }
    /// <summary>Evenement : atterrissage en s'ecrasant.</summary>
    public bool JustCrashed { get; private set; }
    /// <summary>Evenement : interaction effectuee.</summary>
    public bool JustInteracted { get; private set; }

    /// <summary>Filtre de detection incluant les triggers.</summary>
    ContactFilter2D filter;
    /// <summary>Tampon de resultats (evite les allocations).</summary>
    readonly List<Collider2D> hits = new();

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => filter = new ContactFilter2D { useTriggers = true };

    /// <summary>Liaison a la scene.</summary>
    internal void Bind() => ResetState();

    /// <summary>Physique : detecte les entites touchees et l'atterrissage.</summary>
    internal void FixedTick()
    {
        PlayerController c = GM.Player.Controller;

        if (c.JustLanded)
        { if (c.LandingSpeed >= crashSpeed) JustCrashed = true; else JustLanded = true; }

        PlayerDeath d = GM.Player.Death;

        if (d.IsDead) return;

        Bounds b = GM.Player.Collider.bounds;
        int n = Physics2D.OverlapBox(b.center, (Vector2)b.size + detectPadding, 0f, filter, hits);
        Interactable = null;

        for (int k = 0; k < n && !d.IsDead; k++)
        {
            Collider2D h = hits[k];

            if (h == GM.Player.Collider) continue;

            if (h.TryGetComponent(out Entity e) && e.isActiveAndEnabled) e.OnPlayerTouch();

            if (Interactable == null && h.TryGetComponent(out IInteractable it)) Interactable = it;
        }
    }

    /// <summary>Frame : interaction (impossible une fois mort).</summary>
    internal void Tick()
    {
        if (GM.Player.Death.IsDead)
        { Interactable = null; return; }

        if (GM.Inputs.InteractPressed && Interactable != null)
        { Interactable.Interact(); JustInteracted = true; }
    }

    /// <summary>Remet les evenements de la frame a zero.</summary>
    internal void ClearEvents() => JustLanded = JustCrashed = JustInteracted = false;

    /// <summary>Remet tous les etats a zero (respawn).</summary>
    internal void ResetState() { Interactable = null; ClearEvents(); }
}

/// <summary>Contrat de tout objet avec lequel le joueur peut interagir (touche Interagir).</summary>
public interface IInteractable
{
    /// <summary>Appele quand le joueur interagit avec l'objet.</summary>
    void Interact();
}
