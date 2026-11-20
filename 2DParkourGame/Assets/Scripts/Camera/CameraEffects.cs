using System;
using UnityEngine;
using static GameManager;

/// <summary>Proprietes du gel du temps.</summary>
[Serializable]
public struct FreezeSettings
{
    /// <summary>Duree du gel en temps reel (0 = desactive).</summary>
    [Min(0f)] public float Duration;
}

/// <summary>Proprietes du tremblement.</summary>
[Serializable]
public struct ShakeSettings
{
    /// <summary>Duree du tremblement (0 = desactive).</summary>
    [Min(0f)] public float Duration;
    /// <summary>Amplitude maximale (unites monde).</summary>
    [Min(0f)] public float Amplitude;
    /// <summary>Frequence du bruit.</summary>
    [Min(0f)] public float Frequency;
}

/// <summary>Proprietes du flash.</summary>
[Serializable]
public struct FlashSettings
{
    /// <summary>Duree du fondu du flash (0 = desactive).</summary>
    [Min(0f)] public float Duration;
    /// <summary>Couleur et opacite de depart du flash.</summary>
    public Color Color;
}

/// <summary>Combinaison libre des trois effets (un effet de duree 0 est ignore).</summary>
[Serializable]
public struct CameraEffect
{
    /// <summary>Gel du temps.</summary>
    public FreezeSettings Freeze;
    /// <summary>Tremblement.</summary>
    public ShakeSettings Shake;
    /// <summary>Flash.</summary>
    public FlashSettings Flash;
}

/// <summary>Racine 2 : systeme auxiliaire d'effets de camera (gel du temps, tremblement, flash), en temps reel.</summary>
[Serializable]
public class CameraEffects
{
    /// <summary>Vrai pendant un gel du temps.</summary>
    public bool IsFrozen => freezeTimer > 0f;
    /// <summary>Decalage de tremblement a ajouter a la camera.</summary>
    public Vector2 ShakeOffset { get; private set; }

    /// <summary>Temps restant du gel.</summary>
    float freezeTimer;
    /// <summary>Temps restant du tremblement.</summary>
    float shakeTimer;
    /// <summary>Temps restant du flash.</summary>
    float flashTimer;
    /// <summary>Tremblement en cours.</summary>
    ShakeSettings shake;
    /// <summary>Flash en cours.</summary>
    FlashSettings flash;

    /// <summary>Initialisation independante de la scene.</summary>
    internal void Awake() => Stop();

    /// <summary>Liaison a la scene.</summary>
    internal void Bind() => Stop();

    /// <summary>Joue une combinaison d'effets.</summary>
    public void Play(CameraEffect e) { Play(e.Freeze); Play(e.Shake); Play(e.Flash); }

    /// <summary>Gele le temps (Time.timeScale a 0) pendant la duree.</summary>
    public void Play(FreezeSettings f)
    {
        if (f.Duration <= 0f) return;

        freezeTimer = Mathf.Max(freezeTimer, f.Duration); GM.RefreshTimeScale();
    }

    /// <summary>Fait trembler la camera.</summary>
    public void Play(ShakeSettings s)
    {
        if (s.Duration > 0f)
        { shake = s; shakeTimer = s.Duration; }
    }

    /// <summary>Lance un flash plein ecran (necessite FlashImage dans le SceneLinker).</summary>
    public void Play(FlashSettings f)
    {
        if (f.Duration <= 0f) return;

        if (GM.Linker == null || GM.Linker.FlashImage == null)
        { Debug.LogWarning("[CameraEffects] Flash sans FlashImage dans le SceneLinker."); return; }

        flash = f; flashTimer = f.Duration;
    }

    /// <summary>Frame : avance les minuteries en temps reel.</summary>
    internal void Tick()
    {
        float dt = Time.unscaledDeltaTime;

        if (freezeTimer > 0f && (freezeTimer -= dt) <= 0f)
        { freezeTimer = 0f; GM.RefreshTimeScale(); }

        ShakeOffset = Vector2.zero;

        if (shakeTimer > 0f && !GM.IsPaused)
        {
            shakeTimer -= dt;
            float t = Time.unscaledTime * shake.Frequency, k = shake.Amplitude * Mathf.Clamp01(shakeTimer / shake.Duration);
            ShakeOffset = new Vector2(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f) * (2f * k);
        }

        if (flashTimer > 0f)
        { flashTimer = Mathf.Max(0f, flashTimer - dt); SetFlash(flash.Color.a * flashTimer / flash.Duration); }
    }

    /// <summary>Arrete tous les effets et retablit le temps.</summary>
    internal void Unbind() => Stop();

    /// <summary>Remet tous les effets a zero.</summary>
    void Stop()
    {
        freezeTimer = shakeTimer = flashTimer = 0f; ShakeOffset = Vector2.zero;
        SetFlash(0f);

        if (GM != null) GM.RefreshTimeScale();
    }

    /// <summary>Applique l'opacite du flash a l'image.</summary>
    void SetFlash(float alpha)
    {
        var img = GM != null && GM.Linker != null ? GM.Linker.FlashImage : null;

        if (img == null) return;

        Color c = flash.Color; c.a = alpha; img.color = c;
        img.enabled = alpha > 0f;
    }
}
