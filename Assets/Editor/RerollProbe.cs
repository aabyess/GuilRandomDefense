using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 희귀함 리롤 버그 실측(10-07 구현담당3) — gameshot call:RerollProbe.Run (플레이 중).
/// 고급도박·고급 유닛 생성을 여러 번 돌려 나온 유닛의 등급별 「리롤 능력 있음」 수를 세고,
/// 특별함에 능력을 강제로 붙여 TryCast를 불러 거부되는지(목재 불변)를 본다.
/// </summary>
public static class RerollProbe
{
    public static string Run()
    {
        var sb = new StringBuilder();
        GamblingShop shop = Object.FindObjectsByType<GamblingShop>(FindObjectsSortMode.None).FirstOrDefault();
        PlayerContext ctx = PlayerContext.Get(0);
        if (shop == null || ctx == null) return "❌ GamblingShop/PlayerContext 없음";
        var seen = new HashSet<UnitIdentity>(Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None));
        var tally = new Dictionary<string, int[]>();   // 등급 → [개수, 능력 있음]
        foreach (string optName in new[] { "고급도박", "고급 유닛 생성" })
        {
            GamblingOptionData opt = UnityEditor.AssetDatabase.LoadAssetAtPath<GamblingOptionData>($"Assets/Data/Gambling/Gambling_{optName}.asset");
            int ok = 0;
            for (int i = 0; i < 150; i++)
            {
                ctx.GoldWallet.ApplyReplicated(ctx.GoldWallet.Gold + 20000); ctx.ResourceWallet.Add(ResourceType.Wood, 10);
                ctx.ResourceWallet.Add(ResourceType.LuckyToken, 5);
                if (!shop.TryRoll(opt, out string why)) { if (i == 0) sb.AppendLine($"   {optName} 첫 시도 실패: {why}"); continue; }
                ok++;
                foreach (UnitIdentity u in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None))
                {
                    if (!seen.Add(u) || u.Data == null) continue;
                    string key = $"{optName} → {u.Data.grade}";
                    if (!tally.TryGetValue(key, out int[] c)) tally[key] = c = new int[2];
                    c[0]++; if (u.GetComponent<UniqueRerollAbility>() != null) c[1]++;
                }
            }
            sb.AppendLine($"   {optName}: 성공 {ok}회");
        }
        foreach (var kv in tally.OrderBy(k => k.Key)) sb.AppendLine($"   {kv.Key}: {kv.Value[0]}기 중 리롤 능력 {kv.Value[1]}");

        // 로스터 전수 — 도박이 붙이는 호출(Attach)을 등급별로: 희귀함 전부 붙음, 나머지 전부 안 붙음.
        var dataAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UniqueRerollAbilityData>("Assets/Data/UniqueRerollAbility_희귀함리롤.asset");
        var census = new Dictionary<UnitGrade, int[]>();
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (string g in UnityEditor.AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            UnitData ud = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (ud == null || ud.prefab == null) continue;
            GameObject inst = spawner.Spawn(ud, new Vector3(0, -500, 0), 0);
            if (inst == null) continue;
            if (!census.TryGetValue(ud.grade, out int[] c)) census[ud.grade] = c = new int[2];
            c[0]++; if (UniqueRerollAbility.Attach(inst, dataAsset, spawner) != null) c[1]++;
            Object.Destroy(inst);
        }
        foreach (var kv in census.OrderBy(k => (int)k.Key)) sb.AppendLine($"   전수 {kv.Key}: {kv.Value[0]}종 중 Attach 성공 {kv.Value[1]}");

        // 강제 호출 — 특별함에 능력을 직접 붙여 TryCast.
        UnitData special = UnityEditor.AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).First(d => d.grade == UnitGrade.Special);
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(special, LaneMarker.Get(0).TakeSpawnPosition(special), 0);
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UniqueRerollAbilityData>("Assets/Data/UniqueRerollAbility_희귀함리롤.asset");
        sb.AppendLine($"   특별함({special.unitName}) Attach → {(UniqueRerollAbility.Attach(go, data, null) == null ? "거부(null)" : "붙음 ❌")}");
        UniqueRerollAbility forced = go.AddComponent<UniqueRerollAbility>();
        typeof(UniqueRerollAbility).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(forced, data);
        int woodBefore = ctx.ResourceWallet.Get(ResourceType.Wood);
        bool r = forced.TryCast(out string msg);
        sb.AppendLine($"   특별함 강제 TryCast → {r} 「{msg}」 목재 {woodBefore}→{ctx.ResourceWallet.Get(ResourceType.Wood)} 유닛 생존 {(go != null)}");
        Object.Destroy(go);
        return sb.ToString();
    }
}
