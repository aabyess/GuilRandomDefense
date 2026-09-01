using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 도움소: 플레이어마다 하나, 파괴 불가(TakeDamage 자체가 없다), 적 타겟에서 자동으로 제외된다 —
// UnitAttacker/UnitCombat은 EnemyDummy.Active만, 문 공격 폴백은 DestructibleGate.Active만 훑기
// 때문에 SupportShop은 애초에 그 두 레지스트리에 등록되지 않아 후보에 들어갈 수가 없다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class SupportShop : MonoBehaviour
{
    [SerializeField] List<SupportSkillData> skills = new List<SupportSkillData>();
    [SerializeField] RoundManager roundManager;

    readonly Dictionary<SupportSkillData, float> cooldownUntil = new Dictionary<SupportSkillData, float>();

    // EnemyDummy.TakeDamage는 적이 죽는 즉시 EnemyDummy.Active에서 자기를 뺀다. 그 리스트를
    // 직접 순회하면서 때리면 첫 처치에서 바로 "Collection was modified" 예외가 난다 —
    // 광역기는 죽이라고 있는 것이라 사실상 매번 터진다. 사거리 안 대상을 먼저 모아두고 때린다.
    readonly List<EnemyDummy> targetBuffer = new List<EnemyDummy>();

    List<EnemyDummy> CollectInRadius(Vector3 point, float radius)
    {
        targetBuffer.Clear();
        float radiusSqr = radius * radius;

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null) continue;
            if ((enemy.transform.position - point).sqrMagnitude > radiusSqr) continue;
            targetBuffer.Add(enemy);
        }

        return targetBuffer;
    }

    OwnedByPlayer owner;

    public IReadOnlyList<SupportSkillData> Skills => skills;

    RoundManager RoundManagerRef => roundManager != null
        ? roundManager
        : roundManager = FindFirstObjectByType<RoundManager>();

    PlayerContext OwnerContext => PlayerContext.Get(owner.OwnerId);

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
    }

    public bool CanCast(SupportSkillData skill)
    {
        if (skill == null) return false;
        if (Time.time < GetCooldownUntil(skill)) return false;

        PlayerContext context = OwnerContext;
        if (context == null) return false;

        if (skill.manaCost > 0 && (context.ResourceWallet == null || context.ResourceWallet.Get(ResourceType.Mana) < skill.manaCost))
            return false;

        if (skill.goldCost > 0 && (context.GoldWallet == null || context.GoldWallet.Gold < skill.goldCost))
            return false;

        return true;
    }

    float GetCooldownUntil(SupportSkillData skill)
    {
        return cooldownUntil.TryGetValue(skill, out float until) ? until : 0f;
    }

    void StartCooldown(SupportSkillData skill)
    {
        cooldownUntil[skill] = Time.time + skill.cooldownSeconds;
    }

    bool TrySpendCost(SupportSkillData skill, PlayerContext context)
    {
        if (skill.manaCost > 0 && !context.ResourceWallet.TrySpend(ResourceType.Mana, skill.manaCost))
            return false;

        if (skill.goldCost > 0 && !context.GoldWallet.TrySpend(skill.goldCost))
        {
            // 골드가 모자라 취소되면, 이미 나간 마나가 있다면 되돌린다 (지금은 마나+골드를 동시에
            // 요구하는 스킬이 없지만, 나중에 생겨도 재료만 날아가는 일이 없도록).
            if (skill.manaCost > 0) context.ResourceWallet.Add(ResourceType.Mana, skill.manaCost);
            return false;
        }

        return true;
    }

    // 마나포션: 위치·대상 없이 즉시 자기 자신에게.
    public bool TryCastSelf(SupportSkillData skill)
    {
        if (skill == null || skill.effect != SupportSkillEffect.ManaRestore) return false;
        if (!CanCast(skill)) return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        context.ResourceWallet?.Add(ResourceType.Mana, skill.manaRestoreAmount);
        StartCooldown(skill);
        return true;
    }

    // 폭우/지진/버스터콜/해적선충돌/흡수/해루석/출항이다 — 커서로 찍은 지점에 발동.
    public bool TryCastOnGround(SupportSkillData skill, Vector3 point)
    {
        if (skill == null || skill.targetKind != SupportSkillTargetKind.Ground) return false;
        if (!CanCast(skill)) return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        int round = RoundManagerRef != null ? RoundManagerRef.CurrentRound : 1;

        switch (skill.effect)
        {
            case SupportSkillEffect.Damage:
                ApplyAreaDamage(skill, point, round, context);
                break;
            case SupportSkillEffect.Root:
                ApplyRoot(skill, point, round);
                break;
            case SupportSkillEffect.Buff:
                ApplyBuff(skill, point);
                break;
            default:
                break;
        }

        StartCooldown(skill);
        return true;
    }

    // 연금술: 유닛을 직접 지정.
    public bool TryCastOnUnit(SupportSkillData skill, GameObject targetUnit)
    {
        if (skill == null || skill.targetKind != SupportSkillTargetKind.Unit) return false;
        if (skill.effect != SupportSkillEffect.UnitDismantle) return false;
        if (targetUnit == null) return false;
        if (!CanCast(skill)) return false;

        if (!targetUnit.TryGetComponent(out OwnedByPlayer targetOwner) || targetOwner.OwnerId != owner.OwnerId)
        {
            Debug.Log($"{skill.skillName}: 자기 유닛만 분해할 수 있습니다.");
            return false;
        }

        if (!targetUnit.TryGetComponent(out UnitIdentity identity) || identity.Data == null)
            return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        bool eligible = identity.Data.grade.Tier() <= skill.maxDismantleGrade.Tier();

        if (!eligible)
        {
            // 잘못된 대상 — "등가교환" 실패. 분해하지 않고 마나만 그대로 돌려준다.
            context.ResourceWallet?.Add(ResourceType.Mana, skill.manaCost);
            Debug.Log($"{skill.skillName}: {identity.Data.unitName}은(는) 분해할 수 없는 등급이라 마나를 돌려받았습니다.");
            StartCooldown(skill);
            return false;
        }

        context.UnitInventory?.Remove(identity.Data);
        context.ResourceWallet?.Add(ResourceType.Mana, FindDismantleRefund(skill, identity.Data.grade));
        Destroy(targetUnit);

        StartCooldown(skill);
        return true;
    }

    static int FindDismantleRefund(SupportSkillData skill, UnitGrade grade)
    {
        if (skill.dismantleRefunds == null) return 0;

        foreach (GradeManaRefund entry in skill.dismantleRefunds)
            if (entry.grade == grade) return entry.manaRefund;

        return 0;
    }

    void ApplyAreaDamage(SupportSkillData skill, Vector3 point, int round, PlayerContext context)
    {
        float damage = skill.ComputeDamage(round);
        int hits = 0;

        // 스킬 시전은 쿨다운(수십 초)에 묶여 있어 매 프레임 도는 경로가 아니다.
        List<EnemyDummy> targets = CollectInRadius(point, skill.radius);

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyDummy enemy = targets[i];
            if (enemy == null) continue;

            if (skill.duration > 0f) StartCoroutine(StunRoutine(enemy, skill.duration));

            enemy.TakeDamage(damage, owner.OwnerId);
            hits++;
        }

        if (skill.manaRefundPerHit > 0 && hits > 0 && context.ResourceWallet != null)
        {
            int refund = Mathf.Min(skill.manaRefundCap, skill.manaRefundPerHit * hits);
            if (refund > 0) context.ResourceWallet.Add(ResourceType.Mana, refund);
        }
    }

    void ApplyRoot(SupportSkillData skill, Vector3 point, int round)
    {
        int ticks = Mathf.Max(1, Mathf.RoundToInt(skill.duration));
        float tickDamage = skill.ComputeDamage(round) / ticks;

        List<EnemyDummy> targets = CollectInRadius(point, skill.radius);

        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null)
                StartCoroutine(RootAndDotRoutine(targets[i], tickDamage, ticks));
    }

    IEnumerator RootAndDotRoutine(EnemyDummy enemy, float tickDamage, int ticks)
    {
        enemy.AddFreeze();

        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForSeconds(1f);
            if (enemy == null) yield break;   // 죽었으면 풀어줄 대상 자체가 없다
            enemy.TakeDamage(tickDamage, owner.OwnerId);
        }

        if (enemy != null) enemy.RemoveFreeze();
    }

    IEnumerator StunRoutine(EnemyDummy enemy, float duration)
    {
        enemy.AddFreeze();

        yield return new WaitForSeconds(duration);

        if (enemy != null) enemy.RemoveFreeze();
    }

    // 출항이다: mapWide면 자기 유닛 전체, 아니면 클릭 지점 반경 안의 자기 유닛만.
    void ApplyBuff(SupportSkillData skill, Vector3 point)
    {
        int ownerId = owner.OwnerId;
        float radiusSqr = skill.radius * skill.radius;

        foreach (Selectable selectable in Selectable.All)
        {
            if (selectable == null) continue;
            if (!selectable.TryGetComponent(out OwnedByPlayer unitOwner) || unitOwner.OwnerId != ownerId) continue;

            if (!skill.mapWide)
            {
                float sqrDistance = (selectable.transform.position - point).sqrMagnitude;
                if (sqrDistance > radiusSqr) continue;
            }

            if (!selectable.TryGetComponent(out UnitAttacker attacker)) continue;

            StartCoroutine(BuffRoutine(attacker, skill.buffAttackSpeedMultiplier, skill.duration));
        }
    }

    IEnumerator BuffRoutine(UnitAttacker attacker, float multiplier, float duration)
    {
        float originalDamage = attacker.AttackDamage;
        float originalRange = attacker.AttackRange;
        float originalInterval = attacker.AttackInterval;
        float originalSpeed = originalInterval > 0f ? 1f / originalInterval : 1f;

        attacker.ApplyStats(originalDamage, originalRange, originalSpeed * multiplier);

        yield return new WaitForSeconds(duration);

        if (attacker != null)
            attacker.ApplyStats(originalDamage, originalRange, originalSpeed);
    }
}
