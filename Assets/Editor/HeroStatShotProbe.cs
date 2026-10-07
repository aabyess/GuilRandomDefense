using UnityEngine;

// 촬영 전용(구현담당2): gameshot call:HeroStatShotProbe.AddXp — 판 안의 초월·영원 유닛 전부에 경험치 1000(약 레벨 14)을 준다.
public static class HeroStatShotProbe
{
    public static string Spawn()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_구주호_AD.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ " + unit.DisplayName + " 세움";
    }

    public static string SpawnYongtae()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/불멸_김용태.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ " + unit.DisplayName + " 세움 · 스킬 " + (unit.skills != null ? unit.skills.Count : 0);
    }

    // 버프·디버프 칸 촬영용 — 판 안의 모든 유닛에 공속 오라·공격력 오라·이속 감소·마나 재생·기절을 건다.
    public static string AddBuffs()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        int n = 0;
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        {
            a.AddAuraBonus(a, SkillEffectKind.AttackSpeedBuffPercent, "probe_as", 0.3f);
            a.AddTimedAuraBonus(a, SkillEffectKind.AttackPowerBuffPercent, "probe_ap", 0.2f, 60f);
            a.AddAuraBonus(a, SkillEffectKind.AllyMoveSpeedDebuff, "probe_ms", 0.2f);
            a.AddTimedAuraBonus(a, SkillEffectKind.ManaRegenBuff, "probe_mr", 1f, 60f);
            n++;
        }
        return $"✅ {n}기에 버프 넷";
    }

    // 이름에 그 글자가 든 내 유닛을 직접 고른다(클릭 판정이 겹친 유닛에 흔들릴 때) — 이름은 SelectName 정적 필드로.
    public static string SelectYongtae()
    {
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.Contains("불멸_김용태")) { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel); return "✅ " + sel.name; }
        return "❌ 못 찾음";
    }

    public static string AddXp()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        int n = 0;
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        {
            a.AddHeroXp(1000); n++;
            Debug.Log($"[HeroStatShot] {a.name} Lv.{a.CharacterLevel} STR {a.CurrentStrength:F1} AGI {a.CurrentAgility:F1} INT {a.CurrentIntelligence:F1}");
        }
        return $"✅ {n}기에 경험치 1000";
    }
}
