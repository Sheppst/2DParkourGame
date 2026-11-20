using UnityEditor;
using UnityEngine;

/// <summary>Inspector et poignee de scene de la bande de piques : on etire la bande en tirant la poignee, le stock ne change que par piques entieres.</summary>
[CustomEditor(typeof(SpikeStrip))]
public class SpikeStripEditor : Editor
{
    /// <summary>Inspector par defaut, avec le rappel de la longueur obtenue.</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var s = (SpikeStrip)target;
        EditorGUILayout.HelpBox($"{s.Count} pique(s), longueur {s.Length:0.##} unite(s). Tirez la poignee carree dans la scene pour etirer la bande.", MessageType.Info);
    }

    /// <summary>Poignee au bout de la bande, contrainte a l'axe X local.</summary>
    void OnSceneGUI()
    {
        var s = (SpikeStrip)target;

        if (s == null || PrefabUtility.IsPartOfPrefabAsset(s)) return;

        Transform t = s.transform;
        Vector3 end = t.TransformPoint(new Vector3(s.Length, 0f));
        float size = HandleUtility.GetHandleSize(end) * 0.08f;
        Handles.color = new Color(1f, 0.6f, 0f);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(end, t.right, size, Handles.CubeHandleCap, 0f);

        if (!EditorGUI.EndChangeCheck()) return;

        float length = t.InverseTransformPoint(moved).x;
        int count = Mathf.Max(1, Mathf.FloorToInt(length / SpikeStrip.SpikeWidth + 0.0001f));

        if (count == s.Count) return;

        Undo.RegisterFullObjectHierarchyUndo(s.gameObject, "Etirer la bande de piques");
        s.SetCount(count);
        EditorUtility.SetDirty(s);
    }
}
