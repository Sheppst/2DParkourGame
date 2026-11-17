using System;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Racine 2 : reactions du joueur avec l'environnement (overlap box, mort, chute, interaction).</summary>
[Serializable]
public class PlayerMechanism
{
    /// <summary>Marge ajoutee a la taille du collider pour la detection (overlap box).</summary>
    [SerializeField] Vector2 detectPadding = new(0.02f, 0.02f);
    /// <summary>Vitesse de chute a partir de laquelle l'atterrissage devient un ecrasement (purement visuel).</summary>
    [SerializeField, Min(0f)] float crashSpeed = 20f;
    /// <summary>Duree entre la mort et le retour a la derniere sauvegarde (s).</summary>
    [SerializeField, Min(0f)] float deathDelay = 0.6f;

    /// <summary>Vrai si le joueur est mort (en attente du reset).</summary>
    public bool IsDead { get; private set; }
    /// <summary>Objet interactif actuellement au contact (null si aucun).</summary>
    public IInteractable Interactable { get; private set; }
    /// <summary>Evenement : atterrissage normal.</summary>
    public bool JustLanded { get; private set; }
    /// <summary>Evenement : atterrissage en s'ecrasant.</summary>
    public bool JustCrashed { get; private set; }
    /// <summary>Evenement : interaction effectuee.</summary>
    public bool JustInteracted { get; private set; }
    /// <summary>Evenement : mort.</summary>
    public bool JustDied { get; private set; }

    /// <summary>Temps restant avant le reset apres la mort.</summary>
    float deathTimer;
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
        if (c.JustLanded) { if (c.LandingSpeed >= crashSpeed) JustCrashed = true; else JustLanded = true; }
        if (IsDead) return;
        Bounds b = GM.Player.Collider.bounds;
        int n = Physics2D.OverlapBox(b.center, (Vector2)b.size + detectPadding, 0f, filter, hits);
        Interactable = null;
        for (int k = 0; k < n && !IsDead; k++)
        {
            Collider2D h = hits[k];
            if (h == GM.Player.Collider) continue;
            if (h.TryGetComponent(out Entity e) && e.isActiveAndEnabled) e.OnPlayerTouch();
            if (Interactable == null && h.TryGetComponent(out IInteractable it)) Interactable = it;
        }
    }

    /// <summary>Frame : interaction et compte a rebours de la mort.</summary>
    internal void Tick()
    {
        if (IsDead) { if ((deathTimer -= Time.deltaTime) <= 0f) GM.Level.ResetToSave(); return; }
        if (GM.Inputs.InteractPressed && Interactable != null) { Interactable.Interact(); JustInteracted = true; }
    }

    /// <summary>Tue le joueur : message de debug, commandes bloquees, reset apres le delai.</summary>
    public void Die()
    {
        if (IsDead) return;
        Debug.Log("[Player] Mort.");
        IsDead = JustDied = true; deathTimer = deathDelay; Interactable = null;
        GM.Player.Controller.Locked = true;
        GM.Player.Body.linearVelocity = Vector2.zero;
    }

    /// <summary>Remet les evenements de la frame a zero.</summary>
    internal void ClearEvents() => JustLanded = JustCrashed = JustInteracted = JustDied = false;

    /// <summary>Remet tous les etats a zero (respawn).</summary>
    internal void ResetState() { IsDead = false; deathTimer = 0f; Interactable = null; ClearEvents(); }
}

/// <summary>Contrat de tout objet avec lequel le joueur peut interagir (touche Interagir).</summary>
public interface IInteractable
{
    /// <summary>Appele quand le joueur interagit avec l'objet.</summary>
    void Interact();
}
