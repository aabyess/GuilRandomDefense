using System.Collections.Generic;
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
    const string Title = "맵 생성";

    static readonly Dictionary<string, Color> Tints = new Dictionary<string, Color>
    {
        { "sea",       new Color(0.18f, 0.42f, 0.62f) },
        { "lane",      new Color(0.55f, 0.60f, 0.44f) },
        { "warehouse", new Color(0.62f, 0.55f, 0.38f) },
        { "seal",      new Color(0.48f, 0.62f, 0.58f) },
        { "event",     new Color(0.66f, 0.38f, 0.34f) },
        { "display",   new Color(0.45f, 0.40f, 0.58f) },
        { "story",     new Color(0.58f, 0.50f, 0.62f) },
        { "gacha",     new Color(0.60f, 0.58f, 0.42f) },
        { "combine",   new Color(0.52f, 0.52f, 0.50f) },
        { "portal",    new Color(0.30f, 0.70f, 0.85f) },
    };

    [MenuItem("Tools/맵/원랜디 맵 생성")]
    static void Generate()
    {
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
        foreach (MapLayout.Island lane in MapLayout.Lanes)
            laneObjects.Add(BuildIsland(root.transform, lane));

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
        BuildCombineColumns(combineIsland);
        int portalCount = BuildGachaPortals(gachaIsland, lanePaths.Count > 0 ? lanePaths[0] : null);

        string overlaps = CheckOverlaps();
        string rewire = RewireScene(lanePaths);
        string navResult = BuildNavMesh(root);
        string oldGround = DisableOldGround();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message =
            $"섬 {MapLayout.Lanes.Length + MapLayout.Warehouses.Length + MapLayout.SealIslands.Length + MapLayout.Zones.Length}개, " +
            $"레인 경로 {lanePaths.Count}개, 뽑기 포탈 {portalCount}개를 만들었습니다.\n\n" +
            overlaps + navResult + oldGround + rewire + "\n\nCmd+S 로 저장하세요.";
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
        Paint(sea, "sea");

        // 바다도 NavMesh에 굽되 Sea 영역으로 표시한다. 지상 유닛은 UnitSpawner가 areaMask에서
        // 이 영역을 빼기 때문에 못 지나가고, 비행·수상보행 유닛만 지나간다.
        NavMeshModifier modifier = sea.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = MapLayout.SeaAreaIndex;
    }

    static GameObject BuildIsland(Transform parent, MapLayout.Island island)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = island.name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(
            island.center.x,
            MapLayout.IslandTop - MapLayout.IslandThickness * 0.5f,
            island.center.y);
        obj.transform.localScale = new Vector3(island.size.x, MapLayout.IslandThickness, island.size.y);
        Paint(obj, island.tint);
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

    // 조합식 표 위의 등급별 세로 칸. 지금은 자리 표시이고, 실제 레시피 표시는 UI 작업이다.
    static void BuildCombineColumns(GameObject table)
    {
        if (table == null) return;

        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "CombineTable");
        int count = MapLayout.CombineTableGrades.Length;
        float columnWidth = island.size.x / (count + 1);

        for (int i = 0; i < count; i++)
        {
            UnitGrade grade = MapLayout.CombineTableGrades[i];
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cube);
            column.name = $"Column_{grade.KoreanName()}";
            column.transform.SetParent(table.transform.parent, false);
            column.transform.position = new Vector3(
                island.center.x - island.size.x * 0.5f + columnWidth * (i + 1),
                MapLayout.IslandTop + 0.5f,
                island.center.y);
            column.transform.localScale = new Vector3(columnWidth * 0.7f, 1f, island.size.y * 0.8f);
            Paint(column, "combine");
        }
    }

    static int BuildGachaPortals(GameObject gachaIsland, WaypointPath firstLanePath)
    {
        if (gachaIsland == null) return 0;

        GachaTable table = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/MainGachaTable.asset");
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>(FindObjectsInactive.Include);

        // 지상 유닛은 바다를 못 건넌다. 뽑기 섬에서 소환하면 레인까지 갈 방법이 없으므로
        // 소환 위치를 플레이어 레인으로 잡는다.
        Transform spawnPoint = null;
        if (firstLanePath != null && firstLanePath.PointCount > 0)
        {
            GameObject marker = new GameObject("UnitSpawnPoint");
            marker.transform.SetParent(firstLanePath.transform.parent, false);
            marker.transform.position = MapLayout.Lanes[0].center.x * Vector3.right
                                      + MapLayout.IslandTop * Vector3.up
                                      + MapLayout.Lanes[0].center.y * Vector3.forward;
            spawnPoint = marker.transform;
        }

        MapLayout.Island island = System.Array.Find(MapLayout.Zones, z => z.name == "GachaIsland");
        UnitGrade[] grades = MapLayout.GachaPortalGrades;
        float step = island.size.y / (grades.Length + 1);

        for (int i = 0; i < grades.Length; i++)
        {
            UnitGrade grade = grades[i];
            GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portal.name = $"Portal_{grade.KoreanName()}";
            portal.transform.SetParent(gachaIsland.transform.parent, false);
            // 위쪽(+Z)이 낮은 등급 — 사용자 설명 기준 "제일 위에 흔함"
            portal.transform.position = new Vector3(
                island.center.x,
                MapLayout.IslandTop + 0.25f,
                island.center.y + island.size.y * 0.5f - step * (i + 1));
            portal.transform.localScale = new Vector3(7f, 0.5f, 7f);
            Paint(portal, "portal");

            portal.GetComponent<Collider>().isTrigger = true;

            UnitPortal unitPortal = portal.AddComponent<UnitPortal>();
            SerializedObject so = new SerializedObject(unitPortal);
            SerializedProperty accepted = so.FindProperty("acceptedGrades");
            accepted.ClearArray();
            accepted.InsertArrayElementAtIndex(0);
            accepted.GetArrayElementAtIndex(0).enumValueIndex = (int)grade;
            so.FindProperty("legacyGradeMigrated").boolValue = true;
            so.FindProperty("gachaTable").objectReferenceValue = table;
            so.FindProperty("unitSpawner").objectReferenceValue = spawner;
            so.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            so.ApplyModifiedProperties();
        }

        return grades.Length;
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

        report += SetUpCamera();
        report += EnsureFourPlayers();
        report += WireLanePaths(lanePaths);

        return report;
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
            if (System.Array.Exists(existing, c => c.PlayerId == playerId)) continue;

            MapLayout.Island lane = MapLayout.Lanes[playerId];
            GameObject player = new GameObject($"Player{playerId + 1}");
            player.transform.position = new Vector3(lane.center.x, MapLayout.IslandTop, lane.center.y);

            GoldWallet gold = player.AddComponent<GoldWallet>();
            ResourceWallet resources = player.AddComponent<ResourceWallet>();
            UnitInventory units = player.AddComponent<UnitInventory>();
            Warehouse warehouse = player.AddComponent<Warehouse>();
            PlayerContext context = player.AddComponent<PlayerContext>();

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

        return created > 0
            ? $"\n플레이어 {created}명분(2~4번) 구조를 만들었습니다 — 자리는 비어 있어 그 레인엔 적이 안 나옵니다."
            : "";
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

        if (camera.GetComponent<RtsCameraController>() == null)
            camera.gameObject.AddComponent<RtsCameraController>();

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

    static void Paint(GameObject obj, string tint)
    {
        if (!Tints.TryGetValue(tint, out Color color)) return;

        string path = $"{MaterialFolder}/{tint}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateFolder("Assets/Materials", "Map");
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
        }

        obj.GetComponent<Renderer>().sharedMaterial = material;
    }
}
