using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
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
    const float CliffHeight = 1.6f;       // 바위 치마 높이 (바다 아래까지 내려간다)
    const float CliffOverhang = 2.4f;     // 잔디보다 얼마나 넓게 나올지

    // 구역을 색으로만 구분하면 원랜디 느낌이 안 난다. 잔디/물/바위 텍스처를 깔고
    // 구역 구분은 잔디에 옅은 색조를 얹는 정도로만 한다.
    struct Surface
    {
        public string texture;      // Assets/Textures/Map/<이름>.png
        public Color tint;
        public float tilesPerUnit;  // 섬 크기에 비례해 반복시켜 늘어나 보이지 않게 한다
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
        { "sea",       new Surface("water", new Color(0.72f, 0.86f, 1.00f), 0.020f, 0.85f) },
        { "rock",      new Surface("rock",  Color.white,                    0.140f, 0.10f) },
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

    [MenuItem("Tools/맵/원랜디 맵 생성")]
    static void Generate()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        GameObject existing = GameObject.Find(RootName);
        if (existing != null &&
            !EditorUtility.DisplayDialog(Title,
                "이미 Map이 있습니다. 지우고 다시 만들까요?\n(Map 아래 직접 수정한 것은 사라집니다)",
                "다시 만들기", "취소"))
            return;

        if (existing != null) Object.DestroyImmediate(existing);

        GameObject root = new GameObject(RootName);
        BuildSea(root.transform);

        List<GameObject> laneObjects = new List<GameObject>();
        for (int i = 0; i < MapLayout.Lanes.Length; i++)
        {
            GameObject laneObject = BuildIsland(root.transform, MapLayout.Lanes[i]);
            laneObject.AddComponent<LaneMarker>().SetLaneIndex(i);
            laneObjects.Add(laneObject);
        }

        foreach (MapLayout.Island island in MapLayout.Warehouses) BuildIsland(root.transform, island);
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
        string portalReport = BuildGachaPortals(gachaIsland);

        string overlaps = CheckOverlaps();
        string rewire = RewireScene(lanePaths);
        string navResult = BuildNavMesh(root);
        string oldGround = DisableOldGround();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message =
            $"섬 {MapLayout.Lanes.Length + MapLayout.Warehouses.Length + MapLayout.SealIslands.Length + MapLayout.Zones.Length}개, " +
            $"레인 경로 {lanePaths.Count}개를 만들었습니다." + portalReport + "\n\n" +
            tableReport + displayReport + gateReport + overlaps + navResult + oldGround + rewire + "\n\nCmd+S 로 저장하세요.";
        Debug.Log("[맵] " + message);
        EditorUtility.DisplayDialog(Title, message, "확인");
    }

    static void BuildSea(Transform parent)
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

    // 조합식 표: 등급별 세로 칸에 그 등급 유닛을 늘어놓는다.
    // 원작처럼 표 위에 유닛이 서 있고 플레이어가 그걸 보고 조합을 익히는 구조라,
    // 지금은 스킨 대신 자리 표시 기둥을 세운다. 표가 얼마나 커야 하는지도 이걸로 드러난다.
    const float SlotSpacing = 4.5f;
    const float SlotSize = 2.2f;
    const float SlotHeight = 2.6f;

    static string BuildCombineColumns(GameObject table)
    {
        if (table == null) return "";

        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "CombineTable");
        UnitGrade[] grades = MapLayout.CombineTableGrades;
        float columnWidth = island.size.x / grades.Length;

        int placed = 0;
        float deepestUsed = 0f;

        for (int c = 0; c < grades.Length; c++)
        {
            UnitGrade grade = grades[c];
            List<UnitData> units = LoadUnitsOfGrade(grade);
            float columnCenterX = island.center.x - island.size.x * 0.5f + columnWidth * (c + 0.5f);

            // 칸 바닥 — 등급 구분이 눈에 보이게 옅은 판을 깐다.
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = $"Column_{grade.KoreanName()}";
            strip.transform.SetParent(table.transform.parent, false);
            strip.transform.position = new Vector3(columnCenterX, MapLayout.IslandTop + 0.06f, island.center.y);
            strip.transform.localScale = new Vector3(columnWidth - 1.5f, 0.12f, island.size.y - 2f);
            Paint(strip, "combine", columnWidth, island.size.y);
            Object.DestroyImmediate(strip.GetComponent<Collider>());

            int perRow = Mathf.Max(1, Mathf.FloorToInt((columnWidth - SlotSpacing) / SlotSpacing));
            float startX = columnCenterX - (perRow - 1) * SlotSpacing * 0.5f;
            float startZ = island.center.y + island.size.y * 0.5f - SlotSpacing;

            for (int i = 0; i < units.Count; i++)
            {
                float z = startZ - (i / perRow) * SlotSpacing;

                GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slot.name = $"{grade.KoreanName()}_{units[i].unitName}";
                slot.transform.SetParent(table.transform.parent, false);
                slot.transform.position = new Vector3(
                    startX + (i % perRow) * SlotSpacing, MapLayout.IslandTop + SlotHeight * 0.5f, z);
                slot.transform.localScale = new Vector3(SlotSize, SlotHeight, SlotSize);
                PaintSolid(slot, GradeColor(grade));
                // 표 위를 걸어다녀야 하므로 기둥은 통과시킨다.
                Object.DestroyImmediate(slot.GetComponent<Collider>());

                deepestUsed = Mathf.Max(deepestUsed, startZ - z + SlotSpacing);
                placed++;
            }
        }

        float available = island.size.y;
        string verdict = deepestUsed <= available
            ? $"여유 {available - deepestUsed:F0}"
            : $"⚠️ {deepestUsed - available:F0} 모자람 — CombineTable 세로를 {Mathf.CeilToInt(deepestUsed) + 6}으로 늘리세요";

        return $"\n조합식 표에 {placed}종 배치 (필요 깊이 {deepestUsed:F0} / 확보 {available:F0}, {verdict}).";
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
    static Color GradeColor(UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Legendary: return new Color(0.82f, 0.24f, 0.24f);
            case UnitGrade.Rare:      return new Color(0.60f, 0.36f, 0.78f);
            case UnitGrade.Special:   return new Color(0.88f, 0.78f, 0.28f);
            case UnitGrade.Hidden:    return new Color(0.30f, 0.52f, 0.86f);
            case UnitGrade.Common:
            case UnitGrade.Uncommon:  return new Color(0.36f, 0.70f, 0.40f);
            default:                  return new Color(0.62f, 0.62f, 0.62f);
        }
    }

    static void PaintSolid(GameObject obj, Color color)
    {
        string path = $"{MaterialFolder}/slot_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
        }

        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    // 뽑기 섬. 원작처럼 위쪽에 흔함 유닛을 가로로 늘어놓고, 그 아래에 등급별 랜덤 포탈을 둔다.
    // 흔함 위습은 "선택"이라 원하는 유닛 칸에 넣고, 그 위 등급은 칸 하나에서 등급 내 랜덤이 나온다.
    const float PortalDiameter = 6f;
    const float ChoicePortalDiameter = 4.2f;

    // 흔함 선택 칸은 원작처럼 칸마다 벽을 둘러 부스로 만든다.
    const float BoothDepth = 9f;        // 포탈 앞부터 뒷벽까지
    const float BoothWallHeight = 3.2f;
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
                new Vector3(x, 0f, rowZ + BoothDepth * 0.5f), UnitGrade.Common);
            BuildBooth(parent, unit.unitName, x, rowZ, step);
        }

        // --- 왼쪽 세로줄: 등급 내 랜덤 포탈 ---
        UnitGrade[] randomGrades = MapLayout.GachaRandomGrades;
        float columnZTop = rowZ - 11f;
        float columnZBottom = bottom + 6f;
        float gap = (columnZTop - columnZBottom) / Mathf.Max(1, randomGrades.Length - 1);
        float portalX = left + island.size.x * 0.25f;

        for (int i = 0; i < randomGrades.Length; i++)
        {
            GameObject portal = CreatePortalObject(parent, $"Portal_{randomGrades[i].KoreanName()}",
                new Vector3(portalX, MapLayout.IslandTop + 0.25f, columnZTop - gap * i), PortalDiameter);
            ConfigurePortal(portal, randomGrades[i], null, table, spawner);
        }

        // --- 오른쪽 세로줄: 조합식 없이 캐릭터만 전시하는 등급 ---
        // 이 등급들은 조합식 표에 올리지 않기로 확정돼 있어서, 여기가 유일하게 눈으로 보는 곳이다.
        float displayLeft = island.center.x + 2f;
        float displayWidth = left + island.size.x - 2f - displayLeft;
        int perRow = Mathf.Max(1, Mathf.FloorToInt(displayWidth / SlotSpacing));
        float displayZ = columnZTop;
        int displayed = 0;

        foreach (UnitGrade grade in MapLayout.GachaDisplayGrades)
        {
            List<UnitData> units = LoadUnitsOfGrade(grade);

            for (int i = 0; i < units.Count; i++)
            {
                float x = displayLeft + (i % perRow) * SlotSpacing + SlotSpacing * 0.5f;
                float z = displayZ - (i / perRow) * SlotSpacing;
                PlaceUnitMarker(parent, $"{grade.KoreanName()}_{units[i].unitName}",
                    new Vector3(x, 0f, z), grade);
                displayed++;
            }

            // 다음 등급은 한 줄 띄고 이어서 — 등급 경계가 보이게 한다.
            displayZ -= (Mathf.CeilToInt(units.Count / (float)perRow) + 1) * SlotSpacing;
        }

        float displayDepth = columnZTop - displayZ;
        float available = columnZTop - columnZBottom;
        string fit = displayDepth <= available
            ? $"여유 {available - displayDepth:F0}"
            : $"⚠️ {displayDepth - available:F0} 모자람";

        return $"\n뽑기 섬: 흔함 선택 {commons.Count}칸, 등급 랜덤 {randomGrades.Length}칸, " +
               $"전시 {displayed}종 (깊이 {displayDepth:F0}/{available:F0}, {fit}).";
    }

    // 펑크해저드 한가운데를 가로지르는 정의문. 부수기 전에는 섬이 둘로 나뉜다.
    const float GateWidth = 14f;
    const float GateHeight = 5f;
    const float WallHeight = 4f;
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
            PlaceUnitMarker(parent, $"초월_{units[i].unitName}",
                new Vector3(startX + (i % perRow) * SlotSpacing, 0f, startZ - (i / perRow) * SlotSpacing),
                UnitGrade.Transcendent);
        }

        return units.Count;
    }

    static int BuildImmortalDisplay(Transform parent)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "ImmortalDisplay");
        List<UnitData> units = LoadUnitsOfGrade(UnitGrade.Immortal);

        BuildStoneFloor(parent, "불멸전시_바닥", island);
        BuildBrazier(parent, new Vector3(island.center.x, MapLayout.IslandTop, island.center.y));

        // 화로에서 떨어져 둘러앉는 반지름 — 섬 밖으로 나가지 않는 선에서 가장 넓게 잡는다.
        float radius = Mathf.Min(island.size.x, island.size.y) * 0.5f - SlotSize * 2f;

        for (int i = 0; i < units.Count; i++)
        {
            float angle = i / (float)Mathf.Max(1, units.Count) * Mathf.PI * 2f;
            PlaceUnitMarker(parent, $"불멸_{units[i].unitName}",
                new Vector3(island.center.x + Mathf.Cos(angle) * radius, 0f,
                            island.center.y + Mathf.Sin(angle) * radius),
                UnitGrade.Immortal);
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

    static void PaintGlow(GameObject obj, Color color)
    {
        string path = $"{MaterialFolder}/glow_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * 3f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(material, path);
        }

        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    // 칸마다 좌우 벽과 뒷벽을 세워 원작의 부스 모양을 만든다. 앞쪽(포탈 방향)은 열어 둔다.
    static void BuildBooth(Transform parent, string unitName, float centerX, float portalZ, float boothWidth)
    {
        float backZ = portalZ + BoothDepth;
        float midZ = portalZ + BoothDepth * 0.5f;
        float halfWidth = boothWidth * 0.5f;
        float y = MapLayout.IslandTop + BoothWallHeight * 0.5f;

        BuildWall(parent, $"부스_{unitName}_뒷벽",
            new Vector3(centerX, y, backZ),
            new Vector3(boothWidth, BoothWallHeight, BoothWallThickness));

        BuildWall(parent, $"부스_{unitName}_좌벽",
            new Vector3(centerX - halfWidth, y, midZ),
            new Vector3(BoothWallThickness, BoothWallHeight, BoothDepth));

        BuildWall(parent, $"부스_{unitName}_우벽",
            new Vector3(centerX + halfWidth, y, midZ),
            new Vector3(BoothWallThickness, BoothWallHeight, BoothDepth));
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
    }

    // 조합식 표와 같은 자리 표시 기둥. 나중에 스킨으로 교체한다.
    static void PlaceUnitMarker(Transform parent, string name, Vector3 groundPosition, UnitGrade grade)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.transform.position = new Vector3(
            groundPosition.x, MapLayout.IslandTop + SlotHeight * 0.5f, groundPosition.z);
        marker.transform.localScale = new Vector3(SlotSize, SlotHeight, SlotSize);
        PaintSolid(marker, GradeColor(grade));
        Object.DestroyImmediate(marker.GetComponent<Collider>());
    }

    static GameObject CreatePortalObject(Transform parent, string name, Vector3 position, float diameter)
    {
        GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        portal.name = name;
        portal.transform.SetParent(parent, false);
        portal.transform.position = position;
        portal.transform.localScale = new Vector3(diameter, 0.5f, diameter);
        Paint(portal, "portal");
        portal.GetComponent<Collider>().isTrigger = true;
        return portal;
    }

    static void ConfigurePortal(GameObject portal, UnitGrade grade, UnitData specificUnit,
                                GachaTable table, UnitSpawner spawner)
    {
        UnitPortal unitPortal = portal.AddComponent<UnitPortal>();
        SerializedObject so = new SerializedObject(unitPortal);

        SerializedProperty accepted = so.FindProperty("acceptedGrades");
        accepted.ClearArray();
        accepted.InsertArrayElementAtIndex(0);
        accepted.GetArrayElementAtIndex(0).enumValueIndex = (int)grade;

        so.FindProperty("legacyGradeMigrated").boolValue = true;
        so.FindProperty("specificUnit").objectReferenceValue = specificUnit;
        so.FindProperty("gachaTable").objectReferenceValue = table;
        so.FindProperty("unitSpawner").objectReferenceValue = spawner;
        // 비워두면 위습 주인의 레인 한가운데로 나간다.
        so.FindProperty("spawnPoint").objectReferenceValue = null;
        so.ApplyModifiedProperties();
    }

    // 좌표를 손으로 옮기다 보면 섬이 서로 올라타는 일이 생긴다(초월 전시가 조합식 표를 덮은 적 있음).
    // 눈으로는 위에서 봐야만 보이므로 생성할 때마다 검사한다.
    static string CheckOverlaps()
    {
        List<MapLayout.Island> all = new List<MapLayout.Island>();
        all.AddRange(MapLayout.Lanes);
        all.AddRange(MapLayout.Warehouses);
        all.AddRange(MapLayout.SealIslands);
        all.AddRange(MapLayout.Zones);

        List<string> hits = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            for (int j = i + 1; j < all.Count; j++)
            {
                if (Overlaps(all[i], all[j]))
                    hits.Add($"{all[i].name} ↔ {all[j].name}");
            }
        }

        return hits.Count == 0 ? "" : "\n⚠️ 섬이 겹칩니다: " + string.Join(", ", hits);
    }

    static bool Overlaps(MapLayout.Island a, MapLayout.Island b)
    {
        return Mathf.Abs(a.center.x - b.center.x) * 2f < a.size.x + b.size.x
            && Mathf.Abs(a.center.y - b.center.y) * 2f < a.size.y + b.size.y;
    }

    // 기존 씬은 전부 원점 근처를 전제로 배치돼 있었다. 새 맵에서 원점은 바다 한가운데라,
    // 그대로 두면 적은 물 위를 걷고 위습은 닿을 수 없는 곳에 생긴다.
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

        report += WireCombineWallet();
        report += SetUpCamera();
        report += EnsureFourPlayers();
        report += WireLanePaths(lanePaths);

        return report;
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
                if (already.IsOccupied)
                {
                    SerializedObject reset = new SerializedObject(already);
                    reset.FindProperty("occupied").boolValue = false;
                    reset.ApplyModifiedProperties();
                }
                continue;
            }

            MapLayout.Island lane = MapLayout.Lanes[playerId];
            GameObject player = new GameObject($"Player{playerId + 1}");
            player.transform.position = new Vector3(lane.center.x, MapLayout.IslandTop, lane.center.y);

            GoldWallet gold = player.AddComponent<GoldWallet>();
            ResourceWallet resources = player.AddComponent<ResourceWallet>();
            UnitInventory units = player.AddComponent<UnitInventory>();
            Warehouse warehouse = player.AddComponent<Warehouse>();
            PlayerContext context = player.AddComponent<PlayerContext>();

            // 창고는 소유자가 맞아야 보관을 받아준다. 기본값 0이면 전부 1번 플레이어 창고가 된다.
            SerializedObject warehouseSo = new SerializedObject(warehouse);
            warehouseSo.FindProperty("ownerPlayerId").intValue = playerId;
            warehouseSo.ApplyModifiedProperties();

            SerializedObject so = new SerializedObject(context);
            so.FindProperty("playerId").intValue = playerId;
            // 구조만 만들어 두고 자리는 비워 둔다. 멀티플레이가 붙기 전까지는
            // 이 레인에 적이 안 나오고 보상도 안 나간다. 테스트할 땐 인스펙터에서 체크.
            so.FindProperty("occupied").boolValue = false;
            so.FindProperty("goldWallet").objectReferenceValue = gold;
            so.FindProperty("resourceWallet").objectReferenceValue = resources;
            so.FindProperty("unitInventory").objectReferenceValue = units;
            so.FindProperty("warehouse").objectReferenceValue = warehouse;
            so.ApplyModifiedProperties();

            created++;
        }

        // 0번(로컬)은 반드시 앉아 있어야 한다. 이 값이 꺼져 있으면 내 레인에도 적이 안 나온다.
        PlayerContext local = System.Array.Find(existing, c => c.PlayerId == 0);
        if (local != null && !local.IsOccupied)
        {
            SerializedObject so = new SerializedObject(local);
            so.FindProperty("occupied").boolValue = true;
            so.ApplyModifiedProperties();
        }

        return "\n플레이어 2~4번 자리는 비워뒀습니다 — 그 레인엔 적이 안 나옵니다."
             + (created > 0 ? $" (새로 만든 슬롯 {created}개)" : "");
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

    // 맵이 420×420이라 기존 카메라 위치(원점 근처, 높이 35)에서는 아무것도 안 보인다.
    static string SetUpCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return "";

        RtsCameraController controller = camera.GetComponent<RtsCameraController>();
        if (controller == null)
            controller = camera.gameObject.AddComponent<RtsCameraController>();

        // 스크립트의 기본값을 바꿔도 이미 씬에 저장된 값은 그대로 남는다.
        // 맵 생성은 초기화 동작이므로 조작 관련 수치를 현재 기준값으로 덮어쓴다.
        SerializedObject cameraSo = new SerializedObject(controller);
        cameraSo.FindProperty("moveSpeed").floatValue = 70f;
        cameraSo.FindProperty("edgeThickness").floatValue = 16f;
        cameraSo.FindProperty("minHeight").floatValue = 12f;
        cameraSo.FindProperty("maxHeight").floatValue = 220f;
        cameraSo.ApplyModifiedProperties();

        // 시작 시점은 내 레인(1번) 하나가 화면에 차는 정도. 전체 조망은 휠로 빼면 된다.
        MapLayout.Island lane = MapLayout.Lanes[0];
        const float pitch = 50f;
        const float height = 52f;
        float backOff = height / Mathf.Tan(pitch * Mathf.Deg2Rad);
        camera.transform.position = new Vector3(lane.center.x, height, lane.center.y - backOff);
        camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1000f);

        return "\n카메라에 RTS 조작(가장자리 밀기·WASD·휠 확대)을 붙이고 메인 필드 위로 옮겼습니다.";
    }

    static string BuildNavMesh(GameObject root)
    {
        NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        try
        {
            surface.BuildNavMesh();
            return "NavMesh를 구웠습니다 (바다 = Sea 영역).";
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[맵] NavMesh 굽기 실패: {e.Message}");
            return "⚠️ NavMesh 굽기에 실패했습니다. Map 오브젝트의 NavMeshSurface에서 Bake를 눌러주세요.";
        }
    }

    static string DisableOldGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null) return "";

        ground.SetActive(false);
        return "\n기존 Ground는 새 맵과 겹쳐서 비활성화했습니다.";
    }

    static void Paint(GameObject obj, string key, float sizeX = 1f, float sizeZ = 1f)
    {
        if (!Surfaces.TryGetValue(key, out Surface surface)) return;

        Renderer renderer = obj.GetComponent<Renderer>();
        renderer.sharedMaterial = GetOrCreateMaterial(key, surface);

        if (surface.texture == null || surface.tilesPerUnit <= 0f) return;

        // 섬마다 크기가 달라 타일 횟수도 달라야 하는데, 머티리얼은 공유한다.
        // 머티리얼을 복제하면 배칭이 깨지므로 렌더러별 프로퍼티 블록으로 타일링만 덮어쓴다.
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Vector4 tiling = new Vector4(
            Mathf.Max(1f, sizeX * surface.tilesPerUnit),
            Mathf.Max(1f, sizeZ * surface.tilesPerUnit), 0f, 0f);
        block.SetVector("_BaseMap_ST", tiling);
        block.SetVector("_BumpMap_ST", tiling);
        renderer.SetPropertyBlock(block);
    }

    static Material GetOrCreateMaterial(string key, Surface surface)
    {
        string path = $"{MaterialFolder}/{key}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

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

        if (surface.texture != null)
        {
            material.SetTexture("_BaseMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{surface.texture}.png"));

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{TextureFolder}/{surface.texture}_normal.png");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");   // 켜지 않으면 노멀맵이 무시된다
            }
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
