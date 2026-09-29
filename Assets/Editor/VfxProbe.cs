using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 스킬 이펙트 다듬기 점검(09-29 구현담당1) — gameshot의 call:로 판 안에서 부른다.
///   call:VfxProbe.ShowcaseDefault   PM의 ClaudeCommands.VfxShowcase(표적 넷: 스턴·이감·방깎+마법·적중)를 부른 뒤
///                                   카메라를 **판 시작 기본 높이**(약 374)로 되돌린다 — 실제 게임에서 읽히는지 본다. 두 번 부르는 것까지 같다.
///   call:VfxProbe.RestunShowcase     진열 첫 표적 스턴을 다시 걸어 발구르기를 찍는다(두 번째 ShowcaseDefault 뒤 = 거의 멈춘 시간)
///   call:VfxProbe.ArenaStart         ClaudeCommands.SkillProbeArena(스킬 유닛 전원 교전, 4배속) + 프레임·입자 기록 시작
///   call:VfxProbe.ArenaStartNoVfx    같은 아레나를 이펙트 끄고(SkillVfx.Enabled=false) — 프레임 비교 기준
///   call:VfxProbe.ArenaReport        그 사이 프레임 시간(평균·95%·최대, 실시간 ms) · 살아 있는 이펙트 입자 수 · 화면을 덮은 비율(최대)
/// </summary>
public static class VfxProbe
{
    static float? defaultHeight;

    static object CallClaudeCommand(string name)
    {
        MethodInfo m = typeof(ClaudeCommands).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return m != null ? m.Invoke(null, null) : $"❌ ClaudeCommands.{name} 없음";
    }

    public static string ShowcaseDefault()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        LaneMarker lane = LaneMarker.Get(0);
        if (cam == null || lane == null) return "❌ 카메라·레인 없음";
        if (defaultHeight == null) defaultHeight = cam.transform.position.y;   // 첫 호출 전 = 판 시작 구도 높이

        object result = CallClaudeCommand("VfxShowcase");
        float h = defaultHeight.Value;
        FieldInfo target = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        if (target != null) target.SetValue(cam, h);
        Vector3 p = cam.transform.position; p.y = h; cam.transform.position = p;
        cam.MoveTo(lane.LaneCenter);
        return $"{result} · 카메라 높이 {h:F0}(기본)";
    }

    /// <summary>진열 첫 표적의 스턴을 풀었다 다시 건다 — 걸리는 순간의 발구르기(StunImpact, 0.6초)를 거의 멈춘 시간(두 번째 진열 호출 뒤)에 찍으려고.
    /// 첫 진열 호출 때 걸린 발구르기는 적 소환 프레임이 길어 찍기 전에 끝난다(09-29 VfxDiag: 재생 False).</summary>
    public static string RestunShowcase()
    {
        FieldInfo field = typeof(ClaudeCommands).GetField("showcase", BindingFlags.Static | BindingFlags.NonPublic);
        if (!(field?.GetValue(null) is List<EnemyDummy> list) || list.Count == 0 || list[0] == null) return "❌ 진열 표적 없음(ShowcaseDefault 먼저)";
        list[0].RemoveFreeze();
        list[0].AddFreeze();
        return "스턴 다시 걸음 — 발구르기";
    }

    // ── 아레나 기록 ─────────────────────────────────────────────

    class Recorder : MonoBehaviour
    {
        public readonly List<float> frameMs = new List<float>();
        public int maxParticles;
        public float maxCoverage;
        public int maxSystems;
        int frame;

        void Update()
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            // 입자 세기는 FindObjectsByType라 비싸다 — 5프레임에 한 번만(기록 자체가 프레임을 먹어 결과를 오염시키지 않게).
            if (++frame % 5 != 0) return;
            int particles = 0, systems = 0;
            float covered = 0f;
            Camera cam = Camera.main;
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (!ps.IsAlive(false) || ps.particleCount == 0) continue;
                Transform t = ps.transform;
                bool vfx = t.name.StartsWith("Vfx_") || (t.parent != null && t.parent.name.StartsWith("Vfx_"));
                if (!vfx) continue;
                systems++;
                particles += ps.particleCount;
                if (cam != null) covered += ScreenArea(cam, ps.GetComponent<ParticleSystemRenderer>().bounds);
            }
            maxParticles = Mathf.Max(maxParticles, particles);
            maxSystems = Mathf.Max(maxSystems, systems);
            maxCoverage = Mathf.Max(maxCoverage, covered / (Screen.width * (float)Screen.height));
        }

        // 경계 상자 8꼭짓점을 화면에 투영한 사각형 넓이(겹침은 따로 안 뺀다 — 「덮을 수 있는 최대치」로 읽는다).
        static float ScreenArea(Camera cam, Bounds b)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 s = cam.WorldToScreenPoint(corner);
                if (s.z <= 0f) return 0f;
                min = Vector2.Min(min, s); max = Vector2.Max(max, s);
            }
            min = Vector2.Max(min, Vector2.zero); max = Vector2.Min(max, new Vector2(Screen.width, Screen.height));
            return Mathf.Max(0f, max.x - min.x) * Mathf.Max(0f, max.y - min.y);
        }
    }

    static Recorder recorder;

    public static string ArenaStartNoVfx()
    {
        SkillVfx.Enabled = false;   // 같은 아레나를 이펙트 없이 — 프레임 차이가 이펙트 몫이다
        return ArenaStart() + " · 이펙트 끔";
    }

    public static string ArenaStart()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        object result = CallClaudeCommand("SkillProbeArena");
        // 카메라를 싸움 한가운데(0번 레인 가운데)로 — 가상 마우스 가장자리 밀기로 카메라가 조합표 판까지 흘러간 적이 있다(09-29).
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        LaneMarker lane = LaneMarker.Get(0);
        if (cam != null && lane != null) { cam.enabled = false; cam.MoveTo(lane.LaneCenter); }
        if (recorder != null) Object.Destroy(recorder.gameObject);
        recorder = new GameObject("VfxProbeRecorder").AddComponent<Recorder>();
        return $"{result} · 기록 시작";
    }

    public static string ArenaReport()
    {
        if (recorder == null) return "❌ ArenaStart 먼저";
        List<float> ms = recorder.frameMs.Skip(5).OrderBy(x => x).ToList();   // 처음 몇 프레임은 스폰 자체라 뺀다
        if (ms.Count == 0) return "❌ 기록 없음";
        StringBuilder sb = new StringBuilder();
        sb.Append($"프레임 {ms.Count}개 · 평균 {ms.Average():F1}ms · 95% {ms[(int)(ms.Count * 0.95f)]:F1}ms · 최대 {ms[ms.Count - 1]:F1}ms");
        sb.Append($" · 이펙트 입자 최대 {recorder.maxParticles} · 동시 시스템 최대 {recorder.maxSystems} · 화면 덮음 최대 {recorder.maxCoverage:P0}(겹침 포함 상한)");
        return sb.ToString();
    }
}
