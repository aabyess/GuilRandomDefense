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

    /// <summary>레인 상점 6채의 「_모양」만 다시 입힌다(StructureDresser.UseS2ShopModels에 따라 옛/새 모델). 상자·상점 컴포넌트·위치는 그대로.</summary>
    [MenuItem("Tools/맵/상점 모양 다시 입히기(StructureDresser.UseS2ShopModels 따라)")]
    public static void RedressShops()
    {
        string[] suffixes = { "도박소", "유닛강화소", "영원함강화소", "공격타입강화소", "도움소", "항해일지" };
        UnityEngine.SceneManagement.Scene scene = default;
        var lines = new System.Text.StringBuilder("상점 모양 다시 입히기");
        foreach (MapLayout.Island lane in MapLayout.Lanes)
            foreach (string suffix in suffixes)
            {
                GameObject body = GameObject.Find($"{lane.name}_{suffix}");
                if (body == null) { lines.Append($"\n  ⚠️ {lane.name}_{suffix} 없음"); continue; }
                scene = body.scene;
                GameObject old = GameObject.Find($"{lane.name}_{suffix}_모양");
                if (old != null) Object.DestroyImmediate(old);
                // 상자가 건물 크기로 늘어나 있으니 모양을 새로 잴 수 있게 상자는 건드리지 않고 DressLaneShop이 다시 맞춘다.
                StructureDresser.DressLaneShop(body);
                GameObject look = GameObject.Find($"{lane.name}_{suffix}_모양");
                Bounds b = default; bool any = false;
                if (look != null) foreach (Renderer r in look.GetComponentsInChildren<MeshRenderer>(true)) { if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; } }
                lines.Append($"\n  {lane.name}_{suffix}: {(look == null ? "⚠️ 못 세움" : $"{b.size.x:0}×{b.size.y:0}×{b.size.z:0}")}");
            }
        if (scene.IsValid()) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        Debug.Log(lines.ToString());
    }
}
