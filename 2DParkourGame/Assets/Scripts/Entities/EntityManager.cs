using System;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Racine 1 : liste toutes les entites (hors joueur), les active/desactive sur demande ou selon la distance au joueur, et compte les pieces.</summary>
[Serializable]
public class EntityManager
{
    /// <summary>Distance au joueur au-dela de laquelle une entite est desactivee (objet entier).</summary>
    [SerializeField, Min(0f)] float activationDistance = 25f;
    /// <summary>Intervalle entre deux verifications de distance (s).</summary>
    [SerializeField, Min(0f)] float checkInterval = 0.2f;

    /// <summary>Nombre de pieces obtenues (sauvegardees + ramassees depuis la derniere sauvegarde).</summary>
    public int Coins { get; private set; }
    /// <summary>Nombre total de pieces du niveau.</summary>
    public int TotalCoins { get; private set; }
    /// <summary>Toutes les entites de la scene.</summary>
    public IReadOnlyList<Entity> All => entities;
    /// <summary>Identifiants des pieces validees par la derniere sauvegarde.</summary>
    public IEnumerable<string> SavedCoinIds => savedCoins;

    /// <summary>Entites de la scene.</summary>
    readonly List<Entity> entities = new();
    /// <summary>Entites par identifiant.</summary>
    readonly Dictionary<string, Entity> byId = new();
    /// <summary>Pieces validees par la derniere sauvegarde.</summary>
    readonly HashSet<string> savedCoins = new();
    /// <summary>Pieces ramassees depuis la derniere sauvegarde.</summary>
    readonly List<Entity> pendingCoins = new();
    /// <summary>Points d'arrivee du niveau (portails ouverts seulement quand toutes les pieces sont recoltees).</summary>
    readonly List<Finish> finishes = new();
    /// <summary>Temps restant avant la prochaine verification de distance.</summary>
    float timer;

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => Clear();

    /// <summary>Recupere et prepare toutes les entites de la scene.</summary>
    internal void Bind()
    {
        Clear();

        foreach (Entity e in GM.Linker.FindEntities())
        {
            e.Init();

            if (!byId.TryAdd(e.Id, e)) Debug.LogWarning($"[EntityManager] Deux entites au meme endroit ({e.Id}) : '{e.name}' ignoree par la sauvegarde.", e);

            entities.Add(e);

            if (e is Coin) TotalCoins++;
            else if (e is Finish f) finishes.Add(f);
        }

        RefreshFinishes();
    }

    /// <summary>Frame : active/desactive les entites selon leur distance au joueur.</summary>
    internal void Tick()
    {
        if ((timer -= Time.deltaTime) > 0f) return;

        timer = checkInterval;
        Vector2 p = GM.Player.Position;
        float max = activationDistance * activationDistance;

        foreach (Entity e in entities)
        {
            if (e == null || e.Consumed) continue;

            bool near = ((Vector2)e.transform.position - p).sqrMagnitude <= max;

            if (e.gameObject.activeSelf != near) e.gameObject.SetActive(near);
        }
    }

    /// <summary>Vide toutes les listes.</summary>
    internal void Unbind() => Clear();

    /// <summary>Remet l'etat a zero.</summary>
    void Clear() { entities.Clear(); byId.Clear(); savedCoins.Clear(); pendingCoins.Clear(); finishes.Clear(); Coins = TotalCoins = 0; timer = 0f; }

    /// <summary>Ouvre les portails si toutes les pieces sont recoltees, les ferme sinon (appele a chaque changement du compteur).</summary>
    void RefreshFinishes()
    {
        bool open = Coins >= TotalCoins;

        foreach (Finish f in finishes) if (f != null) f.SetOpen(open);
    }

    /// <summary>Ramasse une piece : desactivee (pas detruite), compteur incremente, en attente de sauvegarde.</summary>
    public void Collect(Coin coin)
    {
        if (coin == null || coin.Consumed) return;

        coin.SetConsumed(true); pendingCoins.Add(coin); Coins++;

        RefreshFinishes();
    }

    /// <summary>Valide les pieces ramassees (appele lors d'une sauvegarde).</summary>
    public void CommitCoins() { foreach (Entity c in pendingCoins) savedCoins.Add(c.Id); pendingCoins.Clear(); }

    /// <summary>Annule les pieces non sauvegardees : elles reapparaissent (appele au reset).</summary>
    public void RevertCoins()
    {
        foreach (Entity c in pendingCoins) if (c != null) c.SetConsumed(false);

        pendingCoins.Clear(); Coins = savedCoins.Count;

        RefreshFinishes();
    }

    /// <summary>Applique les pieces d'une sauvegarde chargee.</summary>
    public void LoadCoins(IEnumerable<string> ids)
    {
        RevertCoins(); savedCoins.Clear();

        if (ids != null) foreach (string id in ids) if (byId.TryGetValue(id, out Entity e) && e is Coin)
        { savedCoins.Add(id); e.SetConsumed(true); }

        Coins = savedCoins.Count; timer = 0f;

        RefreshFinishes();
    }

    /// <summary>Active/desactive la fonction d'une entite sur demande.</summary>
    public void SetFunction(Entity e, bool on) { if (e != null) e.SetFunction(on); }

    /// <summary>Active/desactive les graphismes d'une entite sur demande.</summary>
    public void SetGraphics(Entity e, bool on) { if (e != null) e.SetGraphics(on); }
}
