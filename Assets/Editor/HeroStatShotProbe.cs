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
