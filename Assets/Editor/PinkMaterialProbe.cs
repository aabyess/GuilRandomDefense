using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 10-14 「형광 분홍」 전수 점검: 유닛 프리팹(Prefabs/Generated)의 렌더러 재질이 비었거나(null) 셰이더가 없거나 오류 셰이더인 곳을 적는다.
/// 쓰는 법: Unity -batchmode -quit -projectPath . -executeMethod PinkMaterialProbe.Run  → Logs/pink_materials.txt, 문제 있으면 종료 코드 1.
/// </summary>
public static class PinkMaterialProbe
{
    /// <summary>재질 슬롯이 빈 프리팹의 원본 FBX를 강제 재임포트한다 — FBX가 외부 .mat보다 먼저 임포트되면 재질 매핑이 null로 굳는다(10-14 조로). 그 뒤 Run으로 다시 점검.</summary>
    public static void FixAndRun()
    {
        var fbx = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Generated" }))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m == null)
                    {
                        GameObject src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r.gameObject);
                        string sp = src != null ? AssetDatabase.GetAssetPath(src) : null;
                        if (!string.IsNullOrEmpty(sp) && sp.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) fbx.Add(sp);
                    }
        }
        foreach (string f in fbx) { Debug.Log("[분홍 점검] 재임포트 " + f); AssetDatabase.ImportAsset(f, ImportAssetOptions.ForceUpdate); }
        AssetDatabase.Refresh();
        Run();
    }

    public static void Run()
    {
        var lines = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Generated" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    string why = m == null ? "재질 없음(null)"
                        : m.shader == null ? "셰이더 없음"
                        : m.shader.name.Contains("InternalErrorShader") || m.shader.name.StartsWith("Hidden/InternalError") ? "오류 셰이더"
                        : !m.shader.isSupported ? $"지원 안 되는 셰이더 {m.shader.name}"
                        : null;
                    if (why != null) lines.Add($"{Path.GetFileNameWithoutExtension(path)} / {r.name} [{i}] {why}" + (m != null ? $" ({AssetDatabase.GetAssetPath(m)})" : ""));
                }
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/pink_materials.txt", lines);
        Debug.Log($"[분홍 점검] 문제 렌더러 슬롯 {lines.Count}개 → Logs/pink_materials.txt");
        if (Application.isBatchMode) EditorApplication.Exit(lines.Count == 0 ? 0 : 1);
    }
}
