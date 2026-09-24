using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

/// <summary>
/// MapLayout의 수치대로 원랜디 맵을 씬에 생성한다.
/// 섬이 19개에 포탈·경로까지 있어 손배치는 현실적이지 않아 메뉴로 만든다.
/// 생성물은 전부 "Map" 루트 아래에 들어가므로, 다시 실행하면 통째로 갈아끼운다.
/// </summary>
public static class MapGenerator
{
    const string RootName = "Map";
    const string MaterialFolder = "Assets/Materials/Map";
    const string TextureFolder = "Assets/Textures/Map";
    const string Title = "맵 생성";
    const float GrassThickness = 0.35f;   // 잔디 윗면 두께
    // 바위 치마 높이 (바다 아래까지 내려간다). 윗면은 늘 잔디 밑면(IslandTop − GrassThickness)에
    // 붙으므로, 아랫면이 바다 상자 바닥(−IslandThickness)보다 내려가려면 이만큼은 돼야 한다.
    // 2026-09-23에 IslandTop이 1 → 8이 되면서(NavMeshVoxelSize 주석 참고) 2.2로는 섬이 공중에
    // 떴다 — 치마가 수면까지 못 닿았다. 8.65가 최소고 여유를 둬 10으로 잡는다.
    const float CliffHeight = 10f;
    // 잔디보다 얼마나 넓게 나올지. **섬의 진짜 바깥 끝은 잔디가 아니라 이 치마다** —
    // 그래서 MapLayout으로 옮겼다(2026-09-24). 섬 사이 간격을 재는 쪽이 이 값을 알아야
    // 「섬변끼리 38.1」과 「보이는 끝끼리 34.6」이 갈리는 것을 셀 수 있다.
    const float CliffOverhang = MapLayout.CliffOverhang;

    // 구역을 색으로만 구분하면 원랜디 느낌이 안 난다. 잔디/물/바위 텍스처를 깔고
    // 구역 구분은 잔디에 옅은 색조를 얹는 정도로만 한다.
    struct Surface
    {
        public string texture;      // Assets/Textures/Map/<이름>.png
        public Color tint;
        /// <summary>
        /// 면적 1 × 1당 타일 몇 장. ⚠️ 이 값은 **맵 배율 1일 때** 기준이다 —
        /// 실제로 쓸 때는 <see cref="TilesPerUnit"/>가 Scale로 나눠 준다.
        /// </summary>
        public float tilesPerUnit;
        public float smoothness;

        public Surface(string texture, Color tint, float tilesPerUnit, float smoothness)
        {
            this.texture = texture;
            this.tint = tint;
            this.tilesPerUnit = tilesPerUnit;
            this.smoothness = smoothness;
        }
    }

    static readonly Dictionary<string, Surface> Surfaces = new Dictionary<string, Surface>
    {
        { "sea",       new Surface("water", new Color(0.78f, 0.90f, 1.00f), 0.080f, 0.92f) },
        { "rock",      new Surface("rock",  Color.white,                    0.140f, 0.10f) },
        { "dirt",      new Surface("dirt",  Color.white,                    0.180f, 0.05f) },
        { "lane",      new Surface("grass", Color.white,                    0.120f, 0.05f) },
        { "warehouse", new Surface("grass", new Color(1.00f, 0.94f, 0.78f), 0.120f, 0.05f) },
        { "seal",      new Surface("grass", new Color(0.82f, 1.00f, 0.94f), 0.160f, 0.05f) },
        { "event",     new Surface("grass", new Color(1.00f, 0.76f, 0.70f), 0.120f, 0.05f) },
        { "display",   new Surface("grass", new Color(0.86f, 0.80f, 1.00f), 0.120f, 0.05f) },
        { "story",     new Surface("grass", new Color(0.92f, 0.84f, 1.00f), 0.120f, 0.05f) },
        { "gacha",     new Surface("grass", new Color(1.00f, 0.98f, 0.82f), 0.120f, 0.05f) },
        { "combine",   new Surface("grass", new Color(0.90f, 0.90f, 0.88f), 0.120f, 0.05f) },
        { "portal",    new Surface(null,    new Color(0.30f, 0.70f, 0.85f), 0f,     0.60f) },
    };

    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/맵/원랜디 맵 생성")]
    static void Generate()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;
        if (!EnsureSampleSceneOpen()) return;

        GameObject existing = GameObject.Find(RootName);
        if (existing != null &&
            !EditorGuards.Dialog(Title,
                "이미 Map이 있습니다. 지우고 다시 만들까요?\n(Map 아래 직접 수정한 것은 사라집니다)",
                "다시 만들기", "취소"))
            return;

        if (existing != null) Object.DestroyImmediate(existing);

        TiledCache.Clear();
        TiledUsed.Clear();

        GameObject root = new GameObject(RootName);
        StructureDresser.BeginReport();
        string seaReport = BuildSea(root.transform);

        List<PirateQuestData> pirateQuests = LoadPirateQuests();

        List<GameObject> laneObjects = new List<GameObject>();
        for (int i = 0; i < MapLayout.Lanes.Length; i++)
        {
            GameObject laneObject = BuildIsland(root.transform, MapLayout.Lanes[i]);
            laneObject.AddComponent<LaneMarker>().SetLaneIndex(i);
            // 앞치마(우리 줄 + 상점 줄) 지반. 6단계에서 레인 섬 = 필드가 되면서 이 땅이
            // 섬 밖으로 나갔다 — 따로 안 깔면 우리와 상점이 바다 위에 뜬다.
            BuildIsland(root.transform, MapLayout.LaneApron(MapLayout.Lanes[i]));
            DecorateLane(root.transform, MapLayout.Lanes[i]);
            BuildLaneShopStrip(root.transform, MapLayout.Lanes[i]);
            WireUnitPen(laneObject, BuildUnitPen(root.transform, MapLayout.Lanes[i], i), MapLayout.Lanes[i]);
            BuildSupportShop(root.transform, MapLayout.Lanes[i], i);
            BuildGamblingShop(root.transform, MapLayout.Lanes[i], i);
            BuildUnitUpgradeShop(root.transform, MapLayout.Lanes[i], i);
            BuildOtherWorldUpgradeShop(root.transform, MapLayout.Lanes[i], i);
            BuildEternalUpgradeShop(root.transform, MapLayout.Lanes[i], i);
            BuildAttackTypeUpgradeShop(root.transform, MapLayout.Lanes[i], i);
            BuildPirateQuestShop(root.transform, MapLayout.Lanes[i], i, pirateQuests);
            BuildVoyageLogShop(root.transform, MapLayout.Lanes[i], i);
            BuildStoryZonePortal(root.transform, MapLayout.Lanes[i], i);
            laneObjects.Add(laneObject);
        }
        BuildInterLaneWalls(root.transform);

        for (int i = 0; i < MapLayout.Warehouses.Length; i++)
        {
            GameObject warehouseIsland = BuildIsland(root.transform, MapLayout.Warehouses[i]);
            // 창고는 섬 위에 붙는다 — 자기 위치가 곧 유닛을 보낼 곳이다.
            Warehouse warehouse = warehouseIsland.AddComponent<Warehouse>();
            SerializedObject so = new SerializedObject(warehouse);
            so.FindProperty("ownerPlayerId").intValue = i;
            so.ApplyModifiedProperties();
            StructureDresser.PlaceWarehouseShed(root.transform, MapLayout.Warehouses[i], i);
        }
        foreach (MapLayout.Island island in MapLayout.SealIslands) BuildIsland(root.transform, island);

        GameObject gachaIsland = null;
        GameObject combineIsland = null;
        foreach (MapLayout.Island island in MapLayout.Zones)
        {
            GameObject created = BuildIsland(root.transform, island);
            if (island.name == "GachaIsland") gachaIsland = created;
            if (island.name == "CombineTable") combineIsland = created;
        }

        List<WaypointPath> lanePaths = BuildLanePaths(root.transform);
        string tableReport = BuildCombineColumns(combineIsland);
        string displayReport = BuildGradeDisplays(root.transform);
        string gateReport = BuildPunkHazardGate(root.transform);
        string storyReport = BuildStoryZone(root.transform);
        BuildStoryReturnPortal(root.transform);
        string sealReport = BuildSealSpawners(root.transform);
        string seaKingReport = BuildSeaKing(root.transform) + BuildTreasureHunt(root.transform);
        string questReport = BuildPirateQuestManager() +
            $"\n해적단 퀘스트 상점: {pirateQuests.Count}개 연결(레인당 1개, 재고·보충은 상점이 스스로 관리)." +
            (pirateQuests.Count == 0 ? $"\n  ⚠️ {PirateQuestFolder}에서 PirateQuestData를 하나도 못 찾았습니다." : "");
        string chatUnlockReport = BuildChatUnlockManager();
        string hiddenCombineReport = BuildHiddenCombineManager();
        string chatBoxReport = BuildGameChatBox();

        string portalReport = BuildGachaPortals(gachaIsland);
        StructureDresser.DressGachaPortals(root.transform);
        string dockReport = StructureDresser.PlaceDocks(root.transform);

        // 자연물은 **맨 마지막에** 뿌린다 — 건물·포탈·인형이 다 선 뒤라야 그 자리를 피할 수 있다.
        string natureReport = BuildNatureBorders(root.transform);

        string overlaps = CheckOverlaps();

        // 배선 중 예외가 나도 NavMesh는 굽는다. 여기서 통째로 죽으면 길이 안 깔린 맵이 남는데,
        // 화면에는 멀쩡한 맵으로 보이고 유닛만 안 움직여서 원인을 찾기가 매우 어렵다.
        string rewire;
        try
        {
            rewire = RewireScene(lanePaths);
        }
        catch (System.Exception e)
        {
            rewire = $"\n⚠️ 씬 배선 중 오류가 나서 일부만 적용됐습니다: {e.Message}";
            Debug.LogException(e);
        }

        string navResult = BuildNavMesh(root);
        string oldGround = DisableOldGround();

        // HUD 넷은 씬에 GameObject 하나씩만 있으면 되는데, 메뉴로만 두니 아무도 안 돌려서
        // 씬에 0개인 채로 오래 굴렀다(이름표가 화면에 한 번도 안 나온 첫 번째 이유).
        // 맵 생성이 어차피 씬을 갈아엎으니 여기서 보장한다. ⚠️ 저장보다 먼저.
        string hudReport = HudWiring.EnsureAll();

        // BindTextures가 고친 재질은 SetDirty만 걸려 있다 — 여기서 디스크에 남기지 않으면
        // 다음에 열 때 도로 빈 채로 돌아온다.
        // 🔴 기본 재질은 **크기별 변종이 새로 필요할 때만** 손이 닿는다. 변종 55종이 이미 다 있으면
        //    GetOrCreateMaterial이 한 번도 안 불려서, 화면은 맞는데 기본 재질만 옛 값으로 남는다
        //    (09-24에 「표와 다른 재질 10장」이 그렇게 떴다). 판마다 한 번 표에 맞춰 둔다.
        foreach (KeyValuePair<string, Surface> entry in Surfaces) GetOrCreateMaterial(entry.Key, entry.Value);

        string sweep = SweepTiledMaterials();
        AssetDatabase.SaveAssets();
        string textureReport = SurfaceTextureReport() + sweep + SurfaceDriftReport() + WispCellClearanceReport();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        // 여기서 바로 저장한다. 생성기는 씬을 통째로 갈아엎고 NavMesh 에셋까지 새로 쓰는데,
        // 저장을 사람 손에 맡기면 한 번 빠뜨렸을 때 "구웠는데 아무도 안 움직인다"가 된다.
        // 실제로 그것 때문에 오래 헤맸다.
        string saveNote = EditorSceneManager.SaveScene(root.scene)
            ? "\n\n씬을 저장했습니다."
            : "\n\n⚠️ 씬 저장에 실패했습니다 — Cmd+S를 직접 눌러주세요.";

        string message =
            BaselineReport() +
            $"섬 {MapLayout.Lanes.Length + MapLayout.Warehouses.Length + MapLayout.SealIslands.Length + MapLayout.Zones.Length}개, " +
            $"레인 경로 {lanePaths.Count}개를 만들었습니다." + portalReport + natureReport +
            seaReport + dockReport + StructureDresser.Report() + "\n\n" +
            tableReport + displayReport + gateReport + storyReport + sealReport + seaKingReport + questReport +
            chatUnlockReport + hiddenCombineReport + chatBoxReport + overlaps + navResult + oldGround +
            textureReport + hudReport + rewire + saveNote;
        Debug.Log("[맵] " + message);
        EditorGuards.Dialog(Title, message, "확인");
    }

    static string BuildSea(Transform parent)
    {
        GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sea.name = "Sea";
        sea.transform.SetParent(parent, false);
        sea.transform.localPosition = new Vector3(0f, -MapLayout.IslandThickness * 0.5f, 0f);
        sea.transform.localScale = new Vector3(MapLayout.SeaSize, MapLayout.IslandThickness, MapLayout.SeaSize);
        Paint(sea, "sea", MapLayout.SeaSize, MapLayout.SeaSize);
        sea.AddComponent<SeaScroll>();

        // 바다도 NavMesh에 굽되 Sea 영역으로 표시한다. 지상 유닛은 UnitSpawner가 areaMask에서
        // 이 영역을 빼기 때문에 못 지나가고, 비행·수상보행 유닛만 지나간다.
        NavMeshModifier modifier = sea.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = MapLayout.SeaAreaIndex;

        // 보이는 수면은 물결 격자가 맡는다(2026-09-13). 이 상자는 콜라이더(NavMesh Sea 영역)로만 남는다.
        return SeaWaterBuilder.Apply(parent, sea);
    }

    static GameObject BuildIsland(Transform parent, MapLayout.Island island)
    {
        // 섬을 판 하나로 두면 옆면이 잔디로 칠해져 절벽처럼 안 보인다.
        // 조금 더 넓고 낮은 바위 덩어리를 아래에 깔아 가장자리를 만든다.
        GameObject skirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        skirt.name = island.name + "_Cliff";
        skirt.transform.SetParent(parent, false);
        skirt.transform.localPosition = new Vector3(
            island.center.x, MapLayout.IslandTop - CliffHeight * 0.5f - GrassThickness, island.center.y);
        skirt.transform.localScale = new Vector3(
            island.size.x + CliffOverhang, CliffHeight, island.size.y + CliffOverhang);
        Paint(skirt, "rock", island.size.x, island.size.y);

        // 순수 장식이다. 콜라이더를 남기면 NavMesh가 잔디보다 한 단 낮은 이 턱까지
        // 걸을 수 있는 곳으로 구워서, 유닛이 섬 가장자리 밖으로 내려선다.
        Object.DestroyImmediate(skirt.GetComponent<Collider>());

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = island.name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(
            island.center.x, MapLayout.IslandTop - GrassThickness * 0.5f, island.center.y);
        obj.transform.localScale = new Vector3(island.size.x, GrassThickness, island.size.y);
        Paint(obj, island.tint, island.size.x, island.size.y);
        return obj;
    }

    // ──────────────────────────────────────────────────────────── 섬 테두리 자연물
    //
    // 사장님 지시 2026-09-12 「테두리로 맵에 뿌려줘」. Assets/Art/Nature의 저폴리 돌·나무·풀
    // (Blender로 생성, Tools/blender/gen_nature.py)을 **섬 가장자리 띠에만** 둔다 — 디펜스 맵이라
    // 한가운데에 두면 유닛과 적을 가린다.
    //
    // - 띠 안에만 둔다. 소품의 발자국(원)이 섬 안쪽, 가장자리에서 띠 폭 이내에 들어가야 한다.
    //   레인은 띠를 7로 좁게 잡는다 — 흙길(순찰로)이 가장자리에서 8부터 시작해서 그걸 안 덮으려는 것이다.
    //   레인 아래 두 줄(우리·상점)은 통째로 건너뛴다.
    // - 키 큰 나무만 가지가 바다 쪽으로 나가도 된다(줄기가 섬 안이면). 돌·풀이 밖으로 나가면 떠 보인다.
    // - 이미 선 건물·포탈·인형·벽과 겹치면 안 둔다(렌더러 경계로 판정).
    // - 크기는 배율이 아니라 **게임 단위 높이**로 정한다(사람 키 20). 잰 높이로 나눠 맞추므로
    //   FBX 임포트 단위(useFileScale)가 어떻게 먹어도 크기가 안 틀어진다.
    // - 씨앗은 섬 이름에서 뽑는다 — 다시 생성해도 같은 자리에 같은 것이 선다(씬 변경이 안 쌓인다).
    // - 콜라이더가 없고(임포트 addColliders 0) NavMesh는 콜라이더로 굽으므로 길을 안 막는다.
    //   혹시 모르니 담는 오브젝트에 ignoreFromBuild도 건다.

    const string NatureFolder = "Assets/Art/Nature/";
    const int NatureLane = 1, NatureSmall = 2, NatureBig = 4;

    readonly struct NatureProp
    {
        public readonly string file;        // NatureFolder 아래, 확장자 뺀 경로
        public readonly float minHeight;    // 게임 단위(사람 키 20)
        public readonly float maxHeight;
        public readonly int weight;
        public readonly int zones;          // NatureLane | NatureSmall | NatureBig
        public readonly bool canOverhang;   // 줄기만 섬 안이면 가지가 바다 쪽으로 나가도 된다

        public NatureProp(string file, float minHeight, float maxHeight, int weight, int zones, bool canOverhang = false)
        {
            this.file = file;
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
            this.weight = weight;
            this.zones = zones;
            this.canOverhang = canOverhang;
        }
    }

    // 무게가 클수록 자주 나온다. 나무는 적게 — 많으면 섬 테두리가 숲이 되어 시야를 가린다.
    static readonly NatureProp[] NatureProps =
    {
        new NatureProp("Grass/풀_01", 4f, 6f, 6, NatureLane | NatureSmall | NatureBig),
        new NatureProp("Grass/풀_02", 6f, 8f, 6, NatureLane | NatureSmall | NatureBig),
        new NatureProp("Grass/풀_03", 8f, 11f, 5, NatureLane | NatureSmall | NatureBig),
        new NatureProp("Grass/덤불_01", 7f, 11f, 4, NatureBig),
        new NatureProp("Grass/덤불_02", 10f, 15f, 3, NatureBig),
        new NatureProp("Grass/억새_01", 14f, 20f, 3, NatureBig, canOverhang: true),
        new NatureProp("Rocks/바위_01", 2f, 5f, 5, NatureLane | NatureSmall | NatureBig),
        new NatureProp("Rocks/바위_02", 5f, 8f, 4, NatureLane | NatureBig),
        new NatureProp("Rocks/바위_03", 7f, 10f, 3, NatureBig),
        new NatureProp("Rocks/바위_04", 9f, 13f, 2, NatureBig),
        new NatureProp("Rocks/바위_05", 14f, 22f, 1, NatureBig),
        new NatureProp("Rocks/바위무리_01", 8f, 12f, 2, NatureBig),
        new NatureProp("Rocks/바위무리_02", 11f, 16f, 1, NatureBig),
        new NatureProp("Rocks/판석_01", 0.8f, 1.5f, 3, NatureLane | NatureSmall | NatureBig),
        new NatureProp("Rocks/판석_02", 1.5f, 2.2f, 2, NatureLane | NatureBig),
        // 2026-09-12 사실적 바위 11종 추가(구현담당2, 3d699f49). 해안바위·암벽조각은 섬 가장자리에 걸쳐
        // 바다 쪽으로 튀어나오는 게 자연스러워 canOverhang — 원 전체를 띠에 넣으면 너무 작아져 늘 빠진다.
        new NatureProp("Rocks/둥근강돌_01", 1.5f, 2.5f, 3, NatureLane | NatureSmall | NatureBig),
        new NatureProp("Rocks/둥근강돌_02", 3f, 5f, 2, NatureLane | NatureBig),
        new NatureProp("Rocks/뾰족바위_01", 10f, 16f, 1, NatureBig),
        new NatureProp("Rocks/뾰족바위_02", 16f, 24f, 1, NatureBig),
        new NatureProp("Rocks/암벽조각_01", 12f, 21f, 1, NatureBig, canOverhang: true),
        new NatureProp("Rocks/암벽조각_02", 14f, 27f, 1, NatureBig, canOverhang: true),
        new NatureProp("Rocks/이끼바위_01", 4f, 6f, 2, NatureBig),
        new NatureProp("Rocks/이끼바위_02", 8f, 12f, 1, NatureBig),
        new NatureProp("Rocks/자갈무리_01", 3f, 5f, 2, NatureBig),
        new NatureProp("Rocks/해안바위_01", 2.5f, 4f, 2, NatureBig, canOverhang: true),
        new NatureProp("Rocks/해안바위_02", 5f, 8f, 1, NatureBig, canOverhang: true),

        // 나무 20종(C 스타일, 2026-09-12 구현담당1 9355113d). 종류가 세 배로 늘어 무게를 전부 1로 낮췄다 —
        // 그대로 두면 큰 섬 테두리가 숲이 되어 시야를 가린다. 높이는 실제 비율의 1/3쯤(사람 키 20 기준)으로
        // 줄여 둔다 — 실제 크기면 나무 한 그루가 섬 절반을 덮는다.
        new NatureProp("Trees/그루터기_01", 4f, 6f, 2, NatureLane | NatureBig),
        new NatureProp("Trees/쓰러진통나무_01", 3f, 5f, 1, NatureBig),
        new NatureProp("Trees/어린나무_01", 8f, 14f, 2, NatureBig),
        new NatureProp("Trees/침엽수_01", 26f, 38f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/침엽수_02", 30f, 42f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/소나무_01", 24f, 34f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/전나무_01", 30f, 44f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/가문비_01", 28f, 38f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/활엽수_01", 22f, 32f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/활엽수_가을", 24f, 34f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/참나무_01", 24f, 34f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/자작나무_01", 26f, 38f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/벚나무_01", 20f, 30f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/단풍나무_01", 22f, 32f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/은행나무_01", 26f, 36f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/버드나무_01", 20f, 28f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/야자수_01", 22f, 30f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/야자수_02", 24f, 32f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/야자수_03", 16f, 24f, 1, NatureBig, canOverhang: true),
        new NatureProp("Trees/죽은나무_01", 20f, 30f, 1, NatureBig, canOverhang: true),
    };

    class NatureAsset
    {
        public GameObject asset;
        public NatureProp prop;
        public float height;            // 잰 원본 높이
        public float minY;              // 원점에서 최저점까지 — 0이어야 정상이지만 혹시 몰라 보정한다
        public float radiusPerHeight;   // 원점 기준 가로 반경 ÷ 높이. 어떻게 돌려도 이 원 안에 든다
    }

    static string BuildNatureBorders(Transform root)
    {
        List<NatureAsset> assets = new List<NatureAsset>();
        List<string> missing = new List<string>();
        foreach (NatureProp prop in NatureProps)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + prop.file + ".fbx");
            if (asset == null || !TryMeasureFigure(asset, out Bounds bounds) || bounds.size.y < 0.001f)
            {
                missing.Add(prop.file);
                continue;
            }

            // 원점이 발자국 가운데가 아닐 수 있다(야자수는 한쪽으로 기운다) — 원점에서 가장 먼 모서리로 잰다.
            float reachX = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));
            float reachZ = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
            assets.Add(new NatureAsset
            {
                asset = asset,
                prop = prop,
                height = bounds.size.y,
                minY = bounds.min.y,
                radiusPerHeight = Mathf.Sqrt(reachX * reachX + reachZ * reachZ) / bounds.size.y,
            });
        }

        if (assets.Count == 0)
            return "\n⚠️ 섬 테두리 자연물: Assets/Art/Nature에서 FBX를 하나도 못 읽었습니다.";

        // 소품을 놓기 **전에** 이미 선 것들의 자리를 모은다. 섬 판·절벽·바다(윗면이 섬 높이 이하)와
        // 바닥에 깐 흙길·표식(두께 0.6 이하)은 장애물이 아니다.
        List<Rect> obstacles = new List<Rect>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.bounds;
            if (bounds.max.y <= MapLayout.IslandTop + 0.3f) continue;
            if (bounds.size.y <= 0.6f) continue;
            obstacles.Add(Rect.MinMaxRect(bounds.min.x - 1f, bounds.min.z - 1f, bounds.max.x + 1f, bounds.max.z + 1f));
        }

        GameObject container = new GameObject("Nature");
        container.transform.SetParent(root, false);
        container.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        List<Vector3> placed = new List<Vector3>();   // (x, z, 반경)
        int blocked = 0;

        int laneCount = 0;
        foreach (MapLayout.Island lane in MapLayout.Lanes)
            laneCount += ScatterBorder(container.transform, lane, NatureLane, 7f, 6f, 16f,
                MapLayout.LaneApronDepth, assets, obstacles, placed, ref blocked);

        List<MapLayout.Island> others = new List<MapLayout.Island>();
        others.AddRange(MapLayout.Warehouses);
        others.AddRange(MapLayout.SealIslands);
        others.AddRange(MapLayout.Zones);

        int smallCount = 0, bigCount = 0;
        foreach (MapLayout.Island island in others)
        {
            float minSide = Mathf.Min(island.size.x, island.size.y);
            if (minSide < 60f)
                smallCount += ScatterBorder(container.transform, island, NatureSmall, Mathf.Min(6f, minSide * 0.2f),
                    3f, 9f, -1f, assets, obstacles, placed, ref blocked);
            else
                bigCount += ScatterBorder(container.transform, island, NatureBig, Mathf.Min(24f, minSide * 0.22f),
                    1f, 8f, -1f, assets, obstacles, placed, ref blocked);
        }

        string report = $"\n섬 테두리 자연물: {laneCount + smallCount + bigCount}개 — 레인 {laneCount} · 작은 섬 {smallCount} · " +
                        $"큰 섬 {bigCount} (건물·인형과 겹쳐 뺀 자리 {blocked}).";
        if (missing.Count > 0)
            report += $"\n  ⚠️ 못 읽은 자연물 {missing.Count}종: {string.Join(", ", missing)} — " +
                      "유니티가 아직 임포트를 안 했으면 창에 한 번 포커스를 준 뒤 다시 생성하세요.";
        return report;
    }

    // 섬 한 개의 테두리를 변마다 걸으며 소품을 놓는다. bottomApron이 0 이상이면(레인) 아래 변을 빼고,
    // 왼쪽·오른쪽 변도 아래에서 그만큼 올라간 데서 시작한다.
    static int ScatterBorder(Transform parent, MapLayout.Island island, int zone, float band,
        float minGap, float maxGap, float bottomApron, List<NatureAsset> assets,
        List<Rect> obstacles, List<Vector3> placed, ref int blocked)
    {
        List<NatureAsset> choices = assets.Where(a => (a.prop.zones & zone) != 0).ToList();
        if (choices.Count == 0 || band < 1f) return 0;
        int totalWeight = choices.Sum(a => a.prop.weight);

        System.Random rng = new System.Random(StableSeed(island.name));
        float halfX = island.size.x * 0.5f;
        float halfZ = island.size.y * 0.5f;
        float cx = island.center.x;
        float cz = island.center.y;

        bool skipBottom = bottomApron >= 0f;
        // 모서리는 위·아래 변이 덮으므로 왼쪽·오른쪽 변은 띠 폭만큼 건너뛴다.
        float sideStart = skipBottom ? bottomApron : band;
        float sideLength = halfZ * 2f - sideStart - band;

        var sides = new List<(Vector2 start, Vector2 along, Vector2 inward, float length)>
        {
            (new Vector2(cx - halfX, cz + halfZ), Vector2.right, Vector2.down, halfX * 2f),
            (new Vector2(cx - halfX, cz - halfZ + sideStart), Vector2.up, Vector2.right, sideLength),
            (new Vector2(cx + halfX, cz - halfZ + sideStart), Vector2.up, Vector2.left, sideLength),
        };
        if (!skipBottom)
            sides.Add((new Vector2(cx - halfX, cz - halfZ), Vector2.right, Vector2.up, halfX * 2f));

        int count = 0;
        foreach (var side in sides)
        {
            float cursor = (float)rng.NextDouble() * maxGap;
            while (cursor < side.length)
            {
                NatureAsset pick = PickWeighted(choices, totalWeight, rng);
                float height = Mathf.Lerp(pick.prop.minHeight, pick.prop.maxHeight, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;
                float gap = Mathf.Lerp(minGap, maxGap, (float)rng.NextDouble());
                float depthRoll = (float)rng.NextDouble();

                // 발자국이 띠를 넘으면 띠에 맞춰 줄인다. 원래 최소의 60%보다 작아지면 그 자리엔 안 둔다.
                // 가지가 넓은 것(canOverhang)은 줄기만 섬 안이면 되고, 가지는 바다 쪽으로 넘어가도, 띠 안쪽으로
                // 반경의 일부만큼 넘어가도 된다. 발자국 원 전체를 띠에 넣게 하면 넓은 나무가 사람보다 작아진다
                // (2026-09-12 나무 20종 실측: 참나무 가로 158·높이 114 — 원 전체를 띠 24에 넣으면 키가 17이었다).
                // 조건: 줄기 깊이(반경의 25%) + 안쪽으로 뻗는 가지(반경의 60%) ≤ 띠.
                float inwardShare = pick.prop.canOverhang ? 0.85f : 2f;
                float fitHeight = (band - 0.5f) / inwardShare / pick.radiusPerHeight;
                if (fitHeight < height) height = fitHeight;
                if (height < pick.prop.minHeight * 0.6f)
                {
                    cursor += minGap;
                    continue;
                }

                float radius = height * pick.radiusPerHeight;
                float minDepth = pick.prop.canOverhang ? Mathf.Max(1.5f, radius * 0.25f) : radius + 0.5f;
                float maxDepth = Mathf.Max(minDepth, pick.prop.canOverhang ? band - radius * 0.6f : band - radius);
                float along = cursor + radius;
                if (along > side.length) break;

                Vector2 at = side.start + side.along * along + side.inward * Mathf.Lerp(minDepth, maxDepth, depthRoll);
                cursor = along + radius + gap;

                if (HitsAnything(at, radius, obstacles, placed))
                {
                    blocked++;
                    continue;
                }

                PlaceNatureProp(parent, pick, at, height, yaw);
                placed.Add(new Vector3(at.x, at.y, radius));
                count++;
            }
        }
        return count;
    }

    static NatureAsset PickWeighted(List<NatureAsset> choices, int totalWeight, System.Random rng)
    {
        int roll = rng.Next(totalWeight);
        foreach (NatureAsset choice in choices)
        {
            roll -= choice.prop.weight;
            if (roll < 0) return choice;
        }
        return choices[choices.Count - 1];
    }

    static bool HitsAnything(Vector2 at, float radius, List<Rect> obstacles, List<Vector3> placed)
    {
        foreach (Rect rect in obstacles)
        {
            float dx = at.x - Mathf.Clamp(at.x, rect.xMin, rect.xMax);
            float dz = at.y - Mathf.Clamp(at.y, rect.yMin, rect.yMax);
            if (dx * dx + dz * dz < radius * radius) return true;
        }

        foreach (Vector3 other in placed)
        {
            float reach = radius + other.z + 0.5f;
            float dx = at.x - other.x;
            float dz = at.y - other.y;
            if (dx * dx + dz * dz < reach * reach) return true;
        }
        return false;
    }

    static void PlaceNatureProp(Transform parent, NatureAsset pick, Vector2 at, float height, float yaw)
    {
        GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(pick.asset, parent);
        float scale = height / pick.height;

        // FBX 루트가 가진 회전·크기(축 변환)를 지우지 않고 그 위에 곱한다. 대입하면 옆으로 눕는다.
        prop.transform.localScale = pick.asset.transform.localScale * scale;
        prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * pick.asset.transform.localRotation;
        prop.transform.position = new Vector3(at.x, MapLayout.IslandTop - pick.minY * scale, at.y);

        foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        // 수백 개가 서므로 정적 배칭으로 묶어 드로우콜을 줄인다.
        foreach (Transform part in prop.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // string.GetHashCode는 실행 환경마다 달라질 수 있다 — 이름에서 늘 같은 씨앗을 뽑는다(FNV-1a).
    static int StableSeed(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    // 레인 지형. 적이 도는 자리에 흙길을 깐다.
    // 언덕·웅덩이도 넣어봤는데 납작한 원판으로만 보여서 뺐다 —
    // 높낮이는 실제 지형(메시)이 있어야 나오지, 판을 얹어서 될 일이 아니다.
    // 장식이라 콜라이더는 붙이지 않는다. 붙이면 NavMesh가 울퉁불퉁해져
    // 적이 순찰 경로를 못 따라가거나 유닛이 걸린다.
    // 랜덤 위습 포탈이 흔함 대신 해적선을 줄 확률. 원작과 같은 0.24%다(2026-09-06
    // 정정 — 예전 1%는 "안흔함_상붕카"라는 잘못된 자리표에 딸려온 근거 없는 값이었다).
    // 모델을 눈으로 확인할 일이 있으면 잠깐 100으로 올렸다가 반드시 되돌릴 것 —
    // 100인 동안에는 이 포탈에서 흔함 유닛이 하나도 안 나온다.
    const float RandomShipBonusChance = 0.24f;

    // 흙길 폭은 **유닛 크기에 묶인 값이라 맵 배율(Scale)을 안 탄다.** 대신 유닛 크기가 바뀌면
    // 같이 바뀐다 — ArtBinder.EnemyHeight 주석이 "지름 5.4니까 폭 12 길에 2.2마리가 나란히
    // 선다"로 이 값을 직접 근거로 쓰기 때문이다.
    // 2026-09-23 사장님 "유닛이 너무 작아서 안 보인다" → 적 키 15 → 22.5(1.5배)가 되면서
    // 지름이 5.4 → 8.1이 됐다. 폭 12 그대로면 1.48마리밖에 안 들어간다 → **18로 올려
    // 2.22마리를 유지**한다(1.5배, 같은 근거·같은 비율).
    const float TrackWidth = 18f;

    // 🔴 2026-09-23: 흙길 inset을 고정 상수로 두면 순찰 경로와 갈라진다. 실제로 1단계에서
    //    MapLayout.LaneLoop의 기본 inset만 Scale을 태우고 여기(14f)를 안 고쳐서 58.3 대 14로
    //    44만큼 어긋났고, 적이 흙길 한참 안쪽을 걸었다. 이제 **양쪽 다 MapLayout.LaneTrackInset
    //    하나를 본다** — "순찰 경로와 같은 값"을 주석이 아니라 코드로 보장한다.
    //    값 자체는 원작 실측 비율이다(MapLayout.TrackInsetRatioX/Z 주석 참고).

    static void DecorateLane(Transform parent, MapLayout.Island lane)
    {
        // 흙길은 적이 실제로 도는 자리다 — 상점 줄을 뺀 필드에서만 잡는다.
        MapLayout.Island field = MapLayout.LaneField(lane);
        Vector2 trackInset = MapLayout.LaneTrackInset(lane);
        float halfX = field.size.x * 0.5f - trackInset.x;
        float halfZ = field.size.y * 0.5f - trackInset.y;
        float x = field.center.x;
        float z = field.center.y;
        float y = MapLayout.IslandTop + 0.04f;   // 잔디 위에 살짝 얹어 z-fighting을 피한다

        // 순찰 경로를 따라 도는 흙길 — 적이 실제로 지나는 자리다.
        BuildDecor(parent, $"{lane.name}_흙길_위", new Vector3(x, y, z + halfZ),
                   new Vector3(halfX * 2f + TrackWidth, 0.08f, TrackWidth), "dirt");
        BuildDecor(parent, $"{lane.name}_흙길_아래", new Vector3(x, y, z - halfZ),
                   new Vector3(halfX * 2f + TrackWidth, 0.08f, TrackWidth), "dirt");
        BuildDecor(parent, $"{lane.name}_흙길_왼", new Vector3(x - halfX, y, z),
                   new Vector3(TrackWidth, 0.08f, halfZ * 2f - TrackWidth), "dirt");
        BuildDecor(parent, $"{lane.name}_흙길_오른", new Vector3(x + halfX, y, z),
                   new Vector3(TrackWidth, 0.08f, halfZ * 2f - TrackWidth), "dirt");

    }

    const string SupportSkillFolder = "Assets/Data/SupportSkills";

    // 도움소 — 레인 안, 순찰 경로(inset 14)보다 안쪽인 섬 중앙에 둔다. 파괴 불가라 Collider만
    // 있으면 되고(TakeDamage가 아예 없다), EnemyDummy.Active/DestructibleGate.Active 어디에도
    // 등록되지 않으니 적 타겟팅 후보에도 자연히 들어가지 않는다.
    // 레인 안 상점 건물들. 순찰 흙길(TrackInset 14)보다 안쪽, 레인 가운데에 가로로 늘어선다.
    // 자리는 인덱스로 잡는다 — 0 도박소, 1 유닛강화소, 2 다른세계강화소, 3 영원함강화소,
    // 4 도움소, 5 해적단 판매 포탈, 6 공격타입강화소. 슬롯을 늘려도 LaneShopSlot의 간격
    // 계산이 strip 폭을 그대로 다시 나누므로 자리를 손으로 다시 잡을 필요가 없다.
    //
    // ⚠️ 6번(공격타입강화소)은 1번(유닛강화소)과 **다른 건물**이다 — 원작에 「강화소」라는
    // 이름의 건물이 최소 셋 있고(등급트랙 / 캐릭터 전용 보너스 / 공격타입 매트릭스),
    // 우리 1번이 그중 등급트랙이다. 하나로 합치지 말 것(뿌리 ㊴).
    // 2026-09-09: 7→8 (항해일지 추가, ITEM_SYSTEM_AUDIT_2026-09-09.md ③).
    // 이 값이 상점 사이 간격을 정한다 — 늘리면 기존 상점들이 조금씩 좁혀 선다.
    // 자리는 0 도박소 · 1 유닛강화 · 2 다른세계강화 · 3 영원함강화 · 4 도움소 ·
    //          5 해적단상점 · 6 공격타입강화 · 7 항해일지.
    const int LaneShopCount = 8;
    // 원작 비율 5단계(PM 지시 2026-09-23) — 실제 건물 모델이 있으면 StructureDresser.
    // DressLaneShop이 클릭 상자를 모델 크기로 다시 맞추므로 이 값은 최종 크기를 안 정한다.
    // 그래도 모델이 없을 때(자리표시 큐브)의 크기·판정이라 맵 배율과 같이 키운다.
    const float LaneShopSize = 9f * MapLayout.Scale;

    // 상점 줄은 필드와 벽 하나로 갈린다 — 적이 도는 곳과 내가 쓰는 곳이 눈으로 구분돼야 한다.
    // 새 유닛이 처음 서는 우리. 상점 줄 바로 위, 벽으로 둘러싸여 있고 위쪽만 트여 있다 —
    // 레인 한가운데에 소환하면 적 한복판에 나오고, 플레이어가 손쓸 새도 없이 맞는다.
    // 폭은 상수로 안 박는다 — LaneUnitPenRow가 주는 실제 줄 폭에서 계산한다. 레인 크기가
    // 나중에 또 바뀌어도(2026-09-02에 이미 한 번 1.5배 됐다) 하드코딩한 값만 어긋나는 걸 막는다.
    const float UnitPenEdgeMargin = 4f;   // 좌우 벽이 레인 가장자리에 딱 붙지 않게 남기는 여유
    const float UnitPenInset = 3f;    // 우리 줄 안에서 위아래로 남기는 여유

    // 칸막이 전용 두께. GateThickness(1.4)를 그대로 썼더니 칸 11개로는 유닛이 좁아 보인다고
    // 하셔서(2026-09-02) 얇게 뺐다 — GateThickness는 부스·문 등 다른 22곳이 같이 쓰는 공용
    // 값이라 여기서 낮추면 그쪽 전부가 얇아진다. 우리 칸막이만 따로 둔다.
    const float UnitPenPartitionThickness = 0.8f;

    static float ResolveUnitPenWidth(MapLayout.Island lane)
    {
        return MapLayout.LaneUnitPenRow(lane).size.x - UnitPenEdgeMargin * 2f;
    }

    // 레인끼리 좁은 바다 틈(현재 세로 5, 가로 7)으로 넘나드는 걸 막는다(사장님 지시, 2026-09-03).
    // 지상 유닛은 이미 그 틈(Sea 영역)을 못 건너지만, MovementAbility.Flying/WaterWalk는
    // UnitSpawner.ComputeAreaMask가 전체 영역을 허용해서 건널 수 있다 — 그걸 막는 게 목적이다.
    //
    // 세로 벽 하나(Lane1|Lane2, Lane3|Lane4 틈을 동시에), 가로 벽 하나(Lane1|Lane3, Lane2|Lane4
    // 틈을 동시에)로 십자 모양을 이룬다 — 왼쪽 열(Lane1/3)과 오른쪽 열(Lane2/4)의 x, 위쪽 행
    // (Lane1/2)과 아래쪽 행(Lane3/4)의 z가 각각 같아서 레인 4개 다 한 쌍의 벽으로 끝난다.
    // 두 벽이 가운데서 겹치는 것도 의도다 — 각자 자기 몫만 딱 맞게 지으면 네 레인이 만나는
    // 가운데 교차점에 아주 작은 빈틈이 남는다.
    //
    // 콜라이더는 평범한 솔리드 큐브(BuildWall)다 — 트리거가 아니라 실제 NavMesh 장애물이 되므로
    // areaMask와 무관하게 전부(지상·비행·수상보행) 막힌다. 순찰 경로(TrackInset 14, 라인 안쪽)
    // 와는 안 겹친다 — 벽은 레인 가장자리에서 겨우 0.5(margin 1의 절반)만 파고든다. 유닛 우리
    // 벽(가장자리에서 4 들어간 자리)·상점 칸(27.5 들어간 자리)에도 한참 못 미친다. 레인 사이에
    // 다리·통로는 없다(코드 전체에 그런 걸 찾지 못함) — 그래서 변 전체를 막아도 된다.
    const float InterLaneWallMargin = 1f;

    static void BuildInterLaneWalls(Transform parent)
    {
        MapLayout.Island topLeft = MapLayout.Lanes[0];
        MapLayout.Island topRight = MapLayout.Lanes[1];
        MapLayout.Island bottomLeft = MapLayout.Lanes[2];

        float leftColRightEdge = topLeft.center.x + topLeft.size.x * 0.5f;
        float rightColLeftEdge = topRight.center.x - topRight.size.x * 0.5f;
        float vWallX = (leftColRightEdge + rightColLeftEdge) * 0.5f;
        float vGap = rightColLeftEdge - leftColRightEdge;

        // 🔴 레인 섬은 6단계 이후 **필드만**이고, 앞치마(우리 줄 + 상점 줄)는 섬 밖 아래로
        //    LaneApronDepth만큼 더 뻗는다. 섬 아래변끼리로 간격을 재면 그 앞치마가
        //    「레인 사이 빈 땅」으로 잡혀서 벽이 통째로 덮어 버린다.
        //    2026-09-24에 실제로 그랬다 — 가로벽(두께 338.9)이 앞치마 199.3을 **100%** 삼켰다.
        //    벽은 렌더러만 떼고 **콜라이더를 남기므로**(BuildWall 주석) 보기 문제로 끝나지 않는다.
        //    NavMesh가 콜라이더로 구워져 **우리와 상점 줄 전체가 걸을 수 없는 땅**이 됐었다.
        //    설계(두께 = 레인 사이 간격)는 그대로다 — 기준선이 옮겨진 것이라 기준선을 고친다.
        float topRowBottomEdge = topLeft.center.y - topLeft.size.y * 0.5f - MapLayout.LaneApronDepth;
        float bottomRowTopEdge = bottomLeft.center.y + bottomLeft.size.y * 0.5f;
        float hWallZ = (topRowBottomEdge + bottomRowTopEdge) * 0.5f;
        float hGap = topRowBottomEdge - bottomRowTopEdge;

        float overallMinX = topLeft.center.x - topLeft.size.x * 0.5f;
        float overallMaxX = topRight.center.x + topRight.size.x * 0.5f;
        float overallMinZ = bottomLeft.center.y - bottomLeft.size.y * 0.5f;
        float overallMaxZ = topLeft.center.y + topLeft.size.y * 0.5f;

        BuildWall(parent, "레인간_세로벽",
            new Vector3(vWallX, MapLayout.IslandTop + WallHeight * 0.5f, (overallMinZ + overallMaxZ) * 0.5f),
            new Vector3(vGap + InterLaneWallMargin, WallHeight, overallMaxZ - overallMinZ));

        BuildWall(parent, "레인간_가로벽",
            new Vector3((overallMinX + overallMaxX) * 0.5f, MapLayout.IslandTop + WallHeight * 0.5f, hWallZ),
            new Vector3(overallMaxX - overallMinX, WallHeight, hGap + InterLaneWallMargin));
    }

    /// <summary>
    /// 레인 사이 벽이 바닥에서 차지하는 자리. <see cref="BuildInterLaneWalls"/>와 **같은 식**으로
    /// 낸다 — 겹침 검사가 벽을 보려면 자리가 필요한데, 따로 적으면 둘이 갈라져서 검사가
    /// 거짓말을 한다. 계산이 한 군데 더 필요해지면 이 함수를 쓰고 식을 베끼지 말 것.
    /// </summary>
    static IEnumerable<MapLayout.Island> InterLaneWallFootprints()
    {
        MapLayout.Island topLeft = MapLayout.Lanes[0];
        MapLayout.Island topRight = MapLayout.Lanes[1];
        MapLayout.Island bottomLeft = MapLayout.Lanes[2];

        float leftColRightEdge = topLeft.center.x + topLeft.size.x * 0.5f;
        float rightColLeftEdge = topRight.center.x - topRight.size.x * 0.5f;
        float topRowBottomEdge = topLeft.center.y - topLeft.size.y * 0.5f - MapLayout.LaneApronDepth;
        float bottomRowTopEdge = bottomLeft.center.y + bottomLeft.size.y * 0.5f;

        float overallMinX = topLeft.center.x - topLeft.size.x * 0.5f;
        float overallMaxX = topRight.center.x + topRight.size.x * 0.5f;
        float overallMinZ = bottomLeft.center.y - bottomLeft.size.y * 0.5f;
        float overallMaxZ = topLeft.center.y + topLeft.size.y * 0.5f;

        yield return new MapLayout.Island("레인간_세로벽",
            (leftColRightEdge + rightColLeftEdge) * 0.5f, (overallMinZ + overallMaxZ) * 0.5f,
            rightColLeftEdge - leftColRightEdge + InterLaneWallMargin, overallMaxZ - overallMinZ, "rock");

        yield return new MapLayout.Island("레인간_가로벽",
            (overallMinX + overallMaxX) * 0.5f, (topRowBottomEdge + bottomRowTopEdge) * 0.5f,
            overallMaxX - overallMinX, topRowBottomEdge - bottomRowTopEdge + InterLaneWallMargin, "rock");
    }

    static Transform BuildUnitPen(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        MapLayout.Island row = MapLayout.LaneUnitPenRow(lane);
        float unitPenWidth = ResolveUnitPenWidth(lane);
        float penDepth = row.size.y - UnitPenInset * 2f;
        float centerZ = row.center.y;
        float centerX = lane.center.x;
        float halfX = unitPenWidth * 0.5f;
        float halfZ = penDepth * 0.5f;

        BuildDecor(parent, $"{lane.name}_유닛우리_바닥",
            new Vector3(centerX, MapLayout.IslandTop + 0.05f, centerZ),
            new Vector3(unitPenWidth, 0.1f, penDepth), "dirt");

        // 좌·우·아래만 막는다. 위가 열려 있어야 플레이어가 유닛을 필드로 꺼낸다.
        BuildWall(parent, $"{lane.name}_유닛우리_왼벽",
            new Vector3(centerX - halfX, MapLayout.IslandTop + WallHeight * 0.5f, centerZ),
            new Vector3(GateThickness, WallHeight, penDepth + GateThickness));
        BuildWall(parent, $"{lane.name}_유닛우리_오른벽",
            new Vector3(centerX + halfX, MapLayout.IslandTop + WallHeight * 0.5f, centerZ),
            new Vector3(GateThickness, WallHeight, penDepth + GateThickness));
        BuildWall(parent, $"{lane.name}_유닛우리_아래벽",
            new Vector3(centerX, MapLayout.IslandTop + WallHeight * 0.5f, centerZ - halfZ),
            new Vector3(unitPenWidth, WallHeight, GateThickness));

        BuildUnitPenPartitions(parent, lane, unitPenWidth, penDepth, centerX, centerZ);

        GameObject anchor = new GameObject($"{lane.name}_유닛우리");
        anchor.transform.SetParent(parent, false);
        anchor.transform.position = new Vector3(centerX, MapLayout.IslandTop, centerZ);
        return anchor.transform;
    }

    // 원작처럼 우리 안을 기둥으로 칸칸이 나눈다. 칸 하나에 자리(LaneMarker.TakeSpawnPosition)
    // 하나가 정확히 가운데 오도록, 자리 간격(LaneMarker.ResolveSlotSpacing)의 배수 자리에만 세운다 —
    // 어긋나면 유닛이 기둥에 박히거나 기둥을 뚫고 서 있게 된다(PM 지시).
    // 칸 수는 LaneMarker.CompartmentCount로 고정이고, EndMarginCompartments는 이제 0이다 —
    // 우리 벽 자체가 1번·11번 칸의 바깥 경계 노릇을 하므로 칸막이는 칸 사이(CompartmentCount-1개)
    // 만 세운다(2026-09-03). 한때 바깥 경계까지 세운 적이 있었는데, 그때는 EndMarginCompartments가
    // 1이라 우리 벽이 그만큼 떨어져 있어서 양 끝 칸이 두 배로 넓어지는 문제가 났었다 — 지금은
    // 여백이 0이라 우리 벽이 정확히 그 자리에 있으므로 따로 세우면 오히려 겹친다.
    // LaneMarker가 런타임에 쓰는 것과 같은 상수·계산식을 그대로 쓴다 — 이 둘이 갈라지면 자리와
    // 칸이 어긋난다.
    static void BuildUnitPenPartitions(Transform parent, MapLayout.Island lane,
        float unitPenWidth, float penDepth, float centerX, float centerZ)
    {
        float spacing = LaneMarker.ResolveSlotSpacing(unitPenWidth);

        for (int column = 0; column < LaneMarker.CompartmentCount - 1; column++)
        {
            float boundaryX = (column - (LaneMarker.CompartmentCount - 1) * 0.5f + 0.5f) * spacing;

            BuildWall(parent, $"{lane.name}_유닛우리_칸막이{column}",
                new Vector3(centerX + boundaryX, MapLayout.IslandTop + WallHeight * 0.5f, centerZ),
                new Vector3(UnitPenPartitionThickness, WallHeight, penDepth + UnitPenPartitionThickness));
        }
    }

    static void BuildLaneShopStrip(Transform parent, MapLayout.Island lane)
    {
        MapLayout.Island strip = MapLayout.LaneShopStrip(lane);

        BuildDecor(parent, $"{lane.name}_상점바닥",
            new Vector3(strip.center.x, MapLayout.IslandTop + 0.05f, strip.center.y),
            new Vector3(strip.size.x - 2f, 0.1f, strip.size.y - 2f), "rock");

        BuildDecor(parent, $"{lane.name}_상점벽",
            new Vector3(strip.center.x, MapLayout.IslandTop + WallHeight * 0.5f,
                        strip.center.y + strip.size.y * 0.5f),
            new Vector3(strip.size.x, WallHeight, GateThickness), "rock");
    }

    // 레인 섬에 붙은 LaneMarker에 우리를 물려준다 — 포탈·도박소·조합이 전부 여기로 소환한다.
    static void WireUnitPen(GameObject laneObject, Transform pen, MapLayout.Island lane)
    {
        if (laneObject == null || pen == null) return;

        LaneMarker marker = laneObject.GetComponent<LaneMarker>();
        if (marker == null) return;

        SerializedObject so = new SerializedObject(marker);
        so.FindProperty("unitPen").objectReferenceValue = pen;
        so.ApplyModifiedProperties();

        marker.SetUnitRowWidth(ResolveUnitPenWidth(lane));
    }

    static Vector3 LaneShopSlot(MapLayout.Island lane, int slot)
    {
        MapLayout.Island strip = MapLayout.LaneShopStrip(lane);
        float step = strip.size.x / (LaneShopCount + 1);
        return new Vector3(strip.center.x - strip.size.x * 0.5f + step * (slot + 1),
                           MapLayout.IslandTop + 1.5f, strip.center.y);
    }

    static GameObject BuildLaneShopBody(Transform parent, string name, Vector3 at, int laneIndex, string surface)
    {
        GameObject shop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shop.name = name;
        shop.transform.SetParent(parent, false);
        shop.transform.position = at;
        shop.transform.localScale = new Vector3(LaneShopSize, 3f * MapLayout.Scale, LaneShopSize * 0.7f);
        Paint(shop, surface, LaneShopSize, LaneShopSize * 0.7f);   // 임시 — 전용 모델이 없어 기존 텍스처를 쓴다

        shop.AddComponent<Selectable>();
        shop.AddComponent<OwnedByPlayer>().SetOwner(laneIndex);
        // Blender 상점 건물을 입히고 클릭 상자를 건물 크기로 늘린다. 모델이 없으면 이 상자가 그대로 보인다.
        StructureDresser.DressLaneShop(shop);
        return shop;
    }

    // 도박소는 0번 자리다(사장님이 준 원작 순서: 도박소·유닛강화소·다른세계 강화소·영원함 강화소·도움소).
    static void BuildGamblingShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        GameObject shop = BuildLaneShopBody(parent, $"{lane.name}_도박소",
            LaneShopSlot(lane, 0), laneIndex, "event");

        GamblingShop gambling = shop.AddComponent<GamblingShop>();
        SerializedObject so = new SerializedObject(gambling);

        // 슬롯 인덱스가 곧 하단 칸 자리다 — 순서가 화면 배치를 정한다.
        FillAssetList(so.FindProperty("moneyOptions"), "10엔 도박", "500엔 도박");
        FillAssetList(so.FindProperty("unitOptions"), "하급도박", "중급도박", "고급도박", "다른세계 도박");

        so.FindProperty("gachaTable").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/MainGachaTable.asset");
        so.FindProperty("unitSpawner").objectReferenceValue =
            Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);
        so.ApplyModifiedProperties();
    }

    static void FillAssetList(SerializedProperty list, params string[] optionNames)
    {
        list.ClearArray();

        for (int i = 0; i < optionNames.Length; i++)
        {
            GamblingOptionData option = AssetDatabase.LoadAssetAtPath<GamblingOptionData>(
                $"{GamblingFolder}/Gambling_{optionNames[i]}.asset");

            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = option;

            if (option == null)
                Debug.LogWarning($"[맵] 도박 옵션 에셋을 찾지 못했습니다: Gambling_{optionNames[i]}");
        }
    }

    const string GamblingFolder = "Assets/Data/Gambling";

    // 유닛강화소는 1번 자리다(사장님이 준 원작 순서: 도박소·유닛강화소·다른세계 강화소·영원함 강화소·도움소).
    // 슬롯 순서가 화면 배치를 정하므로, 폴더 전체를 훑어 이름순 정렬하지 않고(한글 정렬은 의도한
    // 등급 순서와 안 맞는다) 여기서 순서를 명시한다.
    // 2026-09-05: "히든 강화" 추가(사장님 결정 05번 — 원작 연구소 8종 중 유일하게 빠져 있던
    // 등급). 8개→9개.
    static readonly string[] UnitUpgradeTrackNames =
    {
        "흔함·안흔함 강화", "특별함 강화", "희귀함 강화", "히든 강화", "전설적인 강화",
        "제한됨 강화", "초월 강화", "불멸 강화", "랜덤유닛 강화",
    };

    const string UnitUpgradeFolder = "Assets/Data/UnitUpgrades";
    const string AttackTypeUpgradeFolder = "Assets/Data/AttackTypeUpgrades";

    static void BuildUnitUpgradeShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        BuildUpgradeShop(parent, lane, laneIndex, "유닛강화소", 1, "display", UnitUpgradeTrackNames);
    }

    // 다른세계·영원함도 같은 상점이다 — 트랙이 하나뿐인 것만 다르다.
    static void BuildOtherWorldUpgradeShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        BuildUpgradeShop(parent, lane, laneIndex, "다른세계강화소", 2, "gacha",
                         new[] { "다른세계 강화" });
    }

    static void BuildEternalUpgradeShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        BuildUpgradeShop(parent, lane, laneIndex, "영원함강화소", 3, "combine",
                         new[] { "영원함 강화" });
    }

    // 공격타입강화소(원작 「강화소 3」) — 유닛강화소(강화소 1)와 트랙 타입 자체가 다르므로
    // BuildUpgradeShop을 재사용하지 않고 따로 짓는다. 마스터버튼 4개(일반·공성·관통·패기)를
    // 판다. 자산 폴더도 분리돼 있다 — 스키마가 달라 같은 폴더에 두면 검사기가 오탐을 낸다.
    static void BuildAttackTypeUpgradeShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        GameObject shop = BuildLaneShopBody(parent, $"{lane.name}_공격타입강화소",
            LaneShopSlot(lane, 6), laneIndex, "display");

        AttackTypeUpgradeShop attackShop = shop.AddComponent<AttackTypeUpgradeShop>();
        SerializedObject so = new SerializedObject(attackShop);
        SerializedProperty tracksProp = so.FindProperty("tracks");

        // 폴더를 통째로 스캔한다 — 트랙이 늘어도 이 목록을 손보지 않게. 이름 순으로 고정해
        // 맵을 다시 생성해도 슬롯 순서가 안 흔들린다.
        List<AttackTypeUpgradeTrackData> tracks = AssetDatabase
            .FindAssets("t:AttackTypeUpgradeTrackData", new[] { AttackTypeUpgradeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<AttackTypeUpgradeTrackData>)
            .Where(track => track != null)
            .ToList();

        if (tracks.Count == 0)
            Debug.LogWarning($"[맵] 공격타입 강화 트랙 에셋을 찾지 못했습니다: {AttackTypeUpgradeFolder}");

        tracksProp.ClearArray();
        for (int i = 0; i < tracks.Count; i++)
        {
            tracksProp.InsertArrayElementAtIndex(i);
            tracksProp.GetArrayElementAtIndex(i).objectReferenceValue = tracks[i];
        }

        so.ApplyModifiedProperties();
    }

    static void BuildUpgradeShop(Transform parent, MapLayout.Island lane, int laneIndex,
                                 string name, int slot, string tint, string[] trackNames)
    {
        GameObject shop = BuildLaneShopBody(parent, $"{lane.name}_{name}",
            LaneShopSlot(lane, slot), laneIndex, tint);

        UnitUpgradeShop upgradeShop = shop.AddComponent<UnitUpgradeShop>();
        SerializedObject so = new SerializedObject(upgradeShop);
        SerializedProperty tracksProp = so.FindProperty("tracks");

        tracksProp.ClearArray();
        for (int i = 0; i < trackNames.Length; i++)
        {
            UnitUpgradeTrackData track = AssetDatabase.LoadAssetAtPath<UnitUpgradeTrackData>(
                $"{UnitUpgradeFolder}/UnitUpgrade_{trackNames[i]}.asset");

            tracksProp.InsertArrayElementAtIndex(i);
            tracksProp.GetArrayElementAtIndex(i).objectReferenceValue = track;

            if (track == null)
                Debug.LogWarning($"[맵] 강화 트랙 에셋을 찾지 못했습니다: UnitUpgrade_{trackNames[i]}");
        }

        so.ApplyModifiedProperties();
    }

    static void BuildSupportShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        GameObject shop = BuildLaneShopBody(parent, $"{lane.name}_도움소",
            LaneShopSlot(lane, 4), laneIndex, "warehouse");
        SupportShop supportShop = shop.AddComponent<SupportShop>();
        SerializedObject so = new SerializedObject(supportShop);
        SerializedProperty skillsProp = so.FindProperty("skills");

        List<SupportSkillData> skills = AssetDatabase
            .FindAssets("t:SupportSkillData", new[] { SupportSkillFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<SupportSkillData>)
            .Where(skill => skill != null)
            .ToList();

        skillsProp.ClearArray();
        for (int i = 0; i < skills.Count; i++)
        {
            skillsProp.InsertArrayElementAtIndex(i);
            skillsProp.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        }

        so.ApplyModifiedProperties();
    }

    const string PirateQuestFolder = "Assets/Data/PirateQuests";

    // 퀘스트 목록을 한 번만 훑어서 레인마다의 상점(quests)에 그대로 물려준다. 상점은
    // 레인(=플레이어)별 독립 재고를 스스로 들고 있어서(PirateQuestShop.Awake) 여기서는
    // 목록만 꽂으면 된다 — 매니저 쪽엔 더 이상 이 목록을 안 물린다(아래 참고).
    static List<PirateQuestData> LoadPirateQuests()
    {
        return AssetDatabase.FindAssets("t:PirateQuestData", new[] { PirateQuestFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<PirateQuestData>)
            .Where(quest => quest != null)
            .ToList();
    }

    // 해적단 퀘스트 상점 — 레인당 하나. 2026-09-05 2차 정정(사장님 발견 + PM 재조사)으로
    // 트리거 포탈(CreatePortalObject)에서 클릭형 상점(BuildLaneShopBody)으로 바뀌었다 —
    // 원작이 "유닛이 걸어 들어가 판다"가 아니라 "상점 h07A에서 사는 순간 발동"
    // (`GetSoldUnit()`)이라, 이번엔 반대로 클릭형이 정답이다(다른 포탈들과 헷갈리지 말 것 —
    // 저건 여전히 몸으로 들어가야 맞다). 슬롯 위치(5번)는 기존 포탈 자리를 그대로 쓴다 —
    // 배치는 사장님 몫이라 이번 정정과 무관하게 안 바꿨다.
    static void BuildPirateQuestShop(Transform parent, MapLayout.Island lane, int laneIndex,
                                     List<PirateQuestData> quests)
    {
        GameObject shop = BuildLaneShopBody(parent, $"{lane.name}_해적단상점",
            LaneShopSlot(lane, 5), laneIndex, "event");

        PirateQuestShop questShop = shop.AddComponent<PirateQuestShop>();
        SerializedObject so = new SerializedObject(questShop);
        SerializedProperty list = so.FindProperty("quests");

        list.ClearArray();
        for (int i = 0; i < quests.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
        }

        so.ApplyModifiedProperties();
    }

    // 항해일지(원작 H0C4) — 7번 자리. 원작은 판 시작 시 4명 기지에 하나씩 놓이는 건물이라
    // (CreateUnitsForPlayer0~3의 CreateUnit(Player(N),'H0C4',...)), 우리도 레인당 하나다.
    //
    // 🔴 이걸 안 지어서 아이템 시스템이 통째로 안 돌았다 — 메타몽(h0BS) 유닛도 있고
    //    도박 풀 배선도 정확한데 **그 유닛을 주는 경로가 프로젝트 전체에 0건**이었다.
    //    (ITEM_SYSTEM_AUDIT_2026-09-09.md ③) 재고는 이 상점이 아니라 ItemGambleState가
    //    들고 RoundManager가 6·9라운드에 채운다 — 원작도 같은 자리다.
    static void BuildVoyageLogShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        GameObject shop = BuildLaneShopBody(parent, $"{lane.name}_항해일지",
            LaneShopSlot(lane, 7), laneIndex, "gacha");

        VoyageLogShop voyageLog = shop.AddComponent<VoyageLogShop>();
        SerializedObject so = new SerializedObject(voyageLog);

        UnitData metamong = AssetDatabase.LoadAssetAtPath<UnitData>(VoyageLogUnitPath);
        if (metamong == null)
            Debug.LogWarning($"[맵] 항해일지가 팔 유닛을 찾지 못했습니다: {VoyageLogUnitPath}");

        so.FindProperty("gambleUnit").objectReferenceValue = metamong;
        so.FindProperty("unitSpawner").objectReferenceValue =
            Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);
        so.ApplyModifiedProperties();
    }

    // 원작 w3u 원문 그대로 ugol=5000·ulum=3 — 값은 VoyageLogShop의 기본값에 있다.
    const string VoyageLogUnitPath = "Assets/Data/Units/Special/Unit_메타몽_h0BS.asset";

    // 해적단류 퀘스트 매니저 — 씬 전체에 하나, 퀘스트 목록은 안 들고 있다(그건 이제 상점
    // 쪽 몫). 미니보스 소환·제한시간 판정·성공/실패 보상만 한다 — 빈 껍데기가 아니다.
    static string BuildPirateQuestManager()
    {
        PirateQuestManager manager = Object.FindFirstObjectByType<PirateQuestManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject managerObject = new GameObject("PirateQuestManager");
            manager = managerObject.AddComponent<PirateQuestManager>();
        }

        return "\n해적단 퀘스트 매니저 확인.";
    }

    const string ChatUnlockFolder = "Assets/Data/ChatUnlocks";
    const string HiddenCombineFolder = "Assets/Data/HiddenCombines";

    // 초월·불멸·영원·니카 채팅 코드 해금 — PirateQuestManager와 같은 모양(플레이어별이
    // 아니라 씬 전체에 하나, playerId는 메서드 인자로만 받는다 — ChatUnlockManager.cs 참고).
    // TryUnlock(playerId, data)이 판정·지급 전부이고 여기선 목록·스포너만 꽂는다.
    static string BuildChatUnlockManager()
    {
        ChatUnlockManager manager = Object.FindFirstObjectByType<ChatUnlockManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject managerObject = new GameObject("ChatUnlockManager");
            manager = managerObject.AddComponent<ChatUnlockManager>();
        }

        List<ChatUnlockData> unlocks = AssetDatabase.FindAssets("t:ChatUnlockData", new[] { ChatUnlockFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<ChatUnlockData>)
            .Where(unlock => unlock != null)
            .ToList();

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("unlocks");

        list.ClearArray();
        for (int i = 0; i < unlocks.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = unlocks[i];
        }

        so.FindProperty("unitSpawner").objectReferenceValue =
            Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);
        so.ApplyModifiedProperties();

        return $"\n채팅 코드 해금(초월·불멸·영원·니카): {unlocks.Count}개 연결." +
               (unlocks.Count == 0 ? $"\n  ⚠️ {ChatUnlockFolder}에서 ChatUnlockData를 하나도 못 찾았습니다(사장님 콘텐츠 배정 전이면 정상)." : "");
    }

    // 히든 등급 23종 재료 조합 — ChatUnlockManager와 별개 진입점(HiddenCombineManager.cs 참고,
    // CombineSystem과도 별개). 마찬가지로 씬 전체에 하나다.
    static string BuildHiddenCombineManager()
    {
        HiddenCombineManager manager = Object.FindFirstObjectByType<HiddenCombineManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject managerObject = new GameObject("HiddenCombineManager");
            manager = managerObject.AddComponent<HiddenCombineManager>();
        }

        List<HiddenCombineData> combines = AssetDatabase.FindAssets("t:HiddenCombineData", new[] { HiddenCombineFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<HiddenCombineData>)
            .Where(combine => combine != null)
            .ToList();

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("combines");

        list.ClearArray();
        for (int i = 0; i < combines.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = combines[i];
        }

        so.FindProperty("unitSpawner").objectReferenceValue =
            Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);
        so.ApplyModifiedProperties();

        return $"\n히든 조합: {combines.Count}개 연결." +
               (combines.Count == 0 ? $"\n  ⚠️ {HiddenCombineFolder}에서 HiddenCombineData를 하나도 못 찾았습니다(사장님 콘텐츠 배정 전이면 정상)." : "");
    }

    // 채팅 코드·히든 조합 통합 입력창(사장님 지시 2026-09-05) — 반드시 위 두 매니저를 먼저
    // 만든 뒤에 불러야 한다(참조를 그 결과에서 찾는다).
    static string BuildGameChatBox()
    {
        GameChatBox chatBox = Object.FindFirstObjectByType<GameChatBox>(FindObjectsInactive.Include);
        if (chatBox == null)
        {
            GameObject chatBoxObject = new GameObject("GameChatBox");
            chatBox = chatBoxObject.AddComponent<GameChatBox>();
        }

        SerializedObject so = new SerializedObject(chatBox);
        so.FindProperty("chatUnlockManager").objectReferenceValue =
            Object.FindFirstObjectByType<ChatUnlockManager>(FindObjectsInactive.Include);
        so.FindProperty("hiddenCombineManager").objectReferenceValue =
            Object.FindFirstObjectByType<HiddenCombineManager>(FindObjectsInactive.Include);
        so.ApplyModifiedProperties();

        return "\n채팅 입력창(코드+히든 조합 통합)을 연결했습니다(엔터로 열기, Esc로 닫기).";
    }

    static void BuildDecor(Transform parent, string name, Vector3 position, Vector3 scale,
                           string surface, PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject obj = GameObject.CreatePrimitive(shape);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        Paint(obj, surface, scale.x, scale.z);
        Object.DestroyImmediate(obj.GetComponent<Collider>());
    }

    static List<WaypointPath> BuildLanePaths(Transform parent)
    {
        List<WaypointPath> paths = new List<WaypointPath>();

        for (int i = 0; i < MapLayout.Lanes.Length; i++)
        {
            MapLayout.Island lane = MapLayout.Lanes[i];
            GameObject pathObj = new GameObject($"{lane.name}_Path", typeof(WaypointPath));
            pathObj.transform.SetParent(parent, false);

            Vector3[] corners = MapLayout.LaneLoop(lane);
            Transform[] points = new Transform[corners.Length];
            for (int c = 0; c < corners.Length; c++)
            {
                GameObject point = new GameObject($"P{c}");
                point.transform.SetParent(pathObj.transform, false);
                point.transform.position = corners[c];
                points[c] = point.transform;
            }

            WaypointPath path = pathObj.GetComponent<WaypointPath>();
            SerializedObject so = new SerializedObject(path);
            SerializedProperty list = so.FindProperty("points");
            list.ClearArray();
            for (int c = 0; c < points.Length; c++)
            {
                list.InsertArrayElementAtIndex(c);
                list.GetArrayElementAtIndex(c).objectReferenceValue = points[c];
            }
            so.ApplyModifiedProperties();

            paths.Add(path);
        }

        return paths;
    }

    // 조합식 표. 원작처럼 한 줄이 조합식 하나다: 재료들 → 결과.
    // 등급별로 유닛만 모아두면 "무엇으로 만드는지"를 알 수 없어서 표의 역할을 못 한다.
    // 전시용 자리 표시 기둥 (뽑기 섬 전시, 초월·불멸 전시에서 쓴다).
    // 조합식 표를 갈아엎을 때 같이 지워져서 컴파일이 깨졌었다 — 쓰는 곳이 여러 군데다.
    //
    // (2026-09-23, 원작 비율 4단계, PM 지시) 섬(Zones)은 이미 Scale(4.167)을 탔는데 이 아래
    // 값들은 그대로라 표·인형이 섬 안에서 성기게 떠 있었다. 원작 소스가 있는 셋(SlotSpacing·
    // RecipeSlot·RecipeRowHeight)은 그 값÷Scale로 각각 새로 계산했고(축마다 원작 원본이
    // 달라 비율이 서로 다르다 — 레인 때와 같은 이유), 나머지는 "한 축만 키우면 글자·아이콘
    // 비례가 깨진다"는 PM 지시대로 SlotSpacing의 배율(약 ×10.24)을 그대로 물려받는다.
    // 🔴 2026-09-23 정정: 4단계에서 "나머지는 전부 ×10.24"로 올렸더니 **원래 비율이 뒤집혔다.**
    //    원작 소스가 있는 셋(SlotSpacing 256÷Scale · RecipeSlot 64÷Scale · RecipeRowHeight)은
    //    배율이 서로 다른데(10.23 / 3.42 / 4.73), 나머지를 전부 SlotSpacing 배율로 올린 탓이다.
    //    증상: 비용 아이콘이 유닛 칸의 **2.13배**가 됐다(원래 0.71배) — 사장님이 뽑기섬에서
    //    "거대한 공"으로 보신 게 이것이고, 조합식 표에서도 같이 커져 있었다.
    //    규칙을 바로잡는다: **각 상수는 "자기가 비율로 매달린 대상"의 배율을 따른다.**
    //      · 유닛 칸(RecipeSlot)에 매달린 것 → ×3.422
    //      · 줄 높이(RecipeRowHeight)에 매달린 것 → ×4.733
    //      · 전시 격자(SlotSpacing)에 매달린 것 → ×10.233
    /// <summary>
    /// 뽑기섬 전시 격자 한 줄의 칸 수 — **6칸**(사장님 지시 2026-09-23: "랜덤유닛들 4개씩
    /// 배치돼 있던데 간격 줄여서 6개씩 배치하자. 자리를 너무 잡아먹는다").
    /// 랜덤유닛 14종 → 3줄, 다른세계 9종 → 2줄이 된다(예전 4칸일 땐 4줄·3줄이었다).
    /// ⚠️ 원작 흔함 줄은 9칸(<c>1com1</c>~<c>1com9</c>, LaneMarker.CompartmentCount가 그 값이다)
    /// 이지만, 여기는 원작에 대응물이 없는 **우리 전시 칸**이고 사장님이 6으로 정하셨다.
    /// </summary>
    const int DisplayColumns = 6;

    /// <summary>
    /// 전시 격자 칸 간격. **조합표 SlotSpacing(61.4)과 분리한다**(PM 지시 2026-09-23) —
    /// 그 값은 원작 조합 슬롯 간격 256÷Scale이라 전시 칸에는 과하고, 줄당 4칸밖에 안 나와
    /// 14종이 4줄로 흩어졌다.
    ///
    /// 16인 근거(2026-09-23 재조정): 사장님이 「선택위습이랑 랜덤, 다른세계 유닛 크기도
    /// 키워줘」라고 하셔서 **씬 파일에서 직접 재 봤더니 크기는 이미 같았다** — 같은 흔함
    /// 유닛이 뽑기섬과 조합판에 동시에 서는데 두 곳의 배율이 정확히 1.00배였다.
    /// 작아 보인 건 크기가 아니라 **여백**이었다:
    /// <code>
    ///   받침(11.7)이 칸을 채우는 비율 — 조합판 칸(15.4) 76% · 뽑기섬(28) 42%
    /// </code>
    /// 그래서 키를 올리는 대신 칸을 조합판과 같은 밀도로 좁힌다(16 → 채움 73%).
    /// 키를 올리면 「인형은 전부 레인 유닛과 같은 키」라는 사장님이 세우신 규칙이 깨진다.
    /// 6칸 × 16 = 96으로 전시 칸 폭(약 281)에 그대로 들어간다.
    /// </summary>
    const float DisplaySlotSpacing = 16f;

    const float SlotSpacing = 61.4f;    // 원작 조합 슬롯 간격 256 ÷ Scale
    const float SlotSize = 30.7f;       // 자리표시 큐브(전시 격자 기준) 3.0 × 10.233
    const float SlotHeight = 34.79f;    // 3.4 × 10.233
    const float PedestalDiameter = 6f;         // 받침_불멸 지름·받침_초월 모서리 지름(Blender 규격) — ⚠️ 모델 실측값, Scale 안 탄다
    const float CombinePedestalWidth = 5.98f;  // 받침_조합 한 변 — 위와 같은 이유로 그대로

    /// <summary>
    /// 보여주기용 인형(조합표 칸·초월/불멸 전시·뽑기섬 흔함 선택)의 키.
    /// **레인에 실제로 서는 유닛과 같은 키**를 쓴다(사장님 지시 2026-09-23:
    /// "유닛크기 전부 선택위습 고르는 유닛크기에 맞춰줄래").
    ///
    /// 전에는 자리마다 기준이 달라서 같은 유닛이 화면마다 다른 크기로 섰다 —
    /// 흔함 선택 51(섬 폭÷칸수×0.9) · 초월/불멸 전시 98(SlotSpacing×1.6) ·
    /// 조합표 칸 13(RecipeRowHeight×0.9×축소율). 전부 칸 크기에서 유도한 값이라
    /// 2단계에서 맵이 4.167배가 되자 같이 튀었다. 유닛 키는 맵 배율을 안 타므로
    /// (ArtBinder.UnitHeight가 기준자) 인형도 그 값 하나로 고정한다.
    /// </summary>
    const float DisplayFigureHeight = ArtBinder.UnitHeight;

    /// <summary>
    /// 인형이 올라서는 받침의 지름(게임 단위). **칸 간격이 아니라 인형 키에서 유도한다**
    /// (PM 지시 2026-09-23: "받침은 인형이 올라서는 판이지 주인공이 아니다").
    ///
    /// 예전에는 받침 배율이 칸 간격÷받침지름이라, 칸이 61.4로 커지자 받침_초월이 배율 9.21
    /// (지름 55)까지 부풀어 키 20짜리 인형보다 훨씬 커졌다. 이제 칸이 아무리 넓어져도
    /// 받침은 인형에 붙어 있는다.
    ///
    /// 0.39 = 받침 지름이 인형 어깨폭의 1.5~2배가 되는 값. 사람 어깨폭은 키의 0.25~0.28쯤이라
    /// 키 20이면 5.0~5.6이고, 지름 7.8은 그 1.4~1.56배다. 받침_초월(모델 지름 6) 기준 배율
    /// 1.3에 해당한다. **모델 실측 규격(PedestalDiameter 6·CombinePedestalWidth 5.98)은
    /// 그대로 두고 여기서만 배율을 만든다** — 규격을 바꾸면 칸 폭 계산이 어긋난다.
    /// 화면을 보고 조정할 값이다(PM이 맵 생성 뒤 확정).
    /// </summary>
    const float PedestalDiameterPerFigureHeight = 0.39f;
    const float PedestalWidth = DisplayFigureHeight * PedestalDiameterPerFigureHeight;   // 7.8

    // 등급이 바뀔 때 두는 벽 자리. 줄 하나보다 조금 더 벌려 "여기서 등급이 바뀐다"가 읽히게 한다.
    const float GradeWallGap = RecipeRowHeight * 1.3f;   // 60.1

    /// <summary>
    /// 조합식 표의 열 수. 169줄을 이 수로 **고르게 나눠** 담는다(BuildCombineColumns 참고).
    /// 6인 이유: 5열이면 세로가 1737로 늘고, 7열이면 가로가 1285가 되어 「너비가 너무
    /// 길어지는 느낌」(사장님 2026-09-23)으로 되돌아간다. 6열이 가로 1119·세로 1460으로
    /// 둘 다 만족하는 자리다.
    /// </summary>
    // 사장님이 직접 정하신 열 수(2026-09-24: 9열 → 흔함 열이 앞에 붙어 **10열**).
    // 아래 `spread` 표와 **반드시 같아야 한다** — 표에 10열을 쓰는데 여기가 9면 마지막 열이
    // 통째로 사라진다. `neededColumns` 검사가 그걸 잡지만, 걸리기 전에 같이 올리는 게 맞다.
    const int CombineTableColumns = 10;
    const float RecipeSlot = 15.4f;     // 유닛 한 칸. 원작 슬롯 한 변 64 ÷ Scale
    // ── 조합식 표 간격 (2026-09-23 재설계) ────────────────────────────────
    // 사장님 「조합판도 너무 붙어있으니깐 답답한 느낌이든다」.
    //
    // ⚠️ **이 간격에는 원작 근거가 없다.** 원작에 조합식 표 자체가 없기 때문이다 —
    //    원문(war3map_new.j)의 조합 관련 rect는 `johab1`(256×160, 유닛을 끌어다 놓는
    //    조합소) 하나뿐이고 표·목록류 rect는 0건이다. 표는 우리가 만든 것이다.
    //    한때 "원작 256 ÷ 64 = 4배"를 근거로 삼으려 했으나, 그 256/64는 `1com1~9`
    //    (흔함 유닛이 **서는 줄**, 1comZone)의 칸 피치라 **축이 다르다**
    //    (war3map_new.j:3217~3225에서 확인). 다른 축의 숫자를 근거로 쓰면 안 된다.
    //    그래서 아래는 **읽기 편함을 위한 우리 설계 판단**이고, 그렇게 적어 둔다.
    //
    // 규칙: **재료 간격 = 칸 한 변**("칸 하나 띄우고 칸"). 피치÷칸이 1.31 → 2.00이 된다.
    //       칸 크기를 바꾸면 간격이 따라오므로 손으로 박은 수가 또 어긋나는 일이 없다.
    //       가로는 이만큼만 벌린다 — 사장님이 앞서 「너비가 너무 길어지는 느낌」이라 하셔서
    //       열 수(6)를 그대로 둔 채 여백만 준다. 답답함은 **세로로** 푼다(줄 높이·등급 벽).
    const float RecipeGap = RecipeSlot;          // 15.4
    const float RecipeArrowGap = 13.689f; // 재료 묶음과 결과 사이. 4.0 × 3.422
    // 줄 높이 = 칸의 3배. 위 "원작 근거 없음"이 여기에도 그대로 적용된다 —
    // 예전 값 28.4는 "원작 조합표 줄 간격"이라 적혀 있었지만 원작에 그 표가 없다.
    const float RecipeRowHeight = RecipeSlot * 3f;   // 46.2
    const float RecipeSlotHeight = 10.951f; // 3.2 × 3.422

    // 조합 비용(코인·목재·행운토큰)을 줄 왼쪽에 세우는 아이콘.
    // 재료 칸보다 작게 둬야 "이건 유닛이 아니라 자원"으로 읽힌다.
    const float CostSlot = 10.951f;    // 3.2 × 3.422 — 유닛 칸(15.4)의 0.71배, 반드시 더 작아야 한다
    const float CostGap = 4.107f;      // 1.2 × 3.422
    const float CostBlockGap = 6.844f; // 비용 묶음과 첫 재료 사이. 2.0 × 3.422
    const float ColumnPad = 6.844f;    // 열 바닥판 좌우 여백 — 열 사이 벽이 이 안에 선다. 2.0 × 3.422

    // 조합표 가로 축소율. 열을 자연 폭으로 늘어놓으면 섬 폭(274)을 50 넘겨서
    // 양쪽으로 25씩 삐져나온다(2026-09-06 사장님 스크린샷). 자연 폭을 먼저 재고
    // 섬에 맞는 비율을 여기 넣은 뒤 다시 배치한다. 1이면 축소 없음.
    // ⚠️ 세로(RecipeRowHeight)는 건드리지 않는다 — 넘치는 건 가로뿐이고,
    //    세로까지 줄이면 줄 간격이 좁아져 오히려 읽기 나빠진다.
    static float RecipeScale = 1f;

    static float SlotW  => RecipeSlot * RecipeScale;
    static float GapW   => RecipeGap * RecipeScale;
    static float ArrowW => RecipeArrowGap * RecipeScale;
    static float PadW   => ColumnPad * RecipeScale;
    static float CostW  => CostSlot * RecipeScale;
    static float CostGapW => CostGap * RecipeScale;
    static float CostBlockGapW => CostBlockGap * RecipeScale;

    static string BuildCombineColumns(GameObject table)
    {
        if (table == null) return "";

        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "CombineTable");
        Transform parent = table.transform.parent;

        // 한 등급이 42행까지 가면 세로로 너무 길어 읽히지 않는다. 25행에서 끊는다.
        // 그리고 짧은 등급끼리는 한 열에 위아래로 쌓되 사이에 벽을 세운다 —
        // 제한됨 9행이 열 하나를 통째로 쓰면 옆이 비어 보인다.
        List<List<(UnitGrade grade, List<CombineRecipe> chunk)>> columns =
            new List<List<(UnitGrade, List<CombineRecipe>)>>();

        // 열마다 「유닛만 세우는 열」인지. 흔함 열은 여기에 유닛 목록이 들어오고 columns의
        // 같은 자리는 비어 있다. 두 배열은 **항상 같은 길이로 나란히 움직인다** — 아래
        // 압축 루프에서 한쪽만 넣으면 열이 어긋나므로 반드시 짝으로 Add한다.
        List<List<UnitData>> columnUnits = new List<List<UnitData>>();

        // 🔴 2026-09-23 사장님: "오른쪽 보면 밑에 너무 비잖아? 3번째 열로 본다면 그 밑에 바로
        //    희귀함 와도 됨. 너비가 너무 길어지는 느낌이라." → 같은 날 다시: 6열 중 **마지막
        //    열만 15행**(다른 열은 31행)이라 그 아래가 또 비었다.
        //
        // 규칙이 두 번 바뀌었다. 왜 지금 모양이 됐는지 순서대로 남긴다:
        //  ① 등급을 25줄 덩어리로 자르고 쪼개진 등급은 제 열을 독점 → 자투리가 열을 혼자 써서
        //     아래가 텅 빔. 세로가 남는데 옆으로만 길어졌다.
        //  ② 한 열을 **용량까지 꽉 채우고** 다음 열로 → 폭은 잡혔지만 나머지가 전부 마지막
        //     열로 몰려, 그 열만 44%만 차는 같은 증상이 작게 재발했다.
        //  ③ (지금) **열마다 목표 행수를 먼저 정해 고르게 나눈다.** 169줄 ÷ 6열 = 28.17이라
        //     나머지 1을 **앞 열 하나에만** 준다(29·28·28·28·28·28). 전부 29로 올림하면
        //     29×5 + 24가 되어 마지막 열이 또 짧아진다 — 고치려던 문제를 작게 되풀이하는 셈이다.
        //     결과: 가장 얕은 열이 가장 깊은 열의 48% → **92%**.
        //  ④ (2026-09-24, 지금) **사장님이 열마다 어느 등급인지를 직접 정하셨다.**
        //     「1열 안흔함 2열 특별함 … 2,3열 특별함 4,5열 희귀함 6,7열 전설 7열 전설 밑에
        //      제한됨 8열 히든」. 그래서 ③의 「고르게 나누기」는 더 이상 안 쓴다 —
        //     **지시가 배정을 정하고, 코드는 등급 안에서 행만 나눈다.**
        //     ⚠️ ①②③을 지우지 않는 이유: 이 배정이 **결함을 고쳐서 바뀐 게 아니라 지시로
        //        바뀐 것**이라, 다음에 자동 분배로 돌아갈 일이 생기면 ①②③이 그대로 값한다.
        //
        // ⚠️ "행수 대신 **깊이**로 고르게 자르기"도 시도했다가 버렸다. 등급 구분벽이 붙는 자리가
        //    열마다 달라서, 깊이로 끊으면 경계가 어긋나 **7열째로 2행이 새어 나간다**(그 열 깊이
        //    139 = 10%). 다음 사람이 같은 길을 시도할 만해서 적어 둔다 — 자르는 기준은 행수다.
        //
        // 열 수는 섬 깊이가 아니라 이 목표 행수가 정한다. 등급 경계에는 여전히 구분벽이 서므로
        // 한 열에 "특별함 17줄 + 희귀함 11줄"처럼 이어 담아도 어디서 바뀌는지 읽힌다.
        int totalRecipes = 0;
        List<(UnitGrade grade, List<CombineRecipe> recipes)> loaded =
            new List<(UnitGrade, List<CombineRecipe>)>();
        foreach (UnitGrade grade in MapLayout.CombineTableGrades)
        {
            List<CombineRecipe> recipes = LoadRecipesProducing(grade);
            loaded.Add((grade, recipes));
            totalRecipes += recipes.Count;
        }

        // 등급 → 어느 열에 몇 개로 나눠 담을지. 사장님 지시를 그대로 옮긴 표다.
        // 한 등급이 여러 열에 걸치면 **앞 열이 한 행 더** 받는다(33 → 17/16).
        // 🔴 2026-09-24 사장님 「안흔함 왼쪽에 비는 거 같은데 여기에 흔함 배치하자」 →
        //    **0열이 흔함 전시로 들어가고 나머지가 한 칸씩 밀렸다.** 흔함은 조합식이 0개라
        //    이 표에 안 실린다(아래 commonUnits가 따로 담당한다) — 그래서 여기 항이 없다.
        Dictionary<UnitGrade, int[]> spread = new Dictionary<UnitGrade, int[]>
        {
            [UnitGrade.Uncommon] = new[] { 1 },          // 2열
            [UnitGrade.Special] = new[] { 2, 3 },        // 3·4열
            [UnitGrade.Rare] = new[] { 4, 5 },           // 5·6열
            [UnitGrade.Legendary] = new[] { 6, 7 },      // 7·8열
            [UnitGrade.Limited] = new[] { 7 },           // 8열 전설 밑 「남는 공간에」
            [UnitGrade.Hidden] = new[] { 8, 9 },         // 9·10열 (2026-09-24 사장님이 둘로 가르셨다)
            // 🔴 영원은 09-24에 사장님이 거두셨다(「안흔함 밑에 영원함 조합식 빼줘」) — 그래서
            //    1열은 안흔함 13식만이다. MapLayout.CombineTableGrades에서 빠졌으니 여기 항이
            //    남아 있어도 실려 올 게 없지만, **둘을 같이 지운다** — 불멸 때와 같은 방식이다.
            //    되살릴 때는 `[UnitGrade.Eternal] = new[] { 0 }`을 여기에, 등급을 저쪽에 같이 넣는다.
            //    (영원을 1열에 둔 것은 PM 판단이었다 — 1열이 가장 얕아서였고, 7열에 두면 34행이
            //     되어 깊이가 먼저 터진다. 폭은 반대로 가장 비싸다: 안흔함 2칸 → 영원 8칸.)
        };

        // 🔴 배정표가 쓰는 열 수와 CombineTableColumns가 어긋나면 **마지막 열이 통째로 사라진다.**
        //    주석으로 「같아야 한다」고 적어 뒀지만 주석은 사람이 읽어야 아는 자리다(PM 지시 09-24).
        //    코드가 스스로 세게 한다.
        int neededColumns = 0;
        foreach (int[] targets in spread.Values)
            foreach (int t in targets) neededColumns = Mathf.Max(neededColumns, t + 1);
        if (neededColumns > CombineTableColumns)
            Debug.LogError($"[맵] 조합표: 배정표가 {neededColumns}열을 쓰는데 CombineTableColumns가 " +
                           $"{CombineTableColumns}입니다 — **{neededColumns - CombineTableColumns}열이 통째로 사라집니다.** " +
                           "둘을 같이 고쳐야 합니다.");

        int columnCount = Mathf.Max(CombineTableColumns, neededColumns);
        List<(UnitGrade, List<CombineRecipe>)>[] byColumn =
            new List<(UnitGrade, List<CombineRecipe>)>[columnCount];
        for (int c = 0; c < columnCount; c++) byColumn[c] = new List<(UnitGrade, List<CombineRecipe>)>();

        // 🔴 흔함 열(0열) — 사장님 09-24 「안흔함 왼쪽에 비는 거 같은데 여기에 흔함 배치하자」.
        //
        // ⚠️ **흔함은 조합식이 0개다.** 위습으로만 얻는 시작 등급이라 만드는 법이 아예 없다
        //    (LoadRecipesProducing(Common)은 빈 목록이다). 그래서 다른 아홉 열과 **모양이 다르다** —
        //    재료·화살표 없이 **결과 칸만** 세운다.
        //    빈 재료 칸을 두면 「재료가 없는 조합식」으로 읽혀 오히려 더 헷갈린다. 이 열이 말해야
        //    하는 것은 「이 등급은 여기서 시작한다」 하나다.
        List<UnitData>[] unitsByColumn = new List<UnitData>[columnCount];
        unitsByColumn[0] = LoadUnitsOfGrade(UnitGrade.Common);
        if (unitsByColumn[0].Count == 0)
            Debug.LogWarning("[맵] 조합표: 흔함 유닛이 0종이라 0열이 빈 칸으로 남습니다 — " +
                             "로스터에 grade==Common이 있는지 보십시오.");

        foreach ((UnitGrade grade, List<CombineRecipe> recipes) in loaded)
        {
            if (!spread.TryGetValue(grade, out int[] targets) || targets.Length == 0)
            {
                Debug.LogWarning($"[맵] 조합표: {grade.KoreanName()} 등급의 열 배정이 표에 없습니다 — " +
                                 $"{recipes.Count}줄을 못 싣습니다. MapLayout.CombineTableGrades에 등급을 " +
                                 "더했으면 위 spread에도 자리를 정해 줘야 합니다.");
                continue;
            }

            int given = 0;
            for (int t = 0; t < targets.Length; t++)
            {
                // 앞 열이 한 행 더: 33을 둘로 나누면 17·16.
                int take = recipes.Count / targets.Length + (t < recipes.Count % targets.Length ? 1 : 0);
                if (take <= 0) continue;
                byColumn[targets[t]].Add((grade, recipes.GetRange(given, take)));
                given += take;
            }
        }

        // 빈 열은 버리고 남은 것만 이어 붙인다. ⚠️ 유닛 열은 조합식이 0개라도 **비어 있지 않다** —
        //    `column.Count == 0`만 보면 흔함 열이 통째로 사라진다.
        for (int c = 0; c < columnCount; c++)
        {
            List<UnitData> units = unitsByColumn[c];
            bool hasUnits = units != null && units.Count > 0;
            if (byColumn[c].Count == 0 && !hasUnits) continue;
            columns.Add(byColumn[c]);
            columnUnits.Add(hasUnits ? units : null);
        }

        if (columns.Count == 0) return "";

        // 열 폭을 표 폭에서 균등하게 나누면, 재료 2칸짜리(안흔함)와 5칸짜리(제한·히든)가
        // 같은 폭을 받아 한쪽은 텅 비고 한쪽은 바닥판 밖으로 삐져나온다.
        // 열마다 그 안에서 가장 긴 줄에 맞춰 폭을 따로 잡고, 왼쪽부터 이어 붙인다.
        float[] columnWidths = new float[columns.Count];
        int[] columnMaxSlots = new int[columns.Count];
        float totalWidth = 0f;

        for (int c = 0; c < columns.Count; c++)
        {
            foreach ((UnitGrade _, List<CombineRecipe> chunk) in columns[c])
                foreach (CombineRecipe recipe in chunk)
                    columnMaxSlots[c] = Mathf.Max(columnMaxSlots[c], RecipeSlotCount(recipe));

            // 유닛 열은 재료 칸도 화살표도 없다 — 결과 칸 하나 + 좌우 여백뿐(약 29).
            // 조합식 열 식에 재료칸 0을 넣으면 화살표 몫(13.7)이 남아 칸보다 넓어진다.
            columnWidths[c] = columnUnits[c] != null
                ? RecipeSlot + ColumnPad * 2f
                : columnMaxSlots[c] * (RecipeSlot + RecipeGap) + RecipeArrowGap + RecipeSlot
                  + ColumnPad * 2f;
            totalWidth += columnWidths[c];
        }

        // 자연 폭이 섬을 넘으면 가로만 줄여 맞춘다. 넘치지 않으면 그대로 둔다.
        RecipeScale = totalWidth > island.size.x ? island.size.x / totalWidth : 1f;
        if (RecipeScale < 1f)
        {
            for (int c = 0; c < columnWidths.Length; c++) columnWidths[c] *= RecipeScale;
            totalWidth *= RecipeScale;
        }

        float tableLeft = island.center.x - island.size.x * 0.5f;
        float tableTop = island.center.y + island.size.y * 0.5f;
        float cursorX = tableLeft + (island.size.x - totalWidth) * 0.5f;   // 표 안에서 가운데 정렬

        int placed = 0;
        float deepest = 0f;
        string sample = null;

        for (int c = 0; c < columns.Count; c++)
        {
            float columnWidth = columnWidths[c];
            float columnLeft = cursorX;
            cursorX += columnWidth;
            float rowZ = tableTop - RecipeRowHeight;
            float rowLeftX = columnLeft + PadW + SlotW * 0.5f;
            float resultX = rowLeftX + columnMaxSlots[c] * (SlotW + GapW) + ArrowW;

            // 첫 열 왼쪽부터 마지막 열 오른쪽까지, 칸 경계마다 한 장씩.
            BuildColumnWall(parent, $"조합표_칸벽_{c}", columnLeft, island.center.y, island.size.y);
            if (c == columns.Count - 1)
                BuildColumnWall(parent, "조합표_칸벽_끝", cursorX, island.center.y, island.size.y);

            // 유닛 열(흔함) — 결과 칸만 세로로. 등급이 하나뿐이라 등급 구분벽도 없다.
            // ⚠️ 흔함↔안흔함 경계는 **열 경계**이므로 바로 위 칸벽이 이미 가른다. 여기에 등급
            //    구분벽을 또 세우면 다른 열 경계(안흔함↔특별함 등)와 달라져 오히려 튄다 —
            //    등급 구분벽은 **한 열 안에서** 등급이 바뀔 때만 쓰는 것이다.
            if (columnUnits[c] != null)
            {
                GameObject unitStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unitStrip.name = "조합표_흔함";
                unitStrip.transform.SetParent(parent, false);
                float unitDepth = columnUnits[c].Count * RecipeRowHeight;
                unitStrip.transform.position = new Vector3(columnLeft + columnWidth * 0.5f,
                    MapLayout.IslandTop + 0.06f, rowZ + RecipeRowHeight * 0.5f - unitDepth * 0.5f);
                unitStrip.transform.localScale = new Vector3(columnWidth, 0.12f, unitDepth);
                Paint(unitStrip, "combine", columnWidth, unitDepth);
                Object.DestroyImmediate(unitStrip.GetComponent<Collider>());

                foreach (UnitData unit in columnUnits[c])
                {
                    // 재료가 없으니 결과 칸 자리(resultX)가 아니라 **첫 칸 자리**에 세운다 —
                    // resultX는 재료 묶음 + 화살표만큼 오른쪽이라 이 좁은 열 밖으로 나간다.
                    PlaceRecipeSlot(parent, rowLeftX, rowZ, unit.unitName,
                                    GradeColor(unit.grade), $"흔함_{unit.unitName}", unit);
                    rowZ -= RecipeRowHeight;
                }

                deepest = Mathf.Max(deepest, tableTop - rowZ);
                continue;
            }

            for (int b = 0; b < columns[c].Count; b++)
            {
                (UnitGrade grade, List<CombineRecipe> chunk) = columns[c][b];

                // 같은 열에서 등급이 바뀌는 자리에만 벽을 세운다.
                if (b > 0 && columns[c][b - 1].grade != grade)
                {
                    float wallZ = rowZ + RecipeRowHeight * 0.5f - GradeWallGap * 0.5f;
                    BuildDecor(parent, $"조합표_구분벽_{grade.KoreanName()}",
                        new Vector3(columnLeft + columnWidth * 0.5f,
                                    MapLayout.IslandTop + WallHeight * 0.5f, wallZ),
                        new Vector3(columnWidth, WallHeight, GateThickness), "rock");
                    rowZ -= GradeWallGap;
                }

                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"조합표_{grade.KoreanName()}";
                strip.transform.SetParent(parent, false);
                float blockDepth = chunk.Count * RecipeRowHeight;
                strip.transform.position = new Vector3(columnLeft + columnWidth * 0.5f,
                    MapLayout.IslandTop + 0.06f, rowZ + RecipeRowHeight * 0.5f - blockDepth * 0.5f);
                strip.transform.localScale = new Vector3(columnWidth, 0.12f, blockDepth);
                Paint(strip, "combine", columnWidth, blockDepth);
                Object.DestroyImmediate(strip.GetComponent<Collider>());

                for (int r = 0; r < chunk.Count; r++)
                {
                    PlaceRecipeRow(parent, chunk[r], rowLeftX, rowZ, resultX);
                    rowZ -= RecipeRowHeight;
                    placed++;

                    if (sample == null) sample = DescribeRecipe(chunk[r]);
                }
            }

            deepest = Mathf.Max(deepest, tableTop - rowZ);
        }

        // ⚠️ 여기 `deepest`는 아래 열별 줄의 「깊이」와 **다른 수**다. 커서(rowZ)가 마지막 줄
        //    아래로 한 행 더 내려가 있어 딱 RecipeRowHeight만큼 크다(열 1215.1 vs 여기 1261.3).
        //    2026-09-24에 PM이 열 쪽 숫자로 섬 세로를 1260으로 잡았다가 1.3이 모자랄 뻔했다 —
        //    **같은 것을 가리키는 두 이름은 둘 다 이름을 달고 나와야 한다.**
        float available = island.size.y;
        string verdict = deepest <= available
            ? $"여유 {available - deepest:F0}"
            : $"⚠️ {deepest - available:F0} 모자람 — CombineTable 세로를 {Mathf.CeilToInt(deepest) + 8}으로";

        // 보고문의 ⚠️는 스크롤에 묻힌다. 잘린 표는 사장님 화면에서 「글씨가 없다」로만 보이므로
        // 콘솔에도 소리를 낸다(PM 요청 09-24). 섬 세로는 순환 때문에 리터럴이라 자동 교정이
        // 안 되니, **고칠 값까지 적어 준다.**
        if (deepest > available)
            Debug.LogWarning($"[맵] 조합표가 섬보다 깊습니다 — 필요 {deepest:F0} / 섬 {available:F0} " +
                             $"({deepest - available:F0} 넘침). MapLayout.CombineSizeZ를 " +
                             $"{Mathf.CeilToInt(deepest) + Mathf.CeilToInt(RecipeRowHeight)}으로 올려야 " +
                             "마지막 줄이 안 잘립니다.");

        string fit = totalWidth <= island.size.x
            ? $"가로 {totalWidth:F0}/{island.size.x:F0} 여유 {island.size.x - totalWidth:F0}"
            : $"⚠️ 가로 {totalWidth - island.size.x:F0} 모자람 — CombineTable 가로를 {Mathf.CeilToInt(totalWidth) + 8}으로";

        // ⚠️ 뽑기섬 조합식은 제 폭이 따로 있으므로 축소율을 물려주면 안 된다 — 여기서 되돌린다.
        float usedScale = RecipeScale;
        RecipeScale = 1f;

        string fitNote = usedScale < 1f
            ? $"\n  가로 {usedScale:P0}로 줄여 섬(폭 {island.size.x:F0})에 맞췄습니다 — 자연 폭이 {totalWidth / usedScale:F0}였습니다."
            : "";

        // 열마다 「무엇이 몇 행, 폭 얼마」를 찍는다 — 사장님 지시대로 배정됐는지 눈으로 세려면
        // 이게 있어야 한다. 그리고 다음에 누가 등급을 더하거나 섬 크기를 만질 때 **어느 열이
        // 먼저 터지는지**가 한눈에 보인다(깊이는 행수, 폭은 최대 재료칸이 정한다 — 축이 둘이다).
        List<string> perColumn = new List<string>();
        for (int c = 0; c < columns.Count; c++)
        {
            int rows = 0;
            List<string> parts = new List<string>();

            // 유닛 열은 조합식이 0이라 조합식 쪽 루프로는 「0행」으로 찍힌다 — 따로 센다.
            if (columnUnits[c] != null)
            {
                rows = columnUnits[c].Count;
                parts.Add($"흔함{rows}종(전시)");
            }

            foreach ((UnitGrade g, List<CombineRecipe> chunk) in columns[c])
            {
                rows += chunk.Count;
                parts.Add($"{g.KoreanName()}{chunk.Count}");
            }
            float depth = rows * RecipeRowHeight + (columns[c].Count - 1) * GradeWallGap;
            perColumn.Add($"\n    {c + 1}열 {string.Join("+", parts),-22} {rows,3}행 · 글씨깊이 {depth:F0} · " +
                          $"폭 {columnWidths[c]:F0}(" +
                          (columnUnits[c] != null ? "결과칸만" : $"재료 {columnMaxSlots[c]}칸") + ")");
        }

        int displayed = 0;
        foreach (List<UnitData> units in columnUnits) if (units != null) displayed += units.Count;

        return fitNote + $"\n조합식 표: {placed}개 조합식" +
               (displayed > 0 ? $" + 흔함 {displayed}종 전시" : "") +
               $", {columns.Count}열 (사장님 지시 배정)." +
               $"\n  섬깊이 필요 {deepest:F0}/{available:F0} {verdict}" +
               $" — 아래 열별 「글씨깊이」보다 한 행({RecipeRowHeight:F0}) 큰 것이 정상입니다\n  {fit}" +
               string.Join("", perColumn) +
               (sample != null ? $"\n  예시: {sample}" : "");
    }

    // 재료가 가장 많은 조합식이 열 폭에 들어가는지 확인하는 데 쓴다.
    // count가 3이면 칸 세 개를 차지한다 — 원작 표가 그렇게 늘어놓는다.
    static int RecipeSlotCount(CombineRecipe recipe)
    {
        int slots = 0;

        if (recipe.ingredients != null)
            foreach (RecipeIngredient ingredient in recipe.ingredients)
                if (ingredient != null) slots += Mathf.Max(1, ingredient.count);

        return slots;
    }

    // 줄 하나가 차지하는 가로 — 첫 재료 칸의 왼쪽 끝부터 결과 칸의 오른쪽 끝까지.
    static float RecipeRowWidth(CombineRecipe recipe, bool withCosts = false)
    {
        int slots = RecipeSlotCount(recipe);
        float width = slots * (RecipeSlot + RecipeGap) + RecipeArrowGap + RecipeSlot;
        if (withCosts) width += CountCostIcons(recipe) * (CostSlot + CostGap) + CostBlockGap;
        return width;
    }

    // ⚠️ withCosts일 때는 비용 칸을 「그 등급에서 가장 많은 아이콘 수」로 고정해서 잰다 —
    // PlaceRecipeRow가 실제로 그렇게 자리를 잡으므로(계단 어긋남 방지), 줄마다 제 아이콘
    // 수로 재면 폭을 실제보다 좁게 잡아 "칸 폭을 넘는다" 경고를 놓친다.
    static float MaxRecipeRowWidth(UnitGrade[] grades, bool withCosts)
    {
        float widest = 0f;

        foreach (UnitGrade grade in grades)
        {
            List<CombineRecipe> recipes = LoadRecipesProducing(grade);

            int maxCostIcons = 0;
            if (withCosts)
                foreach (CombineRecipe recipe in recipes)
                    maxCostIcons = Mathf.Max(maxCostIcons, CountCostIcons(recipe));

            float costWidth = maxCostIcons == 0
                ? 0f
                : maxCostIcons * (CostSlot + CostGap) + CostBlockGap;

            foreach (CombineRecipe recipe in recipes)
                widest = Mathf.Max(widest, RecipeRowWidth(recipe) + costWidth);
        }

        return widest;
    }

    static int CountCostIcons(CombineRecipe recipe)
    {
        int icons = recipe.goldCost > 0 ? 1 : 0;

        if (recipe.resourceCosts != null)
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
                if (cost != null && cost.amount > 0) icons++;

        return icons;
    }

    // 한 줄: 재료를 왼쪽부터 늘어놓고, 사이를 띄운 뒤 결과를 놓는다.
    // resultX를 열마다 하나로 고정하면, 재료가 2개든 5개든 결과 칸이 세로로 나란히 선다 —
    // 재료 개수만큼 결과가 오른쪽으로 밀리면 칸 안에서 대각선으로 흩어져 읽기 어렵다.
    // costBlockWidth: 비용 칸에 고정으로 잡아둘 가로. 0이면 비용을 안 그린다.
    // ⚠️ 실제 아이콘 개수만큼만 밀면 안 된다 — 코인만 드는 줄과 코인+목재+토큰이 드는 줄에서
    // 첫 재료 X가 달라져 표가 계단처럼 어긋난다(2026-09-06 사장님 지적). 그 블록에서
    // 가장 많은 아이콘 수로 자리를 잡아두고, 아이콘이 적은 줄은 그 자리를 비워 둔다.
    static void PlaceRecipeRow(Transform parent, CombineRecipe recipe, float leftX, float z, float resultX,
                               bool showCosts = false, float costBlockWidth = 0f)
    {
        float x = leftX;
        string label = recipe.result != null ? recipe.result.unitName : "?";

        // 비용은 재료보다 앞에 세운다. 재료 개수가 줄마다 달라서 뒤에 붙이면 들쭉날쭉해지는데,
        // 앞에 두면 줄이 달라도 세로로 나란히 서서 "여긴 다 토큰이 든다"가 한눈에 보인다.
        if (showCosts)
        {
            PlaceCostIcons(parent, recipe, x, z, label);
            x += costBlockWidth;   // 그린 개수가 아니라 고정 폭만큼 민다
        }

        if (recipe.ingredients != null)
        {
            foreach (RecipeIngredient ingredient in recipe.ingredients)
            {
                if (ingredient == null) continue;

                // count가 3이면 같은 칸을 세 번 놓는다 — 원작 표가 그렇게 늘어놓는다.
                for (int n = 0; n < Mathf.Max(1, ingredient.count); n++)
                {
                    PlaceRecipeSlot(parent, x, z, IngredientName(ingredient), IngredientColor(ingredient),
                                    $"재료_{label}", ingredient.unit);
                    x += SlotW + GapW;
                }
            }
        }

        Color resultColor = recipe.result != null ? GradeColor(recipe.result.grade) : Color.gray;
        PlaceRecipeSlot(parent, resultX, z, label, resultColor, $"결과_{label}", recipe.result);
    }

    // 팝업에 찍을 한 줄 요약. "초록 + 초록 = 노랑"처럼 색이 바뀌는지 눈으로 확인하는 용도다.
    static string DescribeRecipe(CombineRecipe recipe)
    {
        List<string> parts = new List<string>();

        if (recipe.ingredients != null)
        {
            foreach (RecipeIngredient ingredient in recipe.ingredients)
            {
                if (ingredient == null) continue;
                for (int n = 0; n < Mathf.Max(1, ingredient.count); n++)
                    parts.Add(IngredientName(ingredient));
            }
        }

        string result = recipe.result != null
            ? $"{recipe.result.grade.KoreanName()} {recipe.result.unitName}"
            : "?";

        return string.Join(" + ", parts) + " = " + result;
    }

    // 코인·목재·행운토큰을 세우고, 재료가 시작할 자리까지의 폭을 돌려준다.
    // 개수(코인 10000, 목재 7)는 3D로 못 적는다 — 오브젝트 이름에만 남기고,
    // 정확한 수치는 하단 HUD의 조합 카드 툴팁이 보여준다.
    static float PlaceCostIcons(Transform parent, CombineRecipe recipe, float leftX, float z, string label)
    {
        int placed = 0;
        float x = leftX;

        if (recipe.goldCost > 0)
        {
            BuildCostIcon(parent, $"비용_{label}_코인_{recipe.goldCost}", x, z,
                          new Color(1.00f, 0.82f, 0.25f), CostIconShape.Coin, glow: false);
            x += CostW + CostGapW;
            placed++;
        }

        if (recipe.resourceCosts != null)
        {
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
            {
                if (cost == null || cost.amount <= 0) continue;

                bool wood = cost.type == ResourceType.Wood;
                BuildCostIcon(parent, $"비용_{label}_{cost.type}_{cost.amount}", x, z,
                              wood ? new Color(0.45f, 0.30f, 0.16f) : new Color(0.35f, 0.95f, 0.75f),
                              wood ? CostIconShape.Log : CostIconShape.Coin,
                              glow: cost.type == ResourceType.LuckyToken);
                x += CostW + CostGapW;
                placed++;
            }
        }

        return placed == 0 ? 0f : x - leftX + CostBlockGapW;
    }

    enum CostIconShape { Coin, Log }

    static void BuildCostIcon(Transform parent, string name, float x, float z, Color color,
                              CostIconShape shape, bool glow)
    {
        GameObject icon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        icon.name = name;
        icon.transform.SetParent(parent, false);

        if (shape == CostIconShape.Coin)
        {
            // 위에서 내려다보는 카메라라 눕힌 원판이 동전으로 읽힌다.
            icon.transform.position = new Vector3(x, MapLayout.IslandTop + 0.4f, z);
            icon.transform.localScale = new Vector3(CostSlot, 0.4f, CostSlot);
        }
        else
        {
            // 통나무 — 원기둥을 눕혀서 줄 방향과 직각으로 놓는다.
            icon.transform.position = new Vector3(x, MapLayout.IslandTop + 0.8f, z);
            icon.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            icon.transform.localScale = new Vector3(CostSlot * 0.5f, CostSlot * 0.5f, CostSlot * 0.5f);
        }

        if (glow) PaintGlow(icon, color);
        else PaintSolid(icon, color);

        Object.DestroyImmediate(icon.GetComponent<Collider>());
    }

    // 등급 칸과 칸 사이에 세로로 서는 벽 — 표를 "흔함|안흔함|특별함|…"으로 끊어 읽게 한다.
    // 열 경계 위에 걸터앉되 두께가 양쪽 여백(ColumnPad) 안에 들어가므로 열 폭을 더 먹지 않는다.
    static void BuildColumnWall(Transform parent, string name, float x, float centerZ, float depth)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = new Vector3(x, MapLayout.IslandTop + WallHeight * 0.5f, centerZ);
        wall.transform.localScale = new Vector3(GateThickness, WallHeight, depth);
        Paint(wall, "rock", GateThickness, depth);
        // 콜라이더는 뺀다. 표를 가로지르는 벽 8장이 실제로 막히면 NavMesh가 세로로 조각나고
        // 표 위를 걸어 지나갈 수 없게 된다. 여긴 싸우는 곳이 아니라 보는 곳이다.
        Object.DestroyImmediate(wall.GetComponent<Collider>());
        DressWall(wall, "돌담_얇음");   // 보기용이라 막는 상자는 없고 겉모습만 돌담
    }

    // 모델이 붙은 유닛은 조합표에도 그 모습으로 세운다 — 색 큐브만 있으면 무엇이 재료인지
    // 이름표를 눌러봐야 안다. 모델이 없는 유닛은 예전처럼 등급 색 큐브다.
    static void PlaceRecipeSlot(Transform parent, float x, float z, string label, Color color, string prefix,
                                UnitData unit = null)
    {
        Vector3 ground = new Vector3(x, MapLayout.IslandTop, z);

        // 칸마다 낮은 돌 받침(5.98×5.98, 2026-09-13 Blender). 지름은 인형 기준(PedestalWidth,
        // 전시 받침과 같은 규칙 — PM 지시 2026-09-23)이되, 칸 폭이 그보다 좁으면 칸에 맞춘다
        // (옆 칸 받침과 맞닿지 않게).
        float lift = StructureDresser.PlacePedestal(parent, "받침_조합", $"{prefix}_{label}_받침",
                                                    ground, 0f, Mathf.Min(PedestalWidth, SlotW) / CombinePedestalWidth);
        ground.y += lift;

        // 인형 키는 레인 유닛과 같은 값으로 고정한다(사장님 지시 2026-09-23, DisplayFigureHeight).
        // ⚠️ 예전 값(RecipeRowHeight×0.9×RecipeScale)은 "칸이 좁아지면 인형도 같이 줄여 겹침을
        //    막는다"는 뜻이었다. 지금은 칸 쪽이 훨씬 커져서(줄 높이 28.4, 축소 후 칸 폭 8.1)
        //    키 20짜리 인형이 가로·세로 모두 칸 안에 들어간다 — 겹침 조건이 사라졌다.
        //    칸이 이보다 더 좁아지면 이 줄을 Mathf.Min으로 다시 묶어야 한다.
        if (TryPlaceUnitModel(parent, $"{prefix}_{label}", ground, unit, DisplayFigureHeight)) return;

        GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slot.name = $"{prefix}_{label}";
        slot.transform.SetParent(parent, false);
        slot.transform.position = new Vector3(x, ground.y + RecipeSlotHeight * 0.5f, z);
        slot.transform.localScale = new Vector3(SlotW, RecipeSlotHeight, SlotW);
        PaintSolid(slot, color);
        // 표 위를 걸어다녀야 하므로 통과시킨다.
        Object.DestroyImmediate(slot.GetComponent<Collider>());
    }

    /// <summary>
    /// 자세를 입힌 뒤 실제로 누워 있으면 인형을 통째로 돌려 세운다.
    ///
    /// 🔴 2026-09-09 사장님 스크린샷(조합판) — 코비(흔함_문필환)가 누워 있었다. 씬의 뼈를
    ///    월드로 풀어 재 보니 **머리 y=0.23 · 발 y=1.37**로 머리가 발보다 아래였다
    ///    (머리-발 비율 -0.06). 안흔함_박민수(로이킴)도 0.01이었다.
    ///    원본 FBX의 바인드 포즈는 멀쩡히 서 있다(assimp 실측: 세로 1.75가 제일 길고 발이 y=0).
    ///    즉 눕힌 것은 **리타게팅된 Idle 자세**다 — 프리팹 회전은 0이었고 아무도 못 잡았다.
    ///
    /// ⚠️ PoseLooksApplied는 팔이 내려왔는지만 본다. 리타게팅이 "성공"하면서 몸을 눕혀도
    ///    통과한다 — 그래서 경고가 한 건도 안 떴다.
    ///
    /// AutoUpright와 같은 방법(축 직접 맞추기)을 쓴다. 오일러를 축마다 90°로 반올림하면
    /// 회전이 그렇게 분해되지 않아 오히려 눕는다(2026-09-08 박준희 실측).
    /// 서 있는 인형은 회전이 identity로 나와 아무것도 안 한다 — 회귀 없음.
    /// </summary>
    static void StandFigureUpright(GameObject figure)
    {
        Animator animator = figure.GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid) return;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (hips == null || head == null || leftArm == null || rightArm == null) return;

        Vector3 up = head.position - hips.position;
        Vector3 right = rightArm.position - leftArm.position;
        if (up.sqrMagnitude < 1e-8f || right.sqrMagnitude < 1e-8f) return;

        up.Normalize();
        right = Vector3.ProjectOnPlane(right, up);
        if (right.sqrMagnitude < 1e-8f) return;      // 팔이 몸통 축과 나란하면 못 잰다
        right.Normalize();

        Vector3 upAxis = NearestWorldAxis(up);
        Vector3 rightAxis = NearestWorldAxis(right, exclude: upAxis);
        if (rightAxis == Vector3.zero) return;

        Quaternion snapped = Quaternion.Inverse(
            Quaternion.LookRotation(Vector3.Cross(rightAxis, upAxis), upAxis));
        if (Quaternion.Angle(snapped, Quaternion.identity) < 1f) return;   // 이미 서 있다

        figure.transform.rotation = snapped * figure.transform.rotation;
        Debug.Log($"[맵] {figure.name}: 자세가 누워 있어 세웠습니다 — 위 {upAxis}, 오른쪽 {rightAxis}.");
    }

    // ArtBinder.NearestAxis와 같은 것 — 에디터 클래스가 서로를 못 부르므로 여기 한 벌 둔다.
    static Vector3 NearestWorldAxis(Vector3 v, Vector3 exclude = default)
    {
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        Vector3 best = Vector3.zero;
        float bestDot = -2f;
        foreach (Vector3 a in axes)
        {
            if (exclude != Vector3.zero && Mathf.Abs(Vector3.Dot(a, exclude)) > 0.9f) continue;
            float d = Vector3.Dot(v.normalized, a);
            if (d > bestDot) { bestDot = d; best = a; }
        }
        return best;
    }

    /// <summary>
    /// 인형의 월드 경계를 **메시 자산에서 직접** 잰다.
    ///
    /// 🔴 Renderer.bounds를 쓰면 안 된다. 한 번도 그려진 적이 없는 인형에서는 갱신이 늦어
    ///    엉뚱한 값이 나온다 — 2026-09-09에 이걸로 두 번 당했다.
    ///    ① 스케일을 준 뒤 다시 읽어 검사했더니 437배가 그대로 통과했다.
    ///    ② 스케일을 주기 전 값도 못 믿는다 — 그 값으로 height/size.y를 계산하니
    ///       박준희가 97배로 부풀었다(사장님 「검은 물체 안사라지는데」).
    ///
    /// sharedMesh.bounds는 **자산에 저장된 값**이라 언제 읽어도 같다. 그 8개 꼭짓점을
    /// 렌더러의 localToWorld로 옮겨 합치면, 지금 걸린 회전·스케일이 그대로 반영된
    /// 월드 경계가 나온다. 그려졌는지 여부와 무관하다.
    /// </summary>
    static bool TryMeasureFigure(GameObject figure, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (Renderer renderer in figure.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : (renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null);
            if (mesh == null) continue;

            Bounds local = mesh.bounds;
            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                                           (corner & 2) == 0 ? -1f : 1f,
                                           (corner & 4) == 0 ? -1f : 1f);
                Vector3 world = toWorld.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; }
                else bounds.Encapsulate(world);
            }
        }

        return any;
    }

    // 가로·앞뒤가 키의 몇 배까지 되어도 봐줄 것인가. 서 있는 사람은 팔을 벌려도 0.7배
    // 남짓이고, 네 발 짐승(재규어)이나 자전거(상붕카)를 감안해도 3배면 넉넉하다.
    // 이걸 넘으면 누워 있는 것이다 — 그 짧은 세로에 키를 맞추면 전체가 폭주한다.
    const float MaxFigureSpread = 3f;

    /// <summary>
    /// 크기를 다 먹인 뒤 인형이 목표 키의 몇 배까지 커져도 봐줄지. 넘으면 색 큐브로 바꾼다
    /// (TryPlaceUnitModel 아래쪽 "폭주 방어막" 참고). 넉넉하게 잡는 게 원칙이다 —
    /// 좁히면 멀쩡한 유닛을 큐브로 만든다(edda60a8 전례).
    /// </summary>
    const float MaxFigureWorldSize = 5f;

    // 🔴 뼈와 메시가 어긋난 변환본 — 조합표·부스 인형으로 세우지 않고 색 큐브로 둔다(프리팹 이름에서 Unit_을 뗀 모델 이름).
    //    2026-09-13 사장님 「조합판 이상한 게 크게 있다」: 안흔함_김수빈 인형 넷이 350×41×112 흰 덩어리로 표를 덮었다.
    //    이 모델은 TryPlaceUnitModel이 믿는 메시 자산 경계와 실제 스킨 크기가 수백 배 달라 크기 맞추기가 폭주한다.
    //    ⚠️ 모든 인형을 BakeMesh로 다시 재는 일반 검사를 먼저 넣었다가 **정상 유닛 수십 개를 큐브로 바꿔** 걷어냈다(edda60a8 되돌림) —
    //       편집 중 스킨 크기 재기는 모델마다 기준이 달라 믿을 수 없다. 이름으로 막는다.
    //    blender가 원본 glb로 다시 지은 파일이 들어오면 이 줄을 지운다.
    //    ✅ 09-13 김수빈 줄 지움 — 덩어리의 진짜 원인은 새 파일(원본 glb 재건본)이 아니라 **덮어쓰기 전에 만든 옛 프리팹**의
    //       회전·크기가 새 모델에 걸린 것이었다. 모델 배선을 다시 돌리니 키 18.9로 곧게 섰다(원격 units·preview).
    //       교훈: 모델 파일을 바꾼 뒤엔 반드시 모델 배선부터 — 옛 프리팹 값이 새 모델을 비틀어도 조용하다.
    static readonly string[] BrokenSkinDolls = { };

    // 유닛 프리팹에서 보이는 부분만 떼어 세운다. 프리팹을 통째로 놓으면 조합표 위에
    // 진짜 유닛이 살아 움직이게 된다 — 이건 보여주기용 인형이라 부품을 전부 걷어낸다.
    static bool TryPlaceUnitModel(Transform parent, string name, Vector3 ground, UnitData unit, float height, float yaw = 0f)
    {
        if (unit == null || unit.prefab == null) return false;
        string brokenCheck = unit.prefab.name.StartsWith("Unit_") ? unit.prefab.name.Substring(5) : unit.prefab.name;
        if (BrokenSkinDolls.Contains(brokenCheck)) return false;   // 색 큐브로 — 위 표 주석 참고

        GameObject figure = Object.Instantiate(unit.prefab, parent);
        figure.name = name;

        PoseAsIdle(figure);
        // 🔴 자세를 입힌 결과가 누워 있으면 인형을 통째로 돌려 세운다.
        //    2026-09-09 실측으로 **이것만이 실제로 듣는다**는 게 확인됐다 — 프리팹 쪽에서
        //    같은 일을 시도한 ArtBinder.UprightAnimatedPose는 문필환에 회전 0을 돌려줬고
        //    (프리팹은 재생성됐는데 회전이 그대로였다), 이걸 걷어내자마자 코비가 다시 누웠다
        //    (머리-발 0.77 → -0.06). 살아 있는 인형에서 실제로 구운 자세를 재는 쪽이 맞다.
        //    ⚠️ 이 회전이 틀리면 바로 아래 크기 맞추기가 엉뚱한 축에 키를 맞춰 폭주한다 —
        //    그 대비가 MaxFigureSpread 검사다(박준희가 97배로 부풀었던 자리).
        // ⚠️ 표에 회전을 직접 적은 모델(뼈와 메시가 따로 노는 변환본)은 뼈로 세우면 오히려 눕는다 — 프리팹 회전을 믿는다.
        string prefabModel = unit.prefab.name.StartsWith("Unit_") ? unit.prefab.name.Substring("Unit_".Length) : unit.prefab.name;
        if (!ArtBinder.HasManualRotation(prefabModel)) StandFigureUpright(figure);

        // 스크립트를 먼저 지운다. NavMeshAgent를 먼저 지우려 하면 UnitMover가 그것을 요구하고
        // 있어서 거부당하고, 결과적으로 조합표 위에 살아 있는 에이전트가 남는다.
        foreach (MonoBehaviour script in figure.GetComponentsInChildren<MonoBehaviour>(true))
            if (script != null) Object.DestroyImmediate(script);

        // ⚠️ Animator는 **남긴다**. 예전엔 이것까지 지우고 PoseAsIdle이 남긴 뼈 회전에만
        //    기댔는데, 그 한 번의 평가가 실패하면 인형이 T자로 굳는다 — 실패해도 조용해서
        //    240개 중 어느 게 굳었는지 눈으로 못 고른다(2026-09-07 사장님 「팔 벌리고있음」).
        //    Animator를 살려 두면 컨트롤러 기본 상태가 Idle이라 실행 중엔 반드시 선다.
        //    CharacterAnimator(게임 상태를 읽는 쪽)는 위에서 이미 지워졌으므로 Idle에 머문다.
        foreach (Component component in figure.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            if (component is Transform || component is Renderer || component is MeshFilter) continue;
            if (component is Animator) continue;
            Object.DestroyImmediate(component);
        }

        // 표 위 인형이 수백 개다. 안 보이는 동안은 계산도 끄고 시간도 안 흘린다.
        foreach (Animator doll in figure.GetComponentsInChildren<Animator>(true))
        {
            doll.applyRootMotion = false;          // 제자리에 세워 둔다
            doll.cullingMode = AnimatorCullingMode.CullCompletely;
            // 에디터에선 꺼 둔다 — 켜진 Animator는 씬 로드·프리팹 갱신 때 다시 바인드하면서
            // 방금 써 둔 Idle 자세를 기본 자세(T)로 되돌릴 수 있다. 실행 때 DollIdle이 켠다.
            doll.enabled = false;
        }
        if (figure.GetComponent<DollIdle>() == null) figure.AddComponent<DollIdle>();

        Renderer[] renderers = figure.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(figure);
            return false;
        }

        figure.transform.position = ground;

        // 🔴 재기 **전에** 배율을 프리팹 값으로 되돌린다. 아래에서 잰 크기로 배율을 정하는데,
        //    이미 배율이 먹은 몸을 재면 그 배율이 한 번 더 곱해진다 — 두 번 돌리면 두 번 커진다.
        //    적 자리표시 상자가 그렇게 키 15에 폭 25가 됐다(PM 17eb0034). 지금은 `figure`가
        //    바로 위에서 만든 새 인스턴스라 이 줄이 아무것도 안 바꾸지만, **안전의 근거가
        //    「호출자가 새것을 준다」는 우연이면 안 된다.** 여기서 출발점을 고정한다.
        figure.transform.localScale = unit.prefab.transform.localScale;

        if (!TryMeasureFigure(figure, out Bounds bounds) || bounds.size.y < 0.001f)
        {
            Object.DestroyImmediate(figure);
            return false;
        }

        // 🔴 키를 맞추기 **전에** 가로세로 비율로 판정한다.
        //    서 있는 사람은 가로·앞뒤가 키보다 작다. 키(세로)가 가장 짧으면 그 모델은
        //    누워 있는 것이고, 그 짧은 값에 목표 키를 맞추려다 전체가 폭주한다.
        //    2026-09-09 실측(사장님 「조합편에 이상한게 생김」): 안흔함_박준희가 가로 437로
        //    부풀어 표 절반을 가렸다. 목표 키는 4.5였다 — 97배다.
        //    같은 사고가 세 번째다(2026-09-08엔 7,062배였다).
        //
        //    ⚠️ 스케일을 바꾼 뒤에 renderers[].bounds를 **다시 읽어서** 재면 안 된다.
        //       SkinnedMeshRenderer의 bounds는 한 번도 그려지지 않은 인형에서 갱신이 늦어,
        //       방금 준 스케일이 반영 안 된 옛 값이 나온다. 2026-09-09에 그렇게 짜서
        //       437배가 검사를 그대로 통과했다. 비율은 스케일과 무관하므로 이 문제가 없다.
        //
        //    ⚠️ 원인(방향 오판)은 여기서 못 고친다. 여기서는 **번지지 않게** 막고 이름을 남긴다 —
        //    색 큐브로 떨어지면 어느 모델이 문제인지 표에서 바로 보인다.
        Vector3 raw = bounds.size;
        float flatness = Mathf.Max(raw.x, raw.z) / Mathf.Max(raw.y, 1e-6f);
        if (flatness > MaxFigureSpread)
        {
            Debug.LogWarning($"[맵] {name}: 원본 비율이 가로 {raw.x:F2} · 세로 {raw.y:F2} · 앞뒤 {raw.z:F2}로 " +
                             $"납작합니다(가로/키 {flatness:F0}배). 누워 있는 모델에 키를 맞추면 " +
                             $"{height * flatness:F0} 크기로 부풀어 표를 덮으므로 색 큐브로 둡니다 — " +
                             "Tools > 아트 > 스킨 방향 점검으로 확인하세요.", figure);
            Object.DestroyImmediate(figure);
            return false;
        }

        // 사람은 키(Y)에 맞추고, 사람이 아닌 모델(네 발 짐승·탈것)은 **가장 긴 축**에 맞춘다.
        // 재규어를 키로 맞추면 몸길이가 키의 1.7배라 옆 칸을 밀고 나간다(2026-09-11 사장님
        // 스크린샷 — 조합 표 위 재규어가 사람 셋만 했다). ArtBinder.FitToHeight와 같은 규칙이다.
        Animator poser = figure.GetComponentInChildren<Animator>(true);
        bool humanFigure = poser != null && poser.avatar != null && poser.avatar.isValid && poser.isHuman;
        float fit = humanFigure ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        // 짐승은 사람만큼 크게 두지 않는다(사장님 지시 2026-09-11). 프리팹 쪽 배수와 같은 표를
        // 쓴다 — 여기서는 프리팹 크기를 안 쓰고 칸 폭에 맞춰 다시 재우기 때문에 따로 먹여야 한다.
        // 프리팹 이름은 "Unit_<모델명>"이라 접두사만 떼면 ArtBinder의 표 열쇠가 된다.
        string modelName = unit.prefab.name.StartsWith("Unit_") ? unit.prefab.name.Substring(5) : unit.prefab.name;
        // 출발점(프리팹 배율)에 **절대값으로** 쓴다. `*=`로 쌓으면 두 번 돌릴 때 두 번 커진다.
        figure.transform.localScale =
            unit.prefab.transform.localScale * (height * ArtBinder.FigureScaleFor(modelName) / fit);

        // 스케일을 바꾸면 경계도 바뀐다. 다시 재서 발을 바닥에 붙인다.
        // (여기 `+=`는 쌓이지 않는다 — 바꾼 배율로 **다시 재서** 절대 목표 `ground.y`로 보정하므로,
        //  두 번 돌려도 같은 높이에서 멈춘다. 자기교정이라 `=`로 바꿀 수 없다.)
        if (TryMeasureFigure(figure, out bounds))
            figure.transform.position += Vector3.up * (ground.y - bounds.min.y);

        // ── 폭주 방어막 (2026-09-23, PM 승인) ──────────────────────────────
        // ⚠️ **이건 방어막이지 수정이 아니다.** 원인은 모델 쪽(뼈/노드 배율)이고 blender 몫이다.
        //
        // 2026-09-23 사장님 「조합판에 이상한거 있는데 이거뭐야」 — 인형 하나의 팔 뼈가
        // 수백 단위로 늘어나 화면을 가로질렀다. 같은 인형의 머리뼈 둘이 **174 떨어져** 있었으니
        // (키가 30인데 머리 안에서 그만큼 벌어질 수는 없다) 실제로 늘어난 것이 맞다.
        //
        // ⚠️ 그때 「원점에서 1,677 떨어졌으니 범인」이라는 판정이 한 번 나왔는데 **그건 틀린
        //    잣대였다.** 원점 거리는 「멀리 놓였다」는 뜻이지 「늘어났다」가 아니다. 늘어남은
        //    **그 인형 자신의 경계**로만 재야 한다 — 아래 검사가 그렇게 한다.
        //    그리고 그 인형은 **씬에 구워진 옛 것**이라 프리팹을 다시 지어도 안 바뀌었다.
        //    맵을 다시 생성해야 걷힌다(모델 파일 자체는 정상으로 재확인됐다).
        //
        // 위쪽 MaxFigureSpread 검사로는 못 잡는다. 이유 둘:
        //  · 그 검사는 **납작한**(누운) 모델만 본다. 팔이 키 축으로 늘어나면 비율이 오히려
        //    정상으로 보여 통과한다.
        //  · 그 검사가 재는 건 **메시 자산 경계**인데, 이 모델은 노드에 배율이 걸려 있어
        //    잣대 자체가 틀렸다(ArtBinder.cs:323의 "0.01 배율을 못 봐 12.29로 읽었다"와 같은 함정).
        //    게다가 blender가 준 "뼈퍼짐:메시경계 0.794"는 **쉬는 자세** 값이고, 유니티가 공용
        //    Idle을 리타게팅하는 순간 팔이 터진다 — 자산만 봐서는 원리적으로 못 본다.
        //
        // 그래서 자산이 아니라 **크기를 다 먹인 뒤의 실제 월드 경계**를 잰다. 화면에 그려질
        // 바로 그 크기라 잣대가 틀릴 여지가 없고, 이름 블랙리스트(BrokenSkinDolls)처럼
        // 새 모델이 들어올 때마다 사람이 갱신해야 하는 문제도 없다.
        //
        // 문턱 5배: 3배로 좁히면 멀쩡한 유닛을 큐브로 바꿀 위험이 있다 — 실제로 모든 인형을
        // BakeMesh로 다시 재는 일반 검사를 넣었다가 **정상 유닛 수십 개를 큐브로 만들어**
        // 되돌린 적이 있다(edda60a8). 놓치는 쪽이 멀쩡한 걸 죽이는 것보다 낫다(PM 지시).
        if (TryMeasureFigure(figure, out Bounds finalBounds))
        {
            float longest = Mathf.Max(finalBounds.size.x, finalBounds.size.y, finalBounds.size.z);
            if (longest > height * MaxFigureWorldSize)
            {
                Debug.LogWarning($"[맵] {unit.unitName}({modelName}): 크기를 맞춘 뒤 실제 크기가 " +
                                 $"{longest:F0}으로 목표 키({height:F0})의 {longest / height:F0}배입니다 — " +
                                 $"판을 덮으므로 색 큐브로 둡니다(경계 {finalBounds.size}). " +
                                 "모델의 뼈/노드 배율 문제이니 blender 쪽에서 고쳐야 합니다.", figure);
                Object.DestroyImmediate(figure);
                return false;
            }
        }

        // 표를 보는 방향(위에서 남쪽을 향해)에서 얼굴이 보이게 돌린다.
        // 대입이 아니라 곱이다 — 앞 단계가 회전을 걸어 뒀다면 덮지 않는다.
        //    지금은 앞에서 회전을 안 걸므로 identity라 예전과 결과가 같다.
        // yaw는 그 기본 방향에서 더 돌릴 각이다(불멸 전시: 화로를 바라보게 — 사장님 지시 2026-09-23).
        figure.transform.rotation = Quaternion.Euler(0f, 180f + yaw, 0f) * figure.transform.rotation;
        return true;
    }


    // 🔴 Untitled 씬에서 돌리면 SaveScene이 조용히 false를 돌려주고, NavMesh도 파일로 못 남긴다.
    //    2026-09-08 로그 실측: "씬이 저장된 적 없어" + "씬 저장에 실패" — 씬 파일이 어제 것으로
    //    멈춰 있었고, 사장님이 보시는 화면과 저장소가 달랐다. 여기서 실제 씬을 열고 시작한다.
    static bool EnsureSampleSceneOpen()
    {
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (active.path == ScenePath) return true;

        if (!EditorGuards.Dialog(Title,
                (string.IsNullOrEmpty(active.path) ? "지금 열린 씬이 저장된 적 없는(Untitled) 씬입니다." : $"지금 열린 씬이 {active.path}입니다.") +
                $"\n\n맵은 {ScenePath}에 만들어야 저장됩니다. 그 씬을 열고 계속할까요?",
                "열고 계속", "취소"))
            return false;

        // 무인 실행(ClaudeCommands)에선 저장 여부를 물을 사람이 없다 — 저장 안 된 다른 씬을 조용히 버리지 않게 멈춘다.
        if (EditorGuards.IsUnattended)
        {
            if (active.isDirty)
            {
                Debug.LogError($"[맵] 저장 안 된 씬({active.path})이 열려 있어 무인 맵 생성을 멈춥니다.");
                return false;
            }
        }
        else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return true;
    }

    // 편집 중(재생 전) 씬 뷰에서도 인형이 Idle 자세로 보이게 한 번 평가해 둔다.
    // 실행 중 자세는 살아 있는 Animator가 책임진다 — 여긴 보조다.
    //
    // ⚠️ Animator.Update()·Play()는 에디터 모드에서 포즈를 **안 쓴다**(2026-09-08 사장님
    //    스크린샷 — Rebind+Update 두 번으로 바꾼 뒤에도 전부 T자였다). 에디터에서 Humanoid
    //    클립을 실제로 뼈에 쓰는 검증된 길은 PlayableGraph다 — 타임라인 미리보기가 쓰는 것과
    //    같은 경로라 아바타 리타게팅까지 탄다. 평가 뒤 그래프를 지워도 뼈에 쓰인 값은 남는다.
    static void PoseAsIdle(GameObject figure)
    {
        Animator animator = figure.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // 공용 Idle은 Humanoid 클립이라 아바타를 거쳐 옮겨진다. 아바타가 성립 안 하면
        // 결과가 정의되지 않으므로 아예 손대지 않는다 — 바인드 포즈로 두는 게 낫다.
        if (!animator.isHuman || animator.avatar == null || !animator.avatar.isValid
            || animator.GetBoneTransform(HumanBodyBones.Hips) == null)
            return;

        AnimationClip idle = FindIdleClip(animator);
        if (idle == null) return;

        // 자세를 입히면 뼈가 움직인다. 터졌을 때 되돌리려면 먼저 적어 둬야 한다(아래 참고).
        Transform[] bones = figure.GetComponentsInChildren<Transform>(true);
        var saved = new (Vector3 pos, Quaternion rot, Vector3 scale)[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            saved[i] = (bones[i].localPosition, bones[i].localRotation, bones[i].localScale);

        // 🔴 Animator는 렌더러가 **보이지 않으면 뼈를 안 쓴다**(기본 컬링 CullUpdateTransforms).
        //    맵 생성 중의 인형은 아직 한 번도 그려진 적이 없어 "안 보임"이다 — Update()도,
        //    PlayableGraph도 예외 없이 지나가면서 아무것도 안 썼다(2026-09-08, 로그에 실패 기록
        //    0건인데 전부 T자). 평가하는 동안만 항상 계산하게 푼다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;

        PlayableGraph graph = PlayableGraph.Create("맵 인형 자세");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "자세", animator);
            AnimationClipPlayable clip = AnimationClipPlayable.Create(graph, idle);
            clip.SetTime(0.0);
            output.SetSourcePlayable(clip);
            graph.Evaluate(0f);   // 여기서 뼈 Transform에 실제로 써진다
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[맵] {figure.name} 대기 자세 평가 실패 — T자로 둡니다: {e.Message}");
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
        }

        // 🔴 리타게팅이 **터지는** 모델이 있다. 실패로 안 끝나고 뼈가 사방으로 흩어진다.
        //    2026-09-09 실측(사장님 「검은 물체 안사라지는데」): 안흔함_박준희(사이렌헤드)의
        //    인형은 뼈 구름이 437까지 퍼져 있었다 — 다른 인형은 전부 4~5다.
        //    그 상태로 키를 맞추면 무엇을 기준으로 재든 표를 통째로 덮는 크기가 나온다.
        //    크기 검사로는 못 막는다(원인이 크기가 아니라 자세다). **자세를 되돌린다** —
        //    바인드 포즈(T자)는 보기 아쉬워도 표를 가리지는 않는다.
        if (PoseExploded(figure, bones))
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = saved[i].pos;
                bones[i].localRotation = saved[i].rot;
                bones[i].localScale = saved[i].scale;
            }
            Debug.LogWarning($"[맵] {figure.name}: Idle을 입히니 뼈가 사방으로 흩어졌습니다 " +
                             "(리타게팅 실패). 바인드 포즈로 되돌렸습니다 — " +
                             "이 모델은 아바타를 다시 봐야 합니다.", figure);
            return;
        }

        // 실패는 조용하다. 여기서 한 번 재서 이름을 남긴다 — 수백 개 중 어느 게 굳었는지 눈으로 못 고른다.
        if (animator.isHuman && !PoseLooksApplied(animator))
            Debug.LogWarning($"[맵] {figure.name} — Idle을 평가했는데 팔이 아직 수평이다(T자). 아바타·클립을 의심할 것.");
    }

    /// <summary>
    /// 자세를 입힌 뒤 뼈가 메시보다 터무니없이 넓게 흩어졌는지 본다.
    ///
    /// 정상적인 자세에서 뼈는 메시 안이나 그 언저리에 있다. 리타게팅이 터지면 뼈가
    /// 사방으로 날아가는데, 스킨 메시가 그걸 따라가므로 화면에서는 거대한 검은 덩어리가 된다.
    /// 메시 자산의 경계(sharedMesh.bounds)는 자세와 무관한 고정값이라 기준으로 쓸 수 있다.
    /// </summary>
    static bool PoseExploded(GameObject figure, Transform[] bones)
    {
        if (!TryMeasureFigure(figure, out Bounds mesh)) return false;

        float meshSize = Mathf.Max(mesh.size.x, Mathf.Max(mesh.size.y, mesh.size.z));
        if (meshSize < 1e-6f) return false;

        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (Transform bone in bones)
        {
            if (bone == null) continue;
            min = Vector3.Min(min, bone.position);
            max = Vector3.Max(max, bone.position);
        }
        Vector3 spread = max - min;
        float boneSize = Mathf.Max(spread.x, Mathf.Max(spread.y, spread.z));

        // 뼈가 메시의 몇 배까지 퍼져도 봐줄 것인가. 무기·머리카락 뼈가 조금 삐져나오는 건
        // 흔하므로 넉넉히 잡는다. 박준희는 이 값이 90배 언저리였다.
        return boneSize > meshSize * BoneSpreadLimit;
    }

    const float BoneSpreadLimit = 6f;

    // T자(바인드 포즈)는 양팔이 좌우로 곧게 뻗어 있다. "손이 위팔보다 얼마나 내려왔는가"로 가른다.
    //
    // 🔴 세계 좌표의 Y로 재면 안 된다. 모델이 돌아가 있으면(자동 세우기가 90°·180° 돌린다)
    //    팔이 제대로 내려와 있어도 세계 Y 차이가 안 난다 — 2026-09-08 실측: 김수빈·박준희만
    //    T자 경고 9건이 떴는데, 그 둘이 정확히 자동 세우기가 회전시킨 유닛이었다.
    //    **모델 자신의 위쪽**(골반→머리)을 기준으로 잰다. 회전과 무관해진다.
    static bool PoseLooksApplied(Animator animator)
    {
        Transform upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (upperArm == null || hand == null || hips == null || head == null) return true;

        Vector3 modelUp = head.position - hips.position;
        if (modelUp.sqrMagnitude < 1e-6f) return true;   // 못 재면 경고하지 않는다
        modelUp.Normalize();

        // 손이 위팔보다 "모델 기준 아래"로 얼마나 내려왔는가.
        //
        // 🔴 문턱을 humanScale에 비례시키면 안 된다. 인형은 FitToHeight가 목표 키(17/20)에
        //    맞춰 크게 키우는데(수백~수천 배), humanScale은 **원본 모델**의 크기를 따른다 —
        //    둘이 따로 놀아서 모델마다 기준이 제멋대로가 된다.
        //    2026-09-09 실측: 김경현과 신문철은 뼈·매핑이 똑같은데(mixamorig 66개, 52매핑)
        //    김경현만 경고가 떴다. 파일 크기·뼈 수·리그 종류 어느 것과도 상관이 없었다.
        //    → **몸통 길이(골반→머리)에 대한 비율**로 잰다. 스케일과 무관해진다.
        float bodyLength = (head.position - hips.position).magnitude;
        if (bodyLength < 1e-4f) return true;

        float drop = Vector3.Dot(upperArm.position - hand.position, modelUp);

        // 팔을 내린 사람은 손이 위팔보다 몸통의 0.25배쯤 아래에 있다(어깨~손목 ≈ 상체 길이).
        // T자는 그 값이 0 근처다. 0.12로 넉넉히 잡아 A자 포즈까지 통과시킨다.
        return drop / bodyLength > 0.12f;
    }

    static AnimationClip FindIdleClip(Animator animator)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null) return null;

        foreach (AnimationClip clip in controller.animationClips)
            if (clip != null && clip.name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;

        return controller.animationClips.Length > 0 ? controller.animationClips[0] : null;
    }

    static string IngredientName(RecipeIngredient ingredient)
    {
        switch (ingredient.kind)
        {
            case IngredientKind.SpecificUnit:
                return ingredient.unit != null ? ingredient.unit.unitName : "?";
            case IngredientKind.SpecificItem:
                return ingredient.item != null ? ingredient.item.name : "?";
            default:
                return $"{ingredient.wildcardGrade.KoreanName()}아무거나";
        }
    }

    static Color IngredientColor(RecipeIngredient ingredient)
    {
        if (ingredient.kind == IngredientKind.SpecificUnit && ingredient.unit != null)
            return GradeColor(ingredient.unit.grade);

        if (ingredient.kind == IngredientKind.UnitGradeWildcard)
            return GradeColor(ingredient.wildcardGrade) * 0.7f;   // 지정 유닛과 구분되게 어둡게

        return new Color(0.55f, 0.45f, 0.35f);   // 아이템
    }

    static List<CombineRecipe> LoadRecipesProducing(UnitGrade grade)
    {
        return AssetDatabase.FindAssets("t:CombineRecipe", new[] { "Assets/Data/Recipes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CombineRecipe>)
            .Where(recipe => recipe != null && recipe.result != null && recipe.result.grade == grade)
            .OrderBy(recipe => recipe.result.unitName, System.StringComparer.Ordinal)
            .ToList();
    }

    static List<UnitData> LoadUnitsOfGrade(UnitGrade grade)
    {
        return AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<UnitData>)
            .Where(unit => unit != null && unit.grade == grade)
            .OrderBy(unit => unit.unitName, System.StringComparer.Ordinal)
            .ToList();
    }

    // 사용자가 정한 등급 색. 하단 HUD의 유닛 카드와 같은 규칙을 쓴다.
    // 색 정의는 UnitGradeExtensions 한 곳에만 있다 — 하단 명령 그리드도 같은 것을 쓴다.
    static Color GradeColor(UnitGrade grade) => grade.Color();

    const float SlotSmoothness = 0.2f;

    /// <summary>PaintGlow와 같은 이유로 가져온 뒤에도 다시 쓴다 — 매끄러움은 이름에 안 들어간다.</summary>
    static void PaintSolid(GameObject obj, Color color)
    {
        string path = $"{MaterialFolder}/slot_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;
        if (isNew)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", SlotSmoothness);

        if (isNew) AssetDatabase.CreateAsset(material, path);
        else SaveChanged(material);

        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    // 특수지급 칸의 한 자리. 대부분은 지정 유닛을 주지만, 목재 자리처럼 유닛이 아닌 걸 주는 자리가 섞여 있다.
    // 이름 배열 두 개를 나란히 두면 자리마다 "무엇을 주는지"를 null 여부로만 구분하게 되어,
    // 유닛이 아닌 지급물이 늘어날수록 구분이 불가능해진다. 자리 하나를 한 값으로 묶는다.
    struct SpecialSlot
    {
        public string label;
        public string[] unitAssets;   // 로스터 에셋 이름. 한 자리가 여러 유닛을 함께 줄 수 있다
        public bool givesResources;   // 골드 + 목재를 함께

        public static SpecialSlot Units(string label, params string[] unitAssets) =>
            new SpecialSlot { label = label, unitAssets = unitAssets };

        public static SpecialSlot Resources(string label) =>
            new SpecialSlot { label = label, givesResources = true };

        // 지급물의 형태가 아직 안 정해진 자리. 표식만 세우고 생성 보고에 남긴다.
        public static SpecialSlot Pending(string label) =>
            new SpecialSlot { label = label };
    }

    struct GachaBand
    {
        public string label;
        public UnitGrade grade;
        public UnitGrade bonusGrade;
        public float bonusChance;
        public SpecialSlot[] specialSlots;   // null이면 등급 랜덤 칸

        public static GachaBand Random(string label, UnitGrade grade) =>
            new GachaBand { label = label, grade = grade };

        public static GachaBand RandomWithBonus(string label, UnitGrade grade, UnitGrade bonus, float chance) =>
            new GachaBand { label = label, grade = grade, bonusGrade = bonus, bonusChance = chance };

        public static GachaBand Special(string label, params SpecialSlot[] slots) =>
            new GachaBand { label = label, specialSlots = slots };
    }

    // 특수 칸은 원작처럼 세 자리다(사장님 확정 2026-09-01): 돈+목재 / 박은석 초월위습 / 레일리+배.
    // 박은석이 가운데다 — 원작 쿠마 초월함 위습에 해당하는 자리라 눈에 먼저 들어와야 한다.
    // 레일리(이승우)와 배(상붕카)는 원작에서 한 자리에서 같이 나오므로 한 칸에 묶었다.
    // 이 줄은 스토리 8 이후 《백수생활》 5분 동안만 열린다 — GateToInterlude()가 이미 그 개폐를 붙인다.
    static readonly GachaBand[] GachaBands =
    {
        GachaBand.Random("안흔함", UnitGrade.Uncommon),
        GachaBand.Random("특별함", UnitGrade.Special),
        GachaBand.RandomWithBonus("희귀함·특수함", UnitGrade.Rare, UnitGrade.Superior, 3f),
        GachaBand.Special("특수지급",
            SpecialSlot.Resources("돈+목재"),
            SpecialSlot.Units("박은석 초월위습", "초월위습_박은석"),
            SpecialSlot.Units("레일리+배", "희귀함_이승우", "안흔함_상붕카")),
        GachaBand.Random("전설·히든", UnitGrade.Legendary),
    };

    // 뽑기 섬. 원작처럼 위쪽에 흔함 유닛을 가로로 늘어놓고, 그 아래에 등급별 랜덤 포탈을 둔다.
    // 흔함 위습은 "선택"이라 원하는 유닛 칸에 넣고, 그 위 등급은 칸 하나에서 등급 내 랜덤이 나온다.
    // 원작 비율 5단계(PM 지시 2026-09-23) — DressPortal이 diameter÷모델크기로 배율을 구해서
    // (StructureDresser.cs:229) 이 지름만 키우면 마법진이 같이 커진다. 포탈 배치 계산(armX 등)도
    // 전부 이 상수를 직접 참조해 자동으로 따라간다.
    const float PortalDiameter = 9f * MapLayout.Scale;
    const float ChoicePortalDiameter = 6.5f * MapLayout.Scale;

    // 흔함 선택 칸은 원작처럼 칸마다 벽을 둘러 부스로 만든다.
    /// <summary>
    /// 위습이 **판정으로 차지하는** 반지름. `WispPrefab`의 `SphereCollider.radius 0.5` ×
    /// 루트 배율(`WispScale`)이다. **위습이 들어갈 자리는 전부 이 값에서 유도한다** —
    /// 맵 배율이 아니라 위습 크기에 묶인 값이기 때문이다(흙길 폭이 적 지름에 묶인 것과 같은 규칙).
    ///
    /// 🔴 2026-09-24 정정: 예전엔 `WispDiameter = 0.6 × WispScale`(=30)을 썼고 주석에
    ///    「프리팹 기본 몸이 0.6」이라고 적혀 있었다. **프리팹을 열어 보니 아니다** —
    ///    몸 메시가 루트에 붙어 있고 루트 배율이 50이라 **보이는 지름도 콜라이더 지름도 50**이다.
    ///    그래서 자리 계산이 전부 **반지름을 10씩 적게** 잡고 있었고, 위습이 생기자마자
    ///    포탈 트리거에 닿아 **먹혔다**(흔함 선택 위습이 10턴 중 9턴 0기).
    ///    그 전까지 안 터진 이유는 **포탈 트리거 높이가 모자라서**였다 — 높이를 고치자(4c8ab5ff)
    ///    가로 거리 결함이 드러났다. **하나를 고치니 그 뒤에 있던 게 나왔다.**
    /// </summary>
    const float WispColliderRadius = 0.5f * WispScale;

    /// <summary>
    /// 위습 생성 자리와 포탈 사이 최소 거리. **두 반지름 합 + 여유**다.
    /// ⚠️ 포탈마다 지름이 달라서 **포탈 종류별로 따로 둔다.** 하나로 뭉치면 큰 포탈 쪽이 모자란다.
    /// </summary>
    const float WispCellMargin = 3f;
    const float ChoiceWispGap = ChoicePortalDiameter * 0.5f + WispColliderRadius + WispCellMargin;
    const float BandWispGap = PortalDiameter * 0.5f + WispColliderRadius + WispCellMargin;

    // 위습이 생겨 머무는 칸. 생성 지점에서 위습 반지름만큼 더 내려가도 칸 안이어야 한다(여유 5).
    const float CommonAreaDepth = ChoiceWispGap + WispColliderRadius + 5f;

    const float PortalInset = 9f;       // 칸 위벽에서 포탈까지
    const float BoothDepth = 14f;        // 포탈 앞부터 뒷벽까지
    const float BoothWallHeight = 4.5f;
    const float BoothWallThickness = 0.6f;

    static string BuildGachaPortals(GameObject gachaIsland)
    {
        if (gachaIsland == null) return "";

        GachaTable table = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/MainGachaTable.asset");
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        Transform parent = gachaIsland.transform.parent;

        float left = island.center.x - island.size.x * 0.5f;
        float top = island.center.y + island.size.y * 0.5f;
        float bottom = island.center.y - island.size.y * 0.5f;

        // --- 위쪽 가로줄: 흔함 유닛을 하나씩 고르는 칸 ---
        List<UnitData> commons = LoadUnitsOfGrade(UnitGrade.Common);
        // 부스 뒷벽까지 섬 안에 들어와야 한다 — 포탈은 뒷벽에서 부스 깊이만큼 앞에 놓는다.
        float rowZ = top - BoothDepth - 2f;
        float step = island.size.x / (commons.Count + 1);

        for (int i = 0; i < commons.Count; i++)
        {
            UnitData unit = commons[i];
            float x = left + step * (i + 1);

            GameObject stand = CreatePortalObject(parent, $"흔함선택_{unit.unitName}",
                new Vector3(x, MapLayout.IslandTop + 0.25f, rowZ), ChoicePortalDiameter);
            ConfigurePortal(stand, UnitGrade.Common, unit, table, spawner);

            PlaceUnitMarker(parent, $"흔함선택_{unit.unitName}_표식",
                new Vector3(x, 0f, rowZ + BoothDepth * 0.5f), UnitGrade.Common,
                unit, DisplayFigureHeight);
        }

        // 부스를 하나씩 두르면 이웃끼리 옆벽이 겹친다. 뒷벽 한 장 + 칸막이 한 줄로 세운다.
        BuildBoothRow(parent, left, left + island.size.x, rowZ, step, commons.Count);

        // 흔함 구역 아래를 막는다. 안 막으면 부스 앞 공간이 첫 등급 칸과 이어져,
        // 흔함 위습이 안흔함 칸으로 내려가고 안흔함 위습도 올라온다.
        float commonFloorZ = rowZ - CommonAreaDepth;
        // 섬 끝까지 물리지 않으면 양 끝에 틈이 남는다. 위습 지름이 0.56이라
        // 1짜리 틈으로도 빠져나가고, 그러면 칸 옆 좁은 길을 따라 아래로 내려간다.
        BuildWall(parent, "흔함구역_아래벽",
            new Vector3(island.center.x, MapLayout.IslandTop + WallHeight * 0.5f, commonFloorZ),
            new Vector3(island.size.x + GateThickness, WallHeight, GateThickness));

        // 흔함 선택 위습은 부스 줄 앞, 막힌 구역 안에서 생긴다 — 어느 부스로 갈지는 플레이어가 고른다.
        GameObject commonCell = new GameObject("위습칸_흔함선택");
        commonCell.transform.SetParent(parent, false);
        // 🔴 예전엔 포탈 줄과 아래벽의 **한가운데**(rowZ − 25.8)에 뒀다. 그 자리가 포탈에
        //    너무 가까워, 트리거 높이를 고치자마자(4c8ab5ff) 위습이 **생기자마자 먹혔다**.
        //    한가운데는 「칸 안에 있다」는 뜻일 뿐 **포탈과의 거리를 보장하지 않는다.**
        //    필요 거리에서 유도한다 — 위습이나 포탈 크기가 바뀌어도 따라온다.
        commonCell.transform.position = new Vector3(
            island.center.x, MapLayout.IslandTop, rowZ - ChoiceWispGap);
        commonCell.AddComponent<WispCell>().SetGrade(UnitGrade.Common);

        // --- 왼쪽: 벽으로 나뉜 칸 5줄 ---
        // 원작 구조. 위에서 아래로 등급이 올라가고, 중간에 특수 지급 칸이 하나 낀다.
        float bandTop = commonFloorZ - GateThickness;
        float bandBottom = bottom + 4f;
        float bandHeight = (bandTop - bandBottom) / GachaBands.Length;
        float columnLeft = left + 2f;
        // 왼쪽 등급 칸 열과 오른쪽 전시 칸의 경계. 전시 칸이 다른세계 조합식 한 줄을
        // 통째로 담아야 해서 섬 가운데보다 왼쪽에 둔다(왼쪽 열 폭은 예전 그대로다).
        float columnRight = island.center.x - 4f;

        List<string> specialPending = new List<string>();

        // 칸 경계는 칸 수보다 하나 적다. 열 테두리는 아래에서 한 번에 세운다.
        List<float> bandDividers = new List<float>();
        for (int i = 1; i < GachaBands.Length; i++)
            bandDividers.Add(bandTop - bandHeight * i);
        BuildCellColumn(parent, "뽑기칸", columnLeft, columnRight, bandTop, bandBottom, bandDividers);

        for (int b = 0; b < GachaBands.Length; b++)
        {
            GachaBand band = GachaBands[b];
            float cellTop = bandTop - bandHeight * b;
            // 원작처럼 포탈은 칸 위쪽에 붙이고, 위습은 그 아래에서 생겨 포탈로 올라간다.
            float portalZ = cellTop - PortalInset;
            float bandCenterZ = portalZ;

            // 칸을 사방으로 막고 오른쪽 가운데만 입구로 연다.
            // 안 막으면 위습이 한 칸에 들어갔다가 옆 칸 포탈로 흘러가 엉뚱한 등급이 나온다.
            // 이 칸에서 생길 위습의 등급을 표시한다.
            // 특수 칸은 등급 랜덤이 아니라 《백수생활》에 한 번 주는 선택 위습을 받는다 —
            // 그 위습이 여기 생겨야 플레이어가 셋 중 하나로 끌고 갈 수 있다.
            GameObject cell = new GameObject($"위습칸_{band.label}");
            cell.transform.SetParent(parent, false);
            // ⚠️ 이 줄의 포탈은 PortalDiameter(큰 것)라 BandWispGap을 쓴다. 흔함선택 쪽과 값이 다르다 —
            //    하나로 뭉치면 큰 포탈 쪽이 모자라서 위습이 생기자마자 먹힌다(2026-09-24).
            cell.transform.position = new Vector3((columnLeft + columnRight) * 0.5f,
                                                  MapLayout.IslandTop, portalZ - BandWispGap);
            cell.AddComponent<WispCell>().SetGrade(
                band.specialSlots == null ? band.grade : InterludeChoiceGrade);

            if (band.specialSlots == null)
            {
                GameObject portal = CreatePortalObject(parent, $"Portal_{band.label}",
                    new Vector3((columnLeft + columnRight) * 0.5f, MapLayout.IslandTop + 0.25f, bandCenterZ),
                    PortalDiameter);
                ConfigurePortal(portal, band.grade, null, table, spawner);

                if (band.bonusChance > 0f) ApplyBonusGrade(portal, band.bonusGrade, band.bonusChance);
                continue;
            }

            // 특수 칸: 지정 유닛·자원을 주는 자리를 가로로 늘어놓는다.
            float slotStep = (columnRight - columnLeft) / (band.specialSlots.Length + 1);
            for (int i = 0; i < band.specialSlots.Length; i++)
            {
                SpecialSlot slot = band.specialSlots[i];
                Vector3 at = new Vector3(columnLeft + slotStep * (i + 1),
                                         MapLayout.IslandTop + 0.25f, bandCenterZ);

                if (slot.givesResources)
                {
                    // 이 줄은 《백수생활》에만 열린다. 게이트가 콜라이더를 끄고 색을 어둡게 한다.
                    // 한 자리에서 골드와 목재를 함께 준다. 포탈 하나가 두 자원을 못 주므로
                    // 같은 자리에 겹쳐 세운다 — 위습이 들어오면 둘 다 지급된다.
                    // 목재는 자원 칸 서쪽 포탈과 같은 조건이다(WISP_SYSTEM.md: 66% 확률로 목재 1개).
                    // ⚠️ 여긴 아래 "자원 칸"(RandomUnit)과 다른 위습이다 — 《백수생활선택》
                    // 위습(InterludeChoiceGrade=Transcendent, 실제 등급 의미 없는 라우팅 키)만
                    // 받는다. 여기도 RandomUnit을 넣으면 그 위습이 갈 곳이 아예 없어진다.
                    GateToInterlude(BuildResourcePortal(parent, $"Portal_{slot.label}_엔",
                        new Vector3(at.x, 0f, at.z),
                        ResourcePortal.Payout.Gold, ResourceType.Wood, 15, 20, 100f,
                        InterludeChoiceGrade, ChoicePortalDiameter));
                    GateToInterlude(BuildResourcePortal(parent, $"Portal_{slot.label}_목재",
                        new Vector3(at.x, 0f, at.z),
                        ResourcePortal.Payout.Resource, ResourceType.Wood, 1, 0, 66f,
                        InterludeChoiceGrade, ChoicePortalDiameter));
                    continue;
                }

                if (slot.unitAssets == null || slot.unitAssets.Length == 0)
                {
                    // 지급물의 형태가 아직 안 정해진 자리(박은석 초월위습). 자리만 세우고 보고에 남긴다.
                    PlaceUnitMarker(parent, $"미구현_{slot.label}", new Vector3(at.x, 0f, at.z),
                        UnitGrade.RandomUnit);
                    specialPending.Add(slot.label);
                    continue;
                }

                // 한 자리가 여러 유닛을 줄 수 있다(레일리+배). 포탈을 나란히 겹쳐 세우지 않고
                // 자리 안에서 살짝 벌려, 어느 유닛이 나오는지 눈으로 구분되게 한다.
                float spread = ChoicePortalDiameter * 0.55f;
                float first = at.x - spread * (slot.unitAssets.Length - 1) * 0.5f;

                for (int u = 0; u < slot.unitAssets.Length; u++)
                {
                    UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(
                        $"Assets/Data/Units/Roster/{slot.unitAssets[u]}.asset");
                    if (unit == null)
                    {
                        specialPending.Add($"{slot.label}({slot.unitAssets[u]} 없음)");
                        continue;
                    }

                    GameObject portal = CreatePortalObject(parent, $"Portal_{unit.unitName}",
                        new Vector3(first + spread * u, at.y, at.z), ChoicePortalDiameter);
                    // acceptedGrades는 "이 포탈이 받는 위습의 등급"이지 지급할 유닛의 등급이 아니다 —
                    // 이 줄에 들어오는 위습은 전부 백수생활 선택위습(InterludeChoiceGrade)이다.
                    // unit.grade를 넘기면(레일리=희귀함, 상붕카=안흔함) 그 등급으로 필터링돼
                    // 선택위습(초월함)을 영원히 거부한다 — 겉보기엔 배선됐지만 실제로는 안 열리는 상태였다.
                    ConfigurePortal(portal, InterludeChoiceGrade, unit, table, spawner);
                    GateToInterlude(portal);
                }
            }
        }

        // --- 오른쪽 세로줄: 조합식 없이 캐릭터만 전시하는 등급 ---
        // 이 등급들은 조합식 표에 올리지 않기로 확정돼 있어서, 여기가 유일하게 눈으로 보는 곳이다.
        float rightColumnLeft = island.center.x - 4f;   // 등급 칸 열의 오른벽과 같은 자리
        float displayLeft = rightColumnLeft + 4f;
        float displayWidth = left + island.size.x - 2f - displayLeft;
        // 전시 격자는 조합표와 분리된 제 간격을 쓴다(DisplaySlotSpacing 주석 참고).
        // 줄당 6칸 고정 — 폭에서 칸 수를 유도하면 섬 크기가 바뀔 때마다 줄 수가 흔들린다.
        float displaySpacing = DisplaySlotSpacing;
        int perRow = DisplayColumns;
        // bandTop은 열 위벽이 서는 자리다. 거기서 바로 시작하면 첫 줄이 벽에 끼인다.
        float displayZ = bandTop - displaySpacing;
        int displayed = 0;

        int recipeRows = 0;

        foreach (UnitGrade grade in MapLayout.GachaDisplayGrades)
        {
            // 다른세계는 조합으로만 나오는 등급인데 조합식 표에는 안 올리기로 했다.
            // 캐릭터만 세워두면 만드는 법을 볼 데가 없어서, 이 칸에서 한 줄씩 보여준다.
            if (System.Array.IndexOf(MapLayout.GachaRecipeGrades, grade) >= 0)
            {
                List<CombineRecipe> recipes = LoadRecipesProducing(grade);
                if (recipes.Count == 0) continue;

                float blockDepth = recipes.Count * RecipeRowHeight;
                int blockMaxSlots = 0;
                int blockMaxCostIcons = 0;
                foreach (CombineRecipe recipe in recipes)
                {
                    blockMaxSlots = Mathf.Max(blockMaxSlots, RecipeSlotCount(recipe));
                    blockMaxCostIcons = Mathf.Max(blockMaxCostIcons, CountCostIcons(recipe));
                }

                float rowLeftX = displayLeft + ColumnPad + CostSlot * 0.5f;
                float costWidth = blockMaxCostIcons * (CostSlot + CostGap) + CostBlockGap;
                float blockResultX = rowLeftX + costWidth
                                     + blockMaxSlots * (RecipeSlot + RecipeGap) + RecipeArrowGap;
                float blockWidth = Mathf.Min(
                    blockResultX + RecipeSlot * 0.5f + ColumnPad - displayLeft, displayWidth);

                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"뽑기섬_조합식_{grade.KoreanName()}";
                strip.transform.SetParent(parent, false);
                strip.transform.position = new Vector3(displayLeft + blockWidth * 0.5f,
                    MapLayout.IslandTop + 0.06f, displayZ + RecipeRowHeight * 0.5f - blockDepth * 0.5f);
                strip.transform.localScale = new Vector3(blockWidth, 0.12f, blockDepth);
                Paint(strip, "combine", blockWidth, blockDepth);
                Object.DestroyImmediate(strip.GetComponent<Collider>());

                foreach (CombineRecipe recipe in recipes)
                {
                    PlaceRecipeRow(parent, recipe, rowLeftX, displayZ, blockResultX,
                                   showCosts: true, costBlockWidth: costWidth);
                    displayZ -= RecipeRowHeight;
                    recipeRows++;
                }

                displayZ -= displaySpacing;
                continue;
            }

            List<UnitData> units = LoadUnitsOfGrade(grade);

            for (int i = 0; i < units.Count; i++)
            {
                float x = displayLeft + (i % perRow) * displaySpacing + displaySpacing * 0.5f;
                float z = displayZ - (i / perRow) * displaySpacing;
                // 스킨이 있으면 색 큐브 대신 인형을 세운다(2026-09-23 사장님 「랜덤유닛도 배치해」).
                // 키는 레인 유닛과 같은 DisplayFigureHeight — 조합표·전시와 같은 규칙이다.
                // 스킨이 아직 없는 종은 PlaceUnitMarker가 **같은 키의** 자리표시로 세운다.
                PlaceUnitMarker(parent, $"{grade.KoreanName()}_{units[i].unitName}",
                    new Vector3(x, 0f, z), grade, units[i], DisplayFigureHeight);
                displayed++;
            }

            // 다음 등급은 한 줄 띄고 이어서 — 등급 경계가 보이게 한다.
            displayZ -= (Mathf.CeilToInt(units.Count / (float)perRow) + 1) * displaySpacing;
        }

        float displayRight = left + island.size.x - 2f;

        // 자원 칸은 동서남북 포탈을 두려면 정사각형에 가까워야 한다.
        // 열 아래쪽에서 폭만큼 떼어 쓰고, 남는 위쪽 전부를 전시 칸으로 준다.
        float hubHeight = displayRight - rightColumnLeft;
        float hubTop = bandBottom + hubHeight;

        // 전시와 자원은 위아래로 붙은 한 열이다. 칸막이 한 장으로 나눈다.
        BuildCellColumn(parent, "오른열", rightColumnLeft, displayRight, bandTop, bandBottom,
                        new List<float> { hubTop }, skipLeftWall: true);

        string resourceReport = BuildResourceHub(parent,
            rightColumnLeft, displayRight, hubTop, bandBottom);

        float displayDepth = bandTop - displayZ;
        float available = bandTop - bandBottom;
        string fit = displayDepth <= available
            ? $"여유 {available - displayDepth:F0}"
            : $"⚠️ {displayDepth - available:F0} 모자람";

        string pending = specialPending.Count > 0
            ? $"\n  ⚠️ 특수지급 {specialPending.Count}칸({string.Join(", ", specialPending)})은 " +
              "지급물의 형태가 정해지지 않아 자리만 표시했습니다."
            : "";

        float widestRecipe = MaxRecipeRowWidth(MapLayout.GachaRecipeGrades, true);
        string recipeFit = recipeRows == 0 || widestRecipe <= displayWidth
            ? ""
            : $"\n  ⚠️ 다른세계 조합식({widestRecipe:F0})이 칸 폭({displayWidth:F0})을 넘습니다";

        return $"\n뽑기 섬: 흔함 선택 {commons.Count}칸, 등급 칸 {GachaBands.Length}줄, " +
               $"전시 {displayed}종 + 조합식 {recipeRows}줄 (깊이 {displayDepth:F0}/{available:F0}, {fit})." +
               pending + recipeFit + resourceReport;
    }

    // 자원 포탈 칸. 원작의 자원 섬을 한 칸으로 옮긴 것 —
    // 가운데에서 위습이 생기고, 플레이어가 동서남북 네 포탈 중 하나로 끌고 간다.
    // 넷으로 칸을 쪼개면 위습이 어느 칸에 생기느냐가 선택을 대신해버려서 고를 여지가 없어진다.
    static string BuildResourceHub(Transform parent, float xLeft, float xRight, float zTop, float zBottom)
    {
        GachaTable table = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/MainGachaTable.asset");
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);

        float centerX = (xLeft + xRight) * 0.5f;
        float centerZ = (zTop + zBottom) * 0.5f;
        float armX = (xRight - xLeft) * 0.5f - PortalDiameter * 0.5f - 2f;
        float armZ = (zTop - zBottom) * 0.5f - PortalDiameter * 0.5f - 2f;
        float y = MapLayout.IslandTop + 0.25f;

        // 북: 랜덤 위습을 넣으면 흔함 유닛이 하나 나온다. 게임 시작 위습 5개가 여기로 들어간다.
        // 0.24%로 해적선(h060) — 등급이 아니라 특정 유닛이라 bonusUnit 쪽을 쓴다.
        // ⚠️ 2026-09-06 정정(리서치담당 원문 확인, 3912c33·bedb45d): 이 포탈의 보너스
        // 분기는 원작에 이것 하나뿐이다(GetRandomPercentageBJ()<=0.24 → h060, else → 흔함
        // 9종). "안흔함_상붕카"는 커뮤니티 문서로 만든 자리표였고 실제로는 h060(HP10·
        // 공격510·사거리600·비행·[히든])과 전혀 다른 스탯(HP180·공격90·안흔함)이었다 —
        // 확률도 1%는 실제(0.24%)의 약 4배 과다였다. 안흔함 상붕카는 이 포탈에서 아예
        // 나오지 않는 게 원작이라 교체(추가 아님)한다.
        GameObject unitRandom = CreatePortalObject(parent, "Portal_유닛랜덤",
            new Vector3(centerX, y, centerZ + armZ), PortalDiameter);
        // 받는 건 랜덤유닛 위습(시작에 5개 받는 그것), 주는 건 흔함 유닛이다.
        // 둘을 같은 값으로 두면 위습이 거부당하거나 랜덤유닛 등급에서 뽑힌다.
        ConfigurePortal(unitRandom, UnitGrade.RandomUnit, null, table, spawner,
                        rewardGrade: UnitGrade.Common);
        ApplyBonusUnit(unitRandom, "해적선", RandomShipBonusChance);

        // 동: 금화 랜덤 — 원작 그대로 "15 + 라운드×12~35"(2026-09-04, ORD11.089.w3x 확인).
        // 예전엔 범위를 20 하나로 뭉개뒀는데, 그 폭이 원작 골드포탈의 도박성 그 자체다.
        // 이 셋(금화·목재·마나)은 위 "위습칸_자원"이 뿌리는 RandomUnit 등급 위습만 받는다.
        BuildResourcePortal(parent, "Portal_금화랜덤", new Vector3(centerX + armX, 0f, centerZ),
            ResourcePortal.Payout.Gold, ResourceType.Wood, 15, 12, 100f, UnitGrade.RandomUnit, perRoundMax: 35);

        // 서: 목재 랜덤 — 원작은 66% 확률로 목재 1개.
        BuildResourcePortal(parent, "Portal_목재랜덤", new Vector3(centerX - armX, 0f, centerZ),
            ResourcePortal.Payout.Resource, ResourceType.Wood, 1, 0, 66f, UnitGrade.RandomUnit);

        // 남: 도움소 마나 — 원작은 "20 + 라운드×1.5 회복".
        // 마나를 쓰는 도움소 건물은 아직 없지만, 자원은 지금부터 쌓아둔다.
        // 원작 확정 공식(2026-09-04, ORD11.089.w3x Trig_Random_Mana 직접 확인): 20 + 라운드×1.5.
        BuildResourcePortal(parent, "Portal_도움소마나", new Vector3(centerX, 0f, centerZ - armZ),
            ResourcePortal.Payout.Resource, ResourceType.Mana, 20, 1.5f, 100f, UnitGrade.RandomUnit);

        // 가운데에서 위습이 생긴다. 여기서 어느 포탈로 갈지는 플레이어가 정한다.
        GameObject cell = new GameObject("위습칸_자원");
        cell.transform.SetParent(parent, false);
        cell.transform.position = new Vector3(centerX, MapLayout.IslandTop, centerZ);
        cell.AddComponent<WispCell>().SetGrade(UnitGrade.RandomUnit);

        return "\n자원 칸: 가운데 위습 → 북 흔함 유닛(1% 상붕카) · 동 금화 · 서 목재 · 남 마나.";
    }

    // 도박소. 뽑기 섬과 달리 위습을 안 쓴다 — 도박은 목재만 있으면 반복해서 돌리는 행위라
    // 위습 스폰→드래그 조작을 넣으면 판당 마찰이 너무 크다(GAMBLING.md). 포탈을 직접 클릭한다.
    // 도박소 수치는 이제 Assets/Data/Gambling/의 GamblingOptionData 에셋에 있다 —
    // 여기 있던 GamblingTier 표는 그쪽으로 옮겨갔다. 건물 배치는 에셋이 준비되면 붙인다.

    // diameter는 자원 칸(넓은 포탈)과 뽑기 섬 특수지급 칸(좁은 선택 포탈)이 서로 다른 크기를 쓴다.
    // ⚠️ 2026-09-05 정정(사장님이 게임을 돌려서 발견): acceptedGrades를 안 채우면
    // ResourcePortal.Accepts가 "비어 있으면 전부 허용"이라 초월함위습도 목재 포탈에서
    // 받아버렸다(PM 재조사) — Accepts 자체의 그 기본값은 다른 포탈이 기대고 있을 수 있어
    // 안 건드리고, 여기서 값을 채우는 쪽으로 고쳤다. 그래서 acceptedGrade를 필수 인자로
    // 뺐다 — 앞으로 새 자원 포탈을 추가할 때 등급을 빠뜨리면 컴파일 단계에서 걸린다.
    static GameObject BuildResourcePortal(Transform parent, string name, Vector3 ground,
                                    ResourcePortal.Payout payout, ResourceType resource,
                                    int baseAmount, float perRound, float chance,
                                    UnitGrade acceptedGrade,
                                    float diameter = PortalDiameter, float perRoundMax = 0f)
    {
        GameObject portal = CreatePortalObject(parent, name,
            new Vector3(ground.x, MapLayout.IslandTop + 0.25f, ground.z), diameter);

        ResourcePortal component = portal.AddComponent<ResourcePortal>();

        SerializedObject so = new SerializedObject(component);
        so.FindProperty("payout").enumValueIndex = (int)payout;
        so.FindProperty("resourceType").enumValueIndex = (int)resource;
        so.FindProperty("baseAmount").intValue = baseAmount;
        so.FindProperty("perRound").floatValue = perRound;
        so.FindProperty("perRoundMax").floatValue = perRoundMax;
        so.FindProperty("successChancePercent").floatValue = chance;
        so.ApplyModifiedProperties();

        // ⚠️ acceptedGrades(List<UnitGrade>)만은 SerializedProperty를 안 거친다 — UnitPortal과
        // 같은 이유(ResourcePortal.SetAcceptedGrade 코멘트 참고). so.ApplyModifiedProperties()
        // 뒤에 마지막으로 불러서, 혹시 모를 SerializedObject 왕복에 덮이지 않게 한다.
        // SetDirty를 반드시 같이 부른다.
        component.SetAcceptedGrade(acceptedGrade);
        EditorUtility.SetDirty(component);
        return portal;
    }

    // 스토리존. 가운데 단상에서 스토리 적이 나오고, 플레이어는 자기 레인에서 유닛을 보내 잡는다.
    static string BuildStoryZone(Transform parent)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "StoryZone");

        Vector3 center = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);

        // 포석 광장(지름 66, 윗면 1, 가운데 45×45는 스토리 적 건물 자리). 모델이 없으면 옛 원기둥.
        if (StructureDresser.PlaceStoryPlaza(parent, center) < 0f)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "스토리_단상";
            platform.transform.SetParent(parent, false);
            platform.transform.position = new Vector3(island.center.x, MapLayout.IslandTop + 0.15f, island.center.y);
            // 단상은 섬 크기에 비례하게 — 섬을 키울 때마다 따로 고치지 않아도 되게.
            float platformSize = Mathf.Min(island.size.x, island.size.y) * 0.35f;
            platform.transform.localScale = new Vector3(platformSize, 0.3f, platformSize);
            Paint(platform, "rock", platformSize, platformSize);
            Object.DestroyImmediate(platform.GetComponent<Collider>());
        }

        GameObject spawn = new GameObject("스토리_등장지점");
        spawn.transform.SetParent(parent, false);
        spawn.transform.position = center + Vector3.up * StructureDresser.StoryPlazaLift;

        StoryManager manager = Object.FindFirstObjectByType<StoryManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject managerObject = new GameObject("StoryManager");
            manager = managerObject.AddComponent<StoryManager>();
        }

        List<StoryData> ordered = AssetDatabase.FindAssets("t:StoryData", new[] { "Assets/Data/Stories" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<StoryData>)
            .Where(story => story != null)
            .OrderBy(story => story.order)
            .ToList();

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("stories");
        list.ClearArray();
        for (int i = 0; i < ordered.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
        }
        so.FindProperty("spawnPoint").objectReferenceValue = spawn.transform;
        so.ApplyModifiedProperties();

        int playable = ordered.Count(story => story.IsPlayable);
        return $"\n스토리존: 스토리 {ordered.Count}개 연결 (적이 정해진 것 {playable}개)." +
               (playable == 0 ? "\n  ⚠️ 적이 정해진 스토리가 없어 아무것도 등장하지 않습니다." : "");
    }

    // 원작처럼 레인 순찰 경로 오른쪽 위 모서리에 스토리존 포탈을 둔다(사장님 지시, 2026-09-03).
    // 처음 5 → 10(밟기 어렵다) → 15(2026-09-03, "크기 1.5배") 순으로 커졌다 — 흙길 폭이
    // 12라 거의 다 덮지만, 적은 WaypointMover가 transform.position을 그대로 옮길 뿐 물리/
    // NavMesh를 전혀 안 봐서(콜라이더가 있든 없든) 순찰이 막히지 않는다. 플레이어 유닛의
    // NavMesh는 NavMeshModifier.ignoreFromBuild로 이미 굽기에서 뺐으니 크기와 무관하게 안전하다.
    // 원작 비율 5단계(PM 지시 2026-09-23) — 다른 포탈 지름과 같은 이유로 Scale을 태운다.
    // TrackWidth(흙길 폭)는 이번 지시 범위 밖이라 그대로 뒀다 — 그래도 아래 식은 "지금
    // TrackWidth 기준으로 포탈 가장자리가 길 안쪽 경계에 닿는다"는 관계라 자동으로 다시 맞는다.
    const float StoryPortalDiameter = 15f * MapLayout.Scale;

    // "적 유닛 지나는 길이랑 딱 맞닿게, 넘지는 말고"(사장님, 2026-09-03) — 오른쪽 세로 흙길
    // (반폭 TrackWidth/2)의 안쪽 경계에 포탈 가장자리(반지름 StoryPortalDiameter/2)가 정확히
    // 닿는 값. 상수 두 개를 따로 손으로 맞추면 지름이 또 바뀔 때 반드시 어긋나므로, 두 값의
    // 관계 자체를 식으로 남겨 지름이 바뀌어도 자동으로 다시 맞는다(둘 다 const라 컴파일 타임에
    // 계산된다 — 여전히 "상수 하나"다, 그 값이 다른 상수에서 유도될 뿐).
    const float StoryPortalInwardShift = StoryPortalDiameter * 0.5f + TrackWidth * 0.5f;

    // "아래로 내려봐"(사장님, 2026-09-03) — 순찰 경로 오른쪽 위 꼭짓점에서 오른쪽 아래
    // 꼭짓점 쪽으로 이 비율만큼 내려간다(0=위 꼭짓점 그대로, 1=아래 꼭짓점). 자전거 크기처럼
    // 몇 번 왔다 갔다 할 값이라 상수 하나로 뺐다 — 조정은 이 줄 하나만 고치면 된다.
    // 한계: 1.0을 넘기면 순찰 경로의 세로 구간(오른쪽 줄) 자체를 벗어나 필드 아래쪽 가장자리
    // 쪽으로 다가가기 시작한다 — 그 너머엔 유닛 우리·상점 줄이 있으니 1.0을 넘기지 말 것.
    const float StoryPortalDownwardFraction = 0.08f;

    static void BuildStoryZonePortal(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        // 순찰 경로(LaneLoop)와 같은 계산식을 그대로 쓴다 — 따로 좌표를 잡으면 나중에
        // 레인 크기가 또 바뀔 때 순찰 경로와 포탈 자리가 어긋난다.
        Vector3[] loop = MapLayout.LaneLoop(lane);
        Vector3 corner = loop[3]; // 오른쪽 위
        Vector3 lowerCorner = loop[2]; // 오른쪽 아래 — 내려가는 방향의 목표점
        float z = corner.z - StoryPortalDownwardFraction * (corner.z - lowerCorner.z);
        Vector3 ground = new Vector3(corner.x - StoryPortalInwardShift, corner.y, z);

        GameObject portal = CreatePortalObject(parent, $"{lane.name}_스토리포탈",
            new Vector3(ground.x, MapLayout.IslandTop + 0.25f, ground.z), StoryPortalDiameter);

        // 트리거만으로는 부족하다 — PhysicsColliders로 굽는 NavMesh는 트리거도 장애물로
        // 잡는다(CheckNavMeshCoverage 근처 DescribeBlockers 주석 참고, 실제로 겪은 문제).
        // 굽기에서 아예 빼서 흙길이 안 끊기게 한다.
        NavMeshModifier modifier = portal.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;

        StoryZonePortal component = portal.AddComponent<StoryZonePortal>();
        component.SetDestination(StoryZoneLandingPoint(laneIndex));
        StructureDresser.DressPortal(portal, "포탈_마법진_스토리", StructureDresser.StoryGlow);
    }

    // ⚠️ 2026-09-05 정정(사장님 발견): 예전엔 zone.size×0.25(존 크기에 비례)였다 — 존이
    // 180×150으로 커지면서(2026-09-03, 1.5배) 착지점이 중심에서 58.6 떨어지게 됐는데,
    // 로스터 최장 사거리가 47.5라 **전 유닛이 못 때리는 자리**였다. "스토리존 가도 아무
    // 일 없다"의 원인이 이거였다.
    //
    // ⚠️ 2차 정정(PM, 같은 날): 존 크기와 안 얽매이게 절대 거리로 바꾸며 처음엔 상수 22를
    // 썼는데(그때 최소 사거리 30 기준 여유 8), 그 뒤 사거리에 등급 편차가 들어가면서
    // (d04e91a) 최소 사거리가 22.2로 내려가 여유가 0.2로 사실상 사라졌다. **상수를 22→18로
    // 다시 손으로 맞추면 사거리가 또 바뀔 때 같은 사고가 반복된다** — 그래서 상수 자체를
    // 버리고 로스터 최소 사거리에서 매번 새로 구한다(배율 0.8만 고정 상수). 초월위습은
    // 싸우지 않는 재료 유닛이라 attackRange=0이 정상이라 이 계산에서 제외한다.
    const float StoryZoneLandingDistanceRatio = 0.8f;

    static float MinRosterAttackRange()
    {
        float min = float.MaxValue;
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (unit == null || unit.grade == UnitGrade.TranscendentWisp) continue;
            if (unit.attackRange <= 0f) continue;
            if (unit.attackRange < min) min = unit.attackRange;
        }
        // 로스터를 못 찾는 극단적인 경우에만 쓰는 안전망 — 정상 실행에선 절대 안 걸린다.
        return min < float.MaxValue ? min : 30f;
    }

    static float StoryZoneLandingDistance => MinRosterAttackRange() * StoryZoneLandingDistanceRatio;

    // 스토리존 한가운데 한 점에 네 레인이 전부 쏟아지면 겹친다(사장님이 지적한 흔함 칸
    // 겹침·개별 선택 문제와 같은 종류) — 레인마다 존 안의 네 귀퉁이로 살짝 나눠 보낸다.
    // 45도 대각선으로 등분해서(offset = distance/√2) 네 귀퉁이 모양은 그대로 유지한다 —
    // 이웃한 두 착지점(예: 레인0·레인1, X부호만 다름) 사이 거리는 2×offset이다. 로스터
    // 최소 사거리가 바뀌면 이 값도 같이 움직인다 — 사거리가 좁아져 이웃 간격이 너무
    // 좁다 싶으면(대략 로스터 최소 사거리 미만) 이 45도 배치 자체를 넓히는 걸 검토할 것.
    static Vector3 StoryZoneLandingPoint(int laneIndex)
    {
        MapLayout.Island zone = System.Array.Find(MapLayout.Zones, z => z.name == "StoryZone");

        float offset = StoryZoneLandingDistance / Mathf.Sqrt(2f);
        float offsetX = (laneIndex % 2 == 0 ? -1f : 1f) * offset;
        float offsetZ = (laneIndex < 2 ? 1f : -1f) * offset;

        // 착지점은 광장 안(반지름 33)이라 광장 윗면에 내린다.
        return new Vector3(zone.center.x + offsetX, MapLayout.IslandTop + StructureDresser.StoryPlazaLift,
                           zone.center.y + offsetZ);
    }

    // 기존 최대(StoryPortalDiameter=15)보다 크게 — "크게 만들라"는 사장님 지시.
    // 원작 비율 5단계(PM 지시 2026-09-23) — 다른 포탈 지름과 같은 이유로 Scale을 태운다.
    const float StoryReturnPortalDiameter = 24f * MapLayout.Scale;

    // 스토리존 → 레인 복귀. 레인마다가 아니라 존에 큰 포탈 하나(사장님 지시, 2026-09-05) —
    // StoryReturnPortal이 소유자별 목적지 4개를 들고 있다가 밟은 사람의 레인 한가운데로
    // 보낸다. 착지 지점(반지름 StoryZoneLandingDistance)·스토리 단상(반지름 platformSize×0.5,
    // 대략 26)과 안 겹치게 존 귀퉁이 쪽(180×150의 40%)에 둔다 — 존이 넉넉히 커서 여유 있다.
    static void BuildStoryReturnPortal(Transform parent)
    {
        MapLayout.Island zone = System.Array.Find(MapLayout.Zones, z => z.name == "StoryZone");

        Vector3 ground = new Vector3(zone.center.x + zone.size.x * 0.4f,
                                     MapLayout.IslandTop + 0.25f, zone.center.y + zone.size.y * 0.4f);

        GameObject portal = CreatePortalObject(parent, "스토리_복귀포탈", ground, StoryReturnPortalDiameter);

        // BuildStoryZonePortal과 같은 이유 — 트리거도 NavMesh 굽기엔 장애물로 잡힌다.
        NavMeshModifier modifier = portal.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;

        StoryReturnPortal component = portal.AddComponent<StoryReturnPortal>();
        Vector3[] destinations = new Vector3[MapLayout.Lanes.Length];
        for (int i = 0; i < MapLayout.Lanes.Length; i++)
        {
            MapLayout.Island lane = MapLayout.Lanes[i];
            destinations[i] = new Vector3(lane.center.x, MapLayout.IslandTop, lane.center.y);
        }
        component.SetDestinations(destinations);
        StructureDresser.DressPortal(portal, "포탈_마법진_복귀", StructureDresser.ReturnGlow);
    }

    // 크립섬 4곳. **원작은 3단계 순차 체인**이다(물범 → 노루 → 양) — SealSpawner 주석 참고.
    static string BuildSealSpawners(Transform parent)
    {
        EnemyData seal = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_Seal.asset");
        EnemyData[] later =
        {
            AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_Creep2_노루.asset"),
            AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_Creep3_양.asset"),
        };

        // 물범바위(Assets/Art/Creatures/물범바위.fbx)는 섬에 두지 않는다 — 사장님 09-13 「물범 섬에는 돌맹이 치워줘」.
        // 단계 적(물범→노루→양)은 섬 한가운데에 선다.
        foreach (MapLayout.Island island in MapLayout.SealIslands)
        {
            GameObject spawner = new GameObject($"{island.name}_물범");
            spawner.transform.SetParent(parent, false);
            spawner.transform.position = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);

            SealSpawner component = spawner.AddComponent<SealSpawner>();
            SerializedObject so = new SerializedObject(component);
            so.FindProperty("sealData").objectReferenceValue = seal;

            SerializedProperty stages = so.FindProperty("laterStages");
            stages.arraySize = later.Length;
            for (int i = 0; i < later.Length; i++)
                stages.GetArrayElementAtIndex(i).objectReferenceValue = later[i];

            so.ApplyModifiedProperties();
        }

        return seal != null
            ? $"\n물범: {MapLayout.SealIslands.Length}곳에 배치."
            : "\n  ⚠️ Enemy_Seal 에셋을 찾지 못해 물범이 안 나옵니다.";
    }

    // 원작 [퀘스트] 거대 해왕류(o02N) — 이동 안 하는 고정 표적이라 웨이브가 아니라 여기서
    // 한 번만 배치한다(PM 지시, 2026-09-05).
    //
    // 자리(사장님 09-13 「원랜디 참고해서 해왕류 바다에 넣어줄래? 원랜디처럼」): 원작은 CreateUnitsForPlayer5에서
    // (-8404.1, -1619.3)에 방향 316.717°로 세운다(war3map_new.j:13434) — 레인 필드 넷의 서남쪽 바다다
    // (3번 필드 서쪽 가장자리에서 필드 폭의 0.89배 서쪽, 아래 가장자리에서 필드 높이의 0.14배 남쪽).
    // 우리 맵은 배치가 달라 좌표를 그대로 못 쓰니 보물 구역(BuildTreasureHunt)과 같은 방법으로 **레인 필드 넷을
    // 감싼 사각형 기준 비율**로 옮긴다. 우리 해왕류 모델은 몸길이 350·높이 195(원점 = 수면)라 그 자리에 그대로 두면
    // 3번 레인 섬·스토리존에 몸이 걸친다 — 필드 묶음 중심에서 멀어지는 쪽으로 모든 섬과 떨어질 때까지 민다.
    // 원작처럼 바다를 건너는 유닛(비행·수상보행)만 닿는다.
    static readonly Vector2 OriginalSeaKingPoint = new Vector2(-8404.1f, -1619.3f);
    const float OriginalSeaKingFacing = 316.717f;   // WC3 각도: 동=0°, 반시계
    const float SeaKingClearance = 100f;            // 몸길이 175(09-13 절반으로 줄임)의 절반 + 여유 — 이만큼 모든 섬과 떨어진다
    const float SeaKingExtraWest = 120f;            // 사장님 09-13 「너무 붙어있다, 왼쪽으로 벌려」 — 섬에서 떨어뜨린 뒤 서쪽으로 더
    const float SeaSurfaceY = 0f;                   // BuildSea: 바다 상자 윗면. 해왕류 모델 원점이 수면이다

    static Vector3 SeaKingPosition()
    {
        Rect[] fields = MapLayout.Lanes.Select(lane =>
        {
            MapLayout.Island field = MapLayout.LaneField(lane);
            return new Rect(field.center - field.size * 0.5f, field.size);
        }).ToArray();

        Rect originalBlock = EncloseRects(OriginalLifeZones);
        Vector2 originalField = AverageRectSize(OriginalLifeZones);
        Rect block = EncloseRects(fields);
        Vector2 fieldSize = AverageRectSize(fields);

        Vector2 point = new Vector2(
            block.xMin - (originalBlock.xMin - OriginalSeaKingPoint.x) / originalField.x * fieldSize.x,
            block.yMin - (originalBlock.yMin - OriginalSeaKingPoint.y) / originalField.y * fieldSize.y);

        Vector2 outward = (point - block.center).normalized;
        for (int step = 0; step < 400 && TooCloseToIsland(point, SeaKingClearance); step++)
            point += outward * 2f;
        point.x -= SeaKingExtraWest;

        // 바다 판(±SeaSize/2) 안에 몸 전체가 남게 — 서쪽으로 민 만큼 가장자리를 넘지 않는지 막는다.
        float limit = MapLayout.SeaSize * 0.5f - SeaKingClearance;
        point.x = Mathf.Clamp(point.x, -limit, limit);
        point.y = Mathf.Clamp(point.y, -limit, limit);

        return new Vector3(point.x, SeaSurfaceY, point.y);
    }

    static bool TooCloseToIsland(Vector2 point, float clearance)
    {
        foreach (MapLayout.Island island in AllIslands())
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - island.center.x) - island.size.x * 0.5f, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.y - island.center.y) - island.size.y * 0.5f, 0f);
            if (dx * dx + dz * dz < clearance * clearance) return true;
        }
        return false;
    }

    // 원작 방향을 **머리 뼈**로 맞춘다 — 모델 파일의 앞 축을 추측하지 않는다(Blender→FBX→유니티 축 변환은 헷갈리기 쉽다).
    static float SeaKingYaw(EnemyData data)
    {
        float want = 90f - OriginalSeaKingFacing;   // WC3 (cos a, sin a) → 유니티 yaw(+Z=0°, 위에서 시계)
        if (data == null || data.prefab == null) return want;

        Transform head = data.prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head");
        if (head == null) return want;

        Vector3 toHead = head.position - data.prefab.transform.position;
        if (new Vector2(toHead.x, toHead.z).sqrMagnitude < 1e-4f) return want;
        return want - Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
    }

    static string BuildSeaKing(Transform parent)
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_거대해왕류.asset");
        WispData rewardWisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_흔함선택.asset");

        GameObject spawner = new GameObject("거대해왕류");
        spawner.transform.SetParent(parent, false);
        spawner.transform.position = SeaKingPosition();
        spawner.transform.rotation = Quaternion.Euler(0f, SeaKingYaw(data), 0f);

        SeaKingSpawner component = spawner.AddComponent<SeaKingSpawner>();
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("seaKingData").objectReferenceValue = data;
        so.FindProperty("rewardWisp").objectReferenceValue = rewardWisp;
        so.ApplyModifiedProperties();

        return data != null
            ? $"\n거대 해왕류: 원작 자리(레인 서남쪽 바다) ({spawner.transform.position.x:F0}, {spawner.transform.position.z:F0})에 배치 — 바다를 건너는 유닛만 닿는다." +
              (data.prefab == null || !data.prefab.name.Contains("해왕류") ? "\n  ⚠️ 해왕류 모델이 아직 안 붙었습니다 — 모델 배선을 먼저 돌리세요." : "")
            : "\n  ⚠️ Enemy_거대해왕류 에셋을 찾지 못해 거대 해왕류가 안 나옵니다.";
    }

    // ---- 보물찾기(TreasureHunt, TREASURE_SPEC_2026-09-12.md) ----
    // 원작 좌표(war3map_new.j) — 레인 필드 넷과 보물 구역. 우리 맵은 배치를 새로 짰으니 좌표를 그대로 못 쓴다.
    // 대신 **레인 필드 넷을 감싼 사각형에서 사방으로 얼마나 더 나가는지**를 필드 한 칸 크기의 비율로 옮긴다
    // (리서치담당 대조: 원작 구역은 레인 넷을 전부 품고 창고는 밖이다). 탐색 반경도 필드 크기 비율로 옮긴다.
    static readonly Rect[] OriginalLifeZones =
    {
        Rect.MinMaxRect(-5440f, 2848f, -2240f, 5600f),   // gg_rct_p1_life_zone
        Rect.MinMaxRect(-1280f, 2848f, 1984f, 5536f),    // p2
        Rect.MinMaxRect(-5504f, -1248f, -2240f, 1440f),  // p3
        Rect.MinMaxRect(-1440f, -1248f, 1856f, 1472f),   // p4
    };
    static readonly Rect OriginalTreasureZone = Rect.MinMaxRect(-5856f, -2400f, 2464f, 6080f);
    const float OriginalSearchRange = 750f;              // udg_treasure_range_int 초기값
    const float OriginalLegendNamiSearchRange = 863f;    // 전설 나미 뒤

    const string TreasureLegendNamiPath = "Assets/Data/Units/Roster/전설적인_엄태웅.asset";   // h02P
    const string TreasureCooldownItemPath = "Assets/Data/Items/ItemData_I00K_탐사도구.asset";

    static string BuildTreasureHunt(Transform parent)
    {
        Rect[] fields = new Rect[MapLayout.Lanes.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            MapLayout.Island field = MapLayout.LaneField(MapLayout.Lanes[i]);
            fields[i] = new Rect(field.center - field.size * 0.5f, field.size);
        }

        Rect originalBlock = EncloseRects(OriginalLifeZones);
        Vector2 originalField = AverageRectSize(OriginalLifeZones);
        Rect block = EncloseRects(fields);
        Vector2 fieldSize = AverageRectSize(fields);

        Rect zone = Rect.MinMaxRect(
            block.xMin - (originalBlock.xMin - OriginalTreasureZone.xMin) / originalField.x * fieldSize.x,
            block.yMin - (originalBlock.yMin - OriginalTreasureZone.yMin) / originalField.y * fieldSize.y,
            block.xMax + (OriginalTreasureZone.xMax - originalBlock.xMax) / originalField.x * fieldSize.x,
            block.yMax + (OriginalTreasureZone.yMax - originalBlock.yMax) / originalField.y * fieldSize.y);

        // 원작 필드는 가로로 길고(3256×2712) 우리 필드는 정사각형이라, 가로로 재면 23.0%·세로로 재면 27.7%로
        // 갈린다. 넓이가 같은 정사각형의 한 변끼리 비교해 그 사이 값을 쓴다.
        float rangeScale = Mathf.Sqrt(fieldSize.x * fieldSize.y) / Mathf.Sqrt(originalField.x * originalField.y);

        GameObject huntObject = new GameObject("보물찾기");
        huntObject.transform.SetParent(parent, false);
        TreasureHunt hunt = huntObject.AddComponent<TreasureHunt>();

        WispData randomWisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");      // e0IX
        WispData commonChoiceWisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_흔함선택.asset"); // e018
        WispData uncommonWisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_안흔함.asset");      // e017
        UnitData legendNami = AssetDatabase.LoadAssetAtPath<UnitData>(TreasureLegendNamiPath);
        ItemData cooldownItem = AssetDatabase.LoadAssetAtPath<ItemData>(TreasureCooldownItemPath);

        SerializedObject so = new SerializedObject(hunt);
        SerializedProperty zones = so.FindProperty("chestZones");
        zones.arraySize = 1;
        zones.GetArrayElementAtIndex(0).rectValue = zone;
        so.FindProperty("chestHeight").floatValue = MapLayout.IslandTop;
        so.FindProperty("searchRange").floatValue = OriginalSearchRange * rangeScale;
        so.FindProperty("legendNamiSearchRange").floatValue = OriginalLegendNamiSearchRange * rangeScale;
        so.FindProperty("randomWisp").objectReferenceValue = randomWisp;
        so.FindProperty("commonChoiceWisp").objectReferenceValue = commonChoiceWisp;
        so.FindProperty("uncommonWisp").objectReferenceValue = uncommonWisp;
        so.FindProperty("legendNami").objectReferenceValue = legendNami;
        so.FindProperty("boostedCooldownItem").objectReferenceValue = cooldownItem;
        so.ApplyModifiedProperties();

        string report = $"\n보물찾기: 구역 X {zone.xMin:0}~{zone.xMax:0} · Z {zone.yMin:0}~{zone.yMax:0}, " +
                        $"탐색 반경 {OriginalSearchRange * rangeScale:0.#}(전설 나미 {OriginalLegendNamiSearchRange * rangeScale:0.#}).";
        if (randomWisp == null || commonChoiceWisp == null || uncommonWisp == null)
            report += "\n  ⚠️ 보물 보상 위습(Wisp_랜덤유닛·흔함선택·안흔함) 중 못 찾은 것이 있습니다.";
        if (legendNami == null)
            report += $"\n  ⚠️ 전설 나미를 못 찾아 보물 보너스가 안 붙습니다: {TreasureLegendNamiPath}";
        if (cooldownItem == null)
            report += $"\n  ⚠️ 탐사도구(I00K)를 못 찾아 탐색 쿨타임이 70초로 안 줄어듭니다: {TreasureCooldownItemPath}";
        return report;
    }

    static Rect EncloseRects(Rect[] rects)
    {
        Rect enclosed = rects[0];
        foreach (Rect rect in rects)
            enclosed = Rect.MinMaxRect(Mathf.Min(enclosed.xMin, rect.xMin), Mathf.Min(enclosed.yMin, rect.yMin),
                                       Mathf.Max(enclosed.xMax, rect.xMax), Mathf.Max(enclosed.yMax, rect.yMax));
        return enclosed;
    }

    static Vector2 AverageRectSize(Rect[] rects)
    {
        Vector2 sum = Vector2.zero;
        foreach (Rect rect in rects) sum += rect.size;
        return sum / rects.Length;
    }

    // 펑크해저드 한가운데를 가로지르는 정의문. 부수기 전에는 섬이 둘로 나뉜다.
    const float GateWidth = 22f;
    const float GateHeight = 7f;
    const float WallHeight = 5.5f;
    const float GateThickness = 1.4f;

    static string BuildPunkHazardGate(Transform parent)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "PunkHazard");

        float z = island.center.y;
        float left = island.center.x - island.size.x * 0.5f;
        float right = island.center.x + island.size.x * 0.5f;
        float gateLeft = island.center.x - GateWidth * 0.5f;
        float gateRight = island.center.x + GateWidth * 0.5f;

        // 문 양옆은 고정 벽 — 여기가 뚫려 있으면 문을 부술 이유가 없다.
        BuildWall(parent, "펑크해저드_좌측벽",
            new Vector3((left + gateLeft) * 0.5f, MapLayout.IslandTop + WallHeight * 0.5f, z),
            new Vector3(gateLeft - left, WallHeight, GateThickness));
        BuildWall(parent, "펑크해저드_우측벽",
            new Vector3((gateRight + right) * 0.5f, MapLayout.IslandTop + WallHeight * 0.5f, z),
            new Vector3(right - gateRight, WallHeight, GateThickness));

        // 문기둥
        foreach (float pillarX in new[] { gateLeft, gateRight })
            BuildWall(parent, "펑크해저드_문기둥",
                new Vector3(pillarX, MapLayout.IslandTop + GateHeight * 0.5f, z),
                new Vector3(GateThickness * 1.6f, GateHeight, GateThickness * 1.6f));

        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "정의문";
        gate.transform.SetParent(parent, false);
        gate.transform.position = new Vector3(island.center.x, MapLayout.IslandTop + GateHeight * 0.5f, z);
        gate.transform.localScale = new Vector3(GateWidth - GateThickness, GateHeight, GateThickness);
        PaintGlow(gate, new Color(0.85f, 0.72f, 0.30f));   // 부술 대상이라 눈에 띄어야 한다
        gate.AddComponent<DestructibleGate>();
        DressGate(gate);
        StructureDresser.ScatterPunkHazard(parent, island, GateWidth, GateThickness);

        return "\n펑크해저드에 정의문을 세웠습니다 (부수면 길이 열립니다).";
    }

    // 초월·불멸은 조합식 표에 올리지 않고 전시만 한다(사용자 확정).
    // 초월은 석판 위에 가로줄로 세우고, 불멸은 가운데 화로를 둘러싸는 원형으로 놓는다.
    static string BuildGradeDisplays(Transform parent)
    {
        int transcend = BuildTranscendDisplay(parent);
        int immortal = BuildImmortalDisplay(parent);
        return $"\n전시: 초월 {transcend}종(가로줄), 불멸 {immortal}종(화로 원형).";
    }

    static int BuildTranscendDisplay(Transform parent)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "TranscendDisplay");
        List<UnitData> units = LoadUnitsOfGrade(UnitGrade.Transcendent);

        BuildStoneFloor(parent, "초월전시_바닥", island);

        int perRow = Mathf.Max(1, Mathf.FloorToInt((island.size.x - SlotSpacing) / SlotSpacing));
        float startX = island.center.x - (perRow - 1) * SlotSpacing * 0.5f;
        float startZ = island.center.y + island.size.y * 0.5f - SlotSpacing;

        for (int i = 0; i < units.Count; i++)
        {
            Vector3 ground = new Vector3(startX + (i % perRow) * SlotSpacing, MapLayout.IslandTop,
                                         startZ - (i / perRow) * SlotSpacing);
            // 받침 지름은 인형 기준(PedestalWidth)이다. 다만 칸 간격이 그보다 좁아지면 이웃
            // 받침과 맞닿으므로 칸 쪽(0.9배로 틈을 둔 값)으로 한 번 더 잠근다 — 지금 간격은
            // 61.4라 인형 기준이 이긴다.
            float lift = StructureDresser.PlacePedestal(parent, "받침_초월", $"초월_{units[i].unitName}_받침",
                ground, 0f, Mathf.Min(PedestalWidth, SlotSpacing * 0.9f) / PedestalDiameter);
            // 스킨이 있으면 색 큐브 대신 인형을 세운다(2026-09-18 초월 25종 스킨 완성). 칸 간격보다 조금 작게.
            PlaceUnitMarker(parent, $"초월_{units[i].unitName}", new Vector3(ground.x, 0f, ground.z),
                UnitGrade.Transcendent, units[i], DisplayFigureHeight, lift);
        }

        return units.Count;
    }

    static int BuildImmortalDisplay(Transform parent)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "ImmortalDisplay");
        List<UnitData> units = LoadUnitsOfGrade(UnitGrade.Immortal);

        BuildStoneFloor(parent, "불멸전시_바닥", island);
        Vector3 center = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);
        // 원작 불멸 전시 가운데 캠프파이어(2026-09-13 Blender). 모델이 없으면 옛 원기둥 화로.
        if (!StructureDresser.PlaceCampfire(parent, center)) BuildBrazier(parent, center);

        // 화로에서 떨어져 둘러앉는 반지름 — 섬 밖으로 나가지 않는 선에서 가장 넓게 잡는다.
        float radius = Mathf.Min(island.size.x, island.size.y) * 0.5f - SlotSize * 2f;
        // 받침 지름은 인형 기준(PedestalWidth, 초월 전시와 같은 규칙 — PM 지시 2026-09-23).
        // 다만 8명이 둘레에 다 안 들어가면 그쪽으로 줄인다 — 이웃과 닿지 않게 둘레 몫의 85%까지.
        // (지금 둘레 몫은 넉넉해서 인형 기준이 이긴다. 예전엔 천장이 1f라 섬이 4.167배가 돼도
        //  늘 1에서 잘려 받침이 하나도 안 커졌는데, 이제 기준 자체가 인형이라 그 문제도 없다.)
        float pedestalScale = Mathf.Min(PedestalWidth,
            2f * Mathf.PI * radius / Mathf.Max(1, units.Count) * 0.85f) / PedestalDiameter;

        for (int i = 0; i < units.Count; i++)
        {
            float angle = i / (float)Mathf.Max(1, units.Count) * Mathf.PI * 2f;
            Vector3 ground = new Vector3(island.center.x + Mathf.Cos(angle) * radius, MapLayout.IslandTop,
                                         island.center.y + Mathf.Sin(angle) * radius);
            // 받침_불멸은 +Y(그을린 쪽)가 불을 본다.
            float lift = StructureDresser.PlacePedestal(parent, "받침_불멸", $"불멸_{units[i].unitName}_받침",
                ground, StructureDresser.YawTowardCenter(ground, center), pedestalScale);
            // 스킨이 있으면 색 큐브 대신 인형을 세운다(2026-09-23 불멸 8종 스킨 완성). 초월 전시와 같은 규칙.
            // 받침과 같은 각으로 돌려 가운데 화로를 바라보게 한다(사장님 지시 2026-09-23).
            //   받침의 yaw는 "+Y(그을린 쪽)가 중심을 본다"는 규약이고, 인형의 기본 방향은 남쪽(180°)이다.
            //   같은 각을 그대로 더하면 두 방향 규약이 어긋나 등을 보이므로, 중심을 향한 각을 따로 구한다.
            Vector3 toCenter = center - ground;
            float faceYaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg - 180f;
            PlaceUnitMarker(parent, $"불멸_{units[i].unitName}", new Vector3(ground.x, 0f, ground.z),
                UnitGrade.Immortal, units[i], DisplayFigureHeight, lift, faceYaw);
        }

        return units.Count;
    }

    static void BuildStoneFloor(Transform parent, string name, MapLayout.Island island)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = name;
        floor.transform.SetParent(parent, false);
        floor.transform.position = new Vector3(island.center.x, MapLayout.IslandTop + 0.06f, island.center.y);
        floor.transform.localScale = new Vector3(island.size.x - 2f, 0.12f, island.size.y - 2f);
        Paint(floor, "rock", island.size.x, island.size.y);
        Object.DestroyImmediate(floor.GetComponent<Collider>());
    }

    static void BuildBrazier(Transform parent, Vector3 ground)
    {
        GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bowl.name = "불멸전시_화로";
        bowl.transform.SetParent(parent, false);
        bowl.transform.position = ground + Vector3.up * 0.9f;
        bowl.transform.localScale = new Vector3(4.5f, 0.9f, 4.5f);
        Paint(bowl, "rock", 4.5f, 4.5f);
        Object.DestroyImmediate(bowl.GetComponent<Collider>());

        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flame.name = "불멸전시_불";
        flame.transform.SetParent(parent, false);
        flame.transform.position = ground + Vector3.up * 2.6f;
        flame.transform.localScale = new Vector3(2.6f, 1.0f, 2.6f);
        PaintGlow(flame, new Color(1f, 0.55f, 0.18f));
        Object.DestroyImmediate(flame.GetComponent<Collider>());

        // 불빛이 주위 유닛에 닿아야 캠프파이어로 읽힌다.
        GameObject lightObject = new GameObject("불멸전시_불빛");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = ground + Vector3.up * 3.5f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.25f);
        light.range = 26f;
        light.intensity = 3.5f;
    }

    const float GlowEmissionBoost = 3f;

    /// <summary>
    /// 🔴 이름에 **색만** 들어간다. 색을 바꾸면 새 파일이 나서 멀쩡해 보이지만,
    ///    <see cref="GlowEmissionBoost"/>만 바꾸면 이미 만들어진 `glow_*.mat`에 영영 안 닿았다 —
    ///    위습 발광이 코드는 1.1배인데 `.mat`은 4배로 남아 있던 것과 **같은 모양**이다(09-24).
    ///    **가장 나쁜 결함은 틀렸다고 말해 주지 않는 결함이다.** 그래서 가져온 뒤에도 다시 쓴다.
    /// </summary>
    static void PaintGlow(GameObject obj, Color color)
    {
        string path = $"{MaterialFolder}/glow_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;
        if (isNew)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_EmissionColor", color * GlowEmissionBoost);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        if (isNew) AssetDatabase.CreateAsset(material, path);
        else SaveChanged(material);

        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    /// <summary>
    /// 이미 있는 에셋에 값을 다시 쓴 뒤 부른다. `SetDirty`만으로는 **디스크에 안 써지고**,
    /// 다음 리로드 때 옛 값이 그대로 돌아온다(09-24에 실제로 그랬다).
    /// </summary>
    static void SaveChanged(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    // 부스 줄. 앞쪽(포탈 방향)은 열어 두고 뒤와 칸막이만 세운다.
    static void BuildBoothRow(Transform parent, float xLeft, float xRight,
                              float portalZ, float step, int count)
    {
        float y = MapLayout.IslandTop + BoothWallHeight * 0.5f;
        float backZ = portalZ + BoothDepth;
        float midZ = portalZ + BoothDepth * 0.5f;

        BuildWall(parent, "부스_뒷벽",
            new Vector3((xLeft + xRight) * 0.5f, y, backZ),
            new Vector3(xRight - xLeft + BoothWallThickness, BoothWallHeight, BoothWallThickness));

        // 칸막이는 부스 사이마다 한 장씩, 양 끝까지 포함해 count + 1장.
        for (int i = 0; i <= count; i++)
            BuildWall(parent, $"부스_칸막이{i}",
                new Vector3(xLeft + step * (i + 0.5f), y, midZ),
                new Vector3(BoothWallThickness, BoothWallHeight, BoothDepth + BoothWallThickness));

        // 맨 바깥 칸막이와 섬 가장자리 사이에도 틈이 남는다. 그 두 곳을 막는다.
        float edgeDepth = BoothDepth + BoothWallThickness;
        BuildWall(parent, "부스_왼끝벽",
            new Vector3((xLeft + (xLeft + step * 0.5f)) * 0.5f, y, midZ),
            new Vector3(step * 0.5f, BoothWallHeight, edgeDepth));
        BuildWall(parent, "부스_오른끝벽",
            new Vector3(((xLeft + step * (count + 0.5f)) + xRight) * 0.5f, y, midZ),
            new Vector3(xRight - (xLeft + step * (count + 0.5f)), BoothWallHeight, edgeDepth));
    }

    static void BuildWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        Paint(wall, "rock", scale.x, scale.z);
        // 콜라이더는 남긴다 — 실제로 막히는 벽이라야 유닛이 부스 사이로 새지 않는다.
        DressWall(wall, WallPieceFor(name, scale));
    }

    // ──────────────────────────────────────────────────────────── 벽 겉모습 (2026-09-12)
    //
    // 사장님 지시 「벽도 새롭게 만드는거 어때?」. 벽은 지금까지 회색 상자였다. Assets/Art/Walls의 조각
    // (Tools/blender/gen_walls.py)을 이어 붙여 **겉모습만** 바꾼다.
    //
    // 🔴 막히는 상자(콜라이더)는 위치·크기 그대로 둔다. 렌더러만 떼고 그 자리에 조각을 세운다 —
    //    NavMesh는 콜라이더로 굽으므로 유닛 길과 게임 동작이 하나도 안 바뀐다.
    // - 조각은 벽 상자의 자식이 아니라 옆에 둔다. 상자는 422×5.5×6처럼 한쪽으로 늘어나 있어서,
    //   자식으로 두면 그 배율을 물려받아 조각이 찌그러진다.
    // - 긴 변을 따라 조각을 N개 이어 붙이고, 끝이 정확히 맞게 길이 방향으로만 살짝 늘린다
    //   (조각은 양 끝면이 같게 만들어져 있어 이어도 틈이 없다 — b91838ed 보고).
    // - 조각 FBX를 못 찾으면 예전 상자 그대로 둔다. 맵 생성이 깨지지 않게.

    const string WallFolder = "Assets/Art/Walls/";

    // 벽 조각의 **규격 치수**(게임 단위, 길이 X × 높이 Y × 두께 Z). gen_walls.py가 이 규격으로 짓는다.
    // 🔴 늘릴 배율은 잰 크기가 아니라 이 규격으로 잡는다 — 흉벽성벽은 총안이 위로 1.3, 나무기둥은 랜턴 팔이
    //    옆으로 튀어나와서, 잰 크기로 나누면 몸통이 눌린다(2026-09-12 벽 17종, a8cde0fb). 장식은 튀어나온 채로 붙는다.
    static readonly Dictionary<string, Vector3> WallPieceSizes = new Dictionary<string, Vector3>
    {
        // 두꺼운 벽
        { "돌담_두꺼움", new Vector3(8f, 5.5f, 7f) },
        { "성벽_마름돌", new Vector3(8f, 5.5f, 7f) },
        { "폐허벽", new Vector3(8f, 5.5f, 7f) },
        { "흉벽성벽", new Vector3(8f, 5.5f, 7f) },
        // 얇은 벽
        { "돌담_얇음", new Vector3(6f, 5.5f, 1.4f) },
        { "석축_이끼", new Vector3(6f, 5.5f, 1.4f) },
        { "벽돌담", new Vector3(6f, 5.5f, 1.4f) },
        { "해안방파제", new Vector3(6f, 5.5f, 1.4f) },
        // 울타리
        { "나무울타리", new Vector3(6f, 5.5f, 1f) },
        { "목책", new Vector3(6f, 5.5f, 1f) },
        { "목장울타리", new Vector3(6f, 5.5f, 1f) },
        { "대나무울타리", new Vector3(6f, 5.5f, 1f) },
        { "밧줄난간", new Vector3(6f, 5.5f, 1f) },
        // 기둥
        { "돌기둥", new Vector3(2.2f, 7f, 2.2f) },
        { "나무기둥", new Vector3(2.2f, 7f, 2.2f) },
        { "이끼돌기둥", new Vector3(2.2f, 7f, 2.2f) },
        // 문
        { "정의문", new Vector3(20.6f, 7f, 1.4f) },
    };

    // 조각의 실제 크기를 규격으로 돌려준다. 유니티 임포트 단위(useFileScale)가 어떻게 먹었는지는
    // **장식이 안 걸리는 축**으로 잰다 — 기둥은 높이(랜턴 팔은 옆), 나머지는 길이(이어 붙이는 조각은 끝면이 정확).
    // 규격표에 없는 조각은 잰 크기를 그대로 쓴다.
    static Vector3 WallPieceSize(string pieceName, Vector3 measured, out bool isPillar, out bool isKnown)
    {
        isPillar = pieceName.EndsWith("기둥");
        isKnown = WallPieceSizes.TryGetValue(pieceName, out Vector3 nominal);
        if (!isKnown) return measured;

        float unit = isPillar ? measured.y / nominal.y : measured.x / nominal.x;
        return nominal * unit;
    }

    // 어떤 조각을 쓸지 고른다. 두께만으로 가르면 부스 끝벽처럼 뭉툭한 울타리가 돌담이 되므로 이름을 먼저 본다.
    static string WallPieceFor(string name, Vector3 scale)
    {
        float along = Mathf.Max(scale.x, scale.z);
        float across = Mathf.Min(scale.x, scale.z);
        if (along < across * 1.5f && scale.y > along) return "돌기둥";   // 가로세로가 비슷하고 키가 크다
        if (name.StartsWith("레인간")) return "돌담_두꺼움";
        if (name.StartsWith("펑크해저드")) return "돌담_얇음";
        return "나무울타리";                                              // 유닛 우리·부스·칸막이
    }

    static void DressWall(GameObject wall, string pieceName)
    {
        GameObject piece = AssetDatabase.LoadAssetAtPath<GameObject>(WallFolder + pieceName + ".fbx");
        if (piece == null || !TryMeasureFigure(piece, out Bounds bounds) ||
            bounds.size.x < 0.001f || bounds.size.y < 0.001f || bounds.size.z < 0.001f)
            return;

        Vector3 size = wall.transform.localScale;
        bool alongZ = size.z > size.x;
        float length = alongZ ? size.z : size.x;
        float thickness = alongZ ? size.x : size.z;
        float height = size.y;

        GameObject dressing = new GameObject(wall.name + "_모양");
        dressing.transform.SetParent(wall.transform.parent, false);
        dressing.transform.position = wall.transform.position - Vector3.up * (height * 0.5f);   // 바닥 가운데
        dressing.transform.rotation = Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);                // 조각의 길이 방향은 X

        Vector3 pieceSize = WallPieceSize(pieceName, bounds.size, out bool pillar, out bool known);
        int count = pillar ? 1 : Mathf.Max(1, Mathf.RoundToInt(length / pieceSize.x));
        Vector3 factor = new Vector3(length / (count * pieceSize.x), height / pieceSize.y, thickness / pieceSize.z);
        float step = length / count;
        // 규격 조각은 원점이 바닥 한가운데로 약속돼 있다 — 경계 상자로 다시 가운데를 잡으면 한쪽으로 튀어나온
        // 장식(랜턴 팔) 때문에 몸통이 반대로 밀린다. 규격표에 없는 조각만 경계 상자로 되돌린다.
        float offsetX = known ? 0f : -(bounds.min.x + bounds.max.x) * 0.5f * factor.x;
        float offsetZ = known ? 0f : -(bounds.min.z + bounds.max.z) * 0.5f * factor.z;

        for (int i = 0; i < count; i++)
        {
            GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(piece, dressing.transform);
            tile.transform.localRotation = piece.transform.localRotation;
            tile.transform.localScale = ScaleInWorldAxes(piece.transform, factor);
            tile.transform.localPosition = new Vector3(
                -length * 0.5f + step * (i + 0.5f) + offsetX, -bounds.min.y * factor.y, offsetZ);

            foreach (Collider collider in tile.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (Transform part in tile.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
        }

        // 막히는 상자는 남기고 보이는 것만 뗀다.
        if (wall.TryGetComponent(out MeshRenderer renderer)) Object.DestroyImmediate(renderer);
        if (wall.TryGetComponent(out MeshFilter filter)) Object.DestroyImmediate(filter);
    }

    // 정의문 겉모습. 벽과 달리 **문 상자의 자식**으로 붙인다 — DestructibleGate.Break가 부서질 때
    // 문 상자 자체를 아래로 내리므로(transform.position), 옆에 두면 모양만 제자리에 남는다.
    // 문 상자는 20.6×7×1.4로 한쪽으로 늘어나 있어서 자식이 그 배율을 물려받는다 — 배율과 위치를
    // 부모 배율로 나눠 되돌린다. 문 상자는 회전이 없고 조각 회전은 90° 단위라 모양이 비틀리지 않는다.
    // 문을 찾는 쪽(UnitAttacker)은 DestructibleGate.Active 목록을 보므로 렌더러를 떼도 공격 대상은 그대로다.
    static void DressGate(GameObject gate)
    {
        GameObject piece = AssetDatabase.LoadAssetAtPath<GameObject>(WallFolder + "정의문.fbx");
        if (piece == null || !TryMeasureFigure(piece, out Bounds bounds) ||
            bounds.size.x < 0.001f || bounds.size.y < 0.001f || bounds.size.z < 0.001f)
            return;

        Vector3 parentScale = gate.transform.localScale;
        Vector3 gateSize = WallPieceSize("정의문", bounds.size, out _, out bool gateKnown);
        Vector3 world = new Vector3(parentScale.x / gateSize.x, parentScale.y / gateSize.y, parentScale.z / gateSize.z);

        GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(piece, gate.transform);
        tile.name = "정의문_모양";
        tile.transform.localRotation = piece.transform.localRotation;
        tile.transform.localScale = ScaleInWorldAxes(piece.transform,
            new Vector3(world.x / parentScale.x, world.y / parentScale.y, world.z / parentScale.z));

        // 문 상자 원점은 한가운데, 조각 원점은 바닥 가운데다. 세계 기준 오프셋을 부모 배율로 나눠 넣는다.
        Vector3 offset = new Vector3(
            gateKnown ? 0f : -(bounds.min.x + bounds.max.x) * 0.5f * world.x,
            -parentScale.y * 0.5f - bounds.min.y * world.y,
            gateKnown ? 0f : -(bounds.min.z + bounds.max.z) * 0.5f * world.z);
        tile.transform.localPosition = new Vector3(offset.x / parentScale.x, offset.y / parentScale.y, offset.z / parentScale.z);

        foreach (Collider collider in tile.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        // 문 상자의 콜라이더(막는 판정)와 DestructibleGate는 그대로. 빛나는 상자만 안 보이게 한다.
        if (gate.TryGetComponent(out MeshRenderer renderer)) Object.DestroyImmediate(renderer);
        if (gate.TryGetComponent(out MeshFilter filter)) Object.DestroyImmediate(filter);
    }

    // FBX 루트가 축 변환 회전(예: X −90)을 갖고 있으면 루트의 로컬 축과 세계 축이 뒤바뀐다.
    // 세계 축 기준 배율을 루트 로컬 축 배율로 옮겨 적는다. 회전이 90° 단위라 축이 섞이지는 않는다.
    static Vector3 ScaleInWorldAxes(Transform root, Vector3 factor)
    {
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        Vector3 local = Vector3.one;
        for (int j = 0; j < 3; j++)
        {
            Vector3 world = root.localRotation * axes[j];
            float ax = Mathf.Abs(world.x), ay = Mathf.Abs(world.y), az = Mathf.Abs(world.z);
            local[j] = ax >= ay && ax >= az ? factor.x : (ay >= az ? factor.y : factor.z);
        }
        return Vector3.Scale(root.localScale, local);
    }

    // 조합식 표와 같은 자리 표시 기둥. 유닛과 키가 오면 스킨 인형을 먼저 세우고(2026-09-07,
    // 사장님 지시 — 선택위습 부스에서도 스킨이 보이게), 모델이 없는 유닛만 등급 색 큐브다.
    // lift = 섬 윗면에서 더 올려 세울 높이(받침 윗면). 0이면 섬 바닥에 선다.
    static void PlaceUnitMarker(Transform parent, string name, Vector3 groundPosition, UnitGrade grade,
                                UnitData unit = null, float figureHeight = 0f, float lift = 0f, float yaw = 0f)
    {
        if (unit != null && figureHeight > 0f
            && TryPlaceUnitModel(parent, name,
                                 new Vector3(groundPosition.x, MapLayout.IslandTop + lift, groundPosition.z),
                                 unit, figureHeight, yaw))
            return;

        // 스킨이 아직 없는 유닛의 자리표시. **인형과 같은 키로 세운다**(사장님 지적 2026-09-23:
        // "스킨 없는 유닛 기둥 크기가 인형과 달라 줄이 들쭉날쭉하다"). 스킨이 채워질수록
        // 같은 격자·같은 키로 자연스럽게 메워진다.
        // 폭은 사람 어깨폭 비율(키의 0.28, PedestalWidth 주석과 같은 근거)로 잡는다.
        // figureHeight를 안 준 호출부(전시가 아닌 자리표시)는 예전처럼 SlotSize/SlotHeight를 쓴다.
        float markerHeight = figureHeight > 0f ? figureHeight : SlotHeight;
        float markerWidth = figureHeight > 0f ? figureHeight * 0.28f : SlotSize;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.transform.position = new Vector3(
            groundPosition.x, MapLayout.IslandTop + lift + markerHeight * 0.5f, groundPosition.z);
        marker.transform.localScale = new Vector3(markerWidth, markerHeight, markerWidth);
        PaintSolid(marker, GradeColor(grade));
        Object.DestroyImmediate(marker.GetComponent<Collider>());
    }

    // 칸을 하나씩 벽으로 두르면 인접한 칸 사이에 벽이 두 장씩 겹친다.
    // 열 전체를 한 번에 세우고, 칸 경계마다 칸막이를 한 장씩만 둔다.
    //
    // 모서리 규칙: 세로벽이 모서리를 덮고, 가로벽은 그 안쪽만 채운다.
    // 이렇게 해야 어디서도 두 벽이 같은 자리를 차지하지 않는다.
    static void BuildCellColumn(Transform parent, string label,
                                float xLeft, float xRight, float zTop, float zBottom,
                                IReadOnlyList<float> dividerZ, bool skipLeftWall = false)
    {
        float y = MapLayout.IslandTop + WallHeight * 0.5f;
        float centerX = (xLeft + xRight) * 0.5f;
        float innerWidth = xRight - xLeft - GateThickness;
        float outerDepth = zTop - zBottom + GateThickness;

        // 옆 열과 맞닿는 쪽은 그 열의 벽을 함께 쓴다 — 두 장이 겹쳐 서지 않게.
        if (!skipLeftWall)
            BuildWall(parent, $"{label}_왼벽",
                new Vector3(xLeft, y, (zTop + zBottom) * 0.5f),
                new Vector3(GateThickness, WallHeight, outerDepth));

        BuildWall(parent, $"{label}_오른벽",
            new Vector3(xRight, y, (zTop + zBottom) * 0.5f),
            new Vector3(GateThickness, WallHeight, outerDepth));

        BuildWall(parent, $"{label}_위벽",
            new Vector3(centerX, y, zTop), new Vector3(innerWidth, WallHeight, GateThickness));

        BuildWall(parent, $"{label}_아래벽",
            new Vector3(centerX, y, zBottom), new Vector3(innerWidth, WallHeight, GateThickness));

        if (dividerZ == null) return;

        for (int i = 0; i < dividerZ.Count; i++)
            BuildWall(parent, $"{label}_칸막이{i + 1}",
                new Vector3(centerX, y, dividerZ[i]),
                new Vector3(innerWidth, WallHeight, GateThickness));
    }

    // 보너스가 등급이 아니라 특정 유닛인 경우.
    static void ApplyBonusUnit(GameObject portal, string unitAsset, float chance)
    {
        UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(
            $"Assets/Data/Units/Roster/{unitAsset}.asset");

        if (unit == null)
        {
            Debug.LogWarning($"[맵] 보너스 유닛 에셋을 못 찾았습니다: {unitAsset}");
            return;
        }

        SerializedObject so = new SerializedObject(portal.GetComponent<UnitPortal>());
        so.FindProperty("bonusUnit").objectReferenceValue = unit;
        so.FindProperty("bonusChancePercent").floatValue = chance;
        so.ApplyModifiedProperties();
    }

    static void ApplyBonusGrade(GameObject portal, UnitGrade bonusGrade, float chance)
    {
        SerializedObject so = new SerializedObject(portal.GetComponent<UnitPortal>());
        so.FindProperty("bonusGrade").enumValueIndex = (int)bonusGrade;
        so.FindProperty("bonusChancePercent").floatValue = chance;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// 포탈 판정 상자의 높이.
    ///
    /// 🔴 왜 위습에서 유도하나: 포탈을 밟는 것 중 **가장 높이 뜬 게 위습**이다. 위습은 영혼이라
    ///    일부러 떠 있게 만들었고(<see cref="WispFloatHeight"/>), 콜라이더도 보이는 몸을 따라
    ///    올라가 있다(클릭 선택이 몸을 맞혀야 한다). 그 꼭대기가 바닥 + 1.35 × WispScale ≈ 67.5다.
    ///    그러니 기준은 「캐릭터 키」가 아니라 **위습이 떠 있는 높이**다.
    ///
    /// 🔴 옛 값 24는 **그 위습에 못 미쳤다.** 「캐릭터 키 20 + 조금」으로 잡았는데 위습 아래끝이
    ///    이미 25.5였다. 그런데도 자원·랜덤 포탈이 돌아간 것은 **엔진이 값을 늘려 줬기 때문**이다
    ///    (아래 CreatePortalObject 주석). 우리 코드가 맞아서가 아니라 **엔진이 틀린 방향으로
    ///    틀려 줘서** 살아 있었다.
    ///
    /// ⚠️ **왜 위습 꼭대기(67.5)보다 두 배 넉넉한가** — 캡슐은 「높이 ≥ 2×반지름」이라,
    ///    이 값이 **가장 큰 포탈 반지름의 2배보다 작으면 엔진이 다시 부풀린다.**
    ///    지금 가장 큰 것은 스토리 복귀(지름 100 → 반지름 50)라 하한이 **100**이다.
    ///    100으로 잡으면 딱 걸쳐서, 포탈을 조금만 키워도 조용히 옛 병으로 돌아간다.
    ///    150이면 반지름 75짜리 포탈까지 견딘다. **줄이지 말 것** — 줄이면 그 하한부터 확인해야 한다.
    /// </summary>
    const float PortalTriggerHeight = 3f * WispScale;   // 150 — 위습 꼭대기 67.5 + 캡슐 하한 100 둘 다 넘긴다

    static GameObject CreatePortalObject(Transform parent, string name, Vector3 position, float diameter)
    {
        GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        portal.name = name;
        portal.transform.SetParent(parent, false);
        portal.transform.position = position;
        portal.transform.localScale = new Vector3(diameter, 0.5f, diameter);
        Paint(portal, "portal");

        Collider collider = portal.GetComponent<Collider>();
        collider.isTrigger = true;

        // ⚠️ 2026-09-05 정정(사장님이 게임을 돌려서 발견): 유니티 OnTriggerEnter는 둘 중
        // 하나에 Rigidbody가 있어야 뜬다. UnitPrefab·WispPrefab은 자체 Rigidbody가 있어서
        // 우연히 됐지만, 스킨 프리팹(Unit_idle, Unit_안흔함_상붕카 등)은 둘 다 없어 이
        // 함수를 거치는 트리거형 포탈(StoryZonePortal·ResourcePortal·InterludeGate·
        // UnitPortal — 해적단은 그 뒤 클릭형 상점으로 바뀌어 더 이상 여기 안 걸린다)
        // 전부가 그 유닛들에게 통째로 안 통했다. 포탈 쪽에 한 번만 붙이면 앞으로 어떤
        // 프리팹이 와도(자체 Rigidbody 유무와 무관하게) 작동한다 — 스킨마다 따로 고치는
        // 게 아니라 여기 한 곳이 근본 수정이다.
        Rigidbody rb = portal.AddComponent<Rigidbody>();
        rb.isKinematic = true;   // 없으면 포탈이 중력에 떨어진다
        rb.useGravity = false;

        // 보이는 건 바닥에 깔린 납작한 원판이지만, 판정은 위아래로 높아야 한다.
        // 원판 두께(1)만 판정하면 몸이 떠 있는 위습이나 키 20짜리 유닛의 콜라이더가
        // 그 위를 지나가면서도 한 번도 닿지 않아, 포탈에 들어가도 아무 일이 없다.
        // 🔴 2026-09-24 — 여기가 「위습을 포탈에 넣어도 아무 일이 없다」의 정체였다.
        //
        //    유니티 캡슐은 **높이를 2×반지름 아래로 못 내린다.** 원판을 만들려고 y배율 0.5를
        //    줬지만 월드 반지름이 0.5×지름이라, 옛 코드가 넣은 높이 24는 **전부 무시되고
        //    반지름짜리 구**가 됐다. 즉 **포탈 지름이 트리거 높이를 정하고 있었다**:
        //      스토리복귀 지름 100.0 → 꼭대기 58.3   스토리 62.5 → 39.5
        //      자원·랜덤   지름  37.5 → 꼭대기 27.0   흔함선택 27.1 → **21.8**
        //    위습 콜라이더 아래끝이 25.5라, **작은 포탈 둘만 조용히 죽어 있었다.**
        //    「지름이 큰 포탈일수록 잘 된다」가 네 종에 순서대로 나타난 것이 그 증거다.
        //
        //    ⚠️ 상자로 바꾸지 말 것. 포탈은 **원**이라, 상자로 바꾸면 발자국이 정사각형이 되어
        //       모서리에서 √2 ≈ 1.41배 밖까지 판정이 나간다 — 「원 밖인데 들어갔다」가 된다.
        //       캡슐은 원을 유지하면서 높이만 고칠 수 있다. 높이를 **키우는** 것은 합법이다.
        if (collider is CapsuleCollider capsule)
        {
            float scaleY = portal.transform.localScale.y;
            // 원판 아래끝에서 위로 뻗는다. 아래로 파고드는 건 땅에 묻혀 무해하고, **위가 중요하다**.
            capsule.height = PortalTriggerHeight / scaleY;
            capsule.center = new Vector3(0f, (PortalTriggerHeight * 0.5f - 0.5f) / scaleY, 0f);
        }

        return portal;
    }

    // 특수 지급 줄은 스토리 8을 깬 뒤 《백수생활》 5분 동안만 열린다.
    // 게이트는 콜라이더만 알면 되므로 UnitPortal이든 ResourcePortal이든 같은 컴포넌트로 덮인다.
    // 《백수생활》 선택 위습. 실제 등급 의미는 없고 WispCell 라우팅 키로만 쓴다 —
    // 픽업 위습들이 안 쓰는 값이라야 엉뚱한 칸으로 흘러가지 않는다.
    const UnitGrade InterludeChoiceGrade = UnitGrade.Transcendent;
    const string InterludeChoiceWispPath = "Assets/Data/Wisps/Wisp_백수생활선택.asset";

    static void GateToInterlude(GameObject portal)
    {
        if (portal == null) return;

        InterludeGate gate = portal.AddComponent<InterludeGate>();
        WispData choiceWisp = AssetDatabase.LoadAssetAtPath<WispData>(InterludeChoiceWispPath);

        if (choiceWisp == null)
        {
            Debug.LogWarning($"[맵] 선택 위습 에셋을 못 찾았습니다: {InterludeChoiceWispPath}");
            return;
        }

        // 특수 칸 포탈 전부가 같은 위습을 구독한다. 하나에서 소모되면 나머지도 "이미 골랐다"로 바뀐다.
        SerializedObject so = new SerializedObject(gate);
        so.FindProperty("choiceTrackedWispData").objectReferenceValue = choiceWisp;
        so.ApplyModifiedProperties();
    }

    // rewardGrade를 주면 "이 등급 위습을 받아 저 등급 유닛을 준다"가 된다.
    // 안 주면 받은 위습의 등급 그대로 뽑는다(대부분의 포탈이 그렇다).
    static void ConfigurePortal(GameObject portal, UnitGrade grade, UnitData specificUnit,
                                GachaTable table, UnitSpawner spawner, UnitGrade? rewardGrade = null)
    {
        UnitPortal unitPortal = portal.AddComponent<UnitPortal>();
        SerializedObject so = new SerializedObject(unitPortal);

        so.FindProperty("legacyGradeMigrated").boolValue = true;
        so.FindProperty("overrideRewardGrade").boolValue = rewardGrade.HasValue;
        so.FindProperty("rewardGrade").enumValueIndex = (int)(rewardGrade ?? grade);
        so.FindProperty("specificUnit").objectReferenceValue = specificUnit;
        so.FindProperty("gachaTable").objectReferenceValue = table;
        so.FindProperty("unitSpawner").objectReferenceValue = spawner;
        // 비워두면 위습 주인의 레인 한가운데로 나간다.
        so.FindProperty("spawnPoint").objectReferenceValue = null;
        so.ApplyModifiedProperties();

        // ⚠️ acceptedGrades(List<UnitGrade>)만은 SerializedProperty를 안 거친다 —
        // enumValueIndex로 채워도 씬 파일에 한 번도 안 들어간 것으로 확인됐다(UnitPortal.
        // SetAcceptedGrade 코멘트 참고). so.ApplyModifiedProperties() 뒤에 마지막으로 불러서
        // SerializedObject 왕복에 덮이지 않게 한다. SetDirty를 반드시 같이 불러야 한다.
        unitPortal.SetAcceptedGrade(grade);
        EditorUtility.SetDirty(unitPortal);
    }

    // 좌표를 손으로 옮기다 보면 섬이 서로 올라타는 일이 생긴다(초월 전시가 조합식 표를 덮은 적 있음).
    // 눈으로는 위에서 봐야만 보이므로 생성할 때마다 검사한다.
    //
    // 🔴 2026-09-24: 이 검사가 `MapLayout.Lanes`만 봐서 **앞치마도 레인 사이 벽도 검사 밖**이었다.
    //    그래서 가로벽이 앞치마를 100% 덮는 것을 아무도 못 봤고, 사진에서 「하얀 가시밭」으로
    //    드러날 때까지 하루가 갔다. 목록에 없는 것은 검사가 못 본다 —
    //    **땅을 차지하는 것은 전부 넣는다.**
    static string CheckOverlaps()
    {
        List<MapLayout.Island> ground = new List<MapLayout.Island>();
        ground.AddRange(MapLayout.Lanes);
        foreach (MapLayout.Island lane in MapLayout.Lanes) ground.Add(MapLayout.LaneApron(lane));
        ground.AddRange(MapLayout.Warehouses);
        ground.AddRange(MapLayout.SealIslands);
        ground.AddRange(MapLayout.Zones);

        List<string> hits = new List<string>();
        for (int i = 0; i < ground.Count; i++)
        {
            for (int j = i + 1; j < ground.Count; j++)
            {
                if (!Overlaps(ground[i], ground[j], out float depth) || depth <= OverlapTolerance) continue;
                hits.Add($"{ground[i].name} ↔ {ground[j].name} ({depth:0.#})");
            }
        }

        // 벽은 땅끼리와 따로 본다. 레인 사이 벽 둘은 십자로 **일부러 교차**해서 두 통로를 다
        // 막으므로, 서로 견주면 늘 790쯤 겹쳤다고 나온다 — 그건 결함이 아니다.
        foreach (MapLayout.Island wall in InterLaneWallFootprints())
        {
            foreach (MapLayout.Island land in ground)
            {
                if (!Overlaps(wall, land, out float depth) || depth <= OverlapTolerance) continue;
                hits.Add($"{wall.name} ↔ {land.name} ({depth:0.#})");
            }
        }

        return (hits.Count == 0 ? "" : "\n⚠️ 겹칩니다: " + string.Join(", ", hits)) + SpacingReport();
    }

    // 사장님이 「더 벌려라 / 좁혀라」로 말씀하시는 간격들을 숫자로 찍는다(PM 요청 2026-09-24).
    // 다음에 그 지시가 올 때 **지금이 얼마인지**를 알아야 얼마나 움직일지 정할 수 있다.
    //
    // 여기서 찍는 값은 MapLayout의 목표 상수가 아니라 **섬 정의에서 재서 낸 것**이다 —
    // 유도식(DownShift·GachaCenterX)이 섬 정의와 어긋나면 목표에서 벗어나고, 그러면 아래
    // 경고가 뜬다. 목표를 그대로 다시 찍으면 어긋난 날에도 "500"이라고 나와 아무 도움이 안 된다.
    static string SpacingReport()
    {
        MapLayout.SpacingReadout s = MapLayout.MeasureSpacing();

        string report =
            $"\n간격(치마 기준): 레인 남쪽 끝 {MapLayout.LaneClusterSouthEdgeZ:0.0} ↔ 섬 무리 윗변 " +
            $"{s.islandClusterNorthZ:0.0} = **{s.laneToIsland:0.0}** (목표 {MapLayout.LaneToIslandGapZ:0.0})" +
            $"\n  스토리존↔뽑기섬 {s.storyToGacha:0.0} · 뽑기섬↔조합판 {s.gachaToCombine:0.0} " +
            $"(목표 {MapLayout.IslandGapX:0.0})";

        // 문턱은 절대값이 아니라 비례로 — 간격 목표가 바뀌어도 같이 따라오게 한다.
        float tolerance = Mathf.Max(1f, MapLayout.LaneToIslandGapZ * 0.01f);
        if (Mathf.Abs(s.laneToIsland - MapLayout.LaneToIslandGapZ) > tolerance)
            report += $"\n  ⚠️ 레인↔섬 간격이 목표에서 {s.laneToIsland - MapLayout.LaneToIslandGapZ:+0.0;-0.0} " +
                      "벗어났습니다 — MapLayout.DownShift의 유도식이 섬 정의와 어긋났습니다 " +
                      "(가장 북쪽 섬이 뽑기섬이 아니게 됐거나, 뽑기섬 z가 바뀌었습니다).";

        float gapTolerance = Mathf.Max(1f, MapLayout.IslandGapX * 0.01f);
        if (Mathf.Abs(s.storyToGacha - MapLayout.IslandGapX) > gapTolerance ||
            Mathf.Abs(s.gachaToCombine - MapLayout.IslandGapX) > gapTolerance)
            report += "\n  ⚠️ 섬 사이 x 간격이 목표와 다릅니다 — 섬 중심을 간격에서 유도하지 않고 " +
                      "손으로 박은 자리가 있습니다.";

        return report;
    }

    // 레인 사이 벽은 틈을 확실히 메우려고 InterLaneWallMargin의 절반만큼 **일부러** 섬을 문다.
    // 그만큼은 겹침이 아니다. 대신 얼마나 겹쳤는지를 같이 찍어서, 문턱 아래라고 조용히
    // 넘어간 것인지 애초에 안 겹친 것인지 구별되게 한다.
    const float OverlapTolerance = InterLaneWallMargin;

    static bool Overlaps(MapLayout.Island a, MapLayout.Island b, out float depth)
    {
        float x = (a.size.x + b.size.x) * 0.5f - Mathf.Abs(a.center.x - b.center.x);
        float z = (a.size.y + b.size.y) * 0.5f - Mathf.Abs(a.center.y - b.center.y);
        depth = Mathf.Min(x, z);
        return x > 0f && z > 0f;
    }

    // 기존 씬은 전부 원점 근처를 전제로 배치돼 있었다. 새 맵에서 원점은 바다 한가운데라,
    // 그대로 두면 적은 물 위를 걷고 위습은 닿을 수 없는 곳에 생긴다.
    // 시작 자원 (사장님 확정 2026-09-01): 30엔 + 목재 1개.
    // [SerializeField] 기본값을 바꿔봐야 씬에 이미 직렬화된 컴포넌트는 옛 값을 그대로 쓴다.
    // 여기서 명시적으로 덮어써야 실제로 반영된다.
    const int StartingYen = 30;
    const int StartingWood = 1;

    // 시작 위습: 랜덤 위습 5개(사장님 확정 2026-09-01). 이걸 자원 칸 북쪽 포탈에 넣으면
    // 흔함 유닛이 하나씩 나온다. 씬에 이미 직렬화된 값이 있어도 여기서 덮어쓴다.
    const int StartingWispCount = 5;

    // 라운드 클리어 보상: 라운드 하나 지날 때마다 랜덤위습 2개(사장님 지시). 시작 위습과
    // 같은 에셋(Wisp_랜덤유닛)을 재사용한다 — "랜덤위습"이 곧 이 등급(랜덤유닛)의 위습이다.
    const int RoundRewardWispCount = 2;

    // 위습을 영혼처럼 보이게 하고 맵 크기에 맞춰 키운다.
    // 프리팹 기본 크기가 0.6이라 유닛(키 20) 옆에 두면 먼지처럼 보인다.
    //
    // 🔴 2026-09-23 사장님: "선택위습 속도가 너무 느리다, 크기도 키워야 할 듯".
    // 둘 다 맵 배율을 안 타고 있었다.
    //  · 속도: 위습은 플레이어가 포탈까지 **끌고 가는** 것이라, 걸어갈 거리가 4.167배면
    //    속도도 4.167배여야 체감이 같다. 25 → 104.2.
    //  · 크기: 3.6(유닛의 18%, 점처럼 보임) → 15(75%) → **30(100%)**.
    //    🔴 75%로 올린 뒤에도 사장님이 **또** 작다고 하셨다(같은 지적 두 번). 그래서 이번엔
    //    **유닛과 같은 키**로 맞춘다 — 위습은 플레이어가 직접 끌고 다니는 것이라 유닛보다
    //    작을 이유가 없다. 프리팹 기본 몸이 0.6이므로 배율 12×Scale이 몸 키 30이다.
    // ⚠️ 아래 agent.radius·height는 WispScale로 나눠서 넣으므로 월드 기준 값(0.28·2)은
    //    그대로 유지된다 — 몸만 커지고 길찾기 판정은 안 커진다(좁은 데 못 들어가는 일 없음).
    const float WispScale = 12f * MapLayout.Scale;
    const float WispSpeed = 25f * MapLayout.Scale;
    const string WispPrefabPath = "Assets/Prefabs/WispPrefab.prefab";

    [MenuItem("Tools/맵/위습 모양 맞추기")]
    public static void ShapeWispPrefabMenu()
    {
        string report = ShapeWispPrefab();
        Debug.Log("[맵] " + report);
        EditorGuards.Dialog(Title, report.TrimStart('\n'), "확인");
    }

    static string ShapeWispPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WispPrefabPath);
        if (prefab == null) return $"\n⚠️ 위습 프리팹을 못 찾았습니다: {WispPrefabPath}";

        GameObject root = PrefabUtility.LoadPrefabContents(WispPrefabPath);

        root.transform.localScale = Vector3.one * WispScale;
        LiftWispBody(root);

        // NavMeshAgent의 반지름·높이는 트랜스폼 스케일을 그대로 따라간다. 프리팹을 10배로 키우면
        // 발자국도 10배가 되어, 굽힌 NavMesh(반지름 0.5 기준)보다 훨씬 커진다. 그러면 벽 근처와
        // 벽으로 두른 칸 안에서 설 자리를 못 찾아 아예 안 움직인다.
        // 보이는 크기만 키우고 실제 발자국은 굽힌 값보다 살짝 작게 되돌린다.
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // 우물 한 변이 70이다. 8로는 건너는 데 9초가 걸려서 조작이 답답하다.
            agent.speed = WispSpeed;
            agent.acceleration = WispSpeed * 5f;

            // 유닛과 같은 값(0.28). 아군끼리는 살짝 비켜주기만 하고 서로 통과하듯 겹친다 —
            // 위습도 같은 규칙이어야 한 칸에 여러 개를 모아둘 수 있다.
            agent.radius = 0.28f / WispScale;
            agent.height = 2f / WispScale;
            // 띄우는 건 baseOffset이 아니라 몸(자식)을 올려서 한다 — 아래 LiftWispBody 참고.
            agent.baseOffset = 0f;
        }

        // 영혼답게 — 스스로 빛나고 반쯤 비친다. 단단한 공이면 그냥 구슬로 보인다.
        Renderer renderer = root.GetComponentInChildren<Renderer>(true);
        if (renderer != null) renderer.sharedMaterial = WispSoulMaterial();

        // 빛무리. 발광 머티리얼만으로는 어두운 데서 티가 안 난다.
        Transform glow = root.transform.Find("영혼빛");
        if (glow == null)
        {
            GameObject lightObject = new GameObject("영혼빛");
            lightObject.transform.SetParent(root.transform, false);
            glow = lightObject.transform;
        }
        // ??를 쓰면 안 된다. 유니티는 ==를 오버로드해서 "없는 컴포넌트"를 진짜 null이 아닌
        // 가짜 null로 돌려주는데, ??는 그 오버로드를 안 거치므로 가짜 null을 그대로 통과시킨다.
        // 그러면 AddComponent가 안 불리고 다음 줄에서 MissingComponentException이 난다.
        // 그 예외가 Generate()를 통째로 중단시켜서 NavMesh 굽기까지 못 갔다.
        Light light = glow.GetComponent<Light>();
        if (light == null) light = glow.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = WispSoulColor;
        light.range = WispScale * 4f;
        light.intensity = 3f;

        PrefabUtility.SaveAsPrefabAsset(root, WispPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        return $"\n위습: 크기 {WispScale:F0}, 영혼 형태(발광·반투명)로 맞췄습니다.";
    }

    // 위습 몸은 구인데 메시가 루트에 붙어 있어서, 트랜스폼(=구의 한가운데)을 바닥에 두면
    // 아래 절반이 땅에 묻힌다. baseOffset으로 올리면 스케일을 타는지가 불확실해서,
    // 몸을 자식으로 떼어내 확실하게 올린다. 루트에는 콜라이더·에이전트·스크립트만 남는다.
    const float WispFloatHeight = 0.85f;   // 로컬 기준. 반지름 0.5보다 조금 위 — 영혼이니 떠 있게.

    static void LiftWispBody(GameObject root)
    {
        Transform body = root.transform.Find("몸");
        if (body == null)
        {
            MeshFilter filter = root.GetComponent<MeshFilter>();
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return;   // 이미 옮겨둔 프리팹

            GameObject visual = new GameObject("몸", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            visual.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;

            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
            body = visual.transform;
        }

        body.localPosition = new Vector3(0f, WispFloatHeight, 0f);

        // 클릭 판정도 눈에 보이는 몸을 따라가야 한다.
        if (root.TryGetComponent(out SphereCollider sphere))
            sphere.center = new Vector3(0f, WispFloatHeight, 0f);
    }

    static readonly Color WispSoulColor = new Color(0.55f, 0.85f, 1f);

    // 반투명 발광. URP/Lit의 Surface Type을 Transparent로 돌려야 알파가 먹는다.
    //
    // 🔴 2026-09-24: 발광이 4배라 화면에서 **형체 없는 흰 덩어리**로 나왔다(플레이 캡처).
    //    색이 (2.2, 3.4, 4.0)이면 세 채널이 다 1을 한참 넘겨 흰색으로 잘리고, 그러면
    //    파란 기도 없고 구의 명암도 없다 — 「반쯤 비치는 영혼」이 아니라 흰 공이 된다.
    //    1.1배로 낮춘다. 그래도 어두운 데서 스스로 빛나되, 색과 둥근 티가 남는다.
    const float WispEmissionBoost = 1.1f;

    static Material WispSoulMaterial()
    {
        const string path = MaterialFolder + "/wisp_soul.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;

        // ⚠️ 예전엔 **있으면 그대로 돌려줬다.** 그래서 위 값을 고쳐도 이미 만들어진 에셋엔
        //    영영 안 닿았다 — 바닥 텍스처가 `_BaseMap` 빈 채로 남아 있던 것과 같은 병이다
        //    (f3da5f93). 있으면 값을 **다시 써 넣는다.**
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (isNew) material = new Material(shader);

        material.SetColor("_BaseColor", new Color(WispSoulColor.r, WispSoulColor.g, WispSoulColor.b, 0.55f));
        material.SetColor("_EmissionColor", WispSoulColor * WispEmissionBoost);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        material.SetFloat("_Surface", 1f);            // 0 불투명 / 1 투명
        material.SetFloat("_Blend", 0f);              // Alpha
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (isNew)
        {
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            // SetDirty만으로는 **디스크에 안 써진다.** 다음 리로드 때 옛 값이 그대로 돌아온다
            // (09-24에 실제로 그랬다 — 코드는 1.1배인데 .mat은 4배 그대로였다).
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        return material;
    }

    static string WireStartingWisps()
    {
        RewardDistributor distributor = Object.FindFirstObjectByType<RewardDistributor>(FindObjectsInactive.Include);
        if (distributor == null) return "";

        WispData wisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");
        if (wisp == null) return "\n⚠️ 시작 위습 에셋(Wisp_랜덤유닛)을 못 찾았습니다.";

        SerializedObject so = new SerializedObject(distributor);
        so.FindProperty("startingWisp").objectReferenceValue = wisp;
        so.FindProperty("startingWispCount").intValue = StartingWispCount;
        so.ApplyModifiedProperties();

        return $"\n시작 위습을 {wisp.wispName} {StartingWispCount}개로 맞췄습니다.";
    }

    // 05번 「고대의 배」 지급 경로 ㉡(스토리 7=Story07_메가스터디 클리어 보상, 사장님 결정) —
    // RewardDistributor.GrantAncientShip이 읽을 결과 유닛을 연결한다.
    static string WireAncientShipReward()
    {
        RewardDistributor distributor = Object.FindFirstObjectByType<RewardDistributor>(FindObjectsInactive.Include);
        if (distributor == null) return "";

        UnitData ship = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Special/Unit_고대의배_h05Y.asset");
        if (ship == null) return "\n⚠️ 고대의 배 에셋(Unit_고대의배_h05Y)을 못 찾았습니다.";

        SerializedObject so = new SerializedObject(distributor);
        so.FindProperty("ancientShipUnit").objectReferenceValue = ship;
        so.ApplyModifiedProperties();

        return "\n스토리 7(임펠다운 대응) 보상에 고대의 배를 연결했습니다.";
    }

    // 레인 하나에 이만큼 쌓이면 데스카운트가 깎인다. 원작 udg_ModeEnemyInt=70(여섯 난이도 공통).
    // 2026-09-03 사장님 지시로 100이었다가 2026-09-11 「원작대로 바꾸자」로 70.
    // RoundManager는 "Map" 루트 밖의 독립 오브젝트라(MapGenerator가 새로 안 만들고 찾기만
    // 한다) 맵을 다시 만들어도 이 값이 안 사라진다 — 씬 파일을 직접 안 건드리고 여기서만
    // 관리한다.
    const int EnemyCountThreshold = 70;
    // 원작 데스카운트 9회 · 0.65초 틱. 씬에 옛 값(10회 · 1초)이 직렬화돼 있어 코드 기본값만
    // 고치면 안 먹는다(유니티는 씬을 이긴다) — 임계치와 같이 여기서 덮어쓴다.
    const int StartingDeathCount = 9;
    const float DeathCountTickInterval = 0.65f;

    // 라운드 길이도 여기서 맞춘다 — 원작값(war3map.j 확인, 2026-09-04):
    // 일반 40.65초 / 보스 75.4초 / 신세계(61+) 38.67초. 우리는 셋 다 28초였다.
    // 씬에 옛 값이 직렬화돼 있으면 코드 기본값을 고쳐도 안 먹는다(유니티는 씬을 이긴다).
    // WaveWiring에도 같은 보정을 넣어뒀는데 그건 별도 메뉴라 맵 생성 때 안 돌아서,
    // 2026-09-05 맵 재생성 뒤에도 28이 그대로 남아 있었다.
    const float NormalRoundDuration = 40.65f;

    static string WireRoundRewardWisp()
    {
        RoundManager roundManager = Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        if (roundManager == null) return "";

        WispData wisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");
        if (wisp == null) return "\n⚠️ 라운드 보상 위습 에셋(Wisp_랜덤유닛)을 못 찾았습니다.";

        SerializedObject so = new SerializedObject(roundManager);
        so.FindProperty("roundRewardWisp").objectReferenceValue = wisp;
        so.FindProperty("roundRewardCount").intValue = RoundRewardWispCount;
        so.FindProperty("enemyCountThreshold").intValue = EnemyCountThreshold;
        so.FindProperty("startingDeathCount").intValue = StartingDeathCount;
        so.FindProperty("deathCountTickInterval").floatValue = DeathCountTickInterval;

        // 28(옛 값)일 때만 덮어쓴다 — 사람이 일부러 바꿔둔 값은 건드리지 않는다.
        SerializedProperty duration = so.FindProperty("roundDuration");
        bool durationFixed = duration != null && Mathf.Approximately(duration.floatValue, 28f);
        if (durationFixed) duration.floatValue = NormalRoundDuration;

        so.ApplyModifiedProperties();

        return $"\n라운드 클리어 보상을 {wisp.wispName} {RoundRewardWispCount}개로, " +
               $"패배 임계치를 레인당 {EnemyCountThreshold}마리 · 데스카운트 {StartingDeathCount}회({DeathCountTickInterval}초 틱, 누적)로 맞췄습니다." +
               (durationFixed ? $"\n라운드 길이를 원작값 {NormalRoundDuration}초로 고쳤습니다(옛 값 28초)." : "");
    }

    // 난이도 6종(2026-09-11, PM 지시) — SideBossManager와 같은 관례로 "Map" 루트 밖의 독립
    // 오브젝트 둘을 둔다. DifficultyManager는 상태(선택된 모드)만 들고, DifficultySelectHud는
    // 그 상태를 읽어 호스트에게 6버튼을, 나머지에게 대기 문구를 그린다 — 둘 다 서로 직접
    // 참조를 안 갖고 DifficultyManager.Instance 정적 접근으로만 통신한다(SerializedObject로
    // 이어줄 필드가 없다). 씬을 다시 만들어도 없으면 새로 만들고, 있으면 그대로 둔다(상태를
    // 안 갖는 컴포넌트라 재배선할 것도 없다).
    static string WireDifficultySystem()
    {
        bool madeManager = false;
        if (Object.FindFirstObjectByType<DifficultyManager>(FindObjectsInactive.Include) == null)
        {
            new GameObject("DifficultyManager").AddComponent<DifficultyManager>();
            madeManager = true;
        }

        bool madeHud = false;
        if (Object.FindFirstObjectByType<DifficultySelectHud>(FindObjectsInactive.Include) == null)
        {
            new GameObject("DifficultySelectHud").AddComponent<DifficultySelectHud>();
            madeHud = true;
        }

        if (!madeManager && !madeHud) return "\n난이도 시스템(모드 매니저·선택창)은 이미 배선돼 있습니다.";

        return "\n난이도 시스템을 배선했습니다" +
               (madeManager ? " (DifficultyManager 신설)" : "") +
               (madeHud ? " (DifficultySelectHud 신설)" : "") + ".";
    }

    // 신세계 사이드보스(도플라밍고·빅맘·카이도, ORIGINAL_BOSS_COMBAT_SPEC.md) — RoundManager처럼
    // "Map" 루트 밖의 독립 오브젝트로 둔다. 맵을 다시 만들어도 스턴게이지·정산 배율(플레이어별
    // 배열) 상태가 안 사라진다. 없으면 여기서 새로 만들고, 있으면 참조만 다시 맞춘다.
    static string WireSideBossManager()
    {
        SideBossManager manager = Object.FindFirstObjectByType<SideBossManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject managerObject = new GameObject("SideBossManager");
            manager = managerObject.AddComponent<SideBossManager>();
        }

        RoundManager roundManager = Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        WaveSpawner waveSpawner = Object.FindFirstObjectByType<WaveSpawner>(FindObjectsInactive.Include);

        EnemyData boss62 = AssetDatabase.LoadAssetAtPath<EnemyData>(
            "Assets/Data/Enemies/Enemy_SideBoss62_도플라밍고.asset");
        EnemyData boss66 = AssetDatabase.LoadAssetAtPath<EnemyData>(
            "Assets/Data/Enemies/Enemy_SideBoss66_빅맘.asset");
        EnemyData boss71 = AssetDatabase.LoadAssetAtPath<EnemyData>(
            "Assets/Data/Enemies/Enemy_SideBoss71_카이도.asset");

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("roundManager").objectReferenceValue = roundManager;
        so.FindProperty("waveSpawner").objectReferenceValue = waveSpawner;
        so.FindProperty("boss62").objectReferenceValue = boss62;
        so.FindProperty("boss66").objectReferenceValue = boss66;
        so.FindProperty("boss71").objectReferenceValue = boss71;
        so.ApplyModifiedProperties();

        string warnings = "";
        if (roundManager == null) warnings += "\n  ⚠️ RoundManager를 못 찾았습니다.";
        if (waveSpawner == null) warnings += "\n  ⚠️ WaveSpawner를 못 찾았습니다.";
        if (boss62 == null) warnings += "\n  ⚠️ Enemy_SideBoss62_도플라밍고 에셋을 못 찾았습니다.";
        if (boss66 == null) warnings += "\n  ⚠️ Enemy_SideBoss66_빅맘 에셋을 못 찾았습니다.";
        if (boss71 == null) warnings += "\n  ⚠️ Enemy_SideBoss71_카이도 에셋을 못 찾았습니다.";

        return "\n신세계 사이드보스 매니저(R62·66·71) 배선 완료." + warnings;
    }

    static string SetStartingResources(PlayerContext[] contexts)
    {
        int walletsSet = 0;

        foreach (PlayerContext context in contexts)
        {
            if (context.GoldWallet != null)
            {
                SerializedObject so = new SerializedObject(context.GoldWallet);
                so.FindProperty("startingGold").intValue = StartingYen;
                so.ApplyModifiedProperties();
                walletsSet++;
            }

            if (context.ResourceWallet == null) continue;

            SerializedObject resources = new SerializedObject(context.ResourceWallet);
            SerializedProperty starting = resources.FindProperty("startingAmounts");
            starting.ClearArray();
            starting.InsertArrayElementAtIndex(0);
            SerializedProperty wood = starting.GetArrayElementAtIndex(0);
            wood.FindPropertyRelative("type").enumValueIndex = (int)ResourceType.Wood;
            wood.FindPropertyRelative("amount").intValue = StartingWood;
            resources.ApplyModifiedProperties();
        }

        return walletsSet == 0 ? "" : $"\n시작 자원을 {StartingYen}엔 + 목재 {StartingWood}개로 맞췄습니다({walletsSet}명).";
    }

    static string RewireScene(List<WaypointPath> lanePaths)
    {
        string report = "";

        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>(FindObjectsInactive.Include);
        if (spawner != null && lanePaths.Count > 0)
        {
            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty single = so.FindProperty("path");
            if (single != null)
            {
                single.objectReferenceValue = lanePaths[0];
                so.ApplyModifiedProperties();
                report += "\n적 경로를 Lane1_Path로 옮겼습니다.";
            }
        }

        GameObject oldLane = GameObject.Find("Lane");
        if (oldLane != null)
        {
            oldLane.SetActive(false);
            report += "\n기존 Lane 경로는 비활성화했습니다.";
        }

        // 위습은 RewardDistributor가 PlayerContext의 위치에 생성한다.
        MapLayout.Island gacha = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        PlayerContext[] contexts = Object.FindObjectsByType<PlayerContext>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerContext context in contexts)
        {
            context.transform.position = new Vector3(
                gacha.center.x,
                MapLayout.IslandTop,
                gacha.center.y + gacha.size.y * 0.5f - 6f);
        }
        if (contexts.Length > 0)
            report += $"\n위습이 생기는 위치(PlayerContext {contexts.Length}개)를 뽑기 섬으로 옮겼습니다.";

        // 배선 단계는 서로 독립이다. 한 줄로 이어놓으면 앞에서 예외가 하나 나는 순간
        // 뒤가 통째로 안 돈다 — 실제로 ShapeWispPrefab이 터져서 그 뒤의 조합식 등록이
        // 몇 주 동안 조용히 건너뛰어졌고, 맵은 멀쩡해 보이는데 조합만 안 되는 상태였다.
        // 하나가 죽어도 나머지는 돌게 두고, 죽은 것은 이름을 대며 보고한다.
        report += Step("시작 자원", () => SetStartingResources(contexts));
        report += Step("위습 프리팹", ShapeWispPrefab);
        report += Step("시작 위습", WireStartingWisps);
        report += Step("고대의 배 지급(스토리7)", WireAncientShipReward);
        report += Step("라운드 보상 위습", WireRoundRewardWisp);
        report += Step("난이도 시스템", WireDifficultySystem);
        report += Step("사이드보스 매니저", WireSideBossManager);
        report += Step("조합 지갑", WireCombineWallet);
        report += Step("창고", MoveWarehousesToIslands);
        report += Step("조합식", WireAllRecipes);
        report += Step("카메라", SetUpCamera);
        report += Step("플레이어", EnsureFourPlayers);
        report += Step("레인 경로", () => WireLanePaths(lanePaths));

        return report;
    }

    static string Step(string name, System.Func<string> action)
    {
        try
        {
            return action();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return $"\n⚠️ '{name}' 배선이 실패했습니다: {e.Message}";
        }
    }

    // 상위 등급 레시피 18개는 골드를 요구한다(최대 20000). 지갑이 안 붙어 있으면 그때 가서 조합이 막힌다.
    static string WireCombineWallet()
    {
        CombineSystem combine = Object.FindFirstObjectByType<CombineSystem>(FindObjectsInactive.Include);
        if (combine == null) return "";

        SerializedObject so = new SerializedObject(combine);
        SerializedProperty wallet = so.FindProperty("goldWallet");
        if (wallet == null || wallet.objectReferenceValue != null) return "";

        GoldWallet local = PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
        if (local == null) local = combine.GetComponent<GoldWallet>();
        if (local == null) return "";

        wallet.objectReferenceValue = local;
        so.ApplyModifiedProperties();
        return "\nCombineSystem에 골드 지갑을 연결했습니다.";
    }

    // 레시피는 199개다. 테스트용 13개만 붙어 있으면 안흔함까지밖에 못 만든다.
    // 맵 생성은 초기화 동작이니 여기서 전부 걸어준다.
    static string WireAllRecipes()
    {
        CombineSystem combine = Object.FindFirstObjectByType<CombineSystem>(FindObjectsInactive.Include);
        if (combine == null) return "";

        List<CombineRecipe> recipes = AssetDatabase
            .FindAssets("t:CombineRecipe", new[] { "Assets/Data/Recipes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<CombineRecipe>)
            .Where(recipe => recipe != null)
            .ToList();

        SerializedObject so = new SerializedObject(combine);
        SerializedProperty list = so.FindProperty("recipes");
        int before = list.arraySize;

        list.ClearArray();
        for (int i = 0; i < recipes.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }
        so.ApplyModifiedProperties();

        return $"\n조합 레시피 {before}개 → {recipes.Count}개로 연결했습니다.";
    }

    // 창고는 이제 섬 위에 있다. 플레이어 오브젝트에 남아 있던 옛 창고를 지운다 —
    // 같은 ownerPlayerId를 가진 창고가 둘이면 FindWarehouse가 어느 쪽을 잡을지 정해지지 않는다.
    static string MoveWarehousesToIslands()
    {
        int removed = 0;

        foreach (PlayerContext context in Object.FindObjectsByType<PlayerContext>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (Warehouse stale in context.GetComponents<Warehouse>())
            {
                Object.DestroyImmediate(stale);
                removed++;
            }
        }

        // 컨트롤러가 옛 창고를 직접 가리키고 있으면 PlayerContext를 거치지 않는다. 비워서 다시 찾게 한다.
        WarehouseController controller = Object.FindFirstObjectByType<WarehouseController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("warehouse").objectReferenceValue = null;
            so.ApplyModifiedProperties();
        }

        return removed > 0 ? $"\n플레이어에 남아 있던 옛 창고 {removed}개를 지우고 섬 창고로 옮겼습니다." : "";
    }

    static Warehouse FindWarehouse(int playerId)
    {
        foreach (Warehouse warehouse in Object.FindObjectsByType<Warehouse>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (warehouse.OwnerPlayerId == playerId)
                return warehouse;

        return null;
    }

    // 협동 4인 구조라 PlayerContext도 4개 있어야 팀 현황판이 채워진다.
    // 0번은 기존 GameManager가 갖고 있으므로 1~3번만 만든다.
    static string EnsureFourPlayers()
    {
        int created = 0;

        PlayerContext[] existing = Object.FindObjectsByType<PlayerContext>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int playerId = 1; playerId < MapLayout.Lanes.Length; playerId++)
        {
            // 이미 있는 슬롯도 비어 있는 상태로 되돌린다. 맵 생성은 초기화 동작이고,
            // 처음 만들 때만 비워두면 두 번째 실행부터 예전 상태가 그대로 남는다.
            PlayerContext already = System.Array.Find(existing, c => c.PlayerId == playerId);
            if (already != null)
            {
                // Player2~4 GameObject는 "Map" 루트 밖(씬 최상위)에 있어 재생성 때 안 지워진다.
                // 반면 창고 섬은 "Map" 아래라 매번 통째로 새로 지어진다 — 옛 Warehouse는
                // 사라지고 새 인스턴스가 생기는데, 여기서 재조회를 안 하면 이 PlayerContext는
                // 죽은(파괴된) 창고를 계속 가리킨다. 0번(로컬)은 아래서 매번 무조건 다시 찾아
                // 거는데 1~3번은 "이미 있으니 넘어간다"에 걸려 그 갱신을 영영 못 받았다 —
                // 구현담당2의 씬 감사(2026-09-05)가 찾은 실제 끊김이 이거다.
                SerializedObject reset = new SerializedObject(already);
                bool changed = false;

                if (already.IsOccupied)
                {
                    reset.FindProperty("occupied").boolValue = false;
                    changed = true;
                }

                Warehouse warehouse = FindWarehouse(playerId);
                if (reset.FindProperty("warehouse").objectReferenceValue != warehouse)
                {
                    reset.FindProperty("warehouse").objectReferenceValue = warehouse;
                    changed = true;
                }

                if (changed) reset.ApplyModifiedProperties();
                continue;
            }

            MapLayout.Island lane = MapLayout.Lanes[playerId];
            GameObject player = new GameObject($"Player{playerId + 1}");
            player.transform.position = new Vector3(lane.center.x, MapLayout.IslandTop, lane.center.y);

            GoldWallet gold = player.AddComponent<GoldWallet>();
            ResourceWallet resources = player.AddComponent<ResourceWallet>();
            UnitInventory units = player.AddComponent<UnitInventory>();
            GamblingProgress gambling = player.AddComponent<GamblingProgress>();
            // 특성강화 상태(특성포인트 4갈래 + 방깎·마방깍) — 이걸 안 붙이면 그 넷이 전부
            // null 리턴에서 안 벗어난다(WIRING_AUDIT.md §1). 유닛강화소는 이것과 별개로
            // UnitUpgradeShop.ResearchLabImplemented가 계속 잠가둔다(사장님 결정 05번,
            // 연구소 완성 전까지 골드만 먹는 상점이 되는 걸 막기 위함) — 이 컴포넌트를
            // 붙인다고 그 상점이 같이 풀리지 않는다.
            UnitUpgrades upgrades = player.AddComponent<UnitUpgrades>();
            // 11번(영속 저장) — GoldWallet 등과 같은 결로 PlayerContext를 되짚어 참조하지
            // 않는다, 그래서 파일 키로 쓸 playerId를 자기 것으로 따로 들고 있다. 아래에서
            // PlayerContext.playerId와 같은 값으로 맞춰 꽂는다.
            PersistentSave save = player.AddComponent<PersistentSave>();
            PlayerContext context = player.AddComponent<PlayerContext>();

            SerializedObject so = new SerializedObject(context);
            so.FindProperty("playerId").intValue = playerId;
            // 구조만 만들어 두고 자리는 비워 둔다. 멀티플레이가 붙기 전까지는
            // 이 레인에 적이 안 나오고 보상도 안 나간다. 테스트할 땐 인스펙터에서 체크.
            so.FindProperty("occupied").boolValue = false;
            so.FindProperty("goldWallet").objectReferenceValue = gold;
            so.FindProperty("resourceWallet").objectReferenceValue = resources;
            so.FindProperty("unitInventory").objectReferenceValue = units;
            so.FindProperty("gamblingProgress").objectReferenceValue = gambling;
            so.FindProperty("unitUpgrades").objectReferenceValue = upgrades;
            so.FindProperty("persistentSave").objectReferenceValue = save;
            so.FindProperty("warehouse").objectReferenceValue = FindWarehouse(playerId);
            so.ApplyModifiedProperties();

            SetPersistentSavePlayerId(save, playerId);

            created++;
        }

        // 0번(로컬)은 반드시 앉아 있어야 한다. 이 값이 꺼져 있으면 내 레인에도 적이 안 나온다.
        // 창고 참조도 섬 쪽으로 다시 걸어준다 — 예전엔 GameManager에 붙어 있었다.
        PlayerContext local = System.Array.Find(existing, c => c.PlayerId == 0);
        if (local != null)
        {
            SerializedObject so = new SerializedObject(local);
            so.FindProperty("occupied").boolValue = true;
            so.FindProperty("warehouse").objectReferenceValue = FindWarehouse(0);
            so.ApplyModifiedProperties();
        }

        // 이미 있던 플레이어에도 빠진 조각을 채운다. 위의 생성 블록은 **새로 만드는 슬롯**에만
        // 도니, 필드가 나중에 추가되면 기존 플레이어는 영영 빈 채로 남는다.
        // GamblingProgress가 실제로 그랬다 — 없으면 GamblingShop.CanRoll이 돈 도박을
        // 무조건 false로 돌려서, 10엔 도박 칸이 눌러도 아무 반응이 없었다.
        int repaired = RepairPlayerParts();
        bool unionWispFixed = RepairRewardDistributorUnionWisp();
        int rerollFixed = RepairUniqueReroll();

        return "\n플레이어 2~4번 자리는 비워뒀습니다 — 그 레인엔 적이 안 나옵니다."
             + (created > 0 ? $" (새로 만든 슬롯 {created}개)" : "")
             + (repaired > 0 ? $"\n기존 플레이어 {repaired}명에게 빠져 있던 조각을 채웠습니다." : "")
             + (unionWispFixed ? "\n연합세력 항법 위습(RewardDistributor.unionWisp)을 채웠습니다." : "")
             + (rerollFixed > 0 ? $"\n희귀함 리롤(고유 재추첨)을 {rerollFixed}곳에 배선했습니다." : "")
             + WiringReport();
    }

    // 2026-09-07 추가(연합세력 항법 훅) — RewardDistributor는 PlayerContext와 달리 씬에
    // 하나뿐인 매니저(Instance 싱글턴)라 RepairPlayerParts의 플레이어별 루프 밖에서 한 번만
    // 검사한다. unionWisp가 비어 있으면 Wisp_흔함.asset(e0IX)을 꽂는다 — 이미 값이 있으면
    // 안 건드리고, 자산 자체가 없으면(경로가 바뀌었거나 지워졌으면) 경고만 남기고 넘어간다.
    static bool RepairRewardDistributorUnionWisp()
    {
        RewardDistributor distributor = Object.FindFirstObjectByType<RewardDistributor>(FindObjectsInactive.Include);
        if (distributor == null) return false;

        WispData unionWisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_흔함.asset");
        if (unionWisp == null)
        {
            Debug.LogWarning("MapGenerator: Assets/Data/Wisps/Wisp_흔함.asset을 찾지 못해 " +
                              "RewardDistributor.unionWisp를 채우지 못했습니다(연합세력 항법 위습 지급 불가).");
            return false;
        }

        return EnsureAssetRef(distributor, "unionWisp", unionWisp);
    }

    /// <summary>이미 물려 있던 참조 중 **남의 오브젝트를 가리키는 것**만 한 줄로. 없으면 빈 문자열.</summary>
    static string WiringReport()
    {
        return WiringKept.Count == 0 ? ""
            : "\n⚠️ 이미 물려 있어 안 건드린 참조 중 이상한 것 " + WiringKept.Count + "건: " +
              string.Join(", ", WiringKept.Distinct());
    }

    static int RepairPlayerParts()
    {
        int repaired = 0;
        WiringKept.Clear();

        foreach (PlayerContext context in Object.FindObjectsByType<PlayerContext>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(context);
            bool changed = false;

            changed |= EnsurePart<GoldWallet>(context, so, "goldWallet");
            changed |= EnsurePart<ResourceWallet>(context, so, "resourceWallet");
            changed |= EnsurePart<UnitInventory>(context, so, "unitInventory");
            changed |= EnsurePart<GamblingProgress>(context, so, "gamblingProgress");
            changed |= EnsurePart<UnitUpgrades>(context, so, "unitUpgrades");
            changed |= EnsurePart<PersistentSave>(context, so, "persistentSave");

            // 2026-09-07 추가 — 이 셋은 PlayerContext에 **필드만** 먼저 생기고 생성 코드가
            // 안 따라왔다. 그래서 붙인 그 순간부터 4명 전부 null이었고, 항법 5택1·
            // 패왕의길 누적(Damage_level_Fixed)·아이템 도박이 **배선된 채로 한 번도 안 돌았다.**
            // GamblingProgress가 똑같이 그랬던 전례가 바로 위 주석에 있다 — 같은 사고가
            // 두 번째다. 순서가 있다: 아래 형제 참조를 걸려면 피참조 쪽이 먼저 있어야 한다.
            changed |= EnsurePart<DamageLevelFixedState>(context, so, "damageLevelFixedState");
            changed |= EnsurePart<NavigationState>(context, so, "navigationState");
            changed |= EnsurePart<ItemGambleState>(context, so, "itemGambleState");

            // 2026-09-09 추가 — 같은 사고의 세 번째다. 이번엔 필드조차 없었다:
            // ItemGambleState는 4명 다 붙어 있는데 ItemInventory는 씬 전체에 하나뿐이라,
            // 누가 도박에 이겨도 아이템이 전부 플레이어 0에게 갔다(원작은 itpool[pid]로
            // 갈린다). 형제가 넷인데 받는 그릇이 하나였던 것이다.
            changed |= EnsurePart<ItemInventory>(context, so, "itemInventory");

            if (changed) so.ApplyModifiedProperties();

            // 아이템 효과 집행기(2026-09-09) — PlayerContext에 참조 필드를 두지 않는다.
            // 아무도 이걸 조회하지 않고 스스로 인벤토리 변화를 듣고 돌기만 하기 때문이다
            // (PersistentSave처럼 되짚어 참조하지 않는 부류). 그래서 EnsurePart가 아니라
            // 컴포넌트 존재만 보장한다.
            if (context.GetComponent<ItemEffectApplier>() == null)
            {
                context.gameObject.AddComponent<ItemEffectApplier>();
                changed = true;
            }

            // 형제끼리 서로를 참조한다 — PlayerContext 쪽 참조를 채운다고 이게 같이 차지
            // 않는다. NavigationState는 패왕의길 +2를 DamageLevelFixedState에 누적하고,
            // ItemGambleState는 「도움소 잠금」을 읽어 축소풀로 갈아탄다.
            changed |= EnsureSiblingRef(context.NavigationState, "damageLevelFixedState",
                                        context.DamageLevelFixedState);
            changed |= EnsureSiblingRef(context.ItemGambleState, "navigationState",
                                        context.NavigationState);

            // EnsurePart는 참조만 걸어준다 — PersistentSave 자신의 playerId 필드는 별도
            // SerializedObject라 여기서 항상 맞춰준다(참조가 이미 있던 기존 플레이어도
            // playerId가 어긋나 있을 수 있다, changed와 무관하게 매번 검사).
            if (context.PersistentSave != null)
            {
                changed |= SetPersistentSavePlayerId(context.PersistentSave, context.PlayerId);
            }

            if (!changed) continue;

            repaired++;
        }

        return repaired;
    }

    // 이미 물려 있는 참조를 본 기록. 09-24 PM 지시 — 지금은 **고치지 않고 눈에만 띄게** 한다.
    // 「빈 것만 채운다」는 「맞는지 본다」가 아니다. 참조가 **틀려 있어도** 생성기는 안 고친다.
    // 그래서 남의 오브젝트를 가리키는 것만 골라 보고문에 올린다 — 고치는 건 나중 판단이다.
    static readonly List<string> WiringKept = new List<string>();

    // 컴포넌트가 없으면 붙이고, 참조가 비어 있으면 걸어준다. 둘은 따로 어긋날 수 있다 —
    // 컴포넌트만 있고 참조가 빈 경우가 실제로 있었다.
    static bool EnsurePart<T>(PlayerContext context, SerializedObject so, string field) where T : Component
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return false;
        if (property.objectReferenceValue != null)
        {
            // 같은 오브젝트의 부품을 가리켜야 정상이다. 남을 가리키면 그건 섞인 것이다.
            if (property.objectReferenceValue is Component held && held.gameObject != context.gameObject)
                WiringKept.Add($"🔴 {context.gameObject.name}.{field} → **{held.gameObject.name}**의 {held.GetType().Name}");
            else if (!(property.objectReferenceValue is T))
                WiringKept.Add($"🔴 {context.gameObject.name}.{field} → {property.objectReferenceValue.GetType().Name}(형이 다름)");
            return false;
        }

        T part = context.GetComponent<T>();
        if (part == null) part = context.gameObject.AddComponent<T>();

        property.objectReferenceValue = part;
        return true;
    }

    // 형제 컴포넌트끼리의 참조. EnsurePart는 PlayerContext 쪽 필드만 채우므로, 컴포넌트가
    // 서로를 직접 들고 있어야 하는 경우는 이걸로 따로 걸어준다.
    static bool EnsureSiblingRef(Component owner, string field, Component target)
    {
        if (owner == null || target == null) return false;

        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return false;
        if (property.objectReferenceValue == target) return false;

        property.objectReferenceValue = target;
        so.ApplyModifiedProperties();
        return true;
    }

    // EnsureSiblingRef와 같은 모양이되 대상이 컴포넌트가 아니라 자산(ScriptableObject 등)인
    // 경우 — 2026-09-07 추가(연합세력 항법, RewardDistributor.unionWisp). 이미 값이 있으면
    // 안 건드린다(수동으로 다른 자산을 꽂아둔 경우를 덮어쓰지 않는다).
    // 🔴 「희귀함 리롤」(원작 A0VX, Trig_unique_rerole)이 통째로 안 돌고 있었다 —
    //    코드는 다 있는데 **UniqueRerollAbilityData 자산이 0개**였고, 씬에도
    //    UniqueRerollState 컴포넌트가 하나도 없었다(PlayerContext 4개 전부 빈 필드).
    //    데이터가 없으면 GamblingShop이 능력을 못 붙이고, 상태가 없으면 한도·실패율이
    //    전부 0으로 읽혀 시도 자체가 성립하지 않는다. 둘 다 조용히 실패한다.
    //    2026-09-08 버그 사냥에서 발견.
    //
    //    원작 값과 자산 기본값이 정확히 같다: 목재 2 · 한도 2(도박 특성이면 3) ·
    //    실패 20%(도박 0%). Trig_unique_rerole_Func001Func001Func005C의
    //    `GetRandomInt(1,100)<=(20-(Dobak_Tech*80))`가 그 근거다.
    static int RepairUniqueReroll()
    {
        UniqueRerollAbilityData data = AssetDatabase.LoadAssetAtPath<UniqueRerollAbilityData>(
            "Assets/Data/UniqueRerollAbility_희귀함리롤.asset");
        if (data == null)
        {
            Debug.LogWarning("[맵] UniqueRerollAbility_희귀함리롤.asset을 찾지 못해 희귀함 리롤을 배선하지 못했습니다.");
            return 0;
        }

        int fixedCount = 0;

        // ① 도박소마다 능력 데이터를 꽂는다.
        foreach (GamblingShop shop in Object.FindObjectsByType<GamblingShop>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EnsureAssetRef(shop, "uniqueRerollAbilityData", data)) fixedCount++;

        // ② 플레이어마다 상태 컴포넌트를 만들고 서로 잇는다.
        //    상태는 씬 컴포넌트라 자산처럼 그냥 꽂을 수 없다 — 없으면 만들어야 한다.
        foreach (PlayerContext context in Object.FindObjectsByType<PlayerContext>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            UniqueRerollState state = context.GetComponent<UniqueRerollState>();
            if (state == null) state = context.gameObject.AddComponent<UniqueRerollState>();

            // 상태 → 데이터, 상태 → 항법(도박 특성 판정), 플레이어 → 상태
            if (EnsureAssetRef(state, "data", data)) fixedCount++;

            NavigationState navigation = context.GetComponent<NavigationState>();
            if (navigation != null) EnsureAssetRef(state, "navigationState", navigation);

            SerializedObject so = new SerializedObject(context);
            SerializedProperty property = so.FindProperty("uniqueRerollState");
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = state;
                so.ApplyModifiedProperties();
                fixedCount++;
            }
        }

        return fixedCount;
    }

    static bool EnsureAssetRef(Component owner, string field, Object asset)
    {
        if (owner == null || asset == null) return false;

        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return false;
        if (property.objectReferenceValue != null) return false;

        property.objectReferenceValue = asset;
        so.ApplyModifiedProperties();
        return true;
    }

    // PersistentSave는 GoldWallet 등과 달리 PlayerContext를 되짚어 참조하지 않고 자기 own
    // playerId를 들고 있다(11번, 영속 저장 — 파일 키로 쓴다) — EnsurePart<T>가 채우는 건
    // PlayerContext 쪽 참조 필드뿐이라 이 값은 따로 맞춰줘야 한다.
    static bool SetPersistentSavePlayerId(PersistentSave save, int playerId)
    {
        SerializedObject so = new SerializedObject(save);
        SerializedProperty property = so.FindProperty("playerId");
        if (property == null || property.intValue == playerId) return false;

        property.intValue = playerId;
        so.ApplyModifiedProperties();
        return true;
    }

    // WaveSpawner가 레인 목록을 받도록 바뀌면 여기서 채운다.
    // 아직 단일 path만 있는 버전이면 조용히 넘어간다.
    static string WireLanePaths(List<WaypointPath> lanePaths)
    {
        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>(FindObjectsInactive.Include);
        if (spawner == null) return "";

        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty list = so.FindProperty("lanePaths");
        if (list == null) return "";

        list.ClearArray();
        for (int i = 0; i < lanePaths.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = lanePaths[i];
        }
        so.ApplyModifiedProperties();

        return $"\n레인 경로 {lanePaths.Count}개를 WaveSpawner에 연결했습니다.";
    }

    // 맵이 넓어서(MapLayout.SeaSize) 원점 근처 낮은 위치에서는 아무것도 안 보인다.
    static string SetUpCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return "";

        RtsCameraController controller = camera.GetComponent<RtsCameraController>();
        if (controller == null)
            controller = camera.gameObject.AddComponent<RtsCameraController>();

        // 스크립트의 기본값을 바꿔도 이미 씬에 저장된 값은 그대로 남는다.
        // 맵 생성은 초기화 동작이므로 조작 관련 수치를 현재 기준값으로 덮어쓴다.
        // 섬 배치가 바뀔 때마다 손으로 맞추면 어긋난다. 실제 범위를 재서 넣는다.
        IslandBounds bounds = MeasureIslands();

        SerializedObject cameraSo = new SerializedObject(controller);
        cameraSo.FindProperty("boundsMin").vector2Value =
            new Vector2(bounds.minX - CameraMargin, bounds.minZ - CameraMargin);
        cameraSo.FindProperty("boundsMax").vector2Value =
            new Vector2(bounds.maxX + CameraMargin, bounds.maxZ + CameraMargin);
        // moveSpeed·maxHeight는 세계 좌표 단위라 맵이 커진 만큼 같이 커져야 한다(원작 비율
        // 2단계, PM 지시 2026-09-23 "카메라 maxHeight·bounds도 같은 배율로"). edgeThickness는
        // 화면 픽셀 단위라 맵 배율과 무관해서 그대로 둔다. minHeight는 유닛 크기(그대로,
        // "유닛 기준 키 20이 기준자")에 맞춘 근접 줌이라 역시 안 건드린다.
        cameraSo.FindProperty("moveSpeed").floatValue = 110f * MapLayout.Scale;
        cameraSo.FindProperty("edgeThickness").floatValue = 16f;
        cameraSo.FindProperty("minHeight").floatValue = 20f;
        cameraSo.FindProperty("maxHeight").floatValue = 420f * MapLayout.Scale;
        cameraSo.ApplyModifiedProperties();

        // 시작 시점은 내 레인(1번) 하나가 화면에 차는 정도. 전체 조망은 휠로 빼면 된다.
        //
        // 🔴 **여기서 잡는 위치·높이는 런타임에 덮어써진다.**
        //    `RtsCameraController.Start()` → `FocusOnLocalLane()`이 LaneMarker를 찾아 높이와
        //    위치를 **다시 계산**한다. 2026-09-23에 편집 시점 값(높이 216.7·z 1432.4)으로
        //    「우리가 카메라 뒤에 있다」를 계산해 고치려 했는데, 실제 플레이 중 값은
        //    **높이 425.5·z 1224.9**로 전혀 달랐다. 계산은 둘 다 맞았고 **재는 대상이 틀렸다.**
        //    시작 화면 구도를 고치려면 여기가 아니라 `RtsCameraController`를 봐야 한다.
        //    (여기 값은 플레이 전 씬을 에디터에서 볼 때의 구도로만 남는다.)
        //
        // ⚠️ 그리고 유니티 FOV는 **세로 기준**이다 — 세로로 긴 창에서는 가로 시야가 줄고,
        //    **높이를 올려도 가로는 그대로**다. 16:9(가로 시야 886 > 필드 폭 781)에서는 들어가지만
        //    9:16이면 280뿐이라 필드의 36%만 보인다. 세로 창을 받쳐야 하면 높이를 올릴 게 아니라
        //    FOV를 가로 기준으로 잡아야 한다(`RtsCameraController`).
        //
        // height도 세계 좌표라 같이 커진다 — 안 키우면 레인이 781×651로 커진 뒤 시작 화면에
        // 구석 일부만 잡힌다.
        MapLayout.Island lane = MapLayout.Lanes[0];
        const float pitch = 50f;
        float height = 52f * MapLayout.Scale;
        float backOff = height / Mathf.Tan(pitch * Mathf.Deg2Rad);
        camera.transform.position = new Vector3(lane.center.x, height, lane.center.y - backOff);
        camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        // farClipPlane도 세계 좌표 거리라 같이 늘려야 한다 — 안 그러면 맵 먼 쪽이 잘려 보인다.
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1000f * MapLayout.Scale);

        // 경계를 숫자로 찍는다(PM 요청 2026-09-24). 섬을 옮길 때마다 "따라올 것이다"까지만
        // 알고 넘어가면, 안 따라온 날에는 **사장님이 그 섬까지 화면을 못 움직이는 것**으로만
        // 드러난다. 섬 무리의 실제 끝과 나란히 찍어야 "여유가 CameraMargin뿐인지"가 보인다.
        Vector2 min = cameraSo.FindProperty("boundsMin").vector2Value;
        Vector2 max = cameraSo.FindProperty("boundsMax").vector2Value;
        return "\n카메라에 RTS 조작(가장자리 밀기·WASD·휠 확대)을 붙이고 메인 필드 위로 옮겼습니다." +
               $"\n  경계 X {min.x:0.0}~{max.x:0.0} · Z {min.y:0.0}~{max.y:0.0}" +
               $" (섬 무리 X {bounds.minX:0.0}~{bounds.maxX:0.0} · Z {bounds.minZ:0.0}~{bounds.maxZ:0.0}" +
               $", 여유 {CameraMargin:0.0})";
    }

    // 섬 끝을 화면 가운데 두고도 주변이 보이도록. 세계 좌표 단위라 맵 배율과 같이 커진다.
    const float CameraMargin = 60f * MapLayout.Scale;

    struct IslandBounds
    {
        public float minX, maxX, minZ, maxZ;
        public Vector3 Center => new Vector3((minX + maxX) * 0.5f, MapLayout.IslandTop, (minZ + maxZ) * 0.5f);
        public float Extent => Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f;
    }

    static IslandBounds MeasureIslands()
    {
        IslandBounds bounds = new IslandBounds
        {
            minX = float.MaxValue, maxX = float.MinValue,
            minZ = float.MaxValue, maxZ = float.MinValue,
        };

        foreach (MapLayout.Island island in AllIslands())
        {
            bounds.minX = Mathf.Min(bounds.minX, island.center.x - island.size.x * 0.5f);
            bounds.maxX = Mathf.Max(bounds.maxX, island.center.x + island.size.x * 0.5f);
            bounds.minZ = Mathf.Min(bounds.minZ, island.center.y - island.size.y * 0.5f);
            bounds.maxZ = Mathf.Max(bounds.maxZ, island.center.y + island.size.y * 0.5f);
        }

        return bounds;
    }

    static IEnumerable<MapLayout.Island> AllIslands()
    {
        foreach (MapLayout.Island island in MapLayout.Lanes) yield return island;
        foreach (MapLayout.Island island in MapLayout.Warehouses) yield return island;
        foreach (MapLayout.Island island in MapLayout.SealIslands) yield return island;
        foreach (MapLayout.Island island in MapLayout.Zones) yield return island;
    }

    // 유닛·위습이 실제로 서게 될 자리에 길이 깔렸는지 확인한다.
    // 길이 없으면 에이전트가 NavMesh에 안 붙고, 그 유닛은 선택은 되는데 명령을 조용히 무시한다 —
    // 화면에는 "클릭이 안 먹는다"로만 보여서 원인을 찾는 데 오래 걸린다.
    static string CheckNavMeshCoverage()
    {
        List<(string name, Vector3 at)> points = new List<(string, Vector3)>();

        foreach (WispCell cell in Object.FindObjectsByType<WispCell>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            points.Add((cell.name, cell.transform.position));

        for (int i = 0; i < MapLayout.Lanes.Length; i++)
            points.Add(($"{MapLayout.Lanes[i].name} 유닛우리",
                        MapLayout.LaneUnitPenRow(MapLayout.Lanes[i]).Center3));

        // 스토리존 도착 지점 — 지금까지 아무도 서본 적 없는 자리라 안 구워졌을 수 있다.
        for (int i = 0; i < MapLayout.Lanes.Length; i++)
            points.Add(($"{MapLayout.Lanes[i].name} 스토리존 도착지점", StoryZoneLandingPoint(i)));

        // 레인 중심 — StoryReturnPortal의 복귀 목적지다. 안 구워지면 UnitCombat.SnapTo가
        // 조용히 실패해서 "복귀 포탈을 탔는데 안 움직인다"가 원인 불명 버그로 남는다
        // (PM 지시, 2026-09-05).
        for (int i = 0; i < MapLayout.Lanes.Length; i++)
            points.Add(($"{MapLayout.Lanes[i].name} 중심(스토리 복귀지점)",
                        new Vector3(MapLayout.Lanes[i].center.x, MapLayout.IslandTop, MapLayout.Lanes[i].center.y)));

        List<string> missing = new List<string>();

        foreach ((string name, Vector3 at) in points)
        {
            if (NavMesh.SamplePosition(at, out _, 6f, NavMesh.AllAreas)) continue;

            missing.Add(name);

            // 왜 없는지까지 남긴다. 대개 그 자리를 덮은 콜라이더가 길을 깎아낸 것이다.
            string blockers = DescribeBlockers(at, 6f);
            Debug.LogWarning($"[맵] '{name}' {at} 에 길이 없습니다. 여기 생기는 유닛은 안 움직입니다.\n" +
                             $"     그 자리를 덮은 콜라이더: {blockers}");
        }

        string report = missing.Count == 0 ? "" :
            $"\n  ⚠️ 길이 안 깔린 자리 {missing.Count}곳 — 콘솔에 원인을 적었습니다:\n     " +
            string.Join(", ", missing);

        return report + CheckUnitPenSlotCoverage();
    }

    // 칸막이(BuildUnitPenPartitions)를 세운 뒤에는 우리 안 자리 하나하나에 길이 남아있는지
    // 따로 세서 보고한다 — 칸막이가 촘촘하면 자리는 있어도 그 위에 길이 안 깔릴 수 있다.
    // 위 CheckNavMeshCoverage는 우리 한가운데 한 점만 보므로 이 문제를 못 잡는다.
    static string CheckUnitPenSlotCoverage()
    {
        System.Text.StringBuilder lines = new System.Text.StringBuilder();
        int totalSlots = 0, totalCovered = 0;
        bool anyMissing = false;

        for (int i = 0; i < MapLayout.Lanes.Length; i++)
        {
            LaneMarker marker = LaneMarker.Get(i);
            if (marker == null) continue;

            int slotCount = 0, covered = 0;
            foreach (Vector3 at in marker.FirstRowSlotPositions())
            {
                slotCount++;
                if (NavMesh.SamplePosition(at, out _, 6f, NavMesh.AllAreas))
                {
                    covered++;
                    continue;
                }

                anyMissing = true;
                string blockers = DescribeBlockers(at, 6f);
                Debug.LogWarning($"[맵] '{MapLayout.Lanes[i].name} 유닛 자리' {at} 에 길이 없습니다 " +
                                 $"(칸막이 간격을 넓히거나 얇게 해야 합니다).\n     그 자리를 덮은 콜라이더: {blockers}");
            }

            totalSlots += slotCount;
            totalCovered += covered;
            lines.Append($"\n     {MapLayout.Lanes[i].name} {covered}/{slotCount}");
        }

        if (totalSlots == 0) return "";

        string header = anyMissing
            ? $"\n  ⚠️ 유닛 우리 자리 NavMesh: 합계 {totalCovered}/{totalSlots} (콘솔에 원인을 적었습니다)"
            : $"\n  유닛 우리 자리 NavMesh: 합계 {totalCovered}/{totalSlots} (전부 정상)";

        return header + lines;
    }

    // 그 지점을 감싸는 콜라이더를 훑는다. 트리거인지도 같이 적는다 —
    // NavMesh를 PhysicsColliders로 구우면 트리거도 장애물로 잡혀서 길을 통째로 지우는 수가 있다.
    static string DescribeBlockers(Vector3 at, float radius)
    {
        Collider[] found = Physics.OverlapSphere(at, radius);
        if (found.Length == 0) return "없음 — 바닥 콜라이더 자체가 없다는 뜻입니다.";

        List<string> names = new List<string>();
        foreach (Collider collider in found)
            names.Add($"{collider.name}{(collider.isTrigger ? "(트리거)" : "")}");

        return string.Join(", ", names);
    }

    // 지금 씬의 NavMesh가 어떤 상태인지 한 번에 본다. "구웠는데 왜 안 움직이지"를
    // 추측으로 좁히지 않으려고 둔다 — 표면이 있는지, 데이터가 있는지, 파일로 남았는지가 다 다르다.
    [MenuItem("Tools/맵/NavMesh 상태 확인")]
    public static void InspectNavMesh()
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        report.AppendLine($"NavMeshSurface {surfaces.Length}개");

        foreach (NavMeshSurface surface in surfaces)
        {
            NavMeshData data = surface.navMeshData;
            string assetPath = data != null ? AssetDatabase.GetAssetPath(data) : "";

            report.AppendLine($"  · {surface.gameObject.name}");
            report.AppendLine($"      데이터: {(data == null ? "❌ 없음 (안 구워졌거나 저장 안 됨)" : "있음")}");
            if (data != null)
                report.AppendLine($"      파일: {(string.IsNullOrEmpty(assetPath) ? "❌ 메모리에만 — 씬을 저장하면 사라집니다" : assetPath)}");
        }

        // 실제로 걸을 수 있는 면이 있는지. 위 둘이 다 멀쩡해도 이게 비면 아무도 안 움직인다.
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        report.AppendLine();
        report.AppendLine($"지금 로드된 NavMesh 삼각형 {mesh.indices.Length / 3}개");

        if (mesh.indices.Length == 0)
            report.AppendLine("  ❌ 걸을 수 있는 면이 하나도 없습니다 — 유닛이 전부 안 움직입니다.");
        else
            report.Append(CheckNavMeshCoverage());

        string text = report.ToString();
        Debug.Log("[맵] " + text);
        EditorGuards.Dialog(Title, text, "확인");
    }

    /// <summary>
    /// 이번 생성에 **실제로 쓰인** 기준값 한 줄. 보고문 맨 앞에 붙인다.
    ///
    /// 🔴 왜 있나: 유니티는 컴파일이 안 끝났거나 실패하면 **마지막으로 성공한 어셈블리로**
    ///    메뉴를 돌린다. 그러면 고친 값이 아니라 **옛 값으로 맵이 구워지는데 아무 경고가 없다.**
    ///    2026-09-23 하루에만 두 번 그랬고(조합판·섬 높이), 두 번 다 **사후에 좌표를 재고 나서야**
    ///    알았다. 그 전까지는 "내 수정이 틀렸나"를 의심하며 시간을 쓴다.
    ///    이 줄이 있으면 **대화상자를 닫기도 전에** 옛 코드임이 드러난다.
    ///
    /// ⚠️ 값을 문자열로 적지 말 것 — 상수를 직접 읽어야 이 줄이 같이 낡지 않는다.
    /// </summary>
    static string BaselineReport()
    {
        // 바다 상자는 윗면이 늘 y=0이므로(BuildSea) 높이 차 = IslandTop이다.
        float gap = MapLayout.IslandTop;
        float cells = gap / MapLayout.NavMeshVoxelSize;
        // 복셀 두 칸 이하로 붙으면 Recast가 섬 윗면과 바다 윗면을 한 층으로 합치고 영역이
        // Sea로 덮인다 — 지상 유닛이 섬에 못 들어간다(MapLayout.NavMeshVoxelSize 주석).
        string flag = cells > 2f ? "" : "  🔴 복셀 두 칸 이하 — 섬 윗면이 바다로 구워진다";
        return $"[기준값] Scale {MapLayout.Scale:0.###} · IslandTop {MapLayout.IslandTop:0.##} · " +
               $"복셀 {MapLayout.NavMeshVoxelSize:0.##} · 섬윗면−바다윗면 {gap:0.##}(복셀 {cells:0.#}칸){flag}\n";
    }

    static string BuildNavMesh(GameObject root)
    {
        // 있으면 그걸 쓴다. AddComponent를 매번 부르면 표면이 여러 장 쌓이고,
        // 그중 빈 것이 섞이면 어느 쪽이 쓰이는지 알 수 없게 된다.
        NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = root.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // 바다가 MapLayout.SeaSize(원작 비율 2단계 기준 6667×6667, PM 지시 2026-09-23)라 굽는
        // 범위가 그만큼 넓다. 넓이가 Scale² ≈ 17.4배가 됐는데 복셀 크기(0.5)를 그대로 두면
        // 칸 수가 같이 17배가 돼서 굽기가 무거워진다. 0.5→2.0으로 올려 칸 수 증가를 상쇄한다
        // (유닛 반지름 0.28 기준으로도 이 정도 거칠기면 통행에는 지장이 없다).
        surface.overrideVoxelSize = true;
        surface.voxelSize = MapLayout.NavMeshVoxelSize;
        surface.overrideTileSize = true;
        surface.tileSize = 256;

        try
        {
            surface.BuildNavMesh();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[맵] NavMesh 굽기 실패: {e.Message}");
            return "⚠️ NavMesh 굽기에 실패했습니다. Map 오브젝트의 NavMeshSurface에서 Bake를 눌러주세요.";
        }

        return SaveNavMeshAsset(surface) + CheckNavMeshCoverage();
    }

    // 스크립트로 BuildNavMesh()를 부르면 결과가 메모리에만 남는다 — 인스펙터의 Bake 버튼은
    // 그걸 에셋으로 저장까지 해주지만 스크립트 호출은 안 한다. 저장을 안 하면 씬을 저장하거나
    // 다시 컴파일하는 순간 통째로 사라지고, 씬에는 m_NavMeshData: {fileID: 0}만 남는다.
    // 그 상태에서 유닛을 스폰하면 "no valid NavMesh"가 뜨고 아무도 안 움직인다.
    static string SaveNavMeshAsset(NavMeshSurface surface)
    {
        NavMeshData data = surface.navMeshData;
        if (data == null)
            return "⚠️ NavMesh가 비어 있습니다 — 걸을 수 있는 바닥을 못 찾았습니다.";

        // 씬과 같은 이름의 폴더에 둔다. 유니티가 씬별 굽기 결과를 두는 자리와 같다.
        string scenePath = surface.gameObject.scene.path;
        if (string.IsNullOrEmpty(scenePath))
            return "\n⚠️ 씬이 저장된 적 없어 NavMesh를 파일로 남기지 못했습니다.";

        string folder = System.IO.Path.ChangeExtension(scenePath, null);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/'),
                                       System.IO.Path.GetFileNameWithoutExtension(scenePath));
        }

        string assetPath = $"{folder}/NavMesh-{surface.gameObject.name}.asset";

        if (AssetDatabase.GetAssetPath(data) != assetPath)
        {
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();

        // 씬이 이 에셋을 가리키게 다시 물려준다.
        SerializedObject so = new SerializedObject(surface);
        so.FindProperty("m_NavMeshData").objectReferenceValue = data;
        so.ApplyModifiedProperties();

        return $"NavMesh를 구워 {assetPath} 에 저장했습니다 (바다 = Sea 영역).";
    }

    static string DisableOldGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null) return "";

        ground.SetActive(false);
        return "\n기존 Ground는 새 맵과 겹쳐서 비활성화했습니다.";
    }

    /// <summary>
    /// 타일 반복 횟수는 면 크기에 비례해야 하지만, **맵 배율에까지 비례하면 안 된다.**
    ///
    /// 🔴 2026-09-24에 난 사고: `tilesPerUnit`은 「세계 단위당 타일 수」라 절대값이다.
    ///    맵이 4.167배 커지자 같은 바닥의 반복 횟수도 4.167배가 됐다 —
    ///    상점 바닥이 가로 19회에서 **81회**, 우리 바닥이 25회에서 **103회**.
    ///    화면 가로로 그렇게 촘촘해지면 밉맵·이방성 필터가 못 따라가서
    ///    바닥이 **희끄무레한 세로 줄무늬 카펫**이 된다(사장님 「하얀 가시밭」).
    ///
    /// 증거: 같은 `dirt` 재질인데 흙길(가로 반복 3.2회)은 멀쩡한 갈색이고,
    ///       우리 바닥(가로 반복 103회)만 깨졌다. 재질도 텍스처도 같고 **반복 횟수만** 달랐다.
    ///
    /// Scale로 나누면 타일 하나의 세계 크기가 맵과 함께 커져서 반복 횟수가 배율에
    /// 상관없이 일정해진다. 표의 값은 「맵 배율 1일 때」의 뜻 그대로 남는다.
    /// </summary>
    static float TilesPerUnit(Surface surface)
    {
        return surface.tilesPerUnit / MapLayout.Scale;
    }

    /// <summary>
    /// 면 하나를 칠한다. 타일링은 **크기별 재질 에셋**에 담는다.
    ///
    /// 🔴 2026-09-24에 하루를 잡아먹은 것: 예전에는 렌더러별 `MaterialPropertyBlock`에
    ///    `_BaseMap_ST`를 넣었다. 배칭을 지키려던 것인데, **프로퍼티 블록은 씬에 저장되지 않는다.**
    ///    씬을 다시 열거나 스크립트를 고쳐 도메인 리로드가 한 번 돌면 통째로 날아가고,
    ///    모든 면이 재질의 기본값 (1,1)로 돌아간다 — 256×256 한 장이 570×77 바닥에 늘어난다.
    ///    가로 텍셀 2.2 · 세로 텍셀 0.3의 **7:1 비등방**이 되어 화면에는 희끄무레한 세로
    ///    줄무늬 카펫으로 보였고(사장님 「하얀 가시밭」), 밉이 가로를 뭉개 **rock과 dirt가
    ///    같은 색**으로 나왔다. 실행 중 사진은 **항상** 그 상태였다 — 플레이 모드가 씬을
    ///    디스크에서 다시 읽기 때문이다.
    ///
    /// 그래서 타일 횟수마다 `.mat`을 하나씩 둔다. 정수로 반올림하는 것은 개수를 줄이려는 것이기도
    /// 하고, 타일 경계가 면 가장자리에 맞아 이음매가 덜 보이기 때문이기도 하다.
    /// </summary>
    static void Paint(GameObject obj, string key, float sizeX = 1f, float sizeZ = 1f)
    {
        if (!Surfaces.TryGetValue(key, out Surface surface)) return;

        Renderer renderer = obj.GetComponent<Renderer>();

        if (surface.texture == null || surface.tilesPerUnit <= 0f)
        {
            renderer.sharedMaterial = GetOrCreateMaterial(key, surface);
            return;
        }

        int tilesX = Mathf.Max(1, Mathf.RoundToInt(sizeX * TilesPerUnit(surface)));
        int tilesZ = Mathf.Max(1, Mathf.RoundToInt(sizeZ * TilesPerUnit(surface)));
        renderer.sharedMaterial = GetOrCreateTiledMaterial(key, surface, tilesX, tilesZ);
    }

    // 한 번 생성하는 동안 Paint가 수천 번 불린다. AssetDatabase를 그때마다 두드리면 느려서
    // 이 판에서 만든 것을 들고 있는다. 생성 시작마다 비운다.
    static readonly Dictionary<string, Material> TiledCache = new Dictionary<string, Material>();
    static readonly HashSet<string> TiledUsed = new HashSet<string>();

    static Material GetOrCreateTiledMaterial(string key, Surface surface, int tilesX, int tilesZ)
    {
        string path = $"{MaterialFolder}/{key}_{tilesX}x{tilesZ}.mat";
        TiledUsed.Add(path);
        if (TiledCache.TryGetValue(path, out Material cached) && cached != null) return cached;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            // 기본 재질에서 복제한다 — 색·매끄러움을 사람이 맞춰 둔 것이 있으면 그대로 따라온다.
            material = new Material(GetOrCreateMaterial(key, surface));
            AssetDatabase.CreateAsset(material, path);
        }

        BindTextures(material, surface);
        Vector2 scale = new Vector2(tilesX, tilesZ);
        if (material.GetTextureScale("_BaseMap") != scale)
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureScale("_BumpMap", scale);
            EditorUtility.SetDirty(material);
        }

        TiledCache[path] = material;
        return material;
    }

    /// <summary>
    /// 이번 판에 안 쓰인 크기별 재질을 지운다. 안 지우면 맵 크기를 바꿀 때마다
    /// `lane_19x7.mat` 같은 것이 쌓여서 어느 게 살아 있는지 알 수 없게 된다.
    /// 이름이 `<키>_<가로>x<세로>.mat` 꼴인 것만 건드린다 — 손으로 만든 재질은 안 지운다.
    /// </summary>
    static string SweepTiledMaterials()
    {
        int removed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^.+_\d+x\d+$")) continue;
            if (TiledUsed.Contains(path)) continue;
            AssetDatabase.DeleteAsset(path);
            removed++;
        }
        return $" · 크기별 재질 {TiledUsed.Count}종{(removed > 0 ? $"(낡은 {removed}종 지움)" : "")}";
    }

    static Material GetOrCreateMaterial(string key, Surface surface)
    {
        string path = $"{MaterialFolder}/{key}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            BindTextures(existing, surface);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Map");
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.SetColor("_BaseColor", surface.tint);
        material.SetFloat("_Smoothness", surface.smoothness);

        BindTextures(material, surface);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>
    /// 표가 가리키는 텍스처를 재질에 물린다. **이미 있는 재질에도 매번 다시 물린다.**
    ///
    /// 🔴 왜 매번인가: 예전에는 `.mat`이 있으면 그대로 돌려주고 끝이었다. 그래서
    ///    `grass.png`가 프로젝트에 들어오기 **전에** 만들어진 `lane.mat`이 `_BaseMap`이 빈 채로
    ///    영원히 남았고, 레인과 모든 섬 윗면이 **단색**으로 굴렀다. 사장님이 구해 주신 잔디
    ///    텍스처가 화면에 한 번도 안 나온 것이다(2026-09-24에 발견). 갱신을 안 하면
    ///    **다음에 텍스처를 넣어도 똑같이 조용히 안 붙는다.**
    ///
    /// ⚠️ 색·매끄러움은 **텍스처가 있는 면에서만** 맞춘다. 기준은 「사람이 눈으로 고를 값인가」다:
    ///
    ///  · **텍스처가 있으면** 표의 색은 색이 아니라 **텍스처에 곱하는 색조**다. 그래서 표가
    ///    흰색 쪽인 것이고, 그건 「텍스처가 제 색을 내게 비켜 준다」는 뜻이다. 거기에 옛 단색
    ///    팔레트(0.55/0.60/0.44)가 남아 있으면 **텍스처 색 × 어두운 색조**로 두 번 어두워진다 —
    ///    취향이 아니라 같은 값을 두 번 먹인 것이다. 코드 것이므로 맞춘다.
    ///  · **텍스처가 없으면** 그 단색이 곧 그 물건의 색이다. 사람이 눈으로 맞춘 값일 수 있으니
    ///    **안 건드린다.** 표와 달라도 보고문에만 올린다(<see cref="SurfaceDriftReport"/>).
    ///
    /// 🔴 왜 이 구분이 필요했나: 09-24에 재질 열 장이 표와 다른 채로 발견됐다. 전부 매끄러움이
    ///    URP 기본값 0.50이었다 — 표가 그 열 장을 **한 번도 만든 적이 없다**는 뜻이다. 표를
    ///    「텍스처 + 옅은 색조」로 바꿨을 때 다시 만들어진 건 rock·dirt 둘뿐이었다.
    /// </summary>
    static void BindTextures(Material material, Surface surface)
    {
        if (surface.texture == null) return;

        Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"{TextureFolder}/{surface.texture}.png");
        if (baseMap != null && material.GetTexture("_BaseMap") != baseMap)
        {
            material.SetTexture("_BaseMap", baseMap);
            EditorUtility.SetDirty(material);
        }

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"{TextureFolder}/{surface.texture}_normal.png");
        if (normal != null && material.GetTexture("_BumpMap") != normal)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");   // 켜지 않으면 노멀맵이 무시된다
            EditorUtility.SetDirty(material);
        }

        if (Vector4.Distance(material.GetColor("_BaseColor"), surface.tint) > 0.01f)
        {
            material.SetColor("_BaseColor", surface.tint);
            EditorUtility.SetDirty(material);
        }

        if (Mathf.Abs(material.GetFloat("_Smoothness") - surface.smoothness) > 0.005f)
        {
            material.SetFloat("_Smoothness", surface.smoothness);
            EditorUtility.SetDirty(material);
        }
    }

    /// <summary>
    /// 바닥 텍스처가 **실제로 붙었는지** 보고문 한 줄. 텍스처는 안 붙어도 아무 에러가 안 나고
    /// 그냥 단색으로 굴러서, 이 줄이 없으면 또 몇 달을 모른 채 지나간다(위 주석 참고).
    /// 같은 텍스처를 여러 재질이 나눠 쓰므로 "붙은 재질 수/전체"로 센다 — 하나만 빠져도 드러난다.
    /// </summary>
    /// <summary>
    /// 표의 색·매끄러움과 실제 `.mat`이 **다르면 한 줄 찍는다.** 덮지는 않는다 — 그쪽은 사람이
    /// 인스펙터에서 눈으로 맞추는 값이라 덮으면 맞춰 놓은 게 소리 없이 사라진다.
    ///
    /// 🔴 그러나 **말없이 안 먹는 것**이 09-24에 우리를 세 번 죽인 병이다. 덮지 않되
    ///    숨기지도 않는다 — 표를 고쳤는데 화면이 안 바뀌면 이 줄이 이유를 바로 말해 준다.
    ///    ⚠️ 어느 쪽이 맞는 값인지는 이 코드가 정하지 않는다. 사람이 보고 정한다.
    ///
    /// ⚠️ **텍스처가 있는 면은 여기 안 뜬다** — 그쪽은 색조가 코드 것이라 <see cref="BindTextures"/>가
    ///    이미 맞췄다. 여기 뜨는 것은 **텍스처 없는 단색 면**뿐이고, 그건 사람이 맞춘 값일 수 있다.
    /// </summary>
    /// <summary>
    /// 위습 생성 자리가 포탈 트리거에 **닿는지** 검사한다. 닿으면 위습이 생기자마자 먹혀서
    /// 플레이어가 고를 기회조차 없다 — 2026-09-24에 흔함 선택 위습이 10턴 중 9턴 0기였다.
    ///
    /// 🔴 왜 검사가 필요한가: 이건 **두 곳을 따로 만지면 조용히 생긴다.** 위습 크기를 키우거나
    ///    포탈 지름을 늘리거나 뽑기섬 배치를 옮기면 그때마다 다시 난다. 그리고 **아무 에러도
    ///    안 난다** — 위습이 사라지는 것뿐이라 「원래 그런가 보다」로 지나간다.
    ///    실제로 포탈 트리거 높이가 모자라던 동안에는 이 결함이 **가려져 있었다**(4c8ab5ff 참고).
    /// </summary>
    static string WispCellClearanceReport()
    {
        List<string> hits = new List<string>();
        WispCell[] cells = Object.FindObjectsByType<WispCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        UnitPortal[] portals = Object.FindObjectsByType<UnitPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (WispCell cell in cells)
        {
            float closest = float.MaxValue, need = 0f;
            string who = "";
            foreach (UnitPortal portal in portals)
            {
                Vector3 a = cell.transform.position, b = portal.transform.position;
                float flat = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                if (flat >= closest) continue;
                closest = flat;
                who = portal.gameObject.name;
                // 포탈 원반의 **실제** 반지름은 그 오브젝트의 가로 배율 절반이다 — 종류마다 지름이 다르다.
                need = portal.transform.localScale.x * 0.5f + WispColliderRadius + WispCellMargin;
            }
            if (portals.Length == 0 || closest >= need) continue;
            hits.Add($"\n    🔴 {cell.gameObject.name} ↔ {who} {closest:0.#} < 필요 {need:0.#}");
        }
        return hits.Count == 0
            ? $"\n위습 자리: {cells.Length}칸 전부 포탈과 떨어져 있습니다(위습 반지름 {WispColliderRadius:0.#})."
            : $"\n⚠️ 위습이 생기자마자 포탈에 먹히는 자리 {hits.Count}곳 — **플레이어가 고를 기회가 없습니다.**" +
              $"(필요 = 포탈 반지름 + 위습 반지름 {WispColliderRadius:0.#} + 여유 {WispCellMargin:0.#})" +
              string.Join("", hits);
    }

    static string SurfaceDriftReport()
    {
        List<string> drift = new List<string>();
        foreach (KeyValuePair<string, Surface> entry in Surfaces)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{entry.Key}.mat");
            if (material == null) continue;

            List<string> parts = new List<string>();
            Color want = entry.Value.tint, have = material.GetColor("_BaseColor");
            if (Vector4.Distance(want, have) > 0.01f)
                parts.Add($"색 {have.r:0.##}/{have.g:0.##}/{have.b:0.##} ≠ 표 {want.r:0.##}/{want.g:0.##}/{want.b:0.##}");

            float wantS = entry.Value.smoothness, haveS = material.GetFloat("_Smoothness");
            if (Mathf.Abs(wantS - haveS) > 0.005f)
                parts.Add($"매끄러움 {haveS:0.##} ≠ 표 {wantS:0.##}");

            if (parts.Count > 0) drift.Add($"\n    {entry.Key}.mat  {string.Join(" · ", parts)}");
        }
        return drift.Count == 0 ? ""
            : $"\n⚠️ 표와 다른 **기본 재질** {drift.Count}장 — 덮지 않습니다." +
              string.Join("", drift) +
              "\n    ⚠️ 이건 **화면이 틀렸다는 뜻이 아닙니다.** 텍스처가 있는 면은 크기별 변종이 그려지고" +
              "\n       그쪽은 표에 맞춰져 있습니다. 여기 뜨는 것은 텍스처 없는 단색 면뿐이고," +
              "\n       그 단색은 사람이 눈으로 맞춘 값일 수 있어 코드가 안 정합니다.";
    }

    static string SurfaceTextureReport()
    {
        Dictionary<string, int> total = new Dictionary<string, int>();
        Dictionary<string, int> bound = new Dictionary<string, int>();
        List<string> order = new List<string>();

        foreach (KeyValuePair<string, Surface> entry in Surfaces)
        {
            string texture = entry.Value.texture;
            if (texture == null) continue;              // 색만 쓰는 면(포탈)은 셀 것이 없다
            if (!total.ContainsKey(texture)) { total[texture] = 0; bound[texture] = 0; order.Add(texture); }
            total[texture]++;

            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{entry.Key}.mat");
            if (material != null && material.GetTexture("_BaseMap") != null) bound[texture]++;
        }

        List<string> parts = new List<string>();
        foreach (string texture in order)
            parts.Add($"{texture} {bound[texture]}/{total[texture]}{(bound[texture] == total[texture] ? " ✅" : " 🔴")}");
        return "\n바닥 텍스처: " + string.Join(" · ", parts);
    }
}
