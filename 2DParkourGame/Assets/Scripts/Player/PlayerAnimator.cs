using System;
using UnityEngine;
using static GameManager;

/// <summary>Pose d'animation obtenue uniquement par scale et rotation.</summary>
[Serializable]
public struct ScaleRotClip
{
    /// <summary>Duree (one-shot) ou periode (boucle) en secondes.</summary>
    [Min(0.01f)] public float Duration;
    /// <summary>Scale visee au maximum de l'animation.</summary>
    public Vector2 Scale;
    /// <summary>Rotation Z visee au maximum de l'animation (degres, orientee vers l'avant).</summary>
    public float Angle;

    /// <summary>Cree un clip.</summary>
    public ScaleRotClip(float duration, Vector2 scale, float angle) { Duration = duration; Scale = scale; Angle = angle; }

    /// <summary>Pose interpolee entre le neutre (w = 0) et le clip (w = 1).</summary>
    public readonly (Vector2 scale, float angle) Pose(float w) => (Vector2.LerpUnclamped(Vector2.one, Scale, w), Angle * w);
}

/// <summary>Animation image par image du corps et de l'echarpe (environ 3 a 4 transitions de 2 images).</summary>
[Serializable]
public struct SpriteClip
{
    /// <summary>Duree d'affichage d'une image (s).</summary>
    [Min(0.01f)] public float FrameTime;
    /// <summary>Images du corps.</summary>
    public Sprite[] Body;
    /// <summary>Images de l'echarpe, une par image du corps (l'echarpe de repos est gardee s'il en manque).</summary>
    public Sprite[] Scarf;

    /// <summary>Vrai si le clip a au moins une image.</summary>
    public readonly bool IsValid => Body != null && Body.Length > 0 && Body[0] != null;
    /// <summary>Duree totale du clip (s).</summary>
    public readonly float Length => IsValid ? Body.Length * FrameTime : 0f;
}

/// <summary>Racine 2 : anime le pivot visuel du joueur par scale/rotation selon ses etats, et change les images du corps pour certaines poses.</summary>
[Serializable]
public class PlayerAnimator
{
    /// <summary>Boucle : se deplacer.</summary>
    [SerializeField] ScaleRotClip move = new(0.35f, new(1f, 0.95f), 4f);
    /// <summary>Boucle : courir.</summary>
    [SerializeField] ScaleRotClip run = new(0.25f, new(1.05f, 0.9f), 8f);
    /// <summary>Boucle : avancer prudemment.</summary>
    [SerializeField] ScaleRotClip sneak = new(0.6f, new(1.05f, 0.8f), 2f);
    /// <summary>Maintien : tomber dans les airs.</summary>
    [SerializeField] ScaleRotClip fallAir = new(0.2f, new(0.85f, 1.15f), 0f);
    /// <summary>One-shot : sauter.</summary>
    [SerializeField] ScaleRotClip jump = new(0.2f, new(0.8f, 1.25f), 0f);
    /// <summary>One-shot : tomber sur le sol.</summary>
    [SerializeField] ScaleRotClip land = new(0.15f, new(1.2f, 0.8f), 0f);
    /// <summary>One-shot : s'ecraser.</summary>
    [SerializeField] ScaleRotClip crash = new(0.45f, new(1.5f, 0.45f), 0f);
    /// <summary>One-shot : interagir.</summary>
    [SerializeField] ScaleRotClip interact = new(0.2f, new(1.1f, 0.9f), 10f);
    /// <summary>One-shot maintenu : mourir.</summary>
    [SerializeField] ScaleRotClip die = new(0.4f, new(0f, 0f), 180f);
    /// <summary>Duree du retournement (scale X passe de 1 a -1).</summary>
    [SerializeField, Min(0.01f)] float turnDuration = 0.12f;
    /// <summary>Vitesse de fondu entre les poses de boucle.</summary>
    [SerializeField, Min(0f)] float blend = 15f;
    /// <summary>Images : saut sur place (la derniere image est gardee jusqu'a l'atterrissage).</summary>
    [SerializeField] SpriteClip jumpInPlace = new() { FrameTime = 0.07f };
    /// <summary>Images : empale sur une pique de mur (la derniere image est gardee jusqu'a la reapparition).</summary>
    [SerializeField] SpriteClip impale = new() { FrameTime = 0.12f };

    /// <summary>One-shot en cours (null si aucun).</summary>
    ScaleRotClip? shot;
    /// <summary>Vrai si le one-shot reste sur sa pose finale (mort).</summary>
    bool shotHold;
    /// <summary>Temps ecoule du one-shot.</summary>
    float shotTime;
    /// <summary>Temps ecoule des boucles.</summary>
    float loopTime;
    /// <summary>Scale X liee a la direction (-1 a 1, anime lors du retournement).</summary>
    float facing = 1f;
    /// <summary>Pose de boucle lissee courante.</summary>
    Vector2 curScale = Vector2.one;
    /// <summary>Angle de boucle lisse courant.</summary>
    float curAngle;
    /// <summary>Clip d'images en cours (null : images de repos).</summary>
    SpriteClip? frames;
    /// <summary>Temps ecoule du clip d'images.</summary>
    float frameTime;
    /// <summary>Image de repos du corps (celle du prefab).</summary>
    Sprite idleBody;
    /// <summary>Image de repos de l'echarpe (celle du prefab).</summary>
    Sprite idleScarf;

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => ResetState();

    /// <summary>Liaison a la scene.</summary>
    internal void Bind()
    {
        SceneLinker l = GM.Linker;
        idleBody = l.PlayerSprite != null ? l.PlayerSprite.sprite : null;
        idleScarf = l.ScarfSprite != null ? l.ScarfSprite.sprite : null;

        if (l.PlayerSprite == null) Debug.LogWarning("[PlayerAnimator] PlayerSprite non assigne dans le SceneLinker : animations image par image desactivees.", l);

        ResetState();
    }

    /// <summary>Frame : choisit et applique la pose.</summary>
    internal void Tick()
    {
        PlayerController c = GM.Player.Controller;
        PlayerMechanism m = GM.Player.Mechanism;
        float dt = Time.deltaTime;

        ReadEvents(c, m, GM.Player.Death);

        int dir = GM.Player.Death.ImpaleSide != 0 ? GM.Player.Death.ImpaleSide : c.Facing;
        facing = Mathf.MoveTowards(facing, dir, 2f / turnDuration * dt);
        loopTime += dt;

        TickFrames(c, dt);

        (Vector2 s, float a) = LoopPose(c);
        float k = 1f - Mathf.Exp(-blend * dt);
        curScale = Vector2.Lerp(curScale, s, k); curAngle = Mathf.Lerp(curAngle, a, k);

        (s, a) = shot.HasValue ? ShotPose(dt) : (curScale, curAngle);

        Transform v = GM.Player.Visual;
        v.localScale = new Vector3(s.x * facing, s.y, 1f);
        v.localRotation = Quaternion.Euler(0f, 0f, -a * dir);
    }

    /// <summary>Lance les one-shots selon les evenements (priorite : mort, ecrasement, atterrissage, saut, interaction).</summary>
    void ReadEvents(PlayerController c, PlayerMechanism m, PlayerDeath d)
    {
        if (shotHold) return;

        if (d.JustDied && d.ImpaleSide != 0 && impale.IsValid)
        { shot = null; shotHold = true; PlayFrames(impale); }
        else if (d.JustDied) Play(die, true);
        else if (m.JustCrashed) Play(crash);
        else if (m.JustLanded) Play(land);
        else if (c.JustJumped)
        { Play(jump); if (!c.IsMoving && jumpInPlace.IsValid) PlayFrames(jumpInPlace); }
        else if (m.JustInteracted) Play(interact);
    }

    /// <summary>Demarre un clip d'images.</summary>
    void PlayFrames(SpriteClip clip) { frames = clip; frameTime = 0f; }

    /// <summary>Avance le clip d'images : derniere image gardee en l'air ou mort, retour au repos a l'atterrissage.</summary>
    void TickFrames(PlayerController c, float dt)
    {
        if (!frames.HasValue) return;

        SpriteClip clip = frames.Value;
        frameTime += dt;

        if (!GM.Player.Death.IsDead && c.IsGrounded && !c.JustJumped && frameTime > clip.FrameTime)
        { StopFrames(); return; }

        int i = Mathf.Min(clip.Body.Length - 1, Mathf.FloorToInt(frameTime / clip.FrameTime));
        bool scarf = clip.Scarf != null && i < clip.Scarf.Length && clip.Scarf[i] != null;

        SetSprites(clip.Body[i] != null ? clip.Body[i] : idleBody, scarf ? clip.Scarf[i] : idleScarf);
    }

    /// <summary>Arrete le clip d'images et remet les images de repos.</summary>
    void StopFrames() { frames = null; frameTime = 0f; SetSprites(idleBody, idleScarf); }

    /// <summary>Applique les images du corps et de l'echarpe (renderers absents ignores).</summary>
    static void SetSprites(Sprite body, Sprite scarf)
    {
        SceneLinker l = GM.Linker;

        if (l == null) return;

        if (l.PlayerSprite != null && body != null && l.PlayerSprite.sprite != body) l.PlayerSprite.sprite = body;
        if (l.ScarfSprite != null && scarf != null && l.ScarfSprite.sprite != scarf) l.ScarfSprite.sprite = scarf;
    }

    /// <summary>Demarre un one-shot.</summary>
    void Play(ScaleRotClip clip, bool hold = false) { shot = clip; shotHold = hold; shotTime = 0f; }

    /// <summary>Pose de boucle selon l'etat : en l'air, prudent, course, marche, ou neutre.</summary>
    (Vector2, float) LoopPose(PlayerController c)
    {
        if (!c.IsGrounded) return c.Velocity.y < 0f ? fallAir.Pose(1f) : (Vector2.one, 0f);

        if (!c.IsMoving) return (Vector2.one, 0f);

        ScaleRotClip l = c.IsSneaking ? sneak : c.IsRunning ? run : move;
        float w = Mathf.Sin(loopTime * Mathf.PI * 2f / l.Duration);
        (Vector2 s, _) = l.Pose(Mathf.Abs(w));

        return (s, l.Angle * w);
    }

    /// <summary>Pose du one-shot : aller-retour en cloche, ou maintien final pour la mort.</summary>
    (Vector2, float) ShotPose(float dt)
    {
        ScaleRotClip clip = shot.Value;
        float t = Mathf.Clamp01((shotTime += dt) / clip.Duration);

        if (!shotHold && t >= 1f) shot = null;

        return clip.Pose(shotHold ? t : Mathf.Sin(t * Mathf.PI));
    }

    /// <summary>Remet la pose a neutre (respawn).</summary>
    internal void ResetState()
    {
        shot = null; shotHold = false; shotTime = loopTime = curAngle = 0f; facing = 1f; curScale = Vector2.one;

        if (frames.HasValue) StopFrames();

        if (GM != null && GM.Player.IsBound)
        { GM.Player.Visual.localScale = Vector3.one; GM.Player.Visual.localRotation = Quaternion.identity; }
    }
}
