using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오라 전용 이펙트의 켜짐·꺼짐 관리(10-20 안 B, 사장님 「오라 전용 모양」) — UnitAttacker.UpdateAuraTick이 오라 적용·해제 지점에서 부른다.
///  · 소스(오라를 내는 유닛): 발밑 얇은 광 고리(등급색 25% 밝힘) + 느린 상승 입자 + 실제 범위 바깥 고리. 한 유닛이 오라가 여럿이어도 고리는 하나(범위는 가장 큰 것).
///  · 받는 쪽(범위 안 아군·적): 발밑 작은 점 하나 — 버프 초록·디버프 보라. 같은 대상이 여러 오라를 받으면 참조 수로 센다(디버프가 하나라도 있으면 보라).
/// 멀티: 호스트 실물의 상태를 NetEntity가 [Networked]로 싣고(<see cref="SourceState"/>·<see cref="ReceiverState"/>), 클라 겉모습은 <see cref="SyncSource"/>·<see cref="SyncReceiver"/>로 같은 이펙트를 붙였다 뗀다.
/// 입자: 오라 한 개당 약 10개 이하, 받는 점은 1개 — 풀링은 SkillVfx.Attach(종류별 풀·화면 밖 입자는 유니티가 컬링).
/// </summary>
public static class AuraVfx
{
    public static readonly Color BuffColor = new Color(0.38f, 0.88f, 0.48f), DebuffColor = new Color(0.70f, 0.42f, 1f);

    class Source { public GameObject go; public UnitGrade grade; public float range; }
    class Receiver { public GameObject go; public int buffs, debuffs; public bool shownDebuff; }

    static readonly Dictionary<Transform, Source> sources = new Dictionary<Transform, Source>();
    static readonly Dictionary<Transform, Receiver> receivers = new Dictionary<Transform, Receiver>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { sources.Clear(); receivers.Clear(); }

    /// <summary>등급색을 25% 밝힌 값(안 B 오라 고리색).</summary>
    public static Color SourceTint(UnitGrade grade)
    {
        Color c = grade.Color();
        return new Color(Mathf.Lerp(c.r, 1f, 0.25f), Mathf.Lerp(c.g, 1f, 0.25f), Mathf.Lerp(c.b, 1f, 0.25f), 1f);
    }

    // ── 소스 ───────────────────────────────────────────────────────────────────────────────
    /// <summary>오라 소스 켜기/갱신(range = 월드 단위 반지름, 0이면 바깥 고리 없음) · off면 끈다.</summary>
    public static void SetSource(Transform unit, UnitGrade grade, float range, bool on)
    {
        if (unit == null) return;
        if (!on) { SourceOff(unit); return; }
        if (sources.TryGetValue(unit, out Source s))
        {
            if (s.go != null && Mathf.Abs(s.range - range) < 0.5f && s.grade == grade) return;   // 그대로
            SkillVfx.Stop(s.go);
        }
        GameObject go = SkillVfx.Attach(SkillVfx.Kind.AuraSource, unit, 0.5f, SourceTint(grade), range);
        sources[unit] = new Source { go = go, grade = grade, range = range };
    }

    static void SourceOff(Transform unit)
    {
        if (sources.TryGetValue(unit, out Source s)) { SkillVfx.Stop(s.go); sources.Remove(unit); }
    }

    /// <summary>호스트 NetEntity용: 이 실물이 오라를 내고 있나(범위 월드 단위) · 등급.</summary>
    public static bool SourceState(Transform unit, out UnitGrade grade, out float range)
    {
        grade = UnitGrade.Common; range = 0f;
        if (unit == null || !sources.TryGetValue(unit, out Source s)) return false;
        grade = s.grade; range = s.range; return true;
    }

    // ── 받는 쪽 ────────────────────────────────────────────────────────────────────────────
    public static void ReceiverOn(Transform target, bool debuff)
    {
        if (target == null) return;
        if (!receivers.TryGetValue(target, out Receiver r)) receivers[target] = r = new Receiver();
        if (debuff) r.debuffs++; else r.buffs++;
        Refresh(target, r);
    }

    public static void ReceiverOff(Transform target, bool debuff)
    {
        if (target == null || !receivers.TryGetValue(target, out Receiver r)) return;
        if (debuff) r.debuffs = Mathf.Max(0, r.debuffs - 1); else r.buffs = Mathf.Max(0, r.buffs - 1);
        Refresh(target, r);
    }

    static void Refresh(Transform target, Receiver r)
    {
        if (r.buffs + r.debuffs <= 0) { SkillVfx.Stop(r.go); receivers.Remove(target); return; }
        bool debuff = r.debuffs > 0;
        if (r.go != null) { if (r.shownDebuff == debuff) return; SkillVfx.Stop(r.go); }
        r.go = SkillVfx.Attach(SkillVfx.Kind.AuraReceiver, target, 1f, debuff ? DebuffColor : BuffColor);
        r.shownDebuff = debuff;
    }

    /// <summary>호스트 NetEntity용: 0 없음 · 1 버프 · 2 디버프.</summary>
    public static int ReceiverState(Transform target)
    {
        if (target == null || !receivers.TryGetValue(target, out Receiver r)) return 0;
        return r.debuffs > 0 ? 2 : r.buffs > 0 ? 1 : 0;
    }

    // ── 멀티 클라 겉모습 ───────────────────────────────────────────────────────────────────
    public static GameObject SyncSource(GameObject attached, Transform visual, bool on, UnitGrade grade, float range, ref float lastRange)
    {
        if (on && visual != null && (attached == null || Mathf.Abs(lastRange - range) >= 0.5f))
        {
            if (attached != null) SkillVfx.Stop(attached);
            lastRange = range;
            return SkillVfx.Attach(SkillVfx.Kind.AuraSource, visual, 0.5f, SourceTint(grade), range);
        }
        if (!on && attached != null) { SkillVfx.Stop(attached); return null; }
        return attached;
    }

    public static GameObject SyncReceiver(GameObject attached, Transform visual, int state, ref int lastState)
    {
        if (state != 0 && visual != null && (attached == null || lastState != state))
        {
            if (attached != null) SkillVfx.Stop(attached);
            lastState = state;
            return SkillVfx.Attach(SkillVfx.Kind.AuraReceiver, visual, 1f, state == 2 ? DebuffColor : BuffColor);
        }
        if (state == 0 && attached != null) { SkillVfx.Stop(attached); lastState = 0; return null; }
        return attached;
    }
}
