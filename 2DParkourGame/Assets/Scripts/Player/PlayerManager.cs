using System;
using UnityEngine;
using static GameManager;

/// <summary>Racine 1 : gestionnaire du joueur, pilote ses racines 2 (controleur, animateur, mecanismes).</summary>
[Serializable]
public class PlayerManager
{
    /// <summary>Racine 2 : deplacement du joueur.</summary>
    [field: SerializeField] public PlayerController Controller { get; private set; }
    /// <summary>Racine 2 : animations par scale/rotation.</summary>
    [field: SerializeField] public PlayerAnimator Animator { get; private set; }
    /// <summary>Racine 2 : reactions avec l'environnement.</summary>
    [field: SerializeField] public PlayerMechanism Mechanism { get; private set; }

    /// <summary>Transform racine du joueur.</summary>
    public Transform Root { get; private set; }
    /// <summary>Rigidbody2D du joueur.</summary>
    public Rigidbody2D Body { get; private set; }
    /// <summary>Collider physique du joueur.</summary>
    public BoxCollider2D Collider { get; private set; }
    /// <summary>Pivot visuel anime.</summary>
    public Transform Visual { get; private set; }
    /// <summary>Vrai si le joueur est lie a une scene.</summary>
    public bool IsBound => Root != null;
    /// <summary>Position du joueur (pieds/pivot).</summary>
    public Vector2 Position => IsBound ? (Vector2)Root.position : Vector2.zero;
    /// <summary>Point le plus haut du collider (sommet de la tete).</summary>
    public Vector2 Head => IsBound ? new(Collider.bounds.center.x, Collider.bounds.max.y) : Vector2.zero;

    /// <summary>Instancie et initialise les racines 2.</summary>
    internal void Awake()
    {
        Controller ??= new(); Animator ??= new(); Mechanism ??= new();
        Controller.Awake(); Animator.Awake(); Mechanism.Awake();
    }

    /// <summary>Recupere les objets du joueur depuis le SceneLinker et lie les racines 2.</summary>
    internal void Bind()
    {
        SceneLinker l = GM.Linker;
        Root = l.PlayerRoot; Body = l.PlayerBody; Collider = l.PlayerCollider; Visual = l.PlayerVisual;
        Controller.Bind(l.PlayerGravity); Animator.Bind(); Mechanism.Bind();
    }

    /// <summary>Physique : deplacement puis detection de l'environnement.</summary>
    internal void FixedTick()
    {
        if (!IsBound) return;
        Controller.FixedTick(); Mechanism.FixedTick();
    }

    /// <summary>Frame : entrees, mecanismes, animation, puis purge des evenements consommes.</summary>
    internal void Tick()
    {
        if (!IsBound) return;
        Controller.Tick(); Mechanism.Tick(); Animator.Tick();
        Controller.ClearEvents(); Mechanism.ClearEvents();
    }

    /// <summary>Delie le joueur de la scene.</summary>
    internal void Unbind() { Root = null; Body = null; Collider = null; Visual = null; }

    /// <summary>Replace le joueur a une position et remet tous ses etats a zero.</summary>
    public void ResetTo(Vector2 position)
    {
        if (!IsBound) return;
        Body.position = position; Root.position = position; Body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        Controller.ResetState(); Mechanism.ResetState(); Animator.ResetState();
    }
}
