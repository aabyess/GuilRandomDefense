using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 불멸 점검(10-06) — gameshot: call:ShopSlotProbe.Fund wait:30 call:ImmortalProbeA.Run<이름> wait:4 call:ImmortalProbeA.Report
static class ImmortalProbeA
{
    static UnitIdentity unit; static UnitAttacker atk; static string label;
    static System.Collections.Generic.List<EnemyDummy> normals; static float[] armor0; static float dmg0, as0;
    static int ally; static UnitAttacker allyAtk; static float allyAs0;

    static string Run정윤식() => Setup("불멸_정윤식");
    static string Run이승우() => Setup("불멸_이승우");
    static string Run신지우() => Setup("불멸_신지우");
    static string Run박은석() => Setup("불멸_박은석");

    static string Setup(string name)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        label = name;
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        var helper = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/전설적인_김건.asset");
        var ha = spawner.Spawn(helper, lane != null ? lane.TakeSpawnPosition(helper) : Vector3.zero, 0);
        allyAtk = ha.GetComponent<UnitAttacker>(); allyAs0 = allyAtk.CurrentAttackSpeedMultiplier;
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        unit = go.GetComponent<UnitIdentity>(); atk = go.GetComponent<UnitAttacker>();
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        normals = EnemyDummy.Active.Where(e => e != null && !e.IsDead && !e.IsBoss && e.PointValue < 200f).OrderBy(e => Vector3.Distance(e.transform.position, go.transform.position)).Take(4).ToList();
        int k = 0;
        foreach (EnemyDummy e in normals)
        {
            hpField.SetValue(e, 1e8f);
            float a = k++ * Mathf.PI / 2f;
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var wm = e.GetComponent<WaypointMover>(); if (wm != null) wm.enabled = false;
            if (ag != null) ag.enabled = false;
            e.transform.position = go.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 60f;
        }
        armor0 = normals.Select(e => e.EffectiveArmor).ToArray();
        dmg0 = atk.AttackDamage; as0 = atk.CurrentAttackSpeedMultiplier;
        return $"[{name}] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType.ToString().Replace("OnHit", "") + ")")) + $" · 스플래시 {d.attackSplashRadius} · trait {(d.trait == null ? "없음" : d.trait.name)} · 마나 {d.manaMax} · 이동 {d.movementAbility} · 방무 {d.attackArmorIgnoreRatio}";
    }

    static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{label}] 방어 변화: " + string.Join(", ", normals.Select((e, i) => $"{e.EffectiveArmor - armor0[i]:F1}")) + " · 이속 배율 " + string.Join(", ", normals.Select(e => $"{e.EffectiveSlowMultiplier:F2}")));
        sb.AppendLine($"  자기 공격력 {dmg0:F0} → {atk.AttackDamage:F0}(비 {atk.AttackDamage / dmg0:F2}) · 자기 공속 {as0:F2} → {atk.CurrentAttackSpeedMultiplier:F2}(비 {atk.CurrentAttackSpeedMultiplier / as0:F2}) · 아군(김건) 공속 {allyAs0:F2} → {allyAtk.CurrentAttackSpeedMultiplier:F2}(비 {allyAtk.CurrentAttackSpeedMultiplier / allyAs0:F2})");
        sb.AppendLine($"  FlyingMover {(unit.GetComponent<FlyingMover>() != null ? "있음" : "없음")} · 전투 4초 뒤 근처 적 사망 {normals.Count(e => e == null || e.IsDead)}/{normals.Count}");
        return sb.ToString();
    }
}
