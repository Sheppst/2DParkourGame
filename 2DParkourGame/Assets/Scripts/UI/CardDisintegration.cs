using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Racine independante (UI) : desintegre une carte du Canvas en morceaux de pixels.
/// Les morceaux partent vers le haut a droite en retrecissant jusqu'a disparaitre, de l'angle haut droit jusqu'a l'angle bas gauche.
/// La carte est une Image (type Simple) ; pour ignorer les zones transparentes, cocher Read/Write dans l'import de sa texture.</summary>
[RequireComponent(typeof(Image))]
public class CardDisintegration : MonoBehaviour
{
    /// <summary>Nombre maximal de morceaux (4 sommets chacun, limite d'un maillage UI : 65 000 sommets).</summary>
    const int MaxChunks = 16000;

    /// <summary>Cote d'un morceau, en pixels de l'aplat (1 = chaque pixel se detache seul).</summary>
    [SerializeField, Min(1)] int chunkPixels = 4;
    /// <summary>Duree totale de la desintegration, en secondes.</summary>
    [SerializeField, Min(0.05f)] float duration = 1.2f;
    /// <summary>Duree de vie d'un morceau, en proportion de la duree totale (le reste etale les departs sur la carte).</summary>
    [SerializeField, Range(0.05f, 1f)] float chunkLife = 0.45f;
    /// <summary>Direction de depart des morceaux (vers le haut a droite par defaut).</summary>
    [SerializeField] Vector2 direction = new(1f, 1f);
    /// <summary>Ecart aleatoire maximal autour de la direction, en degres.</summary>
    [SerializeField, Range(0f, 90f)] float spreadAngle = 20f;
    /// <summary>Distance parcourue par un morceau, en proportion de la hauteur de la carte.</summary>
    [SerializeField, Min(0f)] float travel = 0.5f;
    /// <summary>Irregularite du front de desintegration (0 = diagonale nette, 1 = ordre totalement aleatoire).</summary>
    [SerializeField, Range(0f, 1f)] float edgeNoise = 0.15f;
    /// <summary>Vrai pour ignorer Time.timeScale (menus en pause ou temps gele).</summary>
    [SerializeField] bool unscaledTime = true;
    /// <summary>Vrai pour desactiver l'objet de la carte a la fin de l'effet.</summary>
    [SerializeField] bool hideWhenDone = true;

    /// <summary>Appele a la fin de la desintegration.</summary>
    public event Action<CardDisintegration> Finished;

    /// <summary>Vrai pendant la desintegration.</summary>
    public bool IsPlaying { get; private set; }
    /// <summary>Vrai une fois la desintegration terminee (jusqu'a ResetCard).</summary>
    public bool IsDone { get; private set; }

    /// <summary>Image de la carte.</summary>
    Image image;
    /// <summary>Graphique des morceaux (enfant cree a la premiere desintegration).</summary>
    PixelChunksGraphic graphic;
    /// <summary>Temps ecoule depuis le debut de l'effet.</summary>
    float timer;
    /// <summary>Tampon des morceaux construits.</summary>
    readonly List<PixelChunk> chunks = new();

    /// <summary>Recupere l'image de la carte.</summary>
    void Awake()
    {
        if (!TryGetComponent(out image)) Debug.LogError("[CardDisintegration] Aucune Image sur la carte.", this);
    }

    /// <summary>Lance la desintegration (ignore si elle est deja en cours ou terminee).</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        if (IsPlaying || IsDone) return;

        if (image == null || image.sprite == null)
        { Debug.LogError($"[CardDisintegration] {name} : aucune Image ou aucun sprite, desintegration impossible.", this); return; }

        if (!BuildChunks() || !EnsureGraphic()) return;

        graphic.color = image.color;
        graphic.Setup(image.sprite.texture, chunks, chunkLife);
        graphic.gameObject.SetActive(true);
        image.enabled = false;

        timer = 0f;
        IsPlaying = true;
    }

    /// <summary>Remet la carte intacte et visible.</summary>
    [ContextMenu("Reset Card")]
    public void ResetCard()
    {
        IsPlaying = false;
        IsDone = false;
        timer = 0f;

        if (graphic != null) graphic.gameObject.SetActive(false);
        if (image != null) image.enabled = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    /// <summary>Avance la minuterie et l'effet ; termine a la fin de la duree.</summary>
    void Update()
    {
        if (!IsPlaying) return;

        if (graphic == null)
        { Debug.LogWarning($"[CardDisintegration] {name} : graphique des morceaux detruit, effet arrete.", this); End(); return; }

        timer += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        graphic.SetTime(timer / duration);

        if (timer >= duration) End();
    }

    /// <summary>Fin de l'effet : masque les morceaux, cache la carte si demande et previent les abonnes.</summary>
    void End()
    {
        IsPlaying = false;
        IsDone = true;

        if (graphic != null) graphic.gameObject.SetActive(false);
        if (hideWhenDone) gameObject.SetActive(false);

        try { Finished?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    /// <summary>Cree le graphique des morceaux, enfant etire sur toute la carte, au premier plan.</summary>
    bool EnsureGraphic()
    {
        if (graphic != null) return true;

        GameObject go = new("Disintegration", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;

        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot = image.rectTransform.pivot;
        rt.SetAsLastSibling();

        graphic = go.AddComponent<PixelChunksGraphic>();
        graphic.raycastTarget = false;

        return graphic != null;
    }

    /// <summary>Decoupe le sprite en morceaux (morceaux transparents ignores si la texture est lisible). Retourne faux en cas d'erreur.</summary>
    bool BuildChunks()
    {
        chunks.Clear();
        Sprite s = image.sprite;
        Texture2D tex = s.texture;

        if (tex == null)
        { Debug.LogError($"[CardDisintegration] {name} : le sprite n'a pas de texture.", this); return false; }

        Rect tr = TextureRect(s);
        int w = Mathf.RoundToInt(tr.width), h = Mathf.RoundToInt(tr.height);

        if (w <= 0 || h <= 0)
        { Debug.LogError($"[CardDisintegration] {name} : sprite de taille nulle.", this); return false; }

        int cp = ChunkSize(w, h);
        Color[] px = ReadPixels(tex, tr, w, h);
        Rect r = image.rectTransform.rect;
        Vector2 unit = new(r.width / w, r.height / h), uvUnit = new(1f / tex.width, 1f / tex.height);

        for (int y0 = 0; y0 < h; y0 += cp)
            for (int x0 = 0; x0 < w; x0 += cp)
            {
                int x1 = Mathf.Min(x0 + cp, w), y1 = Mathf.Min(y0 + cp, h);

                if (px != null && Transparent(px, w, x0, y0, x1, y1)) continue;

                chunks.Add(Chunk(r, unit, uvUnit, tr, w, h, x0, y0, x1, y1));
            }

        return true;
    }

    /// <summary>Construit un morceau : position, UV, depart (diagonale haut droit vers bas gauche, bord irregulier) et deplacement.</summary>
    PixelChunk Chunk(Rect r, Vector2 unit, Vector2 uvUnit, Rect tr, int w, int h, int x0, int y0, int x1, int y1)
    {
        float u = (x0 + x1) * 0.5f / w, v = (y0 + y1) * 0.5f / h;
        float key = Mathf.Lerp((2f - u - v) * 0.5f, UnityEngine.Random.value, edgeNoise);
        float angle = UnityEngine.Random.Range(-spreadAngle, spreadAngle);
        Vector2 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;

        return new PixelChunk
        {
            Center = new Vector2(r.xMin + (x0 + x1) * 0.5f * unit.x, r.yMin + (y0 + y1) * 0.5f * unit.y),
            Half = new Vector2((x1 - x0) * unit.x, (y1 - y0) * unit.y) * 0.5f,
            UvMin = new Vector2((tr.x + x0) * uvUnit.x, (tr.y + y0) * uvUnit.y),
            UvMax = new Vector2((tr.x + x1) * uvUnit.x, (tr.y + y1) * uvUnit.y),
            Offset = (Vector2)(Quaternion.Euler(0f, 0f, angle) * dir) * (travel * r.height * UnityEngine.Random.Range(0.7f, 1f)),
            Start = key * (1f - chunkLife)
        };
    }

    /// <summary>Taille des morceaux, agrandie avec un avertissement si la carte en donnerait trop.</summary>
    int ChunkSize(int w, int h)
    {
        int cp = Mathf.Max(1, chunkPixels);

        while (Mathf.CeilToInt((float)w / cp) * Mathf.CeilToInt((float)h / cp) > MaxChunks) cp++;

        if (cp != chunkPixels)
            Debug.LogWarning($"[CardDisintegration] {name} : trop de morceaux de {chunkPixels} px pour un sprite de {w} x {h}, taille portee a {cp} px.", this);

        return cp;
    }

    /// <summary>Lit les pixels du sprite, ou null (avec un avertissement) si la texture n'est pas lisible.</summary>
    Color[] ReadPixels(Texture2D tex, Rect tr, int w, int h)
    {
        if (!tex.isReadable)
        {
            Debug.LogWarning($"[CardDisintegration] {name} : cocher Read/Write dans l'import de \"{tex.name}\" pour ignorer les zones transparentes.", this);
            return null;
        }

        try { return tex.GetPixels(Mathf.RoundToInt(tr.x), Mathf.RoundToInt(tr.y), w, h); }
        catch (Exception e)
        { Debug.LogWarning($"[CardDisintegration] {name} : lecture des pixels impossible ({e.Message}), zones transparentes conservees.", this); return null; }
    }

    /// <summary>Vrai si tous les pixels de la zone sont transparents.</summary>
    static bool Transparent(Color[] px, int w, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            { if (px[y * w + x].a > 0.01f) return false; }

        return true;
    }

    /// <summary>Zone du sprite dans sa texture (repli sur rect si le sprite est empaquete serre dans un atlas).</summary>
    static Rect TextureRect(Sprite s)
    {
        try { return s.textureRect; }
        catch (Exception) { return s.rect; }
    }
}
