using System;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Racine 1 : sauvegarde la progression et replace l'environnement (joueur, pieces) selon elle ; gere la fin du niveau.</summary>
[Serializable]
public class LevelManager
{
    /// <summary>Message affiche a la fin du niveau.</summary>
    [SerializeField] string finishMessage = "Niveau termine !";
    /// <summary>Duree d'affichage du message de fin avant le retour au menu (s, temps reel).</summary>
    [SerializeField, Min(0f)] float finishDelay = 2.5f;

    /// <summary>Demande d'appliquer la sauvegarde au prochain chargement de scene (bouton Continuer).</summary>
    public bool ContinueRequested { get; set; }
    /// <summary>Vrai si le niveau est termine.</summary>
    public bool IsFinished { get; private set; }
    /// <summary>Vrai si le joueur est dans au moins une zone bloquante.</summary>
    public bool IsBlocked => blockers.Count > 0;

    /// <summary>Zones bloquantes contenant le joueur.</summary>
    readonly HashSet<BlockZone> blockers = new();
    /// <summary>Derniere sauvegarde (en memoire ; position de depart tant qu'aucune n'est faite).</summary>
    SaveData last;
    /// <summary>Position du joueur au pas physique precedent.</summary>
    Vector2 lastPos;
    /// <summary>Vrai si le joueur a bouge au pas precedent.</summary>
    bool movedBefore;
    /// <summary>Temps restant avant le retour au menu apres la fin.</summary>
    float finishTimer;

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => last = new SaveData();

    /// <summary>Prepare le niveau : point de depart, puis application de la sauvegarde si Continuer.</summary>
    internal void Bind()
    {
        blockers.Clear(); IsFinished = false; movedBefore = false; finishTimer = 0f;
        lastPos = GM.Player.Position;
        last = new SaveData { Position = lastPos };

        ShowFinish(false);

        if (ContinueRequested && SaveSystem.TryLoad(out SaveData s) && !s.Finished)
        {
            last = s; GM.Entities.LoadCoins(s.Coins); GM.Player.ResetTo(s.Position); lastPos = s.Position;
        }

        ContinueRequested = false;
    }

    /// <summary>Physique : sauvegarde une seule fois, a la premiere frame ou la situation est adequate.</summary>
    internal void FixedTick()
    {
        Vector2 pos = GM.Player.Position;
        bool moved = (pos - lastPos).sqrMagnitude > 1e-6f;
        lastPos = pos;

        if (CanSave(moved)) Save();

        movedBefore = moved;
    }

    /// <summary>Au sol, immobile, a bouge juste avant, pas bloque, ni mort ni fini.</summary>
    bool CanSave(bool moved) => !moved && movedBefore && GM.Player.Controller.IsGrounded && !IsBlocked && !GM.Player.Death.IsDead && !IsFinished;

    /// <summary>Frame : compte a rebours de fin de niveau (temps reel, insensible a la pause).</summary>
    internal void Tick()
    {
        if (IsFinished && (finishTimer -= Time.unscaledDeltaTime) <= 0f)
        { IsFinished = false; GM.LoadMenu(); }
    }

    /// <summary>Delie le niveau.</summary>
    internal void Unbind() { blockers.Clear(); IsFinished = false; }

    /// <summary>Sauvegarde la progression (position + pieces) en JSON.</summary>
    public void Save()
    {
        GM.Entities.CommitCoins();

        last = new SaveData { Position = GM.Player.Position, Coins = new List<string>(GM.Entities.SavedCoinIds) };
        SaveSystem.Write(last);
    }

    /// <summary>Retour a la derniere sauvegarde : pieces non sauvegardees remises, joueur replace.</summary>
    public void ResetToSave()
    {
        GM.Entities.RevertCoins();
        GM.Player.ResetTo(last.Position);

        lastPos = last.Position; movedBefore = false;
    }

    /// <summary>Termine le niveau : message central, commandes bloquees, sauvegarde verrouillee, retour au menu apres le delai.</summary>
    public void Finish()
    {
        if (IsFinished) return;

        IsFinished = true; finishTimer = finishDelay;
        GM.Player.Controller.Locked = true;

        last.Finished = true; SaveSystem.Write(last);

        ShowFinish(true);
    }

    /// <summary>Ajoute/retire une zone bloquante contenant le joueur.</summary>
    public void SetBlocked(BlockZone zone, bool inside) { if (inside) blockers.Add(zone); else blockers.Remove(zone); }

    /// <summary>Affiche/masque le message de fin.</summary>
    void ShowFinish(bool on)
    {
        var t = GM.Linker != null ? GM.Linker.FinishText : null;

        if (t == null)
        { if (on) Debug.Log($"[Level] {finishMessage}"); return; }

        t.text = finishMessage; t.gameObject.SetActive(on);
    }
}
