using System;
using UnityEngine;
using static GameManager;

/// <summary>Racine 2 : coulissement de la camera quand le joueur touche les limites, et retour au centre apres immobilite.</summary>
[Serializable]
public class CameraFollow
{
    /// <summary>Decalage du point de ralliement au-dessus de la tete du joueur.</summary>
    [SerializeField] Vector2 headOffset = new(0f, 0.5f);
    /// <summary>Reduction des cotes du cadre pour former les limites (0 = bords de l'ecran, 1 = centre).</summary>
    [SerializeField, Range(0f, 1f)] float reduction = 0.6f;
    /// <summary>Quantite d'easing du coulissement (plus haut = plus rapide).</summary>
    [SerializeField, Min(0f)] float easing = 6f;
    /// <summary>Temps d'immobilite avant le retour au centre (s).</summary>
    [SerializeField, Min(0f)] float returnDelay = 1.5f;
    /// <summary>Quantite d'easing du retour au centre.</summary>
    [SerializeField, Min(0f)] float returnEasing = 2.5f;

    /// <summary>Centre de la camera sans tremblement.</summary>
    Vector2 center;
    /// <summary>Temps d'immobilite du joueur.</summary>
    float idle;

    /// <summary>Point de ralliement : legerement au-dessus de la tete du joueur.</summary>
    Vector2 Anchor => GM.Player.Head + headOffset;

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => idle = 0f;

    /// <summary>Place directement la camera sur le point de ralliement.</summary>
    internal void Bind() { center = Anchor; idle = 0f; }

    /// <summary>Calcule le nouveau centre de la camera.</summary>
    internal Vector2 LateTick()
    {
        Camera cam = GM.Cam.Camera;
        PlayerController c = GM.Player.Controller;
        float dt = Time.deltaTime;
        Vector2 a = Anchor;

        idle = c.IsMoving || !c.IsGrounded ? 0f : idle + dt;
        bool back = idle >= returnDelay;
        Vector2 target = back ? a : Slide(a, cam);
        center = Vector2.Lerp(center, target, 1f - Mathf.Exp(-(back ? returnEasing : easing) * dt));

        return center;
    }

    /// <summary>Cible de coulissement : la camera ne bouge que si le point de ralliement depasse une limite.</summary>
    Vector2 Slide(Vector2 a, Camera cam)
    {
        float h = cam.orthographic ? cam.orthographicSize : 5f;
        Vector2 lim = new Vector2(h * cam.aspect, h) * (1f - reduction);
        Vector2 d = a - center, t = center;

        if (Mathf.Abs(d.x) > lim.x) t.x = a.x - Mathf.Sign(d.x) * lim.x;
        if (Mathf.Abs(d.y) > lim.y) t.y = a.y - Mathf.Sign(d.y) * lim.y;

        return t;
    }
}
