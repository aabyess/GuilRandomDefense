using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyResourceReward
{
    public ResourceType type;
    public int amount;
}

// 방어 타입. **일반/보스 둘로 시작한다** — 원작의 방어 타입은 어디에도 문서화돼 있지 않고
// (`Docs/reference/ARMOR_SYSTEM_DESIGN.md` §0), 우리 데이터가 실제로 가진 구분축은 isBoss뿐이다.
// 워크3 표준 7종을 베끼면 배율표가 21칸이 되는데 그중 20칸은 채울 근거가 없다.
// 늘릴 때는 **맨 뒤에** 붙일 것 — 중간에 끼우면 직렬화된 값이 밀린다.
public enum ArmorType
{
    Normal,
    Boss,
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

    // isBoss에서 유도하지 않고 따로 둔다. `돌아온 김만경`·`간보는김용태`처럼
    // **보스가 아닌데 보스에서 파생된 적**이 있고, 그들이 보스 몸을 쓰는지 아직 모른다.
    public ArmorType armorType = ArmorType.Normal;

    public GameObject prefab;
}
