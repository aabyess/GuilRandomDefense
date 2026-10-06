using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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
    // 🔴 2026-09-29 — identity와 같은 사고. OwnedByPlayer도 UnitSpawner가 Instantiate 뒤에 붙여서 Awake에선 null이었다
    //    → 등급 강화(UnitUpgrades)를 못 찾고, 피해의 처치자 번호가 −1로 나갔다. 붙을 때까지 다시 찾는다.
    OwnedByPlayer ownerCached;
    OwnedByPlayer owner
    {
        get
        {
            if (ownerCached == null && TryGetComponent(out ownerCached)) { upgradesResolveAttempted = false; upgradeMultiplierDirty = true; }
            return ownerCached;
        }
    }
    UnitCombat combat;
    // 🔴 2026-09-29 — 유닛 프리팹 214개 전부 UnitIdentity가 없다. UnitSpawner가 Instantiate **뒤에** 붙이므로
    //    Awake에서 한 번 찾으면 영원히 null이었다 → 이 유닛의 데이터(등급·피해 타입·스킬·특성·등급 강화)를 못 봤다.
    //    붙을 때까지 매번 다시 찾고, 찾는 순간 강화 캐시를 무효화한다(null로 한 번 계산된 값이 굳지 않게).
    UnitIdentity identityCached;
    UnitIdentity identity
    {
        get
        {
            if (identityCached == null && TryGetComponent(out identityCached)) upgradeMultiplierDirty = true;
            return identityCached;
        }
    }

    // 원작 비비 A0LZ류 자가시전 영구 강화(A0LZ_CASTER_STACK_INVESTIGATION.md/7703d2c) — 유닛
    // 인스턴스마다 따로 쌓인다(WC3 GetUnitAbilityLevelSwapped가 유닛 핸들 단위이듯, 비비가
    // 둘이면 각자 자기 레벨을 갖는다). 직렬화 안 함 — 런타임에만 존재하는 상태다(EnemyDummy의
    // aegrStackLevels 등과 같은 부류). selfUpgradeData가 비어 있으면(대부분의 유닛) TryUpgradeSelf가
    // 안전하게 실패한다 — 이 능력을 가진 유닛(비비)만 자산에 채워 넣으면 된다.
    [SerializeField] SelfUpgradeAbilityData selfUpgradeData;
    int selfUpgradeLevel;

    public int SelfUpgradeLevel => selfUpgradeLevel;

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
    float cachedResearchSpeedMultiplier = 1f;

    // 디버그 표시용 — 스탯이 실제로 적용됐는지 화면에서 확인하기 위해 노출한다.
    // ⚠️ 2026-09-06 정정(구현담당2 발견, PM 확인) — 예전엔 여기서 UnitUpgrades.MultiplierForGrade를
    // 곱했는데, 그 값이 실제로는 공격력이 아니라 원작 공속 증가율(gba1/gmo1) 데이터였다
    // (6개 트랙 전부 리서치담당 공속 표와 정확히 일치, 진짜 공격력 가산치는 이미 ResearchBonus
    // 가 맞게 들고 있었다). 그 배율을 걷어내고 AttackSpeedMultiplier로 옮겼다 — 연구소가
    // 원작대로 데미지가 아니라 공격속도를 올리게 됐다. UpgradeMultiplier엔 이제 특성강화
    // (딜증가)만 남는다.
    // ⚠️ 2026-09-07 정정(PM 지시, 리서치 판정 f924a31) — PrimaryStatAttackBonus(주스탯×800)를
    // 원래 배율 바깥에 더했었는데, 배율 안으로 옮겼다. 근거: Nbr1/Blo1/Inf1(공격력 % 버프
    // 계열) 필드가 war3map.j에 0회 등장한다 — JASS 연산 없이 엔진이 직접 처리하는 네이티브
    // 버프라는 뜻이고, 그 표준 동작은 "현재 공격력 전체(기본+주스탯+업글)에 곱한다"이다.
    // ⚠️ 이건 원문 대조가 아니라 "데이터로는 검증 불가한 엔진 동작"에 대한 워3 표준 동작
    // 근거 판정이다(리서치가 스스로 명시한 한계) — 나중에 반증되면 이 자리부터 다시 볼 것.
    // ResearchBonus는 이번 판정 대상이 아니라 그대로 바깥에 남긴다(별도 판정으로 이미
    // 그 자리에 있던 것).
    // ⚠️ 2026-09-07 추가(PM 지시, SkillEffectKind.AttackPowerBuffFlat 신설) — FlatAttackPowerBonus는
    // 원작 ANbr(배틀로어) 계열의 임시 "공격력 +N" 버프다. 바로 위 PrimaryStatAttackBonus와
    // 정확히 같은 근거(Nbr1이 그 필드 자체다 — 네이티브 버프, 현재 공격력 전체에 곱해지는
    // 배율 안쪽)라 같은 자리에 더한다.
    // ⚠️ 2026-09-30 추가(PM 승인) — 오라 고정 가산(AuraFlatAttackPower)과 공격력 %(PercentAttackPowerBonus, 기본 공격력 =
    // attackDamage + 주스탯 몫에만 곱한 값)도 같은 괄호 안에 더한다. 둘 다 0이면 예전 식 그대로.
    public float AttackDamage => (attackDamage + PrimaryStatAttackBonus + FlatAttackPowerBonus + AuraFlatAttackPower
                                  + (attackDamage + PrimaryStatAttackBonus) * PercentAttackPowerBonus) * UpgradeMultiplier * AttackPowerMultiplier + ResearchBonus;
    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval / AttackSpeedMultiplier;

    // 도움소의 임시 공격속도 버프. 값을 덮어썼다 되돌리는 대신 여기에 쌓는다 —
    // 되돌리는 쪽이 "원래값"을 기억하면, 그 사이에 영구 강화를 사거나 버프가 겹칠 때
    // 기억해둔 옛 값으로 되돌아가면서 산 것이 조용히 사라진다.
    readonly List<float> attackSpeedBuffs = new List<float>();

    // 출항이다(원작 확인, 2026-09-04: 공격속도가 아니라 공격력 버프다)용 — 같은 누적 방식.
    readonly List<float> attackPowerBuffs = new List<float>();

    // ⚠️ 2026-09-06 연결(구현담당2, PM 승인 — "만드세요, 사장님께 안 올립니다") — 연구소
    // 등급 강화가 원작대로 공속도 올린다(gba1/gmo1). 도움소 임시 버프(attackSpeedBuffs)와
    // 곱으로 겹친다. 영원함 트랙은 배선하지 않는다(대응하는 원작 등급 트랙이 없다, PM 지시)
    // — ResearchSpeedMultiplier가 legacyGradeLevels에서 영원함 트랙을 절대 못 찾아(잠긴
    // 트랙이라 LevelUp이 안 불린다) 자동으로 1(무영향)이라 별도 예외 코드가 필요 없다.
    // 타입 업그레이드(+최대 13%, 3레벨 별도 축)는 이번에 넣지 않는다(PM 지시).
    /// <summary>현재 공속 배율의 곱(연구소·버프·오라 포함, 기본 1) — 「공속 비례」 효과(SkillEffect.attackSpeedScale)와 UI가 읽는다.</summary>
    public float CurrentAttackSpeedMultiplier => AttackSpeedMultiplier;

    float AttackSpeedMultiplier
    {
        get
        {
            // ⚠️ 2026-09-07 추가(PM 지시, SkillEffectKind.AttackSpeedBuffPercent 신설) —
            // SkillAttackSpeedBuffMultiplier는 ActiveBuff 레지스트리 기반(자동 만료)이라
            // 기존 attackSpeedBuffs(수동 Add/Remove, SupportShop 전용)와 별도 축이다 — 곱은
            // 순서 무관이라 그냥 같이 곱한다.
            float product = ResearchSpeedMultiplier * HeroAttackSpeedMultiplier * SkillAttackSpeedBuffMultiplier * AuraAttackSpeedMultiplier
                            * (1f + TeamBuffs.AttackSpeedPercent);
            foreach (float buff in attackSpeedBuffs) product *= buff;
            return product > 0f ? product : 1f;
        }
    }

    // 연구소 공속 배율(gba1/gmo1) — 등급트랙("강화소 1")과 공격타입트랙("강화소 3") 둘 다
    // 곱해진 값이다(2026-09-07, 아래 RefreshUpgradeCacheIfDirty 참고). RefreshUpgradeCacheIfDirty가
    // 같이 갱신한다(레벨업 이벤트 하나로 데미지·가산·공속 셋 다 무효화하면 되므로 dirty
    // 플래그를 공유한다).
    float ResearchSpeedMultiplier
    {
        get
        {
            RefreshUpgradeCacheIfDirty();
            return cachedResearchSpeedMultiplier;
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

    // SkillEffectBasis.ResearchLevel 전용 자리 — 원작 예: 핸콕 "연구횟수×30,000+360,000".
    // ⚠️ 2026-09-06 정정(PM 지시, 뿌리 ㉜ 네 번째) — 이 값은 원작 **타입 업그레이드**
    // (`R01L`·`R01M`·`R01O`·`R01T`·`R01V` 등, 공격타입별, **최대 3레벨**)를 뜻한다.
    // **등급 업그레이드**(`R000`~`R004`, 최대 21 — `UnitUpgrades.LevelForGrade`가 읽는
    // 바로 그 트랙, 공속 축(SpeedMultiplierForGrade/BonusForGrade)이 정확히 그 용도로
    // 쓴다)와는 **다른 축**이다 — 등급 트랙을 여기서 읽으면 최대 7배 과대가 된다
    // (2026-09-06 오전, 실제로 이 버그였다 — 제한됨·히든 5효과가 실주행 중이었다).
    // 두 함수가 우연히 같은 저장소(legacyGradeLevels)를 조회하지만 뜻은 다르다 —
    // LevelForGrade는 지우지 않는다(공속 축 소비자가 있다), 여기서 그걸 호출하는
    // 연결만 끊는다. 우리에 타입 업그레이드 축이 아직 없어 **항상 0**이다(연구를
    // 하나도 안 산 것과 값이 같다 — "0×multiplier+bonus=bonus"). 그 축이 실제로
    // 생기면(R01L/R01M/R01O/R01T/R01V별 레벨을 저장하는 무언가) 여기를 그것으로
    // 바꾼다.
    //
    // ⚠️ 2026-09-06 저녁 추가 확인(구현담당2, PM 지시) — "그 축을 만들면 되는가"를
    // 봤을 때는 **못 만든다**고 판단했다. 이 basis를 쓰는 9건(5개 파일)이 전부 같은
    // 연구 하나를 읽는 게 아니라 서로 다른 타입 업그레이드 ID를 참조하는 것처럼
    // 보였고(최상호=`R01M`, 전법규=`R01L`, 황정기 2건=`R01V`/`R01O`), 무엇보다
    // **레벨당 값·구매 비용을 원작에서 조사한 적이 없어** 지금 지으면 창작이 될
    // 상황이었다.
    //
    // ⚠️ 2026-09-06 밤 최종 연결(구현담당2, 리서치담당 `war3map.w3q` 원문 전수
    // `ATTACKTYPE_UPGRADE_RAW_DUMP.md` 이후) — 비용·구조가 나와서 지어낼 게
    // 없어졌다. 정체가 확정됐다: 원작 "강화소 3"(우리 등급트랙 "강화소 1"과 다른
    // 건물) 하나가 공격타입 4종(일반`R00G`·공성`R00H`·관통`R01Q`·패기`R01V`, 각
    // 3000엔·목재1부터 레벨마다 +500엔·+목재1, 최대 3레벨 — 09-25 정정)을 판다 — 사면 트리거가 자식 3개(버킷값 고정)를
    // 그 자리에서 공짜로 준다. **자식의 절대값은 안 옮긴다** — 이 basis를 쓰는
    // 스킬들이 이미 자기 `multiplier`/`bonus`를 갖고 있어서, 필요한 건 "몇 번
    // 샀는가"(레벨, 0~3)뿐이다(옮기면 이중 계상).
    //
    // 위에서 "9건이 서로 다른 ID를 읽는다"고 봤던 건 **원작(재배정 전) 캐릭터가
    // 참조하던 ID였다** — 우리는 유닛을 원작 이름 1:1로 매핑하지 않고 재배정하므로
    // (예: 최상호 파일 설명문의 `R01M`은 원작 카벤디시가 참조하던 것이지, 지금
    // 최상호에게 배정된 공격타입과는 무관하다 — 최상호는 attackType=Hero(패기)로
    // 재배정돼 있다). 등급트랙(`LevelForGrade`)이 "원작 그 캐릭터의 등급"이 아니라
    // "지금 이 유닛에 배정된 등급"을 쓰는 것과 같은 원칙으로, 여기도 **"지금 이
    // 유닛에 배정된 공격타입"**을 기준으로 읽는다 — `UnitUpgrades.LevelForAttackType`
    // 참고. 하드코딩된 파일별 ID 매핑은 안 만든다(재배정 원칙과 안 맞고, 유지보수
    // 부담만 커진다).
    int CountResearchLevel()
    {
        UnitData unitData = identity != null ? identity.Data : null;
        UnitUpgrades source = ResolveUpgrades();
        return (unitData != null && source != null) ? source.LevelForAttackType(unitData.attackType) : 0;
    }

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

        // ⚠️ 2026-09-07 추가(SkillEffectKind.AttackPowerBuffFlat 전용, PM 지시) — 0이면
        // 기존 버프와 동일(이름표만, 수치 효과 없음 — 회귀 없음). 0이 아니면 이 버프가
        // 살아있는 동안 FlatAttackPowerBonus에 이 값만큼 더해진다.
        public float flatAttackPowerAmount;

        // ⚠️ 2026-09-07 추가(SkillEffectKind.AttackSpeedBuffPercent 전용, PM 지시) — 기본값
        // 1f(배율 항등원 — 곱해도 무효과, 회귀 없음). 1이 아니면 이 버프가 살아있는 동안
        // SkillAttackSpeedBuffMultiplier에 이 배율이 곱해진다(1+원작 raw퍼센트로 변환된 값).
        public float attackSpeedMultiplierAmount = 1f;

        // 2026-09-30 추가(SkillEffectKind.AttackPowerBuffPercent 시간제) — 0이면 없음. 살아 있는 동안 PercentAttackPowerBonus에 더해진다.
        public float attackPowerPercentAmount;
    }

    // ---- 오라 수치 레지스트리(2026-09-30, PM 승인) — 오라(Aura 발동방식)가 아군·자기에게 주는 수치(공속 %·공격력 고정·공격력 %).
    // 원작 오라는 같은 버프 ID끼리 안 겹치고(가장 큰 것 하나) 다른 버프 ID끼리는 겹친다 — 엔진 지식, 맵 미확정(마나 재생 오라와 같은 규칙).
    // 예전 오라 경로는 버프 항목을 그냥 붙여서, 같은 오라 유닛이 둘이면 배율이 곱으로 쌓였고 하나가 범위를 벗어나면
    // RemoveBuff(id)가 남의 몫을 지웠다. 여기서는 준 쪽(source)별로 기록하고 버프 ID별 최댓값만 읽는다.
    class AuraBonus { public Object source; public SkillEffectKind kind; public string id; public float value; }
    readonly List<AuraBonus> auraBonuses = new List<AuraBonus>();
    static readonly Dictionary<string, float> auraBonusScratch = new Dictionary<string, float>();

    public void AddAuraBonus(Object source, SkillEffectKind kind, string id, float value)
    {
        if (value == 0f) return;
        auraBonuses.Add(new AuraBonus { source = source, kind = kind, id = id ?? "", value = value });
    }

    public void RemoveAuraBonus(Object source, SkillEffectKind kind, string id)
    {
        id ??= "";
        for (int i = 0; i < auraBonuses.Count; i++)
            if (auraBonuses[i].source == source && auraBonuses[i].kind == kind && auraBonuses[i].id == id) { auraBonuses.RemoveAt(i); return; }
    }

    // 버프 ID별 최댓값을 모아 합(product=false) 또는 (1+값)의 곱(product=true). 준 쪽이 사라진 항목은 버린다.
    float AuraBonusTotal(SkillEffectKind kind, bool product)
    {
        if (auraBonuses.Count == 0) return product ? 1f : 0f;
        auraBonusScratch.Clear();
        for (int i = auraBonuses.Count - 1; i >= 0; i--)
        {
            AuraBonus b = auraBonuses[i];
            if (b.source == null) { auraBonuses.RemoveAt(i); continue; }
            if (b.kind != kind) continue;
            if (kind == SkillEffectKind.AllyMoveSpeedDebuff && b.source is UnitAttacker debuffer && debuffer.YoonseoEnhanced) continue;   // 최윤서 강화 = 아군 디버프 100% 제거
            if (!auraBonusScratch.TryGetValue(b.id, out float best) || b.value > best) auraBonusScratch[b.id] = b.value;
        }
        float total = product ? 1f : 0f;
        foreach (float v in auraBonusScratch.Values) total = product ? total * (1f + v) : total + v;
        return total;
    }

    float AuraAttackSpeedMultiplier => AuraBonusTotal(SkillEffectKind.AttackSpeedBuffPercent, true);
    float AuraFlatAttackPower => AuraBonusTotal(SkillEffectKind.AttackPowerBuffFlat, false);

    /// <summary>공격력 % 증가 합(오라 + 시간제 버프). 기본 공격력에만 곱해진다(AttackDamage 참고).</summary>
    public float PercentAttackPowerBonus
    {
        get
        {
            float sum = AuraBonusTotal(SkillEffectKind.AttackPowerBuffPercent, false) + TeamBuffs.AttackPowerPercent;
            if (activeBuffs.Count > 0)
            {
                PruneExpiredBuffs();
                foreach (ActiveBuff b in activeBuffs) sum += b.attackPowerPercentAmount;
            }
            return sum;
        }
    }

    // 시간제 공격력 % 버프 — 같은 id가 다시 걸리면 만료만 갱신한다(AddAttackSpeedBuffPercent와 같은 규칙).
    public void AddAttackPowerBuffPercent(string id, float percent, float duration, int hitCharges)
    {
        if (percent == 0f) return;
        if (hitCharges > 0)
        {
            activeBuffs.Add(new ActiveBuff { id = id, expiresAt = -1f, hitsRemaining = hitCharges, skipNextTick = true, attackPowerPercentAmount = percent });
            return;
        }
        if (duration <= 0f) return;   // 영구는 오라 레지스트리로만
        PruneExpiredBuffs();
        ActiveBuff existing = string.IsNullOrEmpty(id) ? null
            : activeBuffs.Find(b => b.id == id && b.hitsRemaining <= 0 && b.expiresAt > 0f && b.attackPowerPercentAmount != 0f);
        if (existing != null) { existing.expiresAt = Time.time + duration; existing.attackPowerPercentAmount = percent; return; }
        activeBuffs.Add(new ActiveBuff { id = id, expiresAt = Time.time + duration, attackPowerPercentAmount = percent });
    }

    readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    // ⚠️ 2026-09-07 추가(SkillEffectKind.AttackPowerBuffFlat, PM 지시) — 원작 ANbr(배틀로어)
    // 계열의 임시 "공격력 +N" 총합. AttackDamage가 PrimaryStatAttackBonus와 같은 자리에서
    // 읽는다(위 주석 참고). PruneExpiredBuffs를 먼저 불러 만료분을 걷어낸다.
    public float FlatAttackPowerBonus
    {
        get
        {
            PruneExpiredBuffs();
            float sum = 0f;
            foreach (ActiveBuff b in activeBuffs) sum += b.flatAttackPowerAmount;
            return sum;
        }
    }

    // ApplyBuff(AddBuff)와 같은 관례 — hitCharges>0이면 "평타 N번" 만료, 아니면 duration초
    // 만료(0=영구, RemoveBuff로만 해제). id가 있으면 버프 레지스트리에도 등록해
    // requiredBuffId 게이트가 조회할 수 있다(비어있어도 magnitude 자체는 정상 적용 —
    // 게이팅용 이름표는 선택 사항).
    public void AddFlatAttackPowerBuff(string id, float amount, float duration, int hitCharges)
    {
        if (amount == 0f) return;
        if (hitCharges > 0)
        {
            activeBuffs.Add(new ActiveBuff { id = id, expiresAt = -1f, hitsRemaining = hitCharges, skipNextTick = true, flatAttackPowerAmount = amount });
        }
        else
        {
            activeBuffs.Add(new ActiveBuff { id = id, expiresAt = duration > 0f ? Time.time + duration : -1f, flatAttackPowerAmount = amount });
        }
    }

    // ⚠️ 2026-09-07 추가(SkillEffectKind.AttackSpeedBuffPercent, PM 지시) — 원작 AOae
    // (Endurance Aura) 계열의 임시 "공격속도 +N%" 곱. AttackSpeedMultiplier가 곱하는
    // 자리에서 읽는다(위 주석 참고).
    public float SkillAttackSpeedBuffMultiplier
    {
        get
        {
            PruneExpiredBuffs();
            float product = 1f;
            foreach (ActiveBuff b in activeBuffs) product *= b.attackSpeedMultiplierAmount;
            return product > 0f ? product : 1f;
        }
    }

    // AddFlatAttackPowerBuff와 같은 관례. percent는 원작 raw 퍼센트(0.15=15%) — 여기서
    // (1+percent)로 변환해 저장한다(SkillAttackSpeedBuffMultiplier가 곱셈 항등원 1을
    // 기준으로 곱하므로).
    public void AddAttackSpeedBuffPercent(string id, float percent, float duration, int hitCharges)
    {
        if (percent == 0f) return;
        float multiplier = 1f + percent;
        if (hitCharges > 0)
        {
            activeBuffs.Add(new ActiveBuff { id = id, expiresAt = -1f, hitsRemaining = hitCharges, skipNextTick = true, attackSpeedMultiplierAmount = multiplier });
        }
        else if (duration > 0f)
        {
            // 시간 버프는 원작처럼 같은 id가 다시 걸리면 만료만 갱신한다(2026-09-30). 전에는
            // 항목을 새로 붙여 배율이 곱으로 쌓였다(A095 블러드러스트 0.5초 쿨 → 사실상 무한 누적).
            // 오라·영구(duration 0)는 RemoveBuff와 짝이 맞아야 해서 아래 기존 경로 그대로다.
            PruneExpiredBuffs();
            ActiveBuff existing = string.IsNullOrEmpty(id) ? null
                : activeBuffs.Find(b => b.id == id && b.hitsRemaining <= 0 && b.expiresAt > 0f && b.attackSpeedMultiplierAmount != 1f);
            if (existing != null)
            {
                existing.expiresAt = Time.time + duration;
                existing.attackSpeedMultiplierAmount = multiplier;
                return;
            }
            activeBuffs.Add(new ActiveBuff { id = id, expiresAt = Time.time + duration, attackSpeedMultiplierAmount = multiplier });
        }
        else
        {
            activeBuffs.Add(new ActiveBuff { id = id, expiresAt = -1f, attackSpeedMultiplierAmount = multiplier });
        }
    }

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
    // → 제한을 걷어냈다. 무한히 쌓여도 피해 배율이 2배에 수렴하므로 효과는 유계다(하한 −20은 09-30 걷음).
    //
    // ⚠️ 2026-09-05 정정(2차, 04③): 1차 정정("표 레벨을 올리는 것으로 바뀌었다")이
    // 틀렸었다 — A0TK/A0VI/A0VJ는 범용 방깎 표가 아니라 **카이도·핸콕 전용 스킬**이
    // 올리는 능력이었다(PM, 트리거 재조사). 우리 유닛의 일반 ArmorShred 트레잇은
    // 원래대로 EnemyDummy.armorShred(float, 원작 `Iarp`류)를 직접 깎는다 — 이 값은
    // 피해 배율이 2배에 수렴하므로 무한 누적이어도 효과는 유계다. A0TK/A0VI/A0VJ 쪽은
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

        // 이감부여(원작 AOae류, 2026-09-07 배선 — PM 승인) — 방깎과 같은 자리·같은 관례
        // (영구 누적, EnemyDummy.MoveSpeedFloor에서 잘림). value가 원작 "이동속도 X%
        // 감소"의 X(0.07=7%)다.
        float slow = source.EffectSum(unitData, TraitEffectKind.SlowOnHit);
        if (slow > 0f) target.AddMoveSpeedShred(slow);
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
        float critHpBefore = target.Hp;
        target.TakeDamage(bonus, DamageTypeOf, AttackTypeOf, owner != null ? owner.OwnerId : -1,
                          armorIgnoreRatio: 0f, isAbilityDamage: false);
        SkillTelemetry.Damage(unitData, "치명", target, critHpBefore);

        // 스턴 시간은 걸린 적이 센다(EnemyDummy.FreezeFor) — 이 유닛이 사라져도 풀린다.
        if (unitData.critStunDuration > 0f) target.FreezeFor(unitData.critStunDuration);
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
        public float activeReadyAt;          // ActiveButton 전용 — 다시 쓸 수 있는 Time.time(0 = 바로)

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

    // ---- 액티브(누르는) 스킬 — SkillTriggerType.ActiveButton(2026-10-06, 초월 최상호 바지사장) ----
    // 이 유닛의 단추 스킬(첫 번째 ActiveButton). 없으면 null. 시전은 호스트의 진짜 유닛에서만(멀티 클라는 NetCommands.CastActive로 요청).
    public SkillData ActiveSkill
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            if (unitData == null) return null;
            int count = EffectiveSkillCount(unitData);
            for (int i = 0; i < count; i++)
            {
                SkillData skill = ResolveSkillAt(unitData, i);
                if (skill != null && skill.triggerType == SkillTriggerType.ActiveButton) return skill;
            }
            return null;
        }
    }

    /// <summary>남은 쿨다운(초). 0이면 쓸 수 있다.</summary>
    public float ActiveCooldownRemaining(SkillData skill) =>
        skill == null ? 0f : Mathf.Max(0f, GetRuntimeState(skill).activeReadyAt - Time.time);

    /// <summary>지금 레벨(특성강화 반영)의 전체 쿨다운(초).</summary>
    public float ActiveCooldownTotal(SkillData skill)
    {
        SkillLevel level = skill != null ? CurrentSkillLevel(skill) : null;
        return level != null ? level.cooldown : 0f;
    }

    /// <summary>단추를 눌렀을 때. 쿨 중·효과 없음이면 이유를 돌려주고 false. 마나 소모 없음.</summary>
    public bool TryCastActive(SkillData skill, out string failReason)
    {
        failReason = null;
        UnitData unitData = identity != null ? identity.Data : null;
        if (skill == null || unitData == null || skill.triggerType != SkillTriggerType.ActiveButton) { failReason = "쓸 수 없는 스킬입니다."; return false; }
        SkillLevel level = CurrentSkillLevel(skill);
        if (level == null || level.effects == null || level.effects.Count == 0) { failReason = "아직 효과가 없는 스킬입니다."; return false; }
        SkillRuntimeState state = GetRuntimeState(skill);
        float remaining = state.activeReadyAt - Time.time;
        if (remaining > 0f) { failReason = $"쿨타임 중입니다. ({Mathf.CeilToInt(remaining)}초)"; return false; }
        if (!PassesBuffGate(level, null)) { failReason = "지금은 쓸 수 없습니다."; return false; }

        state.activeReadyAt = Time.time + Mathf.Max(0.01f, level.cooldown);
        SkillTelemetry.Cast(unitData, skill);
        SkillSfx.Cast(unitData, skill, transform.position);
        PulseSphereArt();
        bool vfxBefore = SkillVfx.BeginCast(unitData, skill);
        CastSkillLevel(level, level.WorldRange, null, 0f);
        SkillVfx.EndCast(vfxBefore);
        return true;
    }

    // 「공속 비례」(SkillEffect.attackSpeedScale) — 1 + 계수 × (공속 배율(상한 Cap) − 1), 0 아래로는 안 내려간다. 계수 0이면 1(꺼짐).
    float AttackSpeedScaleFactor(SkillEffect effect)
    {
        if (effect == null || effect.attackSpeedScale <= 0f) return 1f;
        float speed = AttackSpeedMultiplier;
        if (effect.attackSpeedScaleCap > 0f) speed = Mathf.Min(speed, effect.attackSpeedScaleCap);
        return Mathf.Max(0f, 1f + effect.attackSpeedScale * (speed - 1f));
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

    // 게이지 재생(UnitData.manaRegenPerSecond 등 주석 참고) — 초당 재생을 소수로 모았다가 한 칸씩 게이지에 더한다.
    // 검사는 지금처럼 평타 때만 한다 — 원작 트리거도 전부 평타(HashAttack) 안에서 마나를 검사하므로, 재생으로
    // 문턱을 넘은 뒤 다음 평타에서 발동한다. 게이지는 최대 마나에서 멈춘다(원작 umpm 상한).
    const float IntRegenBonus = 0.08f;   // war3mapMisc.txt IntRegenBonus
    float manaRegenCarry, lifeRegenCarry;

    int ManaGaugeCap(UnitData d) => d != null && d.manaMax > 0f ? Mathf.RoundToInt(d.manaMax * Mathf.Max(d.manaGaugePerMana, 0.0001f)) : int.MaxValue;
    int LifeGaugeCap(UnitData d) => d != null && d.lifeGaugeMax > 0f ? Mathf.RoundToInt(d.lifeGaugeMax) : int.MaxValue;

    // 게이지 시작값(UnitData.manaGaugeStart·lifeGaugeStart 주석 참고) — 0이면 지금처럼 스킬의 resetTo.
    static int ManaGaugeStart(UnitData d, SkillLevel level) => d != null && d.manaGaugeStart > 0f ? Mathf.RoundToInt(d.manaGaugeStart) : level.resetTo;
    static int LifeGaugeStart(UnitData d, SkillLevel level) => d != null && d.lifeGaugeStart > 0f ? Mathf.RoundToInt(d.lifeGaugeStart) : level.resetTo;
    // 평타당 체력 게이지 증가 — 기본 +1, lifeGaugeCustomHitGain이면 lifeGaugeHitGain 확률로 +1(0이면 재생으로만 찬다).
    static int LifeGaugeHitGain(UnitData d) => d == null || !d.lifeGaugeCustomHitGain ? 1 : (Random.value < d.lifeGaugeHitGain ? 1 : 0);

    // 마나 재생 오라(UnitData.manaAuraRegenPerSecond 주석 참고) — 주변 같은 주인 유닛의 오라를 0.25초마다 모은다.
    // 같은 버프 ID는 최댓값만, 다른 버프 ID는 합. 멀티에선 진짜 유닛(UnitAttacker)이 호스트에만 있어 호스트에서만 더해진다.
    const float ManaAuraScanInterval = 0.25f;
    float manaAuraTimer, manaAuraBonus;
    static readonly Dictionary<string, float> manaAuraScratch = new Dictionary<string, float>();

    float ScanManaAuraBonus()
    {
        if (identity == null) return 0f;
        int ownerId = identity.OwnerId;
        Vector3 here = transform.position;
        manaAuraScratch.Clear();
        foreach (UnitIdentity other in UnitIdentity.Active)
        {
            if (other == null) continue;
            UnitData od = other.Data;
            if (od == null || od.manaAuraRegenPerSecond <= 0f || other.OwnerId != ownerId) continue;
            if (other == identity) { if (!od.manaAuraIncludesSelf) continue; }
            else
            {
                float worldRange = od.manaAuraRange / WorldScale.Value;
                if ((other.transform.position - here).sqrMagnitude > worldRange * worldRange) continue;
            }
            string key = od.manaAuraBuffId ?? "";
            if (!manaAuraScratch.TryGetValue(key, out float best) || od.manaAuraRegenPerSecond > best) manaAuraScratch[key] = od.manaAuraRegenPerSecond;
        }
        float sum = 0f;
        foreach (float v in manaAuraScratch.Values) sum += v;
        return sum;
    }

    void TickGaugeRegen()
    {
        UnitData d = identity != null ? identity.Data : null;
        if (d == null) return;
        if (manaGaugeInitialized && d.manaGaugePerMana > 0f)
        {
            manaAuraTimer -= Time.deltaTime;
            if (manaAuraTimer <= 0f) { manaAuraTimer = ManaAuraScanInterval; manaAuraBonus = ScanManaAuraBonus(); }
            manaRegenCarry += (d.manaRegenPerSecond + IntRegenBonus * CurrentIntelligence + manaAuraBonus) * d.manaGaugePerMana * Time.deltaTime;
            if (manaRegenCarry >= 1f)
            {
                int n = Mathf.FloorToInt(manaRegenCarry);
                manaRegenCarry -= n;
                manaGaugeCounter = Mathf.Min(manaGaugeCounter + n, ManaGaugeCap(d));
            }
        }
        if (lifeGaugeInitialized && d.lifeGaugeRegenPerSecond > 0f)
        {
            lifeRegenCarry += d.lifeGaugeRegenPerSecond * Time.deltaTime;
            if (lifeRegenCarry >= 1f)
            {
                int n = Mathf.FloorToInt(lifeRegenCarry);
                lifeRegenCarry -= n;
                lifeGaugeCounter = Mathf.Min(lifeGaugeCounter + n, LifeGaugeCap(d));
            }
        }
    }

    // 01번 영웅 스탯(STR/AGI/INT) — 사장님 결정 2026-09-06. 원작 "적을 죽일 때마다
    // AddHeroXP(영웅, 1)"에 대응.
    //
    // ⚠️ 2026-09-06 축 재설계(PM, war3map.j:14734 직접 대조) — 이 값은 "죽인 유닛"이 아니라
    // "그 라인 주인의 등록 영웅 전원"에게 간다(ForGroup(udg_Exp_Hero_Group[라인 주인], ...)
    // → AddHeroXP(그 유닛, 1)). 누가 죽였는지는 안 본다 — 예전엔 DealDamageToEnemy가 자기
    // 타격으로 죽였을 때만 올리게 짜여 있었는데(생사 비교 판정), 그 설계 자체가 축이
    // 틀렸다(구조는 맞았지만 "죽인 사람"이 아니라 "라인 주인 전원"이 대상). 그 래퍼는 지금
    // 존재 이유가 사라져 걷어냈다 — GrantHeroKillExperienceToLane(EnemyDummy.TakeDamage의
    // 사망 처리, RewardDistributor.GrantKillReward와 같은 자리)이 대신한다.
    //
    // ⚠️ udg_Exp_Hero_Group에 등록되는 건 아무 유닛이 아니라 **조합으로 만든 초월함·영원한
    // 등급 영웅뿐**이다(각 Trig_Eternal_* 조합 트리거가 생성 직후 그룹에 넣는다) — 그래서
    // GrantHeroKillExperienceToLane이 UnitData.grade로 그 두 등급만 거른다.
    int heroXp;
    int heroLevel; // 0~23 (캐릭터 레벨 1~24). RecomputeHeroLevel이 heroXp가 바뀔 때마다 갱신.

    // ⚠️ MaxHeroLevel=24(스톡 10에서 원작이 늘림). war3mapMisc.txt: NeedHeroXP=33,75,116,152
    // (레벨 2~5 누적 경험치, 리터럴) · 그 뒤(레벨 6~24)는 필요(N)=필요(N-1)×1.03+15×N.
    // 킬 1회=경험치 1점이므로 누적 경험치=누적 킬 수와 같다. 표는 한 번만 계산해 재사용한다.
    const int MaxHeroLevel = 24;
    static readonly double[] HeroXpThresholds = BuildHeroXpThresholds();

    static double[] BuildHeroXpThresholds()
    {
        // index i → 레벨(i+2)에 필요한 누적 경험치. 레벨 1은 문턱이 없어(이미 시작 레벨)
        // 배열에 안 들어간다 — heroLevel(레벨업 누적 횟수)이 이 배열 길이(23)에 닿으면
        // 캐릭터 레벨 24(상한)다.
        double[] table = new double[MaxHeroLevel - 1];
        table[0] = 33; table[1] = 75; table[2] = 116; table[3] = 152; // 레벨 2~5, 원작 리터럴
        for (int level = 6; level <= MaxHeroLevel; level++)
            table[level - 2] = table[level - 3] * 1.03 + 15 * level;
        return table;
    }

    // 원작 AddHeroXP(영웅, N)에 대응 — 킬 1회는 N=1로 GainKillExperience가 부른다.
    // ⚠️ 타시기 특성처럼 곡선을 건너뛰는 특성(AddHeroXPSwapped(5000) 등, 300~5000짜리
    // 7건 더 있음, PM 지시 대기)이 나오면 이 메서드를 그대로 재사용하면 된다 — 킬이든
    // 특성 일괄 지급이든 "경험치를 더한다"는 같은 동작이라 새 메서드가 필요 없다.
    public void AddHeroXp(int amount)
    {
        if (amount <= 0) return;
        heroXp += amount;
        RecomputeHeroLevel();
    }

    void RecomputeHeroLevel()
    {
        int level = 0;
        while (level < HeroXpThresholds.Length && heroXp >= HeroXpThresholds[level]) level++;
        heroLevel = level; // HeroXpThresholds.Length(23)에서 자연히 멈춰 24레벨 상한을 지킨다.
    }

    public void GainKillExperience() => AddHeroXp(1);

    /// <summary>캐릭터 레벨(1~24) — 정보칸 표시용. 내부 heroLevel은 레벨업 누적 횟수(0~23)라 +1이다.</summary>
    public int CharacterLevel => heroLevel + 1;

    /// <summary>캐릭터 레벨 characterLevel(1~24)에 닿는 누적 경험치(=누적 킬 수). 탐침이 영웅을 그 레벨로 세울 때 쓴다.</summary>
    public static int HeroXpToReach(int characterLevel)
    {
        if (characterLevel <= 1) return 0;
        int index = Mathf.Min(characterLevel - 2, HeroXpThresholds.Length - 1);
        return (int)System.Math.Ceiling(HeroXpThresholds[index]);
    }

    // ⚠️ 아직 안 잇는다 — "어느 유닛의 주스탯이 무엇인가"(STR/AGI/INT 중 무엇이 그 유닛의
    // 성장 축인가) 대응표가 없다(PM 지시 2026-09-06). 대응이 오면 각 유닛의 UnitData에서
    // 주스탯 쪽 xPerLevel엔 0.85f, 나머지 둘엔 0.21f를 채운다 — 코드는 이미 그 값을
    // 그대로 곱할 준비가 돼 있다(CurrentStrength 등, 아래). 리서치담당 검산: 평타 계열
    // (StrAttackBonus류)의 기여는 최대 레벨에서도 7.6%뿐이고, 스탯이 주역인 건 스킬 쪽
    // (SkillEffectBasis.CasterStrength 등, 계수 2,000~50,000)이다 — 평타 보너스를 크게
    // 잡지 말 것.
    const float MainStatGrowthPerLevel = 0.85f;
    const float SecondaryStatGrowthPerLevel = 0.21f;

    // 도움소 「능력치 증가」(H0B7, 사장님 결정 2026-09-06) — 유닛 종류 데이터(UnitData)가
    // 아니라 플레이어 진행 상태다(아침에 확인한 그대로: baseX/xPerLevel은 "이 유닛이
    // 어떤 영웅인가"이고, 구매분은 "이 판에서 플레이어가 얼마나 샀는가"라 성격이 다르다).
    // 그래서 UnitData가 아니라 여기(런타임 인스턴스)에 카운터로 둔다. 원작은
    // GetRandomInt(1,3)으로 STR/AGI/INT 중 하나만 골라 +1 — 무엇이 오를지 플레이어가
    // 못 고른다(설계 의도, 기대비용을 주스탯 1점당 3배로 만든다). SupportShop이 그 굴림을
    // 하고 이 카운터엔 "이미 정해진 결과"만 들어온다(AddPurchasedStat).
    int purchasedStrength;
    int purchasedAgility;
    int purchasedIntelligence;

    // amount 기본값 1 — 도움소 「능력치 증가」(위 주석)는 매번 1점씩 굴려서 부른다.
    // 타시기(06번⑤ 순수스탯형, AddHeroXPSwapped(5000)+STR/AGI/INT 각 +3)처럼 특성강화가
    // 한 번에 여러 점을 몰아줄 때만 amount를 3으로 넘긴다 — 기존 호출부(1점씩)는 그대로.
    public void AddPurchasedStat(int statIndex, int amount = 1)
    {
        switch (statIndex)
        {
            case 0: purchasedStrength += amount; break;
            case 1: purchasedAgility += amount; break;
            case 2: purchasedIntelligence += amount; break;
        }
    }

    // 06번③ 변신 이월(GameHud.ExecuteTransform이 부른다) — 원작 GetHeroXP/GetHeroStatBJ →
    // 새 유닛에 SetHeroXP류로 되돌리는 두 단계를, 우리 축(heroXp·purchasedStat)으로
    // 재현한다. source는 Consume() 전에 읽은 옛 유닛 — 지운 뒤엔 0만 남아 의미가 없다.
    public void CopyProgressionFrom(UnitAttacker source)
    {
        if (source == null) return;
        if (source.heroXp > 0) AddHeroXp(source.heroXp);
        if (source.purchasedStrength > 0) AddPurchasedStat(0, source.purchasedStrength);
        if (source.purchasedAgility > 0) AddPurchasedStat(1, source.purchasedAgility);
        if (source.purchasedIntelligence > 0) AddPurchasedStat(2, source.purchasedIntelligence);
    }

    public float CurrentStrength
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            return unitData != null ? unitData.baseStrength + unitData.strengthPerLevel * heroLevel + purchasedStrength : 0f;
        }
    }

    public float CurrentAgility
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            return unitData != null ? unitData.baseAgility + unitData.agilityPerLevel * heroLevel + purchasedAgility : 0f;
        }
    }

    public float CurrentIntelligence
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            return unitData != null ? unitData.baseIntelligence + unitData.intelligencePerLevel * heroLevel + purchasedIntelligence : 0f;
        }
    }

    // 원작 StrAttackBonus=800 — "주스탯 1점당 공격력 +800"(ORIGINAL_HERO_STATS.md ㉠, 힘
    // 전용이 아니다). UnitData.primaryStat이 그 유닛의 주스탯을 가리킨다. None(기본값,
    // 213종)이면 0 — AttackDamage 어디에 넣어도 회귀 없음.
    //
    // ⚠️ 2026-09-07 정정(PM 지시, 리서치 판정 f924a31) — 이 값은 UpgradeMultiplier·
    // AttackPowerMultiplier(공격력 % 버프 계열) 배율 "안"에 들어간다(attackDamage와 같은
    // 기본항 취급) — ResearchBonus처럼 배율 바깥에 더하는 게 아니다. 근거: Nbr1/Blo1/Inf1
    // (그 % 버프들)이 war3map.j 원문에 0회 등장 = JASS 연산이 아니라 엔진 네이티브 버프이고,
    // 그 표준 동작이 "현재 공격력 전체(기본+주스탯+업글)에 곱한다"이다. 원문 대조가 아니라
    // "데이터로는 검증 불가한 엔진 동작"에 대한 워3 표준 동작 근거 판정(리서치가 스스로 명시한
    // 한계) — 실제 사용처는 AttackDamage 프로퍼티(위) 참고.
    const float PrimaryStatAttackBonusPerPoint = 800f;

    float PrimaryStatAttackBonus
    {
        get
        {
            UnitData unitData = identity != null ? identity.Data : null;
            if (unitData == null) return 0f;

            switch (unitData.primaryStat)
            {
                case PrimaryStat.Strength: return CurrentStrength * PrimaryStatAttackBonusPerPoint;
                case PrimaryStat.Agility: return CurrentAgility * PrimaryStatAttackBonusPerPoint;
                case PrimaryStat.Intelligence: return CurrentIntelligence * PrimaryStatAttackBonusPerPoint;
                default: return 0f;
            }
        }
    }

    // 원작 AgiAttackSpeedBonus=0.01 — AGI 1점당 공격속도 +1%, 주스탯 여부와 무관하게
    // 항상 적용된다(ORIGINAL_HERO_STATS.md ㉠ "AGI: 공격속도 +1%/점" — 주스탯 전용
    // 조항이 아니다, 부스탯 성장분에도 걸린다). CurrentAgility가 0(기본, 213종 전부와
    // 33종 중 AGI가 부스탯인 유닛의 초기 상태)이면 배율 1 — 회귀 없음. 기존 공속 배율
    // 체인(ResearchSpeedMultiplier·attackSpeedBuffs)과 곱으로 합쳐진다(AttackSpeedMultiplier
    // 참고) — 원작도 엔진 보너스가 다른 공속 원천과 곱으로 쌓이는 구조라 그대로 맞다.
    const float AgiAttackSpeedBonusPerPoint = 0.01f;

    float HeroAttackSpeedMultiplier => 1f + CurrentAgility * AgiAttackSpeedBonusPerPoint;

    // EnemyDummy.TakeDamage의 사망 처리(RewardDistributor.GrantKillReward와 같은 자리)가
    // 부른다 — laneIndex(원작 GetUnitUserData와 같은 라인 소유자 변수)의 초월함·영원한
    // 유닛 전원에게 킬 경험치 1을 준다. 누가 죽였는지는 안 본다(원작 그대로).
    public static void GrantHeroKillExperienceToLane(int laneIndex) =>
        ForEachHeroInLane(laneIndex, attacker => attacker.GainKillExperience());

    /// <summary>
    /// 원작 Trig_door_quest·Trig_Red_dog의 ForGroupBJ(udg_Exp_Group, AddHeroXP 600) — 생존 검사가 없다(죽은 플레이어의 영웅도 받는다).
    /// 대상 집합은 ForEachHeroInLane과 같지만(조합 초월함·영원한) 주인을 가리지 않는다.
    /// </summary>
    public static void GrantHeroXpToAllHeroes(int amount)
    {
        foreach (UnitIdentity identity in UnitIdentity.Active)
        {
            if (identity == null || identity.Data == null) continue;
            if (identity.Data.grade != UnitGrade.Transcendent && identity.Data.grade != UnitGrade.Eternal) continue;
            if (identity.TryGetComponent(out UnitAttacker attacker)) attacker.AddHeroXp(amount);
        }
    }

    // 도움소 「능력치 증가」 전용 — statIndex 0=STR·1=AGI·2=INT. 대상 집합이
    // GrantHeroKillExperienceToLane과 완전히 같다(원작이 같은 그룹 udg_Exp_Hero_Group을
    // 재사용) — 그래서 순회를 ForEachHeroInLane으로 뽑아 공유한다(PM 지시).
    public static void GrantHeroStatIncreaseToLane(int laneIndex, int statIndex) =>
        ForEachHeroInLane(laneIndex, attacker => attacker.AddPurchasedStat(statIndex));

    // udg_Exp_Hero_Group에 등록되는 건 아무 유닛이 아니라 **조합으로 만든 초월함·영원한
    // 등급 영웅뿐**이다(각 Trig_Eternal_* 조합 트리거가 생성 직후 그룹에 넣는다) — 그래서
    // UnitData.grade로 그 두 등급만 거른다.
    static void ForEachHeroInLane(int laneIndex, System.Action<UnitAttacker> action)
    {
        if (laneIndex < 0) return;

        foreach (UnitIdentity identity in UnitIdentity.Active)
        {
            if (identity == null || identity.Data == null) continue;
            if (identity.Data.grade != UnitGrade.Transcendent && identity.Data.grade != UnitGrade.Eternal) continue;
            if (identity.OwnerId != laneIndex) continue;
            if (identity.TryGetComponent(out UnitAttacker attacker)) action(attacker);
        }
    }

    // unitData.SkillAt(정적 데이터, UnitData.cs 참고)이 주는 슬롯 위에 런타임 오버레이
    // (06번① 능력교체형 트레잇)를 얹는다. ⚠️ 슬롯 0(첫 스킬)에만 적용한다 — 06번① 15종은
    // 지금 전부 스킬이 하나뿐이라(레거시 skill 필드) 슬롯0=유일한 스킬이라 안 갈린다. 한
    // 유닛이 06번①과 이번 다중스킬(96종)을 동시에 가지면(지금은 없음) 슬롯0만 교체된다는
    // 한계가 남는다 — 실제로 겹치면 그때 다시 설계할 것.
    SkillData ResolveSkillAt(UnitData unitData, int index)
    {
        int baseCount = BaseSkillCount(unitData);
        if (index >= baseCount && grantedSkills.Count > index - baseCount) return grantedSkills[index - baseCount].skill;   // 아군 오라로 빌린 스킬(GrantSkillToAllies)
        if (index == 0)
        {
            UnitUpgrades source = ResolveUpgrades();
            SkillData replacement = source != null ? source.ReplacementSkillFor(unitData) : null;
            if (replacement != null) return replacement;
        }
        return unitData.SkillAt(index);
    }

    // ⚠️ 2026-09-07 버그 발견·수정(PM 지시로 AttackSpeedBuffPercent 채우다가 발견) —
    // 06번① 능력교체형 15종 중 3종(초월_박민석_ADAP·초월_엄태웅_AD·초월_이재윤_AD)은
    // "구 능력 불명"이라 UnitData.skill/skills를 아예 안 채운 채로 남겨뒀다
    // (SkillCount==0). 문제: 위 두 호출부(UpdateSkillCooldown·TryCastOnHitSkill)가
    // unitData.SkillCount로 루프 상한을 정하고 그 안에서만 ResolveSkillAt(0)을 부르는데,
    // SkillCount==0이면 루프 자체가 안 돌아서 **트레잇을 사도 slot0 교체가 영영 실행되지
    // 않는다** — 나머지 12종(skills에 최소 1개 이상 채워둠)은 우연히 이 문제를 피해갔다.
    // 고정: 기본 SkillCount가 0이어도 이 유닛을 targetUnit으로 하는 언락된 트레잇에
    // replacementSkill이 있으면 최소 1로 올려 slot0 진입을 보장한다 — 나머지 12종은
    // SkillCount가 이미 1 이상이라 이 분기를 안 타므로 회귀 없음.
    int BaseSkillCount(UnitData unitData)
    {
        int count = unitData.SkillCount;
        if (count > 0) return count;
        UnitUpgrades source = ResolveUpgrades();
        return (source != null && source.ReplacementSkillFor(unitData) != null) ? 1 : 0;
    }

    int EffectiveSkillCount(UnitData unitData) => BaseSkillCount(unitData) + grantedSkills.Count;

    // 아군 오라(SkillEffectKind.GrantSkillToAllies)로 빌린 스킬 — 준 사람(source)마다 하나. 같은 스킬 에셋이 이미 있으면(내 것이든 빌린 것이든) 또 안 받는다.
    class GrantedSkill { public UnitAttacker source; public SkillData skill; }
    readonly List<GrantedSkill> grantedSkills = new List<GrantedSkill>();

    public void AddGrantedSkill(UnitAttacker source, SkillData skill)
    {
        if (skill == null || source == null) return;
        foreach (GrantedSkill g in grantedSkills) if (g.source == source && g.skill == skill) return;
        grantedSkills.Add(new GrantedSkill { source = source, skill = skill });
    }

    public void RemoveGrantedSkill(UnitAttacker source, SkillData skill)
    {
        for (int i = 0; i < grantedSkills.Count; i++)
            if (grantedSkills[i].source == source && grantedSkills[i].skill == skill) { grantedSkills.RemoveAt(i); return; }
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
    // SkillLevel.targetArmorBreakAbove — 주 대상 스킬 방깎 누적 > N(원작 AId1 레벨 비교). 0이면 통과.
    internal static bool PassesPointValueCondition(SkillEffectTargetCondition condition, float value, EnemyDummy target)
    {
        if (condition == SkillEffectTargetCondition.None) return true;
        if (target == null) return false;
        float pointValue = target.PointValue;
        return condition switch
        {
            SkillEffectTargetCondition.TargetPointValueLessThan => pointValue < value,
            SkillEffectTargetCondition.TargetPointValueEqual => pointValue == value,
            SkillEffectTargetCondition.TargetPointValueAtLeast => pointValue >= value,
            SkillEffectTargetCondition.TargetPointValueNotEqual => pointValue != value,
            _ => true,
        };
    }

    static bool PassesArmorBreakGate(SkillLevel level, EnemyDummy target) =>
        level.targetArmorBreakAbove <= 0f || (target != null && target.Aid1Shred > level.targetArmorBreakAbove);

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

    // 「적이 근처에 오면」(SkillTriggerType.OnEnemyEnterRange 주석) — 0.1초마다 감지 반경 안의 표식 없는 적을 하나씩 판정한다.
    // 한 적에 대해 이 유닛의 근접 스킬을 전부 본 뒤에 표식을 남긴다(갈래가 여러 에셋이라 먼저 남기면 뒤 갈래가 그 적을 못 본다).
    const float EnterRangeScanInterval = 0.1f;
    float enterRangeScanTimer;
    readonly List<SkillData> enterRangeSkills = new List<SkillData>();
    readonly List<string> enterRangeMarks = new List<string>();

    void TickEnterRangeSkills()
    {
        enterRangeScanTimer -= Time.deltaTime;
        if (enterRangeScanTimer > 0f) return;
        enterRangeScanTimer = EnterRangeScanInterval;

        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;
        enterRangeSkills.Clear();
        int count = EffectiveSkillCount(unitData);
        for (int i = 0; i < count; i++)
        {
            SkillData skill = ResolveSkillAt(unitData, i);
            if (skill != null && skill.triggerType == SkillTriggerType.OnEnemyEnterRange) enterRangeSkills.Add(skill);
        }
        if (enterRangeSkills.Count == 0) return;

        // 피해로 적이 죽으면 EnemyDummy.Active가 바뀐다 — 먼저 모아 두고 돈다.
        List<EnemyDummy> enemies = ListPool<EnemyDummy>.Get();
        foreach (EnemyDummy enemy in EnemyDummy.Active) if (enemy != null) enemies.Add(enemy);
        foreach (EnemyDummy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
            firedExclusiveGroups.Clear();
            enterRangeMarks.Clear();
            foreach (SkillData skill in enterRangeSkills)
            {
                SkillLevel level = CurrentSkillLevel(skill);
                if (level == null || level.effects == null || level.effects.Count == 0 || level.enterRange <= 0f) continue;
                float reach = level.enterRange / WorldScale.Value;
                if (sqr > reach * reach) continue;
                // 표식이 이미 있으면 끝. 표식만 없고 다른 버프 게이트(B06B 등)에 걸린 갈래도 표식은 남긴다.
                if (!string.IsNullOrEmpty(level.forbiddenTargetBuffId))
                {
                    if (enemy.HasBuff(level.forbiddenTargetBuffId)) continue;
                    if (!enterRangeMarks.Contains(level.forbiddenTargetBuffId)) enterRangeMarks.Add(level.forbiddenTargetBuffId);
                }
                if (!PassesBuffGate(level, enemy)) { SkillTelemetry.Gate(unitData, skill, "버프게이트"); continue; }
                if (!PassesPointValueCondition(level.primaryTargetCondition, level.primaryTargetConditionValue, enemy))
                { SkillTelemetry.Gate(unitData, skill, "대상조건"); continue; }
                if (level.exclusiveGroup != 0 && firedExclusiveGroups.Contains(level.exclusiveGroup)) { SkillTelemetry.Gate(unitData, skill, "배타"); continue; }
                if (Random.value >= level.triggerChance) { SkillTelemetry.Gate(unitData, skill, "확률실패"); continue; }
                if (level.exclusiveGroup != 0) firedExclusiveGroups.Add(level.exclusiveGroup);

                SkillTelemetry.Cast(unitData, skill);
                SkillSfx.Cast(unitData, skill, transform.position);
                PulseSphereArt();
                bool vfxBefore = SkillVfx.BeginCast(unitData, skill);
                CastSkillLevel(level, level.WorldRange, enemy, 0f);
                SkillVfx.EndCast(vfxBefore);
                if (enemy == null || enemy.IsDead) break;
            }
            if (enemy != null && !enemy.IsDead)
                foreach (string mark in enterRangeMarks) enemy.AddBuff(mark, 0f);
        }
        ListPool<EnemyDummy>.Release(enemies);
    }

    // CooldownAutoCast·Aura 전용 — OnHitChance·OnHitCount는 평타가 실제로 맞았을 때만
    // 판정해야 해서 Update()의 공격 성공 분기에서 TryCastOnHitSkill로 따로 부른다.
    void UpdateSkillCooldown()
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;

        int count = EffectiveSkillCount(unitData);
        for (int i = 0; i < count; i++)
        {
            SkillData skill = ResolveSkillAt(unitData, i);
            if (skill == null) continue;
            if (skill.triggerType != SkillTriggerType.CooldownAutoCast && skill.triggerType != SkillTriggerType.Aura)
                continue;

            SkillLevel level = CurrentSkillLevel(skill);
            if (level == null) { SkillTelemetry.Gate(unitData, skill, "레벨없음"); continue; }

            // 06번① 순위배정으로 13기의 UnitData.skill이 null이 아니게 됐지만 levels의
            // effects는 전부 빈 배열이다(수치 미상, 자리만 있음) — 여기서 걸러서 쿨다운
            // 타이머 자체가 돌지 않게 한다. 안 그러면 "숫자만 없다"가 아니라 "빈 채로 계속
            // 돌고 있다"가 된다(PM 지시, 2026-09-05).
            if (level.effects == null || level.effects.Count == 0) { SkillTelemetry.Gate(unitData, skill, "효과0(쿨·오라)"); continue; }

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
            if (!PassesBuffGate(level, null)) { SkillTelemetry.Gate(unitData, skill, "버프게이트(쿨)"); continue; }

            state.cooldownTimer = Mathf.Max(0.01f, level.cooldown);
            // recentAttackDamage: 0 — CooldownAutoCast는 "방금 맞은 평타"라는 문맥 자체가
            // 없다(TryCastOnHitSkill 쪽만 있음, 아래 참고). ReceivedDamage basis를 쓰는
            // 효과가 이 경로를 타면 0(적용 안 함)으로 안전하게 빠진다.
            SkillTelemetry.Cast(identity != null ? identity.Data : null, skill);
            SkillSfx.Cast(identity != null ? identity.Data : null, skill, transform.position);
            PulseSphereArt();
            bool vfxBefore = SkillVfx.BeginCast(identity != null ? identity.Data : null, skill);
            CastSkillLevel(level, level.WorldRange, null, 0f);
            SkillVfx.EndCast(vfxBefore);
        }
    }

    // 「스킬 중」 구체 부품 오라 — 컴포넌트는 소환 뒤에 붙을 수 있어 Awake 캐시 금지, 첫 시전 때 지연 조회(없으면 null 유지).
    UnitSphereArt sphereArt;
    bool sphereArtLooked;
    void PulseSphereArt()
    {
        if (sphereArt == null && !sphereArtLooked) { sphereArt = GetComponent<UnitSphereArt>(); sphereArtLooked = sphereArt != null; }
        if (sphereArt != null) sphereArt.PulseSkill(1f);
    }

    // ---- Aura 지속효과 — 대상 추적(Apply-once/Remove-on-exit), EnemyAuraCaster와 같은
    // 설계를 플레이어 쪽에 옮긴 것. ⚠️ 범위: ArmorBonus·HealOverTime·ApplyBuff(=지속형
    // kind) 셋만 다룬다 — Damage·Stun·ArmorBreak·ExtraProjectile은 "매 틱 다시 낸다"는
    // 뜻이 자연스러운 즉발형이라 이 경로에 안 들어온다(지금 Aura 자산 전부(H094 1건)
    // ArmorBonus뿐이라 즉발형이 Aura에 실제로 쓰인 사례가 없다 — EnemyAuraCaster도 같은
    // 전제로 Allies-target 외엔 아예 안 본다). 나중에 즉발형을 Aura에 쓸 사례가 생기면
    // 그때 이 전제를 다시 봐야 한다.

    static bool IsAuraStatKind(SkillEffectKind kind) =>
        kind == SkillEffectKind.AttackSpeedBuffPercent || kind == SkillEffectKind.AttackPowerBuffFlat || kind == SkillEffectKind.AttackPowerBuffPercent
        || kind == SkillEffectKind.AllyMoveSpeedDebuff;

    void UpdateAuraTick(SkillLevel level, SkillRuntimeState state, bool gatePasses)
    {
        state.auraAffectedEnemies ??= new List<EnemyDummy>();
        state.auraAffectedAllies ??= new List<UnitIdentity>();
        state.auraSelfAppliedBuffIds ??= new List<string>();

        // Self 효과 — 범위 개념이 없다(캐스터 자기 자신). 게이트가 막히면 뗀다, 풀리면
        // 다시 건다 — 한 번 걸고 다시 안 떼는 게 아니라 "지금 켜져 있는가"를 그대로
        // 따른다(다른 두 타겟과 같은 규칙).
        // ⚠️ 2026-09-07 추가(SkillEffectKind.AttackSpeedBuffPercent, PM 지시) — 원작009
        // H094/A0WK(자기 전용 공속+12%)가 Aura triggerType의 Self 효과라 이 자리를 탄다.
        // ApplyBuff는 이름표만(AddBuff), AttackSpeedBuffPercent는 수치까지
        // (AddAttackSpeedBuffPercent) 건다 — 둘 다 RemoveBuff(id)로 정확히 대칭 해제된다.
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Self) continue;
            if (effect.kind != SkillEffectKind.ApplyBuff && !IsAuraStatKind(effect.kind)) continue;
            if (string.IsNullOrEmpty(effect.buffId)) continue;

            // 수치 오라(공속 %·공격력 고정·%)는 레지스트리로(2026-09-30) — 같은 오라 유닛이 곁에 있어도 버프 ID가 같으면 안 겹친다.
            // 이름표(HasBuff 게이트용)는 예전처럼 같이 건다.
            bool alreadyApplied = state.auraSelfAppliedBuffIds.Contains(effect.buffId);
            if (gatePasses && !alreadyApplied)
            {
                if (IsAuraStatKind(effect.kind)) AddAuraBonus(this, effect.kind, effect.buffId, ScaledAuraValue(effect));
                AddBuff(effect.buffId, 0f);
                state.auraSelfAppliedBuffIds.Add(effect.buffId);
            }
            else if (!gatePasses && alreadyApplied)
            {
                if (IsAuraStatKind(effect.kind)) RemoveAuraBonus(this, effect.kind, effect.buffId);
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
                if (level.range > 0f && Vector3.Distance(enemy.transform.position, transform.position) > level.WorldRange) continue;
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
            ? UnitIdentity.AlliesOf(identity, level.WorldRange)
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

        RefreshScaledAuraBonuses(level, state);
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
                // 이감 오라(원작 AOae 음수 Oae1·Aasl, 2026-09-30) — 범위 안에 있는 동안 영구로 걸고,
                // 나가면 아래 Remove가 같은 값으로 뗀다. 겹치면 EnemyDummy가 남은 것 중 가장 강한 것을 다시 고른다.
                case SkillEffectKind.Slow:
                    target.AddSlow(effect.multiplier);
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
                case SkillEffectKind.Slow:
                    target.RemoveSlow(effect.multiplier);
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
            if (effect.target != SkillTargetKind.Allies) continue;
            // 2026-09-07 추가(SkillEffectKind.AttackSpeedBuffPercent, PM 지시) — 아직 이
            // 경로를 실제로 쓰는 자산은 없다(H09I/A0QZ가 후보였으나 "소환된 더미가 지속
            // 오라를 낸다"를 표현할 방법이 없어 여전히 미완성) — RemoveBuff를 Enemies
            // 타겟에도 미리 만들어둔 것과 같은 이유로, 대칭을 미리 갖춰둔다.
            // 2026-09-30 — 수치 오라는 받는 쪽 레지스트리에 「누가 줬나」와 함께 적는다(버프 ID별 최댓값, AddAuraBonus 주석).
            if (effect.kind == SkillEffectKind.GrantSkillToAllies)
            {
                if (ally.Data != null && ally.Data.grade.Tier() >= effect.minAllyTier && !ally.IsSummon) allyAttacker.AddGrantedSkill(this, effect.grantSkill);
            }
            else if (IsAuraStatKind(effect.kind))
                allyAttacker.AddAuraBonus(this, effect.kind, effect.buffId, ScaledAuraValue(effect));
            else if (effect.kind == SkillEffectKind.ApplyBuff)
                allyAttacker.AddBuff(effect.buffId, 0f);
        }
    }

    void RemovePersistentAuraEffectsFromAlly(SkillLevel level, UnitIdentity ally)
    {
        UnitAttacker allyAttacker = ally != null ? ally.GetComponent<UnitAttacker>() : null;
        if (allyAttacker == null) return;
        foreach (SkillEffect effect in level.effects)
        {
            if (effect.target != SkillTargetKind.Allies) continue;
            if (effect.kind == SkillEffectKind.GrantSkillToAllies) allyAttacker.RemoveGrantedSkill(this, effect.grantSkill);
            else if (IsAuraStatKind(effect.kind)) allyAttacker.RemoveAuraBonus(this, effect.kind, effect.buffId);
            else if (effect.kind == SkillEffectKind.ApplyBuff) allyAttacker.RemoveBuff(effect.buffId);
        }
    }

    // 간호학과대표(SkillEffectKind.AttackSpeedStack) — 평타마다 공속 +stackPerHit 누적(상한 stackCap), 마지막 평타 뒤 stackResetSeconds초 지나면 0.
    // 값은 오라 레지스트리(버프 id 하나, AttackSpeedBuffPercent)에 매번 다시 적는다 — 다른 공속 오라와는 곱으로 겹친다.
    const string AttackSpeedStackId = "ATTACK_SPEED_STACK";
    float attackSpeedStack, attackSpeedStackResetAt;

    void AddAttackSpeedStack(SkillEffect effect)
    {
        attackSpeedStack = Mathf.Min(effect.stackCap, attackSpeedStack + effect.stackPerHit);
        attackSpeedStackResetAt = Time.time + effect.stackResetSeconds;
        RemoveAuraBonus(this, SkillEffectKind.AttackSpeedBuffPercent, AttackSpeedStackId);
        AddAuraBonus(this, SkillEffectKind.AttackSpeedBuffPercent, AttackSpeedStackId, attackSpeedStack);
    }

    public float AttackSpeedStackValue => attackSpeedStack;

    void TickAttackSpeedStack()
    {
        if (attackSpeedStack <= 0f || Time.time < attackSpeedStackResetAt) return;
        attackSpeedStack = 0f;
        RemoveAuraBonus(this, SkillEffectKind.AttackSpeedBuffPercent, AttackSpeedStackId);
    }

    // 유닛삭제(SkillEffectKind.KillNormalEnemies) — 범위 안에서 가장 가까운 「일반 적」(보스·스토리·신세계 광폭화 제외) 한 기.
    static bool IsNormalEnemy(EnemyDummy e) => e != null && !e.IsBoss && e.PointValue < 200f && !e.HasBuff("B06B");

    EnemyDummy NearestNormalEnemy(float worldRange)
    {
        EnemyDummy best = null;
        float bestSqr = worldRange > 0f ? worldRange * worldRange : float.MaxValue;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (!IsNormalEnemy(enemy)) continue;
            float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            best = enemy;
        }
        return best;
    }

    bool HasNormalEnemyInRange(float worldRange) => NearestNormalEnemy(worldRange) != null;

    void KillNearestNormalEnemy(float worldRange)
    {
        EnemyDummy target = NearestNormalEnemy(worldRange);
        if (target == null) return;
        // 막타 피해로 처리 — 일반 처치와 같은 경로(보상·처치 알림)를 탄다. 방어·상성은 무시하고 확실히 죽는 크기.
        target.TakeDamage(1e12f, DamageType.AD, AttackType.Unassigned, owner != null ? owner.OwnerId : -1, armorIgnoreRatio: 1f, isAbilityDamage: false);
    }

    // 보잡(SkillEffectKind.BossDamageMultiplier) — 이 유닛 스킬 중 패시브 배율의 곱. 보스(EnemyDummy.IsBoss) 상대 평타·스킬 최종 피해에 곱한다.
    // 유닛 종류가 바뀔 때만 다시 센다(Awake 캐시 금지 — identity.Data가 늦게 세워진다).
    UnitData bossMultiplierFor;
    float bossMultiplier = 1f;

    float BossDamageFactor(EnemyDummy target)
    {
        if (target == null || !target.IsBoss) return 1f;
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return 1f;
        if (bossMultiplierFor != unitData)
        {
            bossMultiplierFor = unitData;
            bossMultiplier = 1f;
            int count = BaseSkillCount(unitData);
            for (int i = 0; i < count; i++)
            {
                SkillData skill = ResolveSkillAt(unitData, i);
                if (skill == null || skill.levels == null || skill.levels.Count == 0 || skill.levels[0].effects == null) continue;
                foreach (SkillEffect effect in skill.levels[0].effects)
                    if (effect != null && effect.kind == SkillEffectKind.BossDamageMultiplier && effect.multiplier > 0f) bossMultiplier *= effect.multiplier;
            }
        }
        return bossMultiplier;
    }

    // 아군발 디버프 개수 비례 피해(SkillEffectKind.DamagePerAllyDebuff, 초월 강재규) — 패시브 비율·상한을 스킬에서 읽어 둔다.
    UnitData debuffRateFor;
    float debuffRate, debuffCap;

    float AllyDebuffDamageFactor()
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null || auraBonuses.Count == 0) return 1f;
        if (debuffRateFor != unitData)
        {
            debuffRateFor = unitData;
            debuffRate = 0f; debuffCap = 0f;
            int count = BaseSkillCount(unitData);
            for (int i = 0; i < count; i++)
            {
                SkillData skill = ResolveSkillAt(unitData, i);
                if (skill == null || skill.levels == null || skill.levels.Count == 0 || skill.levels[0].effects == null) continue;
                foreach (SkillEffect effect in skill.levels[0].effects)
                    if (effect != null && effect.kind == SkillEffectKind.DamagePerAllyDebuff) { debuffRate += effect.multiplier; debuffCap = Mathf.Max(debuffCap, effect.bonus); }
            }
        }
        if (debuffRate <= 0f) return 1f;
        int debuffs = CountAllyDebuffs();
        if (debuffs == 0) return 1f;
        float extra = debuffRate * debuffs;
        if (debuffCap > 0f) extra = Mathf.Min(extra, debuffCap);
        return 1f + extra;
    }

    static readonly HashSet<string> debuffIdScratch = new HashSet<string>();

    // 받고 있는 아군발 디버프 가짓수(같은 ID는 하나) — 준 쪽이 사라졌거나 최윤서 강화로 꺼진 건 세지 않는다.
    public int CountAllyDebuffs()
    {
        debuffIdScratch.Clear();
        for (int i = 0; i < auraBonuses.Count; i++)
        {
            AuraBonus b = auraBonuses[i];
            if (b.source == null || b.kind != SkillEffectKind.AllyMoveSpeedDebuff) continue;
            if (b.source is UnitAttacker debuffer && debuffer.YoonseoEnhanced) continue;
            debuffIdScratch.Add(b.id);
        }
        return debuffIdScratch.Count;
    }

    float DamagePassiveFactor(EnemyDummy target) => BossDamageFactor(target) * AllyDebuffDamageFactor();

    // 만성피로(SelfStunRefillLifeGauge) — 자기 스턴 동안 공격·스킬이 멈추고, 끝나면 체력 게이지가 즉시 가득 찬다.
    public const string SelfStunBuffId = "SELF_STUN";
    float selfStunUntil;
    bool selfStunActive;
    public bool IsSelfStunned => Time.time < selfStunUntil;

    void BeginSelfStun(float duration)
    {
        if (duration <= 0f) return;
        selfStunUntil = Mathf.Max(selfStunUntil, Time.time + duration);
        selfStunActive = true;
        AddBuff(SelfStunBuffId, duration);
    }

    // Update 맨 앞에서: 스턴이 끝난 순간 게이지를 채운다(true면 아직 스턴 중 — 호출한 쪽이 공격·스킬을 건너뛴다).
    bool TickSelfStun()
    {
        if (!selfStunActive) return false;
        if (Time.time < selfStunUntil) return true;
        selfStunActive = false;
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData != null) { lifeGaugeCounter = LifeGaugeCap(unitData) == int.MaxValue ? lifeGaugeCounter : LifeGaugeCap(unitData); lifeGaugeInitialized = true; }
        return false;
    }

    // 폭발증폭(SkillEffectKind.SplashDamageMultiplier) — 이 유닛 스킬 중 패시브 배율의 곱. 평타 광역(ApplyAttackSplash) 피해에만 곱한다.
    UnitData splashMultiplierFor;
    float splashMultiplier = 1f;

    float SplashDamageFactor(UnitData unitData)
    {
        if (splashMultiplierFor != unitData)
        {
            splashMultiplierFor = unitData;
            splashMultiplier = 1f;
            int count = BaseSkillCount(unitData);
            for (int i = 0; i < count; i++)
            {
                SkillData skill = ResolveSkillAt(unitData, i);
                if (skill == null || skill.levels == null || skill.levels.Count == 0 || skill.levels[0].effects == null) continue;
                foreach (SkillEffect effect in skill.levels[0].effects)
                    if (effect != null && effect.kind == SkillEffectKind.SplashDamageMultiplier && effect.multiplier > 0f) splashMultiplier *= effect.multiplier;
            }
        }
        return splashMultiplier;
    }

    // ---- 최윤서 강화(초월 노태현, 사장님 10-06) — 영구 상태. 켜지면 방무딜(armorIgnoreRequiresBuff) + 아군 이속 감소 디버프 100% 제거. ----
    public const string YoonseoBuffId = "YOONSEO_ENHANCED";
    public bool YoonseoEnhanced => HasBuff(YoonseoBuffId);
    public void SetYoonseoEnhanced() { if (!YoonseoEnhanced) AddBuff(YoonseoBuffId, 0f); }

    // ---- 아군 이속 감소(AllyMoveSpeedDebuff) 적용 — 오라가 받는 쪽 레지스트리에 넣은 값을 NavMeshAgent 속도에 반영한다. 0.25초마다. ----
    float moveDebuffTimer;
    float baseMoveSpeed = -1f;
    bool moveDebuffApplied;

    void TickMoveSpeedDebuff()
    {
        moveDebuffTimer -= Time.deltaTime;
        if (moveDebuffTimer > 0f) return;
        moveDebuffTimer = 0.25f;
        float reduction = auraBonuses.Count == 0 ? 0f : Mathf.Clamp(AuraBonusTotal(SkillEffectKind.AllyMoveSpeedDebuff, false), 0f, 0.9f);
        if (reduction <= 0f && !moveDebuffApplied) return;
        if (!TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) return;
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;
        if (baseMoveSpeed < 0f) baseMoveSpeed = unitData.moveSpeed;
        agent.speed = baseMoveSpeed * (1f - reduction);
        moveDebuffApplied = reduction > 0f;
    }

    // 소환(SkillEffectKind.SummonUnit) — 종류마다 동시 1기. 이미 있으면 남은 시간을 되돌린다. 자리는 시전자 앞쪽 부채꼴(가운데 = 시전자가 보는 방향).
    // 부채꼴 각도는 종류 개수로 −fan·0·+fan 식으로 나눈다(1기=가운데, 2기=±fan/2, 3기=−fan·0·+fan). 자리는 NavMesh.SamplePosition으로 보정하고 지상 유닛을 바다에 안 세운다.
    readonly Dictionary<UnitData, GameObject> summonedByKind = new Dictionary<UnitData, GameObject>();

    void SummonFor(SkillEffect effect)
    {
        if (!GameAuthority.IsServer || effect.summonUnits == null || effect.summonUnits.Count == 0 || owner == null) return;
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        if (spawner == null) return;

        Vector3 forward = transform.forward; forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        int n = effect.summonUnits.Count;
        for (int i = 0; i < n; i++)
        {
            UnitData kind = effect.summonUnits[i];
            if (kind == null) continue;
            if (summonedByKind.TryGetValue(kind, out GameObject existing) && existing != null)
            {
                if (existing.TryGetComponent(out TimedLife life)) life.Begin(effect.summonLifetime);   // 이미 있으면 새로 안 만들고 남은 시간만 되돌린다
                continue;
            }
            float angle = n == 1 ? 0f : Mathf.Lerp(-effect.summonFanDegrees, effect.summonFanDegrees, i / (float)(n - 1));
            Vector3 position = FanPosition(forward, angle, effect.summonRadius, kind);
            GameObject summoned = spawner.Spawn(kind, position, owner.OwnerId, summoned: true);
            if (summoned == null) continue;
            summoned.AddComponent<TimedLife>().Begin(effect.summonLifetime);
            summonedByKind[kind] = summoned;
        }
    }

    // 시전자 앞 부채꼴의 한 자리 — 걸을 수 있는 땅이 아니면(섬 밖·벽 안) 각도를 가운데 쪽으로 좁혀 다시 본다.
    Vector3 FanPosition(Vector3 forward, float angleDegrees, float radius, UnitData kind)
    {
        float world = radius;   // summonRadius는 이미 월드 단위(유닛 키 48 기준 약 70) — 원작 단위가 아니라 WorldScale로 안 나눈다
        int mask = UnitSpawner.ComputeAreaMask(kind.movementAbility);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float angle = angleDegrees * (1f - attempt * 0.25f);   // 각도를 25%씩 가운데로
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 want = transform.position + dir * world;
            if (UnityEngine.AI.NavMesh.SamplePosition(want, out UnityEngine.AI.NavMeshHit hit, world * 0.5f, mask)) return hit.position;
        }
        return transform.position;   // 끝내 못 찾으면 시전자 자리(겹치지만 소환은 된다)
    }

    // 오라 수치의 실제 값 — 「전설 이상 유닛 수 비례」(SkillEffect.perHighGradeUnitBonus)가 있으면 개수만큼 더한다(상한 있음).
    float ScaledAuraValue(SkillEffect effect)
    {
        if (effect.perHighGradeUnitBonus <= 0f) return effect.multiplier;
        float extra = effect.perHighGradeUnitBonus * CountHighGradeUnits();
        if (effect.perHighGradeUnitBonusCap > 0f) extra = Mathf.Min(extra, effect.perHighGradeUnitBonusCap);
        return effect.multiplier + extra;
    }

    // 주인의 전설 이상(등급 서열 ≥ 5, 초월위습 제외) 유닛 수 — 소환수 제외. 오라가 1초마다 부른다.
    int CountHighGradeUnits()
    {
        int ownerId = owner != null ? owner.OwnerId : -1;
        int count = 0;
        foreach (UnitIdentity unit in UnitIdentity.Active)
        {
            if (unit == null || unit.IsSummon || unit.Data == null || unit.OwnerId != ownerId) continue;
            if (unit.Data.grade == UnitGrade.TranscendentWisp || unit.Data.grade.Tier() < 5) continue;
            count++;
        }
        return count;
    }

    // 개수 비례 오라 — 개수가 바뀌었을 수 있으니 이미 걸린 값을 지우고 다시 건다(UpdateAuraTick 끝, 1초 주기).
    void RefreshScaledAuraBonuses(SkillLevel level, SkillRuntimeState state)
    {
        foreach (SkillEffect effect in level.effects)
        {
            if (!IsAuraStatKind(effect.kind) || effect.perHighGradeUnitBonus <= 0f || string.IsNullOrEmpty(effect.buffId)) continue;
            float value = ScaledAuraValue(effect);
            if (effect.target == SkillTargetKind.Self)
            {
                if (!state.auraSelfAppliedBuffIds.Contains(effect.buffId)) continue;
                RemoveAuraBonus(this, effect.kind, effect.buffId);
                AddAuraBonus(this, effect.kind, effect.buffId, value);
            }
            else if (effect.target == SkillTargetKind.Allies)
            {
                foreach (UnitIdentity ally in state.auraAffectedAllies)
                {
                    UnitAttacker a = ally != null ? ally.GetComponent<UnitAttacker>() : null;
                    if (a == null) continue;
                    a.RemoveAuraBonus(this, effect.kind, effect.buffId);
                    a.AddAuraBonus(this, effect.kind, effect.buffId, value);
                }
            }
        }
    }

    // 이번 평타에서 굴림을 맞힌 배타 묶음(SkillLevel.exclusiveGroup) — TryCastOnHitSkill이 평타마다 비운다.
    readonly HashSet<int> firedExclusiveGroups = new HashSet<int>();

    void TryCastOnHitSkill(EnemyDummy attackedTarget)
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;
        SkillTelemetry.Hit(unitData);

        int count = EffectiveSkillCount(unitData);
        if (count == 0) { SkillTelemetry.Gate(unitData, null, "스킬수0"); return; }

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

        firedExclusiveGroups.Clear();
        for (int i = 0; i < count; i++)
        {
            SkillData skill = ResolveSkillAt(unitData, i);
            if (skill == null) { SkillTelemetry.Gate(unitData, null, "슬롯 비어 있음"); continue; }
            if (skill.triggerType != SkillTriggerType.OnHitChance && skill.triggerType != SkillTriggerType.OnHitCount)
                continue;

            SkillLevel level = CurrentSkillLevel(skill);
            if (level == null || level.effects == null || level.effects.Count == 0) { SkillTelemetry.Gate(unitData, skill, "효과0"); continue; }

            // 버프 게이트(requiredBuffId/forbiddenBuffId, 2026-09-06) — OnHitChance·
            // OnHitCount 둘 다 판정 시작 전에 먼저 걸린다. 원작 예: 드래곤 "B00J 미보유",
            // 루피 "B06Y 미보유 AND 1/80"(뒤의 확률은 아래 triggerChance가 그대로 처리).
            // ⚠️ 06번①(2026-09-06) — attackedTarget을 그대로 넘겨 requiredTargetBuffId/
            // forbiddenTargetBuffId(대상의 버프)도 같이 판정한다. 원작 예: B06B(신세계
            // 광폭화 몬스터 전용) — "대상이 이 상태일 때만 발동"은 캐스터가 아니라
            // attackedTarget의 버프를 봐야 한다.
            if (!PassesBuffGate(level, attackedTarget)) { SkillTelemetry.Gate(unitData, skill, "버프게이트"); continue; }
            // 스킬 단위 대상 조건(SkillLevel.primaryTargetCondition) — 못 채우면 확률·게이지를 건드리지 않고 건너뛴다.
            if (!PassesPointValueCondition(level.primaryTargetCondition, level.primaryTargetConditionValue, attackedTarget))
            { SkillTelemetry.Gate(unitData, skill, "대상조건"); continue; }

            if (skill.triggerType == SkillTriggerType.OnHitChance)
            {
                // 절대쿨(SkillLevel.cooldown 주석 참고, PM 지시 2026-09-05) — 원작은 버프
                // 검사가 바깥 if라서, 잠긴 동안은 확률 판정까지 안 간다. cooldown<=0이면 이
                // 줄이 항상 통과해 기존 동작과 완전히 같다(회귀 없음).
                SkillRuntimeState state = GetRuntimeState(skill);
                if (level.cooldown > 0f && Time.time < state.onHitChanceLockedUntil) { SkillTelemetry.Gate(unitData, skill, "절대쿨"); continue; }

                if (!PassesArmorBreakGate(level, attackedTarget)) { SkillTelemetry.Gate(unitData, skill, "방깎게이트"); continue; }
                // 배타 분기(SkillLevel.exclusiveGroup) — 같은 묶음의 앞선 스킬이 이번 평타에 굴림을 맞혔으면 굴리지도 않는다.
                if (level.exclusiveGroup != 0 && firedExclusiveGroups.Contains(level.exclusiveGroup)) { SkillTelemetry.Gate(unitData, skill, "배타"); continue; }
                if (Random.value >= level.triggerChance) { SkillTelemetry.Gate(unitData, skill, "확률실패"); continue; }
                if (level.exclusiveGroup != 0) firedExclusiveGroups.Add(level.exclusiveGroup);

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
            else if (level.hitCountFloor > 0)
            {
                // ⚠️ 2026-09-06 추가(카타쿠리/B045, PM 지시) — "바닥 조건" 모드
                // (SkillLevel.hitCountFloor 주석 참고): "정확히 N타째"가 아니라 "게이지가
                // N을 넘는 동안"이다. 자동 리셋이 없다 — 넘긴 채로 계속 있다가 매 타
                // triggerChance를 굴리고, 발동에 성공했을 때만 gaugeSpendAmount만큼 깎는다
                // (원작 "1/7 AND LIFE>36 → 발동 시 LIFE−17"). 실패해도 카운터는 그대로다
                // (아래 위쪽 hitCountThreshold 경로처럼 실패해도 리셋되는 것과 다르다 —
                // 여기는 애초에 자동 리셋 개념이 없다).
                if (level.gaugeKind == SkillGaugeKind.Mana)
                {
                    if (!manaGaugeInitialized) { manaGaugeCounter = ManaGaugeStart(unitData, level); manaGaugeInitialized = true; }
                    if (!manaIncremented) { manaGaugeCounter = Mathf.Min(manaGaugeCounter + 1, ManaGaugeCap(unitData)); manaIncremented = true; }
                    if (manaGaugeCounter <= level.hitCountFloor) { SkillTelemetry.Gate(unitData, skill, "게이지바닥미달"); continue; }
                }
                else
                {
                    if (!lifeGaugeInitialized) { lifeGaugeCounter = LifeGaugeStart(unitData, level); lifeGaugeInitialized = true; }
                    if (!lifeIncremented) { lifeGaugeCounter = Mathf.Min(lifeGaugeCounter + LifeGaugeHitGain(unitData), LifeGaugeCap(unitData)); lifeIncremented = true; }
                    if (lifeGaugeCounter <= level.hitCountFloor) { SkillTelemetry.Gate(unitData, skill, "게이지바닥미달"); continue; }
                }

                if (!PassesArmorBreakGate(level, attackedTarget)) { SkillTelemetry.Gate(unitData, skill, "방깎게이트"); continue; }
                if (Random.value >= level.triggerChance) { SkillTelemetry.Gate(unitData, skill, "바닥후확률실패"); continue; }

                // "발동 시에만" 차감 — 위 hitCountThreshold 경로의 resetTo(확률과 무관하게
                // 항상 적용)와 다르다, 여기까지 왔다는 건 이미 확률까지 통과했다는 뜻이라
                // 바로 깎아도 된다(뒤쪽 스킬이 이번 평타에 같은 게이지를 또 봐야 하는
                // 사례는 지금 김민규에게 없다 — 있으면 그때 defer 방식으로 바꿀 것).
                if (level.gaugeSpendAmount > 0)
                {
                    if (level.gaugeKind == SkillGaugeKind.Mana)
                        manaGaugeCounter = Mathf.Max(0, manaGaugeCounter - level.gaugeSpendAmount);
                    else
                        lifeGaugeCounter = Mathf.Max(0, lifeGaugeCounter - level.gaugeSpendAmount);
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
                    if (!manaGaugeInitialized) { manaGaugeCounter = ManaGaugeStart(unitData, level); manaGaugeInitialized = true; }
                    if (!manaIncremented) { manaGaugeCounter = Mathf.Min(manaGaugeCounter + 1, ManaGaugeCap(unitData)); manaIncremented = true; }
                    if (manaGaugeCounter < level.hitCountThreshold) { SkillTelemetry.Gate(unitData, skill, "게이지미달(마나)"); continue; }
                    if (!PassesArmorBreakGate(level, attackedTarget)) { SkillTelemetry.Gate(unitData, skill, "방깎게이트"); continue; }
                    if (level.requireNormalEnemyInRange && !HasNormalEnemyInRange(level.WorldRange)) { SkillTelemetry.Gate(unitData, skill, "일반적없음"); continue; }
                    manaShouldReset = true;
                    manaResetValue = level.resetTo;
                }
                else
                {
                    if (!lifeGaugeInitialized) { lifeGaugeCounter = LifeGaugeStart(unitData, level); lifeGaugeInitialized = true; }
                    if (!lifeIncremented) { lifeGaugeCounter = Mathf.Min(lifeGaugeCounter + LifeGaugeHitGain(unitData), LifeGaugeCap(unitData)); lifeIncremented = true; }
                    if (lifeGaugeCounter < level.hitCountThreshold) { SkillTelemetry.Gate(unitData, skill, "게이지미달(생명)"); continue; }
                    if (!PassesArmorBreakGate(level, attackedTarget)) { SkillTelemetry.Gate(unitData, skill, "방깎게이트"); continue; }
                    if (level.requireNormalEnemyInRange && !HasNormalEnemyInRange(level.WorldRange)) { SkillTelemetry.Gate(unitData, skill, "일반적없음"); continue; }
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
                if (Random.value >= level.triggerChance) { SkillTelemetry.Gate(unitData, skill, "게이지후확률실패"); continue; }
            }

            // recentAttackDamage: 방금 이 평타로 실제로 나간 피해량(AttackDamage) — 원작
            // GetEventDamage()에 대응한다. OnHitChance/OnHitCount는 "평타가 맞았을 때"만
            // 도는 경로라 이 값이 항상 뜻이 통한다(아래 ResolveSkillEffectValue.
            // ReceivedDamage 참고, 2026-09-06 PM 지시로 연결).
            SkillTelemetry.Cast(unitData, skill);
            SkillSfx.Cast(unitData, skill, transform.position);
            PulseSphereArt();
            bool vfxBefore = SkillVfx.BeginCast(unitData, skill);
            CastSkillLevel(level, level.WorldRange, attackedTarget, AttackDamage);
            SkillVfx.EndCast(vfxBefore);
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

        // 캐스케이드 그룹(2026-09-06, "캐스케이드 그룹" — SkillEffect.cascadeGroup 주석
        // 참고) 추적. 대상(EnemyDummy/UnitIdentity, object로 키를 잡는다)별로 "이 시전
        // 안에서 이미 발동한 그룹 번호"를 기억한다 — 이 CastSkillLevel 호출 하나에만
        // 산다(다음 평타·발동에선 새로 만든다, 영구 상태 아님).
        Dictionary<object, HashSet<int>> firedCascadeGroups = new Dictionary<object, HashSet<int>>();

        // 범위 중심은 시전 시작 때 한 번 잡는다 — 앞 효과가 주 대상을 죽여도 같은 자리에서
        // 나머지 효과가 터진다(원작도 GetUnitLoc를 먼저 저장해 두고 그 점을 쓴다).
        Vector3 aoeCenter = level.aoeCenter == SkillAoeCenter.Target && primaryTarget != null
            ? primaryTarget.transform.position
            : transform.position;
        // 스킬별 이펙트(09-30): 범위 중심 땅·시전자 발밑에 한 번 — 적중 이펙트는 피해마다 EnemyDummy 쪽에서 바뀐다.
        SkillVfx.CastAt(aoeCenter, transform.position, range);

        randomEnemyPicked = false;
        randomEnemyPick = null;
        foreach (SkillEffect effect in level.effects)
        {
            if (effect == null) continue;
            // 그룹 없음(0, 기본값) — 기존과 완전히 같다(회귀 없음): 시전 단위로 한 번만
            // chance를 굴린다. 그룹 있음 — 여기서 안 굴린다. 대상마다 따로 굴려야 하므로
            // ApplyToEnemy/ApplyToAlly로 미뤄서 그쪽에서 판정한다(범위 스킬이면 적마다
            // 독립된 캐스케이드가 되도록).
            if (effect.cascadeGroup == 0 && Random.value >= effect.chance) continue;
            ApplySkillEffect(effect, range, aoeCenter, primaryTarget, recentAttackDamage, firedCascadeGroups);
        }
    }

    // 캐스케이드 그룹 추적용 — target(EnemyDummy 또는 UnitIdentity, object로 통일)마다
    // 처음 보면 새 HashSet을 만들어 등록하고, 이미 있으면 그걸 그대로 돌려준다.
    static HashSet<int> GetFiredCascadeGroups(Dictionary<object, HashSet<int>> firedCascadeGroups, object targetKey)
    {
        if (!firedCascadeGroups.TryGetValue(targetKey, out HashSet<int> fired))
        {
            fired = new HashSet<int>();
            firedCascadeGroups[targetKey] = fired;
        }
        return fired;
    }

    // 데이터 사고 방지 — target이 Enemies/Allies인데 range<=0이면 거리 검사 자체가 빠져
    // 맵 전체(EnemyDummy.Active/AlliesOf 전원)를 때린다(check_required_fields.py #14가 같은
    // 위험을 데이터 단계에서 잡는다). 핸콕에서 실제로 760을 0으로 비워둔 채 커밋할 뻔했다
    // (2026-09-05, PM 지시로 런타임에도 가드 추가). 콘솔이 도배되지 않게 한 번만 찍는다.
    static bool loggedUnboundedRange;

    // 이번 시전에서 뽑은 무작위 적(RandomEnemyInRange) — CastSkillLevel이 시전마다 비운다.
    bool randomEnemyPicked;
    EnemyDummy randomEnemyPick;
    // 연쇄(ChainEnemies)가 지금 몇 번째 적을 치고 있나에 따른 피해 배수 — 연쇄 밖에선 늘 1.
    float chainDamageScale = 1f;

    void ApplySkillEffect(SkillEffect effect, float range, Vector3 aoeCenter, EnemyDummy primaryTarget, float recentAttackDamage,
        Dictionary<object, HashSet<int>> firedCascadeGroups)
    {
        // 소환(최상호 구일) — 대상이 없다. 확률·쿨다운은 위(CastSkillLevel·평타 확률 발동)가 이미 판정했다.
        if (effect.kind == SkillEffectKind.SummonUnit) { SummonFor(effect); return; }
        if (effect.kind == SkillEffectKind.KillNormalEnemies) { KillNearestNormalEnemy(range); return; }
        if (effect.kind == SkillEffectKind.AttackSpeedStack) { AddAttackSpeedStack(effect); return; }
        if (effect.kind == SkillEffectKind.SelfStunRefillLifeGauge) { BeginSelfStun(effect.duration); return; }
        // 아군에게 스킬 빌려주기·보스 배율은 오라/패시브로만 쓴다(여기서는 할 일 없음).
        if (effect.kind == SkillEffectKind.GrantSkillToAllies || effect.kind == SkillEffectKind.BossDamageMultiplier
            || effect.kind == SkillEffectKind.SplashDamageMultiplier || effect.kind == SkillEffectKind.AllyMoveSpeedDebuff
            || effect.kind == SkillEffectKind.DamagePerAllyDebuff) return;

        // 장풍 직선(SkillEffect.lineLength 주석) — 시전자에서 범위 중심 쪽으로 뻗는 사다리꼴 안의 적 모두.
        if (effect.lineLength > 0f && effect.zoneTickInterval <= 0f)
        {
            Vector3 dir = aoeCenter - transform.position; dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            float length = effect.lineLength / WorldScale.Value;
            float startRadius = effect.lineStartRadius / WorldScale.Value, endRadius = effect.lineEndRadius / WorldScale.Value;
            List<EnemyDummy> inLine = ListPool<EnemyDummy>.Get();
            foreach (EnemyDummy enemy in EnemyDummy.Active)
            {
                if (enemy == null) continue;
                Vector3 to = enemy.transform.position - transform.position; to.y = 0f;
                float along = Vector3.Dot(to, dir);
                if (along < 0f || along > length) continue;
                float across = (to - dir * along).magnitude;
                if (across <= Mathf.Lerp(startRadius, endRadius, along / length)) inLine.Add(enemy);
            }
            foreach (EnemyDummy enemy in inLine)
                if (enemy != null) ApplyToEnemy(effect, enemy, recentAttackDamage, firedCascadeGroups);
            ListPool<EnemyDummy>.Release(inLine);
            return;
        }

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

        // 주기 피해 지대(SkillEffect.zoneTickInterval 주석) — 대상 종류와 무관하게 범위 중심에 세우고 끝. 틱은 SkillDamageZone이 센다.
        if (effect.kind == SkillEffectKind.Damage && effect.zoneTickInterval > 0f)
        {
            if (effect.duration <= 0f) return;
            int zones = Mathf.Max(1, effect.hitCount);
            Vector3 forward = aoeCenter - transform.position; forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            float radius = effect.zoneRadius > 0f ? effect.zoneRadius / WorldScale.Value : range;
            for (int i = 0; i < zones; i++)
            {
                Vector3 at = effect.zoneSpacing > 0f ? transform.position + forward * (effect.zoneSpacing / WorldScale.Value * (i + 1))
                    : effect.zoneAtCaster ? transform.position : aoeCenter;
                SkillDamageZone.Spawn(at, radius, effect, identity != null ? identity.Data : null, owner != null ? owner.OwnerId : -1, SkillVfx.CasterAllowsVfx);
            }
            return;
        }

        switch (effect.target)
        {
            case SkillTargetKind.Enemies:
                // 🔴 2026-09-29 — EnemyDummy.Active를 그대로 돌면 피해로 적이 죽는 순간 목록이 바뀌어
                //    InvalidOperationException(Collection was modified)이 났다(스킬 계측 판 127회). 예외는 이 시전의 남은 적·남은 효과를
                //    건너뛰고, 위로 올라가 TryCastOnHitSkill의 게이지 리셋·평타 N회 버프 차감까지 끊었다 → 게이지 스킬이
                //    처치 뒤 몇 타마다 계속 터졌다(진연서 LIFE33이 172타에 48번 — 정상 5번). 사거리 안 적을 먼저 모아 두고 돈다.
                List<EnemyDummy> inRange = ListPool<EnemyDummy>.Get();
                foreach (EnemyDummy enemy in EnemyDummy.Active)
                {
                    if (enemy == null) continue;
                    if (range > 0f && Vector3.Distance(enemy.transform.position, aoeCenter) > range) continue;
                    inRange.Add(enemy);
                }
                // 맞는 수 상한(SkillEffect.maxTargets) — 범위 중심에서 가까운 순으로.
                if (effect.maxTargets > 0 && inRange.Count > effect.maxTargets)
                {
                    inRange.Sort((a, b) => (a.transform.position - aoeCenter).sqrMagnitude.CompareTo((b.transform.position - aoeCenter).sqrMagnitude));
                    inRange.RemoveRange(effect.maxTargets, inRange.Count - effect.maxTargets);
                }
                foreach (EnemyDummy enemy in inRange)
                    if (enemy != null) ApplyToEnemy(effect, enemy, recentAttackDamage, firedCascadeGroups);
                ListPool<EnemyDummy>.Release(inRange);
                break;

            // 연쇄(SkillTargetKind.ChainEnemies 주석) — 주 대상에서 시작해 방금 맞은 적에서 range 안의 안 맞은 가장 가까운 적으로.
            case SkillTargetKind.ChainEnemies:
            {
                EnemyDummy current = primaryTarget != null ? primaryTarget : FindClosestEnemyWithin(range);
                if (current == null) break;
                List<EnemyDummy> chain = ListPool<EnemyDummy>.Get();
                int limit = Mathf.Max(1, effect.maxTargets);
                while (current != null && chain.Count < limit)
                {
                    chain.Add(current);
                    EnemyDummy next = null;
                    float best = range > 0f ? range * range : float.MaxValue;
                    foreach (EnemyDummy enemy in EnemyDummy.Active)
                    {
                        if (enemy == null || enemy.IsDead || chain.Contains(enemy)) continue;
                        float sqr = (enemy.transform.position - current.transform.position).sqrMagnitude;
                        if (sqr <= best) { best = sqr; next = enemy; }
                    }
                    current = next;
                }
                chainDamageScale = 1f;
                foreach (EnemyDummy enemy in chain)
                {
                    if (enemy != null) ApplyToEnemy(effect, enemy, recentAttackDamage, firedCascadeGroups);
                    chainDamageScale *= 1f + effect.chainDamageStep;
                }
                chainDamageScale = 1f;
                ListPool<EnemyDummy>.Release(chain);
                break;
            }

            // 반경 안 무작위 적 하나(SkillTargetKind.RandomEnemyInRange 주석) — 시전마다 한 번만 뽑아 그 시전의 효과들이 같이 쓴다.
            case SkillTargetKind.RandomEnemyInRange:
                if (!randomEnemyPicked)
                {
                    randomEnemyPicked = true;
                    List<EnemyDummy> pool = ListPool<EnemyDummy>.Get();
                    foreach (EnemyDummy enemy in EnemyDummy.Active)
                    {
                        if (enemy == null || enemy.IsDead) continue;
                        if (range > 0f && Vector3.Distance(enemy.transform.position, aoeCenter) > range) continue;
                        pool.Add(enemy);
                    }
                    randomEnemyPick = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
                    ListPool<EnemyDummy>.Release(pool);
                }
                if (randomEnemyPick != null) ApplyToEnemy(effect, randomEnemyPick, recentAttackDamage, firedCascadeGroups);
                break;

            case SkillTargetKind.SingleTarget:
                EnemyDummy target = primaryTarget != null ? primaryTarget : FindClosestEnemyWithin(range);
                if (target != null) ApplyToEnemy(effect, target, recentAttackDamage, firedCascadeGroups);
                break;

            case SkillTargetKind.Self:
                if (identity != null) ApplyToAlly(effect, identity, firedCascadeGroups);
                break;

            case SkillTargetKind.Allies:
                // "같은 편"은 UnitIdentity.AlliesOf(소유자 기준)로 푼다 — 캐스터가 플레이어
                // 유닛일 때의 정의다. 캐스터가 보스(EnemyDummy)면 EnemyDummy.AlliesOf를 쓴다
                // (04번 오라가 그쪽이다) — UnitAttacker는 플레이어 유닛에만 붙으므로 여기선
                // 이 갈래만 있으면 된다.
                foreach (UnitIdentity ally in UnitIdentity.AlliesOf(identity, range))
                    ApplyToAlly(effect, ally, firedCascadeGroups);
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
    void ApplyToAlly(SkillEffect effect, UnitIdentity ally, Dictionary<object, HashSet<int>> firedCascadeGroups)
    {
        // 캐스케이드 그룹(2026-09-06) — ApplyToEnemy와 같은 자리·같은 방식. 그룹 없음(0)
        // 이면 CastSkillLevel에서 이미 시전 단위로 확률을 굴렸으니 그대로 통과(회귀 없음).
        // 그룹 있으면 대상(ally)마다 따로 굴린다 — 앞선 그룹 멤버가 이 대상에게 이미
        // 발동했으면 chance를 굴리지도 않고 건너뛴다.
        if (effect.cascadeGroup != 0)
        {
            HashSet<int> fired = GetFiredCascadeGroups(firedCascadeGroups, ally);
            if (fired.Contains(effect.cascadeGroup)) return;
            if (Random.value >= effect.chance) return;
            fired.Add(effect.cascadeGroup);
        }

        if (effect.kind != SkillEffectKind.ApplyBuff && effect.kind != SkillEffectKind.RemoveBuff
            && effect.kind != SkillEffectKind.AttackPowerBuffFlat
            && effect.kind != SkillEffectKind.AttackSpeedBuffPercent
            && effect.kind != SkillEffectKind.AttackPowerBuffPercent) return;

        UnitAttacker allyAttacker = ally != null ? ally.GetComponent<UnitAttacker>() : null;
        if (allyAttacker == null) return;

        if (effect.kind == SkillEffectKind.RemoveBuff)
        {
            // B03Z/로우(2026-09-06, PM 지시) — 거는 곳과 쓰는 곳이 같은 트리거 안이라,
            // 쓰는 쪽이 직접 떼지 않으면 다음 열림 때까지 남아 무한 누적된다. ApplyBuff의
            // 정확한 반대짝 — duration/buffHitCharges는 안 본다(그냥 지금 뗀다).
            allyAttacker.RemoveBuff(effect.buffId);
            return;
        }

        // 눈에 보이는 버프는 공격력·공속 둘뿐 — ApplyBuff는 대부분 게이트용 내부 표식(B03Z 등)이라 이펙트를 안 띄운다.
        if (effect.kind == SkillEffectKind.AttackPowerBuffFlat || effect.kind == SkillEffectKind.AttackSpeedBuffPercent
            || effect.kind == SkillEffectKind.AttackPowerBuffPercent)
            SkillVfx.Burst(SkillVfx.Kind.Buff, ally.transform.position + Vector3.up * 3f);

        if (effect.kind == SkillEffectKind.AttackPowerBuffFlat)
        {
            // 2026-09-07 추가(PM 지시) — ApplyBuff와 같은 자리, multiplier가 더할 고정
            // 공격력 값이다(buffHitCharges/duration 관례도 ApplyBuff와 동일).
            allyAttacker.AddFlatAttackPowerBuff(effect.buffId, effect.multiplier, effect.duration, effect.buffHitCharges);
            return;
        }

        if (effect.kind == SkillEffectKind.AttackPowerBuffPercent)
        {
            allyAttacker.AddAttackPowerBuffPercent(effect.buffId, effect.multiplier, effect.duration, effect.buffHitCharges);
            return;
        }

        if (effect.kind == SkillEffectKind.AttackSpeedBuffPercent)
        {
            // 2026-09-07 추가(PM 지시) — ApplyBuff와 같은 자리, multiplier가 원작 raw
            // 퍼센트(0.15=15%)다(buffHitCharges/duration 관례도 ApplyBuff와 동일).
            allyAttacker.AddAttackSpeedBuffPercent(effect.buffId, effect.multiplier, effect.duration, effect.buffHitCharges);
            return;
        }

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
        return ResolveBaseSkillEffectValue(effect, target, recentAttackDamage) * RandomDamageMultiplier(effect) * AttackSpeedScaleFactor(effect);
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
            // %체력 세 basis — 2026-09-30부터 보스 게이트가 없다(구현담당1, PM 5번). 전엔
            // EnemyData.takesPercentDamage=false(보스 36종)면 상수항까지 0이었는데, 원작은 보스를
            // 막는 전역 장치가 없고(SKILL_BOSS_BRANCH.md §5) 트리거마다 GetUnitPointValue 분기로
            // 보스에게 다른 식을 준다(173건 중 114건은 보스도 %HP를 받는다). 그 분기는 이제 효과별
            // SkillEffect.targetCondition(PV <200 / ==200 / ≥200 / ≥300)이 맡는다 — 조건 없는
            // %체력 효과는 원작처럼 보스도 맞는다.
            // 감수성 계수(PercentDamageTakenMultiplier)는 여기서 안 곱한다 — 아래
            // DealSkillDamage에서 스킬 피해 전반에 곱한다.
            case SkillEffectBasis.TargetMaxHpPercent:
                return target.MaxHp * effect.multiplier + effect.bonus;
            case SkillEffectBasis.TargetCurrentHpPercent:
                return target.Hp * effect.multiplier + effect.bonus;
            case SkillEffectBasis.TargetMissingHpPercent:
                return Mathf.Max(0f, target.MaxHp - target.Hp) * effect.multiplier + effect.bonus;
            case SkillEffectBasis.CasterAttackPower: return AttackDamage * effect.multiplier + effect.bonus;
            // 연구단계 × multiplier + bonus 꼴을 명시적으로 쓴다(원작 예: 핸콕 "연구횟수×
            // 30,000+360,000") — 이 "연구단계"는 타입 업그레이드(원작 "강화소 3",
            // 공격타입별 R00G/R00H/R00I/R01V, 최대3)다. CountResearchLevel() 주석 참고 —
            // 2026-09-06 밤 연결 완료(`UnitUpgrades.LevelForAttackType`). 등급 업그레이드
            // (최대21, 공속 축 전용)를 여기 연결하면 안 된다 — 2026-09-06 오전에 실제로
            // 그렇게 연결됐다가 최대 7배 과대로 터졌다(뿌리 ㉜ 네 번째). 지금은 구매 UI가
            // 아직 없어 레벨이 항상 0으로 남는다(0×multiplier+bonus=bonus, 회귀 없음) —
            // 구매 경로가 생기기 전까지는 이전과 값이 같다.
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
            // 원작 비비 A0LZ류 자가시전 영구 강화 레벨(0~N) x multiplier + bonus. selfUpgradeLevel은
            // TryUpgradeSelf로만 오르고(A0LZ_CASTER_STACK_INVESTIGATION.md/7703d2c) 리셋이 없다 —
            // CasterSkillLevel(능력 레벨 1/2 전용)과 다른 축이다. 기본값 0이라 배선 전엔 항상
            // bonus만 나간다(회귀 없음).
            case SkillEffectBasis.CasterSelfUpgradeLevel: return selfUpgradeLevel * effect.multiplier + effect.bonus;
            default: return 0f;
        }
    }

    // 적 하나에게 효과 하나를 적용한다 — kind별로 갈린다. ApplySkillEffect의 Enemies/
    // SingleTarget 갈래가 여길 거친다(Self/Allies는 ApplyToAlly). recentAttackDamage는
    // SkillEffectBasis.ReceivedDamage 전용(위 ResolveSkillEffectValue 참고) — Damage가
    // 아닌 kind는 그냥 무시한다.
    void ApplyToEnemy(SkillEffect effect, EnemyDummy target, float recentAttackDamage,
        Dictionary<object, HashSet<int>> firedCascadeGroups)
    {
        // 캐스케이드 그룹(2026-09-06, "캐스케이드 그룹" — SkillData.cs SkillEffect.cascadeGroup
        // 주석 참고) — 원작 if/elseif/else 사슬 대응. 그룹 없음(0, 기본값)이면 위
        // CastSkillLevel에서 이미 시전 단위로 확률을 굴렸으니 여기선 그대로 통과한다
        // (기존 동작 그대로, 회귀 없음). 그룹 있으면 여기서 대상(target)마다 새로 굴린다
        // — 앞선 그룹 멤버가 이 대상에게 이미 발동했으면 chance를 굴리지도 않고
        // 건너뛴다("정확히 하나만"을 구조적으로 강제, 범위 스킬이면 적마다 독립).
        if (effect.cascadeGroup != 0)
        {
            HashSet<int> fired = GetFiredCascadeGroups(firedCascadeGroups, target);
            if (fired.Contains(effect.cascadeGroup)) return;
            if (Random.value >= effect.chance) return;
            fired.Add(effect.cascadeGroup);
        }

        // 효과 단위 대상 버프 게이트(06번①-2, 2026-09-06) — SkillLevel의 게이트와
        // 별개다(위 SkillData.cs SkillEffect.requiredTargetBuffId 주석 참고). Enemies
        // AoE면 target이 매번 다른 개체라 이 검사도 개체마다 다시 돈다 — 그래서 "AoE
        // 중 버프 있는 놈만" 같은 원작 구조가 자연히 나온다.
        if (!string.IsNullOrEmpty(effect.requiredTargetBuffId) && (target == null || !target.HasBuff(effect.requiredTargetBuffId)))
            return;
        if (!string.IsNullOrEmpty(effect.forbiddenTargetBuffId) && target != null && target.HasBuff(effect.forbiddenTargetBuffId))
            return;

        // 대상 조건 게이트(2026-09-06, PM 지시, "대상 조건 게이트") — 위 버프 게이트와 같은
        // 자리·같은 방식(대상마다 따로 평가)으로 원작 GetUnitPointValue 3단 분기(<·==·>=)를
        // 검사한다. targetCondition==None(기본값)이면 항상 통과 — 기존 365개 에셋 전부 이
        // 필드가 없어 회귀 없다. target==null(Aura/CooldownAutoCast가 대상 없이 도는 경로)
        // 이면 위 버프 게이트와 같은 원칙으로 조건이 걸려 있는 한 항상 막는다(대상이 없는데
        // "대상이 조건을 만족한다"고 통과시키면 안전하지 않다).
        if (!PassesPointValueCondition(effect.targetCondition, effect.targetConditionValue, target)) return;

        // 평타 피해 문턱(원작 GetEventDamage() > X) — SkillEffect.triggerDamageAbove 주석 참고. 0이면 통과.
        if (effect.triggerDamageAbove > 0f && recentAttackDamage <= effect.triggerDamageAbove) return;

        switch (effect.kind)
        {
            case SkillEffectKind.Damage:
                DealSkillDamage(effect, target, recentAttackDamage);
                break;

            // 일반 행동정지 스턴만이다 — 원작의 "게이지를 미는 스턴"(신세계 사이드보스
            // 전용, 우리에 그 시스템 자체가 없다)은 안 만든다. AddFreeze/RemoveFreeze는 겹침
            // 횟수를 세므로 다른 스턴원과 동시에 걸려도 서로를 밀어내지 않는다.
            // 시한 효과(스턴·이감·방깎·ArmorBonus/HealOverTime)는 전부 걸린 적이 센다(EnemyDummy.*For, 2026-09-30) —
            // 여기서 코루틴으로 세면 이 유닛이 조합·판매로 사라질 때 영영 안 풀린다.
            case SkillEffectKind.Stun:
                if (effect.duration > 0f) target.FreezeFor(StunDurationOn(target, effect) * AttackSpeedScaleFactor(effect));
                break;

            // 이감(2026-09-29) — multiplier = 남는 속도 비율. AddSlow/RemoveSlow는 같은 값으로 짝을 맞춰야 빠진다.
            // multiplier 0 = 「최저 이속까지」(원작 Htc3·Ctc3 ≥ 1) — EnemyDummy가 원작 MinUnitSpeed 하한으로 올린다.
            case SkillEffectKind.Slow:
                if (effect.duration > 0f && effect.multiplier >= 0f && effect.multiplier < 1f)
                    target.SlowFor(effect.multiplier, StunDurationOn(target, effect));
                break;

            // 방깎 — 부호 없는 감소값(effect.multiplier 그대로가 곧 깎는 양, ArmorBonus와
            // 달리 뒤집지 않는다). 레일리(2026-09-05, 구현담당1)가 값을 채웠는데 여기가
            // 안 읽어서 "값은 있는데 아무 일도 안 난다"였다(PM 지시로 정정). duration>0이면
            // 그 시간 뒤에 되돌린다 — 0(기본)이면 SupportShop 독약과 같은 관례로 영구
            // 누적한다(원작 방깎은 대개 지속시간이 없다, war3map.w3h 버프 311개 전수 확인).
            // 원작에선 트리거가 AId1 레벨을 올리는 것이라 합계 −75에서 멈춘다(EnemyDummy.Aid1ShredCap).
            case SkillEffectKind.ArmorBreak:
                if (effect.duration > 0f) target.Aid1ArmorShredFor(effect.multiplier, effect.duration);
                else target.AddAid1ArmorShred(effect.multiplier);
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
                if (effect.duration > 0f) target.AllyAuraEffectFor(effect, effect.duration);
                else target.ApplyAllyAuraEffect(effect);
                break;

            // 버프 부여(2026-09-06, PM 지시 — "버프를 실제로 걸 때만 게이트로 쓰라") — 원작
            // 디버프(핸콕 석화가 대상 Aegr를 올리는 것 등)가 이 자리를 쓸 것이다.
            // EnemyDummy.AddBuff가 duration을 스스로 추적해 만료시키므로(위 ArmorBonus·
            // HealOverTime과 달리) 되돌리는 코루틴이 따로 필요 없다.
            case SkillEffectKind.ApplyBuff:
                target.AddBuff(effect.buffId, effect.duration);
                break;

            // ApplyBuff의 반대짝(2026-09-06, B03Z/로우) — ApplyToAlly 쪽과 같은 이유.
            // 대상(적) 버프를 즉시 뗀다 — 지금은 쓰는 자산이 없지만(캐스터 자기버프
            // B03Z만 있음) ApplyBuff가 Enemies 타겟도 있으니 대칭으로 같이 만든다.
            case SkillEffectKind.RemoveBuff:
                target.RemoveBuff(effect.buffId);
                break;

            // 대상측 3축 스택(2026-09-06, TARGET_SIDE_AXES.md) — multiplier가 올릴 레벨 수
            // 그대로다. Add* 쪽이 각자 상한(꺾임레벨 포함)을 알아서 자르므로 여기선 안 자른다.
            case SkillEffectKind.AegrStack:
                target.AddAegrStack((int)effect.multiplier);
                break;
            case SkillEffectKind.AisrStack:
                target.AddAisrStack((int)effect.multiplier);
                break;
            case SkillEffectKind.A11SStack:
                target.AddA11SStack((int)effect.multiplier);
                break;

            // 핸콕 석화 방어 감소 표(A0VJ) — 표 길이에서 절로 멈춘다.
            case SkillEffectKind.A0VJStack:
                for (int i = 0; i < (int)effect.multiplier; i++) target.AddHancockPetrificationStack();
                break;

            // ExtraProjectile은 아직 값 의미가 없다(이번 작업 범위 밖) — 조용히 무시.
        }
    }

    // 저항 피부(원작 ACrk) 적에게는 스턴·시한 이감이 영웅 지속(ahdu)으로 걸린다(EnemyData.resistantSkin · SkillEffect.heroDuration 주석).
    // heroDuration이 0(모름)이면 일반 지속 × 이 비율 — 맵의 스턴 능력 가운데 adur·ahdu가 둘 다 적힌 것의 분포가
    // 두 무리(stomp 계열 ≈0.15 · 강타/파이어볼트 계열 ≈0.5)라 덜 깎는 쪽 0.5를 쓴다(값을 모를 때 보스 스턴을 과하게 줄이지 않게).
    public const float HeroDurationFallbackRatio = 0.5f;

    static float StunDurationOn(EnemyDummy target, SkillEffect effect)
    {
        if (target == null || !target.HasResistantSkin) return effect.duration;
        return effect.heroDuration > 0f ? effect.heroDuration : effect.duration * HeroDurationFallbackRatio;
    }

    // 계측 채널 — %체력 세 basis는 「스킬%HP」로 따로 센다(보스 %HP 게이트 제거 효과를 재려고, 2026-09-30).
    static string TelemetryChannel(SkillEffect effect) =>
        effect.basis == SkillEffectBasis.TargetMaxHpPercent || effect.basis == SkillEffectBasis.TargetCurrentHpPercent
        || effect.basis == SkillEffectBasis.TargetMissingHpPercent ? "스킬%HP" : "스킬";

    void DealSkillDamage(SkillEffect effect, EnemyDummy target, float recentAttackDamage)
    {
        // ⚠️ 2026-09-05 정정: PercentDamageTakenMultiplier(원작 A11S)는 "%체력 피해 전용
        // 감수성"이 아니라 "이 대상이 스킬 피해를 얼마나 받는가" 계수다 — 원작에 게이트 없이
        // 고정 피해에도 같은 계수가 곱는 사례가 43곳 중 7곳 있다(리서치담당 재조사). 그래서
        // basis를 안 가리고 스킬 피해 전반에 곱한다. %체력 분기 자체를 타는지는 별개 축
        // (2026-09-30부터 효과별 targetCondition — 전역 TakesPercentDamage 게이트는 걷었다)이다.
        // 원작 식에 A11S 인자가 없는 효과는 감수성 계수를 안 곱한다(SkillEffect.skipDamageTakenMultiplier).
        float amount = ResolveSkillEffectValue(effect, target, recentAttackDamage) * chainDamageScale
            * (effect.skipDamageTakenMultiplier ? 1f : target.PercentDamageTakenMultiplier);
        // 원작 realD = 0.03×버프개수(SkillEffect.casterBuffCountFactor 주석 참고). 기존
        // 227개 효과는 이 필드가 직렬화에 없어 C# 기본값 0f로 읽힌다 — (1+0×count)=1이라
        // 배율이 완전히 무효, 회귀 없음. ⚠️ 2026-09-06: CountCasterBuffs()가 이제 버프
        // 레지스트리를 실제로 센다(:181 참고) — 다만 원작은 "시전자의 워크3 버프 전부"를
        // 세는데 우리는 우리 레지스트리(SupportShop 버프 + 절대쿨 자기버프)만 세는 과소
        // 근사다. 거프 4행(Garp_AttackDamage #5·#6·#7, 값×(1+0.12×버프개수))이 이 factor로
        // 실제로 걸린다.
        amount *= 1f + effect.casterBuffCountFactor * CountCasterBuffs();
        amount *= DamagePassiveFactor(target);   // 보잡(BossDamageMultiplier) × 아군발 디버프 개수 비례(DamagePerAllyDebuff)
        if (amount <= 0f) return;

        // ⚠️ 평타(DamageTypeOf/AttackTypeOf)가 아니라 이 효과 자신의 damageType/attackType을
        // 쓴다 — 평타는 항상 물리라 스킬만 마법을 낼 수 있다(SkillEffect 필드 주석 참고).
        // 캐스터의 평타 속성을 그대로 물려주면 물리 유닛의 스킬이 전부 물리로만 나가버린다.
        int hits = Mathf.Max(1, effect.hitCount);
        if (hits <= 1)
        {
            float skillHpBefore = target.Hp;
            target.TakeDamage(amount, effect.damageType, effect.attackType, owner != null ? owner.OwnerId : -1, armorIgnoreRatio: SkillArmorIgnore(effect));
            SkillTelemetry.Damage(identity != null ? identity.Data : null, TelemetryChannel(effect), target, skillHpBefore);
            return;
        }

        // SupportSkillData.waveCount/duration과 같은 관례 — duration에 걸쳐 나눠 때린다.
        StartCoroutine(SkillMultiHitRoutine(target, amount, effect.damageType, effect.attackType, hits, effect.duration, SkillVfx.CasterAllowsVfx));
    }

    // 최윤서 강화 방무딜 — 효과의 방어 무시 비율(armorIgnoreRequiresBuff가 있으면 그 버프를 가진 시전자만).
    float SkillArmorIgnore(SkillEffect effect)
    {
        if (effect.armorIgnoreRatio <= 0f) return 0f;
        if (!string.IsNullOrEmpty(effect.armorIgnoreRequiresBuff) && !HasBuff(effect.armorIgnoreRequiresBuff)) return 0f;
        return effect.armorIgnoreRatio;
    }

    IEnumerator SkillMultiHitRoutine(EnemyDummy target, float amountPerHit, DamageType damageType, AttackType attackType, int hits, float duration, bool vfxAllowed = true)
    {
        float interval = duration > 0f ? duration / hits : 0f;
        for (int i = 0; i < hits; i++)
        {
            if (target != null)
            {
                float hitHpBefore = target.Hp;
                // 여러 번 때리기는 시전 문맥 밖(코루틴)이라 시작 때 등급 판정을 싣고 온다. 첫 타는 시전 안에서 동기로 돌므로
                // 스킬 이펙트 칸을 지우지 않는 SetCasterGate로(09-30).
                bool vfxBefore = SkillVfx.SetCasterGate(vfxAllowed);
                target.TakeDamage(amountPerHit, damageType, attackType, owner != null ? owner.OwnerId : -1);
                SkillVfx.SetCasterGate(vfxBefore);
                SkillTelemetry.Damage(identity != null ? identity.Data : null, "스킬", target, hitHpBefore);   // 다단 히트는 효과 정보가 없어 채널을 안 나눈다
            }
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

    // 배수(특성강화 딜증가)·가산치(연구소 절대 가산)·공속배율(연구소 등급 공속)을 한 번에
    // 갱신한다 — 셋 다 같은 UnitUpgrades.OnLevelChanged 이벤트로만 바뀌므로 dirty 플래그를
    // 공유해도 된다. ⚠️ 2026-09-06 정정 — 예전엔 여기서 연구소 배율(source.MultiplierForGrade)
    // 을 데미지에 곱했는데, 그 필드가 실제로는 공속(gba1/gmo1) 데이터였다(구현담당2 발견,
    // PM 확인) — 데미지 쪽 곱은 걷어내고 특성강화(딜증가)만 남겼다.
    void RefreshUpgradeCacheIfDirty()
    {
        if (!upgradeMultiplierDirty) return;

        UnitUpgrades source = ResolveUpgrades();
        UnitData unitData = identity != null ? identity.Data : null;
        float damageBonusPercent = source != null && unitData != null
            ? source.EffectSum(unitData, TraitEffectKind.DamageIncrease)
            : 0f;

        cachedUpgradeMultiplier = 1f + damageBonusPercent;

        // 절대 가산치 — 전설·히든·불멸·초월·제한됨 5개 트랙만 0이 아니다. 대응 트랙이 없거나
        // 레벨 0이면 BonusForGrade가 0을 돌려준다.
        // 2026-09-25: 공격타입 강화(「강화소 3」)의 공격력 가산도 같은 연구 절대 가산이라 여기 더한다.
        cachedResearchBonus = source != null && unitData != null
            ? source.BonusForGrade(unitData.grade) + source.AttackBonusForAttackType(unitData.attackType, unitData.grade)
            : 0f;

        // 연구소 등급 공속(gba1/gmo1, 2026-09-06 신규 연결) — 유닛 종의 등급이 담당 트랙에
        // 없거나 그 트랙이 아직 레벨 0이면 SpeedMultiplierForGrade가 1을 돌려줘서 무영향이다.
        // ⚠️ 2026-09-07 추가(PM 지시, "필드만·아직 없다" 뼈대 구멍 점검) — 공격타입강화소
        // ("강화소 3")의 공속(gba1/gmo1, AttackTypeUpgradeTrackData.speedPercentPerLevel)도
        // 같은 성격의 연구소 공속이라 여기 같이 곱한다 — 서로 다른 건물(등급트랙 vs
        // 공격타입트랙)이라 독립적으로 곱해져도 안전하다(원작에도 둘 다 존재).
        cachedResearchSpeedMultiplier = source != null && unitData != null
            ? source.SpeedMultiplierForGrade(unitData.grade) * source.SpeedMultiplierForAttackType(unitData.attackType)
            : 1f;

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

    /// <summary>원작 비비 A0LZ류 자가시전 영구 강화 시도(A0LZ_CASTER_STACK_INVESTIGATION.md/
    /// 7703d2c) — 상점이 아니라 유닛 자신의 액션이라 GamblingShop에 안 얹는다(PM 지시).
    /// 자원(목재+위습)은 <b>성공/실패 무관하게 매 시도 소모</b>된다 — 원작 그대로다. 자원이
    /// 부족하면(둘 중 하나라도) 아무것도 안 깎고 그대로 실패 반환(원작 "랜덤위습의 개수나
    /// 목재가 부족합니다" 입장 게이트와 같다). 골드/자원 순서 되돌림은 GamblingShop.TryRollUnit의
    /// "먼저 뺀 것을 나중 실패에 되돌린다" 패턴을 그대로 따른다.</summary>
    public bool TryUpgradeSelf()
    {
        if (selfUpgradeData == null) return false;
        if (owner == null) return false;
        // ⚠️ 2026-09-06 정정(PM 지시): 레벨10 이상이면 시전 자체를 막던 안전장치를 뺐다 —
        // 원작은 레벨10에서도 시전이 되고 성공확률만 공식으로 0%가 될 뿐, 자원 낭비 자체를
        // 막지 않는다(그게 원작 밸런스의 일부다). "전부 원작대로" 기준상 이런 "더 친절한"
        // 가드는 창작 허용 범위 밖이다 — 막았으면 위습이 원작보다 여유로워졌을 것이다.
        // 아래 성공확률 계산(Mathf.Clamp(...,0,100))이 레벨10 이상에서 자연히 0%를 만드므로
        // 상한 로직은 그쪽에만 있으면 충분하다.

        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        if (context == null || context.ResourceWallet == null) return false;

        if (!context.ResourceWallet.TrySpend(ResourceType.Wood, selfUpgradeData.woodCost))
            return false;

        List<Wisp> candidates = null;
        if (selfUpgradeData.wispCurrency != null)
        {
            candidates = new List<Wisp>();
            foreach (Wisp w in Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None))
                if (w != null && !w.IsConsumed && w.Data == selfUpgradeData.wispCurrency)
                    candidates.Add(w);
        }

        if (candidates == null || candidates.Count < selfUpgradeData.wispCost)
        {
            // 위습 자산이 아직 안 배정됐거나(candidates==null) 맵에 부족하면 목재를 되돌리고 실패.
            context.ResourceWallet.Add(ResourceType.Wood, selfUpgradeData.woodCost);
            return false;
        }

        // 맵에서 무작위로 wispCost기를 골라 소모한다(원작 "무작위로 3기를 집어 제거").
        for (int i = 0; i < selfUpgradeData.wispCost; i++)
        {
            int pick = Random.Range(0, candidates.Count);
            candidates[pick].MarkConsumed();
            Destroy(candidates[pick].gameObject);
            candidates.RemoveAt(pick);
        }

        float successChance = Mathf.Clamp(
            selfUpgradeData.baseChancePercent - selfUpgradeLevel * selfUpgradeData.chancePerLevelPercent,
            0f, 100f);
        bool success = Random.Range(0f, 100f) < successChance;

        // 자원은 이미 소모됐다(원작 "실패해도 자원이 나간다") — 성공했을 때만 레벨을 올린다.
        if (success) selfUpgradeLevel++;
        return success;
    }

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
        combat = GetComponent<UnitCombat>();
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
        TickGaugeRegen();
        TickMoveSpeedDebuff();
        TickAttackSpeedStack();
        UpdateSkillCooldown();
        TickEnterRangeSkills();
        if (TickSelfStun()) { attackTimer = Mathf.Max(attackTimer, 0.05f); return; }   // 만성피로 — 자기 스턴 중엔 공격·스킬 정지

        // ⚠️ 2026-09-30(PM 승인) — 예전엔 `attackTimer = AttackInterval`이라 프레임이 넘긴 시간을 버렸다: 평타 주기가
        // 프레임에 매여 설정보다 길었다(1배속 0.38→0.391 +2.8%, 2배속 +5.4%). 이제 넘친 시간(≤0)을 다음 주기에서 뺀다.
        // · 한 프레임에 한 번만 쏜다 — 주기가 프레임보다 짧으면 남는 시간은 버린다(폭주 없음).
        // · 대상이 없는 동안은 0에서 멈춘다 — 음수로 쌓였다가 적이 오면 몰아 쏘지 않는다. 적이 오면 바로 쏜다
        //   (예전엔 대상이 없어도 한 주기를 통째로 기다렸다).
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;
        }

        // UnitCombat의 목표는 읽기만 하면 돼서 매 프레임 본다. 직접 훑는 탐색(UnitCombat 없는 유닛·문)은 비싸서 띄엄띄엄.
        bool canScan = true;
        if (idleScanTimer > 0f) { idleScanTimer -= Time.deltaTime; canScan = idleScanTimer <= 0f; }

        EnemyDummy target = combat != null ? combat.CurrentTarget : (canScan ? FindClosestEnemyInRange() : null);
        if (target != null)
        {
            attackTimer = Mathf.Max(0f, attackTimer + AttackInterval);
            Anim?.PlayAttack();
            ApplyArmorShred(target);
            // isAbilityDamage: false — 평타는 원작에 UNIVERSAL이 없다(EnemyDummy.TakeDamage
            // 문서 참고). 로스터 damageType이 AP인 유닛이라도 평타로 방어를 무시하면 안 된다.
            float basicHpBefore = target.Hp;
            target.TakeDamage(AttackDamage * DamagePassiveFactor(target), DamageTypeOf, AttackTypeOf, owner != null ? owner.OwnerId : -1,
                              armorIgnoreRatio: 0f, isAbilityDamage: false);
            SkillTelemetry.Damage(identity != null ? identity.Data : null, "평타", target, basicHpBefore);
            ApplyAttackSplash(target);
            ApplyAttackMultishot(target);
            ApplyCritIfTriggered(target);
            if (target.IsDead) TryRaiseOnKill(target);
            TryCastOnHitSkill(target);
            return;
        }

        attackTimer = 0f;
        if (!canScan) return;

        // 적이 없을 때만 문을 친다. 문이 우선이면 적이 몰려와도 문만 때리고 있게 된다.
        DestructibleGate gate = FindClosestGateInRange();
        if (gate != null)
        {
            attackTimer = AttackInterval;
            Anim?.PlayAttack();
            gate.TakeDamage(AttackDamage);
            return;
        }
        idleScanTimer = IdleScanInterval;
    }

    // 평타 광역(UnitData.attackSplashRadius·attackCleave* 주석) — 주 대상은 이미 맞았으니 건너뛴다(이중 타격 없음).
    // 같은 레인 적만(멀티에서 남의 레인 적을 안 때리게, PM 결정). 계측은 「평타광역」 채널로 따로.
    // 평타 다중 대상(UnitData.attackExtraTargets 주석) — 공격자에서 가까운 순으로 다른 적 N마리에게 평타와 같은 피해.
    void ApplyAttackMultishot(EnemyDummy primary)
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null || unitData.attackExtraTargets <= 0 || unitData.attackExtraTargetRadius <= 0f) return;
        float reach = unitData.attackExtraTargetRadius / WorldScale.Value;
        Vector3 center = transform.position;
        List<EnemyDummy> inRange = ListPool<EnemyDummy>.Get();
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null || enemy == primary || enemy.IsDead || enemy.LaneIndex != primary.LaneIndex) continue;
            if ((enemy.transform.position - center).sqrMagnitude <= reach * reach) inRange.Add(enemy);
        }
        if (inRange.Count > unitData.attackExtraTargets)
        {
            inRange.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
            inRange.RemoveRange(unitData.attackExtraTargets, inRange.Count - unitData.attackExtraTargets);
        }
        int ownerId = owner != null ? owner.OwnerId : -1;
        float damage = AttackDamage * SplashDamageFactor(unitData);   // 폭발증폭(SplashDamageMultiplier) — 범위 피해량만
        foreach (EnemyDummy enemy in inRange)
        {
            float hpBefore = enemy.Hp;
            enemy.TakeDamage(damage * DamagePassiveFactor(enemy), DamageTypeOf, AttackTypeOf, ownerId, armorIgnoreRatio: 0f, isAbilityDamage: false);
            SkillTelemetry.Damage(unitData, "평타다중", enemy, hpBefore);
        }
        ListPool<EnemyDummy>.Release(inRange);
    }

    void ApplyAttackSplash(EnemyDummy primary)
    {
        UnitData unitData = identity != null ? identity.Data : null;
        if (unitData == null) return;
        float splash = unitData.attackSplashRadius / WorldScale.Value;
        float cleave = unitData.attackCleaveFactor > 0f ? unitData.attackCleaveRadius / WorldScale.Value : 0f;
        float reach = Mathf.Max(splash, cleave);
        if (reach <= 0f) return;

        // 피해로 적이 죽으면 EnemyDummy.Active가 바뀐다 — 먼저 모아 두고 돈다(스킬 Enemies 갈래와 같은 이유).
        Vector3 center = primary.transform.position;
        List<EnemyDummy> inRange = ListPool<EnemyDummy>.Get();
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null || enemy == primary || enemy.IsDead || enemy.LaneIndex != primary.LaneIndex) continue;
            if (Vector3.Distance(enemy.transform.position, center) <= reach) inRange.Add(enemy);
        }
        int ownerId = owner != null ? owner.OwnerId : -1;
        float damage = AttackDamage;
        foreach (EnemyDummy enemy in inRange)
        {
            float distance = Vector3.Distance(enemy.transform.position, center);
            float hpBefore = enemy.Hp;
            if (distance <= splash)
                enemy.TakeDamage(damage * DamagePassiveFactor(enemy), DamageTypeOf, AttackTypeOf, ownerId, armorIgnoreRatio: 0f, isAbilityDamage: false);
            if (distance <= cleave)
                enemy.TakeDamage(damage * unitData.attackCleaveFactor * DamagePassiveFactor(enemy), DamageTypeOf, AttackTypeOf, ownerId, armorIgnoreRatio: 1f, isAbilityDamage: false);
            SkillTelemetry.Damage(unitData, "평타광역", enemy, hpBefore);
            SkillTelemetry.SplashHit(unitData);
        }
        ListPool<EnemyDummy>.Release(inRange);
    }

    // 대상이 없을 때 비싼 탐색(전체 적 훑기·문)을 다시 하기까지의 간격.
    const float IdleScanInterval = 0.2f;
    float idleScanTimer;

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

    static UnitSpawner raiseSpawner;

    // 원작 A113 그림자그림자 열매(모리아): 평타로 죽인 적이 좀비로 부활해 이 유닛의 주인 것이 된다. 값은 UnitData.raiseOnKill*(주석에 [추정] 근거).
    // 평타 주 대상만(원작 오브 효과는 맞은 유닛 하나) · 서버만 · 보스·PV 200 이상(원작 ancient·sapper)은 제외.
    void TryRaiseOnKill(EnemyDummy dead)
    {
        UnitData data = identity != null ? identity.Data : null;
        if (data == null || data.raiseOnKillUnit == null || owner == null) return;
        if (!GameAuthority.IsServer) return;
        if (dead.PointValue >= 200f) return;
        if (Random.Range(0f, 100f) >= data.raiseOnKillChancePercent) return;

        if (raiseSpawner == null) raiseSpawner = FindFirstObjectByType<UnitSpawner>();
        if (raiseSpawner == null) return;
        LaneMarker lane = LaneMarker.Get(owner.OwnerId);
        Vector3 position = lane != null ? lane.TakeSpawnPosition(data.raiseOnKillUnit) : transform.position;
        GameObject raised = raiseSpawner.Spawn(data.raiseOnKillUnit, position, owner.OwnerId);
        if (raised != null && data.raiseOnKillLifetimeSeconds > 0f)
            raised.AddComponent<TimedLife>().Begin(data.raiseOnKillLifetimeSeconds);
    }
}
