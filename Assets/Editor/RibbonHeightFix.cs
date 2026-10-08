using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>이미 만든 상시 오라 프리팹의 리본(TrailRenderer) 높이를 SphereArtBuilder 규칙(몸 키 1.3배 넘으면 0.95배)으로 고친다. 프리팹 전체를 다시 안 만든다. call RibbonHeightFix.Run</summary>
public static class RibbonHeightFix
{
    public static string Run()
    {
        const float body = 30f;
        var sb = new StringBuilder(); int fixedCount = 0, prefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<TrailRenderer>(true) == null) continue;
            bool any = false;
            foreach (TrailRenderer t in prefab.GetComponentsInChildren<TrailRenderer>(true)) if (t.transform.localPosition.y > 1.3f * body) { any = true; break; }
            if (!any) continue;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (TrailRenderer t in contents.GetComponentsInChildren<TrailRenderer>(true))
                {
                    Vector3 lp = t.transform.localPosition;
                    if (lp.y > 1.3f * body) { sb.AppendLine($"   {System.IO.Path.GetFileName(path)} / {t.name}: y {lp.y:F0} → {0.95f * body:F1}"); lp.y = 0.95f * body; t.transform.localPosition = lp; fixedCount++; }
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path); prefabs++;
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        return $"리본 {fixedCount}개 · 프리팹 {prefabs}개 보정\n{sb}";
    }
}
