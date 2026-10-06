using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 유재헌 실측(10-06 구현담당3) — gameshot: call:JaeheonProbe.Setup wait:7 call:JaeheonProbe.Report call:JaeheonProbe.KillOne wait:7 call:JaeheonProbe.Report2 (신 모드).
/// 소환수 2기 상시(하나 죽이면 5초 안에 다시) · 돈벌이 GoldPlus +0.2 · 현상수배(내 레인 적만 이감 0.82, 처치 보상 +18%) · 토토 600회 분포 · 순간이동 · 식·입력말.
/// </summary>
public static class JaeheonProbe
{
    static UnitAttacker a;
    static EnemyDummy mine, other;

    static EnemyDummy Make(Vector3 at, int lane)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(lane);
        return d;
    }

    public static string Setup()
    {
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_유재헌_ADAP.asset");
        a = Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        mine = Make(c + Vector3.back * 60f, 0);
        other = Make(c + Vector3.back * 80f, 1);
        return "   유재헌 + 내 레인 적 1 + 남의 레인(1번) 적 1 세움";
    }

    static int Minions() => UnitIdentity.Active.Count(u => u != null && u.Data != null && u.Data.name.StartsWith("Summon_유재헌_수하") && u.IsSummon);

    public static string Report()
    {
        var sb = new StringBuilder();
        UnitData d = a.GetComponent<UnitIdentity>().Data;
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · trait {(d.trait == null ? "없음" : d.trait.name)}");
        sb.AppendLine($"   ① 소환수: {Minions()}기(기대 2)");
        PlayerContext ctx = PlayerContext.Local;
        sb.AppendLine($"   ② 돈벌이: GoldPlus {ctx.GoldWallet.GoldPlus:F2}(기대 0.20 이상 — 기본값 포함)");
        sb.AppendLine($"   ③ 현상수배: 내 레인 적 남는 속도 {mine.EffectiveSlowMultiplier:F2}(기대 0.82) · 남의 레인 적 {other.EffectiveSlowMultiplier:F2}(기대 1.00)");

        // 처치 보상: 이감 18%일 때 골드 비율(25% 확률 지급이라 많이 굴려 평균)
        var rewards = Object.FindFirstObjectByType<RewardDistributor>();
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        long g0 = 0, g1 = 0;
        for (int i = 0; i < 2000; i++) { int before = ctx.GoldWallet.Gold; rewards.GrantKillReward(data, 0, 49, 0, null, 0f); g0 += ctx.GoldWallet.Gold - before; }
        for (int i = 0; i < 2000; i++) { int before = ctx.GoldWallet.Gold; rewards.GrantKillReward(data, 0, 49, 0, null, 18f); g1 += ctx.GoldWallet.Gold - before; }
        sb.AppendLine($"   처치 골드 2000회 합: 이감 0% {g0} vs 이감 18% {g1} = ×{(float)g1 / Mathf.Max(1, g0):F2}(기대 ×1.18)");

        // 토토
        var hud = Object.FindFirstObjectByType<GameHud>();
        var sel = a.GetComponent<Selectable>();
        ctx.GoldWallet.Add(2000000);
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        int gold0 = ctx.GoldWallet.Gold;
        for (int i = 0; i < 600; i++) { hud.ExecuteTotoOn(sel); string r = hud.LastTotoResult ?? "?"; counts[r] = counts.TryGetValue(r, out int n) ? n + 1 : 1; }
        sb.AppendLine($"   토토 600회: {string.Join(" · ", counts.Select(kv => kv.Key + " " + kv.Value))} (기대 성공 33% = 198, 각 1/3 ≈ 66) · 엔 순변화 {ctx.GoldWallet.Gold - gold0}");

        // 순간이동
        SkillData tp = d.skills.First(s => s.skillName.StartsWith("도망자의삶"));
        Vector3 start = a.transform.position, dest = LaneMarker.Get(0).LaneCenter;
        bool ok = a.TryCastActiveAtPoint(tp, dest, out string why);
        sb.AppendLine($"   ④ 순간이동: {(ok ? "성공" : "실패 " + why)} · {Vector3.Distance(start, a.transform.position):F0} 이동");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/초월_유재헌_ADAP.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }

    public static string KillOne()
    {
        var m = UnitIdentity.Active.FirstOrDefault(u => u != null && u.Data != null && u.Data.name.StartsWith("Summon_유재헌_수하") && u.IsSummon);
        if (m == null) return "   죽일 수하 없음";
        Object.Destroy(m.gameObject);
        return "   수하 한 기를 없앴다(5초 안에 다시 나와야 함)";
    }

    public static string Report2() => $"   재소환 뒤 소환수 {Minions()}기(기대 2)";
}
