using System.Collections.Generic;
using UnityEngine;

/// <summary>Etat d'une pique apres analyse de toutes les bandes de la scene.</summary>
public enum SpikeState
{
    /// <summary>Posee sur un support, sans conflit : visible.</summary>
    Ok,
    /// <summary>Aucun support dessous : masquee.</summary>
    Unsupported,
    /// <summary>Doublon d'une pique de meme orientation d'une bande prioritaire : masquee.</summary>
    Duplicate,
    /// <summary>Face a face avec une pique opposee dans la meme case (passage trop etroit, erreur de niveau) : masquee.</summary>
    Narrow,
    /// <summary>Coin rentrant avec une pique perpendiculaire : masquee, remplacee par une pique d'angle.</summary>
    Corner
}

/// <summary>Pique d'angle a creer dans un coin rentrant.</summary>
public struct SpikeCorner
{
    /// <summary>Bande qui porte la pique d'angle (la prioritaire des deux).</summary>
    public SpikeStrip Owner;
    /// <summary>Point du coin, a la rencontre des deux surfaces (monde).</summary>
    public Vector2 Point;
    /// <summary>Somme des deux normales des surfaces (composantes de -1 a 1) : la pique pointe dans cette direction.</summary>
    public Vector2 Normals;
}

/// <summary>Probleme de niveau detecte : deux piques opposees dans la meme case.</summary>
public struct SpikeNarrow
{
    /// <summary>Bande qui garde sa pique.</summary>
    public SpikeStrip Kept;
    /// <summary>Bande qui perd sa pique.</summary>
    public SpikeStrip Lost;
    /// <summary>Centre de la case en conflit (monde).</summary>
    public Vector2 Cell;
}

/// <summary>Resultat de l'analyse des bandes de piques.</summary>
public sealed class SpikeAnalysis
{
    /// <summary>Etat de chaque pique, par bande.</summary>
    public readonly Dictionary<SpikeStrip, SpikeState[]> States = new();
    /// <summary>Piques d'angle a creer.</summary>
    public readonly List<SpikeCorner> Corners = new();
    /// <summary>Passages trop etroits a corriger dans le niveau.</summary>
    public readonly List<SpikeNarrow> Narrows = new();
}

/// <summary>Analyse toutes les bandes de piques ensemble : supports, doublons (meme orientation), coins rentrants (orientations
/// perpendiculaires) et passages trop etroits (orientations opposees). La bande la plus haute dans la Hierarchy est prioritaire.</summary>
public static class SpikeStripResolver
{
    /// <summary>Une pique d'une bande.</summary>
    struct Entry
    {
        /// <summary>Bande de la pique.</summary>
        public SpikeStrip Strip;
        /// <summary>Index de la pique dans la bande.</summary>
        public int Index;
        /// <summary>Direction de la pique (normale de sa surface, arrondie aux axes).</summary>
        public Vector2 Up;
    }

#if UNITY_EDITOR
    /// <summary>Derniere analyse faite pour les gizmos.</summary>
    static SpikeAnalysis cached;
    /// <summary>Heure de la derniere analyse des gizmos.</summary>
    static double cachedAt = -1d;
#endif

    /// <summary>Analyse puis applique le resultat a toutes les bandes pas encore preparees de la scene.</summary>
    public static void ResolveAll()
    {
        List<SpikeStrip> strips = FindAll();

        if (strips.Count == 0) return;

        foreach (UnityEngine.Tilemaps.TilemapCollider2D t in Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapCollider2D>(FindObjectsSortMode.None))
            t.ProcessTilemapChanges();

        Physics2D.SyncTransforms();

        foreach (SpikeStrip s in strips) s.RebuildVisuals();

        SpikeAnalysis a = Analyze(strips);

        foreach (SpikeStrip s in strips)
        { if (!s.IsPrepared) s.ApplyResolution(a.States[s], a.Corners.FindAll(c => c.Owner == s)); }

        foreach (SpikeNarrow n in a.Narrows)
            Debug.LogWarning($"[SpikeStripResolver] Passage trop etroit vers ({n.Cell.x:0.##}, {n.Cell.y:0.##}) : '{n.Kept.name}' et '{n.Lost.name}' "
                + "ont des piques face a face dans la meme case. Le joueur ne peut pas passer : corriger le niveau.", n.Lost);
    }

    /// <summary>Toutes les bandes de la scene (y compris desactivees), triees par priorite (Hierarchy).</summary>
    public static List<SpikeStrip> FindAll()
    {
        var list = new List<SpikeStrip>(Object.FindObjectsByType<SpikeStrip>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        list.RemoveAll(s => s == null || !s.gameObject.scene.IsValid());
        list.Sort((x, y) => CompareHierarchy(x.transform, y.transform));

        return list;
    }

    /// <summary>Analyse les bandes (deja triees par priorite) sans rien modifier.</summary>
    public static SpikeAnalysis Analyze(List<SpikeStrip> strips)
    {
        var a = new SpikeAnalysis();
        var cells = new Dictionary<Vector2Int, List<Entry>>();

        foreach (SpikeStrip s in strips)
        {
            var states = new SpikeState[s.Count];
            Vector2 up = s.Up;

            for (int i = 0; i < s.Count; i++)
            {
                states[i] = s.IsSupported(i) ? SpikeState.Ok : SpikeState.Unsupported;

                if (states[i] != SpikeState.Ok) continue;

                Vector2Int key = CellKey(s.CellCenter(i));

                if (!cells.TryGetValue(key, out List<Entry> list))
                { list = new List<Entry>(); cells[key] = list; }

                list.Add(new Entry { Strip = s, Index = i, Up = up });
            }

            a.States[s] = states;
        }

        foreach (KeyValuePair<Vector2Int, List<Entry>> kv in cells)
        { if (kv.Value.Count > 1) ResolveCell(a, kv.Value); }

        return a;
    }

    /// <summary>Regle les conflits d'une case (piques deja dans l'ordre de priorite).</summary>
    static void ResolveCell(SpikeAnalysis a, List<Entry> entries)
    {
        var kept = new List<Entry>();

        foreach (Entry e in entries)
        {
            Entry? same = Find(kept, e.Up, 0.5f), opposite = Find(kept, -e.Up, 0.5f);

            if (same.HasValue)
            { a.States[e.Strip][e.Index] = SpikeState.Duplicate; continue; }

            if (opposite.HasValue)
            {
                a.States[e.Strip][e.Index] = SpikeState.Narrow;
                a.Narrows.Add(new SpikeNarrow { Kept = opposite.Value.Strip, Lost = e.Strip, Cell = e.Strip.CellCenter(e.Index) });
                continue;
            }

            kept.Add(e);
        }

        if (kept.Count < 2) return;

        Entry p = kept[0], q = kept[1];
        Vector2 normals = p.Up + q.Up;
        a.States[p.Strip][p.Index] = a.States[q.Strip][q.Index] = SpikeState.Corner;

        Vector2 point = p.Strip.CellCenter(p.Index) - normals * (SpikeStrip.SpikeWidth * 0.5f);
        a.Corners.Add(new SpikeCorner { Owner = p.Strip, Point = point, Normals = normals });
    }

    /// <summary>Premiere pique gardee dont la direction est proche de dir (produit scalaire superieur a min).</summary>
    static Entry? Find(List<Entry> kept, Vector2 dir, float min)
    {
        foreach (Entry k in kept)
        { if (Vector2.Dot(k.Up, dir) > min) return k; }

        return null;
    }

    /// <summary>Cle de case : centre arrondi au quart d'unite.</summary>
    static Vector2Int CellKey(Vector2 c) => new(Mathf.RoundToInt(c.x * 4f), Mathf.RoundToInt(c.y * 4f));

    /// <summary>Compare deux objets selon leur ordre dans la Hierarchy (le plus haut d'abord).</summary>
    static int CompareHierarchy(Transform x, Transform y)
    {
        List<int> px = Path(x), py = Path(y);

        for (int i = 0; i < Mathf.Min(px.Count, py.Count); i++)
        { if (px[i] != py[i]) return px[i].CompareTo(py[i]); }

        return px.Count.CompareTo(py.Count);
    }

    /// <summary>Chemin d'index de freres depuis la racine de la scene.</summary>
    static List<int> Path(Transform t)
    {
        var p = new List<int>();

        for (Transform c = t; c != null; c = c.parent) p.Insert(0, c.GetSiblingIndex());

        return p;
    }

#if UNITY_EDITOR
    /// <summary>Analyse de toute la scene pour les gizmos, recalculee au plus toutes les 0.2 s.</summary>
    public static SpikeAnalysis AnalyzeForGizmos()
    {
        double now = UnityEditor.EditorApplication.timeSinceStartup;

        if (cached != null && now - cachedAt < 0.2d) return cached;

        cached = Analyze(FindAll());
        cachedAt = now;

        return cached;
    }
#endif
}
