using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 엄태웅 점검(10-06) — gameshot: call:ShopSlotProbe.Fund call:TaewoongProbe.Run
// 스킬 연결·칭호·trait · 「웅교교주」 6번 눌러 엔 소모·최대 5회 · 도박 성공률 가산(100%·0% 제외) · 강도높은트레이너 오라가 곁 아군 공격력에 닿는지.
static class TaewoongProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_엄태웅_AD.asset");
        var friend = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/안흔함_엄태웅.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        var other = spawner.Spawn(friend, lane != null ? lane.TakeSpawnPosition(friend) : Vector3.zero, 0);
        var sb = new StringBuilder();
        sb.AppendLine($"[엄태웅] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")) + $" · trait {(d.trait == null ? "없음" : d.trait.name)}");
        var ctx = PlayerContext.Get(0);
        var hud = Object.FindFirstObjectByType<GameHud>();
        var sel = go.GetComponent<Selectable>();
        int gold0 = ctx.GoldWallet.Gold;
        for (int i = 1; i <= 6; i++)
        {
            hud.ExecuteGambleBoostOn(sel);
            sb.AppendLine($"  눌러 {i}: 횟수 {ctx.GambleBoostCount} · +{ctx.GambleBoostPercent}%p · 엔 {gold0}→{ctx.GoldWallet.Gold}");
        }
        MethodInfo eff = typeof(GamblingShop).GetMethod("EffectiveSuccessChance", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (string n in new[] { "하급도박", "중급도박", "고급도박", "다른세계 도박", "고급 유닛 생성", "목재 구입" })
        {
            var opt = AssetDatabase.LoadAssetAtPath<GamblingOptionData>($"Assets/Data/Gambling/Gambling_{n}.asset");
            if (opt != null) sb.AppendLine($"  도박 {n}: 기본 {opt.successChancePercent}% → {eff.Invoke(null, new object[] { opt, ctx })}%");
        }
        lastAttacker = other.GetComponent<UnitAttacker>();
        sb.AppendLine($"[오라 전] 곁 안흔함_엄태웅 공격력% 보너스 {lastAttacker.PercentAttackPowerBonus:F2}");
        return sb.ToString();
    }
    static UnitAttacker lastAttacker;
    static string Aura() => lastAttacker == null ? "❌ Run 먼저" : $"[오라 후] 곁 안흔함_엄태웅 공격력% 보너스 {lastAttacker.PercentAttackPowerBonus:F2}(기대 0.25 — 반경 안일 때)";

    // ── 폭탄제조(목재강화) 실측: 적을 곁으로 데려와 누른다 ──
    static UnitIdentity bombUnit;
    static string BombRun()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_엄태웅_AD.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        bombUnit = go.GetComponent<UnitIdentity>();
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        var enemies = EnemyDummy.Active.Where(e => e != null && !e.IsDead).OrderBy(e => Vector3.Distance(e.transform.position, go.transform.position)).Take(8).ToList();
        int k = 0;
        foreach (EnemyDummy e in enemies)
        {
            hpField.SetValue(e, 1e8f);
            float a = k++ * Mathf.PI / 4f;
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 to = go.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (k <= 6 ? 40f : 300f);   // 6기는 폭탄 반경 안, 2기는 반경 밖
            if (ag != null) ag.Warp(to); else e.transform.position = to;
        }
        var ctx = PlayerContext.Get(0);
        var hud = Object.FindFirstObjectByType<GameHud>();
        var sel = go.GetComponent<Selectable>();
        var before = enemies.Select(e => e.Hp).ToArray();
        int wood0 = ctx.ResourceWallet.Get(ResourceType.Wood);
        var sb = new StringBuilder();
        sb.AppendLine($"[폭탄] 엄태웅 사거리 {go.GetComponent<UnitAttacker>().AttackRange:F0} · 적 {enemies.Count}기 · 목재 {wood0}");
        hud.ExecuteBombOn(sel);
        int wood1 = ctx.ResourceWallet.Get(ResourceType.Wood);
        sb.AppendLine($"  1발: 목재 {wood0}→{wood1} · 체력Δ " + string.Join(", ", enemies.Select((e, i) => $"{before[i] - e.Hp:F0}")));
        hud.ExecuteBombOn(sel);   // 0.5초 안 — 막혀야 한다
        sb.AppendLine($"  연타(즉시): 목재 {wood1}→{ctx.ResourceWallet.Get(ResourceType.Wood)}(같아야 함)");
        return sb.ToString();
    }

    // 0.6초 뒤 2발째 · 목재 0일 때 세 번째(목재·피해 안 변해야 함)
    static string BombAgain()
    {
        var ctx = PlayerContext.Get(0);
        var hud = Object.FindFirstObjectByType<GameHud>();
        var sel = bombUnit.GetComponent<Selectable>();
        var near = EnemyDummy.Active.Where(e => e != null && !e.IsDead).OrderBy(e => Vector3.Distance(e.transform.position, bombUnit.transform.position)).ToList();
        float hpBefore = near.Count > 0 ? near[0].Hp : 0f;
        int wood0 = ctx.ResourceWallet.Get(ResourceType.Wood);
        hud.ExecuteBombOn(sel);
        int wood1 = ctx.ResourceWallet.Get(ResourceType.Wood);
        var sb = new StringBuilder($"[폭탄 2발째] 목재 {wood0}→{wood1} · 가장 가까운 적 체력Δ {hpBefore - (near.Count > 0 ? near[0].Hp : 0f):F0}\n");
        ctx.ResourceWallet.TrySpend(ResourceType.Wood, ctx.ResourceWallet.Get(ResourceType.Wood));   // 목재 0
        float hp2 = near.Count > 0 ? near[0].Hp : 0f;
        hud.ExecuteBombOn(sel);
        sb.AppendLine($"  목재 0에서 누름: 목재 {ctx.ResourceWallet.Get(ResourceType.Wood)} · 체력Δ {hp2 - (near.Count > 0 ? near[0].Hp : 0f):F0}(0이어야 함)");
        return sb.ToString();
    }
}
