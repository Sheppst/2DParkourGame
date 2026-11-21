using UnityEngine;
using static GameManager;

/// <summary>Point d'arrivee : une arche vide tant qu'il reste des pieces, puis un portail qui termine le niveau au contact.</summary>
public class Finish : Entity
{
    /// <summary>Visuel du portail (enfant), affiche seulement une fois toutes les pieces recoltees.</summary>
    [SerializeField] GameObject portal;

    /// <summary>Vrai si toutes les pieces ont ete recoltees : le portail est ouvert.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>Verifie la reference du portail.</summary>
    protected override void Prepare()
    {
        if (portal == null) Debug.LogWarning($"[Finish] {name} : aucun visuel de portail assigne, l'ouverture ne se verra pas.", this);
    }

    /// <summary>Ouvre ou ferme le portail (appele par l'EntityManager quand le nombre de pieces change).</summary>
    public void SetOpen(bool open)
    {
        IsOpen = open;

        if (portal != null && portal.activeSelf != open) portal.SetActive(open);
    }

    /// <summary>Termine le niveau si le portail est ouvert ; sinon il ne se passe rien.</summary>
    public override void OnPlayerTouch() { if (IsOpen) GM.Level.Finish(); }
}
