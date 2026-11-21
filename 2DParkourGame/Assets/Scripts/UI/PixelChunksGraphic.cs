using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Un morceau de pixels detache d'une carte (positions dans le repere local de la carte, temps normalises sur la duree totale).</summary>
public struct PixelChunk
{
    /// <summary>Centre du morceau au repos.</summary>
    public Vector2 Center;
    /// <summary>Demi-taille du morceau au repos.</summary>
    public Vector2 Half;
    /// <summary>Coin bas gauche des UV dans la texture du sprite.</summary>
    public Vector2 UvMin;
    /// <summary>Coin haut droit des UV dans la texture du sprite.</summary>
    public Vector2 UvMax;
    /// <summary>Deplacement total du morceau a la fin de sa vie.</summary>
    public Vector2 Offset;
    /// <summary>Moment du depart (0 = debut de l'effet, 1 = fin).</summary>
    public float Start;
}

/// <summary>Graphique UI cree par CardDisintegration : dessine les morceaux de la carte, qui s'eloignent en retrecissant jusqu'a disparaitre.</summary>
public class PixelChunksGraphic : MaskableGraphic
{
    /// <summary>Texture du sprite de la carte.</summary>
    Texture texture;
    /// <summary>Morceaux a dessiner.</summary>
    readonly List<PixelChunk> chunks = new();
    /// <summary>Duree de vie d'un morceau, en proportion de la duree totale.</summary>
    float life = 1f;
    /// <summary>Avancement de l'effet (0 a 1).</summary>
    float time;

    /// <summary>Texture utilisee par le rendu (blanc par defaut si aucune).</summary>
    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

    /// <summary>Recoit la texture et les morceaux, et remet l'effet au debut.</summary>
    public void Setup(Texture tex, List<PixelChunk> source, float chunkLife)
    {
        texture = tex;
        chunks.Clear();
        if (source != null) chunks.AddRange(source);

        life = Mathf.Max(0.0001f, chunkLife);
        time = 0f;

        SetMaterialDirty();
        SetVerticesDirty();
    }

    /// <summary>Change l'avancement de l'effet (0 a 1) et redessine.</summary>
    public void SetTime(float t)
    {
        time = Mathf.Clamp01(t);

        SetVerticesDirty();
    }

    /// <summary>Construit un quad par morceau encore visible : depart en douceur, taille qui diminue jusqu'a zero.</summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color32 c = color;

        foreach (PixelChunk k in chunks)
        {
            float t = Mathf.Clamp01((time - k.Start) / life);

            if (t >= 1f) continue;

            float ease = 1f - (1f - t) * (1f - t);
            Quad(vh, k.Center + k.Offset * ease, k.Half * (1f - t), k.UvMin, k.UvMax, c);
        }
    }

    /// <summary>Ajoute un quad au maillage.</summary>
    static void Quad(VertexHelper vh, Vector2 p, Vector2 h, Vector2 uv0, Vector2 uv1, Color32 c)
    {
        int i = vh.currentVertCount;

        vh.AddVert(new Vector3(p.x - h.x, p.y - h.y), c, new Vector4(uv0.x, uv0.y));
        vh.AddVert(new Vector3(p.x - h.x, p.y + h.y), c, new Vector4(uv0.x, uv1.y));
        vh.AddVert(new Vector3(p.x + h.x, p.y + h.y), c, new Vector4(uv1.x, uv1.y));
        vh.AddVert(new Vector3(p.x + h.x, p.y - h.y), c, new Vector4(uv1.x, uv0.y));

        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
