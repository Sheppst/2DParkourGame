using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Donnees sauvegardees en JSON.</summary>
[Serializable]
public class SaveData
{
    /// <summary>Position du joueur.</summary>
    public Vector2 Position;
    /// <summary>Identifiants des pieces obtenues.</summary>
    public List<string> Coins = new();
    /// <summary>Vrai si le niveau a ete termine (bouton Continuer verrouille).</summary>
    public bool Finished;
}

/// <summary>Lecture/ecriture securisee du fichier de sauvegarde JSON.</summary>
public static class SaveSystem
{
    /// <summary>Chemin du fichier de sauvegarde.</summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>Vrai si un fichier de sauvegarde existe.</summary>
    public static bool Exists => File.Exists(FilePath);

    /// <summary>Lit la sauvegarde ; faux si absente ou corrompue.</summary>
    public static bool TryLoad(out SaveData data)
    {
        data = null;
        if (!Exists) return false;
        try { data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)); }
        catch (Exception e) { Debug.LogWarning($"[SaveSystem] Lecture impossible : {e.Message}"); }
        return data != null;
    }

    /// <summary>Ecrit la sauvegarde via un fichier temporaire (pas de fichier a moitie ecrit en cas de crash).</summary>
    public static bool Write(SaveData data)
    {
        if (data == null) return false;
        string tmp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (Exists) File.Delete(FilePath);
            File.Move(tmp, FilePath);
            return true;
        }
        catch (Exception e) { Debug.LogError($"[SaveSystem] Ecriture impossible : {e.Message}"); return false; }
    }

    /// <summary>Supprime la sauvegarde (nouvelle partie).</summary>
    public static void Delete()
    {
        try { if (Exists) File.Delete(FilePath); }
        catch (Exception e) { Debug.LogError($"[SaveSystem] Suppression impossible : {e.Message}"); }
    }
}
