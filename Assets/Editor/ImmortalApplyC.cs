using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 불멸 「유닛회유」(김용태·정준영) — 사장님 10-06: 평타 1회당 원작 0.8%로 일반 적 1기를 내 유닛으로 바꿔 같이 싸운다(동시 5기), 팔면 37% 랜덤위습·그중 40% +100엔·목재 1.
/// 호출: call ImmortalApplyC.Apply (다시 불러도 안전). 회유 유닛 UnitData(Summon_회유_적) 만들기 · 김용태 「유닛회유」 · 정준영 「유닛회유-덕담 한마디」 스킬 붙이기.
/// 회유 유닛 공격력 = 시전자 평타 × 10%(제안값), 사거리 160·공속 1.0(제안값), 이동 0(소환수 슈가 장난감과 같은 틀). 수명 없음(팔 때까지).
/// </summary>
static class ImmortalApplyC
{
    const string RecruitPath = "Assets/Data/Units/Summons/Summon_회유_적.asset";
    const string TemplatePath = "Assets/Data/Units/Summons/Summon_슈가_망가진장난감.asset";

    static string Apply()
    {
        var template = AssetDatabase.LoadAssetAtPath<UnitData>(TemplatePath);
        var wisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");
        var yongtae = ImmortalKit.Unit("불멸_김용태");
        var junyeong = ImmortalKit.Unit("불멸_정준영");
        if (template == null || wisp == null || yongtae == null || junyeong == null) return "❌ 에셋 없음";

        var recruit = AssetDatabase.LoadAssetAtPath<UnitData>(RecruitPath);
        if (recruit == null)
        {
            recruit = ScriptableObject.CreateInstance<UnitData>();
            AssetDatabase.CreateAsset(recruit, RecruitPath);
        }
        EditorUtility.CopySerialized(template, recruit);
        recruit.name = "Summon_회유_적";
        recruit.unitName = "회유당한 적";
        recruit.skill = null; recruit.skills = new List<SkillData>(); recruit.trait = null;
        recruit.attackPower = 1000f;   // 실제 값은 시전자 평타 × 10%로 소환 때 덮는다
        recruit.attackRange = 160f; recruit.attackSpeed = 1f; recruit.moveSpeed = 0f;
        recruit.isSystemUnit = true;
        recruit.sellRewardWisp = wisp; recruit.sellRewardWispChance = 0.37f; recruit.sellRewardWispCount = 1;   // 판매 칸 표시용 — 실제 판매는 RewardDistributor.SellRecruit(37%·40% +100엔·목재 1)
        recruit.sellRewardWood = 0;
        EditorUtility.SetDirty(recruit);

        SkillEffect effect() => new SkillEffect { kind = SkillEffectKind.RecruitEnemy, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { recruit }, multiplier = 0.10f, bonus = 5f };
        SkillData sk = ImmortalKit.OnHit("김용태", "유닛회유", "유닛회유", "사장님 10-06 「유닛회유(팔기가능)」(확정: 원작 확률 0.8%). 평타 1회당 0.8% 확률로 사거리 700 안 가장 가까운 일반 적(보스·PV≥200·B06B 제외) 1기를 죽이지 않고 내 유닛으로 바꾼다(보상 없음, 동시 5기 상한, 수명 없음). 회유 유닛은 공격력이 시전자 평타의 10%이고 판매 버튼으로 팔 수 있다: 37% 랜덤위습 1기, 그중 40%는 +100엔·목재 1. 사거리·공격력·수는 제안값.", 0.008f, 700f, effect());
        SkillData sj = ImmortalKit.OnHit("정준영", "유닛회유", "유닛회유-덕담 한마디", "사장님 10-06 「유닛회유」(이름은 설계표가 지음). 평타 1회당 0.8% 확률로 사거리 700 안 가장 가까운 일반 적 1기를 내 유닛으로 바꾼다(동시 5기·팔기: 김용태 유닛회유와 같다).", 0.008f, 700f, effect());

        AddOnce(yongtae, sk); AddOnce(junyeong, sj);
        AssetDatabase.SaveAssets();
        return $"회유 유닛 {RecruitPath} · 김용태 스킬 {yongtae.skills.Count} · 정준영 스킬 {junyeong.skills.Count}";
    }

    static void AddOnce(UnitData unit, SkillData skill)
    {
        if (!unit.skills.Contains(skill)) unit.skills.Add(skill);
        EditorUtility.SetDirty(unit);
    }
}
