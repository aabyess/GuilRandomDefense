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

    static System.Collections.Generic.List<UnitIdentity> RecruitsNow() => UnitIdentity.Active.Where(u => u != null && u.IsRecruit).ToList();
    static string RecruitNow(int times)
    {
        var skill = unit.Data.skills.First(x => x.skillName.StartsWith("유닛회유"));
        var eff = skill.levels[0].effects[0];
        var m = typeof(UnitAttacker).GetMethod("RecruitNearestEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
        int before = EnemyDummy.Active.Count(e => e != null && !e.IsDead);
        for (int i = 0; i < times; i++) m.Invoke(atk, new object[] { 99999f, eff });
        int after = EnemyDummy.Active.Count(e => e != null && !e.IsDead);
        var r = RecruitsNow();
        return $"[회유 {times}회 시도] 적 {before} → {after} · 회유 유닛 {r.Count}기(기대 {Mathf.Min(times, 5)}, 상한 5) · 첫 유닛 {(r.Count > 0 ? r[0].name + " 공격력 " + r[0].GetComponent<UnitAttacker>().AttackDamage.ToString("F0") + "(시전자 " + atk.AttackDamage.ToString("F0") + "의 10% = " + (atk.AttackDamage * 0.1f).ToString("F0") + ")" : "-")}";
    }
    static string Recruit1() => RecruitNow(1);
    static string RecruitInfo()
    {
        var r = RecruitsNow();
        if (r.Count == 0) return "❌ 회유 유닛 없음";
        var go = r[0].gameObject;
        var sb = new StringBuilder("[회유 유닛 몸] 자식: ");
        foreach (Transform c in go.transform) sb.Append($"{c.name}({(c.gameObject.activeSelf ? "켬" : "끔")}) ");
        var anim = go.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.gameObject.activeInHierarchy);
        var rs = go.GetComponentsInChildren<Renderer>().Where(x => x.gameObject.activeInHierarchy && (x is SkinnedMeshRenderer || x is MeshRenderer)).ToList();
        Bounds b = default; bool any = false; foreach (var x in rs) { if (!any) { b = x.bounds; any = true; } else b.Encapsulate(x.bounds); }
        sb.Append($"· 켜진 Animator {(anim != null ? anim.name + "/" + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "컨트롤러없음") : "없음")} · 켜진 렌더러 {rs.Count} · 키 {(any ? b.size.y.ToString("F1") : "-")}");
        // 사진용: 빈 풀밭으로 옮긴다(NavMeshAgent 끄고 위치만)
        var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.enabled = false;
        go.transform.position = unit.transform.position + new Vector3(-90f, 0f, -90f);
        return sb.ToString();
    }
    static string Recruit7() => RecruitNow(7);
    static string SellStats()
    {
        var ctx = PlayerContext.Get(0);
        var r = RecruitsNow();
        var wisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");
        int w0 = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("WispPrefab")), g0 = ctx.GoldWallet.Gold;
        const int N = 400;
        for (int i = 0; i < N; i++) RewardDistributor.Instance.SellRecruit(ctx, wisp);
        int w1 = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("WispPrefab")), g1 = ctx.GoldWallet.Gold;
        return $"[회유 판매 {N}회] 위습 +{w1 - w0}({(w1 - w0) * 100f / N:F1}%, 기대 37) · 엔 +{g1 - g0}(기대 ≈{N * 0.37f * 0.4f * 100f:F0})";
    }
    static string SellUnit()
    {
        var r = RecruitsNow();
        if (r.Count == 0) return "❌ 회유 유닛 없음";
        var hud = Object.FindFirstObjectByType<GameHud>();
        int before = r.Count;
        hud.ExecuteSellOn(r[0].GetComponent<Selectable>());
        return $"[판매 버튼 경로] 회유 유닛 {before} → {RecruitsNow().Count}(기대 −1)";
    }
    static string Run정준영() => Setup("불멸_정준영");
    static string Onion()
    {
        var up = PlayerContext.Get(0).UnitUpgrades;
        var trait = unit.Data.trait;
        up.AddTraitPoints(3);
        bool spent = up.TrySpendTraitPoints(trait.costTraitPoints);
        if (spent) up.Unlock(trait);
        return $"[양파의 결집] 특성 {trait.costTraitPoints}pt {(spent ? "성공" : "실패")} 레벨 인덱스 {up.SkillLevelIndexFor(unit.Data)}";
    }

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
