using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Bande de piques (objet unique, distinct de Trap) : un stock de piques alignees en ligne droite le long de l'axe X local, pointes vers +Y local.
/// L'origine de la bande se pose sur la surface ; les piques sont enfoncees de sinkDepth et affichees derriere la terre.
/// Chaque pique verifie par raycast qu'elle repose sur un support ; sinon elle est masquee et sa zone est exclue du collider.
/// Orientable au sol, au plafond ou aux murs par la rotation de l'objet. Les conflits entre bandes (doublons, coins rentrants)
/// sont regles ensemble par SpikeStripResolver.</summary>
[DisallowMultipleComponent]
public class SpikeStrip : Entity
{
    /// <summary>Largeur d'une pique (2 piques par bloc).</summary>
    public const float SpikeWidth = 0.5f;
    /// <summary>Nom de l'enfant qui contient les visuels des piques.</summary>
    const string ContainerName = "Spikes";
    /// <summary>Nom de l'enfant qui contient les piques d'angle (cree seulement en jeu).</summary>
    const string CornerContainerName = "CornerSpikes";

    /// <summary>Stock : nombre de piques de la bande (la longueur et le collider en decoulent).</summary>
    [SerializeField, Min(1)] int count = 2;
    /// <summary>Sprite d'une pique (pivot en bas au centre).</summary>
    [SerializeField] Sprite spikeSprite;
    /// <summary>Hauteur totale d'une pique, partie enfoncee comprise (unites).</summary>
    [SerializeField, Min(0.01f)] float spikeHeight = 0.5f;
    /// <summary>Profondeur a laquelle la base des piques est enfoncee dans la surface (4 px = 0.125).</summary>
    [SerializeField, Min(0f)] float sinkDepth = 0.125f;
    /// <summary>Hauteur de la zone mortelle, en fraction de la partie visible d'une pique.</summary>
    [SerializeField, Range(0.1f, 1f)] float hitboxHeight = 0.6f;
    /// <summary>Retrait de la zone mortelle a chaque extremite d'un groupe de piques (unites).</summary>
    [SerializeField, Min(0f)] float hitboxInset = 0.04f;
    /// <summary>Calques consideres comme support.</summary>
    [SerializeField] LayerMask supportMask = ~0;
    /// <summary>Distance du raycast sous chaque pique pour trouver son support.</summary>
    [SerializeField, Min(0.01f)] float supportProbe = 0.1f;
    /// <summary>Ordre d'affichage des piques (negatif : derriere les tuiles du sol, a l'ordre 0).</summary>
    [SerializeField] int sortingOrder = -1;

    /// <summary>Nombre de piques en stock.</summary>
    public int Count => count;
    /// <summary>Longueur totale de la bande (unites).</summary>
    public float Length => count * SpikeWidth;
    /// <summary>Hauteur visible d'une pique, hors partie enfoncee.</summary>
    public float VisibleHeight => Mathf.Max(0.01f, spikeHeight - sinkDepth);
    /// <summary>Vrai si la bande a deja ete preparee pour le jeu.</summary>
    public bool IsPrepared => prepared;
    /// <summary>Direction des piques (normale de la surface), arrondie aux axes.</summary>
    public Vector2 Up => new(Mathf.Round(transform.up.x), Mathf.Round(transform.up.y));

    /// <summary>Vrai si la bande a deja ete preparee pour le jeu (supports verifies, colliders construits).</summary>
    bool prepared;
    /// <summary>Tampon de resultats de raycast (evite les allocations).</summary>
    static readonly List<RaycastHit2D> rayHits = new();

    /// <summary>Preparation au jeu, appelee par Entity.Init (EntityManager) avant la collecte des colliders.
    /// La premiere bande preparee regle toutes les bandes de la scene d'un coup.</summary>
    protected override void Prepare()
    {
        if (prepared) return;

        SpikeStripResolver.ResolveAll();

        if (!prepared) Debug.LogError($"[SpikeStrip] {name} n'a pas ete preparee par le resolveur.", this);
    }

    /// <summary>Applique le resultat de l'analyse : piques masquees, piques d'angle creees, colliders reconstruits.</summary>
    public void ApplyResolution(SpikeState[] states, List<SpikeCorner> corners)
    {
        if (states == null || states.Length != count)
        { Debug.LogError($"[SpikeStrip] {name} : analyse invalide, bande ignoree.", this); return; }

        prepared = true;

        Transform box = RebuildVisuals();
        bool[] ok = new bool[count];
        var hidden = new Dictionary<SpikeState, int>();

        for (int i = 0; i < count; i++)
        {
            ok[i] = states[i] == SpikeState.Ok;
            box.GetChild(i).gameObject.SetActive(ok[i]);

            if (!ok[i]) hidden[states[i]] = hidden.TryGetValue(states[i], out int n) ? n + 1 : 1;
        }

        var extra = new List<(Vector2 offset, Vector2 size)>();

        foreach (SpikeCorner c in corners) extra.Add(CreateCorner(c));

        BuildColliders(ok, extra);

        foreach (KeyValuePair<SpikeState, int> kv in hidden) Debug.Log($"[SpikeStrip] {name} : {kv.Value} pique(s) masquee(s) ({kv.Key}).", this);
    }

    /// <summary>Cree le visuel d'une pique d'angle : la pique normale tournee de 45 degres, milieu de la base dans le coin
    /// (les bouts de la base sont caches dans les deux surfaces). Retourne sa zone mortelle en local.</summary>
    (Vector2 offset, Vector2 size) CreateCorner(SpikeCorner c)
    {
        Transform box = transform.Find(CornerContainerName);

        if (box == null)
        { box = new GameObject(CornerContainerName).transform; box.SetParent(transform, false); }

        var go = new GameObject($"CornerSpike_{box.childCount}");
        go.transform.SetParent(box, false);
        float angle = Mathf.Atan2(c.Normals.y, c.Normals.x) * Mathf.Rad2Deg - 90f;
        go.transform.SetPositionAndRotation(c.Point, Quaternion.Euler(0f, 0f, angle));

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = spikeSprite; r.sortingOrder = sortingOrder;

        float reach = VisibleHeight * hitboxHeight;
        Vector2 center = c.Point + c.Normals * (reach * 0.5f);
        Vector2 local = transform.InverseTransformPoint(center);

        return (local, new Vector2(reach, reach));
    }

    /// <summary>Hors scene de jeu (pas de GameManager), la bande se prepare seule.</summary>
    void Start()
    {
        if (!prepared && (GM == null || !GM.InGame)) Prepare();
    }

    /// <summary>Tue le joueur (la mort filtre elle-meme les doublons).</summary>
    public override void OnPlayerTouch()
    {
        if (GM != null) GM.Player.Death.Kill(this);
    }

    /// <summary>Change le stock de piques puis reconstruit les visuels et le collider.</summary>
    public void SetCount(int value)
    {
        count = Mathf.Max(1, value);

        RebuildVisuals();
        BuildColliders(null, null);
    }

    /// <summary>Centre de la case occupee par la pique i (monde) : un demi-bloc colle a la surface.</summary>
    public Vector2 CellCenter(int i) => transform.TransformPoint(LocalBase(i) + new Vector2(0f, SpikeWidth * 0.5f));

    /// <summary>Centre de la base de la pique i (local), au niveau de la surface.</summary>
    public static Vector2 LocalBase(int i) => new((i + 0.5f) * SpikeWidth, 0f);

    /// <summary>Vrai si un support solide se trouve sous la pique i, a moins de supportProbe.</summary>
    public bool IsSupported(int i) => CastSupport(transform.TransformPoint(LocalBase(i)), 0.02f, supportProbe + 0.02f, out _);

    /// <summary>Cherche la surface la plus proche sous les bases des piques, de range au-dessus a range en dessous.
    /// Retourne le decalage a appliquer le long de transform.up pour poser l'origine sur cette surface.</summary>
    public bool FindSurface(float range, out float offset)
    {
        offset = 0f;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            if (!CastSupport(transform.TransformPoint(LocalBase(i)), range, range * 2f, out float d)) continue;

            float o = range - d;

            if (!found || Mathf.Abs(o) < Mathf.Abs(offset))
            { offset = o; found = true; }
        }

        return found;
    }

    /// <summary>Raycast vers le support depuis base + up * lift, sur distance ; ignore triggers, corps dynamiques et la bande elle-meme.</summary>
    bool CastSupport(Vector2 basePoint, float lift, float distance, out float hitDistance)
    {
        hitDistance = 0f;
        Vector2 down = -transform.up;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = supportMask };

        int n = Physics2D.Raycast(basePoint - down * lift, down, filter, rayHits, distance);

        for (int k = 0; k < n; k++)
        {
            Collider2D c = rayHits[k].collider;

            if (c == null || c.transform.IsChildOf(transform)) continue;

            Rigidbody2D rb = c.attachedRigidbody;

            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            { hitDistance = rayHits[k].distance; return true; }
        }

        return false;
    }

    /// <summary>Cree ou met a jour un visuel par pique (enfonce de sinkDepth), supprime les visuels en trop.</summary>
    public Transform RebuildVisuals()
    {
        Transform box = transform.Find(ContainerName);

        if (box == null)
        { box = new GameObject(ContainerName).transform; box.SetParent(transform, false); }

        if (spikeSprite == null) Debug.LogWarning($"[SpikeStrip] {name} : aucun sprite de pique assigne.", this);

        for (int i = 0; i < count; i++)
        {
            Transform t = i < box.childCount ? box.GetChild(i) : NewSpike(box);
            t.name = $"Spike_{i}"; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            t.localPosition = LocalBase(i) - new Vector2(0f, sinkDepth);

            if (!t.TryGetComponent(out SpriteRenderer r)) r = t.gameObject.AddComponent<SpriteRenderer>();

            r.sprite = spikeSprite; r.sortingOrder = sortingOrder;
        }

        for (int i = box.childCount - 1; i >= count; i--) RemoveChild(box.GetChild(i).gameObject);

        return box;
    }

    /// <summary>Construit un BoxCollider2D (trigger) par groupe contigu de piques actives, sur la partie visible ; null = toutes actives.
    /// Les zones en plus (piques d'angle) recoivent chacune leur collider.</summary>
    void BuildColliders(bool[] ok, List<(Vector2 offset, Vector2 size)> extra)
    {
        var runs = new List<(int start, int len)>();

        for (int i = 0; i < count; i++)
        {
            if (ok != null && !ok[i]) continue;

            if (runs.Count > 0 && runs[^1].start + runs[^1].len == i) runs[^1] = (runs[^1].start, runs[^1].len + 1);
            else runs.Add((i, 1));
        }

        float h = VisibleHeight * hitboxHeight;
        var boxes = new List<(Vector2 offset, Vector2 size)>();

        foreach ((int start, int len) in runs)
        {
            float w = Mathf.Max(0.01f, len * SpikeWidth - hitboxInset * 2f);
            boxes.Add((new Vector2((start + len * 0.5f) * SpikeWidth, h * 0.5f), new Vector2(w, h)));
        }

        if (extra != null) boxes.AddRange(extra);

        var cols = new List<BoxCollider2D>(GetComponents<BoxCollider2D>());

        while (cols.Count < boxes.Count) cols.Add(gameObject.AddComponent<BoxCollider2D>());

        for (int k = cols.Count - 1; k >= boxes.Count; k--)
        { RemoveComponent(cols[k]); cols.RemoveAt(k); }

        for (int k = 0; k < boxes.Count; k++)
        { cols[k].isTrigger = true; cols[k].offset = boxes[k].offset; cols[k].size = boxes[k].size; }
    }

    /// <summary>Cree un enfant de pique vide.</summary>
    static Transform NewSpike(Transform box)
    {
        var t = new GameObject("Spike").transform;
        t.SetParent(box, false);

        return t;
    }

    /// <summary>Supprime un enfant (immediatement en edition, une pique d'un prefab est seulement desactivee).</summary>
    static void RemoveChild(GameObject go)
    {
#if UNITY_EDITOR
        bool fromPrefab = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(go) && !UnityEditor.PrefabUtility.IsAddedGameObjectOverride(go);

        if (!Application.isPlaying && fromPrefab)
        { go.SetActive(false); return; }
#endif
        DestroyImmediate(go);
    }

    /// <summary>Supprime un collider en trop (immediatement, pour qu'Entity.Init ne le collecte pas).</summary>
    static void RemoveComponent(Component c) => DestroyImmediate(c);

#if UNITY_EDITOR
    /// <summary>Edition : reconstruit les visuels apres une modification dans l'Inspector (differe, Unity interdit de creer des objets dans OnValidate).</summary>
    void OnValidate()
    {
        if (Application.isPlaying || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;

            RebuildVisuals();
            BuildColliders(null, null);
        };
    }

    /// <summary>Gizmos : contour de la partie visible, puis par pique : base verte (posee), croix rouge (sans support),
    /// croix jaune (doublon), croix magenta (passage trop etroit), croix et fleche violettes (remplacee par une pique d'angle).</summary>
    void OnDrawGizmos()
    {
        SpikeAnalysis a = SpikeStripResolver.AnalyzeForGizmos();

        if (!a.States.TryGetValue(this, out SpikeState[] states) || states.Length != count) return;

        Matrix4x4 m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(Length * 0.5f, VisibleHeight * 0.5f), new Vector3(Length, VisibleHeight));

        for (int i = 0; i < count; i++)
        {
            Vector3 b = LocalBase(i), hw = new(SpikeWidth * 0.4f, 0f), d = new(0f, SpikeWidth * 0.2f);
            Gizmos.color = GizmoColor(states[i]);

            if (states[i] == SpikeState.Ok)
            { Gizmos.DrawLine(b - hw, b + hw); continue; }

            Gizmos.DrawLine(b - hw - d, b + hw + d);
            Gizmos.DrawLine(b - hw + d, b + hw - d);
        }

        Gizmos.matrix = m;
        Gizmos.color = GizmoColor(SpikeState.Corner);

        foreach (SpikeCorner c in a.Corners)
        { if (c.Owner == this) Gizmos.DrawLine(c.Point, c.Point + c.Normals * (SpikeWidth * 0.7f)); }
    }

    /// <summary>Couleur de gizmo d'un etat de pique.</summary>
    static Color GizmoColor(SpikeState s) => s switch
    {
        SpikeState.Ok => Color.green,
        SpikeState.Unsupported => Color.red,
        SpikeState.Duplicate => Color.yellow,
        SpikeState.Narrow => Color.magenta,
        _ => new Color(0.6f, 0.2f, 1f),
    };
#endif
}
