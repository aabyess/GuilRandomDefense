using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyResourceReward
{
    public ResourceType type;
    public int amount;
}

// 방어 타입. **원작 그대로다** — `war3map.w3u`에서 실제로 쓰이는 네 종류다
// (`UNIT_STATS_RESEARCH.md`). 「보스」라는 타입은 원작에 없어서 뺐다.
//
// `Unassigned`가 0인 이유: 우리 적 89종이 아직 아무도 타입을 안 정했다. 0을 `Normal`로 두면
// 분류하지 않은 적이 전부 「normal 갑옷」이 되어, 우리 유닛(전부 물리)과 만나 배율 1.25가 붙는다 —
// **아무것도 안 했는데 피해가 25% 오른다.** `Unassigned`는 배율 1.0이라 분류 전까지 동작이 그대로다.
// 이건 원작에 없는 우리 것이다(원작은 명시 안 하면 베이스 유닛 값을 상속한다).
public enum ArmorType
{
    Unassigned,
    Normal,
    Large,
    Fort,
    Hero,
}

// 배율표에서 **이 피해가 어느 행을 타는가**. 원작 `war3mapMisc.txt`의 일곱 행 그대로다.
//
// 앞의 다섯은 평타의 물리 공격 타입이고, 뒤의 둘은 마법이다 — 원작은 마법도 두 행으로 가른다:
//   `Magic`  = 평타가 마법인 유닛           (large 1.00 / fort 1.00 / normal 1.00 / hero 0.80)
//   `Spells` = 능력·스킬이 주는 피해        (large 0.85 / fort 0.90 / normal 1.00 / hero 0.80)
//
// **DamageType(물리냐 마법이냐)과는 여전히 직교한다.** DamageType은 방어력을 타느냐
// 마법방어 배율을 타느냐를 정하고, 여기는 그 위에 곱할 상성 행을 고른다.
//
// ⚠️ 둘은 **짝이 맞아야 한다** — AP 피해에 물리 행을 넘기면 *마법이 물리 상성을 타던 옛 버그*가
// 그대로 돌아온다. `DamageTable.RowMatches`가 그 짝을 검사한다.
//
// ⚠️ enum은 **맨 뒤에만** 추가한다. 중간에 끼우면 직렬화된 값이 통째로 밀린다.
public enum AttackType
{
    Unassigned,
    Normal,
    Pierce,
    Siege,
    Hero,
    Chaos,
    Magic,
    Spells,
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "GuilRandomDefense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public float hp;
    public float moveSpeed;
    public int goldReward;
    public List<EnemyResourceReward> resourceRewards;

    // true면 처치자 1명이 아니라 전체 플레이어에게 각자 보상 지급 (예: 물범 → 목재 1개씩).
    public bool rewardsAllPlayers;

    public bool isBoss;

    // 물리(AD) 피해만 감폭한다. 마법(AP)은 방어력을 무시한다 — 원작 서술 그대로.
    // 음수도 유효하다: 방깎이 0 아래로 밀어넣으면 피해가 오히려 늘어난다.
    public float armor;

    // 마법(AP) 피해에 곱하는 배율. **1.0이 "감소 없음"**이고, 물리 방어력과는 완전히 별개 축이다.
    //
    // 원작은 워크3 내장 능력 `Aegr`(맵 이름 그대로 "마법 방어력")를 적에게 걸고,
    // 45레벨 중 몇 레벨을 거느냐를 **난이도가 정한다**(쉬움~어려움 L16=1.00 / 지옥·신 L11=0.95 /
    // 악몽 L6=0.90). 레인마다 따로 건다. 우리는 레벨 표를 옮기지 않고 **배율 하나만** 둔다 —
    // 난이도를 만들 때 이 값을 난이도별로 주면 원작과 같은 구조가 된다.
    //
    // 1.0을 넘으면 마법 피해를 **더** 받는다(원작 L20=1.04). 그래서 상한을 두지 않는다.
    public float magicArmorMultiplier = 1f;

    // isBoss에서 유도하지 않고 따로 둔다. `돌아온 김만경`·`간보는김용태`처럼
    // **보스가 아닌데 보스에서 파생된 적**이 있고, 그들이 보스 몸을 쓰는지 아직 모른다.
    public ArmorType armorType = ArmorType.Unassigned;

    public GameObject prefab;
}
