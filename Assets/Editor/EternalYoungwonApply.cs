using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 김영원(영원_김영원, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-1).
/// 호출: call EternalYoungwonApply.Apply (다시 불러도 안전). 스킬 1(원작 핸콕 이식 스킬 5개는 뺀다):
///  · 아주위험한게임 = 토토(Style : 김영원) — 평타 10% 확률로 베팅: 한 번 굴려 **50% 이김 → 대상 최대체력 12%(방어 무시, 보스·스토리 포함) + 스턴 1.5초 / 50% 짐 → 자기 스턴 1초**(실패 대가, 사장님 권장 확정). 값은 제안값.
/// 새 효과 SkillEffectKind.GambleRoll + SkillEffect.gambleGate(1 이김만·2 짐만). 마나 0(마나 스킬 없음) · 칭호 없음(김영원) · 입력말 「교수의은사」 · trait 비움. 재료(제한 김민규 · 히든 김영학)는 이미 원문과 맞다.
/// </summary>
static class EternalYoungwonApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_김영원.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_김영원.asset";
    const string Phrase = "교수의은사";

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 영원_김영원 유닛·조합식 에셋 없음";

        string path = $"{SkillFolder}/SkillData_사장님_영원_김영원_아주위험한게임.asset";
        SkillData game = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (game == null)
        {
            game = ScriptableObject.CreateInstance<SkillData>();
            AssetDatabase.CreateAsset(game, path);
        }
        game.skillName = "아주위험한게임 — 토토(평타 10% 베팅)";
        game.description = "사장님 10-06 「토토(Style : 김영원)」 + 「아주위험한게임」, 사장님 권장 확정: 토토 = 도박(베팅). 평타 10% 확률로 베팅 한 번 — 50% 이김: 대상 최대 체력 12%(방어 무시, 보스·스토리도 비례) + 스턴 1.5초 / 50% 짐: 자기 스턴 1초(위험 대가). 값(12%·1.5초·1초·10%)은 제안값 — 스킬이 하나뿐이라 딜 책임이 커서 실측 뒤 올릴 수 있다.";
        game.triggerType = SkillTriggerType.OnHitChance;
        game.levels = new List<SkillLevel>
        {
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 0.10f, range = 0f, hitCountThreshold = 0, resetTo = 0, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.GambleRoll, target = SkillTargetKind.Self, multiplier = 0.5f },
                    new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMaxHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.12f, armorIgnoreRatio = 1f, gambleGate = 1 },
                    new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1.5f, gambleGate = 1 },
                    new SkillEffect { kind = SkillEffectKind.SelfStunRefillLifeGauge, target = SkillTargetKind.Self, duration = 1f, multiplier = 0f, gambleGate = 2 },
                },
            }
        };
        EditorUtility.SetDirty(game);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { game };
        unit.trait = null;
        unit.manaMax = 0f;
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"김영원 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종 그대로{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
