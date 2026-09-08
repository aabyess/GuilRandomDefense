using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스킨이 어느 방향으로 서 있는지 **유니티 안에서 직접 재서** 표로 찍는다.
///
/// 왜 필요한가 — 2026-09-08에 「흔함_문필환이 누워 있다」를 고치려다 세 번 헛짚었다.
/// 원본 FBX의 정점 경계는 세로가 제일 길었고(서 있다), 임포트 설정도 정상 유닛과 같았고,
/// 프리팹 회전도 0이었다. **바깥에서 파일만 뜯어봐서는 왜 눕는지 알 수가 없다.**
///
/// 이 도구는 실제로 프리팹을 씬에 띄워서 렌더러 경계를 잰다. 그게 화면에 보이는 그것이다.
/// 추측 대신 이 표를 보고 ArtBinder.ModelAdjustments를 채운다.
/// </summary>
public static class SkinOrientationReport
{
    const string GeneratedFolder = "Assets/Prefabs/Generated";

    [MenuItem("Tools/아트/스킨 방향 점검")]
    public static void Report()
    {
        List<GameObject> prefabs = AssetDatabase
            .FindAssets("t:Prefab", new[] { GeneratedFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.Contains("Unit_"))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(g => g != null)
            .OrderBy(g => g.name)
            .ToList();

        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("스킨 방향 점검",
                $"{GeneratedFolder} 에서 유닛 프리팹을 찾지 못했습니다.\n\n" +
                "먼저 Tools > 아트 > 모델 배선을 돌려주세요.", "확인");
            return;
        }

        List<string> lines = new List<string>();
        List<string> suspects = new List<string>();

        foreach (GameObject prefab in prefabs)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Bounds? acc = null;
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (acc == null) acc = r.bounds;
                    else { Bounds a = acc.Value; a.Encapsulate(r.bounds); acc = a; }
                }

                if (acc == null)
                {
                    lines.Add($"{prefab.name,-30} 렌더러 없음");
                    continue;
                }

                Vector3 s = acc.Value.size;
                // 사람 모양은 서 있으면 세로가 제일 길다. 아니면 누웠거나 옆으로 섰다.
                bool standing = s.y >= s.x && s.y >= s.z;
                string verdict = standing ? "서 있음" : (s.x > s.z ? "🔴 옆으로 누움(X가 김)" : "🔴 앞뒤로 누움(Z가 김)");

                lines.Add($"{prefab.name,-30} {s.x,7:F2}{s.y,7:F2}{s.z,7:F2}   {verdict}");
                if (!standing) suspects.Add(prefab.name);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        string report =
            "가로(X) 세로(Y) 앞뒤(Z) — 세로가 제일 길어야 서 있는 것이다.\n\n" +
            string.Join("\n", lines);

        Debug.Log("[아트] 스킨 방향 점검\n" + report);

        string summary = suspects.Count == 0
            ? $"프리팹 {prefabs.Count}개 전부 서 있습니다."
            : $"프리팹 {prefabs.Count}개 중 {suspects.Count}개가 누워 있습니다:\n  " +
              string.Join("\n  ", suspects.Take(12)) +
              "\n\n자세한 수치는 콘솔을 보세요.";

        EditorUtility.DisplayDialog("스킨 방향 점검", summary, "확인");
    }
}
