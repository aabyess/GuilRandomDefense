using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Art/ 에 넣은 모델을 프리팹으로 만들고 EnemyData·UnitData에 연결한다.
///
/// 큐브 하나를 75종이 공유하던 것을 모델별 프리팹으로 가른다. 프리팹은 기존
/// MobPrefab/UnitPrefab을 복제해서 만든다 — 컴포넌트를 손으로 다시 붙이면
/// 언젠가 하나를 빠뜨리고, 그러면 그 유닛만 조용히 안 움직인다.
/// </summary>
public static class ArtBinder
{
    const string Title = "모델 배선";
    const string GeneratedFolder = "Assets/Prefabs/Generated";
    const string MonsterFolder = "Assets/Art/Monsters";
    const string CharacterFolder = "Assets/Art/Characters";
    const string MobTemplate = "Assets/Prefabs/MobPrefab.prefab";
    const string UnitTemplate = "Assets/Prefabs/UnitPrefab.prefab";

    [MenuItem("Tools/아트/모델 배선")]
    public static void Bind()
    {
        List<GameObject> monsters = LoadModels(MonsterFolder);
        List<GameObject> characters = LoadModels(CharacterFolder);

        if (monsters.Count == 0 && characters.Count == 0)
        {
            EditorUtility.DisplayDialog(Title,
                $"모델을 찾지 못했습니다.\n\n{MonsterFolder} 또는 {CharacterFolder} 에 " +
                "FBX·OBJ·glTF 파일을 넣고 다시 실행하세요.\n\n" +
                "받을 곳은 Docs/reference/CHARACTER_ASSETS.md 에 정리돼 있습니다.", "확인");
            return;
        }

        EnsureFolder(GeneratedFolder);

        string report = "";
        if (monsters.Count > 0) report += BindEnemies(monsters);
        if (characters.Count > 0) report += BindUnits(characters);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[아트] " + report);
        EditorUtility.DisplayDialog(Title, report, "확인");
    }

    // ── 적 ─────────────────────────────────────────────────────────────

    static string BindEnemies(List<GameObject> models)
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(MobTemplate);
        if (template == null) return $"\n⚠️ {MobTemplate} 을 찾지 못해 적 배선을 건너뜁니다.";

        List<EnemyData> enemies = LoadAll<EnemyData>("Assets/Data/Enemies");
        if (enemies.Count == 0) return "\n⚠️ EnemyData가 없습니다.";

        // 보스는 눈에 띄어야 한다. 목록 뒤쪽(대개 덩치 큰 종)을 보스에게 몰아준다.
        List<EnemyData> bosses = enemies.Where(e => e.isBoss).ToList();
        List<EnemyData> mobs = enemies.Where(e => !e.isBoss).ToList();

        int made = 0;
        Dictionary<GameObject, GameObject> cache = new Dictionary<GameObject, GameObject>();

        for (int i = 0; i < bosses.Count; i++)
        {
            GameObject model = models[models.Count - 1 - (i % models.Count)];
            bosses[i].prefab = GetOrCreate(cache, template, model, "Mob", ref made);
            EditorUtility.SetDirty(bosses[i]);
        }

        for (int i = 0; i < mobs.Count; i++)
        {
            GameObject model = models[i % models.Count];
            mobs[i].prefab = GetOrCreate(cache, template, model, "Mob", ref made);
            EditorUtility.SetDirty(mobs[i]);
        }

        return $"\n적: 모델 {models.Count}종 → 프리팹 {made}개, " +
               $"잡몹 {mobs.Count}종 · 보스 {bosses.Count}종에 연결했습니다." +
               (models.Count < 10 ? "\n  ⚠️ 모델이 적어 여러 적이 같은 모습을 씁니다." : "");
    }

    // ── 아군 ───────────────────────────────────────────────────────────

    static string BindUnits(List<GameObject> models)
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(UnitTemplate);
        if (template == null) return $"\n⚠️ {UnitTemplate} 을 찾지 못해 유닛 배선을 건너뜁니다.";

        List<UnitData> units = LoadAll<UnitData>("Assets/Data/Units/Roster");
        if (units.Count == 0) return "\n⚠️ UnitData가 없습니다.";

        // 같은 등급이 같은 모델로 몰리지 않도록 등급 안에서 돌려가며 준다.
        // 등급별 색은 SelectionIndicator·마커가 이미 입히므로 모델까지 등급을 나눌 필요는 없다.
        int made = 0;
        Dictionary<GameObject, GameObject> cache = new Dictionary<GameObject, GameObject>();

        for (int i = 0; i < units.Count; i++)
        {
            GameObject model = models[i % models.Count];
            units[i].prefab = GetOrCreate(cache, template, model, "Unit", ref made);
            EditorUtility.SetDirty(units[i]);
        }

        return $"\n유닛: 모델 {models.Count}종 → 프리팹 {made}개, {units.Count}종에 연결했습니다." +
               (models.Count < 20 ? $"\n  ⚠️ 모델 {models.Count}종을 {units.Count}종이 나눠 씁니다 — " +
                                    "파츠·색을 바꿔 변형을 늘리는 건 다음 단계입니다." : "");
    }

    // ── 프리팹 만들기 ──────────────────────────────────────────────────

    static GameObject GetOrCreate(Dictionary<GameObject, GameObject> cache, GameObject template,
                                  GameObject model, string prefix, ref int made)
    {
        if (cache.TryGetValue(model, out GameObject cached)) return cached;

        string path = $"{GeneratedFolder}/{prefix}_{model.name}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            cache[model] = existing;
            return existing;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = $"{prefix}_{model.name}";

        // 자리표시용 큐브 메시를 걷어내고 그 자리에 모델을 자식으로 붙인다.
        // 컴포넌트(EnemyDummy·WaypointMover·콜라이더 등)는 그대로 둔다 — 그게 이 프리팹의 알맹이다.
        Object.DestroyImmediate(instance.GetComponent<MeshFilter>());
        Object.DestroyImmediate(instance.GetComponent<MeshRenderer>());

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, instance.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        FitToCollider(instance, visual);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);

        made++;
        cache[model] = saved;
        return saved;
    }

    // 모델마다 원본 크기가 제각각이라(1미터짜리도, 100미터짜리도 있다) 그대로 붙이면
    // 어떤 적은 점처럼, 어떤 적은 맵을 덮을 만큼 나온다. 콜라이더 높이에 맞춰 재운다.
    static void FitToCollider(GameObject root, GameObject visual)
    {
        Collider collider = root.GetComponent<Collider>();
        if (collider == null) return;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y < 0.001f) return;

        float target = collider.bounds.size.y;
        if (target < 0.001f) return;

        visual.transform.localScale *= target / bounds.size.y;

        // 스케일을 바꾸면 경계도 바뀐다. 발이 바닥에 닿도록 다시 잰 뒤 내린다.
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        visual.transform.position += Vector3.up * (collider.bounds.min.y - bounds.min.y);
    }

    // ── 도우미 ─────────────────────────────────────────────────────────

    static List<GameObject> LoadModels(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return new List<GameObject>();

        return AssetDatabase.FindAssets("t:GameObject", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.EndsWith(".prefab"))   // 모델 파일만. 이미 만든 프리팹은 제외
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(model => model != null)
            .ToList();
    }

    static List<T> LoadAll<T>(string folder) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(folder)) return new List<T>();

        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToList();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
