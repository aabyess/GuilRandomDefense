using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 「적이 근처에 오면」(SkillTriggerType.OnEnemyEnterRange) 실측(2026-09-30, 구조 칸 백로그 13번).
// 로스터 넷을 근접 스킬만 남긴 복제(평타 0.001)로 세우고, 감지 반경 안에 PV 100 표적 21 + PV 200 보스 1, 밖(반경 × 1.2)에 하나.
// 낸다: 스킬별 시전 수(보스 1 · 확률 갈래 ≈ 21 × 확률 · 한 적에 한 번) · 표식이 남은 표적 수(안 22 / 밖 0) · 깎인 체력 비 · 방어.
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:EnterProbe.Arena wait:20 call:EnterProbe.Report
static class EnterProbe
{
    static readonly string[] Rosters = { "제한_전법규", "전설적인_백기현", "제한_김민규", "영원_조세민" };
    const int Inside = 21;

    class Slot { public UnitData data; public string mark; public List<EnemyDummy> inside = new List<EnemyDummy>(); public EnemyDummy boss, outside; public float bossHp0, outsideHp0; public List<float> hp0 = new List<float>(); public int stunnedSeen; }
    static readonly List<Slot> slots = new List<Slot>();

    static EnemyDummy Target(EnemyData enemy, Vector3 at)
    {
        GameObject go = Object.Instantiate(enemy.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
        dummy.Initialize(enemy, 1e9f / Mathf.Max(1f, enemy.hp));
        dummy.SetLane(-1);
        return dummy;
    }

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData normal = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        if (spawner == null || lane == null || normal == null || boss == null) return "❌ 준비 실패";
        slots.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        float k = 1f / WorldScale.Value;
        for (int u = 0; u < Rosters.Length; u++)
        {
            UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Rosters[u]}.asset");
            if (source == null) continue;
            var enter = new List<SkillData>();
            for (int i = 0; i < source.SkillCount; i++)
            {
                SkillData s = source.SkillAt(i);
                if (s != null && s.triggerType == SkillTriggerType.OnEnemyEnterRange) enter.Add(s);
            }
            if (enter.Count == 0) continue;
            UnitData data = Object.Instantiate(source);
            data.name = source.name; data.attackPower = 0.001f; data.critChance = 0f; data.skill = null; data.skills = enter;
            data.attackSplashRadius = 0f; data.attackCleaveFactor = 0f;
            float range = enter[0].levels[0].enterRange;
            var slot = new Slot { data = data, mark = enter[0].levels[0].forbiddenTargetBuffId };
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 90f, 0f) * Vector3.forward * 3000f;
            for (int i = 0; i < Inside; i++)
            {
                EnemyDummy t = Target(normal, home + Quaternion.Euler(0f, i * 360f / (Inside + 1), 0f) * Vector3.forward * range * 0.8f * k);
                slot.inside.Add(t); slot.hp0.Add(t.Hp);
            }
            slot.boss = Target(boss, home + Quaternion.Euler(0f, Inside * 360f / (Inside + 1), 0f) * Vector3.forward * range * 0.8f * k);
            slot.bossHp0 = slot.boss.Hp;
            slot.outside = Target(normal, home + Vector3.forward * range * 1.2f * k);
            slot.outsideHp0 = slot.outside.Hp;
            spawner.Spawn(data, home, 0);
            slots.Add(slot);
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
        return $"로스터 {slots.Count} · 안쪽 표적 {Inside} + 보스 1 · 바깥 1";
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        foreach (Slot s in slots) s.stunnedSeen = Mathf.Max(s.stunnedSeen, s.inside.Count(t => t != null && t.IsStunned) + (s.boss != null && s.boss.IsStunned ? 1 : 0));
    }

    static string Report()
    {
        EditorApplication.update -= Sample;
        var sb = new StringBuilder();
        foreach (Slot s in slots)
        {
            int marked = s.inside.Count(t => t != null && t.HasBuff(s.mark)) + (s.boss != null && s.boss.HasBuff(s.mark) ? 1 : 0);
            int hurt = s.inside.Where((t, i) => t != null && s.hp0[i] - t.Hp > 1f).Count();
            sb.Append($"\n{s.data.name}: 표식 {marked}/{Inside + 1} · 바깥 표식 {(s.outside != null && s.outside.HasBuff(s.mark) ? 1 : 0)}·피해 {(s.outside != null ? s.outsideHp0 - s.outside.Hp : -1f):0}"
                      + $" · 안쪽 일반 중 깎인 것 {hurt} · 보스 깎임 {(s.boss != null ? s.bossHp0 - s.boss.Hp : -1f):0}({(s.boss != null ? (s.bossHp0 - s.boss.Hp) / s.bossHp0 : 0f):P2})"
                      + $" · 일반 깎임 최대 {s.inside.Select((t, i) => t == null ? 0f : (s.hp0[i] - t.Hp) / s.hp0[i]).Max():P2}"
                      + $" · 같이 스턴된 최대 수 {s.stunnedSeen}"
                      + $" · 방어(일반 첫 표적 {(s.inside[0] != null ? s.inside[0].EffectiveArmor : 0f):0.0} · 보스 {(s.boss != null ? s.boss.EffectiveArmor : 0f):0.0})");
            foreach (var g in SkillTelemetry.CastLog.Where(e => e.unit == s.data).GroupBy(e => e.skill))
                sb.Append($"\n    「{g.Key}」 시전 {g.Count()}");
        }
        return sb.ToString();
    }
}
