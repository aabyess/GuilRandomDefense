using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 보스 %HP 게이트 제거(260424bc) 실측 — 보스에게 닿는 %체력 효과(조건 없음·PV==200·PV≥200)를 가진
// 로스터 유닛마다 보스 한 마리씩을 따로 세우고, 피해를 「스킬%HP」·「스킬」·「평타」·「치명」 채널로 나눠 잰다.
// 보스는 원래 체력 그대로, 죽으면 그 자리에 새로 세운다(체력 배율을 곱하면 %HP만 부풀어 비율이 왜곡된다 — PM).
// 게이트 제거 전에는 보스가 %체력 피해를 전혀 안 받았다(takesPercentDamage=false → 0).
// 실행 중에만 쓰는 EnemyData 복제본에서 isBoss만 끈다 — 죽을 때 라운드보스 보상 경로를 안 타게(PV·방어·체력은 그대로).
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:BossPercentProbe.ArenaR50 wait:60 call:BossPercentProbe.Report
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:BossPercentProbe.ArenaR10 wait:20 call:BossPercentProbe.Report
static class BossPercentProbe
{
    const float RingRadius = 2000f;   // 이웃 간격 ≈ 2π·2000/38 ≈ 330 > 최대 사거리 240(월드 단위)

    class Slot { public EnemyDummy boss; public Vector3 home; public int kills; }

    static readonly Dictionary<UnitData, Slot> slots = new Dictionary<UnitData, Slot>();
    static EnemyData bossData;
    static float startTime;

    static string ArenaR50() => Arena("Assets/Data/Enemies/Enemy_R50_이태훈.asset");
    static string ArenaR10() => Arena("Assets/Data/Enemies/Enemy_R10_주영호.asset");

    static string Arena(string bossPath)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData source = AssetDatabase.LoadAssetAtPath<EnemyData>(bossPath);
        if (spawner == null || lane == null || source == null || source.prefab == null) return "❌ UnitSpawner·레인·보스 데이터 없음";
        bossData = Object.Instantiate(source);
        bossData.isBoss = false;

        List<UnitData> units = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(u => u != null && u.prefab != null && HasBossReachablePercentSkill(u))
            .OrderBy(u => u.name).ToList();

        slots.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        Vector3 c = lane.LaneCenter;
        int spawned = 0;
        for (int i = 0; i < units.Count; i++)
        {
            Vector3 home = c + Quaternion.Euler(0f, i * 360f / units.Count, 0f) * Vector3.forward * RingRadius;
            var slot = new Slot { home = home, boss = SpawnBoss(home) };
            slots[units[i]] = slot;
            if (spawner.Spawn(units[i], home + Vector3.back * 40f, 0) != null) spawned++;
        }
        SetDeathCount(false);   // 탐침은 레인 적을 안 막는다 — 판 도중 데스카운트 0(게임오버)이 나지 않게(PM, 09-30)
        startTime = Time.time;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        Time.timeScale = 2f;
        return $"{source.name}(hp {source.hp}, PV {source.pointValue}, 방어 {source.armor}) · 유닛 {spawned}/{units.Count} · 원래 체력·죽으면 다시 세움 · 2배속";
    }

    static EnemyDummy SpawnBoss(Vector3 at)
    {
        GameObject go = Object.Instantiate(bossData.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
        dummy.Initialize(bossData, 1f);
        dummy.SetLane(-1);
        return dummy;
    }

    static void Watch()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Watch; return; }
        foreach (Slot s in slots.Values)
        {
            if (s.boss != null && !s.boss.IsDead) continue;
            s.kills++;
            s.boss = SpawnBoss(s.home);
        }
    }

    // 보스(PV 200)에게 닿는 %체력 효과: 조건 없음, PV==200, PV≥(≤200).
    static bool HasBossReachablePercentSkill(UnitData u)
    {
        for (int s = 0; s < u.SkillCount; s++)
        {
            SkillData skill = u.SkillAt(s);
            if (skill == null) continue;
            foreach (SkillLevel lv in skill.levels)
                foreach (SkillEffect e in lv.effects)
                {
                    if (e.kind != SkillEffectKind.Damage) continue;
                    if (e.basis != SkillEffectBasis.TargetMaxHpPercent && e.basis != SkillEffectBasis.TargetCurrentHpPercent
                        && e.basis != SkillEffectBasis.TargetMissingHpPercent) continue;
                    if (e.targetCondition == SkillEffectTargetCondition.None
                        || (e.targetCondition == SkillEffectTargetCondition.TargetPointValueEqual && e.targetConditionValue == 200f)
                        || (e.targetCondition == SkillEffectTargetCondition.TargetPointValueAtLeast && e.targetConditionValue <= 200f))
                        return true;
                }
        }
        return false;
    }

    static readonly System.Reflection.FieldInfo DeathCountField =
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    static void SetDeathCount(bool enabled)
    {
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        if (rm != null && DeathCountField != null) DeathCountField.SetValue(rm, enabled);
    }

    static string Report()
    {
        EditorApplication.update -= Watch;
        SetDeathCount(true);
        Time.timeScale = 1f;
        float elapsed = Mathf.Max(0.01f, Time.time - startTime);
        float maxHp = bossData != null ? bossData.hp : 1f;
        var sb = new StringBuilder($"\n{(bossData != null ? bossData.name : "?")} hp {maxHp} · 게임 시간 {elapsed:0.0}초 · 비율 = 그 유닛 총피해 중 몫 · 처치시간 = 총피해 속도로 한 마리");
        foreach (var kv in slots.OrderByDescending(kv => SkillTelemetry.DamageOf(kv.Key, "스킬%HP")))
        {
            UnitData u = kv.Key;
            float pct = SkillTelemetry.DamageOf(u, "스킬%HP");
            float skill = SkillTelemetry.DamageOf(u, "스킬");
            float basic = SkillTelemetry.DamageOf(u, "평타") + SkillTelemetry.DamageOf(u, "치명");
            float total = pct + skill + basic;
            string kill = total > 0f ? $"{maxHp / (total / elapsed):0.0}초" : "—";
            sb.Append($"\n{u.name}: %HP {Share(pct, total)} · 스킬 전체 {Share(pct + skill, total)} · 평타 {Share(basic, total)} · 한 마리 {kill} · 처치 {kv.Value.kills} · %HP {pct / maxHp * 60f / elapsed:0.00}마리분/분");
        }
        return sb.ToString();
    }

    static string Share(float part, float total) => total > 0f ? $"{part / total * 100f:0}%" : "—";
}
