using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 영원함 서민성 실측(10-06 구현담당3) — gameshot: call:EternalMinseongProbe.Report (신 모드).
/// 강화(엔·위습 소모·레벨 1→3)·해금 게이트(레벨 5/6/11/16 경계)·그의스킬샷(지점 반경 안 적 피해 +5%/레벨)·막타충(스킬 처치 후 스킬 피해 ×1.2)·입력말·재료.
/// </summary>
public static class EternalMinseongProbe
{
    static EnemyDummy Make(Vector3 at, float hp = 1e3f)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, hp); d.SetLane(-1);
        return d;
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_서민성.asset");
        var a = Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        PlayerContext ctx = PlayerContext.Local;
        ctx.GoldWallet.Add(100000);
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 마나 {d.manaMax} · 강화 최대 {d.enhanceMaxLevel} ({d.enhanceGoldCost}엔 + 위습 {d.enhanceWispCount})");

        int wispBefore = Wisp.Active.Count(w => w != null && !w.IsConsumed), gold0 = ctx.GoldWallet.Gold;
        bool ok1 = a.TryEnhance(out string r1);
        bool ok2 = a.TryEnhance(out string r2);
        int wispAfter = Wisp.Active.Count(w => w != null && !w.IsConsumed);
        sb.AppendLine($"   강화 2회: {(ok1 ? "성공" : r1)} / {(ok2 ? "성공" : r2)} → 레벨 {a.EnhanceLevel} · 엔 {gold0} → {ctx.GoldWallet.Gold}(기대 −10,000) · 위습 {wispBefore} → {wispAfter}(기대 −2)");

        var gate = typeof(UnitAttacker).GetMethod("PassesBuffGate", BindingFlags.NonPublic | BindingFlags.Instance);
        var setLevel = typeof(UnitAttacker).GetProperty("EnhanceLevel").GetSetMethod(true);
        var results = new StringBuilder();
        foreach (string key in new[] { "도망치지마", "가게두어라 — 발동 이감", "고혈 — 마지막" })
        {
            SkillData s = d.skills.First(x => x.skillName.StartsWith(key));
            results.Append($"[{key.Split(' ')[0]}:");
            foreach (int lv in new[] { 5, 6, 10, 11, 15, 16 })
            {
                setLevel.Invoke(a, new object[] { lv });
                results.Append($" {lv}={((bool)gate.Invoke(a, new object[] { s.levels[0], null }) ? "O" : "x")}");
            }
            results.Append("] ");
        }
        sb.AppendLine($"   해금 게이트(O=발동 가능): {results}(기대 6강·11강·16강부터 O)");

        SkillData shot = d.skills.First(x => x.skillName.StartsWith("그의스킬샷"));
        Vector3 c = LaneMarker.Get(0).LaneCenter + Vector3.back * 70f;
        var dmg = new System.Collections.Generic.List<string>();
        float[] v = new float[2];
        for (int i = 0; i < 2; i++)
        {
            setLevel.Invoke(a, new object[] { i == 0 ? 0 : 10 });
            a.GetType().GetField("activeReadyAt", BindingFlags.NonPublic | BindingFlags.Instance);
            EnemyDummy t = Make(c), outside = Make(c + Vector3.forward * shot.levels[0].WorldRange * 1.6f);
            float hp0 = t.Hp, ob = outside.Hp;
            // 쿨 초기화: 런타임 상태의 activeReadyAt을 직접 못 만져서 땅 시전은 반복마다 다른 스킬 상태를 쓰지 않도록 CastSkillLevel을 center override로 직접 부른다.
            typeof(UnitAttacker).GetField("castCenterOverride", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, (Vector3?)c);
            typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { shot.levels[0], shot.levels[0].WorldRange, null, 0f });
            typeof(UnitAttacker).GetField("castCenterOverride", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, null);
            v[i] = hp0 - t.Hp;
            dmg.Add($"강화 {(i == 0 ? 0 : 10)}: 표적 {v[i]:F0} · 밖 {ob - outside.Hp:F0}");
            Object.Destroy(t.gameObject); Object.Destroy(outside.gameObject);
        }
        sb.AppendLine($"   그의스킬샷(지점 중심): {string.Join(" | ", dmg)} → 비 ×{v[1] / v[0]:F2}(기대 ×1.50)");

        // 막타충: 스킬 피해로 일반 적 처치(체력 1) → 직후 스킬 피해 비교
        setLevel.Invoke(a, new object[] { 0 });
        EnemyDummy weak = Make(c, 1e-9f);
        typeof(UnitAttacker).GetField("castCenterOverride", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, (Vector3?)c);
        EnemyDummy big1 = Make(c + Vector3.right * 2f); float b1 = big1.Hp;
        var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        // 처치 이후 효과를 따로 재려고 먼저 약한 적만 있는 상태에서 한 번, 그다음 큰 적에 한 번
        Object.Destroy(big1.gameObject);
        cast.Invoke(a, new object[] { shot.levels[0], shot.levels[0].WorldRange, null, 0f });   // 약한 적 처치 → 막타 버프
        EnemyDummy big = Make(c); float bh = big.Hp;
        cast.Invoke(a, new object[] { shot.levels[0], shot.levels[0].WorldRange, null, 0f });
        float afterKill = bh - big.Hp;
        typeof(UnitAttacker).GetField("afterKillUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, 0f);
        EnemyDummy big2 = Make(c); float bh2 = big2.Hp;
        cast.Invoke(a, new object[] { shot.levels[0], shot.levels[0].WorldRange, null, 0f });
        float normal = bh2 - big2.Hp;
        typeof(UnitAttacker).GetField("castCenterOverride", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, null);
        sb.AppendLine($"   막타충: 처치 직후 스킬 피해 {afterKill:F0} vs 평소 {normal:F0} = ×{afterKill / normal:F2}(기대 ×1.20)");

        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_서민성.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }

    /// <summary>서민성을 세워 고른다 — 명령 카드에 「강화」 칸·그의스킬샷 액티브 칸이 뜨는지 사진용(call:EternalMinseongProbe.Show wait:3).</summary>
    public static string Show()
    {
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_서민성.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0);
        Object.FindFirstObjectByType<SelectionManager>().SelectOnly(go.GetComponent<Selectable>());
        return "   서민성을 세우고 골랐다";
    }
}
