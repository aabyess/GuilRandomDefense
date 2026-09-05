using System.Collections.Generic;
using UnityEngine;

// 보스 오라(원작 A153 방어력 버프, A11T 회복)의 시전 루프.
//
// SkillData.triggerType이 Aura인 것만 다룬다 — OnHitChance/CooldownAutoCast는 보스 쪽에
// 아직 배정된 게 없다(플레이어 유닛 쪽은 UnitAttacker가 이미 그 둘을 처리한다). 사장님이
// 아직 어느 보스에 어느 SkillData를 배정할지 안 정하셔서(04번 원문: "유닛별 배정은 나중에
// 내가 준다") 에셋은 없다 — 이건 그 배정이 왔을 때 붙일 캐스터 쪽 메커니즘이다.
//
// ⚠️ 매 프레임 EnemyDummy.AlliesOf를 부르면 안 된다 — 그 메서드가 호출마다 새 List를
// 할당해서, 몹이 100마리 넘게 몰리는 후반 라운드에서 프레임당 GC가 튄다. TickInterval마다만
// 범위를 다시 잰다(PM 지시).
//
// ⚠️ Apply와 Remove는 반드시 짝을 맞춰 부른다 — 워크3와 달리 엔진이 오라를 자동으로
// 지워주지 않는다. "범위를 벗어났다"와 "캐스터가 죽었다" 둘 다 Remove를 불러야 한다.
// 안 그러면 EnemyDummy.ApplyAllyAuraEffect의 armorShred/regenBonus가 무한히 쌓인다.
[RequireComponent(typeof(EnemyDummy))]
public class EnemyAuraCaster : MonoBehaviour
{
    [SerializeField] SkillData skill;
    [SerializeField] float tickInterval = 0.5f;

    EnemyDummy self;
    float tickTimer;

    // 지금 이 오라 효과를 받고 있는 대상들. 매 틱 AlliesOf(range) 결과와 비교해서, 새로
    // 들어온 대상엔 Apply를, range를 벗어났거나 죽은 대상엔 Remove를 정확히 한 번씩만 건다.
    // 매 틱 전원에게 다시 Apply하면(같은 대상에 중복으로) 위 경고 그대로 무한 누적이 된다.
    readonly List<EnemyDummy> affected = new List<EnemyDummy>();

    void Awake()
    {
        self = GetComponent<EnemyDummy>();
    }

    void Update()
    {
        if (skill == null || skill.triggerType != SkillTriggerType.Aura || skill.levels.Count == 0) return;

        tickTimer -= Time.deltaTime;
        if (tickTimer > 0f) return;
        tickTimer = tickInterval;

        Tick();
    }

    void Tick()
    {
        // 06번(트레잇 배선)이 실제 레벨을 골라오면 이 인덱스를 바꾼다 — 지금은 SkillData 쪽에
        // 레벨을 올리는 코드가 어디에도 없어(SkillData.cs 자체 코멘트) 항상 레벨1만 쓴다.
        SkillLevel level = skill.levels[0];
        List<EnemyDummy> inRange = EnemyDummy.AlliesOf(self, level.range);

        for (int i = affected.Count - 1; i >= 0; i--)
        {
            EnemyDummy target = affected[i];
            if (target == null || !inRange.Contains(target))
            {
                if (target != null) RemoveEffectsFrom(target, level);
                affected.RemoveAt(i);
            }
        }

        foreach (EnemyDummy target in inRange)
        {
            if (target == null || affected.Contains(target)) continue;
            ApplyEffectsTo(target, level);
            affected.Add(target);
        }
    }

    void ApplyEffectsTo(EnemyDummy target, SkillLevel level)
    {
        foreach (SkillEffect effect in level.effects)
        {
            // Enemies/Self/SingleTarget 효과는 이 캐스터의 몫이 아니다 — 오라는 아군(같은 편
            // 몹) 버프만 다룬다. 원작 A153의 "적 피해" 서술은 다른 능력(A0CJ)의 툴팁이 잘못
            // 복사된 것으로 이미 확인됐다(04번 재정의, 사장님) — 실제로 피해를 주지 않는다.
            if (effect.target != SkillTargetKind.Allies) continue;
            target.ApplyAllyAuraEffect(effect);
        }
    }

    void RemoveEffectsFrom(EnemyDummy target, SkillLevel level)
    {
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Allies) continue;
            target.RemoveAllyAuraEffect(effect);
        }
    }

    // TakeDamage의 사망 처리와 RemoveInstantly 둘 다 Destroy(gameObject)로 끝나서 OnDestroy는
    // 예외 없이 걸린다 — 캐스터가 죽는 순간 지금까지 건 효과를 전부 되돌린다. 안 그러면
    // 보스가 죽어도 그 순간 범위 안에 있던 몹들에게 버프가 영구히 남는다.
    void OnDestroy()
    {
        if (skill == null || skill.levels.Count == 0) return;
        SkillLevel level = skill.levels[0];
        foreach (EnemyDummy target in affected)
        {
            if (target != null) RemoveEffectsFrom(target, level);
        }
        affected.Clear();
    }
}
