using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 섬 가장자리 돌 테두리(사장님 10-20 밤샘 「섬 가장자리 돌 테두리 — 선택 위습 부스 돌 질감으로 통일」). 장식만(콜라이더 없음) — 맵 재생성 없이 씬에 「섬돌테두리」 루트 하나를 얹는다.
/// 스위치: StructureDresser.IslandStoneBorderOn(false면 메뉴가 걷기만 한다). 메뉴: Tools/맵/섬 돌 테두리 얹기 · 걷기.
/// 대상: 스토리존 · 창고 4 · 뽑기섬(위쪽 선택 위습 구역은 이미 띠벽이 있어 그 아래 변·옆 변만). 재료는 선택 위습 구역과 같은 Wall_Strip·Corner_Pillar(blender 정식 모델).
/// </summary>
public static class IslandStoneBorder
{
    const string RootName = "섬돌테두리";
    const float Inset = 2f;

    [MenuItem("Tools/맵/섬 돌 테두리 얹기")]
    public static void Apply()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        if (!StructureDresser.IslandStoneBorderOn) { Debug.Log("섬 돌 테두리: 스위치 꺼짐 — 걷기만 했습니다."); Save(); return; }

        GameObject map = GameObject.Find("Map");
        GameObject root = new GameObject(RootName);
        if (map != null) root.transform.SetParent(map.transform, false);
        float u = StructureDresser.BorderUnit;
        int pieces = 0;

        foreach (MapLayout.Island island in MapLayout.Zones.Concat(MapLayout.Warehouses).Where(i => i.name == "StoryZone" || i.name.StartsWith("Warehouse")))
            pieces += Border(root.transform, island, u, 0f);

        MapLayout.Island gacha = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        if (!string.IsNullOrEmpty(gacha.name))
        {
            // 위쪽 선택 위습 구역(부스·띠벽)은 따로 둘러져 있다 — 그 아래 구역 아래벽 높이부터.
            GameObject below = GameObject.Find("위습칸_아래띠벽0");
            float skipTop = below != null ? (gacha.center.y + gacha.size.y * 0.5f) - below.transform.position.z : 0f;
            pieces += Border(root.transform, gacha, u, skipTop);
        }
        Save();
        Debug.Log($"섬 돌 테두리 {pieces}개 얹음");
    }

    [MenuItem("Tools/맵/섬 돌 테두리 걷기")]
    public static void Remove()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Save();
    }

    static void Save()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 섬 한 개의 테두리 — 윗변(skipTop이 0일 때만)·아랫변·좌우 변. skipTop은 위에서 그만큼 내려온 데서 좌우 벽을 시작한다(뽑기섬).
    static int Border(Transform parent, MapLayout.Island island, float u, float skipTop)
    {
        float left = island.center.x - island.size.x * 0.5f + Inset;
        float right = island.center.x + island.size.x * 0.5f - Inset;
        float top = island.center.y + island.size.y * 0.5f - Inset;
        float bottom = island.center.y - island.size.y * 0.5f + Inset;
        float y = MapLayout.IslandTop;
        string n = island.name;
        int count = 0;

        count += Row(parent, $"{n}_아래", left, right, bottom, u);
        if (skipTop <= 0f) count += Row(parent, $"{n}_위", left, right, top, u);
        float sideTop = top - skipTop;
        count += Column(parent, $"{n}_왼", left, bottom, sideTop, u);
        count += Column(parent, $"{n}_오른", right, bottom, sideTop, u);

        foreach (Vector2 c in new[] { new Vector2(left, bottom), new Vector2(right, bottom) })
        { StructureDresser.PlaceStretched(parent, "선택위습_모서리기둥", $"{n}_모서리기둥", new Vector3(c.x, y, c.y), 0f, Vector3.one * u); count++; }
        if (skipTop <= 0f)
            foreach (Vector2 c in new[] { new Vector2(left, top), new Vector2(right, top) })
            { StructureDresser.PlaceStretched(parent, "선택위습_모서리기둥", $"{n}_모서리기둥", new Vector3(c.x, y, c.y), 0f, Vector3.one * u); count++; }
        return count;
    }

    static int Row(Transform parent, string name, float x0, float x1, float z, float u)
    {
        float width = x1 - x0;
        int n = Mathf.Max(1, Mathf.RoundToInt(width / (2f * u)));
        float sx = width / n / 2f;
        for (int i = 0; i < n; i++)
            StructureDresser.PlaceStretched(parent, "선택위습_띠벽", $"{name}{i}", new Vector3(x0 + width / n * (i + 0.5f), MapLayout.IslandTop, z), 0f, new Vector3(sx, u, u));
        return n;
    }

    static int Column(Transform parent, string name, float x, float z0, float z1, float u)
    {
        float length = z1 - z0;
        if (length <= 0f) return 0;
        int n = Mathf.Max(1, Mathf.RoundToInt(length / (2f * u)));
        float sz = length / n / 2f;
        for (int i = 0; i < n; i++)
            StructureDresser.PlaceStretched(parent, "선택위습_띠벽", $"{name}{i}", new Vector3(x, MapLayout.IslandTop, z0 + length / n * (i + 0.5f)), 90f, new Vector3(sz, u, u));
        return n;
    }

    // ───────────────────────── 바닥 포인트(맵 폴리시 ②) ─────────────────────────
    const string AccentRootName = "바닥포인트";

    /// <summary>뽑기섬 등급 칸·특수 칸 포탈 밑에 얇은 돌 판, 조합판 네 모서리에 작은 돌 기둥. 과하지 않게(판 두께 0.03·기둥 0.55배). 스위치 StructureDresser.FloorAccentsOn.</summary>
    [MenuItem("Tools/맵/바닥 포인트 얹기")]
    public static void ApplyAccents()
    {
        GameObject old = GameObject.Find(AccentRootName);
        if (old != null) Object.DestroyImmediate(old);
        if (!StructureDresser.FloorAccentsOn) { Debug.Log("바닥 포인트: 스위치 꺼짐 — 걷기만 했습니다."); Save(); return; }

        GameObject map = GameObject.Find("Map");
        GameObject root = new GameObject(AccentRootName);
        if (map != null) root.transform.SetParent(map.transform, false);
        float u = StructureDresser.BorderUnit;
        int pads = 0, pillars = 0;

        MapLayout.Island gacha = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        Rect gachaRect = new Rect(gacha.center - gacha.size * 0.5f, gacha.size);
        foreach (Collider trigger in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!trigger.isTrigger) continue;
            GameObject go = trigger.gameObject;
            if (go.GetComponent<UnitPortal>() == null && go.GetComponent<ResourcePortal>() == null) continue;
            if (go.name.StartsWith("흔함선택_")) continue;   // 부스 바닥은 이미 정식 모델이 있다
            Vector3 p = go.transform.position;
            if (!gachaRect.Contains(new Vector2(p.x, p.z))) continue;
            float pad = Mathf.Max(go.transform.lossyScale.x, 20f) * 1.5f;
            // 선택위습_부스바닥 모델: 가로 2.0m · 깊이 1.7m · 두께 0.06m → 정사각 판 pad×pad, 두께 0.03.
            StructureDresser.PlaceStretched(root.transform, "선택위습_부스바닥", $"{go.name}_돌판", new Vector3(p.x, MapLayout.IslandTop, p.z), 0f,
                new Vector3(pad / 2f, 0.5f, pad / 1.7f));
            pads++;
        }

        MapLayout.Island combine = System.Array.Find(MapLayout.Zones, z => z.name == "CombineTable");
        if (!string.IsNullOrEmpty(combine.name))
        {
            float l = combine.center.x - combine.size.x * 0.5f + 3f, r = combine.center.x + combine.size.x * 0.5f - 3f;
            float b = combine.center.y - combine.size.y * 0.5f + 3f, t = combine.center.y + combine.size.y * 0.5f - 3f;
            foreach (Vector2 c in new[] { new Vector2(l, b), new Vector2(r, b), new Vector2(l, t), new Vector2(r, t) })
            {
                StructureDresser.PlaceStretched(root.transform, "선택위습_모서리기둥", "조합판_모서리기둥", new Vector3(c.x, MapLayout.IslandTop, c.y), 0f, Vector3.one * (u * 0.55f));
                pillars++;
            }
        }
        Save();
        Debug.Log($"바닥 포인트: 포탈 돌판 {pads} · 조합판 모서리 기둥 {pillars}");
    }

    [MenuItem("Tools/맵/바닥 포인트 걷기")]
    public static void RemoveAccents()
    {
        GameObject old = GameObject.Find(AccentRootName);
        if (old != null) Object.DestroyImmediate(old);
        Save();
    }
}
