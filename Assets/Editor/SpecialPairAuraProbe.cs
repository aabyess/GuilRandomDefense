using System.Text;
using UnityEditor;
using UnityEngine;

// 특수함 짝 스킬(율희 A0EF·임재현 A0PE+A0PD·헬로우먼 A0WO) 오라 실발동 확인(구현담당1, 10-16)
// gameshot: call:SpecialPairAuraProbe.Setup wait:5 call:SpecialPairAuraProbe.Report
static class SpecialPairAuraProbe
{
    static EnemyDummy enemy; static UnitAttacker baseUnit; static float armor0, dmg0, int0;
    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sp = Object.FindFirstObjectByType<UnitSpawner>();
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        EnemyData d = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        GameObject go = Object.Instantiate(d.prefab, c + new Vector3(0f, 0f, 15f), Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
        enemy = go.GetComponent<EnemyDummy>(); enemy.Initialize(d, 1f); enemy.SetLane(0);
        armor0 = enemy.EffectiveArmor;
        baseUnit = sp.Spawn(AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/흔함_강재규.asset"), c, 0).GetComponent<UnitAttacker>();
        dmg0 = baseUnit.AttackDamage; int0 = baseUnit.AttackInterval;
        foreach (string n in new[] { "특수함_BJ_율희", "특수함_임재현", "특수함_헬로우먼" })
            sp.Spawn(AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset"), c, 0);
        return $"세움 · 전 방어 {armor0:F1} · 기준 유닛 공격력 {dmg0:F1} 간격 {int0:F3}";
    }
    static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"방어 {armor0:F1} → {enemy.EffectiveArmor:F1} (기대: A0EF −30 → +30)");
        sb.AppendLine($"아군 공격력 {dmg0:F1} → {baseUnit.AttackDamage:F1} (기대 기본의 +120%: A0PE 0.6 + A0PD 0.6)");
        sb.AppendLine($"아군 공격 간격 {int0:F3} → {baseUnit.AttackInterval:F3} (기대 ÷1.11)");
        return sb.ToString();
    }
}
