using System;
using UnityEngine;
using static GameManager;

/// <summary>Racine 1 : camera de jeu, pilote le suivi du joueur et les effets.</summary>
[Serializable]
public class CameraManager
{
    /// <summary>Racine 2 : suivi par coulissement.</summary>
    [field: SerializeField] public CameraFollow Follow { get; private set; }
    /// <summary>Racine 2 : effets (gel, tremblement, flash).</summary>
    [field: SerializeField] public CameraEffects Effects { get; private set; }

    /// <summary>Camera liee (null hors scene de jeu).</summary>
    public Camera Camera { get; private set; }

    /// <summary>Instancie et initialise les racines 2.</summary>
    internal void Awake() { Follow ??= new(); Effects ??= new(); Follow.Awake(); Effects.Awake(); }

    /// <summary>Lie la camera de la scene.</summary>
    internal void Bind() { Camera = GM.Linker.MainCamera; Follow.Bind(); Effects.Bind(); }

    /// <summary>Frame : minuteries des effets.</summary>
    internal void Tick() { if (Camera != null) Effects.Tick(); }

    /// <summary>Apres le joueur : position de la camera + tremblement.</summary>
    internal void LateTick()
    {
        if (Camera == null) return;
        Vector2 p = Follow.LateTick() + Effects.ShakeOffset;
        Camera.transform.position = new Vector3(p.x, p.y, Camera.transform.position.z);
    }

    /// <summary>Delie la camera (les effets en cours sont arretes).</summary>
    internal void Unbind() { Effects.Unbind(); Camera = null; }
}
