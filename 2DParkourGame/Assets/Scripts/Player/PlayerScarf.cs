using System;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Type d'un attribut (Boucle OCR, deckbuilding de fin de niveau). Chaque type a une couleur.</summary>
public enum AttributeType
{
    /// <summary>Etat d'esprit du joueur.</summary>
    Rouge,
    /// <summary>Rapport avec l'environnement.</summary>
    Vert,
    /// <summary>L'offre du demon : assombrit l'echarpe.</summary>
    Noir,
    /// <summary>La benediction de l'ange : eclaircit l'echarpe.</summary>
    Blanc,
    /// <summary>Competence energetique.</summary>
    Bleu,
    /// <summary>Type encore a definir.</summary>
    Violet
}

/// <summary>Racine 2 : couleur de l'echarpe calculee a partir des attributs du joueur.
/// Les trois couleurs les plus presentes (hors blanc et noir) se melangent en proportion ; le blanc eclaircit, le noir assombrit.</summary>
[Serializable]
public class PlayerScarf
{
    /// <summary>Couleur de chaque type, dans l'ordre de l'enum AttributeType.</summary>
    [SerializeField] Color[] typeColors =
    {
        new(0.85f, 0.15f, 0.15f), new(0.2f, 0.7f, 0.25f), Color.black, Color.white, new(0.2f, 0.45f, 0.95f), new(0.6f, 0.25f, 0.85f)
    };
    /// <summary>Nombre maximal de couleurs dominantes melangees.</summary>
    [SerializeField, Range(1, 5)] int maxDominant = 3;
    /// <summary>Force de l'eclaircissement par le blanc (1 = proportion exacte des attributs blancs).</summary>
    [SerializeField, Range(0f, 1f)] float whiteStrength = 1f;
    /// <summary>Force de l'assombrissement par le noir (1 = proportion exacte des attributs noirs).</summary>
    [SerializeField, Range(0f, 1f)] float blackStrength = 1f;
    /// <summary>Couleur sans aucun attribut (magenta, aspect "texture manquante").</summary>
    [SerializeField] Color emptyColor = new(1f, 0f, 1f);
    /// <summary>Attributs du joueur, un element par attribut (modifiable dans l'Inspector pour tester le melange).</summary>
    [field: SerializeField] public List<AttributeType> Attributes { get; private set; } = new();

    /// <summary>Couleur actuelle de l'echarpe.</summary>
    public Color Color { get; private set; }

    /// <summary>Nombre d'attributs par type (tampon reutilise).</summary>
    readonly int[] counts = new int[Enum.GetValues(typeof(AttributeType)).Length];
    /// <summary>Types colores tries par nombre decroissant (tampon reutilise).</summary>
    readonly List<int> ranked = new();

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => Attributes ??= new();

    /// <summary>Liaison a la scene : applique la couleur.</summary>
    internal void Bind() => Tick();

    /// <summary>Frame : recalcule et applique la couleur (la liste peut changer dans l'Inspector).</summary>
    internal void Tick()
    {
        Color = Mix();

        SpriteRenderer r = GM.Linker != null ? GM.Linker.ScarfSprite : null;

        if (r != null && r.color != Color) r.color = Color;
    }

    /// <summary>Calcule la couleur : melange en proportion des dominantes, puis eclaircissement et assombrissement.</summary>
    public Color Mix()
    {
        int total = Count();

        if (total == 0) return emptyColor;

        int white = counts[(int)AttributeType.Blanc], black = counts[(int)AttributeType.Noir];
        Color c = Dominant(out int used) ? Blend(used) : white > 0 ? Color.white : Color.black;

        c = Color.Lerp(c, Color.white, whiteStrength * white / total);
        c = Color.Lerp(c, Color.black, blackStrength * black / total);
        c.a = 1f;

        return c;
    }

    /// <summary>Compte les attributs par type et retourne le total (types inconnus ignores avec un avertissement).</summary>
    int Count()
    {
        Array.Clear(counts, 0, counts.Length);
        int total = 0;

        foreach (AttributeType a in Attributes)
        {
            if ((int)a < 0 || (int)a >= counts.Length)
            { Debug.LogWarning($"[PlayerScarf] Type d'attribut inconnu : {(int)a}."); continue; }

            counts[(int)a]++; total++;
        }

        return total;
    }

    /// <summary>Range les types colores presents par nombre decroissant (egalite : ordre de l'enum). Retourne vrai s'il y en a.</summary>
    bool Dominant(out int used)
    {
        ranked.Clear();

        for (int t = 0; t < counts.Length; t++)
        {
            if (counts[t] == 0 || t == (int)AttributeType.Blanc || t == (int)AttributeType.Noir) continue;

            int k = ranked.Count;

            while (k > 0 && counts[ranked[k - 1]] < counts[t]) k--;

            ranked.Insert(k, t);
        }

        used = Mathf.Min(ranked.Count, maxDominant);

        return used > 0;
    }

    /// <summary>Moyenne des couleurs dominantes ponderee par leur nombre d'attributs.</summary>
    Color Blend(int used)
    {
        Color sum = Color.clear;
        int n = 0;

        for (int k = 0; k < used; k++)
        { int t = ranked[k]; sum += TypeColor(t) * counts[t]; n += counts[t]; }

        return sum / n;
    }

    /// <summary>Couleur d'un type (gris avec un avertissement si le tableau des couleurs est incomplet).</summary>
    Color TypeColor(int t)
    {
        if (typeColors != null && t < typeColors.Length) return typeColors[t];

        Debug.LogWarning($"[PlayerScarf] Pas de couleur pour le type {(AttributeType)t}.");

        return Color.gray;
    }
}
