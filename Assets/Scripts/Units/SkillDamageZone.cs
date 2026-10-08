using System.Collections.Generic;
using UnityEngine;

// 주기 피해 지대(2026-09-30, 구조 칸 백로그 8번 — SkillEffect.zoneTickInterval 주석).
// 원작: 트리거가 만든 더미(수명 = 체력 ÷ 음수 재생)가 ANpi 영구 이몰레이션으로 주변 적을 틱마다 태운다.
// 지대는 시전자와 따로 산다(원작 더미처럼) — 시전자가 조합·판매로 사라져도 수명까지 돈다. 그래서 UnitAttacker 코루틴이 아니라
// 여기 러너 하나가 전부 센다.
public class SkillDamageZone : MonoBehaviour
{
    class Zone
    {
        public Vector3 center;
        public float radius;
        public SkillEffect effect;
        public UnitData caster;
        public int ownerId;
        public bool vfxAllowed;
        public float nextTick;
        public float endTime;
    }

    static SkillDamageZone runner;
    readonly List<Zone> zones = new List<Zone>();
    static readonly List<EnemyDummy> scratch = new List<EnemyDummy>();

    public static int ActiveCount => runner != null ? runner.zones.Count : 0;

    public static void Spawn(Vector3 center, float worldRadius, SkillEffect effect, UnitData caster, int ownerId, bool vfxAllowed)
    {
        if (effect == null || effect.zoneTickInterval <= 0f || effect.duration <= 0f || worldRadius <= 0f) return;
        if (runner == null) runner = new GameObject("SkillDamageZones").AddComponent<SkillDamageZone>();
        runner.zones.Add(new Zone
        {
            center = center, radius = worldRadius, effect = effect, caster = caster, ownerId = ownerId, vfxAllowed = vfxAllowed,
            nextTick = Time.time + effect.zoneTickInterval,
            // 틱 수 = floor(수명 ÷ 간격) — 부동소수 오차로 마지막 틱이 빠지지 않게 조금 넉넉히.
            endTime = Time.time + effect.duration + effect.zoneTickInterval * 0.01f,
        });
    }

    void Update()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            Zone zone = zones[i];
            while (zone.nextTick <= Time.time && zone.nextTick <= zone.endTime)
            {
                Tick(zone);
                zone.nextTick += zone.effect.zoneTickInterval;
            }
            if (zone.nextTick > zone.endTime) zones.RemoveAt(i);
        }
    }

    static void Tick(Zone zone)
    {
        float amount = zone.effect.multiplier + zone.effect.bonus;
        if (amount <= 0f) return;
        // 피해로 적이 죽으면 EnemyDummy.Active가 바뀐다 — 먼저 모아 두고 돈다.
        scratch.Clear();
        float sqr = zone.radius * zone.radius;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
            if (enemy != null && !enemy.IsDead && (enemy.transform.position - zone.center).sqrMagnitude <= sqr) scratch.Add(enemy);
        bool vfxBefore = SkillVfx.SetCasterGate(zone.vfxAllowed);
        foreach (EnemyDummy enemy in scratch)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (!UnitAttacker.PassesPointValueCondition(zone.effect.targetCondition, zone.effect.targetConditionValue, enemy)) continue;
            float hpBefore = enemy.Hp;
            enemy.TakeDamage(amount, zone.effect.damageType, zone.effect.attackType, zone.ownerId);
            SkillTelemetry.Damage(zone.caster, "지대", enemy, hpBefore);
        }
        SkillVfx.SetCasterGate(vfxBefore);
        scratch.Clear();
    }
}
