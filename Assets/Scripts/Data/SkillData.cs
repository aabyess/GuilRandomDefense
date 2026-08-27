using UnityEngine;

public enum SkillEffectType
{
    Damage,
    Slow,
    Buff
}

[CreateAssetMenu(fileName = "NewSkillData", menuName = "GuilRandomDefense/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public float cooldown;
    public float range;
    public SkillEffectType effectType;
    public float value;
}
