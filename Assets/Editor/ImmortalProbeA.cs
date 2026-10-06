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
    static string Run고도현() => Setup("불멸_고도현");
    static string Run이이삭() => Setup("불멸_이이삭");
    static string Run김용태() => Setup("불멸_김용태");

    // 김용태: 체력 게이지를 99 직전으로 두고 근처 적을 죽여 +5 훅이 도는지, 체력스킬 버프가 붙는지
    static string DeathHook()
    {
        var f = typeof(UnitAttacker).GetField("lifeGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance);
        int before = (int)f.GetValue(atk);
        EnemyDummy t = normals[0];
        t.TakeDamage(1e12f, DamageType.AD, AttackType.Unassigned, 0, armorIgnoreRatio: 1f, isAbilityDamage: false);
        int after = (int)f.GetValue(atk);
        return $"[적 사망 훅] 근처 적 1기 사망({(t == null || t.IsDead ? "예" : "아니오")}) → 체력 게이지 {before} → {after}(전투 평타 +1이 섞일 수 있음, 기대 최소 +5)";
    }
    static string Gauge99()
    {
        typeof(UnitAttacker).GetField("lifeGaugeInitialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(atk, true);
        typeof(UnitAttacker).GetField("lifeGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(atk, 99);
        return "체력 게이지 99로 둠";
    }
    static string BuffCheck() => $"[체력스킬 후] 자기 공격력 비 {atk.AttackDamage / dmg0:F2} 공속 비 {atk.CurrentAttackSpeedMultiplier / as0:F2}(발동했다면 ≈1.30)";

    // 고도현 약처방: 아군 1(김건)에 걸고 → 아군 2(새로 세운 특별함)로 옮기면 옛 대상에서 떼어지는지
    static UnitAttacker second; static float secondAd0, secondAs0;
    static string Cure1()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var other = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/전설적인_노태현.asset");
        var go = spawner.Spawn(other, lane != null ? lane.TakeSpawnPosition(other) : Vector3.zero, 0);
        second = go.GetComponent<UnitAttacker>(); secondAd0 = second.AttackDamage; secondAs0 = second.CurrentAttackSpeedMultiplier;
        var skill = unit.Data.skills.First(x => x.skillName.StartsWith("약처방"));
        float ad0 = allyAtk.AttackDamage, as1 = allyAtk.CurrentAttackSpeedMultiplier;
        bool ok = atk.TryCastActiveOnAlly(skill, allyAtk.GetComponent<UnitIdentity>(), out string why);
        return $"[약처방 1] 시전 {(ok ? "성공" : "실패 " + why)} · 김건 공격력 비 {allyAtk.AttackDamage / ad0:F2}(기대 1.10) 공속 비 {allyAtk.CurrentAttackSpeedMultiplier / as1:F2}(기대 1.10)";
    }
    static string Cure2()
    {
        var skill = unit.Data.skills.First(x => x.skillName.StartsWith("약처방"));
        var st = typeof(UnitAttacker).GetMethod("GetRuntimeState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(atk, new object[] { skill });
        st.GetType().GetField("activeReadyAt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(st, 0f);
        bool ok = atk.TryCastActiveOnAlly(skill, second.GetComponent<UnitIdentity>(), out string why);
        return $"[약처방 2: 노태현으로 옮김] 시전 {(ok ? "성공" : "실패 " + why)} · 노태현 공격력 비 {second.AttackDamage / secondAd0:F2}(기대 1.10) · 김건 공속 {allyAs0:F2} → {allyAtk.CurrentAttackSpeedMultiplier:F2}(기대 원래값, 옛 대상에서 뗌)";
    }

    static string ShredFactor()
    {
        var m = typeof(UnitAttacker).GetMethod("ArmorShredDamageFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        EnemyDummy t = normals[0];
        float shred = t.Data.armor - t.EffectiveArmor;
        float factor = (float)m.Invoke(atk, new object[] { t });
        return $"[방깍 비례] 적 방깍 {shred:F1} → 피해 배율 {factor:F3}(기대 1 + min(0.5, 0.005×{shred:F1}) = {1f + Mathf.Min(0.5f, 0.005f * shred):F3})";
    }

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
