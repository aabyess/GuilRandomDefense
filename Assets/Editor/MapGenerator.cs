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
    const float CliffHeight = 2.2f;       // 바위 치마 높이 (바다 아래까지 내려간다)
    const float CliffOverhang = 3.5f;     // 잔디보다 얼마나 넓게 나올지

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
            DecorateLane(root.transform, MapLayout.Lanes[i]);
            BuildSupportShop(root.transform, MapLayout.Lanes[i], i);
            laneObjects.Add(laneObject);
        }

        for (int i = 0; i < MapLayout.Warehouses.Length; i++)
        {
            GameObject warehouseIsland = BuildIsland(root.transform, MapLayout.Warehouses[i]);
            // 창고는 섬 위에 붙는다 — 자기 위치가 곧 유닛을 보낼 곳이다.
            Warehouse warehouse = warehouseIsland.AddComponent<Warehouse>();
            SerializedObject so = new SerializedObject(warehouse);
            so.FindProperty("ownerPlayerId").intValue = i;
            so.ApplyModifiedProperties();
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
        string sealReport = BuildSealSpawners(root.transform);

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
            tableReport + displayReport + gateReport + storyReport + sealReport + overlaps + navResult + oldGround + rewire + "\n\nCmd+S 로 저장하세요.";
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

    // 레인 지형. 적이 도는 자리에 흙길을 깐다.
    // 언덕·웅덩이도 넣어봤는데 납작한 원판으로만 보여서 뺐다 —
    // 높낮이는 실제 지형(메시)이 있어야 나오지, 판을 얹어서 될 일이 아니다.
    // 장식이라 콜라이더는 붙이지 않는다. 붙이면 NavMesh가 울퉁불퉁해져
    // 적이 순찰 경로를 못 따라가거나 유닛이 걸린다.
    const float TrackWidth = 12f;        // 흙길 폭
    const float TrackInset = 14f;        // 섬 가장자리에서 흙길 중심까지 (순찰 경로와 같은 값)

    static void DecorateLane(Transform parent, MapLayout.Island lane)
    {
        float halfX = lane.size.x * 0.5f - TrackInset;
        float halfZ = lane.size.y * 0.5f - TrackInset;
        float x = lane.center.x;
        float z = lane.center.y;
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
    static void BuildSupportShop(Transform parent, MapLayout.Island lane, int laneIndex)
    {
        GameObject shop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shop.name = $"{lane.name}_도움소";
        shop.transform.SetParent(parent, false);
        shop.transform.position = new Vector3(lane.center.x, MapLayout.IslandTop + 1.5f, lane.center.y);
        shop.transform.localScale = new Vector3(4f, 3f, 4f);
        Paint(shop, "warehouse", 4f, 4f);   // 임시 — 전용 모델 없어 창고와 같은 텍스처만 재사용

        shop.AddComponent<Selectable>();
        shop.AddComponent<OwnedByPlayer>().SetOwner(laneIndex);

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
    const float SlotSpacing = 6f;
    const float SlotSize = 3f;
    const float SlotHeight = 3.4f;

    const float GradeWallGap = 8f;     // 한 열 안에서 등급이 바뀔 때 두는 벽 자리
    const int MaxRecipeRows = 25;      // 한 열에 넣을 최대 조합식 수. 넘으면 옆 열로 이어간다
    const float RecipeSlot = 4.5f;      // 유닛 한 칸
    const float RecipeGap = 1.4f;       // 재료 사이 간격
    const float RecipeArrowGap = 4.0f;  // 재료 묶음과 결과 사이
    const float RecipeRowHeight = 6.0f;
    const float RecipeSlotHeight = 3.2f;

    // 조합 비용(코인·목재·행운토큰)을 줄 왼쪽에 세우는 아이콘.
    // 재료 칸보다 작게 둬야 "이건 유닛이 아니라 자원"으로 읽힌다.
    const float CostSlot = 3.2f;
    const float CostGap = 1.2f;
    const float CostBlockGap = 2.0f;   // 비용 묶음과 첫 재료 사이
    const float ColumnPad = 2.0f;      // 열 바닥판 좌우 여백 — 열 사이 벽이 이 안에 선다

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
        List<(UnitGrade grade, List<CombineRecipe> chunk)> current =
            new List<(UnitGrade, List<CombineRecipe>)>();
        float usedDepth = 0f;

        bool columnLocked = false;   // 쪼개진 등급이 쓰는 열에는 다른 등급을 들이지 않는다

        foreach (UnitGrade grade in MapLayout.CombineTableGrades)
        {
            List<CombineRecipe> recipes = LoadRecipesProducing(grade);
            bool splits = recipes.Count > MaxRecipeRows;

            for (int i = 0; i < recipes.Count; i += MaxRecipeRows)
            {
                List<CombineRecipe> chunk = recipes.GetRange(i, Mathf.Min(MaxRecipeRows, recipes.Count - i));
                float body = chunk.Count * RecipeRowHeight;

                // 25행을 넘겨 쪼개진 등급은 "끊어서 옆 열로 잇는다"는 규칙 그대로 항상 새 열에서 시작한다.
                // 같은 등급을 한 열에 위아래로 쌓으면 끊은 의미가 없고, 사이에 서는 벽이
                // 등급이 바뀐 것처럼 보인다. 열을 나눠 쓰는 건 통째로 들어가는 짧은 등급끼리뿐이다.
                bool stack = current.Count > 0 && !splits && !columnLocked
                             && usedDepth + GradeWallGap + body <= island.size.y;

                if (!stack && current.Count > 0)
                {
                    columns.Add(current);
                    current = new List<(UnitGrade, List<CombineRecipe>)>();
                    usedDepth = 0f;
                    columnLocked = false;
                }

                current.Add((grade, chunk));
                usedDepth += body + (current.Count > 1 ? GradeWallGap : 0f);
                columnLocked |= splits;
            }
        }

        if (current.Count > 0) columns.Add(current);

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

            columnWidths[c] = columnMaxSlots[c] * (RecipeSlot + RecipeGap) + RecipeArrowGap + RecipeSlot
                              + ColumnPad * 2f;
            totalWidth += columnWidths[c];
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
            float rowLeftX = columnLeft + ColumnPad + RecipeSlot * 0.5f;
            float resultX = rowLeftX + columnMaxSlots[c] * (RecipeSlot + RecipeGap) + RecipeArrowGap;

            // 첫 열 왼쪽부터 마지막 열 오른쪽까지, 칸 경계마다 한 장씩.
            BuildColumnWall(parent, $"조합표_칸벽_{c}", columnLeft, island.center.y, island.size.y);
            if (c == columns.Count - 1)
                BuildColumnWall(parent, "조합표_칸벽_끝", cursorX, island.center.y, island.size.y);

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

        float available = island.size.y;
        string verdict = deepest <= available
            ? $"여유 {available - deepest:F0}"
            : $"⚠️ {deepest - available:F0} 모자람 — CombineTable 세로를 {Mathf.CeilToInt(deepest) + 8}으로";

        string fit = totalWidth <= island.size.x
            ? $"가로 {totalWidth:F0}/{island.size.x:F0} 여유 {island.size.x - totalWidth:F0}"
            : $"⚠️ 가로 {totalWidth - island.size.x:F0} 모자람 — CombineTable 가로를 {Mathf.CeilToInt(totalWidth) + 8}으로";

        return $"\n조합식 표: {placed}개 조합식, {columns.Count}열 (등급 블록 최대 {MaxRecipeRows}행)." +
               $"\n  깊이 {deepest:F0}/{available:F0} {verdict}\n  {fit}" +
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

    static float MaxRecipeRowWidth(UnitGrade[] grades, bool withCosts)
    {
        float widest = 0f;

        foreach (UnitGrade grade in grades)
            foreach (CombineRecipe recipe in LoadRecipesProducing(grade))
                widest = Mathf.Max(widest, RecipeRowWidth(recipe, withCosts));

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
    static void PlaceRecipeRow(Transform parent, CombineRecipe recipe, float leftX, float z, float resultX,
                               bool showCosts = false)
    {
        float x = leftX;
        string label = recipe.result != null ? recipe.result.unitName : "?";

        // 비용은 재료보다 앞에 세운다. 재료 개수가 줄마다 달라서 뒤에 붙이면 들쭉날쭉해지는데,
        // 앞에 두면 줄이 달라도 세로로 나란히 서서 "여긴 다 토큰이 든다"가 한눈에 보인다.
        if (showCosts) x += PlaceCostIcons(parent, recipe, x, z, label);

        if (recipe.ingredients != null)
        {
            foreach (RecipeIngredient ingredient in recipe.ingredients)
            {
                if (ingredient == null) continue;

                // count가 3이면 같은 칸을 세 번 놓는다 — 원작 표가 그렇게 늘어놓는다.
                for (int n = 0; n < Mathf.Max(1, ingredient.count); n++)
                {
                    PlaceRecipeSlot(parent, x, z, IngredientName(ingredient), IngredientColor(ingredient),
                                    $"재료_{label}");
                    x += RecipeSlot + RecipeGap;
                }
            }
        }

        Color resultColor = recipe.result != null ? GradeColor(recipe.result.grade) : Color.gray;
        PlaceRecipeSlot(parent, resultX, z, label, resultColor, $"결과_{label}");
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
            x += CostSlot + CostGap;
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
                x += CostSlot + CostGap;
                placed++;
            }
        }

        return placed == 0 ? 0f : x - leftX + CostBlockGap;
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
    }

    static void PlaceRecipeSlot(Transform parent, float x, float z, string label, Color color, string prefix)
    {
        GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slot.name = $"{prefix}_{label}";
        slot.transform.SetParent(parent, false);
        slot.transform.position = new Vector3(x, MapLayout.IslandTop + RecipeSlotHeight * 0.5f, z);
        slot.transform.localScale = new Vector3(RecipeSlot, RecipeSlotHeight, RecipeSlot);
        PaintSolid(slot, color);
        // 표 위를 걸어다녀야 하므로 통과시킨다.
        Object.DestroyImmediate(slot.GetComponent<Collider>());
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

    // 특수지급 칸의 한 자리. 대부분은 지정 유닛을 주지만, 목재 자리처럼 유닛이 아닌 걸 주는 자리가 섞여 있다.
    // 이름 배열 두 개를 나란히 두면 자리마다 "무엇을 주는지"를 null 여부로만 구분하게 되어,
    // 유닛이 아닌 지급물이 늘어날수록 구분이 불가능해진다. 자리 하나를 한 값으로 묶는다.
    struct SpecialSlot
    {
        public string label;
        public string unitAsset;   // 로스터 에셋 이름. 유닛을 주는 자리에서만 채운다
        public bool givesWood;

        public static SpecialSlot Unit(string label, string unitAsset) =>
            new SpecialSlot { label = label, unitAsset = unitAsset };

        public static SpecialSlot Wood(string label) =>
            new SpecialSlot { label = label, givesWood = true };

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

    // 원작 배치 순서 그대로. 특수 칸은 목재·쿠마위습(박은석)·레일리(이승우)·배(상붕카).
    static readonly GachaBand[] GachaBands =
    {
        GachaBand.Random("안흔함", UnitGrade.Uncommon),
        GachaBand.Random("특별함", UnitGrade.Special),
        GachaBand.RandomWithBonus("희귀함·특수함", UnitGrade.Rare, UnitGrade.Superior, 3f),
        GachaBand.Special("특수지급",
            SpecialSlot.Wood("목재"),
            SpecialSlot.Pending("박은석위습"),
            SpecialSlot.Unit("이승우", "희귀함_이승우"),
            SpecialSlot.Unit("상붕카", "안흔함_상붕카")),
        GachaBand.Random("전설·히든", UnitGrade.Legendary),
    };

    // 뽑기 섬. 원작처럼 위쪽에 흔함 유닛을 가로로 늘어놓고, 그 아래에 등급별 랜덤 포탈을 둔다.
    // 흔함 위습은 "선택"이라 원하는 유닛 칸에 넣고, 그 위 등급은 칸 하나에서 등급 내 랜덤이 나온다.
    const float PortalDiameter = 9f;
    const float ChoicePortalDiameter = 6.5f;

    // 흔함 선택 칸은 원작처럼 칸마다 벽을 둘러 부스로 만든다.
    const float CommonAreaDepth = 18f;  // 흔함 포탈 줄에서 아래벽까지 — 위습이 생길 자리
    const float PortalInset = 9f;       // 칸 위벽에서 포탈까지
    const float WispSpawnGap = 11f;      // 포탈에서 위습 생성 지점까지
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
                new Vector3(x, 0f, rowZ + BoothDepth * 0.5f), UnitGrade.Common);
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
        commonCell.transform.position = new Vector3(
            island.center.x, MapLayout.IslandTop, (rowZ + commonFloorZ) * 0.5f);
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
            // 이 칸에서 생길 위습의 등급을 표시한다. 특수 칸은 위습이 따로 없다.
            if (band.specialSlots == null)
            {
                GameObject cell = new GameObject($"위습칸_{band.label}");
                cell.transform.SetParent(parent, false);
                cell.transform.position = new Vector3((columnLeft + columnRight) * 0.5f,
                                                      MapLayout.IslandTop, portalZ - WispSpawnGap);
                cell.AddComponent<WispCell>().SetGrade(band.grade);
            }

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

                if (slot.givesWood)
                {
                    // 자원 칸 서쪽 목재 포탈과 같은 지급이다(WISP_SYSTEM.md: 66% 확률로 목재 1개).
                    // 등급을 안 가리는 것도 그쪽과 같다 — BuildResourcePortal이 acceptedGrades를 비워둔다.
                    BuildResourcePortal(parent, $"Portal_{slot.label}", new Vector3(at.x, 0f, at.z),
                        ResourcePortal.Payout.Resource, ResourceType.Wood, 1, 0, 66f,
                        ChoicePortalDiameter);
                    continue;
                }

                UnitData unit = slot.unitAsset == null
                    ? null
                    : AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{slot.unitAsset}.asset");

                if (unit != null)
                {
                    GameObject portal = CreatePortalObject(parent, $"Portal_{unit.unitName}", at, ChoicePortalDiameter);
                    ConfigurePortal(portal, unit.grade, unit, table, spawner);
                }
                else
                {
                    // 지급물의 형태가 아직 안 정해진 자리(초월 재료 박은석). 자리만 세워두고 보고에 남긴다.
                    PlaceUnitMarker(parent, $"미구현_{slot.label}", new Vector3(at.x, 0f, at.z),
                        UnitGrade.RandomUnit);
                    specialPending.Add(slot.label);
                }
            }
        }

        // --- 오른쪽 세로줄: 조합식 없이 캐릭터만 전시하는 등급 ---
        // 이 등급들은 조합식 표에 올리지 않기로 확정돼 있어서, 여기가 유일하게 눈으로 보는 곳이다.
        float rightColumnLeft = island.center.x - 4f;   // 등급 칸 열의 오른벽과 같은 자리
        float displayLeft = rightColumnLeft + 4f;
        float displayWidth = left + island.size.x - 2f - displayLeft;
        int perRow = Mathf.Max(1, Mathf.FloorToInt(displayWidth / SlotSpacing));
        // bandTop은 열 위벽이 서는 자리다. 거기서 바로 시작하면 첫 줄이 벽에 끼인다.
        float displayZ = bandTop - SlotSpacing;
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
                    PlaceRecipeRow(parent, recipe, rowLeftX, displayZ, blockResultX, showCosts: true);
                    displayZ -= RecipeRowHeight;
                    recipeRows++;
                }

                displayZ -= SlotSpacing;
                continue;
            }

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

        // 북: 유닛 랜덤 — 등급 무관 랜덤이라 UnitPortal 쪽이다.
        GameObject unitRandom = CreatePortalObject(parent, "Portal_유닛랜덤",
            new Vector3(centerX, y, centerZ + armZ), PortalDiameter);
        ConfigurePortal(unitRandom, UnitGrade.RandomUnit, null, table, spawner);

        // 동: 금화 랜덤 — 원작은 "15 + 라운드×12~35". 라운드 비례 부분만 옮겼다.
        BuildResourcePortal(parent, "Portal_금화랜덤", new Vector3(centerX + armX, 0f, centerZ),
            ResourcePortal.Payout.Gold, ResourceType.Wood, 15, 20, 100f);

        // 서: 목재 랜덤 — 원작은 66% 확률로 목재 1개.
        BuildResourcePortal(parent, "Portal_목재랜덤", new Vector3(centerX - armX, 0f, centerZ),
            ResourcePortal.Payout.Resource, ResourceType.Wood, 1, 0, 66f);

        // 남: 도움소 마나 — 원작은 "20 + 라운드×1.5 회복".
        // 마나를 쓰는 도움소 건물은 아직 없지만, 자원은 지금부터 쌓아둔다.
        BuildResourcePortal(parent, "Portal_도움소마나", new Vector3(centerX, 0f, centerZ - armZ),
            ResourcePortal.Payout.Resource, ResourceType.Mana, 20, 2, 100f);

        // 가운데에서 위습이 생긴다. 여기서 어느 포탈로 갈지는 플레이어가 정한다.
        GameObject cell = new GameObject("위습칸_자원");
        cell.transform.SetParent(parent, false);
        cell.transform.position = new Vector3(centerX, MapLayout.IslandTop, centerZ);
        cell.AddComponent<WispCell>().SetGrade(UnitGrade.RandomUnit);

        return "\n자원 칸: 가운데 위습 → 북 유닛랜덤 · 동 금화 · 서 목재 · 남 마나.";
    }

    // 도박소. 뽑기 섬과 달리 위습을 안 쓴다 — 도박은 목재만 있으면 반복해서 돌리는 행위라
    // 위습 스폰→드래그 조작을 넣으면 판당 마찰이 너무 크다(GAMBLING.md). 포탈을 직접 클릭한다.
    struct GamblingTier
    {
        public string label;
        public int woodCost;
        public float successChance;
        public UnitGrade primaryGrade;
        public UnitGrade? secondaryGrade;
        public bool grantFailureReward;

        public GamblingTier(string label, int woodCost, float successChance, UnitGrade primaryGrade,
                            UnitGrade? secondaryGrade, bool grantFailureReward)
        {
            this.label = label;
            this.woodCost = woodCost;
            this.successChance = successChance;
            this.primaryGrade = primaryGrade;
            this.secondaryGrade = secondaryGrade;
            this.grantFailureReward = grantFailureReward;
        }
    }

    // 성공 등급·확률·비용은 Docs/reference/GAMBLING.md 그대로.
    // 하급(흔함~안흔함)·고급(희귀함+특별함)처럼 등급이 둘인 곳은 GachaTable weight로 가중 추첨한다
    // (문서에 정확한 배분이 없어 새 확률을 지어내는 대신 이미 설정된 값을 재사용 — PM 승인).
    // 실패 보상(행운토큰2+목재1)은 고급·다른세계 도박만 — "고급 도박과 다른 세계 도박이 실패했을 때"라고
    // 원작 인용이 콕 집어 말한 두 곳뿐이다.
    static readonly GamblingTier[] GamblingTiers =
    {
        new GamblingTier("하급도박", 1, 85f, UnitGrade.Common, UnitGrade.Uncommon, false),
        new GamblingTier("중급도박", 1, 70f, UnitGrade.Special, null, false),
        new GamblingTier("고급도박", 4, 70f, UnitGrade.Rare, UnitGrade.Special, true),
        new GamblingTier("다른세계 도박", 5, 17f, UnitGrade.OtherWorld, null, true),
    };

    // 도박소는 섬이 아니라 레인 안 상점 건물이 된다(사장님 확정, 2026-09-01).
    // 등급표와 이 배선 함수는 그 건물이 그대로 재사용한다.
    static void ConfigureGamblingPortal(GameObject portal, GamblingTier tier, GachaTable table, UnitSpawner spawner)
    {
        GamblingPortal component = portal.AddComponent<GamblingPortal>();
        SerializedObject so = new SerializedObject(component);

        so.FindProperty("woodCost").intValue = tier.woodCost;
        so.FindProperty("successChancePercent").floatValue = tier.successChance;
        so.FindProperty("primaryResultGrade").enumValueIndex = (int)tier.primaryGrade;
        so.FindProperty("useSecondaryGrade").boolValue = tier.secondaryGrade.HasValue;
        so.FindProperty("secondaryResultGrade").enumValueIndex = (int)(tier.secondaryGrade ?? tier.primaryGrade);
        so.FindProperty("grantFailureReward").boolValue = tier.grantFailureReward;
        so.FindProperty("gachaTable").objectReferenceValue = table;
        so.FindProperty("unitSpawner").objectReferenceValue = spawner;

        so.ApplyModifiedProperties();
    }

    // diameter는 자원 칸(넓은 포탈)과 뽑기 섬 특수지급 칸(좁은 선택 포탈)이 서로 다른 크기를 쓴다.
    static void BuildResourcePortal(Transform parent, string name, Vector3 ground,
                                    ResourcePortal.Payout payout, ResourceType resource,
                                    int baseAmount, int perRound, float chance,
                                    float diameter = PortalDiameter)
    {
        GameObject portal = CreatePortalObject(parent, name,
            new Vector3(ground.x, MapLayout.IslandTop + 0.25f, ground.z), diameter);

        ResourcePortal component = portal.AddComponent<ResourcePortal>();
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("payout").enumValueIndex = (int)payout;
        so.FindProperty("resourceType").enumValueIndex = (int)resource;
        so.FindProperty("baseAmount").intValue = baseAmount;
        so.FindProperty("perRound").intValue = perRound;
        so.FindProperty("successChancePercent").floatValue = chance;
        // acceptedGrades를 비워두면 어떤 위습이든 받는다 — 자원 칸은 등급을 가리지 않는다.
        so.ApplyModifiedProperties();
    }

    // 스토리존. 가운데 단상에서 스토리 적이 나오고, 플레이어는 자기 레인에서 유닛을 보내 잡는다.
    static string BuildStoryZone(Transform parent)
    {
        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "StoryZone");

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        platform.name = "스토리_단상";
        platform.transform.SetParent(parent, false);
        platform.transform.position = new Vector3(island.center.x, MapLayout.IslandTop + 0.15f, island.center.y);
        // 단상은 섬 크기에 비례하게 — 섬을 키울 때마다 따로 고치지 않아도 되게.
        float platformSize = Mathf.Min(island.size.x, island.size.y) * 0.35f;
        platform.transform.localScale = new Vector3(platformSize, 0.3f, platformSize);
        Paint(platform, "rock", platformSize, platformSize);
        Object.DestroyImmediate(platform.GetComponent<Collider>());

        GameObject spawn = new GameObject("스토리_등장지점");
        spawn.transform.SetParent(parent, false);
        spawn.transform.position = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);

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

    // 물범섬 4곳. 물범을 잡으면 전체 플레이어에게 목재 1개씩.
    static string BuildSealSpawners(Transform parent)
    {
        EnemyData seal = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_Seal.asset");

        foreach (MapLayout.Island island in MapLayout.SealIslands)
        {
            GameObject spawner = new GameObject($"{island.name}_물범");
            spawner.transform.SetParent(parent, false);
            spawner.transform.position = new Vector3(island.center.x, MapLayout.IslandTop, island.center.y);

            SealSpawner component = spawner.AddComponent<SealSpawner>();
            SerializedObject so = new SerializedObject(component);
            so.FindProperty("sealData").objectReferenceValue = seal;
            so.ApplyModifiedProperties();
        }

        return seal != null
            ? $"\n물범: {MapLayout.SealIslands.Length}곳에 배치."
            : "\n  ⚠️ Enemy_Seal 에셋을 찾지 못해 물범이 안 나옵니다.";
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

    static void ApplyBonusGrade(GameObject portal, UnitGrade bonusGrade, float chance)
    {
        SerializedObject so = new SerializedObject(portal.GetComponent<UnitPortal>());
        so.FindProperty("bonusGrade").enumValueIndex = (int)bonusGrade;
        so.FindProperty("bonusChancePercent").floatValue = chance;
        so.ApplyModifiedProperties();
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
        report += FitMinimapToIslands();
        report += MoveWarehousesToIslands();
        report += WireAllRecipes();
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
            PlayerContext context = player.AddComponent<PlayerContext>();

            SerializedObject so = new SerializedObject(context);
            so.FindProperty("playerId").intValue = playerId;
            // 구조만 만들어 두고 자리는 비워 둔다. 멀티플레이가 붙기 전까지는
            // 이 레인에 적이 안 나오고 보상도 안 나간다. 테스트할 땐 인스펙터에서 체크.
            so.FindProperty("occupied").boolValue = false;
            so.FindProperty("goldWallet").objectReferenceValue = gold;
            so.FindProperty("resourceWallet").objectReferenceValue = resources;
            so.FindProperty("unitInventory").objectReferenceValue = units;
            so.FindProperty("warehouse").objectReferenceValue = FindWarehouse(playerId);
            so.ApplyModifiedProperties();

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
        // 섬 배치가 바뀔 때마다 손으로 맞추면 어긋난다. 실제 범위를 재서 넣는다.
        IslandBounds bounds = MeasureIslands();

        SerializedObject cameraSo = new SerializedObject(controller);
        cameraSo.FindProperty("boundsMin").vector2Value =
            new Vector2(bounds.minX - CameraMargin, bounds.minZ - CameraMargin);
        cameraSo.FindProperty("boundsMax").vector2Value =
            new Vector2(bounds.maxX + CameraMargin, bounds.maxZ + CameraMargin);
        cameraSo.FindProperty("moveSpeed").floatValue = 110f;
        cameraSo.FindProperty("edgeThickness").floatValue = 16f;
        cameraSo.FindProperty("minHeight").floatValue = 20f;
        cameraSo.FindProperty("maxHeight").floatValue = 420f;
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

    // 미니맵은 바다 전체가 아니라 섬이 있는 범위를 담아야 섬이 크게 보인다.
    static string FitMinimapToIslands()
    {
        MinimapCamera minimap = Object.FindFirstObjectByType<MinimapCamera>(FindObjectsInactive.Include);
        if (minimap == null) return "";   // 미니맵은 실행 중에 만들어진다 — 편집 모드엔 없을 수 있다

        IslandBounds bounds = MeasureIslands();
        SerializedObject so = new SerializedObject(minimap);
        so.FindProperty("mapCenter").vector3Value = bounds.Center;
        so.FindProperty("mapExtent").floatValue = bounds.Extent + 30f;
        so.ApplyModifiedProperties();

        return "\n미니맵 범위를 섬 전체에 맞췄습니다.";
    }

    const float CameraMargin = 60f;   // 섬 끝을 화면 가운데 두고도 주변이 보이도록

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
