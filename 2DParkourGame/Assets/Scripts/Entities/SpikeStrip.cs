using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Bande de piques (objet unique, distinct de Trap) : un stock de piques alignees en ligne droite le long de l'axe X local, pointes vers +Y local.
/// L'origine de la bande se pose sur la surface ; les piques sont enfoncees de sinkDepth et affichees derriere la terre.
/// Chaque pique verifie par raycast qu'elle repose sur un support ; sinon elle est masquee et sa zone est exclue du collider.
/// Orientable au sol, au plafond ou aux murs par la rotation de l'objet.</summary>
[DisallowMultipleComponent]
public class SpikeStrip : Entity
{
    /// <summary>Largeur d'une pique (2 piques par bloc).</summary>
    public const float SpikeWidth = 0.5f;
    /// <summary>Nom de l'enfant qui contient les visuels des piques.</summary>
    const string ContainerName = "Spikes";

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

    /// <summary>Vrai si la bande a deja ete preparee pour le jeu (supports verifies, colliders construits).</summary>
    bool prepared;
    /// <summary>Tampon de resultats de raycast (evite les allocations).</summary>
    static readonly List<RaycastHit2D> rayHits = new();

    /// <summary>Preparation au jeu, appelee par Entity.Init (EntityManager) avant la collecte des colliders.</summary>
    protected override void Prepare()
    {
        if (prepared) return;

        prepared = true;

        Physics2D.SyncTransforms();
        Transform box = RebuildVisuals();

        bool[] ok = new bool[count];

        for (int i = 0; i < count; i++)
        { ok[i] = IsSupported(i); box.GetChild(i).gameObject.SetActive(ok[i]); }

        BuildColliders(ok);

        int hidden = System.Array.FindAll(ok, b => !b).Length;

        if (hidden > 0) Debug.Log($"[SpikeStrip] {name} : {hidden} pique(s) sans support masquee(s).", this);
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
        BuildColliders(null);
    }

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

    /// <summary>Construit un BoxCollider2D (trigger) par groupe contigu de piques actives, sur la partie visible ; null = toutes actives.</summary>
    void BuildColliders(bool[] ok)
    {
        var runs = new List<(int start, int len)>();

        for (int i = 0; i < count; i++)
        {
            if (ok != null && !ok[i]) continue;

            if (runs.Count > 0 && runs[^1].start + runs[^1].len == i) runs[^1] = (runs[^1].start, runs[^1].len + 1);
            else runs.Add((i, 1));
        }

        var cols = new List<BoxCollider2D>(GetComponents<BoxCollider2D>());

        while (cols.Count < runs.Count) cols.Add(gameObject.AddComponent<BoxCollider2D>());

        for (int k = cols.Count - 1; k >= runs.Count; k--)
        { RemoveComponent(cols[k]); cols.RemoveAt(k); }

        float h = VisibleHeight * hitboxHeight;

        for (int k = 0; k < runs.Count; k++)
        {
            float w = Mathf.Max(0.01f, runs[k].len * SpikeWidth - hitboxInset * 2f);
            cols[k].isTrigger = true;
            cols[k].offset = new Vector2((runs[k].start + runs[k].len * 0.5f) * SpikeWidth, h * 0.5f);
            cols[k].size = new Vector2(w, h);
        }
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
            BuildColliders(null);
        };
    }

    /// <summary>Gizmos : contour de la partie visible, base verte si la pique a un support, croix rouge sinon.</summary>
    void OnDrawGizmos()
    {
        Matrix4x4 m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(Length * 0.5f, VisibleHeight * 0.5f), new Vector3(Length, VisibleHeight));

        for (int i = 0; i < count; i++)
        {
            Vector3 b = LocalBase(i), hw = new(SpikeWidth * 0.4f, 0f), d = new(0f, SpikeWidth * 0.2f);
            bool ok = IsSupported(i);
            Gizmos.color = ok ? Color.green : Color.red;

            if (ok)
            { Gizmos.DrawLine(b - hw, b + hw); continue; }

            Gizmos.DrawLine(b - hw - d, b + hw + d);
            Gizmos.DrawLine(b - hw + d, b + hw - d);
        }

        Gizmos.matrix = m;
    }
#endif
}
