using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 희귀함·안흔함 재배정 스킬 한 번씩 발동 점검(10-15) — gameshot: call:ShopSlotProbe.Fund call:RareSkillFireProbe.Setup wait:30 call:RareSkillFireProbe.Fire wait:5 call:RareSkillFireProbe.Later
static class RareSkillFireProbe
{
    static UnitIdentity[] units;
    static readonly string[] Names = { "희귀함_김경현", "희귀함_고어진", "희귀함_강재규", "희귀함_구주호", "안흔함_이호준", "안흔함_문필환", "안흔함_김경현", "안흔함_김민준", "안흔함_강주혁", "안흔함_강재규" };
    static System.Collections.Generic.List<EnemyDummy> tested;
    static float[] before;
    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        units = Names.Select(n =>
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            return spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        }).ToArray();
        return $"세움 {units.Length}기";
    }

    static string Fire()
    {
        if (!Application.isPlaying || units == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        var enemies = EnemyDummy.Active.Where(e => e != null && !e.IsBoss).ToList();
        sb.AppendLine($"[적] 일반 {enemies.Count}기");
        if (enemies.Count < 4) return sb + "❌ 적 부족";
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        // 적 하나를 기준으로 그 주변 적도 같이 쓴다 — 범위 효과 수를 보려고 전부 체력을 크게
        foreach (var e in enemies) hpField.SetValue(e, 1e9f);
        tested = enemies;
        before = enemies.Select(e => e.Hp).ToArray();
        var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int ui = 0; ui < units.Length; ui++)
        {
            var u = units[ui];
            if (u == null || u.Data == null) { sb.AppendLine($"[{Names[ui]}] 유닛 없음"); continue; }
            int n = u.Data.SkillCount;
            for (int s = 0; s < n; s++)
            {
                SkillData sk = u.Data.SkillAt(s);
                if (sk == null || sk.levels == null || sk.levels.Count == 0) { sb.AppendLine($"[{Names[ui]}] 스킬 {s} 비어 있음"); continue; }
                // 이 유닛 스킬이 맞출 대상: 가장 가까운 적 하나
                EnemyDummy t = enemies.OrderBy(e => (e.transform.position - u.transform.position).sqrMagnitude).First();
                float[] pre = enemies.Select(e => e.Hp).ToArray();
                cast.Invoke(u.GetComponent<UnitAttacker>(), new object[] { sk.levels[0], sk.levels[0].WorldRange, t, 1000f });
                int hit = 0; float tot = 0f;
                for (int i = 0; i < enemies.Count; i++) { float d = pre[i] - enemies[i].Hp; if (d > 0f) { hit++; tot += d; } }
                sb.AppendLine($"[{Names[ui]}] {sk.name.Replace("SkillData_", "")} 즉시: 맞은 적 {hit}기 · 총 Δ{tot:F0} · 대상 Δ{pre[enemies.IndexOf(t)] - t.Hp:F0} · 대상 스턴 {t.IsStunned}");
            }
        }
        before = enemies.Select(e => e.Hp).ToArray();   // 이후 지속 피해만 Later에서
        return sb.ToString();
    }

    static string Later()
    {
        if (tested == null) return "❌ Fire 먼저";
        int hit = 0; float tot = 0f, max = 0f;
        for (int i = 0; i < tested.Count; i++) { if (tested[i] == null) continue; float d = before[i] - tested[i].Hp; if (d > 0f) { hit++; tot += d; max = Mathf.Max(max, d); } }
        return $"[지속 피해 후] 맞은 적 {hit}기 · 총 Δ{tot:F0} · 한 적 최대 Δ{max:F0} (마젤란 기대: 독 2500×4=10000 + 히드라 1250×33=41250 대상 하나에 집중)";
    }
}
