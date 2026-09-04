using UnityEngine;

/// <summary>
/// 공격 타입 × 방어 타입 배율표. <b>원작 `war3mapMisc.txt`의 값을 그대로 옮긴 것이다</b>
/// (`Docs/reference/UNIT_STATS_RESEARCH.md`에 추출·검증 기록).
///
/// 세 타입이 가위바위보를 이룬다 — normal은 normal에 강하고 fort에 약하며,
/// siege는 fort에 강하고 large에 약하며, pierce는 large에 강하고 normal에 약하다.
/// hero는 전 방어에 1.05 고정, chaos는 전부 1.00으로 상성 밖이다.
///
/// <b>물리 피해에만 적용된다.</b> 마법(AP)은 이 표가 아니라 적의 마법 방어 배율을 탄다.
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

    [Header("공격 타입별 배율 (방어 Large / Fort / Normal / Hero)")]
    public Row normal = new Row();
    public Row pierce = new Row();
    public Row siege = new Row();
    public Row hero = new Row();
    public Row chaos = new Row();

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
}
