using UnityEngine;

/// <summary>
/// 공격 타입 × 방어 타입 배율표.
///
/// <b>여섯 칸이 전부 1.0으로 시작한다. 일부러 그렇다.</b> 원작의 배율은 어디에도 문서화돼 있지 않고
/// (`Docs/reference/ARMOR_SYSTEM_DESIGN.md`), 지금 숫자를 지어 넣으면 그게 곧 밸런스가 되어
/// 나중에 원작 수치가 나와도 되돌리기 어렵다. <b>축만 만들어두고 값은 근거가 생길 때 채운다.</b>
///
/// 밸런싱할 때 코드를 고치지 않아도 되도록 ScriptableObject로 뺐다.
///
/// <b>⚠️ 값을 1.0에서 바꾸기 전에 먼저 적 프리팹의 <c>EnemyDummy.damageTable</c>에 이 에셋을 연결해야 한다.</b>
/// 연결 안 된 상태에서는 <c>EnemyDummy</c>가 배율을 1.0으로 두므로 <b>표를 고쳐도 아무 일도 안 일어난다.</b>
/// 지금은 여섯 칸이 전부 1.0이라 연결 여부가 결과에 영향을 주지 않는다 — 값을 바꾸는 순간부터 문제가 된다.
/// </summary>
[CreateAssetMenu(fileName = "DamageTable", menuName = "GuilRandomDefense/Damage Table")]
public class DamageTable : ScriptableObject
{
    [System.Serializable]
    public class Row
    {
        public float vsNormal = 1f;
        public float vsBoss = 1f;
    }

    [Header("공격 타입별 배율 (방어 타입 Normal / Boss)")]
    public Row ad = new Row();
    public Row ap = new Row();

    [Tooltip("AD와 AP를 겸하는 유닛. 원작에 실제로 있는 범주지만 '어떻게' 겸하는지는 미기재 — " +
             "확정되면 여기가 아니라 피해원마다 타입을 붙이는 구조로 갈 수도 있다.")]
    public Row adap = new Row();

    public float Multiplier(DamageType attack, ArmorType armor)
    {
        Row row = attack == (DamageType.AD | DamageType.AP) ? adap
                : attack == DamageType.AP ? ap
                : ad;   // None도 물리로 취급한다 — 값이 안 붙은 피해가 조용히 사라지는 것보다 낫다

        return armor == ArmorType.Boss ? row.vsBoss : row.vsNormal;
    }
}
