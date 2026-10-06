using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 윤현모 「비지니스끝판왕」(영원_윤현모, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-7).
/// 호출: call EternalYoonApply.Apply (다시 불러도 안전). 원문에 효과 사양이 없다(스킬 이름 4개만) → 원작 에이스 이식 스킬을 빼고 **이름만 단 효과 없는 스킬 4개**(역겨움의극치·이득주의자·이용가치·통수).
/// 재료 7종(상붕카 ×2 포함)은 이미 원문과 일치 — 입력말 「교활하고비겁한바보병신」(채팅 전용)·칭호 「비지니스끝판왕」만 채운다.
/// </summary>
static class EternalYoonApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_윤현모.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_윤현모.asset";
    const string Title = "비지니스끝판왕";
    const string Phrase = "교활하고비겁한바보병신";

    static SkillData NameOnly(string suffix, string skillName, string meaning)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_윤현모_{suffix}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            AssetDatabase.CreateAsset(skill, path);
        }
        skill.skillName = skillName;
        skill.description = $"사장님 10-06 스킬 이름만 있고 효과 사양이 없다(「{meaning}」) — 사장님 10-06 확정: 이름만 달고 평타 유닛으로 둔다. 효과가 생기면 이 스킬에 채운다.";
        skill.triggerType = SkillTriggerType.Aura;
        skill.levels = new List<SkillLevel> { new SkillLevel { cooldown = 0f, triggerChance = 1f, effects = new List<SkillEffect>() } };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 영원_윤현모 유닛·조합식 에셋 없음";

        var skills = new List<SkillData>
        {
            NameOnly("역겨움의극치", "역겨움의극치", "이름만"),
            NameOnly("이득주의자", "이득주의자", "이름만"),
            NameOnly("이용가치", "이용가치", "이름만"),
            NameOnly("통수", "통수", "이름만"),
        };
        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = skills;
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 0f;   // 마나 스킬이 없다 — 게이지 안 쓴다(원작 에이스 마나 185 이식분 제거)
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"윤현모 비지니스끝판왕 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개(이름만) · 칭호 「{unit.unitName}」 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종 그대로{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
