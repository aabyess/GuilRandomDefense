using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitAttacker : MonoBehaviour
{
    [SerializeField] float attackRange = 5f;
    [SerializeField] float attackDamage = 2f;
    [SerializeField] float attackInterval = 1f;

    float attackTimer;

    // 모델이 붙기 전엔 없다. 있으면 공격할 때 모션을 돌린다.
    CharacterAnimator anim;
    bool animResolved;

    CharacterAnimator Anim
    {
        get
        {
            if (!animResolved)
            {
                anim = GetComponent<CharacterAnimator>();
                animResolved = true;
            }
            return anim;
        }
    }
    OwnedByPlayer owner;
    UnitCombat combat;
    UnitIdentity identity;

    // 특성강화(딜증가) + 연구소(등급 전체 강화, 05번 2026-09-05 추가)가 이 유닛 종에 거는
    // 영구 배율 둘을 곱해서 낸다. attackDamage(원본)는 그대로 두고 여기서만 곱한다 — 도움소의
    // 임시 버프(ApplyStats로 원본값을 기억했다 되돌리는 방식)와 순서 상관없이 겹쳐도 안
    // 깨지게 하려는 설계다(PM 지시). 언락/레벨업 상태가 바뀔 때만 다시 계산하도록 이벤트로
    // 무효화한다(UnitUpgrades.OnLevelChanged — Unlock과 LevelUp 둘 다 이걸 쏜다). 특성강화
    // 11개 유형(UnitTraitData.cs 참고) 중 지금 반영되는 건 딜증가뿐이다 — 이감·방깎은
    // EnemyDummy 쪽 인프라가 없어서 2차로 미뤘고, 소환·메커니즘변경 등은 유닛 전용 코드
    // (Tier B)가 필요하다.
    UnitUpgrades upgrades;
    bool upgradesResolveAttempted;
    bool upgradeMultiplierDirty = true;
    float cachedUpgradeMultiplier = 1f;
    float cachedResearchBonus;

    // 디버그 표시용 — 스탯이 실제로 적용됐는지 화면에서 확인하기 위해 노출한다.
    // 연구소 가산치(ResearchBonus)는 배수(UpgradeMultiplier)·도움소 버프(AttackPowerMultiplier)
    // 어느 쪽과도 안 곱한다 — 원작 공식 "기본공격력×배수 + 가산치" 그대로, 맨 위에 더하기만 한다.
    public float AttackDamage => attackDamage * UpgradeMultiplier * AttackPowerMultiplier + ResearchBonus;
    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval / AttackSpeedMultiplier;

    // 도움소의 임시 공격속도 버프. 값을 덮어썼다 되돌리는 대신 여기에 쌓는다 —
    // 되돌리는 쪽이 "원래값"을 기억하면, 그 사이에 영구 강화를 사거나 버프가 겹칠 때
    // 기억해둔 옛 값으로 되돌아가면서 산 것이 조용히 사라진다.
    readonly List<float> attackSpeedBuffs = new List<float>();

    // 출항이다(원작 확인, 2026-09-04: 공격속도가 아니라 공격력 버프다)용 — 같은 누적 방식.
    readonly List<float> attackPowerBuffs = new List<float>();

    float AttackSpeedMultiplier
    {
        get
        {
            float product = 1f;
            foreach (float buff in attackSpeedBuffs) product *= buff;
            return product > 0f ? product : 1f;
        }
    }

    float AttackPowerMultiplier
    {
        get
        {
            float product = 1f;
            foreach (float buff in attackPowerBuffs) product *= buff;
            return product > 0f ? product : 1f;
        }
    }

    public void AddAttackSpeedBuff(float multiplier)
    {
        if (multiplier > 0f) attackSpeedBuffs.Add(multiplier);
    }

    public void RemoveAttackSpeedBuff(float multiplier)
    {
        attackSpeedBuffs.Remove(multiplier);
    }

    public void AddAttackPowerBuff(float multiplier)
    {
        if (multiplier > 0f) attackPowerBuffs.Add(multiplier);
    }

    public void RemoveAttackPowerBuff(float multiplier)
    {
        attackPowerBuffs.Remove(multiplier);
    }

    // 방깎·마방깍 특성을 때릴 때마다 대상에 쌓는다.
    //
    // 처음엔 "이 유닛 몫은 한 번만"으로 막아뒀는데, 그건 무한 누적이 걱정돼서 우리가 정한 것이지
    // 근거가 없었다. 원작 맵(`war3map.w3a`)을 뜯어보니 **방깎은 전역에서 흔하게 중첩되고**
    // 능력마다 상한이 따로 있다(총 -75/-80, 또는 7·9·10회 등). 지속시간 서술은 24건 어디에도
    // 없다. 버프 파일(`war3map.w3h`)의 버프 311개를 전수 확인해도 지속시간 필드 자체가 없고,
    // 능력 파일 쪽 지속시간 필드는 다른 능력 1,036건에서 멀쩡히 쓰이는데 방깎 계열만 0이다 —
    // **영구 누적이 확정이다** (`UNIT_STATS_RESEARCH.md`).
    // → 제한을 걷어냈다. 무한히 쌓여도 EffectiveArmor가 -20에서 잘리므로 효과는 유계다.
    //
    // ⚠️ 2026-09-05 정정(2차, 04③): 1차 정정("표 레벨을 올리는 것으로 바뀌었다")이
    // 틀렸었다 — A0TK/A0VI/A0VJ는 범용 방깎 표가 아니라 **카이도·핸콕 전용 스킬**이
    // 올리는 능력이었다(PM, 트리거 재조사). 우리 유닛의 일반 ArmorShred 트레잇은
    // 원래대로 EnemyDummy.armorShred(float, 원작 `Iarp`류)를 직접 깎는다 — 이 값은
    // ArmorFloor(-20)에서 잘리므로 무한 누적이어도 효과는 유계다. A0TK/A0VI/A0VJ 쪽은
    // EnemyDummy.AddKaidoAttackStack 등 전용 메서드로만 올라간다(카이도·핸콕에 대응하는
    // 유닛이 우리 로스터에 아직 없어 호출부는 없음 — 06번 이후).
    void ApplyArmorShred(EnemyDummy target)
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;

        UnitUpgrades source = ResolveUpgrades();
        if (source == null) return;

        float shred = source.EffectSum(unitData, TraitEffectKind.ArmorShred);
        if (shred > 0f) target.AddArmorShred(shred);

        // 마방깍은 마법 방어 배율을 올린다(= 마법 피해를 더 받게 한다). 방깎과 별개 축이다.
        float magicShred = source.EffectSum(unitData, TraitEffectKind.MagicArmorShred);
        if (magicShred > 0f) target.AddMagicArmorShred(magicShred);
    }

    // 평타 강화(원작 Bash) — 방금 들어간 평타에 이어 확률로 별도 피해 인스턴스를 한 번 더
    // 먹인다. 평타와 같은 DamageType/AttackType을 써서 방어력 감폭·상성표를 평타와
    // 똑같이 통과시킨다(Docs/reference/AUTO_ATTACK_CRIT_DESIGN.md §3). critChance가
    // 0(기본값)인 유닛은 Random.value < 0f가 항상 거짓이라 완전히 비활성이다.
    void ApplyCritIfTriggered(EnemyDummy target)
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null || unitData.critChance <= 0f) return;
        if (Random.value >= unitData.critChance) return;

        float bonus = AttackDamage * unitData.critDamageMultiplier + unitData.critBonusDamage;
        target.TakeDamage(bonus, DamageTypeOf, AttackTypeOf, owner != null ? owner.OwnerId : -1);

        if (unitData.critStunDuration > 0f) StartCoroutine(CritStunRoutine(target, unitData.critStunDuration));
    }

    // SupportShop.StunRoutine과 같은 패턴 — AddFreeze/RemoveFreeze는 겹침 횟수를 세므로
    // 다른 스턴원과 동시에 걸려도 서로를 밀어내지 않는다.
    IEnumerator CritStunRoutine(EnemyDummy target, float duration)
    {
        target.AddFreeze();
        yield return new WaitForSeconds(duration);
        if (target != null) target.RemoveFreeze();
    }

    // ---- 유닛 능력(스킬) — 평타·Bash와 별개 축이다(PM 지시, 2026-09-05). 기존 attackTimer·
    // ApplyCritIfTriggered는 위에서 안 건드렸다. UnitData.skill이 비어 있는 유닛(지금 전부)은
    // 여기서 전부 조용히 리턴하므로 동작이 그대로다 — 사장님이 유닛별로 SkillData를
    // 배정하기 전까지는 실질적으로 죽어 있다.
    float skillCooldownTimer;

    // 06번① 능력교체형 트레잇(UnitTraitData.replacementSkill)이 걸려 있으면 원래
    // UnitData.skill 대신 그걸 통째로 쓴다 — 원작이 레벨을 올리는 게 아니라 능력 자체를
    // 갈아끼우는 26분기 중 8개라(UnitRemoveAbilityBJ+UnitAddAbilityBJ), 레벨 인덱스로는
    // 못 담는다. 스킬승급형(레벨 인덱스)과 능력교체형(스킬 자체 교체)은 유닛 1종당 트레잇
    // 1개뿐이라 겹칠 일이 없다.
    SkillData Skill
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            if (unitData == null) return null;

            UnitUpgrades source = ResolveUpgrades();
            SkillData replacement = source != null ? source.ReplacementSkillFor(unitData) : null;
            return replacement != null ? replacement : unitData.skill;
        }
    }

    // 06번① 완료: 스킬승급형 트레잇(UnitTraitData.skillLevelUnlockIndex)이 UnitUpgrades에
    // 걸려 있으면 그 레벨을, 없으면 레벨1(index 0)을 쓴다. 원작이 "레벨2 = 레벨1 그대로 +
    // 새 효과"로 만들어서(수치 배율이 아니다) 인덱스만 바꾸는 것으로 충분하다 — 레벨1/2
    // 각각의 SkillLevel.effects 자체를 SkillData 에셋 쪽에서 이미 완결된 목록으로 담아둔다.
    SkillLevel CurrentSkillLevel(SkillData skill)
    {
        if (skill.levels == null || skill.levels.Count == 0) return null;

        int index = 0;
        UnitData unitData = identity != null ? identity.Data : null;
        UnitUpgrades source = ResolveUpgrades();
        if (unitData != null && source != null) index = source.SkillLevelIndexFor(unitData);

        return skill.levels[Mathf.Clamp(index, 0, skill.levels.Count - 1)];
    }

    // CooldownAutoCast·Aura 전용 — OnHitChance는 평타가 실제로 맞았을 때만 판정해야 해서
    // Update()의 공격 성공 분기에서 TryCastOnHitSkill로 따로 부른다.
    void UpdateSkillCooldown()
    {
        SkillData skill = Skill;
        if (skill == null) return;
        if (skill.triggerType != SkillTriggerType.CooldownAutoCast && skill.triggerType != SkillTriggerType.Aura) return;

        SkillLevel level = CurrentSkillLevel(skill);
        if (level == null) return;

        // 06번① 순위배정으로 13기의 UnitData.skill이 null이 아니게 됐지만 levels의
        // effects는 전부 빈 배열이다(수치 미상, 자리만 있음) — 여기서 걸러서 쿨다운
        // 타이머 자체가 돌지 않게 한다. 안 그러면 "숫자만 없다"가 아니라 "빈 채로 계속
        // 돌고 있다"가 된다(PM 지시, 2026-09-05).
        if (level.effects == null || level.effects.Count == 0) return;

        skillCooldownTimer -= Time.deltaTime;
        if (skillCooldownTimer > 0f) return;

        // 오라는 쿨다운 개념이 없다("계속 켜져 있다") — 매 프레임 판정하면 값이 생겼을 때
        // 폭증하니 1초 주기로 재판정한다.
        skillCooldownTimer = skill.triggerType == SkillTriggerType.Aura ? 1f : Mathf.Max(0.01f, level.cooldown);
        CastSkillLevel(level, level.range, null);
    }

    void TryCastOnHitSkill(EnemyDummy attackedTarget)
    {
        SkillData skill = Skill;
        if (skill == null || skill.triggerType != SkillTriggerType.OnHitChance) return;

        SkillLevel level = CurrentSkillLevel(skill);
        if (level == null || level.effects == null || level.effects.Count == 0) return;
        if (Random.value >= level.triggerChance) return;

        CastSkillLevel(level, level.range, attackedTarget);
    }

    // primaryTarget: OnHitChance가 이미 골라둔 대상(SingleTarget 효과가 우선 이걸 쓴다).
    // 없으면(쿨다운·오라) 사거리 안에서 새로 고른다.
    void CastSkillLevel(SkillLevel level, float range, EnemyDummy primaryTarget)
    {
        if (level.effects == null) return;

        foreach (SkillEffect effect in level.effects)
        {
            if (effect == null || Random.value >= effect.chance) continue;
            ApplySkillEffect(effect, range, primaryTarget);
        }
    }

    void ApplySkillEffect(SkillEffect effect, float range, EnemyDummy primaryTarget)
    {
        switch (effect.target)
        {
            case SkillTargetKind.Enemies:
                foreach (EnemyDummy enemy in EnemyDummy.Active)
                {
                    if (enemy == null) continue;
                    if (range > 0f && Vector3.Distance(enemy.transform.position, transform.position) > range) continue;
                    ApplyToEnemy(effect, enemy);
                }
                break;

            case SkillTargetKind.SingleTarget:
                EnemyDummy target = primaryTarget != null ? primaryTarget : FindClosestEnemyWithin(range);
                if (target != null) ApplyToEnemy(effect, target);
                break;

            case SkillTargetKind.Self:
                if (identity != null) ApplyToAlly(effect, identity);
                break;

            case SkillTargetKind.Allies:
                // "같은 편"은 UnitIdentity.AlliesOf(소유자 기준)로 푼다 — 캐스터가 플레이어
                // 유닛일 때의 정의다. 캐스터가 보스(EnemyDummy)면 EnemyDummy.AlliesOf를 쓴다
                // (04번 오라가 그쪽이다) — UnitAttacker는 플레이어 유닛에만 붙으므로 여기선
                // 이 갈래만 있으면 된다.
                foreach (UnitIdentity ally in UnitIdentity.AlliesOf(identity, range))
                    ApplyToAlly(effect, ally);
                break;
        }
    }

    // ArmorBonus/HealOverTime은 EnemyDummy 전용이다(EnemyDummy.ApplyAllyAuraEffect 참고) —
    // 플레이어 유닛은 armor·hpRegenPerSecond 개념 자체가 없어서(HP·방어력이 EnemyData에만
    // 있다) 적용할 필드가 없다. Damage/Stun/ArmorBreak/ExtraProjectile도 아군에게 뜻이
    // 통하는 게 없다. 그래서 대상은 모이지만(Self/Allies) 지금은 여전히 아무 것도 안 한다.
    void ApplyToAlly(SkillEffect effect, UnitIdentity ally)
    {
    }

    float ResolveSkillEffectValue(SkillEffect effect, EnemyDummy target)
    {
        switch (effect.basis)
        {
            case SkillEffectBasis.Flat: return effect.multiplier;
            // ⚠️ 2026-09-05 정정: %체력 분기는 "이 대상이 %체력기를 타는가" 게이트가 먼저다
            // (원작 GetUnitPointValue(대상)<200 — 보스는 200 이상이라 이 분기 자체를 건너뛰고
            // 별도 고정값 분기로 간다). 그 고정값 자체는 아직 없어서(사장님 콘텐츠 미상) 게이트가
            // 막히면 0을 돌려준다 — "원작처럼 다른 값이 나간다"가 아니라 "지금은 안 나간다".
            // EnemyData.takesPercentDamage 참고. 감수성 계수(PercentDamageTakenMultiplier)는
            // 여기서 안 곱한다 — 아래 DealSkillDamage에서 스킬 피해 전반에 곱한다.
            case SkillEffectBasis.TargetMaxHpPercent:
                return target.TakesPercentDamage ? target.MaxHp * effect.multiplier : 0f;
            case SkillEffectBasis.TargetCurrentHpPercent:
                return target.TakesPercentDamage ? target.Hp * effect.multiplier : 0f;
            case SkillEffectBasis.CasterAttackPower: return AttackDamage * effect.multiplier + effect.bonus;
            // 연구소(05번, 구현담당1)가 서면 실제 단계값을 여기서 곱한다 — 지금은 자리만이라
            // bonus만 돌려준다(대개 0이라 사실상 무효).
            case SkillEffectBasis.ResearchLevel: return effect.bonus;
            default: return 0f;
        }
    }

    // 적 하나에게 효과 하나를 적용한다 — kind별로 갈린다. ApplySkillEffect의 Enemies/
    // SingleTarget 갈래가 여길 거친다(Self/Allies는 ApplyToAlly, 아직 아무 것도 안 한다).
    void ApplyToEnemy(SkillEffect effect, EnemyDummy target)
    {
        switch (effect.kind)
        {
            case SkillEffectKind.Damage:
                DealSkillDamage(effect, target);
                break;

            // 일반 행동정지 스턴만이다 — 원작의 "게이지를 미는 스턴"(신세계 사이드보스
            // 전용, 우리에 그 시스템 자체가 없다)은 안 만든다. SupportShop.StunRoutine·
            // UnitAttacker.CritStunRoutine과 같은 패턴: AddFreeze/RemoveFreeze는 겹침
            // 횟수를 세므로 다른 스턴원과 동시에 걸려도 서로를 밀어내지 않는다.
            case SkillEffectKind.Stun:
                if (effect.duration > 0f) StartCoroutine(SkillStunRoutine(target, effect.duration));
                break;

            // ArmorBreak/ExtraProjectile은 아직 값 의미가 없다(이번 작업 범위 밖) — 조용히 무시.
        }
    }

    IEnumerator SkillStunRoutine(EnemyDummy target, float duration)
    {
        target.AddFreeze();
        yield return new WaitForSeconds(duration);
        if (target != null) target.RemoveFreeze();
    }

    void DealSkillDamage(SkillEffect effect, EnemyDummy target)
    {
        // ⚠️ 2026-09-05 정정: PercentDamageTakenMultiplier(원작 A11S)는 "%체력 피해 전용
        // 감수성"이 아니라 "이 대상이 스킬 피해를 얼마나 받는가" 계수다 — 원작에 게이트 없이
        // 고정 피해에도 같은 계수가 곱는 사례가 43곳 중 7곳 있다(리서치담당 재조사). 그래서
        // basis를 안 가리고 스킬 피해 전반에 곱한다. %체력 분기 자체를 타는지는 별개 축
        // (target.TakesPercentDamage, ResolveSkillEffectValue에서 이미 갈랐다)이다.
        float amount = ResolveSkillEffectValue(effect, target) * target.PercentDamageTakenMultiplier;
        if (amount <= 0f) return;

        // ⚠️ 평타(DamageTypeOf/AttackTypeOf)가 아니라 이 효과 자신의 damageType/attackType을
        // 쓴다 — 평타는 항상 물리라 스킬만 마법을 낼 수 있다(SkillEffect 필드 주석 참고).
        // 캐스터의 평타 속성을 그대로 물려주면 물리 유닛의 스킬이 전부 물리로만 나가버린다.
        int hits = Mathf.Max(1, effect.hitCount);
        if (hits <= 1)
        {
            target.TakeDamage(amount, effect.damageType, effect.attackType, owner != null ? owner.OwnerId : -1);
            return;
        }

        // SupportSkillData.waveCount/duration과 같은 관례 — duration에 걸쳐 나눠 때린다.
        StartCoroutine(SkillMultiHitRoutine(target, amount, effect.damageType, effect.attackType, hits, effect.duration));
    }

    IEnumerator SkillMultiHitRoutine(EnemyDummy target, float amountPerHit, DamageType damageType, AttackType attackType, int hits, float duration)
    {
        float interval = duration > 0f ? duration / hits : 0f;
        for (int i = 0; i < hits; i++)
        {
            if (target != null)
                target.TakeDamage(amountPerHit, damageType, attackType, owner != null ? owner.OwnerId : -1);
            if (i < hits - 1 && interval > 0f) yield return new WaitForSeconds(interval);
        }
    }

    EnemyDummy FindClosestEnemyWithin(float range)
    {
        EnemyDummy closest = null;
        float closestSqrDistance = range * range;

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null) continue;
            float sqrDistance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closest = enemy;
        }

        return closest;
    }

    // 이 유닛의 데미지 판정. UnitData가 없으면(씬에 손으로 놓은 더미 등) 물리로 둔다 —
    // 여기서 None을 넘기면 EnemyDummy가 어차피 물리로 취급하므로 결과는 같지만, 뜻을 분명히 한다.
    DamageType DamageTypeOf => identity != null && identity.Data != null
        ? identity.Data.damageType
        : DamageType.AD;

    // 이 유닛의 평타 공격 타입. ⚠️ "239종 어디에도 안 붙어서 전부 Unassigned"는 낡은
    // 서술이다(2026-09-05 확인, UnitData.cs의 attackType 필드 주석과 같은 정정) —
    // damageType=AP인 40종엔 이미 Magic이 붙어 있다. 물리(AD) 199종의 세부 타입
    // (normal/pierce/siege/hero/chaos) 배정만 아직 안 된 것뿐이다. 원작 구조 확정
    // (2026-09-05, B안)으로 **평타는 이제 항상 물리다** — damageType=AP인 40종의 Magic도
    // 원작 근거가 없어서(원작 플레이어 유닛 평타는 마법 0건) 배정 단계에서 정리될 값이다.
    AttackType AttackTypeOf => identity != null && identity.Data != null
        ? identity.Data.attackType
        : AttackType.Unassigned;

    float UpgradeMultiplier
    {
        get
        {
            RefreshUpgradeCacheIfDirty();
            return cachedUpgradeMultiplier;
        }
    }

    // 연구소 절대 가산치(gba2/gmo2, 2026-09-05 PM 승인 — 배수 계산과 완전히 별개 필드).
    // 배수와 곱해지는 게 아니라 AttackDamage에서 그 위에 그대로 더해진다.
    float ResearchBonus
    {
        get
        {
            RefreshUpgradeCacheIfDirty();
            return cachedResearchBonus;
        }
    }

    // 배수(특성강화 딜증가 × 연구소 등급배율)와 가산치(연구소 절대 가산)를 한 번에 갱신한다 —
    // 둘 다 같은 UnitUpgrades.OnLevelChanged 이벤트로만 바뀌므로 dirty 플래그를 공유해도 된다.
    void RefreshUpgradeCacheIfDirty()
    {
        if (!upgradeMultiplierDirty) return;

        UnitUpgrades source = ResolveUpgrades();
        UnitData unitData = identity != null ? identity.Data : null;
        float damageBonusPercent = source != null && unitData != null
            ? source.EffectSum(unitData, TraitEffectKind.DamageIncrease)
            : 0f;

        // 연구소(등급 전체 강화, 05번, 2026-09-05) — 특성강화(딜증가)와 별개 축이라
        // 곱으로 겹친다. 유닛 종의 등급이 담당 트랙에 없거나 그 트랙이 아직 레벨 0이면
        // MultiplierForGrade가 1을 돌려줘서 무영향이다.
        float researchMultiplier = source != null && unitData != null
            ? source.MultiplierForGrade(unitData.grade)
            : 1f;

        cachedUpgradeMultiplier = (1f + damageBonusPercent) * researchMultiplier;

        // 절대 가산치 — 전설·히든·불멸·초월·제한됨 5개 트랙만 0이 아니다. 대응 트랙이 없거나
        // 레벨 0이면 BonusForGrade가 0을 돌려준다.
        cachedResearchBonus = source != null && unitData != null
            ? source.BonusForGrade(unitData.grade)
            : 0f;

        upgradeMultiplierDirty = false;
    }

    // Awake 시점엔 OwnedByPlayer.OwnerId가 아직 안 잡혀 있을 수 있어(스폰 직후 동기 설정 순서 —
    // EnemyDummy.SpawnRound와 같은 이유) 실제로 필요해지는 첫 조회 시점까지 미룬다.
    UnitUpgrades ResolveUpgrades()
    {
        if (upgradesResolveAttempted) return upgrades;
        if (owner == null) return null;

        upgradesResolveAttempted = true;
        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        upgrades = context != null ? context.UnitUpgrades : null;
        if (upgrades != null) upgrades.OnLevelChanged += HandleUpgradesChanged;
        return upgrades;
    }

    void HandleUpgradesChanged() => upgradeMultiplierDirty = true;

    public float DistanceToClosestEnemy()
    {
        float best = float.PositiveInfinity;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            float distance = Vector3.Distance(enemy.transform.position, transform.position);
            if (distance < best) best = distance;
        }
        return best;
    }

    // damage는 "지금 적용하고 싶은 유효(강화 반영 후) 데미지"로 받는다 — 도움소 임시 버프가
    // AttackDamage(유효값)를 읽었다가 그대로 돌려놓는 방식으로 쓴다. 여기서 강화 배율을 미리
    // 나눠 원본에 저장해야, 곱한 뒤 결과가 호출자가 넘긴 값과 정확히 같아진다 — 안 그러면
    // 버프가 시작/종료될 때마다 강화 배율이 한 번씩 더 곱해져 버린다.
    /// <summary>
    /// 이 유닛의 <b>기준</b> 스탯을 정한다(UnitData의 값). 강화 배율은 여기 안 섞는다 —
    /// 여기에 배율을 반영하면, 이 함수를 부르는 쪽마다 "기준값을 주는 건지 지금 값을 주는 건지"가
    /// 달라져서 어느 한쪽은 반드시 틀리게 된다.
    /// </summary>
    public void ApplyStats(float damage, float range, float attacksPerSecond)
    {
        attackDamage = damage;
        attackRange = range;
        if (attacksPerSecond > 0f)
            attackInterval = 1f / attacksPerSecond;
    }

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
        combat = GetComponent<UnitCombat>();
        identity = GetComponent<UnitIdentity>();
    }

    void OnDestroy()
    {
        if (upgrades != null) upgrades.OnLevelChanged -= HandleUpgradesChanged;
    }

    void Update()
    {
        UpdateSkillCooldown();

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        attackTimer = AttackInterval;

        EnemyDummy target = ResolveTarget();
        if (target != null)
        {
            Anim?.PlayAttack();
            ApplyArmorShred(target);
            target.TakeDamage(AttackDamage, DamageTypeOf, AttackTypeOf, owner != null ? owner.OwnerId : -1);
            ApplyCritIfTriggered(target);
            TryCastOnHitSkill(target);
            return;
        }

        // 적이 없을 때만 문을 친다. 문이 우선이면 적이 몰려와도 문만 때리고 있게 된다.
        DestructibleGate gate = FindClosestGateInRange();
        if (gate != null)
        {
            Anim?.PlayAttack();
            gate.TakeDamage(AttackDamage);
        }
    }

    // UnitCombat이 있으면 그쪽이 이미 골라둔 목표(사거리 안에 있을 때만 넘겨줌)를 그대로 쓴다 —
    // 둘 다 EnemyDummy.Active를 훑으면 유닛 수만큼 중복 탐색이 된다. UnitCombat이 없는
    // 오브젝트(구버전 프리팹 등)를 위해 예전처럼 스스로 찾는 경로도 남겨둔다.
    EnemyDummy ResolveTarget()
    {
        if (combat != null) return combat.CurrentTarget;

        return FindClosestEnemyInRange();
    }

    DestructibleGate FindClosestGateInRange()
    {
        DestructibleGate closest = null;
        float closestSqrDistance = attackRange * attackRange;

        foreach (DestructibleGate gate in DestructibleGate.Active)
        {
            if (gate == null || gate.IsBroken) continue;

            // 문은 폭이 넓어서 중심까지의 거리로 재면 붙어 있어도 사거리 밖으로 나온다.
            Vector3 point = gate.GetComponent<Collider>() != null
                ? gate.GetComponent<Collider>().ClosestPoint(transform.position)
                : gate.transform.position;

            float sqrDistance = (point - transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closest = gate;
        }

        return closest;
    }

    EnemyDummy FindClosestEnemyInRange()
    {
        EnemyDummy closest = null;
        float closestSqrDistance = attackRange * attackRange;

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            float sqrDistance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closest = enemy;
        }

        return closest;
    }
}
