using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 고유 동작 유닛이 누워 보일 때(2026-10-01 임장혁·조현규) — FBX를 장면 밖에서 세워 T자와 Idle 첫 프레임의 루트·몸통 회전·경계 상자를 비교한다.
//   call:MotionAxisProbe.Run
static class MotionAxisProbe
{
    static readonly string[] Names = { "초월_임장혁_AD", "희귀함_조현규", "초월_강주혁_AP", "희귀함_구주호", "특별함_이정범", "영원_최상호" };

    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string n in Names)
        {
            string path = $"Assets/Art/Units/{n}/{n}.fbx";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { sb.AppendLine($"{n}: ❌ 없음"); continue; }
            GameObject go = Object.Instantiate(src);
            go.hideFlags = HideFlags.HideAndDontSave;
            sb.AppendLine($"== {n}  (루트 회전 {go.transform.eulerAngles} 배율 {go.transform.localScale})");
            Report(sb, go, "T자");
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            if (idle == null) sb.AppendLine("   Idle 클립 없음");
            else
            {
                idle.SampleAnimation(go, 0f);
                Report(sb, go, $"Idle 0초(길이 {idle.length:0.00})");
                idle.SampleAnimation(go, idle.length * 0.5f);
                Report(sb, go, "Idle 중간");
            }
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }

    static void Report(StringBuilder sb, GameObject go, string label)
    {
        Vector3 mn = Vector3.one * 1e9f, mx = Vector3.one * -1e9f;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh m = new Mesh(); r.BakeMesh(m);
            Matrix4x4 l2w = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);   // BakeMesh 결과는 렌더러 로컬(배율 제외)
            foreach (Vector3 v in m.vertices) { Vector3 w = l2w.MultiplyPoint3x4(v); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
            Object.DestroyImmediate(m);
        }
        Vector3 sz = mx - mn;
        sb.AppendLine($"   {label}: 몸 크기 가로 {sz.x:0.00} · 키(Y) {sz.y:0.00} · 앞뒤(Z) {sz.z:0.00} · 바닥 {mn.y:0.00}");
    }

    static Bounds Encapsulate(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
}

static class MotionControllerProbe
{
    static bool HasCurve(AnimationClip c, string path, string axis) => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axis)) != null;
    static float Val(AnimationClip c, string path, string axis) { var k = AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axis)); return k == null ? 0f : k.Evaluate(0f); }

    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string n in new[] { "초월_임장혁_AD", "희귀함_조현규", "초월_강주혁_AP" })
        {
            var c = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>($"Assets/Prefabs/Generated/{n}_자체.controller");
            sb.AppendLine($"== {n}: {(c == null ? "컨트롤러 없음" : "")}");
            if (c == null) continue;
            foreach (var st in c.layers[0].stateMachine.states)
                sb.AppendLine($"   상태 {st.state.name} → 클립 {(st.state.motion != null ? st.state.motion.name : "없음")}");
            // 지금 이 클립들이 몸통 뼈 하나를 얼마나 돌리나(Idle 첫·마지막 프레임 Armature 아래 첫 뼈)
            string path = $"Assets/Art/Units/{n}/{n}.fbx";
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(k => k.name == "Idle" || k.name == "Move" || k.name == "Attack"))
            {
                var binds = AnimationUtility.GetCurveBindings(clip);
                var rootRot = binds.Where(b => b.path.Split('/').Length <= 2 && b.propertyName.StartsWith("m_LocalRotation")).Select(b => b.path + ":" + b.propertyName).Distinct().Take(6);
                sb.AppendLine($"   {clip.name}: 곡선 {binds.Length}");
                foreach (string bp in new[] { "Armature", "Armature/mixamorig:Hips" })
                {
                    Quaternion q = new Quaternion(Val(clip, bp, "x"), Val(clip, bp, "y"), Val(clip, bp, "z"), Val(clip, bp, "w"));
                    sb.AppendLine($"      {bp} t=0 회전 {q.eulerAngles} (곡선 {(HasCurve(clip, bp, "x") ? "있음" : "없음")}) · 위치 곡선 {(AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(bp, typeof(Transform), "m_LocalPosition.y")) != null ? "있음" : "없음")}");
                }
            }
        }
        return sb.ToString();
    }
}

static class MotionLiveProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (UnitIdentity id in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None))
        {
            if (!id.name.StartsWith("Unit_")) continue;
            Animator a = id.GetComponentInChildren<Animator>();
            sb.AppendLine($"{id.name}: 월드 회전 {id.transform.eulerAngles} · Animator {(a == null ? "없음" : $"{a.runtimeAnimatorController?.name} 루트모션 {a.applyRootMotion} 아바타 {(a.avatar == null ? "없음" : a.avatar.isHuman ? "Human" : "Generic")} 상태 {(a.GetCurrentAnimatorClipInfo(0).Length > 0 ? a.GetCurrentAnimatorClipInfo(0)[0].clip.name : "?")}")}");
            foreach (Transform t in id.GetComponentsInChildren<Transform>().Where(x => x.name == "몸" || x.name == "Armature" || x.name == "mixamorig:Hips" || x.name.StartsWith("mixamorig:Spine") || x.name == "mixamorig:Head"))
                sb.AppendLine($"   {t.name}: 월드 회전 {t.eulerAngles} 월드 위치 {t.position - id.transform.position} 배율 {t.lossyScale}");
            Vector3 up = Vector3.zero;
            Transform head = id.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "mixamorig:Head");
            Transform hips = id.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "mixamorig:Hips");
            if (head != null && hips != null) sb.AppendLine($"   머리-엉덩이 방향 {(head.position - hips.position).normalized} (위가 (0,1,0))");
        }
        return sb.ToString();
    }
}

static class MotionArmatureProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string n in new[] { "초월_임장혁_AD", "희귀함_조현규", "초월_강주혁_AP", "영원_최상호" })
        {
            string path = $"Assets/Art/Units/{n}/{n}.fbx";
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(k => k.name == "Idle" || k.name == "Attack"))
            {
                sb.AppendLine($"== {n} {clip.name} 길이 {clip.length:0.00}");
                foreach (string prop in new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" })
                {
                    var cv = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("Armature", typeof(Transform), prop));
                    if (cv == null) { sb.AppendLine($"   {prop}: 곡선 없음"); continue; }
                    var vals = Enumerable.Range(0, 6).Select(i => cv.Evaluate(clip.length * i / 5f).ToString("0.000"));
                    sb.AppendLine($"   {prop}: 키 {cv.length}개 · t=0..끝 6점 {string.Join(" ", vals)}");
                }
            }
        }
        return sb.ToString();
    }
}

// 상시 오라 성능 비교(2026-10-01) — 초월·히든 60기를 세우고 오라 켬/끔/켬 각 3초의 평균 프레임 시간을 잰다.
//   gameshot x.png 1 960x540 call:AuraPerf.Start wait:14 call:AuraPerf.Report
static class AuraPerf
{
    static readonly System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>();

    class Runner : MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
            LaneMarker lane = LaneMarker.Get(0);
            var datas = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
                .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && d.prefab != null && (d.grade == UnitGrade.Transcendent || d.grade == UnitGrade.Hidden)).ToList();
            for (int i = 0; i < 60; i++)
                spawner.Spawn(datas[i % datas.Count], lane.LaneCenter + new Vector3((i % 10 - 5) * 14f, 0f, (i / 10 - 3) * 14f), 0);
            yield return new WaitForSeconds(1.5f);   // 설치기가 붙을 시간
            foreach (bool on in new[] { true, false, true })
            {
                UnitSphereArt.Enabled = on;
                yield return new WaitForSeconds(0.5f);
                float t = 0f; int n = 0; float end = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < end) { t += Time.unscaledDeltaTime; n++; yield return null; }
                result.Add($"오라 {(on ? "켬" : "끔")}: 평균 {t / n * 1000f:0.0}ms ({n / t:0.0}fps) · 붙은 유닛 {UnitSphereArt.AttachedUnits} · 부품 {UnitSphereArt.AttachedParts}");
            }
            UnitSphereArt.Enabled = true;
        }
    }

    static string Start()
    {
        result.Clear();
        new GameObject("[AuraPerf]").AddComponent<Runner>();
        return "시작(60기)";
    }

    static string Report() => string.Join("\n", result);
}
