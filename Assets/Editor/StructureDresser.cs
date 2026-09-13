using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Blender로 지은 구조물(Assets/Art/Structures)을 맵 생성기가 만든 자리표시(큐브·원기둥) 위에 입힌다(2026-09-13).
///
/// 원칙
///   · 게임 판정(콜라이더·트리거·컴포넌트)은 원래 오브젝트에 그대로 둔다. 모델은 보이는 것만 맡는다 —
///     정의문(MapGenerator.DressGate)과 같은 방식이다.
///   · 모델 파일이 없으면(유니티 임포트 전이거나 지워졌으면) 아무것도 안 바꾸고 자리표시를 남긴다 — 리포트에 적는다.
///   · 모델은 실제 치수(게임 단위)로 지어졌다. 원점은 바닥 가운데(부두 잔교는 갑판 윗면, 목선은 흘수선),
///     정면은 Blender −Y = 유니티 −Z(카메라 쪽). 받침_불멸만 +Y(=유니티 +Z)가 불 쪽이다.
///   · 빈 오브젝트 소켓(불_자리·연기_자리·빛_자리)은 EffectSockets가 파티클로 채운다.
/// 모델 목록·규칙의 출처: pending-drafts/art/blender/shops/MANIFEST.md.
/// </summary>
public static class StructureDresser
{
    const string Folder = "Assets/Art/Structures/";

    // BuildSea: 바다 상자 위치 −두께/2, 두께 1 → 윗면이 y=0이다. 목선 흘수선을 여기에 맞춘다.
    const float SeaTop = 0f;

    static readonly List<string> placed = new List<string>();
    static readonly SortedSet<string> missing = new SortedSet<string>();

    public static void BeginReport()
    {
        placed.Clear();
        missing.Clear();
    }

    public static string Report()
    {
        if (placed.Count == 0 && missing.Count == 0) return "";

        string report = placed.Count == 0
            ? "\n구조물 모델: 하나도 못 놓았습니다."
            : "\n구조물 모델: " + string.Join(", ", placed.GroupBy(p => p).Select(g => $"{g.Key}×{g.Count()}"));
        if (missing.Count > 0)
            report += $"\n  ⚠️ 못 읽은 구조물 모델 {missing.Count}종(그 자리는 자리표시 유지): {string.Join(", ", missing)}" +
                      " — 유니티가 임포트를 마친 뒤 맵을 다시 생성하세요.";
        return report;
    }

    // ───────────────────────── 공통 ─────────────────────────

    static GameObject Load(string model)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + model + ".fbx");
        if (asset == null) missing.Add(model);
        return asset;
    }

    /// <summary>
    /// 모델을 세운다. snapBottom이면 경계 최저점을 ground.y에 붙이고, 아니면 모델 원점을 ground에 둔다
    /// (잔교·목선처럼 원점이 바닥이 아닌 모델). yaw는 월드 Y축 회전(도). 없으면 null.
    /// </summary>
    static GameObject Place(Transform parent, string model, string name, Vector3 ground, float yaw,
                            float scale = 1f, bool snapBottom = true)
    {
        GameObject asset = Load(model);
        if (asset == null || !TryMeasure(asset, out Bounds bounds)) return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.name = name;
        // FBX 루트의 축 변환 회전·크기는 지우지 않고 그 위에 곱한다 — 대입하면 옆으로 눕는다(PlaceNatureProp과 같다).
        instance.transform.localScale = asset.transform.localScale * scale;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation;
        instance.transform.position = snapBottom ? ground + Vector3.up * (-bounds.min.y * scale) : ground;

        // 모델은 보이는 것만 맡는다 — 판정은 원래 오브젝트 몫이다.
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        placed.Add(model);
        return instance;
    }

    // 에셋·씬 오브젝트 모두 — 메시에 저장된 경계 8꼭짓점을 렌더러 변환으로 옮겨 합친다(MapGenerator.TryMeasureFigure와 같은 방식).
    static bool TryMeasure(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : (renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null);
            if (mesh == null) continue;

            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 world = toWorld.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(mesh.bounds.extents, sign));
                if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; }
                else bounds.Encapsulate(world);
            }
        }
        return any;
    }

    // 자리표시의 판정(콜라이더)은 살리고 그리기만 끈다. 렌더러를 지우지 않는 이유 — 선택 표시 등이 렌더러를 찾을 수 있다.
    static void HideRenderer(GameObject obj)
    {
        if (obj.TryGetComponent(out MeshRenderer renderer)) renderer.enabled = false;
    }

    // 월드 Y축 회전(도) — 모델의 −Z(정면)가 direction을 보게.
    static float YawFacing(Vector3 direction) => Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;

    static int Seed(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text) { hash ^= c; hash *= 16777619; }
            return (int)hash;
        }
    }

    // ───────────────────────── 레인 상점 ─────────────────────────

    // BuildLaneShopBody의 이름 꼬리 → 모델. 다른세계강화소는 전용 모델 전까지 강화소를 같이 쓴다.
    static readonly (string suffix, string model)[] LaneShopModels =
    {
        ("도박소", "상점_도박소"),
        ("유닛강화소", "상점_강화소"),
        ("다른세계강화소", "상점_강화소"),
        ("영원함강화소", "상점_영원강화소"),
        ("공격타입강화소", "상점_공격타입강화소"),
        ("도움소", "상점_도움소"),
        ("해적단상점", "상점_해적단퀘스트"),
        ("항해일지", "상점_항해일지"),
    };

    /// <summary>상점 상자 자리에 건물 모델을 세우고, 클릭 판정 상자를 건물 크기로 늘린다.</summary>
    public static void DressLaneShop(GameObject body)
    {
        string model = LaneShopModels.FirstOrDefault(entry => body.name.EndsWith("_" + entry.suffix)).model;
        if (model == null) return;

        Vector3 ground = new Vector3(body.transform.position.x, MapLayout.IslandTop, body.transform.position.z);
        GameObject building = Place(body.transform.parent, model, body.name + "_모양", ground, 0f);
        if (building == null) return;

        // 상자의 부모는 Map 루트(배율 1)라 월드 크기 = 로컬 크기. 건물 어디를 눌러도 선택되게 맞춘다.
        if (TryMeasure(building, out Bounds bounds))
        {
            body.transform.position = bounds.center;
            body.transform.localScale = bounds.size;
        }
        HideRenderer(body);
        EffectSockets.Attach(building);   // 강화소 화덕 불·굴뚝 연기
    }

    // ───────────────────────── 전시(불멸·초월) ─────────────────────────

    public static bool PlaceCampfire(Transform parent, Vector3 ground)
    {
        GameObject fire = Place(parent, "캠프파이어", "불멸전시_캠프파이어", ground, 0f);
        if (fire == null) return false;
        EffectSockets.Attach(fire);   // 불_자리·연기_자리 → 불꽃·연기·깜빡이는 불빛
        return true;
    }

    /// <summary>받침을 세우고, 인형을 올릴 높이(섬 윗면 기준 상승량)를 돌려준다. 모델이 없으면 0.</summary>
    public static float PlacePedestal(Transform parent, string model, string name, Vector3 ground, float yaw, float scale)
    {
        GameObject pedestal = Place(parent, model, name, ground, yaw, scale);
        return pedestal != null && TryMeasure(pedestal, out Bounds bounds) ? bounds.max.y - ground.y : 0f;
    }

    /// <summary>받침_불멸의 +Z(Blender +Y, 그을린 쪽)가 center를 보게 하는 회전.</summary>
    public static float YawTowardCenter(Vector3 from, Vector3 center)
        => Mathf.Atan2(center.x - from.x, center.z - from.z) * Mathf.Rad2Deg;

    // ───────────────────────── 스토리존 ─────────────────────────

    const float PlazaSurface = 1.0f;   // 광장 포석 윗면(Blender 치수) — 둘레돌 1.12는 가장자리뿐

    /// <summary>
    /// 스토리존 한가운데가 섬 윗면보다 얼마나 높은가 — 등장 지점·착지 지점을 광장 위로 올리는 데 쓴다.
    /// 착지 지점은 레인 포탈이 단상보다 **먼저** 지어질 때 정해지므로, 놓았는지가 아니라 모델이 있는지로 판단한다.
    /// 광장 가운데 45×45(스토리 적 건물 자리)도 포석이 윗면 높이로 깔려 있다(gen_story_platform.py).
    /// </summary>
    public static float StoryPlazaLift =>
        AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "스토리단상.fbx") != null ? PlazaSurface : 0f;

    /// <summary>
    /// 스토리 단상(지름 66 광장). 윗면을 걸을 수 있게 메시 콜라이더를 달아 NavMesh가 굽게 한다.
    /// 반환: 윗면 높이 상승량, 모델이 없으면 −1(호출부가 옛 원기둥을 짓는다).
    /// </summary>
    public static float PlaceStoryPlaza(Transform parent, Vector3 ground)
    {
        GameObject plaza = Place(parent, "스토리단상", "스토리_단상", ground, 0f);
        if (plaza == null) return -1f;

        foreach (MeshFilter filter in plaza.GetComponentsInChildren<MeshFilter>(true))
            if (!filter.TryGetComponent(out MeshCollider _))
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        return PlazaSurface;
    }

    // ───────────────────────── 포탈 ─────────────────────────

    // 막 재질 색(gen_portals.py: 호박·보라·청록)에 맞춘 빛_자리 빛깔.
    public static readonly Color StoryGlow = new Color(1f, 0.72f, 0.35f);
    public static readonly Color GachaGlow = new Color(0.72f, 0.45f, 1f);
    public static readonly Color ReturnGlow = new Color(0.35f, 0.95f, 0.85f);

    /// <summary>
    /// 트리거 원판(판정)은 그대로 두고 바닥 마법진을 깐다(사장님 09-13: 「문처럼 하지 말고 밑에 마법진 같은 걸로」).
    /// 마법진 지름을 원판 지름에 맞춘다. 원판 그림은 끈다 — 원판 윗면(섬 위 0.75)이 납작한 마법진을 덮기 때문이다.
    /// 원판 색으로 열림·닫힘을 보여 주던 InterludeGate는 마법진 렌더러를 대신 물들인다.
    /// light: 가운데 점광원을 달지.
    /// </summary>
    public static bool DressPortal(GameObject disc, string model, Color glow, bool light = true)
    {
        GameObject asset = Load(model);
        if (asset == null || !TryMeasure(asset, out Bounds bounds)) return false;

        float diameter = disc.transform.lossyScale.x;
        float scale = diameter / Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.z));
        Vector3 ground = new Vector3(disc.transform.position.x, MapLayout.IslandTop, disc.transform.position.z);
        GameObject circle = Place(disc.transform.parent, model, disc.name + "_마법진", ground, 0f, scale);
        if (circle == null) return false;

        HideRenderer(disc);
        WireSpin(circle);
        if (disc.TryGetComponent(out InterludeGate gate))
            gate.SetVisuals(circle.GetComponentsInChildren<Renderer>(true));
        EffectSockets.Attach(circle, glow, light);   // 빛_자리
        return true;
    }

    // Blender 약속: 돌릴 고리는 자식 이름에 `_회전_시계`·`_회전_반시계`. 실행 중 이름 비교(정규화 차이)를 피하려고 여기서 찾아 넣는다.
    static void WireSpin(GameObject circle)
    {
        List<Transform> clockwise = new List<Transform>();
        List<Transform> counterClockwise = new List<Transform>();
        foreach (Transform child in circle.GetComponentsInChildren<Transform>(true))
        {
            string name = child.name.Normalize(System.Text.NormalizationForm.FormC);
            if (name.Contains("_회전_반시계")) counterClockwise.Add(child);
            else if (name.Contains("_회전_시계")) clockwise.Add(child);
        }
        if (clockwise.Count + counterClockwise.Count == 0) return;
        circle.AddComponent<MagicCircleSpin>().SetRings(clockwise.ToArray(), counterClockwise.ToArray());
    }

    /// <summary>뽑기 섬 위의 위습 포탈(UnitPortal·ResourcePortal) 전부에 작은 뽑기 아치.</summary>
    public static int DressGachaPortals(Transform root)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        Rect area = new Rect(island.center - island.size * 0.5f, island.size);

        int dressed = 0;
        foreach (Collider trigger in root.GetComponentsInChildren<Collider>(true))
        {
            if (!trigger.isTrigger) continue;
            GameObject disc = trigger.gameObject;
            if (disc.GetComponent<UnitPortal>() == null && disc.GetComponent<ResourcePortal>() == null) continue;
            if (!area.Contains(new Vector2(disc.transform.position.x, disc.transform.position.z))) continue;

            // 포탈이 수십 개라 광원은 안 단다 — 빛 알갱이만.
            if (DressPortal(disc, "포탈_마법진_뽑기", GachaGlow, light: false)) dressed++;
        }
        return dressed;
    }

    // ───────────────────────── 창고 섬 ─────────────────────────

    static readonly string[] WarehouseModels = { "창고_회색함석", "창고_붉은함석", "창고_초록널", "창고_모래기와" };

    /// <summary>플레이어별 창고 섬의 북쪽 가장자리에 헛간 한 채. 유닛이 헛간 속으로 안 들어가게 보이지 않는 상자를 둔다.</summary>
    public static void PlaceWarehouseShed(Transform parent, MapLayout.Island island, int playerIndex)
    {
        string model = WarehouseModels[playerIndex % WarehouseModels.Length];
        GameObject asset = Load(model);
        if (asset == null || !TryMeasure(asset, out Bounds size)) return;

        Vector3 ground = new Vector3(island.center.x, MapLayout.IslandTop,
                                     island.center.y + island.size.y * 0.5f - size.size.z * 0.5f - 1.5f);
        GameObject shed = Place(parent, model, $"{island.name}_창고헛간", ground, 0f);
        if (shed == null || !TryMeasure(shed, out Bounds bounds)) return;

        GameObject blocker = new GameObject($"{island.name}_창고헛간_막음");
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = bounds.center;
        blocker.AddComponent<BoxCollider>().size = bounds.size;
    }

    // ───────────────────────── 펑크해저드 ─────────────────────────

    static readonly string[] IceModels = { "펑크_얼음가시_01", "펑크_얼음가시_02", "펑크_얼음가시_03" };
    static readonly string[] LavaModels = { "펑크_용암바위_01", "펑크_용암바위_02", "펑크_용암바위_03" };
    static readonly int[] ScatterPlan = { 2, 1, 0, 0, 1, 0, 2, 0 };   // 모델 번호 — 큰 것 둘, 중간 둘, 작은 것 넷

    /// <summary>
    /// 정의문(섬 가운데를 동서로 가로지름)을 사이에 두고 북쪽은 얼음, 남쪽은 용암 —
    /// 원작 펑크해저드(아카이누·아오키지 결투지, 문 퀘스트 dog_zone). 문 앞 길(가운데 x 띠)은 비워 둔다.
    /// </summary>
    public static void ScatterPunkHazard(Transform parent, MapLayout.Island island, float gateWidth, float gateThickness)
    {
        ScatterHalf(parent, island, IceModels, "얼음", north: true, gateWidth, gateThickness);
        ScatterHalf(parent, island, LavaModels, "용암", north: false, gateWidth, gateThickness);
    }

    static void ScatterHalf(Transform parent, MapLayout.Island island, string[] models, string label, bool north,
                            float gateWidth, float gateThickness)
    {
        System.Random rng = new System.Random(Seed(island.name + label));
        List<Vector3> spots = new List<Vector3>();   // (x, z, 반경)

        const float margin = 4f;
        float gateClear = gateThickness * 0.5f + 4f;
        float pathClear = gateWidth * 0.5f + 4f;
        float zMin = north ? island.center.y + gateClear : island.center.y - island.size.y * 0.5f + margin;
        float zMax = north ? island.center.y + island.size.y * 0.5f - margin : island.center.y - gateClear;
        float xMin = island.center.x - island.size.x * 0.5f + margin;
        float xMax = island.center.x + island.size.x * 0.5f - margin;

        foreach (int index in ScatterPlan)
        {
            string model = models[index];
            GameObject asset = Load(model);
            if (asset == null || !TryMeasure(asset, out Bounds bounds)) continue;
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            if (zMax - zMin < radius * 2f) continue;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float x = Mathf.Lerp(xMin + radius, xMax - radius, (float)rng.NextDouble());
                float z = Mathf.Lerp(zMin + radius, zMax - radius, (float)rng.NextDouble());
                if (Mathf.Abs(x - island.center.x) < pathClear + radius) continue;
                if (spots.Any(s => (s.x - x) * (s.x - x) + (s.y - z) * (s.y - z) < (s.z + radius + 1f) * (s.z + radius + 1f)))
                    continue;

                GameObject prop = Place(parent, model, $"펑크해저드_{label}_{spots.Count + 1:00}",
                    new Vector3(x, MapLayout.IslandTop, z), (float)rng.NextDouble() * 360f);
                if (prop == null) break;
                EffectSockets.Attach(prop);   // 용암바위_03 연기
                spots.Add(new Vector3(x, z, radius));
                break;
            }
        }
    }

    // ───────────────────────── 부두 ─────────────────────────

    // 바다와 맞닿은 섬 가장자리. 방향 = 잔교가 뻗는 쪽(바다), along = 그 변 위 위치(0~1).
    // 이웃 섬과 사이가 좁은 변(스토리존 남쪽—물범 섬, 뽑기섬 동쪽—조합표)은 피했다.
    static readonly (string island, Vector2 direction, float along, bool longPier)[] DockSites =
    {
        ("GachaIsland",  new Vector2(0f, -1f), 0.30f, true),
        ("CombineTable", new Vector2(0f, -1f), 0.72f, true),
        ("StoryZone",    new Vector2(-1f, 0f), 0.35f, false),
        ("Warehouse2",   new Vector2(1f, 0f),  0.50f, false),
        ("Warehouse4",   new Vector2(1f, 0f),  0.50f, false),
    };

    public static string PlaceDocks(Transform parent)
    {
        int piers = 0;
        for (int i = 0; i < DockSites.Length; i++)
        {
            var site = DockSites[i];
            MapLayout.Island island = MapLayout.Zones.Concat(MapLayout.Warehouses).FirstOrDefault(z => z.name == site.island);
            if (string.IsNullOrEmpty(island.name)) continue;

            Vector3 d = new Vector3(site.direction.x, 0f, site.direction.y);
            Vector3 perp = new Vector3(-d.z, 0f, d.x);
            float halfAlong = Mathf.Abs(d.x) * island.size.x * 0.5f + Mathf.Abs(d.z) * island.size.y * 0.5f;
            float halfPerp = Mathf.Abs(perp.x) * island.size.x * 0.5f + Mathf.Abs(perp.z) * island.size.y * 0.5f;
            Vector3 center = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);
            Vector3 root = center + d * halfAlong + perp * ((site.along - 0.5f) * 2f * halfPerp * 0.8f);
            float yaw = YawFacing(d);

            string pierModel = site.longPier ? "부두_잔교_긴" : "부두_잔교_짧은";
            float pierLength = site.longPier ? 24f : 14f;
            // 잔교 원점 = 섬 쪽 끝 갑판 윗면(z 0) → 섬 윗면에 그대로 둔다.
            if (Place(parent, pierModel, $"{site.island}_잔교", root, yaw, 1f, snapBottom: false) == null) continue;
            piers++;

            // 목선 — 잔교 옆 물 위(원점 = 흘수선).
            Vector3 boat = root + d * (pierLength * 0.55f) + perp * 5.5f;
            Place(parent, i % 2 == 0 ? "부두_목선_01" : "부두_목선_02", $"{site.island}_목선",
                  new Vector3(boat.x, SeaTop, boat.z), yaw, 1f, snapBottom: false);

            // 섬 가장자리 소품 — 게임 카메라에서 점처럼 작아 배율을 키운다(MANIFEST).
            foreach (float side in new[] { -1f, 1f })
                Place(parent, "부두_계선주", $"{site.island}_계선주", root - d * 2f + perp * (3.5f * side), yaw, 2.5f);
            Place(parent, "부두_부표더미", $"{site.island}_부표더미", root - d * 4f + perp * 7f, yaw, 2f);
        }
        return piers > 0 ? $"\n부두: 잔교 {piers}곳(목선·계선주·부표 포함)." : "";
    }
}
