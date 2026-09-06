using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Assets/Art/Units/<유닛>/<유닛>.fbx 전부를 유닛 프리팹 키(캡슐 높이)에 맞춘다.
//
// 모델마다 원래 단위가 제각각이라(게임 추출본은 cm, 개인 제작은 m 등) 그냥 놓으면 개미만
// 하거나 건물만 하다. 임포트된 모델의 렌더러 바운드를 실제로 재서, 그 높이가 프리팹 캡슐
// 높이와 같아지도록 ModelImporter.globalScale을 다시 쓴다. 손으로 Scale Factor를 고치던
// 일을 없앤다.
//
// 기준은 UnitPrefab의 CapsuleCollider.height에서 읽는다 — 상수로 박지 않는다. 프리팹 크기가
// 바뀌면 이 메뉴를 다시 돌리면 된다.
public static class UnitSkinScaler
{
    const string UnitModelRoot = "Assets/Art/Units";
    const string UnitPrefabPath = "Assets/Prefabs/UnitPrefab.prefab";

    [MenuItem("Tools/맵/유닛 스킨 크기 맞추기")]
    public static void FitAll()
    {
        float target = ReadTargetHeight();
        if (target <= 0f)
        {
            Debug.LogError($"[스킨] {UnitPrefabPath}에서 CapsuleCollider 높이를 못 읽었습니다.");
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine($"기준 높이 {target:0.##} (UnitPrefab 캡슐)");
        int fitted = 0, skipped = 0;

        foreach (string dir in Directory.GetDirectories(UnitModelRoot))
        {
            string name = Path.GetFileName(dir);
            string fbx = $"{UnitModelRoot}/{name}/{name}.fbx";
            if (!File.Exists(fbx)) { skipped++; continue; }

            ModelImporter importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (importer == null || model == null) { report.AppendLine($"  ⚠️ {name}: 임포트 안 됨"); skipped++; continue; }

            float height = MeasureHeight(model);
            if (height <= 0f) { report.AppendLine($"  ⚠️ {name}: 렌더러 바운드 0"); skipped++; continue; }

            // 지금 보이는 높이는 이미 globalScale이 곱해진 값이다. 목표/현재 비율만큼 더 곱한다.
            float newScale = importer.globalScale * (target / height);
            if (Mathf.Abs(newScale - importer.globalScale) < 0.0001f)
            {
                report.AppendLine($"  ✅ {name}: 이미 맞음 (높이 {height:0.##})");
                continue;
            }

            importer.globalScale = newScale;
            importer.SaveAndReimport();
            report.AppendLine($"  🔧 {name}: 높이 {height:0.##} → {target:0.##}, scale {newScale:0.####}");
            fitted++;
        }

        report.AppendLine($"맞춤 {fitted} · 건너뜀 {skipped}");
        Debug.Log("[스킨 크기]\n" + report);
    }

    static float ReadTargetHeight()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath);
        if (prefab == null) return 0f;
        CapsuleCollider capsule = prefab.GetComponentInChildren<CapsuleCollider>(true);
        return capsule != null ? capsule.height : 0f;
    }

    // 바인드 포즈(T포즈) 기준 전체 렌더러 바운드의 세로 크기. 스킨드메시는 sharedMesh.bounds가
    // 로컬이라 월드로 못 쓰고, 프리팹 인스턴스를 잠깐 만들어 실제 bounds를 잰다.
    static float MeasureHeight(GameObject model)
    {
        GameObject temp = (GameObject)Object.Instantiate(model);
        temp.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.size.y;
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }
}
