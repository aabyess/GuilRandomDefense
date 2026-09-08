using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

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
    // 스킨 한 종당 폴더 하나 — Assets/Art/Units/<유닛이름>/<유닛이름>.fbx + Textures/ + SOURCE.txt.
    // Tools/import_skin.sh 가 이 모양으로 넣는다. 파일명이 로스터 에셋 이름과 같아서
    // ResolveUnitForModel 1번 규칙으로 표 없이 바로 붙는다.
    const string UnitFolder = "Assets/Art/Units";
    const string MobTemplate = "Assets/Prefabs/MobPrefab.prefab";
    const string ControllerPath = GeneratedFolder + "/Character.controller";

    // 클립 이름에서 찾을 낱말. Mixamo가 붙이는 영어 이름과, 직접 붙일 수 있는 한국어를 같이 본다.
    static readonly string[] IdleWords   = { "idle", "breathing", "대기" };
    static readonly string[] WalkWords   = { "walk", "run", "move", "이동", "걷" };
    static readonly string[] AttackWords = { "attack", "punch", "slash", "swing", "kick", "공격" };
    static readonly string[] DeathWords  = { "death", "dying", "die", "사망", "죽" };
    const string UnitTemplate = "Assets/Prefabs/UnitPrefab.prefab";

    const string MaterialFolder = "Assets/Art/Materials";

    // 캐릭터가 화면에서 가져야 할 키. 맵이 크다 — 레인 한 변이 110, 순찰 흙길 폭이 12,
    // 상점 건물이 9×3이다. 프리팹의 캡슐은 2라서 그대로 쓰면 점처럼 보인다.
    const float UnitHeight = 20f;

    // 적은 흙길 위를 줄지어 걷는다. 아군과 같은 키 20으로 두면 지름이 7.2가 되어
    // 폭 12짜리 길에 한 마리 반밖에 안 들어가고, 사람이 길보다 커 보인다.
    //
    // 2026-09-07 사장님 지시("적 유닛 크기가 좀 작은 것 같음")로 12 → 15.
    // 지름은 4.32 → 5.4가 되어 폭 12 길에 두 마리는 그대로 나란히 선다(2.2마리).
    // 세 마리 나란히는 안 되니, 그게 필요해지면 이 값을 12로 되돌려야 한다 —
    // 길 폭(MapLayout)과 묶인 값이라 여기만 보고 올리면 길이 좁아 보인다.
    const float EnemyHeight = 15f;

    // 프리팹 이름이 아니라 붙어 있는 컴포넌트로 가른다 — 이름 규칙이 바뀌어도 안 깨진다.
    static float HeightFor(GameObject root)
    {
        return root.GetComponent<EnemyDummy>() != null ? EnemyHeight : UnitHeight;
    }

    // 지정하지 않은 유닛에도 모델을 돌려가며 나눠줄지. 모델이 몇 개 없을 때 켜두면
    // 234종이 전부 같은 얼굴이 된다 — 팩을 통째로 넣어 종류가 충분할 때만 켠다.
    const bool FillUnassignedUnits = false;

    // 파일명이 유닛과 같으면 자동으로 붙는다. 아래 표는 **이름이 다를 때만** 쓴다.
    //
    // 자동 연결 규칙(ResolveUnitForModel):
    //   1) 파일명이 로스터 에셋 이름과 같다   — "안흔함_상붕카.fbx"
    //   2) 파일명이 유닛 표시 이름과 같다     — "상붕카.fbx"
    //      (단 그 이름을 쓰는 유닛이 하나뿐일 때만. "최상호"는 넷이라 안 걸린다)
    //
    // 모델별로 방향과 크기를 바로잡는다. 파일명 → (오일러 각도, 키 배수).
    //
    // Sketchfab·Blender에서 나온 모델은 축 방향이 제각각이라, 임포터가 Y-up으로 맞춰줘도
    // 원본이 눕혀 저장돼 있으면 그대로 눕는다. 크기도 마찬가지다 — 사람 기준 키 20에
    // 맞추면 탈것이나 짐승은 어색해진다. 모델 파일을 고치는 대신 여기서 조정한다.
    static readonly (string model, Vector3 euler, float heightScale)[] ModelAdjustments =
    {
        // 자전거가 끝으로 선 채(앞바퀴가 하늘) 들어온다. X로 눕혀 바퀴 둘을 바닥에 놓고,
        // Y로 돌려 옆모습이 보이게 한다. 바퀴가 위로 가면 X를 +90으로 뒤집으면 된다.
        // 키 10 — 기준 20의 절반.
        ("안흔함_상붕카", new Vector3(-90f, 90f, 0f), 0.5f),

        // ⚠️ 사람형(Humanoid)은 여기 적지 않는다 — AutoUpright가 뼈 위치로 재서 자동으로 세운다.
        //    2026-09-08: 박준희 거꾸로 → X180으로 고쳤더니 박민수는 「거꾸로 서서 뒤돎」, 김수빈은
        //    「누워서 오른쪽 봄」. 부호를 두 번 틀리고 나서 추측을 그만뒀다.
        //    변환 축 오류는 90°·180° 단위라 뼈로 재면 정확히 나온다.
    };

    // 에셋 이름의 한글은 macOS에서 NFC가 아닐 수 있다 — 리터럴과 견주기 전에 맞춘다.
    static string Nfc(string s) => s?.Normalize(System.Text.NormalizationForm.FormC);

    // 머리·골반·양팔 뼈의 위치로 이 모델이 지금 어느 쪽을 "위"와 "앞"으로 삼는지 재서,
    // 위=+Y·앞=+Z가 되게 돌린다(유니티 규약: 캐릭터는 +Z를 본다).
    //
    // 왜 필요한가 — glb→fbx(assimp) 변환은 스킨 메시를 90°·180° 단위로 틀어 놓는다.
    // 원본 GLB의 루트 회전을 읽어도 안 나온다(실측: 셋 다 -90/+90 상쇄 = 0인데 결과는 제각각).
    // 그래서 결과물의 뼈를 직접 잰다. 잰 각은 90° 단위로 반올림한다 — 바인드 포즈의
    // 자잘한 기울기(A자 팔 등)는 노이즈로 버린다.
    //
    // Generic(아바타 없음)은 못 잰다 — 그건 수동 표(ModelAdjustments)에 적는다.
    static void AutoUpright(GameObject visual)
    {
        Animator animator = visual.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman;

        Transform hips = human ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        Transform head = human ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        Transform leftArm = human ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
        Transform rightArm = human ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;

        // 🔴 아바타가 없는 모델(사람 골격 매핑 실패·Generic)은 뼈로 못 잰다.
        //    2026-09-08 흔함_문필환이 그랬다 — 매핑 0개라 자동 세우기를 그냥 건너뛰고 누운 채로 섰다.
        //    그런 모델은 **렌더러 경계 상자**로 잰다. 사람 모양은 서 있으면 세로가 제일 길고
        //    앞뒤(두께)가 제일 짧다. 그 성질만으로 어느 축이 「위」인지 정해진다.
        //    정확도는 뼈보다 낮지만 「누워 있는 것」과 「서 있는 것」은 확실히 가른다.
        // ⚠️ `avatar.isValid`는 매핑이 0개여도 참을 돌려준다(2026-09-08 흔함_문필환 실측 —
        //    animationType은 Human인데 humanName 매핑이 0개인데도 여기를 통과했다).
        //    **실제로 뼈를 집을 수 있는가**로 판정해야 한다. 넷 중 하나라도 없으면 경계 상자로 간다.
        if (!human || hips == null || head == null || leftArm == null || rightArm == null)
        {
            UprightByBounds(visual);
            return;
        }

        // visual 자신의 회전을 정하는 것이므로 부모(=visual의 부모) 기준 로컬 방향으로 잰다.
        Transform parent = visual.transform.parent;
        Vector3 L(Vector3 world) => parent != null ? parent.InverseTransformPoint(world) : world;

        Vector3 up = L(head.position) - L(hips.position);
        Vector3 right = L(rightArm.position) - L(leftArm.position);
        if (up.sqrMagnitude < 1e-8f || right.sqrMagnitude < 1e-8f) return;

        up.Normalize();
        right = Vector3.ProjectOnPlane(right, up);
        if (right.sqrMagnitude < 1e-8f) return;      // 팔이 머리~골반 축과 평행 = 못 잰다
        right.Normalize();

        // 🔴 오일러 각을 축마다 따로 90°로 반올림하면 안 된다. 회전은 그렇게 분해되지 않는다 —
        //    (270,180,270) 같은 값이 나와도 그게 원하는 회전이라는 보장이 없다.
        //    2026-09-08 실측: 이 방식으로 돌린 안흔함_박준희가 오히려 **누웠다**(머리-발 0.01).
        //
        //    대신 **축을 직접 맞춘다.** 지금의 「위」가 어느 세계 축에 제일 가까운지,
        //    「오른쪽」이 어느 축에 제일 가까운지 골라서 그 둘로 회전을 만든다.
        //    90° 배수만 나오고, 중간에 애매한 각이 끼어들 여지가 없다.
        Vector3 upAxis = NearestAxis(up);
        Vector3 rightAxis = NearestAxis(right, exclude: upAxis);
        if (rightAxis == Vector3.zero) return;

        // 이 모델의 (오른쪽, 위)를 세계의 (+X, +Y)로 보내는 회전.
        Vector3 fwdAxis = Vector3.Cross(rightAxis, upAxis);
        Quaternion snapped = Quaternion.Inverse(Quaternion.LookRotation(fwdAxis, upAxis));
        if (Quaternion.Angle(snapped, Quaternion.identity) < 1f) return;

        visual.transform.localRotation = snapped * visual.transform.localRotation;
        Debug.Log($"[아트] {visual.name}: 뼈로 재서 세웠습니다 — 위 {upAxis}, 오른쪽 {rightAxis}.");
    }

    // 방향 벡터를 여섯 축(±X·±Y·±Z) 중 가장 가까운 것으로 맞춘다.
    // exclude를 주면 그 축과 나란한 것(±)은 후보에서 뺀다 — 위와 오른쪽이 겹치면 안 되기 때문이다.
    static Vector3 NearestAxis(Vector3 v, Vector3 exclude = default)
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

    // 아바타가 없어 뼈로 못 재는 모델을 경계 상자로 세운다.
    //
    // 사람 모양은 서 있을 때 **세로가 가장 길고 두께가 가장 짧다**. 지금 가장 긴 축이
    // Y가 아니면 누워 있는 것이므로, 그 축이 Y로 오게 90° 돌린다.
    // ⚠️ 앞뒤(어느 쪽을 보는가)는 이 방법으로 못 정한다 — 대칭이라 구분할 근거가 없다.
    //    세우는 것까지만 한다. 방향이 틀리면 ModelAdjustments에 Y 회전을 적는다.
    static void UprightByBounds(GameObject visual)
    {
        Bounds b = MeasureRenderers(visual);
        Vector3 size = b.size;

        // 🔴 SkinnedMeshRenderer의 bounds는 프리팹을 막 만든 직후엔 0으로 나올 수 있다
        //    (2026-09-08 안흔함_이호준이 0×0×0으로 찍혔다). 그 상태로 판정하면 엉뚱하게 돌린다.
        //    메시의 정점 경계로 다시 잰다 — 이건 렌더링과 무관하게 항상 값이 있다.
        if (size.x <= 0.0001f || size.y <= 0.0001f || size.z <= 0.0001f)
        {
            size = MeasureMeshes(visual);
            if (size.x <= 0.0001f || size.y <= 0.0001f || size.z <= 0.0001f)
            {
                Debug.LogWarning($"[아트] {visual.name}: 크기를 못 재서 세우지 못했습니다.");
                return;
            }
        }

        // 🔴 여기는 **FitToHeight보다 먼저** 불린다 — 즉 아직 원본 비율이다. 그래서 "가장 긴 축"이
        //    쓸 수 있다. (점검 도구는 반대다: FitToHeight 뒤라 세로가 고정돼 비율로 봐야 한다.)
        //    사람 모양은 서 있으면 세로가 제일 길다.
        int longest = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
        if (longest == 1) return;

        // 가장 긴 축을 Y로 보낸다. X가 길면 Z로 돌리고, Z가 길면 X로 돌린다.
        Vector3 euler = longest == 0 ? new Vector3(0f, 0f, 90f) : new Vector3(-90f, 0f, 0f);
        visual.transform.localRotation = Quaternion.Euler(euler) * visual.transform.localRotation;

        Bounds after = MeasureRenderers(visual);
        Debug.Log($"[아트] {visual.name}: 아바타가 없어 경계 상자로 세웠습니다 — {euler} " +
                  $"(가로세로 {size.x:F1}×{size.y:F1}×{size.z:F1} → {after.size.x:F1}×{after.size.y:F1}×{after.size.z:F1}). " +
                  "앞뒤 방향이 틀리면 ModelAdjustments에 Y 회전을 적으세요.");
    }

    // 메시의 정점으로 크기를 잰다. 렌더러 bounds가 0으로 나오는 경우의 대비책이다.
    static Vector3 MeasureMeshes(GameObject root)
    {
        Bounds? acc = null;
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            Bounds mb = smr.sharedMesh.bounds;
            if (acc == null) acc = mb; else { Bounds a = acc.Value; a.Encapsulate(mb); acc = a; }
        }
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            if (acc == null) acc = mb; else { Bounds a = acc.Value; a.Encapsulate(mb); acc = a; }
        }
        return acc?.size ?? Vector3.zero;
    }

    static Quaternion RotationFor(string modelName)
    {
        modelName = Nfc(modelName);
        foreach ((string name, Vector3 euler, float _) in ModelAdjustments)
            if (Nfc(name) == modelName) return Quaternion.Euler(euler);

        return Quaternion.identity;
    }

    // 흔함은 기준 키(20)보다 조금 작게 세운다 — 사장님 지시 2026-09-07("조금만 더 작게").
    // 등급이 올라갈수록 존재감이 커지는 게 자연스러워서, 최하위 등급을 기준보다 낮춘다.
    // ⚠️ 이 값은 **프리팹 키**만 바꾼다. 조합판·선택위습 부스에 서 있는 인형은
    // MapGenerator가 자리 폭에 맞춰 따로 재우므로(step*0.9) 여기 영향을 안 받는다.
    const float CommonHeightScale = 0.85f;

    static float HeightScaleFor(string modelName)
    {
        // 모델별 개별 지정이 먼저다 — 상붕카(자전거)처럼 등급 규칙으로 못 맞추는 게 있다.
        foreach ((string name, Vector3 _, float scale) in ModelAdjustments)
            if (Nfc(name) == Nfc(modelName)) return scale;

        if (IsCommonGradeModel(modelName)) return CommonHeightScale;

        return 1f;
    }

    // 이 모델이 흔함 유닛에 붙는가. 파일명이 로스터 이름 그대로인 경우와,
    // ModelOverrides로 이름이 다르게 붙는 경우(예: idle → 흔함_최상호) 둘 다 본다.
    // "안흔함_"은 "흔함_"으로 시작하지 않으므로 여기 안 걸린다.
    static bool IsCommonGradeModel(string modelName)
    {
        // 에셋 경로에서 온 한글은 macOS에서 NFC가 아닐 수 있다(UnitModelPostprocessor 참고).
        // 리터럴과 견주기 전에 맞춘다 — 안 맞추면 흔함 유닛이 조용히 안 줄어든다.
        modelName = modelName.Normalize(System.Text.NormalizationForm.FormC);
        if (modelName.StartsWith("흔함_")) return true;

        foreach ((string model, string unit) in ModelOverrides)
            if (model.Normalize(System.Text.NormalizationForm.FormC) == modelName)
                return unit.StartsWith("흔함_");

        return false;
    }

    // 특정 모델을 특정 유닛에 붙인다.
    // 모델 이름은 확장자를 뺀 파일명, 유닛 이름은 로스터 에셋 이름이다.
    static readonly (string model, string unit)[] ModelOverrides =
    {
        ("idle", "흔함_최상호"),   // 나루토 — Mixamo에서 With Skin으로 받은 파일이라 이름이 idle이다
    };

    /// <summary>
    /// 모델의 머티리얼에 텍스처를 붙인다.
    ///
    /// Mixamo는 OBJ를 리깅해서 FBX로 뱉을 때 텍스처를 안 실어준다 — 뼈대만 온다.
    /// 그래서 임포트하면 새하얗게 나온다. 텍스처 파일은 원래 다운로드에 같이 들어 있으니,
    /// 머티리얼 이름과 파일 이름을 맞춰 다시 이어준다.
    /// </summary>
    /// <summary>
    /// 모델이 아직 없는 자리표시 프리팹(큐브·캡슐)을 캐릭터 키에 맞춘다.
    ///
    /// 레인 한 변이 165인 맵에서 캡슐 2, 큐브 1짜리는 점으로 보인다. 모델이 붙은 유닛만
    /// 20이고 나머지가 그대로면 크기가 뒤죽박죽이 된다.
    /// </summary>
    [MenuItem("Tools/아트/자리표시 크기 맞추기")]
    public static void ScalePlaceholders()
    {
        string report = "";
        foreach (string path in new[] { "Assets/Prefabs/UnitPrefab.prefab", "Assets/Prefabs/MobPrefab.prefab" })
            report += ScalePlaceholder(path);

        AssetDatabase.SaveAssets();
        Debug.Log("[아트] " + report);
        EditorUtility.DisplayDialog(Title, report.TrimStart('\n'), "확인");
    }

    static string ScalePlaceholder(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return $"\n⚠️ 못 찾음: {path}";

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        // 메시가 루트에 있으면 키우려면 루트를 키워야 하는데, 그러면 NavMeshAgent의
        // 발자국까지 같이 커져서 굽힌 NavMesh보다 넓어지고 유닛이 설 자리를 잃는다.
        // 몸을 자식으로 떼어내 그것만 키운다(위습과 같은 방식).
        Transform body = root.transform.Find("몸");
        if (body == null)
        {
            MeshFilter filter = root.GetComponent<MeshFilter>();
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return $"\n{System.IO.Path.GetFileNameWithoutExtension(path)}: 이미 옮겨져 있습니다.";
            }

            GameObject visual = new GameObject("몸", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            visual.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;

            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
            body = visual.transform;
        }

        // 원본 메시 높이(캡슐 2, 큐브 1)에 맞춰 키를 낸다.
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        float rawHeight = bodyRenderer != null ? bodyRenderer.bounds.size.y : 1f;
        if (rawHeight < 0.001f) rawHeight = 1f;

        float height = HeightFor(root);
        float scale = height / rawHeight;
        body.localScale = Vector3.one * scale;
        body.localPosition = new Vector3(0f, height * 0.5f, 0f);   // 발을 바닥에

        float radius = height * 0.18f;

        if (root.TryGetComponent(out CapsuleCollider capsule))
        {
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }
        else if (root.TryGetComponent(out BoxCollider box))
        {
            box.size = new Vector3(radius * 2f, height, radius * 2f);
            box.center = new Vector3(0f, height * 0.5f, 0f);
        }

        // 에이전트는 건드리지 않는다 — 발자국은 굽힌 값(0.5)보다 작게 유지해야 한다.
        if (root.TryGetComponent(out NavMeshAgent agent)) agent.height = height;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        return $"\n{System.IO.Path.GetFileNameWithoutExtension(path)}: 키 {height:F0}으로 맞췄습니다.";
    }

    [MenuItem("Tools/아트/텍스처 연결")]
    public static void LinkTextures()
    {
        List<Texture2D> textures = LoadTexturesUnder("Assets/Art");

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

            // 🔴 텍스처는 **그 모델 폴더 안에서만** 찾는다.
            //
            // 예전엔 Assets/Art 전체에서 찾았다. 그러면 김수빈의 텍스처가 문필환 머티리얼에
            // 붙는다 — 특히 glb에서 뽑은 텍스처는 파일명이 `0.png`~`7.png`라, 아래 부분일치가
            // 이름에 "0"이 든 **모든** 머티리얼에 걸린다(2026-09-07 사장님 「스킨 색이 없고」).
            List<Texture2D> ownTextures = LoadTexturesUnder(System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/'));

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

            string unitName = Nfc(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(modelPath)));

            foreach (AssetImporter.SourceAssetIdentifier slot in slots)
            {
                // 유닛별 강제 지정이 먼저다 — Mixamo가 재질 이름을 통째로 잃은 모델은
                // 이름으로는 영영 못 맞춘다(2026-09-08 신문철: 재질 0개, 텍스처 10장).
                Texture2D texture = ForcedTextureFor(unitName, ownTextures)
                                    ?? MatchTexture(slot.name, ownTextures);
                if (texture == null)
                {
                    unmatched.Add($"{System.IO.Path.GetFileName(modelPath)} / {slot.name}");
                    missed++;
                    continue;
                }

                // 유닛 이름을 앞에 붙인다 — glb 변환 머티리얼은 이름이 `material_0` 꼴이라
                // 여러 유닛이 같은 .mat 하나를 두고 서로 덮어쓴다.
                string owner = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(modelPath));
                string materialPath = $"{MaterialFolder}/{owner}_{slot.name}.mat";
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
    // Mixamo가 재질 정보를 잃은 모델: 유닛 이름 → 이 폴더에서 쓸 텍스처 파일명(확장자 없이).
    // 재질이 하나뿐이라 몸통 텍스처 한 장만 붙는다 — 원본이 여러 재질이었으면 눈·머리 등은
    // 그 한 장으로 덮여 어긋날 수 있다. 제대로 하려면 재질을 살린 채 Mixamo에 다시 올린다.
    static readonly (string unit, string texture)[] ForcedTextures =
    {
        ("안흔함_신문철", "nrt_tex01"),   // 나루토 몸통. 2026-09-08 사장님 「색상이 없고」
    };

    static Texture2D ForcedTextureFor(string unitName, List<Texture2D> textures)
    {
        foreach ((string unit, string texture) in ForcedTextures)
            if (Nfc(unit) == unitName)
                return textures.FirstOrDefault(t => Nfc(t.name) == Nfc(texture));
        return null;
    }

    static Texture2D MatchTexture(string materialName, List<Texture2D> textures)
    {
        string target = materialName.ToLowerInvariant();

        Texture2D exact = textures.FirstOrDefault(t => t.name.ToLowerInvariant() == target);
        if (exact != null) return exact;

        // ⚠️ 부분일치는 **양쪽 다 3글자 이상**일 때만 쓴다.
        //    `0.png`처럼 한 글자짜리 이름은 아무 머티리얼에나 걸려서, 짝이 맞는 것처럼
        //    보이는 엉뚱한 텍스처를 붙인다. 차라리 못 찾았다고 하는 게 낫다.
        Texture2D partial = textures.FirstOrDefault(t =>
        {
            string name = t.name.ToLowerInvariant();
            if (name.Length < 3 || target.Length < 3) return false;
            return target.Contains(name) || name.Contains(target);
        });
        if (partial != null) return partial;

        // 폴더에 텍스처가 하나뿐이면 그걸 쓴다. 이제 폴더 단위로 좁혀 놨으므로
        // 「이 모델의 유일한 텍스처」라는 뜻이고, 예전처럼 남의 유닛 것이 아니다.
        return textures.Count == 1 ? textures[0] : null;
    }

    static List<Texture2D> LoadTexturesUnder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return new List<Texture2D>();

        return AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .Where(t => t != null)
            .ToList();
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
        characters.AddRange(LoadModels(UnitFolder));

        if (monsters.Count == 0 && characters.Count == 0)
        {
            EditorUtility.DisplayDialog(Title,
                $"몸이 있는 모델을 찾지 못했습니다.\n\n{MonsterFolder}, {CharacterFolder} 또는 {UnitFolder}/<유닛이름>/ 에 " +
                "FBX·OBJ 파일을 넣고 다시 실행하세요.\n\n" +
                "Mixamo에서 받으셨다면 하나는 반드시 With Skin이어야 합니다 — " +
                "Without Skin은 동작만 들어 있어 몸이 없습니다.\n\n" +
                "받을 곳은 Docs/reference/CHARACTER_ASSETS.md 에 정리돼 있습니다.", "확인");
            return;
        }

        // 매번 새로 만든다. 남아 있던 프리팹을 지우지 않으면 이름이 겹치지 않는 옛 프리팹이
        // 계속 데이터에 물려 있게 된다(모델을 지웠는데 그 모습이 계속 나오는 경우).
        if (AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.DeleteAsset(GeneratedFolder);
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

        // 지정된 유닛부터 먼저 채운다. 나머지는 남은 모델을 돌려가며 나눠 쓴다.
        HashSet<UnitData> assigned = new HashSet<UnitData>();
        List<string> overrideReport = new List<string>();

        foreach ((string modelName, string unitName) in ModelOverrides)
        {
            GameObject model = models.FirstOrDefault(m => m.name == modelName);
            UnitData unit = units.FirstOrDefault(u => u.name == unitName);
            if (model == null || unit == null) continue;

            unit.prefab = GetOrCreate(cache, template, model, "Unit", ref made);
            EditorUtility.SetDirty(unit);
            assigned.Add(unit);
            overrideReport.Add($"{modelName} → {unit.unitName}");
        }

        // 이름이 같으면 표에 안 적어도 붙는다. 모델을 하나 넣을 때마다 코드를 고쳐야 하면
        // 스킨이 늘어날수록 그 표가 병목이 된다 — 파일명을 규칙으로 삼는다.
        foreach (GameObject model in models)
        {
            if (cache.ContainsKey(model)) continue;   // 위 표에서 이미 붙은 모델

            UnitData unit = ResolveUnitForModel(model.name, units);
            if (unit == null || assigned.Contains(unit)) continue;

            unit.prefab = GetOrCreate(cache, template, model, "Unit", ref made);
            EditorUtility.SetDirty(unit);
            assigned.Add(unit);
            overrideReport.Add($"{model.name} → {unit.unitName} (이름 일치)");
        }

        List<UnitData> rest = units.Where(u => !assigned.Contains(u)).ToList();

        if (FillUnassignedUnits)
        {
            for (int i = 0; i < rest.Count; i++)
            {
                GameObject model = models[i % models.Count];
                rest[i].prefab = GetOrCreate(cache, template, model, "Unit", ref made);
                EditorUtility.SetDirty(rest[i]);
            }
        }
        else
        {
            // 지정 안 된 유닛은 자리표시용 프리팹 그대로 둔다. 모델이 하나뿐인데 다 나눠주면
            // 234종이 전부 같은 얼굴이 되어, 누가 누구인지 구분이 안 된다.
            foreach (UnitData unit in rest)
            {
                if (unit.prefab == template) continue;
                unit.prefab = template;
                EditorUtility.SetDirty(unit);
            }
        }

        return $"\n유닛: 모델 {models.Count}종 → 프리팹 {made}개, " +
               (FillUnassignedUnits
                   ? $"{units.Count}종 전부에 연결했습니다."
                   : $"지정한 {assigned.Count}종에만 연결했습니다(나머지 {rest.Count}종은 자리표시 그대로).") +
               (overrideReport.Count > 0 ? $"\n  지정 연결: {string.Join(", ", overrideReport)}" : "") +
               (FillUnassignedUnits && models.Count < 20
                   ? $"\n  ⚠️ 모델 {models.Count}종을 {units.Count}종이 나눠 씁니다 — " +
                     "파츠·색을 바꿔 변형을 늘리는 건 다음 단계입니다."
                   : "");
    }

    /// <summary>
    /// 모델 파일명으로 유닛을 찾는다. 에셋 이름이 먼저고, 그 다음이 표시 이름이다.
    /// 표시 이름은 겹치는 유닛이 16쌍 있어서(최상호가 넷) <b>하나뿐일 때만</b> 인정한다 —
    /// 아무거나 골라 붙이면 엉뚱한 등급에 얼굴이 들어가고, 아무도 왜인지 모른다.
    /// </summary>
    static UnitData ResolveUnitForModel(string modelName, List<UnitData> units)
    {
        UnitData byAssetName = units.FirstOrDefault(u => u.name == modelName);
        if (byAssetName != null) return byAssetName;

        List<UnitData> byDisplayName = units.Where(u => u.unitName == modelName).ToList();
        if (byDisplayName.Count == 1) return byDisplayName[0];

        if (byDisplayName.Count > 1)
            Debug.LogWarning($"[아트] 모델 '{modelName}'과 이름이 같은 유닛이 {byDisplayName.Count}종입니다 " +
                             $"({string.Join(", ", byDisplayName.Select(u => u.name))}) — " +
                             "어느 것인지 알 수 없어 연결하지 않았습니다. 파일명을 에셋 이름으로 바꾸거나 " +
                             "ArtBinder의 ModelOverrides에 적어주세요.");

        return null;
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
    // ⚠️ Characters(공용 클립 라이브러리)만 본다. Units/ 아래 게임 추출 스킨은 클립을 수십 개씩
    // 품고 있어서(idle_a·run·combo_a…) 거기까지 훑으면 "idle"에 어느 유닛 것이 걸리는지가
    // 실행 순서에 달려 컨트롤러가 돌릴 때마다 달라진다. 공용 컨트롤러는 한 곳에서만 뽑는다.
    static AnimationClip FindClip(string[] words)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { CharacterFolder }))
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

        // 이미 있어도 다시 만든다. 재사용하면 크기·애니메이터 설정을 바꿔도 옛 프리팹이 그대로
        // 쓰여서, 손으로 폴더를 지워야만 반영된다 — 안 지우면 조용히 옛것이 도는 함정이 된다.
        // 한 번 실행 안에서는 cache가 중복 생성을 막는다.
        string path = $"{GeneratedFolder}/{prefix}_{model.name}.prefab";

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = $"{prefix}_{model.name}";

        // 자리표시용 큐브 메시를 걷어내고 그 자리에 모델을 자식으로 붙인다.
        // 컴포넌트(EnemyDummy·WaypointMover·콜라이더 등)는 그대로 둔다 — 그게 이 프리팹의 알맹이다.
        Object.DestroyImmediate(instance.GetComponent<MeshFilter>());
        Object.DestroyImmediate(instance.GetComponent<MeshRenderer>());

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, instance.transform);
        visual.transform.localPosition = Vector3.zero;
        // 크기를 재기 **전에** 돌린다. 돌리면 경계 상자가 바뀌므로, 나중에 돌리면
        // 엉뚱한 축 길이에 키를 맞춰 납작하거나 길쭉해진다.
        visual.transform.localRotation = RotationFor(model.name);
        // 사람형은 뼈로 방향을 재서 자동으로 세운다. 수동 표에 적힌 모델은 그게 우선이다.
        if (RotationFor(model.name) == Quaternion.identity) AutoUpright(visual);
        FitToHeight(instance, visual, HeightScaleFor(model.name));
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

        // 🔴 공용 컨트롤러(Character.controller)의 클립은 **Humanoid**다. Humanoid 클립은
        //    아바타를 거쳐 리타게팅되므로, 아바타가 없는 Generic 리그에는 한 프레임도 안 먹는다.
        //    그런데 예전엔 Generic에도 이 컨트롤러를 물렸다 — 재생해도 바인드 포즈로 굳는다.
        //    (사람 아닌 모델: 재규어 같은 네 발 짐승, 비표준 이름 리그)
        //    그런 모델은 **자기 애니메이션**이 유일한 동작이므로 그걸로 컨트롤러를 만들어 준다.
        AnimatorController controller = animator.avatar != null && animator.avatar.isHuman
            ? AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            : GetOrCreateOwnClipController(visual);

        if (controller != null) animator.runtimeAnimatorController = controller;

        // 이동은 NavMeshAgent·WaypointMover가 시킨다. 애니메이션에 담긴 이동까지 살리면
        // 둘이 겹쳐서 유닛이 두 배로 나아가거나 목적지를 지나쳐 미끄러진다.
        // (Mixamo에서 In Place로 받으면 애초에 안 담기지만, 안 켜고 받았을 때를 막아둔다.)
        animator.applyRootMotion = false;

        if (root.GetComponent<CharacterAnimator>() == null) root.AddComponent<CharacterAnimator>();
    }

    // Generic 리그용 — 모델에 딸려 온 클립 하나를 기본 상태로 두는 컨트롤러를 만든다.
    //
    // 공용 컨트롤러처럼 Idle/Move/Attack을 가르지는 못한다(모델이 클립을 한 벌만 갖고 온다).
    // 하지만 「굳어 서 있는 것」과 「살아서 숨 쉬는 것」의 차이가 크고, CharacterAnimator가
    // 없는 파라미터에 값을 쓰지 않도록 미리 확인하므로 경고도 안 난다.
    // 클립이 하나도 없으면 null을 돌려준다 — 그 경우엔 컨트롤러 없이 그냥 서 있는다.
    static AnimatorController GetOrCreateOwnClipController(GameObject visual)
    {
        string modelPath = AssetDatabase.GetAssetPath(
            PrefabUtility.GetCorrespondingObjectFromSource(visual) ?? (Object)visual);
        if (string.IsNullOrEmpty(modelPath)) return null;

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__"));
        if (clip == null) return null;

        EnsureFolder(GeneratedFolder);

        string unit = System.IO.Path.GetFileNameWithoutExtension(modelPath);
        string path = $"{GeneratedFolder}/{unit}_자체.controller";

        AnimatorController made = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        if (made != null && made.layers.Length > 0 && made.layers[0].stateMachine.states.Length > 0)
            made.layers[0].stateMachine.states[0].state.name = "Idle";

        return made;
    }

    // 모델마다 원본 크기가 제각각이라(1미터짜리도, 100미터짜리도 있다) 그대로 붙이면
    // 어떤 적은 점처럼, 어떤 적은 맵을 덮을 만큼 나온다. 정해진 키에 맞춰 재운다.
    //
    // 보이는 모델만 키우면 안 된다 — 콜라이더가 발치에 남아 클릭이 발끝에서만 먹고
    // 체력바도 발밑에 뜬다. NavMeshAgent의 반지름·높이도 스케일을 안 따라가므로 같이 맞춘다.
    static void FitToHeight(GameObject root, GameObject visual, float heightScale = 1f)
    {
        // 콜라이더·에이전트도 같은 키를 쓴다. 보이는 것만 줄이면 클릭 판정과 체력바가
        // 원래 크기 자리에 남아서, 작아진 모델 위 허공을 눌러야 선택된다.
        float height = HeightFor(root) * heightScale;

        Bounds bounds = MeasureRenderers(visual);
        if (bounds.size.y > 0.001f)
        {
            visual.transform.localScale *= height / bounds.size.y;

            // 스케일을 바꾸면 경계도 바뀐다. 다시 재서 발이 바닥에 닿게 내린다.
            bounds = MeasureRenderers(visual);
            visual.transform.position += Vector3.up * (root.transform.position.y - bounds.min.y);
        }

        float radius = height * 0.18f;   // 사람 비율 어림 — 키의 약 1/5

        if (root.TryGetComponent(out CapsuleCollider capsule))
        {
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }
        else if (root.TryGetComponent(out BoxCollider box))
        {
            box.size = new Vector3(radius * 2f, height, radius * 2f);
            box.center = new Vector3(0f, height * 0.5f, 0f);
        }

        // 높이만 맞추고 **반지름은 건드리지 않는다.** NavMesh는 반지름 0.5로 굽혀 있어서
        // 여기서 3.6(키의 0.18)을 넣으면 에이전트가 굽힌 통로보다 넓어져 설 자리를 잃는다.
        // SetDestination이 조용히 아무것도 안 하는 그 증상이 정확히 이것이었다.
        // 반지름이 작으면 유닛끼리 겹치는데, 원작처럼 겹치는 게 우리가 원하는 동작이다.
        if (root.TryGetComponent(out NavMeshAgent agent)) agent.height = height;
    }

    static Bounds MeasureRenderers(GameObject visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(visual.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
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
            .Where(HasVisibleMesh)
            .ToList();
    }

    // Mixamo에서 Without Skin으로 받은 FBX는 뼈대와 동작만 있고 몸이 없다. 그것까지 모델로 세면
    // 유닛 절반이 투명해지고, 화면에 아무것도 없는데 어디가 잘못됐는지 알 길이 없다.
    // 실제로 그릴 메시가 있는 것만 모델로 친다.
    static bool HasVisibleMesh(GameObject model)
    {
        if (model == null) return false;

        foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skinned.sharedMesh != null) return true;

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null) return true;

        return false;
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
