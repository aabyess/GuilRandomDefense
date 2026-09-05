using System.Collections.Generic;
using UnityEngine;

// 유닛 능력(스킬) 시스템의 뼈대. 원작 조사(UPGRADE_SHOP.md 5차 ②, UNIT_SKILL_TRIGGERS.md
// 피해 715건 전수) 근거로 모양만 맞췄다 — 사장님이 "유닛별 배정은 나중에 내가 준다"고
// 하셔서 에셋은 만들지 않았고, 수치도 지어내지 않았다.
//
// ⚠️ 직렬화되는 이 파일의 enum들은 새 값을 반드시 맨 뒤에만 추가할 것 — 중간에 끼우면
// 이미 저장된 에셋의 값이 밀린다(TEAM_RULES.md 참고).

// 발동 방식. 원작에 셋 다 있다 — 평타 적중 시 확률 발동(Bash류), 쿨다운마다 자동 시전,
// 쿨다운 없이 계속 켜져 있는 오라.
public enum SkillTriggerType
{
    OnHitChance,
    CooldownAutoCast,
    Aura,
}

// 효과가 누구에게 가는지. 한 스킬(레벨)이 "적에게 피해 + 아군 회복"을 동시에 할 수 있어서
// (원작 예: 보스 A153 반경450 적 피해 + A11T 반경955 아군 회복) SkillLevel이 아니라
// SkillEffect마다 따로 갖는다.
public enum SkillTargetKind
{
    Self,
    Allies,
    Enemies,
    SingleTarget,
}

// 피해·효과 값이 무엇에 비례하는가. 원작 715건 전수 조사(UNIT_SKILL_TRIGGERS.md) 기준
// 여섯 갈래 중 넷만 담는다 — 연구단계(ResearchLevel)는 연구소가 만들어지는 중이라 포함하고,
// 영웅스탯 비례는 뺐다(우리 게임에 영웅 스탯 개념 자체가 없고 생길 계획도 없다).
public enum SkillEffectBasis
{
    Flat,                    // multiplier가 고정값 그 자체
    TargetMaxHpPercent,      // 대상 최대체력 × multiplier
    TargetCurrentHpPercent,  // 대상 현재체력 × multiplier
    CasterAttackPower,       // 시전자 평타 공격력 × multiplier + bonus (원작 예: atk×2.5+32500)
    ResearchLevel,           // 연구소 단계 × multiplier + bonus. 연구소(05번, 구현담당1)가
                             // 서면 그 값을 여기 잇는다 — 지금은 자리만이다.
}

// 무엇을 하는 효과인가. Damage 말고는 값의 의미도, 읽는 코드도 아직 없다 — 이름만 세워둔
// 자리다. ⚠️ 값이 있으면 사람은 "쓰이는구나"로 읽는다(오늘 buffAttackSpeedMultiplier가
// 그렇게 오해를 샀다) — Stun/ArmorBreak/ExtraProjectile은 값을 채워도 아무 코드도
// 안 읽는다는 걸 여기 명시한다. 사장님이 실제 유닛 능력을 배정할 때 값의 의미를 정하고,
// 그때 읽는 코드를 짠다.
public enum SkillEffectKind
{
    Damage,
    Stun,
    ArmorBreak,
    ExtraProjectile,
}

// 효과 하나. 레벨 하나가 이걸 여러 개 가질 수 있다 — "레벨2에 효과가 하나 더 생긴다"(원작
// 중력장: Lv2에 25% 확률 운석낙하 추가)를 "레벨마다 독립된 리스트"로 자연스럽게 담기 위함이다.
[System.Serializable]
public class SkillEffect
{
    public SkillEffectKind kind = SkillEffectKind.Damage;
    public SkillEffectBasis basis = SkillEffectBasis.Flat;
    public SkillTargetKind target = SkillTargetKind.Enemies;

    // Flat이면 고정값 그 자체. 비례 basis면 배율(원작 "atk×2.5+32500"의 2.5).
    public float multiplier;
    // 비례식의 +상수항(위 예의 32500). Flat이거나 상수항이 없으면 0.
    public float bonus;

    // 이 효과 자체의 발동확률(0~1). 기본 1 = 항상 발동. 원작 예: 중력장 Lv2 운석낙하 0.25.
    [Range(0f, 1f)] public float chance = 1f;

    // 다단히트 — SupportSkillData.waveCount/duration과 같은 관례다: hitCount<=1이면 즉발
    // 1회(duration은 그때 지속시간 용도, 예: 스턴). hitCount>1이면 duration에 걸쳐 나눠
    // 때린다(간격 = duration/hitCount) — 원작 예: 보스 A153의 "22만 데미지 × 3".
    public int hitCount = 1;
    public float duration;
}

// 스킬 레벨 하나. 특성강화(UnitTraitData)가 이 레벨을 올린다 — 원작이 `atp1` 표시 이름에
// `.lv2`/`.Lv3`를 붙여 레벨을 구분하는 것과 같은 구조(UPGRADE_SHOP.md 5차 ②).
[System.Serializable]
public class SkillLevel
{
    // CooldownAutoCast 전용. 다른 발동방식이면 안 쓴다.
    public float cooldown;
    // OnHitChance 전용 발동확률(0~1). 다른 발동방식이면 안 쓴다.
    [Range(0f, 1f)] public float triggerChance = 1f;
    // 시전·오라 반경.
    public float range;

    public List<SkillEffect> effects = new List<SkillEffect>();
}

[CreateAssetMenu(fileName = "NewSkillData", menuName = "GuilRandomDefense/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    public SkillTriggerType triggerType = SkillTriggerType.CooldownAutoCast;

    // [0] = 레벨1, [1] = 레벨2 ... 지금은 어디서도 레벨을 올리는 코드가 없어 전부 레벨1(index 0)만
    // 쓴다 — 특성 배선(06번)이 UnitUpgrades에서 실제 레벨을 읽어오면 그 자리를 바꾼다.
    public List<SkillLevel> levels = new List<SkillLevel>();
}
