using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 도움소: 플레이어마다 하나, 파괴 불가(TakeDamage 자체가 없다), 적 타겟에서 자동으로 제외된다 —
// UnitAttacker/UnitCombat은 EnemyDummy.Active만, 문 공격 폴백은 DestructibleGate.Active만 훑기
// 때문에 SupportShop은 애초에 그 두 레지스트리에 등록되지 않아 후보에 들어갈 수가 없다.
//
// ILaneShop 이전: 슬롯 인덱스는 skills 리스트 인덱스와 그대로 같다. Docs/design/LANE_SHOP.md 참고.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class SupportShop : MonoBehaviour, ILaneShop
{
    static readonly Color SkillColor = new Color(0.25f, 0.55f, 0.9f, 0.9f);

    [SerializeField] List<SupportSkillData> skills = new List<SupportSkillData>();
    [SerializeField] RoundManager roundManager;

    // GetSlotTooltip에서만 쓴다 — 호버할 때만 불려서 여기서 조립 비용이 들어도 된다.
    readonly StringBuilder tooltipBuilder = new StringBuilder(256);

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

    // ---- ILaneShop ----
    // 슬롯 인덱스 = skills 리스트 인덱스. 마나포션은 위치·대상이 필요 없어 targetKind를 None으로
    // 답한다 — 예전엔 GameHud가 "effect == ManaRestore"를 직접 검사했는데, 그 판단이 여기로 옮겨왔다.

    public int SlotCount => skills.Count;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (index < 0 || index >= skills.Count) return LaneShopSlotView.Empty;

        SupportSkillData skill = skills[index];
        if (skill == null) return LaneShopSlotView.Empty;

        return new LaneShopSlotView(skill.skillName, SkillColor, CanCast(skill), TargetKindOf(skill));
    }

    static LaneShopTargetKind TargetKindOf(SupportSkillData skill)
    {
        if (IsSelfCast(skill.effect)) return LaneShopTargetKind.None;
        return skill.targetKind == SupportSkillTargetKind.Unit ? LaneShopTargetKind.Unit : LaneShopTargetKind.Ground;
    }

    // 대상 지정 없이 자기 자신에게 바로 발동하는 이펙트들 — 마나포션(마나 회복),
    // 선택위습제조(원작에서도 targetKind가 "none"이었다, 2026-09-04 확인).
    static bool IsSelfCast(SupportSkillEffect effect) =>
        effect == SupportSkillEffect.ManaRestore || effect == SupportSkillEffect.CraftChosenWisp;

    // 호버할 때만 불린다 — 문자열 조립은 여기서만 한다(GetSlotView는 매번 문자열을 만들지 않는다).
    public string GetSlotTooltip(int index)
    {
        if (index < 0 || index >= skills.Count) return null;

        SupportSkillData skill = skills[index];
        return skill != null ? BuildTooltipText(skill) : null;
    }

    public bool TryUse(int index, LaneShopTarget target)
    {
        if (index < 0 || index >= skills.Count) return false;

        SupportSkillData skill = skills[index];
        if (skill == null) return false;

        if (IsSelfCast(skill.effect)) return TryCastSelf(skill);
        if (skill.targetKind == SupportSkillTargetKind.Unit) return TryCastOnUnit(skill, target.unit);
        return TryCastOnGround(skill, target.point);
    }

    // 스킬 이름·효과 서술(SupportSkillData.description)은 그대로 옮기고, 비용/쿨다운/피해량/범위/
    // 지속시간처럼 수치인 부분만 코드가 채운다 — 스킬마다 분기 없이 필드값으로만 조립된다.
    // (GameHud.BuildSupportSkillTooltipText에서 그대로 옮겨왔다 — 내용 변경 없음.)
    string BuildTooltipText(SupportSkillData skill)
    {
        tooltipBuilder.Clear();
        tooltipBuilder.Append(skill.skillName);

        if (!string.IsNullOrEmpty(skill.description))
            tooltipBuilder.Append('\n').Append(skill.description);

        tooltipBuilder.Append("\n비용: ");
        bool hasCost = false;

        if (skill.manaCost > 0)
        {
            tooltipBuilder.Append("마나 ").Append(skill.manaCost);
            hasCost = true;
        }

        if (skill.goldCost > 0)
        {
            if (hasCost) tooltipBuilder.Append(" + ");
            tooltipBuilder.Append("골드 ").Append(skill.goldCost);
            hasCost = true;
        }

        if (!hasCost) tooltipBuilder.Append("없음");

        tooltipBuilder.Append("\n쿨다운: ").Append(skill.cooldownSeconds.ToString("0.#")).Append('s');

        float remaining = GetCooldownRemaining(skill);
        if (remaining > 0f)
            tooltipBuilder.Append(" (재사용까지 ").Append(remaining.ToString("F1")).Append("s)");

        if (skill.damageBase > 0f || skill.damagePerRound > 0f)
        {
            int round = RoundManagerRef != null ? RoundManagerRef.CurrentRound : 1;
            tooltipBuilder.Append("\n피해량: ").Append(skill.ComputeDamage(round).ToString("F0"))
                .Append(" (").Append(round).Append("라운드 기준)");
        }

        if (skill.targetKind == SupportSkillTargetKind.Ground)
        {
            tooltipBuilder.Append("\n범위: ").Append(skill.mapWide ? "맵 전체" : $"반경 {skill.radius:0.#}");
        }

        if (skill.duration > 0f)
            tooltipBuilder.Append("\n지속시간: ").Append(skill.duration.ToString("0.#")).Append('s');

        return tooltipBuilder.ToString();
    }

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
    }

    public bool CanCast(SupportSkillData skill)
    {
        if (skill == null) return false;
        if (Time.time < GetCooldownUntil(skill)) return false;
        if (skill.maxUses > 0 && UsesSoFar(skill) >= skill.maxUses) return false;

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

    // 툴팁에 "재사용까지 N초" 표시용.
    public float GetCooldownRemaining(SupportSkillData skill)
    {
        return Mathf.Max(0f, GetCooldownUntil(skill) - Time.time);
    }

    // GamblingProgress와 같은 결 — 옵션(스킬) 에셋을 키로 쓴다. 세이브/로드가 없는 한 판짜리
    // 게임이라 "이번 판 누적"이 곧 원작이 말하는 "평생 N회"다.
    readonly Dictionary<SupportSkillData, int> usesSoFar = new Dictionary<SupportSkillData, int>();

    public int UsesSoFar(SupportSkillData skill)
    {
        return skill != null && usesSoFar.TryGetValue(skill, out int count) ? count : 0;
    }

    void StartCooldown(SupportSkillData skill)
    {
        cooldownUntil[skill] = Time.time + skill.cooldownSeconds;

        if (skill.maxUses > 0)
            usesSoFar[skill] = UsesSoFar(skill) + 1;
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

    // 위치·대상 없이 즉시 자기 자신에게 — 마나포션(마나 회복) / 선택위습제조(위습 제조).
    public bool TryCastSelf(SupportSkillData skill)
    {
        if (skill == null) return false;
        if (skill.effect == SupportSkillEffect.ManaRestore) return TryManaRestore(skill);
        if (skill.effect == SupportSkillEffect.CraftChosenWisp) return TryCraftChosenWisp(skill);
        return false;
    }

    bool TryManaRestore(SupportSkillData skill)
    {
        if (!CanCast(skill)) return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        context.ResourceWallet?.Add(ResourceType.Mana, skill.manaRestoreAmount);
        StartCooldown(skill);
        return true;
    }

    // 선택위습제조: 흔함 선택위습 1기를 필드에 만들어낸다(원작 "최대 3번까지" — maxUses로 표현).
    // 위습 지급은 RewardDistributor.GrantWisps를 그대로 재사용한다 — 새 지급 경로를 안 만든다
    // (RoundManager.GrantFlatRoundReward와 같은 이유).
    bool TryCraftChosenWisp(SupportSkillData skill)
    {
        if (skill.craftedWisp == null) return false;
        if (!CanCast(skill)) return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        if (RewardDistributor.Instance != null)
        {
            List<WispReward> reward = new List<WispReward> { new WispReward { wisp = skill.craftedWisp, count = 1 } };
            RewardDistributor.Instance.GrantWisps(context, reward);
        }

        StartCooldown(skill);
        return true;
    }

    // 폭우/지진/버스터콜/불비/해루석/출항이다 — 커서로 찍은 지점에 발동.
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

    // 유닛을 직접 지정하는 스킬. 대상이 내 유닛인지(연금술) 적인지(흡수·낙뢰)는 이펙트별로 갈린다.
    public bool TryCastOnUnit(SupportSkillData skill, GameObject targetUnit)
    {
        if (skill == null || skill.targetKind != SupportSkillTargetKind.Unit) return false;
        if (targetUnit == null) return false;

        if (skill.effect == SupportSkillEffect.InstantKill) return TryInstantKillUnit(skill, targetUnit);
        if (skill.effect == SupportSkillEffect.SingleTargetDamage) return TrySingleTargetDamageUnit(skill, targetUnit);
        return TryDismantleUnit(skill, targetUnit);
    }

    // 흡수: 지정한 적 유닛 하나를 즉시 제거한다(원작 확인, 2026-09-04: RemoveUnit 방식).
    // 보스·스토리 유닛(라인에 안 속한 유닛 포함)은 안 통한다 — 원작 "보스, 스토리 적용X".
    // 단일 지정형이라 광역 스킬과 달리 CollectInRadius를 안 쓴다 — 원작 능력 데이터에
    // 범위(aare) 필드 자체가 없다(즉, 원작도 대상 하나만 잡는다).
    bool TryInstantKillUnit(SupportSkillData skill, GameObject targetUnit)
    {
        if (!targetUnit.TryGetComponent(out EnemyDummy enemy)) return false;
        if (enemy.IsBoss || enemy.LaneIndex < 0) return false;
        if (!CanCast(skill)) return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        // TakeDamage를 안 거친다 — 원작 RemoveUnit은 방어력/저항과 무관하게 무조건 없애고,
        // 킬 보상도 안 나간다(WC3의 RemoveUnit 자체가 그렇다). "즉사기가 킬 보상까지 주면
        // 사실상 무료 학살기가 된다"는 판단도 같이 깔려 있다 — PM 확인 예정.
        enemy.RemoveInstantly();

        StartCooldown(skill);
        return true;
    }

    // 낙뢰: 지정한 적 유닛 하나에 즉발 피해(+duration>0이면 스턴). 흡수와 달리 보스·스토리
    // 제외가 없다 — 원작 툴팁에 그런 제한 서술이 없었다.
    bool TrySingleTargetDamageUnit(SupportSkillData skill, GameObject targetUnit)
    {
        if (!targetUnit.TryGetComponent(out EnemyDummy enemy)) return false;
        if (!CanCast(skill)) return false;

        PlayerContext context = OwnerContext;
        if (!TrySpendCost(skill, context)) return false;

        int round = RoundManagerRef != null ? RoundManagerRef.CurrentRound : 1;
        enemy.TakeDamage(skill.ComputeDamage(round), DamageType.AP, AttackType.Spells, owner.OwnerId);

        if (skill.duration > 0f) StartCoroutine(StunRoutine(enemy, skill.duration));

        StartCooldown(skill);
        return true;
    }

    // 연금술: 유닛을 직접 지정(자기 유닛만).
    bool TryDismantleUnit(SupportSkillData skill, GameObject targetUnit)
    {
        if (skill.effect != SupportSkillEffect.UnitDismantle) return false;
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

        context.ResourceWallet?.Add(ResourceType.Mana, FindDismantleRefund(skill, identity.Data.grade));

        // 인벤토리는 UnitData 목록이 아니라 필드 인스턴스의 등록부다. Consume이 등록 해제와 파괴를
        // 한 번에 한다 — 따로 부르면 Destroy가 프레임 끝에야 처리되는 사이 인벤토리에 유령이 남는다.
        identity.Consume();

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

    // waveCount<=1이면 예전과 완전히 같은 즉발 1회(지진·폭우·버스터콜 전부 이 경로다).
    // waveCount>1이면 지속시간에 걸쳐 나눠 때린다(불비 — 원작 "1웨이브당 5만뎀, 총 4웨이브").
    // 원작의 "맞으면 초당 2.8만뎀 6초 화상"은 이번엔 생략했다 — 알려진 단순화(SUPPORT_SHOP.md 참고).
    void ApplyAreaDamage(SupportSkillData skill, Vector3 point, int round, PlayerContext context)
    {
        if (skill.waveCount > 1)
        {
            StartCoroutine(MultiWaveDamageRoutine(skill, point, round, context));
            return;
        }

        ApplyOneWaveDamage(skill, point, round, context, isFirstWave: true);
    }

    IEnumerator MultiWaveDamageRoutine(SupportSkillData skill, Vector3 point, int round, PlayerContext context)
    {
        float interval = skill.duration > 0f ? skill.duration / skill.waveCount : 1f;

        for (int wave = 0; wave < skill.waveCount; wave++)
        {
            ApplyOneWaveDamage(skill, point, round, context, isFirstWave: wave == 0);
            if (wave < skill.waveCount - 1) yield return new WaitForSeconds(interval);
        }
    }

    // isFirstWave: 방어력 감소(독약)는 캐스트당 딱 한 번만 걸려야 한다 — waveCount가 몇이든
    // 여기서 매 웨이브 걸면 웨이브 수만큼 누적돼 원작 수치(20)보다 훨씬 세진다.
    void ApplyOneWaveDamage(SupportSkillData skill, Vector3 point, int round, PlayerContext context, bool isFirstWave)
    {
        float damage = skill.ComputeDamage(round);
        int hits = 0;

        // 스킬 시전은 쿨다운(수십 초)에 묶여 있어 매 프레임 도는 경로가 아니다.
        List<EnemyDummy> targets = CollectInRadius(point, skill.radius);

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyDummy enemy = targets[i];
            if (enemy == null) continue;

            if (skill.duration > 0f && skill.waveCount <= 1) StartCoroutine(StunRoutine(enemy, skill.duration));
            if (isFirstWave && skill.armorShredOnHit > 0f) enemy.AddArmorShred(skill.armorShredOnHit);

            // 도움소 스킬은 마법 피해로 둔다 — 원작이 "마뎀은 방어력 무시, 스킬딜로 처리"라고
            // 서술한다(`UNIT_STATS_RESEARCH.md`). 스킬 피해가 방어력에 감폭되면 후반에 도움소가
            // 통째로 무의미해진다. ⚠️ 사장님 확인은 못 받은 판단이다.
            enemy.TakeDamage(damage, DamageType.AP, AttackType.Spells, owner.OwnerId);
            hits++;
        }

        if (skill.manaRefundPerHit > 0 && hits > 0 && context.ResourceWallet != null)
        {
            int refund = Mathf.Min(skill.manaRefundCap, skill.manaRefundPerHit * hits);
            if (refund > 0) context.ResourceWallet.Add(ResourceType.Mana, refund);
        }
    }

    // 해루석: 첫 타격에 대상 최대 체력 비례 피해(원작 "전체 체력의 7%")를 한 번 더 얹은 뒤
    // 구속+DoT를 건다.
    void ApplyRoot(SupportSkillData skill, Vector3 point, int round)
    {
        int ticks = Mathf.Max(1, Mathf.RoundToInt(skill.duration));
        float tickDamage = skill.ComputeDamage(round) / ticks;

        List<EnemyDummy> targets = CollectInRadius(point, skill.radius);

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyDummy enemy = targets[i];
            if (enemy == null) continue;

            if (skill.firstHitMaxHpPercent > 0f)
                enemy.TakeDamage(enemy.MaxHp * skill.firstHitMaxHpPercent, DamageType.AP, AttackType.Spells, owner.OwnerId);

            StartCoroutine(RootAndDotRoutine(enemy, tickDamage, ticks));
        }
    }

    IEnumerator RootAndDotRoutine(EnemyDummy enemy, float tickDamage, int ticks)
    {
        enemy.AddFreeze();

        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForSeconds(1f);
            if (enemy == null) yield break;   // 죽었으면 풀어줄 대상 자체가 없다
            enemy.TakeDamage(tickDamage, DamageType.AP, AttackType.Spells, owner.OwnerId);   // 위와 같은 이유
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

            StartCoroutine(BuffRoutine(attacker, skill.buffAttackPowerMultiplier, skill.duration));
        }
    }

    IEnumerator BuffRoutine(UnitAttacker attacker, float multiplier, float duration)
    {
        // 이 버프는 공격력만 건드린다(원작 확인, 2026-09-04 — 예전엔 공격속도로 잘못 만들었었다).
        // 예전 공격속도 버전처럼 값을 덮어쓰는 대신 누적 리스트에 쌓는다 — 되돌릴 때 "원래값"을
        // 기억하는 방식이면 그 사이에 영구 강화를 산 만큼이 복원할 때 지워진다.
        attacker.AddAttackPowerBuff(multiplier);

        yield return new WaitForSeconds(duration);

        if (attacker != null) attacker.RemoveAttackPowerBuff(multiplier);
    }
}
