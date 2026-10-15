using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 유닛 전용 연구·공격타입 가산 버킷 렙 21 실측(구현담당1, 10-16)
// gameshot: call:ShopSlotProbe.Fund call:OwnResearchProbe.Run
static class OwnResearchProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sp = Object.FindFirstObjectByType<UnitSpawner>();
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        var tracks = AssetDatabase.FindAssets("t:UnitUpgradeTrackData", new[] { "Assets/Data/UnitUpgrades" }).Select(g => AssetDatabase.LoadAssetAtPath<UnitUpgradeTrackData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        var types = AssetDatabase.FindAssets("t:AttackTypeUpgradeTrackData", new[] { "Assets/Data/AttackTypeUpgrades" }).Select(g => AssetDatabase.LoadAssetAtPath<AttackTypeUpgradeTrackData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        var sb = new StringBuilder();
        foreach (string n in new[] { "전설적인_정준영", "전설적인_임채현", "제한_최영민", "초월_이태훈_AP", "다른세계_올마이트", "랜덤_손오공", "희귀함_김경현" })
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            var go = sp.Spawn(d, c, 0);
            var att = go.GetComponent<UnitAttacker>();
            var own = go.GetComponent<OwnedByPlayer>();
            var ctx = PlayerContext.Get(own.OwnerId);
            var up = ctx.UnitUpgrades;
            UnitGrade tg = UnitUpgrades.TrackGradeOf(d);
            var gt = tracks.First(t => t.targetUnit == null && t.targetGrades != null && t.targetGrades.Contains(tg));
            var tt = types.FirstOrDefault(t => t.attackType == d.attackType);
            foreach (string fn in new[] { "legacyGradeLevels", "attackTypeLevels" })
                ((System.Collections.IDictionary)typeof(UnitUpgrades).GetField(fn, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(up)).Clear();
            var ev = (System.Action)typeof(UnitUpgrades).GetField("OnLevelChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).GetValue(up);
            ev?.Invoke();
            float dmg0 = att.AttackDamage, int0 = att.AttackInterval;
            for (int i = 0; i < 21; i++) up.LevelUp(gt);
            float dmgG = att.AttackDamage, intG = att.AttackInterval;
            if (tt != null) for (int i = 0; i < 3; i++) up.LevelUp(tt);
            float dmg1 = att.AttackDamage, int1 = att.AttackInterval;
            var tr = d.ownGradeResearch != null ? d.ownGradeResearch : gt;
            float expG = tr.BonusForLevel(21);
            float expType = d.attackTypeBonusPerLevel > 0 ? d.attackTypeBonusPerLevel * 3 : (tt != null ? tt.AttackBonusForLevel(d.grade, 3) : 0f);
            float expSpG = tr.SpeedMultiplierForLevel(21), expSpT = tt != null ? tt.SpeedMultiplierForLevel(3) : 1f;
            sb.AppendLine($"[{n}] 연구 {(d.ownGradeResearch != null ? d.ownGradeResearch.trackName : gt.trackName)} · 타입 {(tt != null ? tt.trackName : "없음")}(버킷 {d.attackTypeBonusPerLevel})");
            sb.AppendLine($"   등급 렙21: 가산 실측 +{dmgG - dmg0:F0} 기대 +{expG:F0} · 공속배 실측 {int0 / intG:F3} 기대 {expSpG:F3}");
            sb.AppendLine($"   +타입 렙3: 가산 실측 +{dmg1 - dmgG:F0} 기대 +{expType:F0} · 공속배 실측 {intG / int1:F3} 기대 {expSpT:F3} · 총 {dmg0:F0}→{dmg1:F0} 간격 {int0:F3}→{int1:F3}");
            Object.Destroy(go);
        }
        return sb.ToString();
    }
}
