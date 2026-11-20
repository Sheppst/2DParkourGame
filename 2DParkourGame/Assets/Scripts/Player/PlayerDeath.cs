using System;
using UnityEngine;
using static GameManager;

/// <summary>Racine 2 : point d'entree unique de la mort du joueur. Filtre toutes les demandes pour qu'une mort ne soit jamais comptee deux fois.</summary>
[Serializable]
public class PlayerDeath
{
    /// <summary>Duree entre la mort et le retour a la derniere sauvegarde (s).</summary>
    [SerializeField, Min(0f)] float deathDelay = 0.6f;
    /// <summary>Duree d'invulnerabilite apres une reapparition (s), le temps que la physique se recale.</summary>
    [SerializeField, Min(0f)] float respawnGrace = 0.1f;

    /// <summary>Vrai si le joueur est mort (en attente du retour a la sauvegarde).</summary>
    public bool IsDead { get; private set; }
    /// <summary>Vrai pendant la courte invulnerabilite qui suit une reapparition.</summary>
    public bool IsRespawning => graceTimer > 0f;
    /// <summary>Evenement : mort survenue cette frame.</summary>
    public bool JustDied { get; private set; }
    /// <summary>Objet responsable de la derniere mort (null si inconnu).</summary>
    public UnityEngine.Object LastKiller { get; private set; }

    /// <summary>Temps restant avant le retour a la sauvegarde.</summary>
    float deathTimer;
    /// <summary>Temps restant d'invulnerabilite apres reapparition.</summary>
    float graceTimer;

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => ResetState();

    /// <summary>Liaison a la scene.</summary>
    internal void Bind() => ResetState();

    /// <summary>Demande la mort du joueur. Retourne vrai seulement si la mort est acceptee.</summary>
    public bool Kill(UnityEngine.Object source = null)
    {
        string refused = Refusal();

        if (refused != null)
        { if (source != null && !IsDead) Debug.Log($"[PlayerDeath] Mort ignoree ({refused}) : {source.name}.", source); return false; }

        IsDead = JustDied = true; deathTimer = deathDelay; LastKiller = source;

        Debug.Log($"[PlayerDeath] Mort{(source != null ? $" par {source.name}" : "")}.", source);

        GM.Player.Controller.Locked = true;
        GM.Player.Body.linearVelocity = Vector2.zero;

        return true;
    }

    /// <summary>Raison du refus d'une mort, ou null si elle est possible.</summary>
    string Refusal()
    {
        if (GM == null || !GM.InGame || !GM.Player.IsBound) return "aucune scene de jeu liee";
        if (IsDead) return "deja mort";
        if (IsRespawning) return "reapparition en cours";
        if (GM.Level.IsFinished) return "niveau termine";
        if (GM.IsPaused) return "jeu en pause";
        if (Time.timeScale <= 0f) return "temps gele";

        return null;
    }

    /// <summary>Frame : compte a rebours de la mort puis retour a la sauvegarde ; decompte de l'invulnerabilite.</summary>
    internal void Tick()
    {
        float dt = Time.deltaTime;

        if (graceTimer > 0f) graceTimer = Mathf.Max(0f, graceTimer - dt);

        if (!IsDead || (deathTimer -= dt) > 0f) return;

        if (GM.Level.IsFinished)
        { IsDead = false; return; }

        GM.Level.ResetToSave();
    }

    /// <summary>Remet les evenements de la frame a zero.</summary>
    internal void ClearEvents() => JustDied = false;

    /// <summary>Remet tous les etats a zero (reapparition) et lance l'invulnerabilite.</summary>
    internal void ResetState() { IsDead = JustDied = false; deathTimer = 0f; graceTimer = respawnGrace; LastKiller = null; }
}
