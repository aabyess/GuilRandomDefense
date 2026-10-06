using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 신문철 「말썽쟁이」 점검(10-06) — gameshot:
//   call:MuncheolProbe.Arena  → wait:1 → call:MuncheolProbe.Report   (공속 오라 +15% · 스노우볼 누적 · 이감 관측)
//   call:MuncheolProbe.Snack  → call:MuncheolProbe.Report            (엄마간식: 아군 공속 +100% · 쿨 · 두 번째 거부)
static class MuncheolProbe
{
    static UnitIdentity unit, ally, far;
    static EnemyDummy target;
    static float allyBase, farBase, minSlow = 1f, maxStack;
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData ed = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        UnitData d = Roster("초월_신문철_AP"), a = Roster("흔함_강재규");
        if (spawner == null || lane == null || ed == null || d == null) return "❌ 준비 안 됨";
        Vector3 c = lane.LaneCenter;
        unit = spawner.Spawn(d, c, 0).GetComponent<UnitIdentity>();
        ally = spawner.Spawn(a, c + new Vector3(60f, 0f, 0f), 0).GetComponent<UnitIdentity>();      // 오라 반경 850 안
        far = spawner.Spawn(a, c + new Vector3(0f, 0f, -1500f), 0).GetComponent<UnitIdentity>();   // 오라 밖(대조)
        GameObject go = Object.Instantiate(ed.prefab, c + new Vector3(-40f, 0f, 80f), Quaternion.Euler(0f, 180f, 0f));
        if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
        target = go.GetComponent<EnemyDummy>();
        target.Initialize(ed, 1e5f);
        target.SetLane(0);
        minSlow = 1f; maxStack = 0f;
        return $"세움: {unit.Data.DisplayName} 스킬 {unit.Data.skills.Count}개 · 사거리 {unit.GetComponent<UnitAttacker>().AttackRange:F0} · 표적 체력 {target.Hp:N0}";
    }

    static string Report()
    {
        if (unit == null) return "❌ Arena 먼저";
        var atk = unit.GetComponent<UnitAttacker>();
        var allyAtk = ally.GetComponent<UnitAttacker>();
        var farAtk = far.GetComponent<UnitAttacker>();
        if (allyBase <= 0f) { allyBase = 1f; }
        maxStack = Mathf.Max(maxStack, atk.AttackDamageStackValue);
        minSlow = Mathf.Min(minSlow, target != null ? target.EffectiveSlowMultiplier : 1f);
        var sb = new StringBuilder();
        sb.AppendLine($"   신문철 공속 배율 {atk.CurrentAttackSpeedMultiplier:F3} · 가까운 아군 {allyAtk.CurrentAttackSpeedMultiplier:F3} · 먼 아군(오라 밖) {farAtk.CurrentAttackSpeedMultiplier:F3}  ← 앞 둘은 먼 쪽의 ×1.15여야 한다(간식 전)");
        sb.AppendLine($"   스노우볼 지금 {atk.AttackDamageStackValue:F2} · 지금까지 최대 {maxStack:F2}(상한 0.15)");
        sb.AppendLine($"   표적 체력 {(target != null ? target.Hp : 0f):N0}/{(target != null ? target.MaxHp : 0f):N0} · 이속 배율 지금 {(target != null ? target.EffectiveSlowMultiplier : 1f):F2} · 관측 최저 {minSlow:F2}(0.85면 노출중독 이감 관측)");
        SkillData snack = unit.Data.skills.FirstOrDefault(s => s != null && s.skillName.StartsWith("엄마간식"));
        if (snack != null) sb.AppendLine($"   엄마간식 쿨 남음 {atk.ActiveCooldownRemaining(snack):F1}/{atk.ActiveCooldownTotal(snack):F1}");
        return sb.ToString();
    }

    static string Snack()
    {
        if (unit == null) return "❌ Arena 먼저";
        var atk = unit.GetComponent<UnitAttacker>();
        SkillData snack = unit.Data.skills.FirstOrDefault(s => s != null && s.skillName.StartsWith("엄마간식"));
        if (snack == null) return "❌ 엄마간식 없음";
        float before = ally.GetComponent<UnitAttacker>().CurrentAttackSpeedMultiplier;
        bool ok = atk.TryCastActiveOnAlly(snack, ally, out string reason);
        float after = ally.GetComponent<UnitAttacker>().CurrentAttackSpeedMultiplier;
        bool ok2 = atk.TryCastActiveOnAlly(snack, ally, out string reason2);
        return $"   1회 {(ok ? "성공" : "거부: " + reason)} · 아군 공속 {before:F3} → {after:F3}(×{after / before:F2}, 기대 ×1.87~2.0 — 합산이면 +1.0)\n   2회(쿨 안) {(ok2 ? "⚠️ 또 성공" : "거부: " + reason2)}";
    }
}
