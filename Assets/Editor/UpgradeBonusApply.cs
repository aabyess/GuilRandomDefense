using UnityEditor;
using UnityEngine;

/// <summary>
/// 유닛강화소 공격력 가산(원작 gba2/gmo2) 누락 3트랙 채우기(10-06 「희귀함 업글이 안 된다」 조사). 원작 war3map.w3q 디코드(Tools/w3x, 직접):
///   R00E 특별함 업그레이드: gba2 20 · gmo2 30 · R008 희귀함 업그레이드: gba2 200 · gmo2 200 · R019 랜덤전용 유닛 업그레이드: gba2 1000 · gmo2 1000
/// 옛 주석 「특별함·희귀함·랜덤전용은 0」은 틀렸다(gef2 = ratx 공격력 보너스가 셋 다 있다). 호출: call UpgradeBonusApply.Apply (다시 불러도 안전).
/// </summary>
static class UpgradeBonusApply
{
    static string Apply()
    {
        string[] names = { "특별함 강화", "희귀함 강화", "랜덤유닛 강화" };
        float[] base2 = { 20f, 200f, 1000f };
        float[] inc2 = { 30f, 200f, 1000f };
        string r = "";
        for (int i = 0; i < names.Length; i++)
        {
            var t = AssetDatabase.LoadAssetAtPath<UnitUpgradeTrackData>($"Assets/Data/UnitUpgrades/UnitUpgrade_{names[i]}.asset");
            if (t == null) return $"❌ {names[i]} 없음";
            r += $"{names[i]}: 공격력 가산 {t.statLevel1Bonus}+{t.statBonusGrowthPerLevel}/렙 → {base2[i]}+{inc2[i]}/렙 · ";
            t.statLevel1Bonus = base2[i];
            t.statBonusGrowthPerLevel = inc2[i];
            EditorUtility.SetDirty(t);
        }
        AssetDatabase.SaveAssets();
        return r;
    }
}
