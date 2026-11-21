using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Outil editeur automatique : apres qu'un piege (SpikeStrip ou Trap) a ete lache et n'a plus ete modifie pendant snapDelay secondes,
/// l'aligne sur la metrique des blocs (rotation au quart de tour, grille des demi-blocs) et le colle a la surface detectee a proximite.
/// Chaque alignement s'annule avec Ctrl+Z.</summary>
[InitializeOnLoad]
public static class TrapSnapper
{
    /// <summary>Delai d'inactivite avant l'alignement (s).</summary>
    const double SnapDelay = 3.0;
    /// <summary>Distance de recherche de la surface, de part et d'autre de la position actuelle (unites).</summary>
    const float SearchRange = 0.5f;
    /// <summary>Pas de la grille d'alignement (demi-bloc, la largeur d'une pique).</summary>
    const float Grid = 0.5f;

    /// <summary>Pieges modifies en attente d'alignement, avec l'heure de leur derniere modification.</summary>
    static readonly Dictionary<Component, double> pending = new();
    /// <summary>Tampon de resultats de raycast (evite les allocations).</summary>
    static readonly List<RaycastHit2D> hits = new();

    /// <summary>S'abonne aux modifications de la scene et a la boucle de l'editeur.</summary>
    static TrapSnapper()
    {
        ObjectChangeEvents.changesPublished += OnChanges;
        EditorApplication.update += Tick;
    }

    /// <summary>Repere les pieges crees ou modifies dans la scene.</summary>
    static void OnChanges(ref ObjectChangeEventStream stream)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        for (int i = 0; i < stream.length; i++)
        {
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var p); Mark(p.instanceId); break;
                case ObjectChangeKind.CreateGameObjectHierarchy:
                    stream.GetCreateGameObjectHierarchyEvent(i, out var c); Mark(c.instanceId); break;
                case ObjectChangeKind.ChangeGameObjectParent:
                    stream.GetChangeGameObjectParentEvent(i, out var r); Mark(r.instanceId); break;
            }
        }
    }

    /// <summary>Met en attente le piege auquel appartient l'objet modifie (lui-meme ou un parent).</summary>
    static void Mark(int id)
    {
        Object o = EditorUtility.EntityIdToObject(id);
        GameObject go = o as GameObject;

        if (go == null && o is Component comp) go = comp.gameObject;

        if (go == null || EditorUtility.IsPersistent(go)) return;

        Component trap = Target(go);

        if (trap != null) pending[trap] = EditorApplication.timeSinceStartup;
    }

    /// <summary>Piege concerne par un objet : une SpikeStrip ou un Trap, sur l'objet ou un de ses parents.</summary>
    static Component Target(GameObject go)
    {
        SpikeStrip strip = go.GetComponentInParent<SpikeStrip>(true);

        return strip != null ? strip : go.GetComponentInParent<Trap>(true);
    }

    /// <summary>Boucle de l'editeur : aligne les pieges inactifs depuis SnapDelay, jamais pendant un glisser.</summary>
    static void Tick()
    {
        if (pending.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode || GUIUtility.hotControl != 0) return;

        double now = EditorApplication.timeSinceStartup;
        var ready = new List<Component>();

        foreach (KeyValuePair<Component, double> kv in pending)
        { if (now - kv.Value >= SnapDelay) ready.Add(kv.Key); }

        if (ready.Count == 0) return;

        Physics2D.SyncTransforms();

        foreach (Component o in ready)
        {
            pending.Remove(o);

            if (o == null) continue;

            if (o is SpikeStrip s) SnapStrip(s);
            else if (o is Trap t) SnapTrap(t);
        }
    }

    /// <summary>Aligne une bande : rotation au quart de tour, debut sur la grille des demi-blocs, origine collee a la surface proche.</summary>
    static void SnapStrip(SpikeStrip s)
    {
        Transform tr = s.transform;
        Vector3 pos = tr.position;
        float z = Mathf.Round(tr.eulerAngles.z / 90f) * 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, z);
        Vector3 right = rot * Vector3.right, up = rot * Vector3.up;

        if (Mathf.Abs(right.x) > 0.5f) pos.x = Snap(pos.x);
        else pos.y = Snap(pos.y);

        Apply(tr, pos, rot, "Alignement de la bande de piques");
        Physics2D.SyncTransforms();

        if (!s.FindSurface(SearchRange, out float offset))
        { Debug.Log($"[TrapSnapper] {s.name} : aucune surface a moins de {SearchRange} unite, seulement alignee sur la grille.", s); return; }

        Apply(tr, tr.position + up * offset, rot, "Alignement de la bande de piques");
    }

    /// <summary>Aligne un Trap : bord gauche sur la grille des demi-blocs, bas du collider colle a la surface sous lui.</summary>
    static void SnapTrap(Trap t)
    {
        if (!t.TryGetComponent(out Collider2D col))
        { Debug.LogWarning($"[TrapSnapper] {t.name} n'a pas de Collider2D : alignement impossible.", t); return; }

        Bounds b = col.bounds;
        Vector3 pos = t.transform.position;
        pos.x += Snap(b.min.x) - b.min.x;

        var filter = new ContactFilter2D { useTriggers = false };
        Vector2 origin = new(b.center.x + pos.x - t.transform.position.x, b.min.y + SearchRange);

        int n = Physics2D.Raycast(origin, Vector2.down, filter, hits, SearchRange * 2f);

        for (int k = 0; k < n; k++)
        {
            Collider2D c = hits[k].collider;

            if (c == null || c.transform.IsChildOf(t.transform)) continue;

            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;

            pos.y += hits[k].point.y - b.min.y;
            break;
        }

        Apply(t.transform, pos, t.transform.rotation, "Alignement du piege");
    }

    /// <summary>Arrondit a la grille des demi-blocs.</summary>
    static float Snap(float v) => Mathf.Round(v / Grid) * Grid;

    /// <summary>Applique une position et une rotation avec Undo, seulement si elles changent vraiment (evite une boucle d'alignement).</summary>
    static void Apply(Transform tr, Vector3 pos, Quaternion rot, string label)
    {
        if ((tr.position - pos).sqrMagnitude < 1e-8f && Quaternion.Angle(tr.rotation, rot) < 0.01f) return;

        Undo.RecordObject(tr, label);
        tr.SetPositionAndRotation(pos, rot);
    }
}
