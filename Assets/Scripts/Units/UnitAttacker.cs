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

    // ⚠️ 2026-09-06 버프 레지스트리에 편입(PM 지시) — 지속시간은 그대로 호출부
    // (SupportShop.BuffRoutine)가 Add→WaitForSeconds→Remove로 직접 관리하므로 여기서는
    // duration<=0(영구, RemoveBuff가 부를 때까지)으로 등록한다. attackSpeedBuffs 리스트
    // 자체의 배율곱 동작(AttackSpeedMultiplier)은 안 건드렸다 — 레지스트리는 "세고
    // 조회하는" 용도로만 옆에 나란히 쓴다.
    static readonly string AttackSpeedBuffId = "AttackSpeedBuff";
    static readonly string AttackPowerBuffId = "AttackPowerBuff";

    public void AddAttackSpeedBuff(float multiplier)
    {
        if (multiplier > 0f)
        {
            attackSpeedBuffs.Add(multiplier);
            AddBuff(AttackSpeedBuffId, 0f);
        }
    }

    public void RemoveAttackSpeedBuff(float multiplier)
    {
        attackSpeedBuffs.Remove(multiplier);
        RemoveBuff(AttackSpeedBuffId);
    }

    public void AddAttackPowerBuff(float multiplier)
    {
        if (multiplier > 0f)
        {
            attackPowerBuffs.Add(multiplier);
            AddBuff(AttackPowerBuffId, 0f);
        }
    }

    // SkillEffectBasis.ResearchLevel 전용 자리 — 연구소(05번, 구현담당1)가 서기 전까지는
    // 항상 0을 돌려준다(2026-09-05). 원작 예: 핸콕 "연구횟수×30,000+360,000" — 연구소가
    // 서면 이 메서드 한 줄만 실제 단계값으로 이으면 된다. 0인 동안은
    // ResolveSkillEffectValue의 "단계×multiplier+bonus"가 "0×multiplier+bonus=bonus"로
    // 예전 동작과 같다 — 회귀 없음.
    int CountResearchLevel() => 0;

    // ---- 버프 레지스트리(2026-09-06, PM 지시) — 흩어져 있던 버프류(도움소 공속·공격력
    // 버프, OnHitChance 절대쿨의 selfBuffId, 앞으로 생길 스킬 자기버프)를 한 자리에서
    // 세고 조회한다. ⚠️ 이번 범위는 "셀 수 있고 물어볼 수 있는 그릇"까지다 — 버프를
    // 실제로 거는 스킬 효과(ApplyToAlly 등)는 아직 안 만든다(PM 지시, 지금 ApplyToAlly는
    // 여전히 빈 메서드다).
    class ActiveBuff
    {
        public string id;
        public float expiresAt; // Time.time 기준. <=0이면 영구(RemoveBuff로만 없어진다).

        // ⚠️ 2026-09-06 추가(SkillEffect.buffHitCharges 전용, PM 지시) — 0이면 기존처럼
        // expiresAt(시간)로만 만료된다. 1 이상이면 "평타 N번"으로 만료되는 버프다 —
        // TickBuffHitCharges가 매 평타 끝에 하나씩 깎는다. expiresAt과 동시에 둘 다
        // 쓰지 않는다(호출부가 hitCharges>0이면 duration을 무시하고 영구(-1)로 건다,
        // 아래 AddBuff 오버로드 참고) — 섞으면 어느 쪽이 먼저 지우는지 애매해진다.
        public int hitsRemaining;

        // 버프를 건 그 평타의 TryCastOnHitSkill이 끝나면서 도는 TickBuffHitCharges 호출
        // 한 번은 건너뛴다 — 안 그러면 "이번 평타에 막 걸린 버프"가 자기 자신이 아직
        // 한 번도 안 쓰였는데 벌써 1회를 까먹어서, N회 요청했는데 N-1회만 유지된다
        // (opener 슬롯이 A09E보다 뒤에 있어 opener가 버프를 걸 때 이미 이번 평타의
        // 판정은 다 끝난 뒤라서, 이번 평타는 애초에 이 버프를 한 번도 못 썼다).
        public bool skipNextTick;
    }

    readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    // duration<=0이면 영구 — attackSpeedBuffs/attackPowerBuffs처럼 지속시간을 호출부가
    // 직접 관리(코루틴으로 Add→대기→Remove)하는 경우에 쓴다. id가 비어있으면 조용히
    // 무시한다(호출부가 selfBuffId 미기재를 실수로 빈 문자열째 넘겨도 안전하게).
    public void AddBuff(string id, float duration)
    {
        if (string.IsNullOrEmpty(id)) return;
        activeBuffs.Add(new ActiveBuff { id = id, expiresAt = duration > 0f ? Time.time + duration : -1f });
    }

    // hitCharges>0 전용 오버로드(2026-09-06, PM 지시) — "N초"가 아니라 "평타 N번" 뒤에
    // 만료된다. duration은 이 경로에서 안 쓴다(시간으로는 절대 안 죽는다, 영구(-1)로
    // 걸고 TickBuffHitCharges가 카운트로 지운다). hitCharges<=0이면 기존 시간 오버로드로
    // 그대로 위임한다(회귀 없음 — 기존 호출부·기존 자산 전부 이 분기를 안 탄다).
    public void AddBuff(string id, float duration, int hitCharges)
    {
        if (hitCharges <= 0) { AddBuff(id, duration); return; }
        if (string.IsNullOrEmpty(id)) return;
        activeBuffs.Add(new ActiveBuff { id = id, expiresAt = -1f, hitsRemaining = hitCharges, skipNextTick = true });
    }

    // TryCastOnHitSkill이 평타 하나를 다 처리한 뒤 한 번 부른다(게이지 리셋과 같은 자리).
    // hitsRemaining==0인 항목(시간 기반 버프)은 건너뛴다. skipNextTick이 서있으면(이번
    // 평타에 막 걸린 버프) 플래그만 내리고 이번 호출에선 안 깎는다 — 위 ActiveBuff.
    // skipNextTick 주석 참고.
    public void TickBuffHitCharges()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            if (activeBuffs[i].hitsRemaining <= 0) continue;
            if (activeBuffs[i].skipNextTick) { activeBuffs[i].skipNextTick = false; continue; }
            activeBuffs[i].hitsRemaining--;
            if (activeBuffs[i].hitsRemaining <= 0) activeBuffs.RemoveAt(i);
        }
    }

    // id가 일치하는 인스턴스를 하나만 지운다 — List.Remove(값)와 같은 관례
    // (attackSpeedBuffs가 이미 그렇게 짝을 맞춘다). 여러 개 겹쳐 있으면 하나만 없어진다.
    public void RemoveBuff(string id)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].id == id) { activeBuffs.RemoveAt(i); return; }
        }
    }

    public bool HasBuff(string id) => !string.IsNullOrEmpty(id) && ActiveBuffCount(id) > 0;
    public bool LacksBuff(string id) => !HasBuff(id);

    // id를 지정하면 그 버프만, null이면 전체 개수(casterBuffCountFactor용).
    int ActiveBuffCount(string id = null)
    {
        PruneExpiredBuffs();
        if (id == null) return activeBuffs.Count;
        int count = 0;
        foreach (ActiveBuff b in activeBuffs)
            if (b.id == id) count++;
        return count;
    }

    void PruneExpiredBuffs()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            if (activeBuffs[i].expiresAt > 0f && Time.time >= activeBuffs[i].expiresAt)
                activeBuffs.RemoveAt(i);
        }
    }

    // SkillEffect.casterBuffCountFactor(원작 realD = 0.03×버프개수) 전용 자리.
    // ⚠️ 2026-09-06: 버프 레지스트리가 생겨서 이제 실제로 센다(예전엔 항상 0). 원작
    // "시전자의 워크3 버프 전부"와 완전히 같지는 않다 — 지금 레지스트리에 등록되는 건
    // SupportShop 버프(AttackSpeedBuff/AttackPowerBuff)와 selfBuffId를 채운 절대쿨뿐이다
    // (적이 거는 디버프 같은 건 아직 없다). 그래도 "0으로 죽어 있진 않다."
    int CountCasterBuffs() => ActiveBuffCount();

    public void RemoveAttackPowerBuff(float multiplier)
    {
        attackPowerBuffs.Remove(multiplier);
        RemoveBuff(AttackPowerBuffId);
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
        // isAbilityDamage: false — Bash도 평타와 같은 DamageType/AttackType을 써서 방어력·
        // 상성표를 평타와 똑같이 통과시키는 게 설계 의도다(위 메서드 주석). UNIVERSAL 무시도
        // 평타와 동일하게 적용 안 한다.
        DealDamageToEnemy(target, bonus, DamageTypeOf, AttackTypeOf, armorIgnoreRatio: 0f, isAbilityDamage: false);

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
    // ApplyCritIfTriggered는 위에서 안 건드렸다. UnitData.skill/skills가 비어 있는 유닛은
    // 여기서 전부 조용히 리턴하므로 동작이 그대로다 — 사장님이 유닛별로 SkillData를
    // 배정하기 전까지는 실질적으로 죽어 있다.
    //
    // ⚠️ 2026-09-05 다중 스킬 확장(MULTI_SKILL_IMPACT.md) — 유닛 하나가 스킬을 최대 6개까지
    // 가질 수 있다(96종이 기존 1·2채널 스킬 위에 게이지·확률·절대쿨 게이트 스킬을 최대 5개
    // 더 받는다). "스킬 하나" 전제였던 것 셋을 갈랐다:
    //   · cooldownTimer·onHitChanceLockedUntil → 스킬(버프 ID)마다 독립이라 그대로 스킬별.
    //     Dictionary<SkillData, SkillRuntimeState>로 스킬 에셋을 키 삼는다.
    //   · onHitCount 카운터만 예외 — 원작이 유닛의 마나·체력 하나를 여러 스킬이 공유해서
    //     쓴다(사보 4개가 마나==125 하나를 같이 보는 실제 사례 확인, ⑤-B). 그래서 스킬별이
    //     아니라 유닛당 게이지 종류별(마나 1개·체력 1개) 공유 카운터로 뺐다.
    class SkillRuntimeState
    {
        public float cooldownTimer;          // CooldownAutoCast·Aura 전용
        public float onHitChanceLockedUntil; // OnHitChance 절대쿨 전용

        // ⚠️ 2026-09-06 추가(오라 무한누적 근본수정, PM 지시) — Aura 전용, 지금 이 오라가
        // 걸어둔 대상들(EnemyAuraCaster.affected와 같은 역할). null이면 "아직 한 번도
        // 안 돌았다"는 뜻이라 UpdateAuraTick이 처음 부를 때 지연 생성한다.
        public List<EnemyDummy> auraAffectedEnemies;
        public List<UnitIdentity> auraAffectedAllies;
        public List<string> auraSelfAppliedBuffIds;
    }

    Dictionary<SkillData, SkillRuntimeState> skillRuntimeStates;

    SkillRuntimeState GetRuntimeState(SkillData skill)
    {
        skillRuntimeStates ??= new Dictionary<SkillData, SkillRuntimeState>();
        if (!skillRuntimeStates.TryGetValue(skill, out SkillRuntimeState state))
        {
            state = new SkillRuntimeState();
            skillRuntimeStates[skill] = state;
        }
        return state;
    }

    // 유닛 공유 게이지(OnHitCount 전용, SkillGaugeKind 참고) — 스킬별이 아니라 게이지
    // 종류당 하나씩이다. 시작값은 C# 기본값 0이 아니라 그 스킬의 resetTo여야 한다(원작
    // 체력형은 체력이 이미 1에서 출발해서 첫 주기도 이후 주기와 같은 길이가 된다, PM 지시
    // 2026-09-05·리서치담당 확인) — 첫 사용 시점에 늦게 채운다(Awake 시점엔 어느 스킬을
    // 쓸지 모른다, 06번①·특성강화로 바뀔 수 있어서).
    int manaGaugeCounter;
    bool manaGaugeInitialized;
    int lifeGaugeCounter;
    bool lifeGaugeInitialized;

    // 01번 영웅 스탯(STR/AGI/INT) — 사장님 결정 2026-09-06. 원작 "적을 죽일 때마다
    // AddHeroXP(영웅, 1)"에 대응 — DealDamageToEnemy가 자기 타격으로 대상의 숨통을 끊을
    // 때만 올린다(다른 유닛이 이미 죽여둔 대상을 다시 때려도 안 오른다).
    //
    // ⚠️ 2026-09-06 단위 정정(PM 지적, 뿌리 ㉑) — AddHeroXP의 "1"은 레벨이 아니라
    // 경험치 1점이다. 워크3 레벨업 문턱은 레벨마다 수백 점씩 커지는 값이라 "킬 1회 =
    // 레벨 1"로 잘못 셌었다(원작보다 두 자릿수 배 빠르게 레벨업). heroXp는 킬마다 그대로
    // 쌓지만, heroXp→heroLevel 변환 문턱이 아직 [미확인]이라 heroLevel은 0에 고정한다 —
    // 문턱이 오면 여기 변환식만 넣으면 된다(CurrentStrength 등을 읽는 쪽은 안 건드려도
    // 된다). ⚠️ 킬 귀속 자체(죽인 유닛 하나에게만 가는가, 전체 아군에게 가는가)도
    // [미확인] — 답이 오기 전엔 이 로직을 더 정교하게 만들지 않는다(축이 바뀌면 버린다).
    int heroXp;
    int heroLevel; // 문턱 확정 전까지 항상 0 — 값을 넣어도 스탯 성장 없음(회귀 없음).

    void GainKillExperience() => heroXp++;

    public float CurrentStrength
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            return unitData != null ? unitData.baseStrength + unitData.strengthPerLevel * heroLevel : 0f;
        }
    }

    public float CurrentAgility
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            return unitData != null ? unitData.baseAgility + unitData.agilityPerLevel * heroLevel : 0f;
        }
    }

    public float CurrentIntelligence
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            return unitData != null ? unitData.baseIntelligence + unitData.intelligencePerLevel * heroLevel : 0f;
        }
    }

    // EnemyDummy.TakeDamage 앞뒤로 생사를 비교해 "이 호출이 실제로 숨통을 끊었는가"만
    // 잡는다 — TakeDamage는 이미 죽은 대상엔 맨 위에서 조용히 리턴하므로, 크리티컬
    // 보너스가 본타격 뒤에 또 들어오는 것처럼 한 공격 안에 여러 번 불려도 두 번 안 오른다.
    void DealDamageToEnemy(EnemyDummy target, float amount, DamageType damageType, AttackType attackType,
                            float armorIgnoreRatio = 0f, bool isAbilityDamage = true)
    {
        bool wasAlive = !target.IsDead;
        target.TakeDamage(amount, damageType, attackType, owner != null ? owner.OwnerId : -1,
                           armorIgnoreRatio, isAbilityDamage);
        if (wasAlive && target.IsDead) GainKillExperience();
    }

    // unitData.SkillAt(정적 데이터, UnitData.cs 참고)이 주는 슬롯 위에 런타임 오버레이
    // (06번① 능력교체형 트레잇)를 얹는다. ⚠️ 슬롯 0(첫 스킬)에만 적용한다 — 06번① 15종은
    // 지금 전부 스킬이 하나뿐이라(레거시 skill 필드) 슬롯0=유일한 스킬이라 안 갈린다. 한
    // 유닛이 06번①과 이번 다중스킬(96종)을 동시에 가지면(지금은 없음) 슬롯0만 교체된다는
    // 한계가 남는다 — 실제로 겹치면 그때 다시 설계할 것.
    SkillData ResolveSkillAt(UnitData unitData, int index)
    {
        if (index == 0)
        {
            UnitUpgrades source = ResolveUpgrades();
            SkillData replacement = source != null ? source.ReplacementSkillFor(unitData) : null;
            if (replacement != null) return replacement;
        }
        return unitData.SkillAt(index);
    }

    // 06번① 완료: 스킬승급형 트레잇(UnitTraitData.skillLevelUnlockIndex)이 UnitUpgrades에
    // 걸려 있으면 그 레벨을, 없으면 레벨1(index 0)을 쓴다. 원작이 "레벨2 = 레벨1 그대로 +
    // 새 효과"로 만들어서(수치 배율이 아니다) 인덱스만 바꾸는 것으로 충분하다 — 레벨1/2
    // 각각의 SkillLevel.effects 자체를 SkillData 에셋 쪽에서 이미 완결된 목록으로 담아둔다.
    // ⚠️ 이 인덱스는 스킬이 아니라 유닛 단위로 정해진다(SkillLevelIndexFor(unitData)) — 한
    // 유닛이 스킬을 여러 개 가지면 전부 같은 인덱스를 쓴다. 06번①은 지금 스킬 1개뿐인
    // 유닛만 써서 문제가 없다 — 다중 스킬 유닛이 스킬승급형 트레잇도 갖는 사례가 생기면
    // 그때 "어느 스킬의 레벨을 올릴지"를 다시 설계해야 한다.
    // 원작 능력 레벨(1-based). 우리 levels 인덱스는 0-based라 +1 한다 —
    // SkillEffectBasis.CasterSkillLevel이 읽는다. 업그레이드가 아직 없으면 인덱스 0 = 레벨 1.
    int CurrentSkillLevelNumber()
    {
        UnitData unitData = identity != null ? identity.Data : null;
        UnitUpgrades source = ResolveUpgrades();
        int index = (unitData != null && source != null) ? source.SkillLevelIndexFor(unitData) : 0;
        return Mathf.Max(0, index) + 1;
    }

    SkillLevel CurrentSkillLevel(SkillData skill)
    {
        if (skill.levels == null || skill.levels.Count == 0) return null;

        int index = 0;
        UnitData unitData = identity != null ? identity.Data : null;
        UnitUpgrades source = ResolveUpgrades();
        if (unitData != null && source != null) index = source.SkillLevelIndexFor(unitData);

        return skill.levels[Mathf.Clamp(index, 0, skill.levels.Count - 1)];
    }

    // SkillLevel.requiredBuffId/forbiddenBuffId 게이트(2026-09-06) — 둘 다 비어있으면
    // 무조건 통과한다(기존 102개 게이트는 전부 비어있어 회귀 없음). CooldownAutoCast·
    // Aura(UpdateSkillCooldown)·OnHitChance·OnHitCount(TryCastOnHitSkill) 네 발동방식이
    // 전부 같은 판정을 쓴다.
    //
    // ⚠️ 06번①(2026-09-06, PM 승인) — requiredTargetBuffId/forbiddenTargetBuffId 추가.
    // 캐스터 자신이 아니라 **대상(적)의** 버프 레지스트리(EnemyDummy.HasBuff)를 본다.
    // target이 null이면(Aura·CooldownAutoCast처럼 게이트 검사 시점에 아직 대상을 안
    // 고른 경로) 두 필드 중 하나라도 채워져 있으면 무조건 막는다 — "대상 없음=통과"로
    // 두면 오라가 조건 없이 나가버린다(SkillData.cs SkillLevel 주석 참고). target이
    // null이어도 두 필드가 전부 비어있으면(기존 전 자산) 이 분기 자체를 안 타 회귀 없다.
    bool PassesBuffGate(SkillLevel level, EnemyDummy target)
    {
        if (!string.IsNullOrEmpty(level.requiredBuffId) && !HasBuff(level.requiredBuffId)) return false;
        if (!string.IsNullOrEmpty(level.forbiddenBuffId) && HasBuff(level.forbiddenBuffId)) return false;

        bool hasTargetGate = !string.IsNullOrEmpty(level.requiredTargetBuffId) || !string.IsNullOrEmpty(level.forbiddenTargetBuffId);
        if (hasTargetGate)
        {
            if (target == null) return false;
            if (!string.IsNullOrEmpty(level.requiredTargetBuffId) && !target.HasBuff(level.requiredTargetBuffId)) return false;
            if (!string.IsNullOrEmpty(level.forbiddenTargetBuffId) && target.HasBuff(level.forbiddenTargetBuffId)) return false;
        }
        return true;
    }

    // CooldownAutoCast·Aura 전용 — OnHitChance·OnHitCount는 평타가 실제로 맞았을 때만
    // 판정해야 해서 Update()의 공격 성공 분기에서 TryCastOnHitSkill로 따로 부른다.
    void UpdateSkillCooldown()
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;

        int count = unitData.SkillCount;
        for (int i = 0; i < count; i++)
        {
            SkillData skill = ResolveSkillAt(unitData, i);
            if (skill == null) continue;
            if (skill.triggerType != SkillTriggerType.CooldownAutoCast && skill.triggerType != SkillTriggerType.Aura)
                continue;

            SkillLevel level = CurrentSkillLevel(skill);
            if (level == null) continue;

            // 06번① 순위배정으로 13기의 UnitData.skill이 null이 아니게 됐지만 levels의
            // effects는 전부 빈 배열이다(수치 미상, 자리만 있음) — 여기서 걸러서 쿨다운
            // 타이머 자체가 돌지 않게 한다. 안 그러면 "숫자만 없다"가 아니라 "빈 채로 계속
            // 돌고 있다"가 된다(PM 지시, 2026-09-05).
            if (level.effects == null || level.effects.Count == 0) continue;

            SkillRuntimeState state = GetRuntimeState(skill);

            // ⚠️ 2026-09-06 근본수정(PM 지시) — Aura는 CooldownAutoCast와 다르게
            // "재시전"이 아니라 "계속 켜져 있는 지속효과"다. 예전엔 둘을 같은 코드로
            // 다뤄서 Aura도 1초마다 CastSkillLevel을 다시 불렀는데, ArmorBonus/
            // HealOverTime/ApplyBuff를 duration<=0(영구)으로 걸면 "이미 걸었나" 추적이
            // 없어 매 틱 armorShred/버프 인스턴스가 무한히 쌓인다(EnemyAuraCaster는 이미
            // 대상 추적으로 이 문제가 없다 — 플레이어 쪽만 비대칭이었다). Aura는 여기서
            // 갈라 UpdateAuraTick으로 보낸다(대상 추적 + Apply-once/Remove-on-exit).
            if (skill.triggerType == SkillTriggerType.Aura)
            {
                // 오라는 쿨다운 개념이 없다("계속 켜져 있다") — 매 프레임 판정하면 값이
                // 생겼을 때 폭증하니 1초 주기로만 갱신한다(예전과 같은 주기).
                state.cooldownTimer -= Time.deltaTime;
                if (state.cooldownTimer > 0f) continue;
                state.cooldownTimer = 1f;

                // target=null 게이트 검사는 여기선 "새로 걸 수 있는지"만 정한다 — 게이트가
                // 막혀 있어도 이미 걸려 있던 효과는 UpdateAuraTick 안에서 정상적으로
                // 걷어낸다(범위를 "전부 벗어난 것"으로 취급).
                UpdateAuraTick(level, state, PassesBuffGate(level, null));
                continue;
            }

            state.cooldownTimer -= Time.deltaTime;
            if (state.cooldownTimer > 0f) continue;

            // 버프 게이트(requiredBuffId/forbiddenBuffId, 2026-09-06) — 쿨다운이 다 돼도
            // 이 조건을 못 넘으면 시전하지 않는다. 타이머는 일부러 안 되돌린다 — 막힌
            // 동안 매 프레임 다시 검사하다가 조건이 풀리는 순간 그 프레임에 바로 나간다.
            // target=null — CooldownAutoCast는 이 시점에 아직 대상을 안 골랐다(대상은
            // CastSkillLevel 안에서 나중에 정해진다). 이 스킬에 requiredTargetBuffId/
            // forbiddenTargetBuffId가 채워져 있으면 PassesBuffGate가 target==null을 보고
            // 무조건 막는다 — 대상 버프 게이트를 가진 스킬은 CooldownAutoCast로는 못
            // 쓴다는 뜻이고, 지금은 그런 자산이 없어 회귀 없다.
            if (!PassesBuffGate(level, null)) continue;

            state.cooldownTimer = Mathf.Max(0.01f, level.cooldown);
            // recentAttackDamage: 0 — CooldownAutoCast는 "방금 맞은 평타"라는 문맥 자체가
            // 없다(TryCastOnHitSkill 쪽만 있음, 아래 참고). ReceivedDamage basis를 쓰는
            // 효과가 이 경로를 타면 0(적용 안 함)으로 안전하게 빠진다.
            CastSkillLevel(level, level.range, null, 0f);
        }
    }

    // ---- Aura 지속효과 — 대상 추적(Apply-once/Remove-on-exit), EnemyAuraCaster와 같은
    // 설계를 플레이어 쪽에 옮긴 것. ⚠️ 범위: ArmorBonus·HealOverTime·ApplyBuff(=지속형
    // kind) 셋만 다룬다 — Damage·Stun·ArmorBreak·ExtraProjectile은 "매 틱 다시 낸다"는
    // 뜻이 자연스러운 즉발형이라 이 경로에 안 들어온다(지금 Aura 자산 전부(H094 1건)
    // ArmorBonus뿐이라 즉발형이 Aura에 실제로 쓰인 사례가 없다 — EnemyAuraCaster도 같은
    // 전제로 Allies-target 외엔 아예 안 본다). 나중에 즉발형을 Aura에 쓸 사례가 생기면
    // 그때 이 전제를 다시 봐야 한다.

    void UpdateAuraTick(SkillLevel level, SkillRuntimeState state, bool gatePasses)
    {
        state.auraAffectedEnemies ??= new List<EnemyDummy>();
        state.auraAffectedAllies ??= new List<UnitIdentity>();
        state.auraSelfAppliedBuffIds ??= new List<string>();

        // Self 효과 — 범위 개념이 없다(캐스터 자기 자신). 게이트가 막히면 뗀다, 풀리면
        // 다시 건다 — 한 번 걸고 다시 안 떼는 게 아니라 "지금 켜져 있는가"를 그대로
        // 따른다(다른 두 타겟과 같은 규칙).
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Self || effect.kind != SkillEffectKind.ApplyBuff) continue;
            if (string.IsNullOrEmpty(effect.buffId)) continue;

            bool alreadyApplied = state.auraSelfAppliedBuffIds.Contains(effect.buffId);
            if (gatePasses && !alreadyApplied)
            {
                AddBuff(effect.buffId, 0f);
                state.auraSelfAppliedBuffIds.Add(effect.buffId);
            }
            else if (!gatePasses && alreadyApplied)
            {
                RemoveBuff(effect.buffId);
                state.auraSelfAppliedBuffIds.Remove(effect.buffId);
            }
        }

        // Enemies 타겟 지속효과 — 게이트가 막히면 범위를 "전부 벗어난 것"으로 취급해
        // 기존에 걸어둔 효과를 전부 걷어낸다.
        List<EnemyDummy> enemiesInRange = new List<EnemyDummy>();
        if (gatePasses)
        {
            foreach (EnemyDummy enemy in EnemyDummy.Active)
            {
                if (enemy == null) continue;
                if (level.range > 0f && Vector3.Distance(enemy.transform.position, transform.position) > level.range) continue;
                enemiesInRange.Add(enemy);
            }
        }
        for (int i = state.auraAffectedEnemies.Count - 1; i >= 0; i--)
        {
            EnemyDummy target = state.auraAffectedEnemies[i];
            if (target == null || !enemiesInRange.Contains(target))
            {
                if (target != null) RemovePersistentAuraEffectsFromEnemy(level, target);
                state.auraAffectedEnemies.RemoveAt(i);
            }
        }
        foreach (EnemyDummy target in enemiesInRange)
        {
            if (state.auraAffectedEnemies.Contains(target)) continue;
            ApplyPersistentAuraEffectsToEnemy(level, target);
            state.auraAffectedEnemies.Add(target);
        }

        // Allies 타겟 지속효과 — 같은 규칙.
        List<UnitIdentity> alliesInRange = gatePasses && identity != null
            ? UnitIdentity.AlliesOf(identity, level.range)
            : new List<UnitIdentity>();
        for (int i = state.auraAffectedAllies.Count - 1; i >= 0; i--)
        {
            UnitIdentity ally = state.auraAffectedAllies[i];
            if (ally == null || !alliesInRange.Contains(ally))
            {
                if (ally != null) RemovePersistentAuraEffectsFromAlly(level, ally);
                state.auraAffectedAllies.RemoveAt(i);
            }
        }
        foreach (UnitIdentity ally in alliesInRange)
        {
            if (state.auraAffectedAllies.Contains(ally)) continue;
            ApplyPersistentAuraEffectsToAlly(level, ally);
            state.auraAffectedAllies.Add(ally);
        }
    }

    void ApplyPersistentAuraEffectsToEnemy(SkillLevel level, EnemyDummy target)
    {
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Enemies) continue;
            switch (effect.kind)
            {
                case SkillEffectKind.ArmorBonus:
                case SkillEffectKind.HealOverTime:
                    target.ApplyAllyAuraEffect(effect);
                    break;
                case SkillEffectKind.ApplyBuff:
                    target.AddBuff(effect.buffId, 0f);
                    break;
            }
        }
    }

    void RemovePersistentAuraEffectsFromEnemy(SkillLevel level, EnemyDummy target)
    {
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Enemies) continue;
            switch (effect.kind)
            {
                case SkillEffectKind.ArmorBonus:
                case SkillEffectKind.HealOverTime:
                    target.RemoveAllyAuraEffect(effect);
                    break;
                case SkillEffectKind.ApplyBuff:
                    target.RemoveBuff(effect.buffId);
                    break;
            }
        }
    }

    void ApplyPersistentAuraEffectsToAlly(SkillLevel level, UnitIdentity ally)
    {
        UnitAttacker allyAttacker = ally != null ? ally.GetComponent<UnitAttacker>() : null;
        if (allyAttacker == null) return;
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Allies || effect.kind != SkillEffectKind.ApplyBuff) continue;
            allyAttacker.AddBuff(effect.buffId, 0f);
        }
    }

    void RemovePersistentAuraEffectsFromAlly(SkillLevel level, UnitIdentity ally)
    {
        UnitAttacker allyAttacker = ally != null ? ally.GetComponent<UnitAttacker>() : null;
        if (allyAttacker == null) return;
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Allies || effect.kind != SkillEffectKind.ApplyBuff) continue;
            allyAttacker.RemoveBuff(effect.buffId);
        }
    }

    void TryCastOnHitSkill(EnemyDummy attackedTarget)
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;

        int count = unitData.SkillCount;
        if (count == 0) return;

        // 공유 게이지(OnHitCount)는 이 평타 한 번에 게이지 종류당 최대 한 번만 올린다 —
        // 스킬마다 올리면 스킬이 많은 유닛일수록 게이지가 그만큼 빨리 차서, 원작의 "유닛
        // 마나 하나가 딱 1씩 오른다"가 깨진다(⑤-B 사보 예시 참고).
        bool manaIncremented = false;
        bool lifeIncremented = false;

        // ⚠️ 2026-09-05 버그 수정(PM 지적): 리셋을 그 자리에서 바로 하면(예전 코드)
        // 사보처럼 스킬 4개가 마나 125 하나를 같이 보는 경우, 루프 앞쪽 스킬이 임계에
        // 닿아 카운터를 0으로 되돌린 뒤 — **같은 타에 같이 발동해야 할 뒤쪽 스킬**이 그
        // 리셋된(0인) 값을 자기 임계값과 비교해 "안 닿음"으로 읽고 건너뛴다. 즉 4개가
        // 동시에 나가야 할 게 1개만 나가고 나머지 3개가 이번 주기를 통째로 놓친다.
        // → 리셋을 루프 안에서 즉시 하지 않고, 이번 평타에서 실제로 발동(임계 도달)한
        // 스킬이 있었는지만 기록해뒀다가 **루프가 끝난 뒤 한 번만** 적용한다 — 그래야
        // 같은 게이지·같은 타를 보는 다른 스킬들도 리셋 전 값(임계 도달 상태)을 그대로
        // 본다. 서로 다른 임계값을 같은 게이지에 섞어 쓰는 사례는 0건으로 확인됐다
        // (리서치담당) — 있다면 이 근사(먼저 도달한 스킬의 resetTo로 전체를 되돌림)가
        // 정확하진 않지만, 최소한 죽지는 않는다.
        bool manaShouldReset = false;
        int manaResetValue = 0;
        bool lifeShouldReset = false;
        int lifeResetValue = 0;

        for (int i = 0; i < count; i++)
        {
            SkillData skill = ResolveSkillAt(unitData, i);
            if (skill == null) continue;
            if (skill.triggerType != SkillTriggerType.OnHitChance && skill.triggerType != SkillTriggerType.OnHitCount)
                continue;

            SkillLevel level = CurrentSkillLevel(skill);
            if (level == null || level.effects == null || level.effects.Count == 0) continue;

            // 버프 게이트(requiredBuffId/forbiddenBuffId, 2026-09-06) — OnHitChance·
            // OnHitCount 둘 다 판정 시작 전에 먼저 걸린다. 원작 예: 드래곤 "B00J 미보유",
            // 루피 "B06Y 미보유 AND 1/80"(뒤의 확률은 아래 triggerChance가 그대로 처리).
            // ⚠️ 06번①(2026-09-06) — attackedTarget을 그대로 넘겨 requiredTargetBuffId/
            // forbiddenTargetBuffId(대상의 버프)도 같이 판정한다. 원작 예: B06B(신세계
            // 광폭화 몬스터 전용) — "대상이 이 상태일 때만 발동"은 캐스터가 아니라
            // attackedTarget의 버프를 봐야 한다.
            if (!PassesBuffGate(level, attackedTarget)) continue;

            if (skill.triggerType == SkillTriggerType.OnHitChance)
            {
                // 절대쿨(SkillLevel.cooldown 주석 참고, PM 지시 2026-09-05) — 원작은 버프
                // 검사가 바깥 if라서, 잠긴 동안은 확률 판정까지 안 간다. cooldown<=0이면 이
                // 줄이 항상 통과해 기존 동작과 완전히 같다(회귀 없음).
                SkillRuntimeState state = GetRuntimeState(skill);
                if (level.cooldown > 0f && Time.time < state.onHitChanceLockedUntil) continue;

                if (Random.value >= level.triggerChance) continue;

                if (level.cooldown > 0f)
                {
                    state.onHitChanceLockedUntil = Time.time + level.cooldown;
                    // selfBuffId를 채운 경우에만 이 절대쿨을 버프 레지스트리에도 등록한다
                    // (2026-09-06) — 다른 스킬의 requiredBuffId/forbiddenBuffId가 이 잠금을
                    // 조회할 수 있게 하는 자리다. 비어있으면(기존 21종 전부) 아무 일도 안
                    // 하고 그대로 지나간다 — 절대쿨 자체(위 두 줄)는 이 필드와 무관하게
                    // 계속 SkillRuntimeState 기반으로 돈다, 회귀 없음.
                    if (!string.IsNullOrEmpty(level.selfBuffId)) AddBuff(level.selfBuffId, level.cooldown);
                }
            }
            else
            {
                // OnHitCount: 확률이 아니라 "정확히 N타째" — 원작 특성 24건이 이렇다
                // (SkillTriggerType.OnHitCount 주석 참고). 카운터는 게이지 종류별로 이
                // 유닛이 공유한다(위 manaGaugeCounter/lifeGaugeCounter 주석 참고) — 첫
                // 사용 시점에 resetTo로 시작값을 늦게 채운다(그래야 첫 주기도 이후 주기와
                // 같은 길이가 된다).
                if (level.gaugeKind == SkillGaugeKind.Mana)
                {
                    if (!manaGaugeInitialized) { manaGaugeCounter = level.resetTo; manaGaugeInitialized = true; }
                    if (!manaIncremented) { manaGaugeCounter++; manaIncremented = true; }
                    if (manaGaugeCounter < level.hitCountThreshold) continue;
                    manaShouldReset = true;
                    manaResetValue = level.resetTo;
                }
                else
                {
                    if (!lifeGaugeInitialized) { lifeGaugeCounter = level.resetTo; lifeGaugeInitialized = true; }
                    if (!lifeIncremented) { lifeGaugeCounter++; lifeIncremented = true; }
                    if (lifeGaugeCounter < level.hitCountThreshold) continue;
                    lifeShouldReset = true;
                    lifeResetValue = level.resetTo;
                }

                // ⚠️ 2026-09-06 추가(구현담당1 요청, PM 지시): 원작은 「게이지 AND 확률」
                // 조합이 있다 — 임계에 닿아도 그걸로 끝이 아니라 SkillLevel.triggerChance로
                // 2차 확률 판정을 한 번 더 한다. 기존 자산은 전부 triggerChance=1f(기본값)라
                // 이 줄이 항상 통과해 회귀가 없다. ⚠️ 게이지 리셋은 확률과 별개 블록이다
                // (원작도 그렇다) — 위에서 이미 manaShouldReset/lifeShouldReset을 세팅한
                // *뒤에* 이 판정을 하므로, 확률에 실패해 여기서 continue해도 리셋 예약은
                // 그대로 살아서 루프 끝에 적용된다. 발동(CastSkillLevel)만 건너뛴다.
                if (Random.value >= level.triggerChance) continue;
            }

            // recentAttackDamage: 방금 이 평타로 실제로 나간 피해량(AttackDamage) — 원작
            // GetEventDamage()에 대응한다. OnHitChance/OnHitCount는 "평타가 맞았을 때"만
            // 도는 경로라 이 값이 항상 뜻이 통한다(아래 ResolveSkillEffectValue.
            // ReceivedDamage 참고, 2026-09-06 PM 지시로 연결).
            CastSkillLevel(level, level.range, attackedTarget, AttackDamage);
        }

        // 루프가 다 끝난 뒤에 한 번만 리셋한다 — 위 주석 참고.
        if (manaShouldReset) manaGaugeCounter = manaResetValue;
        if (lifeShouldReset) lifeGaugeCounter = lifeResetValue;

        // 평타 하나가 끝났다 — "평타 N회" 버프(SkillEffect.buffHitCharges)를 여기서 한
        // 번 깎는다. 이번 평타에서 opener가 막 건 버프도 같이 깎이지만 그래도 안전하다 —
        // hitsRemaining을 N으로 걸었으므로 "이 평타 이후 N번"이 정확히 나온다(이 평타
        // 자체는 게이트 판정에서 이미 버프가 없는 채로 통과됐다, 위 PassesBuffGate 호출이
        // 이 Tick보다 먼저 돈다).
        TickBuffHitCharges();
    }

    // primaryTarget: OnHitChance가 이미 골라둔 대상(SingleTarget 효과가 우선 이걸 쓴다).
    // 없으면(쿨다운·오라) 사거리 안에서 새로 고른다. recentAttackDamage: 이 발동을 일으킨
    // 평타의 피해량(SkillEffectBasis.ReceivedDamage 전용, 없으면 0 — UpdateSkillCooldown이
    // 그렇게 부른다).
    void CastSkillLevel(SkillLevel level, float range, EnemyDummy primaryTarget, float recentAttackDamage)
    {
        if (level.effects == null) return;

        foreach (SkillEffect effect in level.effects)
        {
            if (effect == null || Random.value >= effect.chance) continue;
            ApplySkillEffect(effect, range, primaryTarget, recentAttackDamage);
        }
    }

    // 데이터 사고 방지 — target이 Enemies/Allies인데 range<=0이면 거리 검사 자체가 빠져
    // 맵 전체(EnemyDummy.Active/AlliesOf 전원)를 때린다(check_required_fields.py #14가 같은
    // 위험을 데이터 단계에서 잡는다). 핸콕에서 실제로 760을 0으로 비워둔 채 커밋할 뻔했다
    // (2026-09-05, PM 지시로 런타임에도 가드 추가). 콘솔이 도배되지 않게 한 번만 찍는다.
    static bool loggedUnboundedRange;

    void ApplySkillEffect(SkillEffect effect, float range, EnemyDummy primaryTarget, float recentAttackDamage)
    {
        if (range <= 0f &&
            (effect.target == SkillTargetKind.Enemies || effect.target == SkillTargetKind.Allies))
        {
            if (!loggedUnboundedRange)
            {
                loggedUnboundedRange = true;
                Debug.LogWarning($"{name}: {effect.target} 효과의 range가 {range}(<=0)라 시전을 " +
                                 "건너뛴다 — 데이터 확인 필요(SkillLevel.range).", this);
            }
            return;
        }

        switch (effect.target)
        {
            case SkillTargetKind.Enemies:
                foreach (EnemyDummy enemy in EnemyDummy.Active)
                {
                    if (enemy == null) continue;
                    if (range > 0f && Vector3.Distance(enemy.transform.position, transform.position) > range) continue;
                    ApplyToEnemy(effect, enemy, recentAttackDamage);
                }
                break;

            case SkillTargetKind.SingleTarget:
                EnemyDummy target = primaryTarget != null ? primaryTarget : FindClosestEnemyWithin(range);
                if (target != null) ApplyToEnemy(effect, target, recentAttackDamage);
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
    // 통하는 게 없다.
    //
    // ApplyBuff(2026-09-06)가 **아군에게 뜻이 통하는 첫 효과다** — Self(자기 자신,
    // ApplySkillEffect가 identity를 그대로 넘긴다)와 Allies 둘 다 여기로 온다. ally의
    // UnitAttacker를 찾아 그쪽 버프 레지스트리에 건다(캐스터인 this가 아니라 대상인
    // ally에게 걸리는 게 맞다 — "자기 자신에게 버프"도 ally==identity==this인 경우다).
    void ApplyToAlly(SkillEffect effect, UnitIdentity ally)
    {
        if (effect.kind != SkillEffectKind.ApplyBuff) return;

        UnitAttacker allyAttacker = ally != null ? ally.GetComponent<UnitAttacker>() : null;
        if (allyAttacker == null) return;

        // buffHitCharges>0이면 "N초"가 아니라 "평타 N번"으로 만료된다(2026-09-06,
        // SkillEffect.buffHitCharges 주석 참고) — 이 대상(ally, Self 포함) 자신의
        // TryCastOnHitSkill이 그 카운트다운을 돈다.
        allyAttacker.AddBuff(effect.buffId, effect.duration, effect.buffHitCharges);
    }

    // recentAttackDamage: SkillEffectBasis.ReceivedDamage 전용 — 이 효과를 일으킨 평타의
    // 피해량(원작 GetEventDamage(), 2026-09-06 PM 지시로 연결). CooldownAutoCast/Aura
    // 경로에선 그런 문맥이 없어 0이 들어온다(CastSkillLevel 주석 참고).
    //
    // ⚠️ 2026-09-06 추가(리서치담당 전수, PM 지시): 원작 RRD(시전자, 대상, c, min, max,
    // 공격타입, 피해타입)의 "실제 피해 = c × GetRandomReal(min, max)"에서 지금까지 c만
    // 옮기고 4·5번째 인자(난수 배율)를 통째로 무시해왔다(RRD 649건 중 152건=23.4%가
    // 배율≠1). basis가 무엇이든 c 전체를 감싸는 배율이라 — basis별 switch 안쪽이 아니라
    // **여기 최종 결과 하나에만 곱한다**(RandomDamageMultiplier 참고).
    float ResolveSkillEffectValue(SkillEffect effect, EnemyDummy target, float recentAttackDamage)
    {
        return ResolveBaseSkillEffectValue(effect, target, recentAttackDamage) * RandomDamageMultiplier(effect);
    }

    // 원작 RRD의 GetRandomReal(min, max) 부분. 원작이 매 시전마다 새로 굴리므로 여기서도
    // 캐싱 없이 그렇게 한다.
    //
    // ⚠️ min==0 && max==0이면 필드가 비어있던 것으로 보고 1.0(배율 없음)으로 읽는다 —
    // 필드 기본값 자체도 1f로 선언해뒀지만(SkillData.cs randMin/randMax), 어떤 경로로든
    // 0,0이 들어와도 기존 302개 효과의 피해가 조용히 전멸하지 않게 여기서 한 번 더
    // 막는다(PM 지시 — acceptedGrades·range 0·casterBuffCountFactor와 같은 사고 꼴).
    // 실제로 원작이 0..X 배율을 쓰는 행이 있다면 min을 0이 아닌 아주 작은 값으로 잡을
    // 것 — "정확히 0,0"만 이 안전장치에 걸린다.
    float RandomDamageMultiplier(SkillEffect effect)
    {
        if (effect.randMin == 0f && effect.randMax == 0f) return 1f;
        return Random.Range(effect.randMin, effect.randMax);
    }

    float ResolveBaseSkillEffectValue(SkillEffect effect, EnemyDummy target, float recentAttackDamage)
    {
        switch (effect.basis)
        {
            // ⚠️ 2026-09-05 버그 수정(구현담당1 발견, PM 확인): SkillData.cs:126의
            // hitCountThreshold 위 필드 주석 "비례식의 +상수항"은 basis 전체에 적용되는
            // 뜻인데, 아래 %체력 두 케이스가 bonus를 빼먹고 있었다 — 거프(h04C)의
            // "(6,000,000 + maxHP×0.05)"에서 600만이 통째로 증발하는 실피해 버그였다.
            //
            // Flat은 여기서 안 고친다(PM 지시) — "고정값 그 자체"라는 정의상 bonus를 더하는
            // 게 오히려 basis 의미를 흐린다. 관례상 Flat엔 bonus가 항상 0이라 지금은 안
            // 터지는데, 누가 채우면 조용히 사라진다 — 코드가 아니라
            // check_required_fields.py(#13, basis=Flat인데 bonus≠0)로 막는다.
            case SkillEffectBasis.Flat: return effect.multiplier;
            // %체력 분기는 "이 대상이 %체력기를 타는가" 게이트가 먼저다(원작
            // GetUnitPointValue(대상)<200 — 보스는 200 이상이라 이 분기 자체를 건너뛰고 별도
            // 고정값 분기로 간다). bonus는 **게이트 안쪽**이다 — 게이트에 막히면 상수항도 같이
            // 0이어야 한다(상수항만 나가면 원작과 다르다). EnemyData.takesPercentDamage 참고.
            // 감수성 계수(PercentDamageTakenMultiplier)는 여기서 안 곱한다 — 아래
            // DealSkillDamage에서 스킬 피해 전반에 곱한다.
            case SkillEffectBasis.TargetMaxHpPercent:
                return target.TakesPercentDamage ? target.MaxHp * effect.multiplier + effect.bonus : 0f;
            case SkillEffectBasis.TargetCurrentHpPercent:
                return target.TakesPercentDamage ? target.Hp * effect.multiplier + effect.bonus : 0f;
            case SkillEffectBasis.CasterAttackPower: return AttackDamage * effect.multiplier + effect.bonus;
            // 연구단계 × multiplier + bonus 꼴을 명시적으로 쓴다(원작 예: 핸콕 "연구횟수×
            // 30,000+360,000") — 예전엔 "연구단계 0"을 암묵적으로 가정해 bonus만 돌려줬는데,
            // 연구소(05번, 구현담당1)가 서는 순간 조용히 틀렸을 것이다(연구단계가 안 곱해져
            // 360,000에서 안 늘어남). CountResearchLevel()이 자리만 만들고 지금 0을 돌려주므로
            // 당장은 결과가 이전과 같다(0×multiplier+bonus=bonus) — 회귀 없음.
            case SkillEffectBasis.ResearchLevel: return CountResearchLevel() * effect.multiplier + effect.bonus;
            // ⚠️ 2026-09-06 연결(PM 지시): 원작 CSV의 ReceivedDamage 17행 전부 게이트가
            // "(게이트 없음)" 아니면 "MANA/LIFE게이지…"다 — 전부 OnHitChance/OnHitCount,
            // 즉 "평타가 맞았을 때"만 도는 경로다. GetEventDamage()는 새 아키텍처(적이
            // 피격에 반응)가 아니라 **그 순간 방금 나간 평타 자신의 피해량**이었다 —
            // TryCastOnHitSkill이 그 값을 이미 알고 있어서(AttackDamage), CastSkillLevel부터
            // 여기까지 recentAttackDamage로 그대로 흘려보내면 끝이었다. 새 훅이 필요 없었다.
            case SkillEffectBasis.ReceivedDamage: return recentAttackDamage * effect.multiplier + effect.bonus;
            // 2026-09-06(PM 직접 배선, 구현담당3 무응답). 원작 "상수 x (1 + 0.01 x 이속 x 계수)"를
            // 전개한 꼴이라 데이터 쪽 multiplier에 (상수 x 0.01 x 계수)가 들어온다. 대상이
            // 없거나(target null) data가 아직 없으면 이속 0 -> bonus만 남는다.
            // ⚠️ 야마토의 반비례 꼴은 여기 안 담긴다(SkillEffectBasis 주석 참고).
            case SkillEffectBasis.TargetMoveSpeed:
                return (target != null ? target.MoveSpeed : 0f) * effect.multiplier + effect.bonus;
            // 시전자 게이지 비례. 지금은 마나 게이지만 읽는다 — 원작의 이 꼴 5행이 전부
            // 부릉냐(마나)라서다. 게이지는 lazily 초기화되므로 아직 안 돌았으면 0이고,
            // 그 경우 bonus만 남는다(원작의 "게이지 0" 상태와 같다).
            case SkillEffectBasis.CasterGaugeValue:
                return manaGaugeCounter * effect.multiplier + effect.bonus;
            // 원작 능력 레벨(1 또는 2) x multiplier + bonus. 우리 levels 인덱스 +1이 원작
            // 레벨과 1:1이다(CurrentSkillLevel의 index 계산과 같은 자리를 쓴다).
            case SkillEffectBasis.CasterSkillLevel:
                return CurrentSkillLevelNumber() * effect.multiplier + effect.bonus;
            // 01번 영웅 스탯(STR/AGI/INT, 사장님 결정 2026-09-06) — 원작
            // GetHeroStatBJ(영웅, STR/AGI/INT, true) x multiplier + bonus 대응. UnitData의
            // baseX/xPerLevel이 전부 0f인 지금은 항상 0을 돌려준다(회귀 없음).
            case SkillEffectBasis.CasterStrength: return CurrentStrength * effect.multiplier + effect.bonus;
            case SkillEffectBasis.CasterAgility: return CurrentAgility * effect.multiplier + effect.bonus;
            case SkillEffectBasis.CasterIntelligence: return CurrentIntelligence * effect.multiplier + effect.bonus;
            default: return 0f;
        }
    }

    // 적 하나에게 효과 하나를 적용한다 — kind별로 갈린다. ApplySkillEffect의 Enemies/
    // SingleTarget 갈래가 여길 거친다(Self/Allies는 ApplyToAlly). recentAttackDamage는
    // SkillEffectBasis.ReceivedDamage 전용(위 ResolveSkillEffectValue 참고) — Damage가
    // 아닌 kind는 그냥 무시한다.
    void ApplyToEnemy(SkillEffect effect, EnemyDummy target, float recentAttackDamage)
    {
        // 효과 단위 대상 버프 게이트(06번①-2, 2026-09-06) — SkillLevel의 게이트와
        // 별개다(위 SkillData.cs SkillEffect.requiredTargetBuffId 주석 참고). Enemies
        // AoE면 target이 매번 다른 개체라 이 검사도 개체마다 다시 돈다 — 그래서 "AoE
        // 중 버프 있는 놈만" 같은 원작 구조가 자연히 나온다.
        if (!string.IsNullOrEmpty(effect.requiredTargetBuffId) && (target == null || !target.HasBuff(effect.requiredTargetBuffId)))
            return;
        if (!string.IsNullOrEmpty(effect.forbiddenTargetBuffId) && target != null && target.HasBuff(effect.forbiddenTargetBuffId))
            return;

        switch (effect.kind)
        {
            case SkillEffectKind.Damage:
                DealSkillDamage(effect, target, recentAttackDamage);
                break;

            // 일반 행동정지 스턴만이다 — 원작의 "게이지를 미는 스턴"(신세계 사이드보스
            // 전용, 우리에 그 시스템 자체가 없다)은 안 만든다. SupportShop.StunRoutine·
            // UnitAttacker.CritStunRoutine과 같은 패턴: AddFreeze/RemoveFreeze는 겹침
            // 횟수를 세므로 다른 스턴원과 동시에 걸려도 서로를 밀어내지 않는다.
            case SkillEffectKind.Stun:
                if (effect.duration > 0f) StartCoroutine(SkillStunRoutine(target, effect.duration));
                break;

            // 방깎 — 부호 없는 감소값(effect.multiplier 그대로가 곧 깎는 양, ArmorBonus와
            // 달리 뒤집지 않는다). 레일리(2026-09-05, 구현담당1)가 값을 채웠는데 여기가
            // 안 읽어서 "값은 있는데 아무 일도 안 난다"였다(PM 지시로 정정). duration>0이면
            // 그 시간 뒤에 되돌린다 — 0(기본)이면 SupportShop 독약과 같은 관례로 영구
            // 누적한다(원작 방깎은 대개 지속시간이 없다, war3map.w3h 버프 311개 전수 확인).
            case SkillEffectKind.ArmorBreak:
                target.AddArmorShred(effect.multiplier);
                if (effect.duration > 0f) StartCoroutine(RevertArmorShredRoutine(target, effect.multiplier, effect.duration));
                break;

            // ArmorBonus·HealOverTime — EnemyDummy.ApplyAllyAuraEffect/RemoveAllyAuraEffect가
            // 04번(보스 오라)용으로 이미 만들어둔 add/remove 쌍을 그대로 쓴다("아직 아무도
            // 안 부른다"던 자리 — 이제 여기가 두 번째 호출부다). 그 메서드가 kind별 부호
            // 규칙(ArmorBonus는 양수=버프라 AddArmorShred(-multiplier)로 뒤집음, HealOverTime은
            // 그대로)을 이미 갖고 있어 여기서 새로 안 만든다. duration>0이면 그 시간 뒤에
            // RemoveAllyAuraEffect로 정확히 상쇄한다 — 0이면 영구(호출부가 그런 스킬을 만들
            // 때까진 실질적으로 안 씀).
            case SkillEffectKind.ArmorBonus:
            case SkillEffectKind.HealOverTime:
                target.ApplyAllyAuraEffect(effect);
                if (effect.duration > 0f) StartCoroutine(RevertAllyAuraEffectRoutine(target, effect, effect.duration));
                break;

            // 버프 부여(2026-09-06, PM 지시 — "버프를 실제로 걸 때만 게이트로 쓰라") — 원작
            // 디버프(핸콕 석화가 대상 Aegr를 올리는 것 등)가 이 자리를 쓸 것이다.
            // EnemyDummy.AddBuff가 duration을 스스로 추적해 만료시키므로(위 ArmorBonus·
            // HealOverTime과 달리) 되돌리는 코루틴이 따로 필요 없다.
            case SkillEffectKind.ApplyBuff:
                target.AddBuff(effect.buffId, effect.duration);
                break;

            // ExtraProjectile은 아직 값 의미가 없다(이번 작업 범위 밖) — 조용히 무시.
        }
    }

    IEnumerator SkillStunRoutine(EnemyDummy target, float duration)
    {
        target.AddFreeze();
        yield return new WaitForSeconds(duration);
        if (target != null) target.RemoveFreeze();
    }

    IEnumerator RevertArmorShredRoutine(EnemyDummy target, float amount, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (target != null) target.AddArmorShred(-amount);
    }

    IEnumerator RevertAllyAuraEffectRoutine(EnemyDummy target, SkillEffect effect, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (target != null) target.RemoveAllyAuraEffect(effect);
    }

    void DealSkillDamage(SkillEffect effect, EnemyDummy target, float recentAttackDamage)
    {
        // ⚠️ 2026-09-05 정정: PercentDamageTakenMultiplier(원작 A11S)는 "%체력 피해 전용
        // 감수성"이 아니라 "이 대상이 스킬 피해를 얼마나 받는가" 계수다 — 원작에 게이트 없이
        // 고정 피해에도 같은 계수가 곱는 사례가 43곳 중 7곳 있다(리서치담당 재조사). 그래서
        // basis를 안 가리고 스킬 피해 전반에 곱한다. %체력 분기 자체를 타는지는 별개 축
        // (target.TakesPercentDamage, ResolveSkillEffectValue에서 이미 갈랐다)이다.
        float amount = ResolveSkillEffectValue(effect, target, recentAttackDamage) * target.PercentDamageTakenMultiplier;
        // 원작 realD = 0.03×버프개수(SkillEffect.casterBuffCountFactor 주석 참고). 기존
        // 227개 효과는 이 필드가 직렬화에 없어 C# 기본값 0f로 읽힌다 — (1+0×count)=1이라
        // 배율이 완전히 무효, 회귀 없음. ⚠️ 2026-09-06: CountCasterBuffs()가 이제 버프
        // 레지스트리를 실제로 센다(:181 참고) — 다만 원작은 "시전자의 워크3 버프 전부"를
        // 세는데 우리는 우리 레지스트리(SupportShop 버프 + 절대쿨 자기버프)만 세는 과소
        // 근사다. 거프 4행(Garp_AttackDamage #5·#6·#7, 값×(1+0.12×버프개수))이 이 factor로
        // 실제로 걸린다.
        amount *= 1f + effect.casterBuffCountFactor * CountCasterBuffs();
        if (amount <= 0f) return;

        // ⚠️ 평타(DamageTypeOf/AttackTypeOf)가 아니라 이 효과 자신의 damageType/attackType을
        // 쓴다 — 평타는 항상 물리라 스킬만 마법을 낼 수 있다(SkillEffect 필드 주석 참고).
        // 캐스터의 평타 속성을 그대로 물려주면 물리 유닛의 스킬이 전부 물리로만 나가버린다.
        int hits = Mathf.Max(1, effect.hitCount);
        if (hits <= 1)
        {
            DealDamageToEnemy(target, amount, effect.damageType, effect.attackType);
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
                DealDamageToEnemy(target, amountPerHit, damageType, attackType);
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

        // ⚠️ 2026-09-06 추가(오라 무한누적 근본수정) — EnemyAuraCaster.OnDestroy와 같은
        // 이유: 이 유닛이 죽는 순간까지 걸어둔 오라 지속효과를 전부 되돌린다. 안 그러면
        // 이 유닛이 사라져도 그 순간 범위 안에 있던 대상들에게 효과가 영구히 남는다.
        if (skillRuntimeStates != null)
        {
            foreach (KeyValuePair<SkillData, SkillRuntimeState> kv in skillRuntimeStates)
            {
                SkillData skill = kv.Key;
                SkillRuntimeState state = kv.Value;
                if (skill == null || skill.triggerType != SkillTriggerType.Aura) continue;
                SkillLevel level = CurrentSkillLevel(skill);
                if (level == null) continue;

                if (state.auraAffectedEnemies != null)
                    foreach (EnemyDummy target in state.auraAffectedEnemies)
                        if (target != null) RemovePersistentAuraEffectsFromEnemy(level, target);
                if (state.auraAffectedAllies != null)
                    foreach (UnitIdentity ally in state.auraAffectedAllies)
                        if (ally != null) RemovePersistentAuraEffectsFromAlly(level, ally);
                // Self 버프는 이 UnitAttacker 자신의 activeBuffs에 있고, 이 컴포넌트 자체가
                // 지금 사라지는 중이라 별도로 뗄 필요가 없다.
            }
        }
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
            // isAbilityDamage: false — 평타는 원작에 UNIVERSAL이 없다(EnemyDummy.TakeDamage
            // 문서 참고). 로스터 damageType이 AP인 유닛이라도 평타로 방어를 무시하면 안 된다.
            DealDamageToEnemy(target, AttackDamage, DamageTypeOf, AttackTypeOf, armorIgnoreRatio: 0f, isAbilityDamage: false);
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
