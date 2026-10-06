using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 최상호 바지사장 점검(10-06) — gameshot:
//   call:BajisajangProbe.Setup wait:1 call:BajisajangProbe.Report1 wait:12 call:BajisajangProbe.Report2 call:BajisajangProbe.Report3
// 공속 비례(②절대공격 피해·③절대방어 스턴 지속이 공속에 비례하나) · 액티브(④ 쿨 40초·재시전 거부·10초 뒤 공속 복귀) · 특성 3pt → 쿨 20초 · 타이핑 「씹덕대마왕」.
static class BajisajangProbe
{
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static UnitIdentity baji;
    static readonly MethodInfo Resolve = typeof(UnitAttacker).GetMethod("ResolveSkillEffectValue", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly MethodInfo ScaleFactor = typeof(UnitAttacker).GetMethod("AttackSpeedScaleFactor", BindingFlags.NonPublic | BindingFlags.Instance);

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        UnitData d = Roster("초월_최상호_AP");
        baji = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        return $"세움: {baji.Data.unitName} 스킬 {baji.Data.skills.Count}개 [{string.Join(", ", baji.Data.skills.Select(s => s.skillName))}] · 평타 공속 {baji.Data.attackSpeed}";
    }

    static string Numbers(UnitAttacker a, SkillEffect dmg, SkillEffect stun)
    {
        float v = (float)Resolve.Invoke(a, new object[] { dmg, null, 0f });
        float f = (float)ScaleFactor.Invoke(a, new object[] { stun });
        return $"공속 배율 {a.CurrentAttackSpeedMultiplier:F2} → ②절대공격 피해 {v:N0} · ③절대방어 스턴 {stun.duration * f:F2}초(배율 ×{f:F2})";
    }

    static string SelectIt()
    {
        if (baji == null) return "❌ Setup 먼저";
        var selection = Object.FindFirstObjectByType<SelectionManager>();
        if (selection == null || !baji.TryGetComponent(out Selectable sel)) return "❌ SelectionManager/Selectable 없음";
        selection.SelectOnly(sel);
        return "선택: 바지사장 한 기";
    }

    // 정보 창 스킬 아이콘 호버 툴팁 재현 — GameHud.OnSkillIconHover(index)를 직접 부른다(마우스를 올린 것과 같은 길).
    static string HoverIcon()
    {
        var hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null) return "❌ GameHud 없음";
        var m = typeof(GameHud).GetMethod("OnSkillIconHover", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(hud, new object[] { 3 });
        return "툴팁: 스킬 아이콘 4번째(분노조절장애) 호버";
    }

    static string Report1()
    {
        if (!Application.isPlaying || baji == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = baji.GetComponent<UnitAttacker>();
        SkillData stunS = baji.Data.skills.First(s => s.skillName.StartsWith("적응불가"));
        SkillData atk = baji.Data.skills.First(s => s.skillName.StartsWith("절대공격"));
        SkillData def = baji.Data.skills.First(s => s.skillName.StartsWith("절대방어"));
        SkillData rage = a.ActiveSkill;
        SkillEffect dmg = atk.levels[0].effects[0], stun = def.levels[0].effects[0];
        sb.AppendLine($"[①] {stunS.triggerType} 확률 {stunS.levels[0].triggerChance:P0} · {stunS.levels[0].effects[0].kind} {stunS.levels[0].effects[0].duration}초");
        sb.AppendLine($"[②] {atk.triggerType} {atk.levels[0].gaugeKind} {atk.levels[0].hitCountThreshold} · 범위 {atk.levels[0].range} · 기본 {dmg.multiplier:N0} · 공속비례 {dmg.attackSpeedScale}/상한 {dmg.attackSpeedScaleCap}");
        sb.AppendLine($"[③] {def.triggerType} {def.levels[0].gaugeKind} {def.levels[0].hitCountThreshold} · 범위 {def.levels[0].range} · 스턴 {stun.duration}초 · 공속비례 {stun.attackSpeedScale}/상한 {stun.attackSpeedScaleCap}");
        sb.AppendLine($"[④] ActiveSkill = {(rage != null ? rage.skillName : "없음")} · 쿨 {a.ActiveCooldownTotal(rage)}초 · 남은 {a.ActiveCooldownRemaining(rage):F1}");
        sb.AppendLine("  [전] " + Numbers(a, dmg, stun));
        bool ok = a.TryCastActive(rage, out string why);
        sb.AppendLine($"  시전 → {(ok ? "성공" : "실패: " + why)} · 남은 쿨 {a.ActiveCooldownRemaining(rage):F1}초");
        sb.AppendLine("  [후] " + Numbers(a, dmg, stun));
        bool again = a.TryCastActive(rage, out string why2);
        sb.AppendLine($"  재시전 → {(again ? "⚠️ 성공(쿨 무시)" : "거부 ✅: " + why2)}");
        return sb.ToString();
    }

    static string Report2()
    {
        if (!Application.isPlaying || baji == null) return "❌ Setup 먼저";
        UnitAttacker a = baji.GetComponent<UnitAttacker>();
        SkillData rage = a.ActiveSkill;
        SkillEffect dmg = baji.Data.skills.First(s => s.skillName.StartsWith("절대공격")).levels[0].effects[0];
        SkillEffect stun = baji.Data.skills.First(s => s.skillName.StartsWith("절대방어")).levels[0].effects[0];
        return $"[12초 뒤] 남은 쿨 {a.ActiveCooldownRemaining(rage):F1}초(기대 ≈28) · " + Numbers(a, dmg, stun) + "(기대: 공속 원래대로)";
    }

    static string Report3()
    {
        if (!Application.isPlaying || baji == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = baji.GetComponent<UnitAttacker>();
        SkillData rage = a.ActiveSkill;
        var trait = AssetDatabase.LoadAssetAtPath<UnitTraitData>("Assets/Data/Traits/Trait_초월_최상호_AP.asset");
        UnitUpgrades up = PlayerContext.Get(0).UnitUpgrades;
        sb.AppendLine($"[특성] {trait.traitName} · {trait.costTraitPoints}pt · 전 쿨 {a.ActiveCooldownTotal(rage)}초");
        up.AddTraitPoints(3);
        bool spent = up.TrySpendTraitPoints(trait.costTraitPoints);
        if (spent) up.Unlock(trait);
        sb.AppendLine($"  포인트 {trait.costTraitPoints} 소모 {(spent ? "성공" : "실패")} → 레벨 인덱스 {up.SkillLevelIndexFor(baji.Data)} · 쿨 {a.ActiveCooldownTotal(rage)}초(기대 20 = −50%)");
        var box = Object.FindFirstObjectByType<GameChatBox>();
        string msg = box != null ? box.TryExecuteCode(0, "씹덕대마왕") : null;
        sb.AppendLine($"[타이핑] 「씹덕대마왕」 → {msg ?? "(코드 아님)"}");
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/초월_최상호_AP.asset");
        sb.AppendLine($"[재료] {string.Join(" + ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name + "(" + i.unit.unitName + ")" : "null"))}");
        return sb.ToString();
    }
}
