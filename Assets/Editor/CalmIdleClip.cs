using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 공용 대기 클립에서 오른팔 몸짓을 걷어 낸 「잔잔한 대기」를 만든다(사장님 2026-09-30 「서 있는 모습이 오른팔을 자꾸 휘젓던데」).
///
/// 원인 — Assets/Art/Characters/idle.fbx(Mixamo, 330프레임 = 11초)는 오른손을 **60cm씩 두 번** 들어 올리는 몸짓이 든 대기다
/// (Blender 실측: 엉덩이 기준 오른손 이동 최대 60cm, 프레임 19~127 · 157~301. 왼손은 최대 10cm, 머리 7cm).
/// 몸짓이 없는 구간은 1초 남짓 두 토막뿐이라 잘라 쓰면 숨쉬기까지 사라진다.
///
/// 방법 — 클립을 .anim으로 복제하고, **오른팔 근육 곡선만** 「오른팔의 첫 프레임 값 + 왼팔 곡선의 흔들림」으로 바꾼다.
/// Humanoid 근육 공간에선 왼쪽·오른쪽 같은 이름 근육이 서로 거울이라, 왼팔의 잔잔한 흔들림을 그대로 옮기면
/// 오른팔도 같은 크기로 자연스럽게 흔들린다(상수로 얼리면 팔만 굳어 보인다). 몸통·머리·다리·숨쉬기는 원본 그대로.
///
/// 결과 Assets/Art/Characters/idle_calm.anim — ArtBinder.FindClip이 대기 클립으로 이것을 먼저 집는다(CalmIdleWords).
/// 원본 idle.fbx는 그대로 둔다(Blender 검수 도구가 같은 파일을 쓴다 — 더 험한 동작으로 검사하는 쪽이 안전하다).
/// 브리지: `call CalmIdleClip.Build` → 그 뒤 「모델 배선」이 Character.controller를 다시 엮는다.
/// </summary>
public static class CalmIdleClip
{
    const string SourcePath = "Assets/Art/Characters/idle.fbx";
    public const string OutputPath = "Assets/Art/Characters/idle_calm.anim";

    // 오른팔에 딸린 Humanoid 근육·손가락·IK 목표 곡선(이름 앞머리). 짝은 Right → Left로 바꾼 이름이다.
    static readonly string[] RightArmPrefixes = { "Right Shoulder", "Right Arm", "Right Forearm", "Right Hand", "RightHand." };

    [MenuItem("Tools/아트/잔잔한 대기 클립 만들기")]
    public static string Build()
    {
        AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (source == null) return $"❌ {SourcePath}에서 클립을 못 찾았습니다.";

        AnimationClip calm = Object.Instantiate(source);
        calm.name = "idle_calm";

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(calm);
        int replaced = 0, frozen = 0;
        float biggestBefore = 0f, biggestAfter = 0f;
        string biggestName = "";
        foreach (EditorCurveBinding binding in bindings)
        {
            string name = binding.propertyName;
            if (!RightArmPrefixes.Any(p => name.StartsWith(p))) continue;

            AnimationCurve right = AnimationUtility.GetEditorCurve(calm, binding);
            float before = Swing(right);
            float rest = right.Evaluate(0f);

            EditorCurveBinding leftBinding = binding;
            leftBinding.propertyName = "Left" + name.Substring("Right".Length);
            AnimationCurve left = AnimationUtility.GetEditorCurve(source, leftBinding);

            AnimationCurve made;
            if (left != null && left.length > 0)
            {
                float leftRest = left.Evaluate(0f);
                Keyframe[] keys = left.keys;
                for (int i = 0; i < keys.Length; i++) keys[i].value = rest + (keys[i].value - leftRest);
                made = new AnimationCurve(keys);
                replaced++;
            }
            else
            {
                made = AnimationCurve.Constant(0f, source.length, rest);
                frozen++;
            }
            AnimationUtility.SetEditorCurve(calm, binding, made);

            float after = Swing(made);
            if (before > biggestBefore) { biggestBefore = before; biggestAfter = after; biggestName = name; }
        }
        if (replaced + frozen == 0)
            return "❌ 오른팔 근육 곡선을 하나도 못 찾았습니다 — 클립이 Humanoid로 임포트됐는지 확인. 곡선 이름 예: " +
                   string.Join(", ", bindings.Take(8).Select(b => b.propertyName));

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(calm, settings);

        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(calm, existing);   // GUID를 지킨다 — 컨트롤러 참조가 안 끊긴다
            existing.name = "idle_calm";
            EditorUtility.SetDirty(existing);
        }
        else AssetDatabase.CreateAsset(calm, OutputPath);
        AssetDatabase.SaveAssets();

        return $"✅ {OutputPath} — 곡선 {bindings.Length}개 중 오른팔 {replaced}개를 왼팔 흔들림으로, {frozen}개를 첫 값으로 고정. " +
               $"길이 {source.length:0.00}초 · 가장 크게 흔들리던 「{biggestName}」 폭 {biggestBefore:0.000} → {biggestAfter:0.000}";
    }

    /// <summary>원본과 잔잔한 판을 사람형 프리팹에 입혀 손이 엉덩이 기준으로 얼마나 움직이는지 잰다(키 대비 %). 브리지 `call CalmIdleClip.Measure`.</summary>
    public static string Measure()
    {
        AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        AnimationClip calm = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputPath);
        string prefabPath = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Generated" }).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Animator>() is Animator a && a.isHuman);
        if (source == null || calm == null || prefabPath == null) return "❌ 클립 또는 사람형 프리팹을 못 찾았습니다.";

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Animator animator = go.GetComponentInChildren<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand), left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            string report = $"프리팹 {System.IO.Path.GetFileNameWithoutExtension(prefabPath)}";
            foreach (AnimationClip clip in new[] { source, calm })
            {
                clip.SampleAnimation(animator.gameObject, 0f);
                float height = Mathf.Max(0.0001f, (head.position - hips.position).magnitude * 2f);
                Vector3 r0 = hips.InverseTransformPoint(right.position), l0 = hips.InverseTransformPoint(left.position);
                float rMax = 0f, lMax = 0f;
                for (float t = 0f; t <= clip.length; t += 0.2f)
                {
                    clip.SampleAnimation(animator.gameObject, t);
                    rMax = Mathf.Max(rMax, (hips.TransformVector(hips.InverseTransformPoint(right.position) - r0)).magnitude);
                    lMax = Mathf.Max(lMax, (hips.TransformVector(hips.InverseTransformPoint(left.position) - l0)).magnitude);
                }
                report += $" | {clip.name}: 오른손 최대 {rMax / height * 100f:0.0}% · 왼손 최대 {lMax / height * 100f:0.0}% (몸통 길이×2 대비)";
            }
            return report;
        }
        finally { Object.DestroyImmediate(go); }
    }

    static float Swing(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0) return 0f;
        float min = float.MaxValue, max = float.MinValue;
        foreach (Keyframe key in curve.keys) { min = Mathf.Min(min, key.value); max = Mathf.Max(max, key.value); }
        return max - min;
    }
}
