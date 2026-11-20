using System;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

/// <summary>Mode de saut, pour comparer les deux comportements.</summary>
public enum JumpMode
{
    /// <summary>Hauteur toujours identique.</summary>
    Fixed,
    /// <summary>Hauteur reduite si la touche est relachee pendant la montee.</summary>
    Variable
}

/// <summary>Racine 2 : deplacement du joueur (marche, course, prudence, saut, gravite).</summary>
[Serializable]
public class PlayerController
{
    /// <summary>Vitesse de marche (unites/s).</summary>
    [SerializeField, Min(0f)] float walkSpeed = 4f;
    /// <summary>Vitesse de course (unites/s).</summary>
    [SerializeField, Min(0f)] float runSpeed = 7f;
    /// <summary>Vitesse en deplacement prudent (unites/s).</summary>
    [SerializeField, Min(0f)] float sneakSpeed = 1.5f;
    /// <summary>Gravite appliquee via le ConstantForce2D (unites/s2).</summary>
    [SerializeField, Min(0f)] float gravity = 30f;
    /// <summary>Vitesse verticale donnee au saut.</summary>
    [SerializeField, Min(0f)] float jumpVelocity = 12f;
    /// <summary>Mode de saut (fixe ou variable).</summary>
    [SerializeField] JumpMode jumpMode = JumpMode.Variable;
    /// <summary>Saut variable : multiplicateur de la vitesse montante au relachement.</summary>
    [SerializeField, Range(0f, 1f)] float jumpCut = 0.5f;
    /// <summary>Coyote : distance horizontale apres le bord ou le saut reste possible (gizmo aux pieds).</summary>
    [SerializeField, Min(0f)] float coyoteDistance = 0.4f;
    /// <summary>Memoire de l'appui saut avant l'atterrissage (s).</summary>
    [SerializeField, Min(0f)] float jumpBuffer = 0.12f;
    /// <summary>Calques consideres comme sol.</summary>
    [SerializeField] LayerMask groundMask = ~0;
    /// <summary>Epaisseur de la detection du sol sous les pieds.</summary>
    [SerializeField, Min(0.001f)] float groundProbe = 0.05f;

    /// <summary>Vrai si le joueur touche le sol.</summary>
    public bool IsGrounded { get; private set; }
    /// <summary>Vrai si le joueur se deplace horizontalement.</summary>
    public bool IsMoving { get; private set; }
    /// <summary>Vrai si le joueur court.</summary>
    public bool IsRunning { get; private set; }
    /// <summary>Vrai si le joueur avance prudemment.</summary>
    public bool IsSneaking { get; private set; }
    /// <summary>Direction regardee (1 droite, -1 gauche).</summary>
    public int Facing { get; private set; } = 1;
    /// <summary>Vitesse courante du joueur.</summary>
    public Vector2 Velocity { get; private set; }
    /// <summary>Vitesse de chute juste avant le dernier atterrissage (positive).</summary>
    public float LandingSpeed { get; private set; }
    /// <summary>Bloque les commandes (mort, fin de niveau).</summary>
    public bool Locked { get; set; }
    /// <summary>Evenement : saut effectue.</summary>
    public bool JustJumped { get; private set; }
    /// <summary>Evenement : atterrissage.</summary>
    public bool JustLanded { get; private set; }
    /// <summary>Evenement : changement de direction.</summary>
    public bool JustTurned { get; private set; }

    /// <summary>Temps restant de la memoire de saut.</summary>
    float bufferTimer;
    /// <summary>Position X du dernier contact au sol (coyote).</summary>
    float lastGroundX;
    /// <summary>Vrai si un saut a eu lieu depuis le dernier contact au sol.</summary>
    bool jumpedSinceGround;
    /// <summary>Vrai si la coupe du saut variable a deja ete appliquee.</summary>
    bool cutDone;
    /// <summary>Vitesse verticale au dernier pas physique en l'air.</summary>
    float lastAirVy;
    /// <summary>Filtre de detection du sol.</summary>
    ContactFilter2D filter;
    /// <summary>Tampon de resultats de detection (evite les allocations).</summary>
    readonly List<Collider2D> hits = new();

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => ResetState();

    /// <summary>Configure la gravite par ConstantForce2D et le Rigidbody2D.</summary>
    internal void Bind(ConstantForce2D gravityForce)
    {
        Rigidbody2D b = GM.Player.Body;
        b.gravityScale = 0f; b.freezeRotation = true;
        gravityForce.force = new Vector2(0f, -gravity * b.mass);
        filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundMask };

        ResetState();
    }

    /// <summary>Frame : memorise l'appui saut.</summary>
    internal void Tick()
    {
        bufferTimer = GM.Inputs.JumpPressed ? jumpBuffer : Mathf.Max(0f, bufferTimer - Time.deltaTime);
    }

    /// <summary>Physique : sol, deplacement horizontal, prudence, saut.</summary>
    internal void FixedTick()
    {
        Rigidbody2D b = GM.Player.Body;
        InputManager i = GM.Inputs;
        Vector2 v = b.linearVelocity;
        bool wasGrounded = IsGrounded;
        IsGrounded = Probe(Vector2.zero);

        if (IsGrounded && !wasGrounded)
        { JustLanded = true; LandingSpeed = Mathf.Max(0f, -lastAirVy); }

        if (IsGrounded)
        { lastGroundX = b.position.x; jumpedSinceGround = false; }
        else lastAirVy = v.y;

        float input = Locked ? 0f : Mathf.Clamp(i.MoveX, -1f, 1f);
        IsSneaking = !Locked && i.SneakHeld;
        IsRunning = !Locked && !IsSneaking && i.RunHeld && input != 0f;
        v.x = input * (IsSneaking ? sneakSpeed : IsRunning ? runSpeed : walkSpeed);

        if (IsSneaking && IsGrounded && v.x != 0f && !Probe(new Vector2(v.x * Time.fixedDeltaTime, 0f))) v.x = 0f;

        IsMoving = v.x != 0f;
        int dir = input > 0f ? 1 : input < 0f ? -1 : Facing;

        if (dir != Facing)
        { Facing = dir; JustTurned = true; }

        if (!Locked && bufferTimer > 0f && CanJump(b.position.x, v.y))
        { v.y = jumpVelocity; Jumped(); }

        if (jumpMode == JumpMode.Variable && jumpedSinceGround && !cutDone && v.y > 0f && !i.JumpHeld)
        { v.y *= jumpCut; cutDone = true; }

        b.linearVelocity = Velocity = v;
    }

    /// <summary>Vrai si le joueur peut sauter : au sol, ou dans la distance coyote apres avoir quitte un bord sans sauter.</summary>
    bool CanJump(float x, float vy) => IsGrounded || (!jumpedSinceGround && vy <= 0f && Mathf.Abs(x - lastGroundX) <= coyoteDistance);

    /// <summary>Enregistre un saut effectue.</summary>
    void Jumped() { bufferTimer = 0f; jumpedSinceGround = true; cutDone = false; IsGrounded = false; JustJumped = true; }

    /// <summary>Detecte du sol sous les pieds, decale de offset (sert aussi au bord en mode prudent).</summary>
    bool Probe(Vector2 offset)
    {
        Bounds c = GM.Player.Collider.bounds;
        Vector2 center = new Vector2(c.center.x, c.min.y - groundProbe * 0.5f) + offset;
        Vector2 size = new(Mathf.Max(0.01f, c.size.x - 0.02f), groundProbe);

        int n = Physics2D.OverlapBox(center, size, 0f, filter, hits);

        for (int k = 0; k < n; k++) if (hits[k] != GM.Player.Collider) return true;

        return false;
    }

    /// <summary>Remet les evenements de la frame a zero.</summary>
    internal void ClearEvents() => JustJumped = JustLanded = JustTurned = false;

    /// <summary>Remet tous les etats a zero (respawn).</summary>
    internal void ResetState()
    {
        bufferTimer = lastAirVy = LandingSpeed = 0f; jumpedSinceGround = cutDone = Locked = false;
        IsGrounded = IsMoving = IsRunning = IsSneaking = false; Velocity = Vector2.zero; Facing = 1;
        lastGroundX = GM != null && GM.Player.IsBound ? GM.Player.Position.x : 0f;

        ClearEvents();
    }

    /// <summary>Dessine aux pieds du joueur la distance coyote (appele par le gizmo du SceneLinker).</summary>
    public void DrawGizmos(Bounds c)
    {
        Gizmos.color = Color.cyan;
        Vector3 l = new(c.min.x - coyoteDistance, c.min.y), r = new(c.max.x + coyoteDistance, c.min.y);

        Gizmos.DrawLine(l, r);
        Gizmos.DrawLine(l + Vector3.up * 0.1f, l - Vector3.up * 0.1f);
        Gizmos.DrawLine(r + Vector3.up * 0.1f, r - Vector3.up * 0.1f);
    }
}
