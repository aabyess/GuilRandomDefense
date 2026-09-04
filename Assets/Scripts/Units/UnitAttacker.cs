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

    // 특성강화(딜증가)가 이 유닛 종에 거는 영구 배율. attackDamage(원본)는 그대로 두고 여기서만
    // 곱한다 — 도움소의 임시 버프(ApplyStats로 원본값을 기억했다 되돌리는 방식)와 순서 상관없이
    // 겹쳐도 안 깨지게 하려는 설계다(PM 지시). 언락 상태가 바뀔 때만 다시 계산하도록 이벤트로
    // 무효화한다. 특성강화 11개 유형(UnitTraitData.cs 참고) 중 지금 반영되는 건 딜증가뿐이다 —
    // 이감·방깎은 EnemyDummy 쪽 인프라가 없어서 2차로 미뤘고, 소환·메커니즘변경 등은 유닛 전용
    // 코드(Tier B)가 필요하다.
    UnitUpgrades upgrades;
    bool upgradesResolveAttempted;
    bool upgradeMultiplierDirty = true;
    float cachedUpgradeMultiplier = 1f;

    // 디버그 표시용 — 스탯이 실제로 적용됐는지 화면에서 확인하기 위해 노출한다.
    public float AttackDamage => attackDamage * UpgradeMultiplier;
    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval / AttackSpeedMultiplier;

    // 도움소의 임시 공격속도 버프. 값을 덮어썼다 되돌리는 대신 여기에 쌓는다 —
    // 되돌리는 쪽이 "원래값"을 기억하면, 그 사이에 영구 강화를 사거나 버프가 겹칠 때
    // 기억해둔 옛 값으로 되돌아가면서 산 것이 조용히 사라진다.
    readonly List<float> attackSpeedBuffs = new List<float>();

    float AttackSpeedMultiplier
    {
        get
        {
            float product = 1f;
            foreach (float buff in attackSpeedBuffs) product *= buff;
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

    // 방깎·마방깍 특성을 때릴 때마다 대상에 쌓는다.
    //
    // 처음엔 "이 유닛 몫은 한 번만"으로 막아뒀는데, 그건 무한 누적이 걱정돼서 우리가 정한 것이지
    // 근거가 없었다. 원작 맵(`war3map.w3a`)을 뜯어보니 **방깎은 전역에서 흔하게 중첩되고**
    // 능력마다 상한이 따로 있다(총 -75/-80, 또는 7·9·10회 등). 지속시간 서술은 24건 어디에도
    // 없어서 영구 누적으로 보인다 (`Docs/reference/ABILITIES_RESEARCH.md`, 구현담당1 조사).
    // → 제한을 걷어냈다. 무한히 쌓여도 EffectiveArmor가 -20에서 잘리므로 효과는 유계다.
    //
    // ⚠️ 능력별 상한은 아직 못 넣는다 — TraitEffect에 상한을 담을 자리가 없다.
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

    // 이 유닛의 데미지 판정. UnitData가 없으면(씬에 손으로 놓은 더미 등) 물리로 둔다 —
    // 여기서 None을 넘기면 EnemyDummy가 어차피 물리로 취급하므로 결과는 같지만, 뜻을 분명히 한다.
    DamageType DamageTypeOf => identity != null && identity.Data != null
        ? identity.Data.damageType
        : DamageType.AD;

    float UpgradeMultiplier
    {
        get
        {
            if (upgradeMultiplierDirty)
            {
                UnitUpgrades source = ResolveUpgrades();
                UnitData unitData = identity != null ? identity.Data : null;
                float damageBonusPercent = source != null && unitData != null
                    ? source.EffectSum(unitData, TraitEffectKind.DamageIncrease)
                    : 0f;
                cachedUpgradeMultiplier = 1f + damageBonusPercent;
                upgradeMultiplierDirty = false;
            }
            return cachedUpgradeMultiplier;
        }
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
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        attackTimer = AttackInterval;

        EnemyDummy target = ResolveTarget();
        if (target != null)
        {
            Anim?.PlayAttack();
            ApplyArmorShred(target);
            target.TakeDamage(AttackDamage, DamageTypeOf, owner != null ? owner.OwnerId : -1);
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
