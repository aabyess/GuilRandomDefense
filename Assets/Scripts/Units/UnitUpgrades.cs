using System;
using System.Collections.Generic;

// 플레이어별 강화 상태. 특성강화(유닛 1종당 {효과, 수치} 리스트, 특성포인트 소모)로 쓴다 —
// GamblingProgress와 같은 결로 상태만 들고, 비용 확인·소비는 상점이 한다.
//
// 획득처는 사장님 확정(2026-09-05, 07번 갱신): 게임 시작 1개 + 15,000엔 구매 1개 +
// 스토리 12 클리어 1개 + 피카 퀘스트 성공 1개, 한 판에 최대 4개(이전엔 3개 + "8라운드"를
// 스토리 8로 잘못 해석한 상태였다 — PirateQuestManager.HandleSuccess·RewardDistributor 주석
// 참고). 넷 다 플레이어별로 딱 한 번씩만 — GamblingProgress처럼 임의 개수의 옵션을
// HashSet으로 추적하는 대신, 출처가 정확히 4개로 고정돼 있어 이름 붙은 bool 네 개로 더 명확하게
// 표현했다. 각 출처의 실제 호출은: RewardDistributor(시작·스토리 클리어), GamblingShop(구매),
// PirateQuestManager(피카).
//
// ⚠️ 11번(영속 저장, 2026-09-05)으로 다섯 번째 이후 출처가 추가됐다 — PersistentSave.
// ApplyLoadThresholdRewards가 누적 세이브 포인트 300/600/900을 넘을 때마다 AddTraitPoints(1)을
// 직접 부른다(원작 Trig_SaveReward_1 그대로). 이건 "플레이어별 한 번"이 아니라 "불러올 때마다,
// 그 시점 누적치가 문턱을 넘으면" 주는 반복 가능한 개시 보너스라 위 bool 네 개와는 다른 축이다
// — 그래서 이름 붙은 플래그로 안 만들고 AddTraitPoints를 직접 썼다.
public class UnitUpgrades : UnityEngine.MonoBehaviour
{
    // ---- 특성강화(신규) ----

    public int TraitPoints { get; private set; }
    public event Action OnTraitPointsChanged;

    public void AddTraitPoints(int amount)
    {
        if (amount <= 0) return;
        TraitPoints += amount;
        OnTraitPointsChanged?.Invoke();
    }

    /// <summary>특성강화 구매 진입점(06번, GameHud "특성강화" 버튼)이 부른다. 원작 순서 그대로
    /// "확인 다 하고 나서 차감" — 모자라면 false를 돌려주고 아무것도 안 바뀐다. 이 메서드
    /// 자체가 확인+차감을 한 호출로 묶어서, 호출부가 "확인하고 나중에 따로 차감"하다 그 사이
    /// 다른 소비가 끼어드는 경우를 원천적으로 막는다.</summary>
    public bool TrySpendTraitPoints(int amount)
    {
        if (amount <= 0 || TraitPoints < amount) return false;
        TraitPoints -= amount;
        OnTraitPointsChanged?.Invoke();
        return true;
    }

    bool startingPointGranted;
    bool purchasedPointGranted;
    bool storyPointGranted;
    bool pirateQuestPointGranted;

    public bool HasStartingPoint => startingPointGranted;
    public bool HasPurchasedPoint => purchasedPointGranted;
    public bool HasStoryPoint => storyPointGranted;
    public bool HasPirateQuestPoint => pirateQuestPointGranted;

    // 게임 시작 시 1개. RewardDistributor.GrantStartingTraitPoints가 부른다.
    public void GrantStartingPoint()
    {
        if (startingPointGranted) return;
        startingPointGranted = true;
        AddTraitPoints(1);
    }

    // 15,000엔으로 1개(1회 한정). 골드 차감까지 여기서 같이 한다 — TryUnlock류와 같은 원자적 형태
    // (차감과 지급 사이에 실패가 끼어들 여지를 없앤다). GamblingShop이 부른다.
    public bool TryPurchasePoint(GoldWallet wallet, int cost)
    {
        if (purchasedPointGranted || wallet == null) return false;
        if (!wallet.TrySpend(cost)) return false;

        purchasedPointGranted = true;
        AddTraitPoints(1);
        return true;
    }

    // 스토리 12 클리어 1개(사장님 확정 2026-09-05 — 이전엔 "8라운드"를 스토리 8로 잘못 해석해
    // order==8이었다). RewardDistributor.GrantStoryReward가 부른다.
    public void GrantStoryPoint()
    {
        if (storyPointGranted) return;
        storyPointGranted = true;
        AddTraitPoints(1);
    }

    // 피카 퀘스트 성공 1개(사장님 확정 2026-09-05, 07번 — "피카 퀘스트도 준다"). 한 플레이어당
    // 피카 토큰이 하나뿐이라 사실상 1회 한정이지만, 다른 셋과 같은 방어선을 둔다.
    // PirateQuestManager.HandleSuccess가 quest.successTraitPoints > 0일 때 부른다 — 지금은
    // 피카 하나뿐이라 "받는다/안 받는다"만 의미 있고, 필드 값 자체(항상 1)는 안 쓴다.
    public void GrantPirateQuestPoint()
    {
        if (pirateQuestPointGranted) return;
        pirateQuestPointGranted = true;
        AddTraitPoints(1);
    }

    readonly HashSet<UnitTraitData> unlockedTraits = new HashSet<UnitTraitData>();

    public bool IsUnlocked(UnitTraitData trait) => trait != null && unlockedTraits.Contains(trait);

    // 06번⑤(반복구매형, 아카이누 전용) — unlockedTraits(HashSet, 1회잠금)와 별개 저장소다.
    // 아카이누 하나 때문에 HashSet의 의미(다른 25개의 "재구매 불가")를 바꾸지 않기 위해서다
    // (PM 지시, 2026-09-07). isRepeatablePurchase==false인 트레잇은 이 딕셔너리에 아예
    // 안 들어온다 — 기존 25개는 완전히 무영향(회귀 없음).
    readonly Dictionary<UnitTraitData, int> repeatablePurchaseCounts = new Dictionary<UnitTraitData, int>();

    public int RepeatablePurchaseCount(UnitTraitData trait) =>
        trait != null && repeatablePurchaseCounts.TryGetValue(trait, out int count) ? count : 0;

    public void IncrementRepeatablePurchase(UnitTraitData trait)
    {
        if (trait == null) return;
        repeatablePurchaseCounts.TryGetValue(trait, out int count);
        repeatablePurchaseCounts[trait] = count + 1;
    }

    // 흔함 9종 판매 누적 카운터(A09G, UNIT_SELL_AND_JOHAB_RESEARCH.md 원문 `Sell_Point1`)
    // — 유닛타입별이 아니라 **플레이어 전체 공유**다(9명 중 누굴 팔든 같은 카운터에 +1,
    // 리서치 확인 2026-09-07). 게임 시작 0 세팅 말고 리셋이 없다 — 라운드 전환·사망과
    // 무관하게 계속 누적, N번째마다 0으로 돌아가고 그 뒤로도 반복된다(1회성 아님).
    // ⚠️ 직렬화 안 함(unlockedTraits·repeatablePurchaseCounts와 같은 결, 한 판 한정).
    int commonSellCount;

    /// <summary>흔함 유닛 판매 1회 등록. everyN(=UnitData.sellRewardEveryNSells, 상수를
    /// 코드에 안 박고 자산에서 읽는다)에 도달하면 카운터를 0으로 리셋하고 true를 돌려준다
    /// (그 순간 위습을 지급하라는 신호 — 실제 지급은 호출부 몫). everyN<=0이면 카운터 자체를
    /// 안 건드리고 항상 false(안전한 무동작).</summary>
    public bool RegisterCommonSell(int everyN)
    {
        if (everyN <= 0) return false;

        commonSellCount++;
        if (commonSellCount < everyN) return false;

        commonSellCount = 0;
        return true;
    }

    // 포인트 차감·비용 확인은 상점(아직 없음) 몫이다 — 여기선 언락 상태만 바꾼다.
    public void Unlock(UnitTraitData trait)
    {
        if (trait == null || !unlockedTraits.Add(trait)) return;
        OnLevelChanged?.Invoke();
    }

    // UnitAttacker가 자기 유닛(UnitData)에 해당하는 특성 효과 합을 조회할 때 쓴다. 한 유닛에
    // 트레잇 에셋이 여러 개 걸릴 일은 없게 설계했지만(유닛 1종 = 에셋 1개), 혹시 겹쳐도 전부 더한다.
    public float EffectSum(UnitData unit, TraitEffectKind kind)
    {
        if (unit == null) return 0f;

        float sum = 0f;
        foreach (UnitTraitData trait in unlockedTraits)
        {
            if (trait == null || trait.targetUnit != unit || trait.effects == null) continue;

            foreach (TraitEffect effect in trait.effects)
                if (effect.kind == kind) sum += effect.value;
        }

        return sum;
    }

    /// <summary>이 유닛의 스킬이 몇 레벨(SkillData.levels의 인덱스)인지 — 06번① 스킬승급형
    /// 트레잇 전용. 언락된 트레잇 중 targetUnit이 이 유닛이고 skillLevelUnlockIndex>0인
    /// 것을 찾아 그 인덱스를 돌려준다("유닛 1종 = 트레잇 1개" 설계라 여러 개가 걸릴 일은
    /// 없다). 아직 안 샀거나 스킬승급형이 아니면 0(레벨1) — UnitAttacker.CurrentSkillLevel이
    /// 이 값으로 SkillLevel을 고른다.</summary>
    public int SkillLevelIndexFor(UnitData unit)
    {
        if (unit == null) return 0;

        foreach (UnitTraitData trait in unlockedTraits)
        {
            if (trait == null || trait.targetUnit != unit || trait.skillLevelUnlockIndex <= 0) continue;

            // 06번⑤(반복구매형, 아카이누) — 1회차 구매는 skillLevelUnlockIndex 그대로,
            // 그 뒤로는 구매할 때마다 인덱스가 하나씩 더 간다(원작: 능력 레벨 = 구매 횟수).
            // CurrentSkillLevel이 범위를 넘는 인덱스를 이미 안전하게 clamp하므로 여기서
            // 상한을 따로 안 잰다.
            int extra = trait.isRepeatablePurchase ? Math.Max(0, RepeatablePurchaseCount(trait) - 1) : 0;
            return trait.skillLevelUnlockIndex + extra;
        }

        return 0;
    }

    /// <summary>능력교체형 트레잇(06번①, UnitTraitData.replacementSkill) 전용. 언락된
    /// 트레잇 중 targetUnit이 이 유닛이고 replacementSkill이 있는 걸 찾아 돌려준다 —
    /// 없으면 null(UnitAttacker.Skill이 그때 원래 UnitData.skill을 쓴다).</summary>
    public SkillData ReplacementSkillFor(UnitData unit)
    {
        if (unit == null) return null;

        foreach (UnitTraitData trait in unlockedTraits)
        {
            if (trait != null && trait.targetUnit == unit && trait.replacementSkill != null)
                return trait.replacementSkill;
        }

        return null;
    }

    public event Action OnLevelChanged;

    // ---- 등급강화(연구소) ----
    //
    // ⚠️ 2026-09-05 정정: 이 필드 이름의 "legacy"는 틀린 전제였다. 만들 당시엔 "원작에
    // '등급 전체 강화' 시스템 자체가 없다"고 알려져 있어서 UI가 정리되면 통째로 지울
    // 자리로 취급했는데, 리서치담당이 `.w3q`(연구소 원본)를 직접 파싱해내면서 원작에
    // 정확히 이 모양의 연구소가 있었다는 게 뒤집혔다(사장님 결정 05번). **지울 대상이
    // 아니라 원작 연구소 그 자체다.** 필드 이름은 굳이 안 바꾼다 — 직렬화된 값이 안전하게
    // 유지되고, 이름과 실제 역할이 어긋난다는 건 이 주석으로 충분히 남는다.
    readonly Dictionary<UnitUpgradeTrackData, int> legacyGradeLevels = new Dictionary<UnitUpgradeTrackData, int>();

    public int Level(UnitUpgradeTrackData track) =>
        track != null && legacyGradeLevels.TryGetValue(track, out int level) ? level : 0;

    public void LevelUp(UnitUpgradeTrackData track)
    {
        if (track == null) return;
        legacyGradeLevels[track] = Level(track) + 1;
        OnLevelChanged?.Invoke();
    }

    // ⚠️ 2026-09-06 정정(구현담당2 발견, PM 확인) — 예전 이름 MultiplierForGrade는 이
    // 값을 "공격력 배율"로 UnitAttacker가 데미지에 곱하고 있었는데, 실제로는 원작 공속
    // 증가율(gba1/gmo1) 데이터였다(6개 트랙 전부 리서치담당 공속 표와 정확히 일치).
    // 진짜 공격력 가산치는 BonusForGrade(gba2/gmo2)가 이미 맞게 들고 있다 — 이 메서드는
    // 이제 UnitAttacker.AttackSpeedMultiplier가 부른다. 레벨 0(한 번도 안 산 트랙)은
    // 이 사전에 키로 없어도 상관없다 — 못 찾으면 기본값 1을 돌려주는데,
    // `SpeedMultiplierForLevel(0)`도 항상 1(무영향)이라 결과가 같다. 영원함 트랙은
    // hasOriginalResearch=false라 LevelUp이 절대 안 불려 legacyGradeLevels에 안 들어온다
    // — 별도 제외 코드 없이 자동으로 무영향(1)이다(PM 지시, 영원함은 배선하지 않는다).
    public float SpeedMultiplierForGrade(UnitGrade grade)
    {
        foreach (KeyValuePair<UnitUpgradeTrackData, int> entry in legacyGradeLevels)
        {
            if (entry.Key != null && entry.Key.targetGrades != null && entry.Key.targetGrades.Contains(grade))
                return entry.Key.SpeedMultiplierForLevel(entry.Value);
        }
        return 1f;
    }

    // MultiplierForGrade와 같은 방식, 가산치(gba2/gmo2)용. 배수와 곱해지는 게 아니라
    // UnitAttacker.AttackDamage에서 그 위에 그대로 더해진다 — UnitUpgradeTrackData.BonusForLevel
    // 참고. 대응하는 트랙이 없거나 아직 레벨 0이면 0(가산 없음)을 돌려준다.
    public float BonusForGrade(UnitGrade grade)
    {
        foreach (KeyValuePair<UnitUpgradeTrackData, int> entry in legacyGradeLevels)
        {
            if (entry.Key != null && entry.Key.targetGrades != null && entry.Key.targetGrades.Contains(grade))
                return entry.Key.BonusForLevel(entry.Value);
        }
        return 0f;
    }

    // 이 등급을 담당하는 트랙(등급 업그레이드, R000~R004 계열, 최대 21)의 원시 레벨을
    // 그대로 돌려준다. 공속 축(SpeedMultiplierForGrade/BonusForGrade) 전용이다.
    // ⚠️ 2026-09-06 정정(PM 지시 — 뿌리 ㉜ 네 번째) — 한때 UnitAttacker.CountResearchLevel
    // (SkillEffectBasis.ResearchLevel)이 이 메서드를 그대로 불렀는데, 그건 틀렸다.
    // ResearchLevel은 원작 **타입 업그레이드**(R01L/R01M/R01O/R01T/R01V, 최대 3)를
    // 뜻하는데 여기는 **등급 업그레이드**(최대 21)라 최대 7배 과대가 났다(핸콕 h05C
    // "연구횟수×30,000+360,000" 등 9효과가 실제로 이 버그였다). 그 연결은 끊었다 —
    // CountResearchLevel은 이제 이 메서드를 안 부르고 항상 0을 돌려준다. 이 메서드
    // 자체는 지우지 않는다(공속 축이 여전히 쓴다) — 여기서는 등급 트랙이 맞다.
    public int LevelForGrade(UnitGrade grade)
    {
        foreach (KeyValuePair<UnitUpgradeTrackData, int> entry in legacyGradeLevels)
        {
            if (entry.Key != null && entry.Key.targetGrades != null && entry.Key.targetGrades.Contains(grade))
                return entry.Value;
        }
        return 0;
    }

    // ---- 공격타입 업그레이드("강화소 3", 2026-09-06) ----
    //
    // 원작 "강화소 3"(위 등급트랙 "강화소 1"·이름없는 "강화소"와 다른 세 번째 건물,
    // AttackTypeUpgradeTrackData.cs 주석·ATTACKTYPE_UPGRADE_RAW_DUMP.md 참고). 등급
    // 트랙과 완전히 같은 패턴이라 별도 Dictionary로 둔다 — 같은 저장소를 공유하면
    // "등급이야 공격타입이야"를 나중에 또 헷갈린다(오늘 이미 그 사고가 한 번 났다).
    readonly Dictionary<AttackTypeUpgradeTrackData, int> attackTypeLevels = new Dictionary<AttackTypeUpgradeTrackData, int>();

    public int Level(AttackTypeUpgradeTrackData track) =>
        track != null && attackTypeLevels.TryGetValue(track, out int level) ? level : 0;

    public void LevelUp(AttackTypeUpgradeTrackData track)
    {
        if (track == null) return;
        attackTypeLevels[track] = Level(track) + 1;
        OnLevelChanged?.Invoke();
    }

    // `UnitAttacker.CountResearchLevel()`(SkillEffectBasis.ResearchLevel) 전용 —
    // 이 유닛 자신의 attackType이 담당하는 공격타입 트랙의 원시 레벨(0~3)을 그대로
    // 돌려준다. 원작 스킬 하나하나가 어느 자식ID(R01M/R01L/R01O/R01T 등)를 읽었는지는
    // 안 갈랐다 — 우리는 유닛을 원작 이름 1:1로 매핑하지 않고 재배정하므로, "그 스킬이
    // 원래 참조하던 ID"가 아니라 "지금 이 유닛에 배정된 공격타입"을 기준으로 읽는 게
    // 등급트랙(LevelForGrade)과 같은 원칙이다(등급트랙도 "원작 그 캐릭터의 등급"이
    // 아니라 "지금 이 유닛에 배정된 등급"을 쓴다). 대응 트랙이 없거나(레벨 0) 아직
    // 안 샀으면 0 — 원작 "연구를 사야 켜지는" 설계와 같다.
    public int LevelForAttackType(AttackType attackType)
    {
        foreach (KeyValuePair<AttackTypeUpgradeTrackData, int> entry in attackTypeLevels)
        {
            if (entry.Key != null && entry.Key.attackType == attackType)
                return entry.Value;
        }
        return 0;
    }
}
