using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 이펙트 개편(안 B) 확인용 촬영 도구 — gameshot의 call:로 부른다(구현담당2, 10-19).
///   call:VfxStyleProbe.Close     ShowcaseDefault 뒤 진열 첫 표적(스턴) 위로 카메라를 가까이(높이 90) — 반짝이·꼬리가 읽히는지 본다
///   call:VfxStyleProbe.CloseN    같은 방식으로 n번째 표적(Close2·Close3·Close4 = 이감·방깎·적중)
/// </summary>
public static class VfxStyleProbe
{
    static string CloseOn(int index, float height)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        object r = typeof(VfxProbe).GetMethod("ShowcaseDefault", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        FieldInfo field = typeof(ClaudeCommands).GetField("showcase", BindingFlags.Static | BindingFlags.NonPublic);
        if (!(field?.GetValue(null) is List<EnemyDummy> list) || list.Count <= index || list[index] == null) return "❌ 진열 표적 없음 " + r;
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        FieldInfo target = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        target?.SetValue(cam, height);
        Vector3 p = cam.transform.position; p.y = height; cam.transform.position = p;   // 높이를 먼저 — MoveTo가 현재 높이로 시선 오프셋을 계산한다
        cam.MoveTo(list[index].transform.position);
        return $"가까이 {index}번 표적 높이 {height}";
    }
    /// <summary>실제 레인 적(H≈22.5)에 스턴을 건다 — 라운드가 시작돼 적이 나온 뒤(gameshot wait:25)에 부른다. 카메라는 첫 적 위 기본 높이 근처.</summary>
    public static string StunLane()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        int n = 0; Vector3 at = Vector3.zero;
        foreach (EnemyDummy e in new List<EnemyDummy>(EnemyDummy.Active))
        {
            if (e == null || e.IsBoss || e.LaneIndex < 0) continue;
            e.AddFreeze();
            if (n == 0) at = e.transform.position;
            if (++n >= 3) break;
        }
        if (n == 0) return "❌ 레인 적 없음(라운드 시작 뒤에)";
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam != null) cam.MoveTo(at);
        return $"레인 적 {n}기 스턴";
    }

    class Repeater : MonoBehaviour
    {
        public System.Action fire; float next;
        void Update() { if (Time.unscaledTime >= next) { next = Time.unscaledTime + 0.9f; fire?.Invoke(); } }
    }

    /// <summary>Hit·SpellHit·Smash·StunImpact 버스트를 카메라 앞 가로 한 줄로 0.9초마다 되풀이(+Buff는 넷째 진열 표적에 붙임) — 찍을 때 wait 짧은 간격 snap으로 한 장 고른다.</summary>
    public static string BurstKinds()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        VfxProbe.ShowcaseDefault();
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        FieldInfo field = typeof(ClaudeCommands).GetField("showcase", BindingFlags.Static | BindingFlags.NonPublic);
        if (!(field?.GetValue(null) is List<EnemyDummy> list) || list.Count < 4) return "❌ 진열 표적 없음";
        Vector3 c = list[0].transform.position + new Vector3(60f, 0f, -70f);
        var go = new GameObject("VfxStyleRepeater").AddComponent<Repeater>();
        go.fire = () =>
        {
            SkillVfx.Burst(SkillVfx.Kind.Hit, c + new Vector3(-90f, 10f, 0f));
            SkillVfx.Burst(SkillVfx.Kind.SpellHit, c + new Vector3(-30f, 10f, 0f));
            SkillVfx.Burst(SkillVfx.Kind.Smash, c + new Vector3(30f, 0.5f, 0f));
            SkillVfx.Burst(SkillVfx.Kind.StunImpact, c + new Vector3(90f, 0.5f, 0f));
            SkillVfx.Burst(SkillVfx.Kind.ArmorBreak, c + new Vector3(150f, 10f, 0f));
            SkillVfx.Burst(SkillVfx.Kind.Buff, c + new Vector3(-150f, 0.5f, 0f));
        };
        SkillVfx.Attach(SkillVfx.Kind.Buff, list[3].transform, 14f);
        if (cam != null) cam.MoveTo(c);
        return "버스트 반복 시작";
    }

    public static string Close() => CloseOn(0, 90f);
    public static string Close2() => CloseOn(1, 90f);
    public static string Close3() => CloseOn(2, 90f);
    public static string Close4() => CloseOn(3, 90f);
}
