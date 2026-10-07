using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 조합 도우미 꼬리표 전수 표(설계표 §7-2) — call SkillTagsTable.Write → Docs/design/RECIPE_HELPER_TAGS_TABLE.md
static class SkillTagsTable
{
    static string Write()
    {
        SkillTags.Clear();
        var roster = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }).Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).Where(u => u != null).OrderBy(u => u.grade.Tier()).ThenBy(u => u.name).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("# 조합 도우미 능력 꼬리표 전수 표 (자동 생성 — SkillTags.Derive)");
        sb.AppendLine();
        sb.AppendLine($"로스터 {roster.Count}기. 꼬리표는 스킬 에셋·유닛 필드에서 규칙으로 유도한 것이다(손으로 붙인 게 아니다). 필터 칩은 「≥1기」인 꼬리표만 뜬다.");
        sb.AppendLine();
        sb.AppendLine("## 꼬리표별 유닛 수");
        sb.AppendLine("| 꼬리표 | 유닛 수 |");
        sb.AppendLine("|---|---|");
        foreach (var entry in SkillTags.All)
            sb.AppendLine($"| {entry.label} | {roster.Count(u => (SkillTags.Of(u) & entry.tag) != 0)} |");
        sb.AppendLine();
        sb.AppendLine("꼬리표가 하나도 없는 유닛: " + roster.Count(u => SkillTags.Of(u) == SkillTag.None) + "기");
        sb.AppendLine();
        sb.AppendLine("## 유닛별");
        sb.AppendLine("| 등급 | 유닛 | 꼬리표 |");
        sb.AppendLine("|---|---|---|");
        foreach (UnitData u in roster)
        {
            SkillTag t = SkillTags.Of(u);
            if (t == SkillTag.None) continue;
            sb.AppendLine($"| {u.grade.KoreanName()} | {u.name} | {string.Join(" · ", SkillTags.Labels(t))} |");
        }
        File.WriteAllText("Docs/design/RECIPE_HELPER_TAGS_TABLE.md", sb.ToString());
        var counts = SkillTags.All.Select(e => $"{e.label} {roster.Count(u => (SkillTags.Of(u) & e.tag) != 0)}");
        return "표 작성 · " + string.Join(" · ", counts);
    }
}
