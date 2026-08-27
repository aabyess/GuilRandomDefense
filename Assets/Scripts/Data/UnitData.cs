using UnityEngine;

public enum UnitGrade
{
    Common,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "NewUnitData", menuName = "GuilRandomDefense/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public UnitGrade grade;
    public float hp;
    public float attackPower;
    public float attackRange;
    public float attackSpeed;
    public float moveSpeed;
    public SkillData skill;
    public GameObject prefab;
}
