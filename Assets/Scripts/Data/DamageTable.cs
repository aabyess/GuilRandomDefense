using UnityEngine;

/// <summary>
/// 공격 타입 × 방어 타입 배율표. <b>원작 `war3mapMisc.txt`의 값을 그대로 옮긴 것이다</b>
/// (`Docs/reference/UNIT_STATS_RESEARCH.md`에 추출·검증 기록).
///
/// 세 타입이 가위바위보를 이룬다 — normal은 normal에 강하고 fort에 약하며,
/// siege는 fort에 강하고 large에 약하며, pierce는 large에 강하고 normal에 약하다.
/// hero는 전 방어에 1.05 고정, chaos는 전부 1.00으로 상성 밖이다.
///
/// ⚠️ 2026-09-05 정정(ORIGINAL_DAMAGE_TYPING.csv 715건 전수, PM): 공격타입(이 표의 행)과
/// 피해타입(방어 무시 여부, <see cref="DamageType"/>)은 <b>독립된 축</b>이다 —
/// `CHAOS+UNIVERSAL` 96건·`MAGIC+UNIVERSAL` 62건처럼 물리 상성 행이 방어 무시 피해와
/// 자유롭게 짝짓는다. 예전엔 "AP(=마법)는 magic/spells 행만" 강제했는데 그 전제가 틀렸다 —
/// <see cref="RowMatches"/>는 이제 항상 통과시킨다. 어느 피해타입이든 이 표는 always 건다.
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
    ///
    /// ⚠️ 2026-09-05부로 <see cref="RowMatches"/>가 이 값을 더 쓰지 않는다(공격타입·피해타입
    /// 독립 축 확정) — 지우지 않고 남겨둔다. 상성 행 종류를 물어야 할 다른 자리가 생기면
    /// 그때 다시 쓸 것.
    /// </summary>
    public static bool IsMagicRow(AttackType attack) =>
        attack == AttackType.Magic || attack == AttackType.Spells;

    /// <summary>
    /// 피해 종류와 행 종류의 짝이 맞는가.
    ///
    /// ⚠️ 2026-09-05 정정(PM, ORIGINAL_DAMAGE_TYPING.csv 715건 전수): 예전엔 "AP(=마법)는
    /// magic/spells 행만, 그 외는 물리 행만" 강제했는데 — 공격타입(행)과 피해타입(방어 무시)이
    /// 독립 축이라는 게 확인되면서 그 전제가 깨졌다(예: `CHAOS+UNIVERSAL` 96건, 물리 상성
    /// 행 + 방어 무시 조합이 원작에 실재). 그래서 지금은 <b>항상 통과</b>시킨다 — 표는
    /// 피해타입과 무관하게 always 건다.
    ///
    /// 메서드·경고 로그 배선(<c>EnemyDummy.MitigatedDamage</c>의 <c>loggedRowMismatch</c>)은
    /// 지우지 않고 남겨둔다 — 나중에 짝이 안 맞는 조합을 다시 막아야 할 일이 생기면 여기만
    /// 고치면 된다.
    /// </summary>
    public static bool RowMatches(DamageType damage, AttackType attack) => true;
}
