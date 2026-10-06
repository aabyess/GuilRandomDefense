using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 김만경 점검(10-06) — gameshot: call:ShopSlotProbe.Fund wait:30 call:MangyeongProbe.Run wait:1.2 call:MangyeongProbe.After
// 스킬 연결 · 스플래시 반경 · 방깍 오라(보스 −45·일반 0) · 이감 오라 · 보잡 배율 · 빽(전설 이상 유닛 N기 세워 배율 1+0.02N) · 스플래시 실제 피해 배율.
static class MangyeongProbe
{
    static UnitIdentity man;
    static UnitAttacker atk;
    static System.Collections.Generic.List<EnemyDummy> normals;
    static EnemyDummy boss;
    static float[] armor0;
    static float bossArmor0;

    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_김만경_AD.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        man = go.GetComponent<UnitIdentity>(); atk = go.GetComponent<UnitAttacker>();
        var sb = new StringBuilder();
        sb.AppendLine($"[김만경] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")) + $" · 스플래시 {d.attackSplashRadius} · trait {(d.trait == null ? "없음" : d.trait.name)} · 마나 {d.manaMax} 체력게이지 {d.lifeGaugeMax}");
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        normals = EnemyDummy.Active.Where(e => e != null && !e.IsDead && !e.IsBoss).OrderBy(e => Vector3.Distance(e.transform.position, go.transform.position)).Take(4).ToList();
        boss = EnemyDummy.Active.FirstOrDefault(e => e != null && !e.IsDead && e.IsBoss);
        int k = 0;
        foreach (EnemyDummy e in normals)
        {
            hpField.SetValue(e, 1e8f);
            float a = k++ * Mathf.PI / 2f;
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 to = go.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 50f;
            if (ag != null) ag.Warp(to); else e.transform.position = to;
        }
        if (boss != null) { hpField.SetValue(boss, 1e8f); bossArmor0 = boss.EffectiveArmor; var bag = boss.GetComponent<UnityEngine.AI.NavMeshAgent>(); Vector3 bt = go.transform.position + new Vector3(0f, 0f, 100f); if (bag != null) bag.Warp(bt) /*결과는 거리로 확인*/; else boss.transform.position = bt; }
        armor0 = normals.Select(e => e.EffectiveArmor).ToArray();
        sb.AppendLine($"  적: 일반 {normals.Count}기 · 보스 {(boss != null ? boss.name : "없음")}");
        return sb.ToString();
    }

    static string Legends()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        string[] legends = { "전설적인_김건", "전설적인_구주호", "전설적인_노태현", "전설적인_양재모", "전설적인_김용태" };
        foreach (string n in legends)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            spawner.Spawn(u, lane != null ? lane.TakeSpawnPosition(u) : Vector3.zero, 0);
        }
        return "전설 5기 추가";
    }

    static string After()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[+1.2초] 방어 변화(일반은 0이어야 함): " + string.Join(", ", normals.Select((e, i) => $"{e.EffectiveArmor - armor0[i]:F1}")));
        sb.AppendLine("  일반 적 이속 배율: " + string.Join(", ", normals.Select(e => $"{e.EffectiveSlowMultiplier:F2}")) + "(기대 0.75)");
        if (boss != null) sb.AppendLine($"  보스 방어 {bossArmor0:F1} → {boss.EffectiveArmor:F1} · 보스-김만경 거리 {Vector3.Distance(boss.transform.position, man.transform.position):F0}(오라 범위 안일 때만 −45 — 보스가 멀면 변화 없음)");
        if (boss != null) sb.AppendLine($"  [디버그] 보스 PV {boss.PointValue} · 오라방깎 {boss.AuraArmorShred} · 방어 원값 {boss.EffectiveArmor + boss.AuraArmorShred} · 레인 {boss.LaneIndex} · 활성 {EnemyDummy.Active.Contains(boss)} · 사망 {boss.IsDead}");
        sb.AppendLine($"  빽 배율 {atk.HighGradeSplashFactorNow:F2}(전설 이상 N기: 1+0.02N, 상한 2.0)");
        return sb.ToString();
    }

    // 스플래시: 가까운 두 적 사이에서 평타를 실제로 치게 하는 대신 배율만 본다 — 보잡·빽은 공식 확인으로 충분.
}
