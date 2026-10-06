using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 김건 점검(10-06) — gameshot: call:ShopSlotProbe.Fund wait:30 call:GunProbe.Run wait:1 call:GunProbe.After
// 스킬 연결 · 포식 카운트(적 곁으로 데려와 100번 시전) · 형태 전 깔아뭉개기 무반응 → 구건 시전 후 버프·공속·지속(카운트 소모) → 형태 중 넉백·방무딜·스턴.
static class GunProbe
{
    static UnitIdentity gun;
    static UnitAttacker atk;
    static System.Collections.Generic.List<EnemyDummy> near;
    static float[] hp0;
    static MethodInfo cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);

    static void Fire(string key, EnemyDummy target)
    {
        SkillData s = gun.Data.skills.First(x => x.skillName.StartsWith(key));
        cast.Invoke(atk, new object[] { s.levels[0], s.levels[0].WorldRange, target, 0f });
    }

    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_김건_AP.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        gun = go.GetComponent<UnitIdentity>(); atk = go.GetComponent<UnitAttacker>();
        var sb = new StringBuilder();
        sb.AppendLine($"[김건] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")) + $" · 체력게이지 {d.lifeGaugeMax} · damageType {(int)d.damageType}");
        var mat = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/변화됨_김건.asset");
        sb.AppendLine($"  변화됨 김건 표시 {mat.DisplayName}");
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        near = EnemyDummy.Active.Where(e => e != null && !e.IsDead && !e.IsBoss).OrderBy(e => Vector3.Distance(e.transform.position, go.transform.position)).Take(12).ToList();
        int k = 0;
        foreach (EnemyDummy e in near)
        {
            hpField.SetValue(e, 1e8f);
            float a = k++ * Mathf.PI / 6f;
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 to = go.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 40f;
            if (ag != null) ag.Warp(to); else e.transform.position = to;
        }
        hp0 = near.Select(e => e.Hp).ToArray();
        // 1) 형태 전: 깔아뭉개기·배치기·넉백은 게이트가 막아야 한다(CastSkillLevel 직접 호출은 게이트를 안 탄다 — 대신 상태만 보고).
        sb.AppendLine($"  형태 전 GunFormActive {atk.GunFormActive} · 카운트 {atk.UnitDeleteCount}");
        // 2) 포식: 일반 적 약간 낮춰 죽을 수 있게(1e12 막타) 50번 시도
        for (int i = 0; i < 5; i++) Fire("포식", near[0]);
        sb.AppendLine($"  포식 5번 시전(가까운 일반 적 즉사): 카운트 {atk.UnitDeleteCount}(죽은 일반 적 수와 같아야 함)");
        int count = atk.UnitDeleteCount;
        // 3) 구건 시전 → 형태
        float asBefore = atk.CurrentAttackSpeedMultiplier;
        Fire("구건", near.FirstOrDefault(e => e != null && !e.IsDead));
        sb.AppendLine($"  구건 시전: 형태 {atk.GunFormActive} 남은 {atk.GunFormRemaining:F1}초(기대 8 + {count}×0.5 = {System.Math.Min(15f, count * 0.5f) + 8f:F1}) · 카운트 {atk.UnitDeleteCount}(0이어야 함) · 공속 {asBefore:F2}→{atk.CurrentAttackSpeedMultiplier:F2}(기대 ×0.70)");
        // 4) 형태 중 넉백·방무딜·스턴 — 살아 있는 적에게
        var alive = near.Where(e => e != null && !e.IsDead).ToList();
        EnemyDummy t = alive.FirstOrDefault();
        if (t != null)
        {
            Vector3 p0 = t.transform.position;
            Fire("힘의작용과반작용", t);
            sb.AppendLine($"  넉백: 이동 {Vector3.Distance(p0, t.transform.position):F1}(기대 160/4.167 = {160f / 4.167f:F1}) · 보스 면역은 코드 경로");
        }
        float[] before = alive.Select(e => e.Hp).ToArray();
        Fire("깔아뭉개기", alive.FirstOrDefault());
        Fire("배치기", alive.FirstOrDefault());
        sb.AppendLine($"  깔아뭉개기: 체력Δ " + string.Join(", ", alive.Take(6).Select((e, i) => $"{before[i] - e.Hp:F0}")) + " · 스턴 " + string.Join(",", alive.Take(6).Select(e => e.IsStunned ? "T" : "F")));
        return sb.ToString();
    }

    static string After() => atk == null ? "❌ Run 먼저" : $"[+1초] 형태 {atk.GunFormActive} 남은 {atk.GunFormRemaining:F1}초 · 공속 {atk.CurrentAttackSpeedMultiplier:F2}";
    static string Later() => atk == null ? "❌ Run 먼저" : $"[+10초] 형태 {atk.GunFormActive}(false여야 함) · 공속 {atk.CurrentAttackSpeedMultiplier:F2}(1.0 복귀)";
}
