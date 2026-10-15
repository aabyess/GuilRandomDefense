using System.Text;
using UnityEditor;
using UnityEngine;

// 자동 생성 스킬 설명의 피해 유형 표기 점검(구현담당2, 10-15) — call DamageLabelProbe.Run
static class DamageLabelProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string n in new[] { "SkillData_사장님_초월_최상호_AD_그분의후계자", "SkillData_사장님_초월_최상호_AD_그분의후계자_스킬2", "SkillData_사장님_초월_노태현_AP_시너지폭발", "SkillData_사장님_초월_노태현_AP_반사회적인격", "SkillData_사장님_초월_노태현_AP_돌발행동" })
        {
            var s = AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/Data/UnitSkills/{n}.asset");
            if (s != null) sb.Append($"\n{n.Replace("SkillData_사장님_", "")} → {PlayerFacingText.Summary(s)}");
        }
        return sb.ToString();
    }
}
