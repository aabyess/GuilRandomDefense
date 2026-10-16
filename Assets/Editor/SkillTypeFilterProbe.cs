using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 조합 검색 서랍 스킬 유형 필터 점검(구현담당2, 10-15)
//  call SkillTypeFilterProbe.Counts  — 유형별 로스터 유닛 수 / 조합식 결과 유닛 수(+ 참고 분류) → ~/grd/GRD_skilltype_counts.txt
//  call SkillTypeFilterProbe.Sample  — 유형마다 무작위 3기(이름·태그·자동 설명) 확인용 표
//  플레이 중: call SkillTypeFilterProbe.Open / Chips:이름,이름 (서랍 열고 칩 켜기, gameshot용)
static class SkillTypeFilterProbe
{
    static List<UnitData> AllUnits() =>
        AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).Where(u => u != null).ToList();

    static string Counts()
    {
        var all = AllUnits();
        var results = new HashSet<UnitData>();
        foreach (string g in AssetDatabase.FindAssets("t:CombineRecipe", new[] { "Assets/Data/Recipes" }))
        {
            var r = AssetDatabase.LoadAssetAtPath<CombineRecipe>(AssetDatabase.GUIDToAssetPath(g));
            if (r != null && r.result != null) results.Add(r.result);
        }
        var sb = new StringBuilder($"로스터 {all.Count}기 · 조합식 결과 유닛 {results.Count}기\n{"태그",-18}{"로스터",8}{"결과유닛",8}\n");
        foreach (SkillTypeTag t in System.Enum.GetValues(typeof(SkillTypeTag)))
        {
            if (t == SkillTypeTag.None) continue;
            int a = all.Count(u => (SkillTypeTags.Of(u) & t) != 0), b = results.Count(u => (SkillTypeTags.Of(u) & t) != 0);
            sb.Append($"{t,-18}{a,8}{b,8}\n");
        }
        // 등급별 분포(칩 겹침 확인)
        sb.Append("\n등급별 로스터 수 / 태그 하나라도 가진 수\n");
        foreach (var gr in all.GroupBy(u => u.grade))
            sb.Append($"{gr.Key,-16}{gr.Count(),5}{gr.Count(u => SkillTypeTags.Of(u) != SkillTypeTag.None),6}\n");
        File.WriteAllText(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd/GRD_skilltype_counts.txt"), sb.ToString());
        return "\n" + sb;
    }

    static string Sample()
    {
        var all = AllUnits().Where(u => u.skills != null && u.skills.Count > 0).ToList();
        var rng = new System.Random(1015);
        var sb = new StringBuilder();
        foreach (SkillTypeTag t in System.Enum.GetValues(typeof(SkillTypeTag)))
        {
            if (t == SkillTypeTag.None) continue;
            var pick = all.Where(u => (SkillTypeTags.Of(u) & t) != 0).OrderBy(_ => rng.Next()).Take(3).ToList();
            sb.Append($"\n[{t}] ({all.Count(u => (SkillTypeTags.Of(u) & t) != 0)}기)");
            foreach (var u in pick)
            {
                sb.Append($"\n  {u.name} · 태그 {SkillTypeTags.Of(u)}");
                foreach (var s in u.skills.Take(8)) if (s != null) sb.Append($"\n     - {s.name.Replace("SkillData_", "")}: {PlayerFacingText.Summary(s)}");
            }
        }
        File.WriteAllText(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd/GRD_skilltype_samples.txt"), sb.ToString());
        return $"표본을 ~/grd/GRD_skilltype_samples.txt에 썼다({sb.Length}자)";
    }

    // 특성 반영 전후 비교 + 특성 자산 현황: call SkillTypeFilterProbe.TraitDelta
    static string TraitDelta()
    {
        var all = AllUnits();
        var results = new HashSet<UnitData>();
        foreach (string g in AssetDatabase.FindAssets("t:CombineRecipe", new[] { "Assets/Data/Recipes" }))
        {
            var r = AssetDatabase.LoadAssetAtPath<CombineRecipe>(AssetDatabase.GUIDToAssetPath(g));
            if (r != null && r.result != null) results.Add(r.result);
        }
        var sb = new StringBuilder();
        int withTrait = all.Count(u => u.trait != null);
        var traitAssets = AssetDatabase.FindAssets("t:UnitTraitData").Select(g => AssetDatabase.LoadAssetAtPath<UnitTraitData>(AssetDatabase.GUIDToAssetPath(g))).Where(t => t != null).ToList();
        int unlinked = traitAssets.Count(t => t.targetUnit != null && t.targetUnit.trait != t);
        sb.Append($"특성 자산 {traitAssets.Count}개 · 유닛에 연결된 trait {withTrait}기 · targetUnit과 UnitData.trait이 다른 자산 {unlinked}개\n");
        var kinds = traitAssets.SelectMany(t => t.effects ?? new List<TraitEffect>()).GroupBy(e => e.kind).Select(g => $"{g.Key} {g.Count()}");
        sb.Append("특성 효과 종류: " + string.Join(", ", kinds) + $" · replacementSkill {traitAssets.Count(t => t.replacementSkill != null)}개\n");
        sb.Append($"{"칩",-18}{"스킬만(로스터)",12}{"특성 포함(로스터)",14}{"결과유닛 포함",12}\n");
        foreach (SkillTypeTag t in new[] { SkillTypeTag.ArmorBreak, SkillTypeTag.MagicArmorBreak, SkillTypeTag.Slow, SkillTypeTag.Stun, SkillTypeTag.Physical, SkillTypeTag.Magic, SkillTypeTag.Explosive, SkillTypeTag.Fixed, SkillTypeTag.ArmorIgnore, SkillTypeTag.Finisher, SkillTypeTag.MagicAmp, SkillTypeTag.ExplosiveAmp })
        {
            int withTr = all.Count(u => (SkillTypeTags.Of(u) & t) != 0);
            // 스킬만 = 특성 기여 없이 스킬 목록(+평타 방무뎀)만으로 계산
            int skillOnly = all.Count(u => ((((u.skills != null && u.skills.Count > 0) ? u.skills : new List<SkillData> { u.skill }).Any(sk => sk != null && (SkillTypeTags.Of(sk) & t) != 0))) || (t == SkillTypeTag.ArmorIgnore && u.attackArmorIgnoreRatio > 0f));
            int resWith = results.Count(u => (SkillTypeTags.Of(u) & t) != 0);
            sb.Append($"{t,-18}{skillOnly,12}{withTr,14}{resWith,12}\n");
        }
        File.WriteAllText(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd/GRD_skilltype_traitdelta.txt"), sb.ToString());
        return "\n" + sb;
    }

    static string Open()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        if (d == null) return "❌ 서랍 없음";
        typeof(RecipeSearchDrawer).GetMethod("SetOpen", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, new object[] { true });
        return "열림";
    }

    // 칩 전체를 끄고 주어진 번호(0~11)만 켠다. 번호 목록은 inbox 한 줄에 못 넣으니 고정 시나리오로: 1=물리+끝딜, 2=방깎+이속, 3=폭발형
    static string ChipsScenario(int n)
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        if (d == null) return "❌ 서랍 없음";
        var f = typeof(RecipeSearchDrawer).GetField("typeMask", BindingFlags.NonPublic | BindingFlags.Instance);
        var on = typeof(RecipeSearchDrawer).GetMethod("OnTypeChip", BindingFlags.NonPublic | BindingFlags.Instance);
        f.SetValue(d, SkillTypeTag.None);
        int[] sel = n == 1 ? new[] { 0, 5 } : n == 2 ? new[] { 6, 10 } : n == 3 ? new[] { 2 } : new int[0];
        foreach (int i in sel) on.Invoke(d, new object[] { i });
        return $"시나리오 {n}: 칩 [{string.Join(",", sel)}]";
    }
    static string Chips1() => ChipsScenario(1);
    static string Chips2() => ChipsScenario(2);
    static string Chips3() => ChipsScenario(3);
}
