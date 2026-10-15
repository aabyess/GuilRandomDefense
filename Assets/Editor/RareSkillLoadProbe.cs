using UnityEngine;
using UnityEditor;

// 희귀함·안흔함 스킬 재배정(10-15) 에셋이 제대로 읽히는지 — 배치모드 -executeMethod RareSkillLoadProbe.Run
public static class RareSkillLoadProbe
{
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        string[] rosters = { "희귀함_김경현", "희귀함_고어진", "희귀함_강재규", "희귀함_구주호", "안흔함_이호준", "안흔함_문필환", "안흔함_김경현", "안흔함_김수빈", "안흔함_김민준", "안흔함_강주혁", "안흔함_강재규", "안흔함_박민수" };
        foreach (var r in rosters)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/" + r + ".asset");
            if (u == null) { sb.AppendLine(r + ": 로드 실패"); continue; }
            sb.Append($"{r}: 공{u.attackPower} 속{u.attackSpeed} 스플{u.attackSplashRadius} 멀티{u.attackExtraTargets}/{u.attackExtraTargetRadius} 마나오라{u.manaAuraRegenPerSecond}/{u.manaAuraRange} 스킬수{u.SkillCount} →");
            for (int i = 0; i < u.SkillCount; i++)
            {
                var s = u.SkillAt(i);
                if (s == null) { sb.Append(" [null]"); continue; }
                sb.Append($" [{s.name} trig{s.triggerType} lv{(s.levels != null ? s.levels.Count : 0)}");
                if (s.levels != null && s.levels.Count > 0)
                {
                    var l = s.levels[0]; sb.Append($" ch{l.triggerChance} rng{l.range}");
                    foreach (var e in l.effects) sb.Append($" ({e.kind} t{e.target} dt{e.damageType} at{e.attackType} x{e.multiplier} hit{e.hitCount} dur{e.duration} max{e.maxTargets})");
                }
                sb.Append("]");
            }
            sb.AppendLine();
        }
        Debug.Log("[RareSkillLoadProbe]\n" + sb);
        System.IO.File.WriteAllText(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/GRD_damage_probe/rare_skill_load.txt", sb.ToString());
    }
}
