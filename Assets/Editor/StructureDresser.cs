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
    static bool TryMeasure(GameObject root, out Bounds bounds, string skipNameContains = null)
    {
        bounds = default;
        bool any = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (skipNameContains != null && renderer.name.Contains(skipNameContains)) continue;   // 빛 겹(_add)은 몸집에서 뺀다
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

    // BuildLaneShopBody의 이름 꼬리 → 모델. 10-04 사장님 「너무 똑같이 생겨서 못생김」 → blender gen_shops_v2로 7채 모양을 다 갈았고
    // 유닛강화소(대장간)·다른세계강화소(보라 관문)가 각자 모델을 갖는다(옛 상점_강화소는 안 쓰인 채 남음).
    static readonly (string suffix, string model)[] LaneShopModels =
    {
        ("도박소", "상점_도박소"),
        ("유닛강화소", "상점_유닛강화소"),
        ("다른세계강화소", "상점_다른세계강화소"),
        ("영원함강화소", "상점_영원강화소"),
        ("공격타입강화소", "상점_공격타입강화소"),
        ("도움소", "상점_도움소"),
        ("해적단상점", "상점_해적단퀘스트"),
        ("항해일지", "상점_항해일지"),
    };

    /// <summary>
    /// 상점 모델 선택(옛/새 한 곳에서). true면 S2 매칭 모델(blender shop_for_oldmap), false면 옛 상점_*.fbx(지우지 않고 남겨 둠).
    /// 바꾼 뒤 메뉴 「Tools/맵/상점 모양 다시 입히기(S2 모델 켜기/끄기)」로 씬에 반영한다.
    /// </summary>
    public const bool UseS2ShopModels = true;
    // 도박소=금항아리 · 유닛강화소=대장간 · 영원함강화소=발전기 · 공격타입강화소(연구소)=마법학교 · 도움소=책 제단 · 항해일지=마법 금고
    static readonly (string suffix, string model)[] S2ShopModels =
    {
        ("도박소", "S2상점_도박소"),
        ("유닛강화소", "S2상점_대장간"),
        ("영원함강화소", "S2상점_발전기"),
        ("공격타입강화소", "S2상점_마법학교"),
        ("도움소", "S2상점_책제단"),
        ("항해일지", "S2상점_마법금고"),
    };
    // 10-20 정렬: 받침대 폭 = 슬롯 간격 × 0.95(전부 같은 폭·높이 돌 단), 건물 발자국 = 슬롯 간격 × 0.72, 키 제한 = 발자국 × 1.15.
    const float ShopPedestalWidthFraction = 0.95f;
    const float ShopFootprintFraction = 0.72f;
    const float ShopTallLimit = 1.15f;
    const float ShopPedestalHeight = 4f;
    /// <summary>모든 상점이 같은 방향(문이 남쪽 = 카메라 쪽)을 보게 하는 회전(도). 모델 정면이 +Z면 180.</summary>
    public static float ShopFacingYaw = 0f;
    static readonly string[] ShopSlotOrder = { "도박소", "유닛강화소", "영원함강화소", "도움소", "공격타입강화소", "항해일지" };

    /// <summary>상점 상자 이름(「Lane1_도박소」)에서 그 슬롯의 이상적인 중앙과 슬롯 간격을 구한다. MapGenerator.LaneShopSlotAt과 같은 식(폭 × 1.15 ÷ 칸 수).</summary>
    public static bool ShopSlotCenter(GameObject body, out Vector3 center, out float pitch)
    {
        center = default; pitch = 0f;
        MapLayout.Island lane = MapLayout.Lanes.FirstOrDefault(l => body.name.StartsWith(l.name + "_"));
        if (string.IsNullOrEmpty(lane.name)) return false;
        int slot = System.Array.FindIndex(ShopSlotOrder, s => body.name.EndsWith("_" + s));
        if (slot < 0) return false;
        MapLayout.Island strip = MapLayout.LaneShopStrip(lane);
        pitch = strip.size.x * 1.15f / ShopSlotOrder.Length;
        center = new Vector3(strip.center.x + (slot - (ShopSlotOrder.Length - 1) * 0.5f) * pitch, MapLayout.IslandTop, strip.center.y);
        return true;
    }

    /// <summary>새 모델 배율 = 옛 상점 발자국에 맞춘 배율과 높이에 맞춘 배율의 기하평균, 단 발자국 배율의 이 배수까지(옆 상점과 안 겹치게).</summary>
    const float S2ShopFootprintCap = 1.25f;

    /// <summary>레인 상점 건물 배율(10-14 사장님 「더 크게」) — 원작 비율 MapLayout.Scale 위에 곱한다.</summary>
    public const float LaneShopModelScale = 1.5f;
    /// <summary>클릭 상자 = 건물 경계 × 이 값(사장님 「클릭 범위 넓게」). 옆 상점 간격(≈110)보다 작게 유지.</summary>
    public const float LaneShopClickPad = 1.3f;
    /// <summary>클릭 상자 가로·깊이 상한(월드). 상점 슬롯 간격 ≈110보다 작게.</summary>
    public const float LaneShopClickMaxWidth = 100f;

    /// <summary>상점 상자 자리에 건물 모델을 세우고, 클릭 판정 상자를 건물 크기로 늘린다.</summary>
    public static void DressLaneShop(GameObject body)
    {
        string model = LaneShopModels.FirstOrDefault(entry => body.name.EndsWith("_" + entry.suffix)).model;
        if (model == null) return;
        float scaleBoost = 1f;
        bool aligned = false;
        float pedestalTop = 0f;
        float alignedScale = 0f;
        if (UseS2ShopModels)
        {
            string s2 = S2ShopModels.FirstOrDefault(entry => body.name.EndsWith("_" + entry.suffix)).model;
            GameObject oldAsset = Load(model), newAsset = s2 == null ? null : Load(s2);
            if (newAsset != null && oldAsset != null && TryMeasure(oldAsset, out Bounds ob) && TryMeasure(newAsset, out Bounds nb, "_add"))
            {
                model = s2;
                if (ShopSlotCenter(body, out Vector3 slot, out float pitch))
                {
                    // 10-20 사장님 「건물들 정렬 잘 배치해줘, 들쑥날쑥」 — 슬롯 중앙·한 줄 앞선·공통 받침대 위 바닥선·발자국 폭 통일.
                    aligned = true;
                    body.transform.position = new Vector3(slot.x, body.transform.position.y, slot.z);
                    GameObject oldPedestal = GameObject.Find(body.name + "_받침");
                    if (oldPedestal != null) Object.DestroyImmediate(oldPedestal);
                    float pedestalW = pitch * ShopPedestalWidthFraction;
                    GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pedestal.name = body.name + "_받침";
                    pedestal.transform.SetParent(body.transform.parent, false);
                    pedestal.transform.position = new Vector3(slot.x, MapLayout.IslandTop + ShopPedestalHeight * 0.5f, slot.z);
                    pedestal.transform.localScale = new Vector3(pedestalW, ShopPedestalHeight, pedestalW * 0.8f);
                    MapGenerator.PaintStone(pedestal, pedestal.transform.localScale.x, pedestal.transform.localScale.z);
                    Object.DestroyImmediate(pedestal.GetComponent<Collider>());
                    pedestalTop = ShopPedestalHeight;
                    float target = pitch * ShopFootprintFraction;
                    alignedScale = target / Mathf.Max(nb.size.x, nb.size.z);
                    if (nb.size.y * alignedScale > target * ShopTallLimit) alignedScale *= 0.8f;   // 키가 발자국의 1.15배를 넘는 건물(마법학교·책 제단)은 0.8배
                }
                else
                {
                    float footprint = Mathf.Max(ob.size.x, ob.size.z) / Mathf.Max(nb.size.x, nb.size.z);
                    float height = ob.size.y / nb.size.y;
                    scaleBoost = Mathf.Min(Mathf.Sqrt(footprint * height), footprint * S2ShopFootprintCap);
                }
            }
        }

        Vector3 ground = new Vector3(body.transform.position.x, MapLayout.IslandTop + pedestalTop, body.transform.position.z);
        // 원작 비율 5단계(PM 지시 2026-09-23) — 모델은 실제 치수로 지어졌고 이 호출이 Scale을
        // 안 넘기면 항상 1배(Blender 그대로)로 서서, 맵이 커진 만큼 건물이 상대적으로 쪼그라든다.
        float buildingScale = aligned ? alignedScale : MapLayout.Scale * LaneShopModelScale * scaleBoost;
        GameObject building = Place(body.transform.parent, model, body.name + "_모양", ground, aligned ? ShopFacingYaw : 0f, buildingScale);
        if (building == null) return;

        // 상자의 부모는 Map 루트(배율 1)라 월드 크기 = 로컬 크기. 건물 어디를 눌러도 선택되게 맞춘다.
        if (TryMeasure(building, out Bounds bounds, "_add"))   // 빛 겹(_add)은 경계에서 뺀다 — 책 제단 빛이 상자를 가로 302로 키워 옆 상점을 덮었다(10-19)
        {
            Vector3 box = bounds.size * LaneShopClickPad;   // 클릭 범위는 건물보다 넉넉하게
            box.x = Mathf.Min(box.x, LaneShopClickMaxWidth);   // 슬롯 간격(≈110) 이하 — 옆 상점 상자와 안 겹치게
            box.z = Mathf.Min(box.z, LaneShopClickMaxWidth);
            body.transform.position = bounds.center;
            body.transform.localScale = box;
        }
        HideRenderer(body);
        MakeAdditive(building, "_add");   // S2 모델의 빛 겹(원작 add 필터)은 가산으로 — 안 그러면 검은 덩어리
        EffectSockets.Attach(building);   // 강화소 화덕 불·굴뚝 연기
    }

    // ───────────────────────── 전시(불멸·초월) ─────────────────────────

    public static bool PlaceCampfire(Transform parent, Vector3 ground)
    {
        GameObject fire = Place(parent, "캠프파이어", "불멸전시_캠프파이어", ground, 0f, MapLayout.Scale);
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

    const float PlazaSurface = 1.0f;   // 광장 포석 윗면(Blender 치수, 배율 1일 때) — 둘레돌 1.12는 가장자리뿐

    /// <summary>
    /// 스토리존 한가운데가 섬 윗면보다 얼마나 높은가 — 등장 지점·착지 지점을 광장 위로 올리는 데 쓴다.
    /// 착지 지점은 레인 포탈이 단상보다 **먼저** 지어질 때 정해지므로, 놓았는지가 아니라 모델이 있는지로 판단한다.
    /// 광장 가운데 45×45(스토리 적 건물 자리)도 포석이 윗면 높이로 깔려 있다(gen_story_platform.py).
    /// ⚠️ PlaceStoryPlaza가 이제 모델을 MapLayout.Scale로 키워서 세우므로(원작 비율 5단계,
    /// 2026-09-23), 실제 윗면 높이도 그만큼 커진다 — 여기서 곱하지 않으면 착지 지점이
    /// 모델 표면 아래(땅속)에 박힌다.
    /// </summary>
    public static float StoryPlazaLift =>
        AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "스토리단상.fbx") != null
            ? PlazaSurface * MapLayout.Scale : 0f;

    /// <summary>
    /// 스토리 단상(지름 66 광장, 배율 1 기준). 윗면을 걸을 수 있게 메시 콜라이더를 달아 NavMesh가 굽게 한다.
    /// 반환: 윗면 높이 상승량, 모델이 없으면 −1(호출부가 옛 원기둥을 짓는다).
    /// </summary>
    public static float PlaceStoryPlaza(Transform parent, Vector3 ground)
    {
        GameObject plaza = Place(parent, "스토리단상", "스토리_단상", ground, 0f, MapLayout.Scale);
        if (plaza == null) return -1f;

        foreach (MeshFilter filter in plaza.GetComponentsInChildren<MeshFilter>(true))
            if (!filter.TryGetComponent(out MeshCollider _))
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        return PlazaSurface * MapLayout.Scale;
    }

    // ───────────────────────── 포탈 ─────────────────────────

    const float CircleLift = 0.06f;   // 마법진 원점을 섬 윗면에서 띄우는 높이(깊이 겹침 방지)

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
        // 🔴 바닥 맞춤(snapBottom)을 쓰면 가장 낮은 층(z 0.03)이 섬 윗면과 **같은 높이**가 되어 땅과 깜빡이며 사라진다
        //    (09-13 inspect 실측: 문자띠·기하 층 월드 y 1.00 = 섬 윗면, 복귀·스토리에서 별 문양이 안 보였음).
        //    원점(바닥 가운데)을 섬 윗면보다 조금 위에 둔다 — 작은 뽑기 포탈(배율 0.65)에서도 층이 땅과 떨어지게.
        Vector3 ground = new Vector3(disc.transform.position.x, MapLayout.IslandTop + CircleLift, disc.transform.position.z);
        GameObject circle = Place(disc.transform.parent, model, disc.name + "_마법진", ground, 0f, scale, snapBottom: false);
        if (circle == null) return false;

        HideRenderer(disc);
        // 빛으로 된 마법진이다 — 떠 있는 고리가 땅에 그림자를 떨어뜨리면 물체처럼 보인다(blender v3 렌더에서 확인).
        foreach (Renderer renderer in circle.GetComponentsInChildren<Renderer>(true))
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

    // ───────────────────────── 선택 위습 구역(10-20) ─────────────────────────

    /// <summary>축마다 따로 키우는 모델 세우기(부스 깊이를 줄이려고 z만 다르게). 원점은 바닥 가운데, 콜라이더는 지운다.</summary>
    public static GameObject PlaceStretched(Transform parent, string model, string name, Vector3 ground, float yaw, Vector3 scale)
    {
        GameObject asset = Load(model);
        if (asset == null) return null;
        // FBX 뿌리는 축 변환 회전이 있어 뿌리 배율을 직접 곱하면 축이 바뀐다 — 회전 없는 빈 부모가 월드 축 배율을 받고 모델은 그 안에 그대로 둔다.
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.position = ground;
        holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        holder.transform.localScale = scale;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
        instance.transform.localPosition = Vector3.zero;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        placed.Add(model);
        return holder;
    }

    static readonly Color BoothPortalPink = new Color(1f, 0.3f, 0.75f, 1f);

    /// <summary>선택 위습 부스 앞 마법진을 분홍 계열로 — 보랏빛 텍스처에 분홍 틴트를 곱한 재질 사본(원본 재질은 그대로).</summary>
    public static void TintBoothPortalPink(GameObject circle)
    {
        if (circle == null) return;
        const string dir = "Assets/Art/Structures/Materials";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Art/Structures", "Materials");
        foreach (Renderer renderer in circle.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string path = $"{dir}/선택위습_분홍_{mats[i].name}.mat";
                Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy == null)
                {
                    copy = new Material(mats[i]);
                    foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor", "_Tint" })
                        if (copy.HasProperty(property)) copy.SetColor(property, copy.GetColor(property) * BoothPortalPink);
                    AssetDatabase.CreateAsset(copy, path);
                }
                mats[i] = copy;
            }
            renderer.sharedMaterials = mats;
        }
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
            if (DressPortal(disc, "포탈_마법진_뽑기", GachaGlow, light: false))
            {
                dressed++;
                if (disc.name.StartsWith("흔함선택_")) TintBoothPortalPink(GameObject.Find(disc.name + "_마법진"));   // 선택 위습 부스 앞 마법진은 분홍(사진)
            }
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

        // size는 아직 스케일 전(자산 원본) 치수라 배치 여백 계산에도 MapLayout.Scale을 곱해야
        // 한다 — 안 곱하면 실제로 세워질(스케일된) 헛간이 이 여백보다 커서 섬 밖으로 걸친다
        // (원작 비율 5단계, 2026-09-23).
        Vector3 ground = new Vector3(island.center.x, MapLayout.IslandTop,
                                     island.center.y + island.size.y * 0.5f - size.size.z * 0.5f * MapLayout.Scale - 1.5f);
        GameObject shed = Place(parent, model, $"{island.name}_창고헛간", ground, 0f, MapLayout.Scale);
        if (shed == null || !TryMeasure(shed, out Bounds bounds)) return;

        GameObject blocker = new GameObject($"{island.name}_창고헛간_막음");
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = bounds.center;
        blocker.AddComponent<BoxCollider>().size = bounds.size;
    }

    // ───────────────────────── S2 장식 모델(옛 맵에 얹기, 장식만) ─────────────────────────
    // blender 추출본(~/grd/GRD_s2_models/map): fbx 1m = 워크3 100 단위. 원작 도두드 배율 s → 우리 월드 배율 = s × 100 ÷ Scale.
    // 맵 생성기를 건드리지 않고 메뉴(OldMapDecor)로 씬에 「S2장식」 루트 하나를 얹는다. 콜라이더 없음(Place가 지움) · NavMesh 굽기엔 안 들어감.
    static bool OnAnyIsland(float x, float z)
    {
        foreach (MapLayout.Island island in MapLayout.Zones.Concat(MapLayout.Warehouses).Concat(MapLayout.SealIslands).Concat(MapLayout.Lanes))
            if (Mathf.Abs(x - island.center.x) < island.size.x * 0.5f && Mathf.Abs(z - island.center.y) < island.size.y * 0.5f) return true;
        return false;
    }

    // 원작 포탈은 검은 바탕이 투명(가산 합성)이라 그대로 그리면 검은 판이 선다 — 재질을 가산 입자 재질로 갈아 끼운다.
    static void MakeAdditive(GameObject root, string onlyNameContains = null)
    {
        const string dir = "Assets/Art/Structures/Materials";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Art/Structures", "Materials");
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (onlyNameContains != null && !mats[i].name.Contains(onlyNameContains) && !renderer.name.Contains(onlyNameContains)) continue;
                Texture tex = mats[i].HasProperty("_BaseMap") ? mats[i].GetTexture("_BaseMap") : mats[i].mainTexture;
                string path = $"{dir}/장식_대형포탈_{mats[i].name}_가산.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = m == null;
                if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, onlyNameContains != null ? 0.5f : 1f));   // 상점 빛 겹은 절반 세기(사장님 전 PM 「너무 세다」)
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_BLENDMODE_ADD");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                if (isNew) AssetDatabase.CreateAsset(m, path); else { EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); }
                mats[i] = m;
            }
            renderer.sharedMaterials = mats;
        }
    }

    public static string PlaceOldMapLandmarks(Transform parent)
    {
        MapLayout.Island story = System.Array.Find(MapLayout.Zones, z => z.name == "StoryZone");
        MapLayout.Island gacha = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        MapLayout.Island lane1 = System.Array.Find(MapLayout.Lanes, z => z.name == "Lane1");
        MapLayout.Island lane2 = System.Array.Find(MapLayout.Lanes, z => z.name == "Lane2");
        float k = 100f / MapLayout.Scale;
        float seaEdge = MapLayout.SeaSize * 0.5f;

        float westX = lane1.center.x - lane1.size.x * 0.5f - 420f;          // 서쪽 레인 가장자리 바깥 바다
        float northZ = lane1.center.y + lane1.size.y * 0.5f + 330f;         // 북쪽 레인 가장자리 바깥 바다
        float eastX = lane2.center.x + lane2.size.x * 0.5f + 650f;          // 오른쪽(창고 위) 바다
        Vector3 storyTop = new Vector3(story.center.x, MapLayout.IslandTop, story.center.y);
        GameObject returnPortal = GameObject.Find("스토리_복귀포탈");   // 스토리존의 마법진 — 소용돌이를 그 중심 위에 겹친다
        Vector3 portalAt = returnPortal != null
            ? new Vector3(returnPortal.transform.position.x, MapLayout.IslandTop, returnPortal.transform.position.z)
            : storyTop + new Vector3(0f, 0f, story.size.y * 0.5f - 120f);
        float statueX = story.center.x - story.size.x * 0.5f + 70f;         // 섬 안쪽 서쪽 줄

        // (모델, 이름, 자리, 보는 각도(도), 원작 배율)
        var items = new (string model, string name, Vector3 at, float yaw, float scale)[]
        {
            ("장식_고잉메리", "S2_고잉메리", new Vector3(westX, 0f, lane1.center.y - lane1.size.y * 0.5f - 60f), 60f, 1.05f),
            ("장식_룸바배", "S2_룸바배", new Vector3(eastX, 0f, northZ + 120f), -140f, 1.8f),
            ("장식_포네글리프", "S2_포네글리프", new Vector3(story.center.x - story.size.x * 0.5f + 110f, MapLayout.IslandTop, story.center.y + story.size.y * 0.5f - 110f), -305f, 1.2f),
            ("장식_대형스토리포탈", "S2_대형스토리포탈", portalAt, -270f, 1.5f),
            ("장식_위습동상", "S2_위습동상1", new Vector3(statueX, MapLayout.IslandTop, story.center.y + 255f), -180f, 1f),
            ("장식_위습동상", "S2_위습동상2", new Vector3(statueX, MapLayout.IslandTop, story.center.y + 85f), -180f, 1f),
            ("장식_위습동상", "S2_위습동상3", new Vector3(statueX, MapLayout.IslandTop, story.center.y - 85f), -180f, 1f),
            ("장식_위습동상", "S2_위습동상4", new Vector3(statueX, MapLayout.IslandTop, story.center.y - 255f), -180f, 1f),
            ("장식_불타는화로", "S2_불타는화로", new Vector3(gacha.center.x + gacha.size.x * 0.5f - 90f, MapLayout.IslandTop, gacha.center.y - gacha.size.y * 0.5f + 90f), -211f, 1.5f),
        };
        string report = "";
        foreach (var item in items)
        {
            Vector3 at = item.at;
            if (OnAnyIsland(at.x, at.z)) at.y = MapLayout.IslandTop;
            GameObject placed = Place(parent, item.model, item.name, at, item.yaw, item.scale * k);
            if (placed == null) { report += $"\n  ⚠️ {item.model} 모델 없음"; continue; }
            if (item.model == "장식_대형스토리포탈") MakeAdditive(placed);
            if (item.model == "장식_포네글리프" && TryMeasure(placed, out Bounds pb))
            {
                // 비석: 키 = 유닛 키 1.5배, 아래 1/4은 땅에 묻는다.
                float target = ArtBinder.UnitHeight * 1.5f;
                placed.transform.localScale *= target / pb.size.y;
                if (TryMeasure(placed, out Bounds pb2))
                    placed.transform.position += Vector3.up * (at.y - pb2.min.y - pb2.size.y * 0.25f);
            }
            string size = TryMeasure(placed, out Bounds b) ? $"{b.size.x:0.#}×{b.size.y:0.#}×{b.size.z:0.#}" : "?";
            report += $"\n  {item.name} 자리 ({at.x:0},{at.z:0}) 크기 {size} {(OnAnyIsland(at.x, at.z) ? "섬 위" : "바다")} 바다끝 ±{seaEdge:0}";
        }
        return "S2 장식(옛 맵):" + report;
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
            // bounds는 자산 원본(배율 1) 치수다. 실제로 세울 인스턴스는 MapLayout.Scale로 키우므로
            // 간격 계산용 radius도 같이 키워야 한다 — 안 그러면 간격 검사가 커진 실제 크기보다
            // 좁게 잡혀 소품끼리 겹친다(원작 비율 5단계, 2026-09-23).
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f * MapLayout.Scale;
            if (zMax - zMin < radius * 2f) continue;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float x = Mathf.Lerp(xMin + radius, xMax - radius, (float)rng.NextDouble());
                float z = Mathf.Lerp(zMin + radius, zMax - radius, (float)rng.NextDouble());
                if (Mathf.Abs(x - island.center.x) < pathClear + radius) continue;
                if (spots.Any(s => (s.x - x) * (s.x - x) + (s.y - z) * (s.y - z) < (s.z + radius + 1f) * (s.z + radius + 1f)))
                    continue;

                GameObject prop = Place(parent, model, $"펑크해저드_{label}_{spots.Count + 1:00}",
                    new Vector3(x, MapLayout.IslandTop, z), (float)rng.NextDouble() * 360f, MapLayout.Scale);
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

    /// <param name="onlyIsland">이 섬의 부두만 짓는다(맵 일부만 다시 지을 때 — 부두 순번 i는 그대로라 목선 모양이 전체 생성과 같다). null이면 전부.</param>
    public static string PlaceDocks(Transform parent, string onlyIsland = null)
    {
        int piers = 0;
        for (int i = 0; i < DockSites.Length; i++)
        {
            var site = DockSites[i];
            if (onlyIsland != null && site.island != onlyIsland) continue;
            MapLayout.Island island = MapLayout.Zones.Concat(MapLayout.Warehouses).FirstOrDefault(z => z.name == site.island);
            if (string.IsNullOrEmpty(island.name)) continue;

            Vector3 d = new Vector3(site.direction.x, 0f, site.direction.y);
            Vector3 perp = new Vector3(-d.z, 0f, d.x);
            float halfAlong = Mathf.Abs(d.x) * island.size.x * 0.5f + Mathf.Abs(d.z) * island.size.y * 0.5f;
            float halfPerp = Mathf.Abs(perp.x) * island.size.x * 0.5f + Mathf.Abs(perp.z) * island.size.y * 0.5f;
            Vector3 center = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);
            Vector3 root = center + d * halfAlong + perp * ((site.along - 0.5f) * 2f * halfPerp * 0.8f);
            float yaw = YawFacing(d);

            // 원작 비율 5단계(PM 지시 2026-09-23) — 모델·간격 상수 전부 MapLayout.Scale을 곱한다.
            // pierLength는 잔교의 실측 길이라, 곱하지 않으면 목선이 커진 잔교 옆이 아니라
            // 갑판 한중간에 박힌다.
            string pierModel = site.longPier ? "부두_잔교_긴" : "부두_잔교_짧은";
            float pierLength = (site.longPier ? 24f : 14f) * MapLayout.Scale;
            // 잔교 원점 = 섬 쪽 끝 갑판 윗면(z 0) → 섬 윗면에 그대로 둔다.
            if (Place(parent, pierModel, $"{site.island}_잔교", root, yaw, MapLayout.Scale, snapBottom: false) == null) continue;
            piers++;

            // 목선 — 잔교 옆 물 위(원점 = 흘수선).
            Vector3 boat = root + d * (pierLength * 0.55f) + perp * (5.5f * MapLayout.Scale);
            Place(parent, i % 2 == 0 ? "부두_목선_01" : "부두_목선_02", $"{site.island}_목선",
                  new Vector3(boat.x, SeaTop, boat.z), yaw, MapLayout.Scale, snapBottom: false);

            // 섬 가장자리 소품 — 게임 카메라에서 점처럼 작아 배율을 키운다(MANIFEST). 그 2.5·2배
            // 과장은 그대로 두고 MapLayout.Scale을 곱해서 얹는다 — 맵 배율과 같이 커진다.
            foreach (float side in new[] { -1f, 1f })
                Place(parent, "부두_계선주", $"{site.island}_계선주",
                      root - d * (2f * MapLayout.Scale) + perp * (3.5f * MapLayout.Scale * side), yaw, 2.5f * MapLayout.Scale);
            Place(parent, "부두_부표더미", $"{site.island}_부표더미",
                  root - d * (4f * MapLayout.Scale) + perp * (7f * MapLayout.Scale), yaw, 2f * MapLayout.Scale);
        }
        return piers > 0 ? $"\n부두: 잔교 {piers}곳(목선·계선주·부표 포함)." : "";
    }
}
