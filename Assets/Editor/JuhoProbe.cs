using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 구주호 점검(10-06) — gameshot: call:ShopSlotProbe.Fund wait:30 call:JuhoProbe.Run wait:3 call:JuhoProbe.Report1 call:JuhoProbe.Upgrade wait:3 call:JuhoProbe.Report2
static class JuhoProbe
{
    static UnitIdentity juho; static UnitAttacker atk;
    static System.Collections.Generic.List<EnemyDummy> normals; static float[] armor0; static float dmg0;

    static string R002Icon()
    {
        string[] g = AssetDatabase.FindAssets("ItemData_R002 t:ItemData");
        if (g.Length == 0) return "❌ R002 없음";
        var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g[0]));
        return $"{AssetDatabase.GUIDToAssetPath(g[0])} · icon {(it.icon == null ? "null ❌" : it.icon.name)}";
    }

    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_구주호_AD.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        juho = go.GetComponent<UnitIdentity>(); atk = go.GetComponent<UnitAttacker>();
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        normals = EnemyDummy.Active.Where(e => e != null && !e.IsDead && !e.IsBoss).OrderBy(e => Vector3.Distance(e.transform.position, go.transform.position)).Take(4).ToList();
        int k = 0;
        foreach (EnemyDummy e in normals)
        {
            hpField.SetValue(e, 1e8f);
            float a = k++ * Mathf.PI / 2f;
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 to = go.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 50f;
            if (ag != null) { var wm = e.GetComponent<WaypointMover>(); if (wm != null) wm.enabled = false; ag.enabled = false; e.transform.position = to; } else e.transform.position = to;
        }
        armor0 = normals.Select(e => e.EffectiveArmor).ToArray();
        dmg0 = atk.AttackDamage;
        return $"[구주호] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")) + $" · 스플래시 {d.attackSplashRadius} · trait {(d.trait == null ? "없음" : d.trait.name + " " + d.trait.costTraitPoints + "pt idx" + d.trait.skillLevelUnlockIndex)} · 공격력 {dmg0}";
    }

    static string Dist() => string.Join(",", normals.Select(e => ((int)Vector3.Distance(e.transform.position, juho.transform.position)).ToString()));
    static string Report1() => "거리 " + Dist() + " " + Report1b();
    static string Report1b() => "[레벨1] 방어 변화(기대 −30): " + string.Join(", ", normals.Select((e, i) => $"{e.EffectiveArmor - armor0[i]:F1}")) + $" · 공격력 {atk.AttackDamage}";

    static string Upgrade()
    {
        UnitUpgrades up = PlayerContext.Get(0).UnitUpgrades;
        var trait = juho.Data.trait;
        up.AddTraitPoints(3);
        bool spent = up.TrySpendTraitPoints(trait.costTraitPoints);
        if (spent) up.Unlock(trait);
        int k = 0;
        foreach (EnemyDummy e in normals) { float a = k++ * Mathf.PI / 2f; e.transform.position = juho.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 50f; }
        return $"특성 {trait.costTraitPoints}pt 소모 {(spent ? "성공" : "실패")} → 레벨 인덱스 {up.SkillLevelIndexFor(juho.Data)}";
    }

    static string Report2() => "거리 " + Dist() + " [레벨2] 방어 변화(기대 −40): " + string.Join(", ", normals.Select((e, i) => $"{e.EffectiveArmor - armor0[i]:F1}")) + $" · 공격력 {atk.AttackDamage}(기대 {dmg0 * 1.2f:F1}대)";
}
