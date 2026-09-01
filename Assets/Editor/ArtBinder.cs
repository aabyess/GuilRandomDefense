using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
    const string ControllerPath = GeneratedFolder + "/Character.controller";

    // 클립 이름에서 찾을 낱말. Mixamo가 붙이는 영어 이름과, 직접 붙일 수 있는 한국어를 같이 본다.
    static readonly string[] IdleWords   = { "idle", "breathing", "대기" };
    static readonly string[] WalkWords   = { "walk", "run", "move", "이동", "걷" };
    static readonly string[] AttackWords = { "attack", "punch", "slash", "swing", "kick", "공격" };
    static readonly string[] DeathWords  = { "death", "dying", "die", "사망", "죽" };
    const string UnitTemplate = "Assets/Prefabs/UnitPrefab.prefab";

    const string MaterialFolder = "Assets/Art/Materials";

    /// <summary>
    /// 모델의 머티리얼에 텍스처를 붙인다.
    ///
    /// Mixamo는 OBJ를 리깅해서 FBX로 뱉을 때 텍스처를 안 실어준다 — 뼈대만 온다.
    /// 그래서 임포트하면 새하얗게 나온다. 텍스처 파일은 원래 다운로드에 같이 들어 있으니,
    /// 머티리얼 이름과 파일 이름을 맞춰 다시 이어준다.
    /// </summary>
    [MenuItem("Tools/아트/텍스처 연결")]
    public static void LinkTextures()
    {
        List<Texture2D> textures = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .Where(t => t != null)
            .ToList();

        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog(Title,
                "Assets/Art 아래에서 텍스처를 찾지 못했습니다.\n\n" +
                "모델을 받은 폴더의 PNG·JPG를 모델과 같은 곳에 넣어주세요.", "확인");
            return;
        }

        EnsureFolder(MaterialFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        int linked = 0, missed = 0;
        List<string> unmatched = new List<string>();

        foreach (string modelPath in ModelPaths())
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) continue;

            // 모델 안에 박힌 머티리얼은 못 고친다. 밖으로 빼서 우리가 만든 것으로 갈아 끼운다.
            List<AssetImporter.SourceAssetIdentifier> slots =
                AssetDatabase.LoadAllAssetsAtPath(modelPath)
                    .OfType<Material>()
                    .Select(m => new AssetImporter.SourceAssetIdentifier(m))
                    .ToList();

            // 임포터가 이미 리맵해둔 슬롯도 다시 훑어야 한다 — 두 번째 실행에서 원본이 안 잡힌다.
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> pair in importer.GetExternalObjectMap())
                if (pair.Key.type == typeof(Material) && !slots.Any(s => s.name == pair.Key.name))
                    slots.Add(pair.Key);

            foreach (AssetImporter.SourceAssetIdentifier slot in slots)
            {
                Texture2D texture = MatchTexture(slot.name, textures);
                if (texture == null)
                {
                    unmatched.Add($"{System.IO.Path.GetFileName(modelPath)} / {slot.name}");
                    missed++;
                    continue;
                }

                string materialPath = $"{MaterialFolder}/{slot.name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_MainTex", texture);   // Standard 폴백
                EditorUtility.SetDirty(material);

                importer.AddRemap(slot, material);
                linked++;
            }

            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report = $"텍스처 {textures.Count}장을 찾아 머티리얼 {linked}개에 연결했습니다.";
        if (missed > 0)
            report += $"\n\n짝을 못 찾은 머티리얼 {missed}개 — 이름이 텍스처 파일명과 달라서입니다:\n  " +
                      string.Join("\n  ", unmatched.Take(10)) +
                      $"\n\n{MaterialFolder} 에서 직접 텍스처를 끌어다 넣으면 됩니다.";

        Debug.Log("[아트] " + report);
        EditorUtility.DisplayDialog(Title, report, "확인");
    }

    // 머티리얼 이름과 텍스처 파일명을 맞춘다. 완전히 같은 것부터 보고, 없으면 한쪽이 다른 쪽을
    // 품고 있는지 본다(Mixamo가 이름에 접미사를 붙이는 경우가 있다).
    // 텍스처가 딱 하나뿐이면 그걸 쓴다 — 머티리얼도 하나일 가능성이 높다.
    static Texture2D MatchTexture(string materialName, List<Texture2D> textures)
    {
        string target = materialName.ToLowerInvariant();

        Texture2D exact = textures.FirstOrDefault(t => t.name.ToLowerInvariant() == target);
        if (exact != null) return exact;

        Texture2D partial = textures.FirstOrDefault(t =>
        {
            string name = t.name.ToLowerInvariant();
            return target.Contains(name) || name.Contains(target);
        });
        if (partial != null) return partial;

        return textures.Count == 1 ? textures[0] : null;
    }

    static IEnumerable<string> ModelPaths()
    {
        return AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.EndsWith(".prefab"))
            .Distinct();
    }

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

        string report = MakeHumanoid();
        report += BuildController();
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

    // ── 리그 ───────────────────────────────────────────────────────────

    // 모델을 Humanoid로 임포트한다. 이게 이 파이프라인의 핵심이다 —
    // Humanoid끼리는 뼈대 이름이 달라도 애니메이션이 통하므로, 클립 한 세트를 234종이 같이 쓴다.
    // Generic으로 들어오면 그 모델 전용 클립만 재생돼서, 캐릭터마다 애니메이션을 따로 받아야 한다.
    static string MakeHumanoid()
    {
        int converted = 0;
        List<string> failed = new List<string>();

        foreach (string path in ModelPaths())
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.animationType == ModelImporterAnimationType.Human) continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();

            // 뼈대가 사람 형태가 아니면 Unity가 매핑에 실패하고 조용히 되돌린다.
            ModelImporter after = AssetImporter.GetAtPath(path) as ModelImporter;
            if (after != null && after.animationType == ModelImporterAnimationType.Human) converted++;
            else failed.Add(System.IO.Path.GetFileName(path));
        }

        if (converted == 0 && failed.Count == 0) return "";

        string report = $"\n리그: 모델 {converted}개를 Humanoid로 맞췄습니다.";
        if (failed.Count > 0)
            report += $"\n  ⚠️ Humanoid로 못 바꾼 모델 {failed.Count}개 — 뼈대가 없거나 사람 형태가 아닙니다:\n     " +
                      string.Join(", ", failed.Take(5));

        return report;
    }

    // ── 애니메이터 컨트롤러 ────────────────────────────────────────────

    // 클립을 찾아 대기/이동/공격/사망 넷을 엮은 컨트롤러를 하나 만든다.
    // Humanoid 리그라면 모델이 달라도 같은 컨트롤러가 붙으므로, 234종이 이 하나를 공유한다.
    static string BuildController()
    {
        AnimationClip idle = FindClip(IdleWords);
        AnimationClip walk = FindClip(WalkWords);
        AnimationClip attack = FindClip(AttackWords);
        AnimationClip death = FindClip(DeathWords);

        if (idle == null && walk == null)
            return "\n애니메이션 클립을 못 찾아 컨트롤러는 만들지 않았습니다 — 모델은 T포즈로 서 있습니다." +
                   "\n  Mixamo에서 대기·이동·공격·사망을 받아 Assets/Art 아래에 넣고 다시 실행하세요.";

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // 다시 돌릴 때 상태가 쌓이지 않도록 매번 새로 짠다.
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState state in machine.states.ToArray()) machine.RemoveState(state.state);
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            controller.RemoveParameter(parameter);

        controller.AddParameter(CharacterAnimator.SpeedParam, AnimatorControllerParameterType.Float);
        controller.AddParameter(CharacterAnimator.AttackParam, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CharacterAnimator.DieParam, AnimatorControllerParameterType.Trigger);

        // 대기↔이동은 속도 하나로 갈린다. 문턱을 하나로 두면 그 값 근처에서 깜빡이므로 위아래를 벌린다.
        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle != null ? idle : walk;
        machine.defaultState = idleState;

        AnimatorState walkState = machine.AddState("Move");
        walkState.motion = walk != null ? walk : idle;

        AnimatorStateTransition toMove = idleState.AddTransition(walkState);
        toMove.hasExitTime = false;
        toMove.duration = 0.1f;
        toMove.AddCondition(AnimatorConditionMode.Greater, 0.15f, CharacterAnimator.SpeedParam);

        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, CharacterAnimator.SpeedParam);

        int made = 2;

        if (attack != null)
        {
            AnimatorState attackState = machine.AddState("Attack");
            attackState.motion = attack;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(attackState);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimator.AttackParam);

            // 공격은 한 번 재생하고 돌아온다 — 안 돌려보내면 그 자세로 굳는다.
            AnimatorStateTransition exit = attackState.AddTransition(idleState);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.1f;
            made++;
        }

        if (death != null)
        {
            AnimatorState deathState = machine.AddState("Death");
            deathState.motion = death;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(deathState);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimator.DieParam);
            made++;
        }

        EditorUtility.SetDirty(controller);

        string missing = "";
        if (idle == null) missing += " 대기";
        if (walk == null) missing += " 이동";
        if (attack == null) missing += " 공격";
        if (death == null) missing += " 사망";

        return $"\n애니메이터: 상태 {made}개를 엮었습니다 ({ControllerPath})." +
               (missing.Length > 0 ? $"\n  ⚠️ 못 찾은 클립:{missing} — 있는 것으로 대신합니다." : "");
    }

    // 이름에 낱말이 들어간 첫 클립. Mixamo FBX는 클립을 파일 안에 품고 있어서 서브에셋으로 찾는다.
    static AnimationClip FindClip(string[] words)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Art" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;

                string haystack = (path + "/" + clip.name).ToLowerInvariant();
                if (words.Any(word => haystack.Contains(word))) return clip;
            }
        }

        return null;
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
        AttachAnimator(instance, visual);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);

        made++;
        cache[model] = saved;
        return saved;
    }

    // 모델에 Animator가 있으면 컨트롤러를 물리고, 게임 상태를 읽어 돌릴 컴포넌트를 붙인다.
    static void AttachAnimator(GameObject root, GameObject visual)
    {
        Animator animator = visual.GetComponentInChildren<Animator>();
        if (animator == null) return;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) animator.runtimeAnimatorController = controller;

        // 이동은 NavMeshAgent·WaypointMover가 시킨다. 애니메이션에 담긴 이동까지 살리면
        // 둘이 겹쳐서 유닛이 두 배로 나아가거나 목적지를 지나쳐 미끄러진다.
        // (Mixamo에서 In Place로 받으면 애초에 안 담기지만, 안 켜고 받았을 때를 막아둔다.)
        animator.applyRootMotion = false;

        if (root.GetComponent<CharacterAnimator>() == null) root.AddComponent<CharacterAnimator>();
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
