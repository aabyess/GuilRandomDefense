using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 김경현 점검(10-06) — gameshot: call:ShopSlotProbe.Fund wait:30 call:GyeonghyeonProbe.Run wait:6 call:GyeonghyeonProbe.Report call:GyeonghyeonProbe.Knife call:GyeonghyeonProbe.Sale call:GyeonghyeonProbe.Level2
static class GyeonghyeonProbe
{
    static UnitIdentity unit; static UnitAttacker atk;
    static System.Collections.Generic.List<EnemyDummy> normals;
    static int WispCount() => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("WispPrefab"));

    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_김경현_AP.asset");
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
            Vector3 to = go.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 50f;
            if (ag != null) { ag.enabled = false; }
            e.transform.position = to;
        }
        return $"[김경현] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")) + $" · 스플래시 {d.attackSplashRadius} · trait {(d.trait == null ? "없음" : d.trait.name + " " + d.trait.costTraitPoints + "pt idx" + d.trait.skillLevelUnlockIndex)} · 마나 {d.manaMax} · 근처 일반 적 {normals.Count}";
    }

    static string Report()
    {
        var inv = PlayerContext.Get(0).ItemInventory;
        return $"[몹삭제] 근처 적 사망 {normals.Count(e => e == null || e.IsDead)}/{normals.Count}(기대 1) · 인벤토리 {string.Join(",", inv.Items.Select(i => i.itemName))}(기대 노획물 1개)";
    }

    static string Knife()
    {
        var skill = unit.Data.skills.First(s => s.skillName.StartsWith("숙련된칼솜씨"));
        EnemyDummy t = normals.FirstOrDefault(e => e != null && !e.IsDead);
        if (t == null) return "❌ 살아 있는 적 없음";
        float before = t.Hp, max = t.MaxHp;
        bool ok = atk.TryCastActiveOn(skill, t, out string why);
        return $"[칼솜씨] 시전 {(ok ? "성공" : "실패 " + why)} · 체력 {before:F0} → {t.Hp:F0} (잃은 {before - t.Hp:F0} = 최대 {max:F0}의 {(before - t.Hp) / max * 100:F1}%, 기대 22%)";
    }

    static string Sale()
    {
        var ctx = PlayerContext.Get(0);
        var inv = ctx.ItemInventory;
        var loot = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/ItemData_L001_치킨.asset");
        foreach (ItemData i in inv.Items.ToList()) inv.Remove(i);
        int w0 = WispCount(), gold0 = ctx.GoldWallet.Gold, wood0 = ctx.ResourceWallet.Get(ResourceType.Wood);
        const int N = 400;
        for (int i = 0; i < N; i++) { inv.Add(loot); RewardDistributor.Instance.UseItem(ctx, ItemUseKind.LootSale); }
        int dw = WispCount() - w0, dg = ctx.GoldWallet.Gold - gold0, dwood = ctx.ResourceWallet.Get(ResourceType.Wood) - wood0;
        return $"[판매 {N}회] 위습 +{dw}({dw * 100f / N:F1}%, 기대 37) · 엔 +{dg}(기대 ≈{N * 0.37f * 0.4f * 100f:F0}) · 목재 +{dwood}(기대 ≈{N * 0.37f * 0.4f:F0}, 상한 클램프 가능)";
    }

    static string Level2()
    {
        var up = PlayerContext.Get(0).UnitUpgrades;
        var inv = PlayerContext.Get(0).ItemInventory;
        var mats = unit.Data.skills.First(s => s.skillName.StartsWith("재료확보"));
        var grant = typeof(UnitAttacker).GetMethod("GrantLoot", BindingFlags.NonPublic | BindingFlags.Instance);
        float Avg(SkillEffect e) { int sum = 0; for (int i = 0; i < 400; i++) { foreach (ItemData x in inv.Items.ToList()) inv.Remove(x); grant.Invoke(atk, new object[] { e }); sum += inv.Items.Count; } return sum / 400f; }
        float a1 = Avg(mats.levels[0].effects[1]);
        up.AddTraitPoints(2);
        var trait = unit.Data.trait;
        bool spent = up.TrySpendTraitPoints(trait.costTraitPoints);
        if (spent) up.Unlock(trait);
        float a2 = Avg(mats.levels[1].effects[1]);
        foreach (ItemData x in inv.Items.ToList()) inv.Remove(x);
        return $"[노획물 수] 레벨1 평균 {a1:F2}(기대 1.00) · 특성 {(spent ? "성공" : "실패")} 레벨 인덱스 {up.SkillLevelIndexFor(unit.Data)} · 레벨2 평균 {a2:F2}(기대 1.50)";
    }
}
