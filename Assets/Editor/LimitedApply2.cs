using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 제한됨 마무리(사장님 10-06 답, 설계표 Docs/design/LIMITED_RECIPES_DESIGN_2026-10-06.md 「사장님 확정」). 호출: call LimitedApply2.Apply (다시 불러도 안전).
///  · 분실된지갑: 새 아이템(ItemData_L006_분실된지갑, 효과 없음 — 재료로만) + 박성호 식에 아이템 재료 추가 + 일반 적 처치 0.5% 드랍(RewardDistributor.lostWalletItem을 씬에 이음).
///  · 전법규 「분신4%」: 분신 1기 상시(Summon_전법규_분신, CooldownAutoCast 5초 재소환) + 분신의 마나 스킬 = 같은 끝딜을 4%(잃은 체력, AP 방어 무시).
///  · 김민규 쿨스킬 「보물위치공개」: 액티브(쿨 60초 제안) — 반경 1000(원작 단위) 안 숨은 보물상자 자리에 땅 빛기둥 6초 + 미니맵 노란 점(TreasureHunt.RevealWithin, 씬 TreasureHunt.revealBeamMaterial을 이음).
///  · 김강민: 사장님 스킬 효과 미정 — 이름 4개만(컨셉은곧나자신·안심은금물·확고한믿음의공포·합의금).
/// </summary>
static class LimitedApply2
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string ItemPath = "Assets/Data/Items/ItemData_L006_분실된지갑.asset";
    const string BeamMatPath = "Assets/Materials/Map/reveal_beam.mat";
    const string SummonTemplate = "Assets/Data/Units/Summons/Summon_시노부_분신.asset";
    const string ClonePath = "Assets/Data/Units/Summons/Summon_전법규_분신.asset";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static UnitData U(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static SkillData MakeSkill(string unit, string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_제한_{unit}_{suffix}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            AssetDatabase.CreateAsset(skill, path);
        }
        skill.skillName = skillName;
        skill.description = description;
        skill.triggerType = trigger;
        skill.levels = new List<SkillLevel>
        {
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var log = new List<string>();

        // ── 분실된지갑 ──
        var wallet = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        if (wallet == null) { wallet = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(wallet, ItemPath); }
        wallet.itemName = "분실된지갑";
        wallet.hasGrade = false;
        wallet.tooltipText = "누군가 잃어버린 지갑 — 박성호 「역대급한숨」 조합 재료. 일반 적을 처치하다 낮은 확률로 줍는다.";
        wallet.designNote = "사장님 10-06: 일반 적 처치 시 낮은 확률로 떨어지는 재료 아이템(노획물처럼), 재료로만 쓴다. 드랍은 RewardDistributor.lostWalletItem(0.5%).";
        wallet.effects = new List<ItemEffect>();
        wallet.useKind = ItemUseKind.None;
        wallet.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Items/I006_돈도박초급.png");   // 기존 아이콘 모음에서 동전 그림
        EditorUtility.SetDirty(wallet);

        var parkRecipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/제한_박성호.asset");
        if (!parkRecipe.ingredients.Exists(i => i != null && i.kind == IngredientKind.SpecificItem && i.item == wallet))
            parkRecipe.ingredients.Add(new RecipeIngredient { kind = IngredientKind.SpecificItem, item = wallet, count = 1 });
        EditorUtility.SetDirty(parkRecipe);
        log.Add($"박성호 식 재료 {parkRecipe.ingredients.Count}종(분실된지갑 포함)");

        // ── 김강민 ──
        UnitData kang = U("제한_김강민");
        string note = "사장님 10-06 스킬 이름만 있고 효과 사양이 없다(미정) — 이름만 단다. 효과가 정해지면 이 스킬에 채운다.";
        kang.skill = null;
        kang.skills = new List<SkillData>
        {
            MakeSkill("김강민", "컨셉은곧나자신", "컨셉은곧나자신", note, SkillTriggerType.Aura, 0f, 1f, 0),
            MakeSkill("김강민", "안심은금물", "안심은금물", note, SkillTriggerType.Aura, 0f, 1f, 0),
            MakeSkill("김강민", "확고한믿음의공포", "확고한믿음의공포", note, SkillTriggerType.Aura, 0f, 1f, 0),
            MakeSkill("김강민", "합의금", "합의금", note, SkillTriggerType.Aura, 0f, 1f, 0),
        };
        EditorUtility.SetDirty(kang);
        log.Add("김강민 스킬 이름 4개");

        // ── 전법규 분신 ──
        UnitData beopgyu = U("제한_전법규");
        UnitData clone = AssetDatabase.LoadAssetAtPath<UnitData>(ClonePath);
        if (clone == null && AssetDatabase.CopyAsset(SummonTemplate, ClonePath)) clone = AssetDatabase.LoadAssetAtPath<UnitData>(ClonePath);
        if (clone == null) return "❌ 분신 소환 유닛 복사 실패";
        SkillData cloneFinisher = MakeSkill("전법규", "분신_끝딜", "분신 — 마나 스킬(잃은 체력 4%)", "사장님 10-06 「끝딜(잃은체력6%, 분신4%)」 — 분신(소환체)이 같은 끝딜을 4%로. 마나 게이지(평타 +1) 145에 차면 맞은 적 한 기가 잃은 체력(최대−현재)의 4%(AP 방어 무시, 보스 상한 없음) → 게이지 0. 분신 유닛의 skills에만 걸린다.", SkillTriggerType.OnHitCount, 0f, 1f, 145,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMissingHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.04f });
        clone.prefab = AssetDatabase.LoadAssetAtPath<UnitData>(SummonTemplate).prefab;
        clone.unitName = "전법규 분신";
        clone.attackPower = Mathf.Round(beopgyu.attackPower * 0.3f); clone.attackRange = beopgyu.attackRange; clone.attackSpeed = beopgyu.attackSpeed;
        clone.moveSpeed = 0f; clone.hp = 100f; clone.trait = null; clone.skill = null; clone.skills = new List<SkillData> { cloneFinisher };
        clone.manaMax = 145f; clone.manaGaugePerMana = 1f;
        EditorUtility.SetDirty(clone);
        SkillData callClone = MakeSkill("전법규", "분신", "분신 — 1기 상시", "사장님 10-06 확정: 분신(소환체) 1기가 늘 곁에 있다(죽으면 5초 안에 다시 — CooldownAutoCast, 유재헌 수하와 같은 틀). 분신 공격력 = 본체의 30%(제안값), 분신은 마나 스킬로 같은 끝딜을 4%.", SkillTriggerType.CooldownAutoCast, 0f, 1f, 0,
            new SkillEffect { kind = SkillEffectKind.SummonUnit, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { clone }, summonLifetime = 99999f, summonFanDegrees = 35f, summonRadius = 70f });
        callClone.levels[0].cooldown = 5f;
        EditorUtility.SetDirty(callClone);
        if (!beopgyu.skills.Contains(callClone)) beopgyu.skills.Add(callClone);
        EditorUtility.SetDirty(beopgyu);
        log.Add("전법규 분신");

        // ── 김민규 보물위치공개 ──
        UnitData minkyu = U("제한_김민규");
        SkillData reveal = MakeSkill("김민규", "보물위치공개", "보물위치공개 — 쿨스킬(반경 1000)", "사장님 10-06 「쿨스킬(반경1000 보물위치공개)」 확정: 명령 카드 액티브 칸을 누르면 반경 1000(원작 단위) 안에 숨은 보물상자 자리마다 땅 빛기둥이 6초 서고 미니맵에 노란 점이 찍힌다(상자는 안 열린다 — 위치만, 열기는 항해일지 탐색). 쿨 60초는 제안값(항해일지 탐색 쿨 100초). 호스트/싱글 화면 기준.", SkillTriggerType.ActiveButton, 1000f, 1f, 0,
            new SkillEffect { kind = SkillEffectKind.RevealTreasure, target = SkillTargetKind.Self, duration = 6f });
        reveal.levels[0].cooldown = 60f;
        EditorUtility.SetDirty(reveal);
        if (!minkyu.skills.Contains(reveal)) minkyu.skills.Add(reveal);
        EditorUtility.SetDirty(minkyu);
        log.Add("김민규 보물위치공개");

        // ── 빛기둥 재질 ──
        Material beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMatPath);
        if (beam == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            beam = new Material(sh);
            AssetDatabase.CreateAsset(beam, BeamMatPath);
        }
        beam.SetColor("_BaseColor", new Color(1f, 0.85f, 0.2f, 0.45f));
        beam.SetFloat("_Surface", 1f); beam.SetFloat("_Blend", 0f);
        beam.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); beam.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        beam.SetFloat("_ZWrite", 0f); beam.SetOverrideTag("RenderType", "Transparent");
        beam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); beam.renderQueue = 3000;
        beam.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(beam);

        AssetDatabase.SaveAssets();

        // ── 씬: TreasureHunt.revealBeamMaterial · RewardDistributor.lostWalletItem ──
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hunt = Object.FindFirstObjectByType<TreasureHunt>(FindObjectsInactive.Include);
        var rewards = Object.FindFirstObjectByType<RewardDistributor>(FindObjectsInactive.Include);
        if (hunt != null) { var so = new SerializedObject(hunt); so.FindProperty("revealBeamMaterial").objectReferenceValue = beam; so.ApplyModifiedProperties(); }
        if (rewards != null) { var so = new SerializedObject(rewards); so.FindProperty("lostWalletItem").objectReferenceValue = wallet; so.ApplyModifiedProperties(); }
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        log.Add($"씬 이음: TreasureHunt {(hunt != null ? "O" : "없음")} · RewardDistributor {(rewards != null ? "O" : "없음")} · 저장 {saved}");
        return "제한됨 마무리: " + string.Join(" · ", log);
    }
}
