using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>옛(25) 맵 씬에 S2 장식 모델을 얹는다. 맵 생성기는 안 건드리고, 「S2장식」 루트를 지우고 다시 세운다(여러 번 돌려도 같다).</summary>
public static class OldMapDecor
{
    const string RootName = "S2장식";

    [MenuItem("Tools/맵/옛 맵에 S2 장식 얹기")]
    public static void Apply()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject(RootName);
        string report = StructureDresser.PlaceOldMapLandmarks(root.transform);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Debug.Log(report);
    }

    [MenuItem("Tools/맵/옛 맵 S2 장식 걷기")]
    public static void Remove()
    {
        GameObject old = GameObject.Find(RootName);
        if (old == null) return;
        UnityEngine.SceneManagement.Scene scene = old.scene;
        Object.DestroyImmediate(old);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
