using UnityEngine;

/// <summary>
/// 공격 타입 × 방어 타입 배율표. <b>원작 `war3mapMisc.txt`의 값을 그대로 옮긴 것이다</b>
/// (`Docs/reference/UNIT_STATS_RESEARCH.md`에 추출·검증 기록).
///
/// 세 타입이 가위바위보를 이룬다 — normal은 normal에 강하고 fort에 약하며,
/// siege는 fort에 강하고 large에 약하며, pierce는 large에 강하고 normal에 약하다.
/// hero는 전 방어에 1.05 고정, chaos는 전부 1.00으로 상성 밖이다.
///
/// <b>마법도 이 표를 탄다.</b> 원작은 마법용 행을 둘 따로 두는데(`magic`·`spells`),
/// 마법 방어 배율(워크3 `Aegr`)과 **둘 다** 적용하는 것이 원작 동작이다.
/// 다만 마법에 <b>물리 행</b>을 먹이면 안 된다 — <see cref="RowMatches"/>가 그 짝을 지킨다.
///
/// <b>⚠️ 값을 바꾸기 전에 적 프리팹의 <c>EnemyDummy.damageTable</c>에 이 에셋을 연결해야 한다.</b>
/// 연결 안 된 상태에서는 <c>EnemyDummy</c>가 배율을 1.0으로 두므로 표를 고쳐도 아무 일도 안 일어난다.
/// </summary>
[CreateAssetMenu(fileName = "DamageTable", menuName = "GuilRandomDefense/Damage Table")]
public class DamageTable : ScriptableObject
{
    [System.Serializable]
    public class Row
    {
        public float vsLarge = 1f;
        public float vsFort = 1f;
        public float vsNormal = 1f;
        public float vsHero = 1f;
    }

    [Header("물리 — 공격 타입별 배율 (방어 Large / Fort / Normal / Hero)")]
    public Row normal = new Row();
    public Row pierce = new Row();
    public Row siege = new Row();
    public Row hero = new Row();
    public Row chaos = new Row();

    [Header("마법 — 평타가 마법인 유닛(magic) / 능력이 주는 피해(spells)")]
    public Row magic = new Row();
    public Row spells = new Row();

    /// <summary>
    /// 한쪽이라도 <c>Unassigned</c>면 1.0이다. 분류하지 않은 유닛·적이 조용히
    /// 어느 칸에 떨어져 밸런스가 바뀌는 것을 막는다 — 분류는 명시적으로 해야 한다.
    /// </summary>
    public float Multiplier(AttackType attack, ArmorType armor)
    {
        if (attack == AttackType.Unassigned || armor == ArmorType.Unassigned) return 1f;

        Row row;
        switch (attack)
        {
            case AttackType.Pierce: row = pierce; break;
            case AttackType.Siege:  row = siege;  break;
            case AttackType.Hero:   row = hero;   break;
            case AttackType.Chaos:  row = chaos;  break;
            case AttackType.Magic:  row = magic;  break;
            case AttackType.Spells: row = spells; break;
            default:                row = normal; break;
        }

        switch (armor)
        {
            case ArmorType.Large: return row.vsLarge;
            case ArmorType.Fort:  return row.vsFort;
            case ArmorType.Hero:  return row.vsHero;
            default:              return row.vsNormal;
        }
    }

    /// <summary>
    /// 마법 행인가. <c>Unassigned</c>는 어느 쪽도 아니다.
    /// </summary>
    public static bool IsMagicRow(AttackType attack) =>
        attack == AttackType.Magic || attack == AttackType.Spells;

    /// <summary>
    /// 피해 종류와 행 종류의 짝이 맞는가. <b>이 검사가 옛 버그를 막는다</b> —
    /// 마법(AP) 피해에 물리 행(normal/pierce/siege…)을 먹이면 마법이 마법 방어 배율과
    /// 물리 상성을 둘 다 맞아 이중으로 불리해진다. 예전에 실제로 그랬다.
    ///
    /// <c>Unassigned</c>는 항상 통과시킨다 — 어차피 <see cref="Multiplier"/>가 1.0을 돌려준다.
    /// </summary>
    public static bool RowMatches(DamageType damage, AttackType attack)
    {
        if (attack == AttackType.Unassigned) return true;
        // AD+AP는 지금 AD와 동일 취급이라(ARMOR_SYSTEM_DESIGN §7) 물리 행을 기대한다.
        return damage == DamageType.AP ? IsMagicRow(attack) : !IsMagicRow(attack);
    }
}
