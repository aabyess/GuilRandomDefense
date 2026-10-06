using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>제한됨 마무리 실측(10-06 구현담당3) — gameshot: call:LimitedProbe2.Setup wait:7 call:LimitedProbe2.Report (신 모드).</summary>
public static class LimitedProbe2
{
    static UnitData U(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static UnitAttacker beop, minkyu;
    static readonly MethodInfo CastLevel = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);

    static EnemyDummy Make(Vector3 at, float lost = 0f)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(0);
        typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, d.MaxHp * (1f - lost));
        return d;
    }

    public static string Setup()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        beop = spawner.Spawn(U("제한_전법규"), LaneMarker.Get(0).TakeSpawnPosition(U("제한_전법규")), 0).GetComponent<UnitAttacker>();
        minkyu = spawner.Spawn(U("제한_김민규"), LaneMarker.Get(0).TakeSpawnPosition(U("제한_김민규")), 0).GetComponent<UnitAttacker>();
        return "   전법규 + 김민규 세움(분신은 5초 틱 뒤)";
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        PlayerContext ctx = PlayerContext.Local;

        // 분신
        var clones = UnitIdentity.Active.Where(u => u != null && u.IsSummon && u.Data != null && u.Data.name == "Summon_전법규_분신").ToList();
        sb.AppendLine($"   전법규 분신: {clones.Count}기(기대 1) · 스킬 {string.Join("/", beop.GetComponent<UnitIdentity>().Data.skills.Select(s => s.skillName.Split('—')[0].Trim()))}");
        if (clones.Count > 0)
        {
            var ca = clones[0].GetComponent<UnitAttacker>();
            SkillData fin = clones[0].Data.skills[0];
            EnemyDummy t = Make(ca.transform.position + Vector3.forward * 30f, 0.5f);
            float lost = t.MaxHp - t.Hp, h0 = t.Hp;
            CastLevel.Invoke(ca, new object[] { fin.levels[0], 0f, t, 0f });
            sb.AppendLine($"     분신 마나 스킬: 표적 잃은 체력의 {(h0 - t.Hp) / lost * 100f:F2}%(기대 4%×상성) · 분신 공격력 {clones[0].Data.attackPower:F0}");
        }

        // 분실된지갑 드랍
        var rewards = Object.FindFirstObjectByType<RewardDistributor>();
        EnemyData normal = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        int before = ctx.ItemInventory.Items.Count(i => i != null && i.itemName == "분실된지갑");
        for (int i = 0; i < 4000; i++) rewards.GrantKillReward(normal, 0, 49, 0, null, 0f);
        int walletCount = ctx.ItemInventory.Items.Count(i => i != null && i.itemName == "분실된지갑");
        sb.AppendLine($"   분실된지갑: 일반 적 4000킬 → {walletCount - before}개(기대 약 20 = 0.5%, 인벤토리 {ItemInventory.MaxItems}칸 한도)");

        // 박성호 식
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (var u in UnitIdentity.Active.Where(x => x != null && x.OwnerId == 0 && !x.IsSummon && x.Data != null && (x.Data.name.StartsWith("전설") || x.Data.name.StartsWith("희귀"))).ToList()) u.Consume();
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/제한_박성호.asset");
        ctx.GoldWallet.Add(50000); ctx.ResourceWallet.Add(ResourceType.Wood, 20);
        foreach (string n in new[] { "전설적인_박성호", "전설적인_박병규", "희귀함_강보명" }) spawner.Spawn(U(n), LaneMarker.Get(0).TakeSpawnPosition(U(n)), 0);
        bool ok = Object.FindFirstObjectByType<CombineSystem>().TryCombine(recipe);
        int after = ctx.ItemInventory.Items.Count(i => i != null && i.itemName == "분실된지갑");
        sb.AppendLine($"   박성호 식(지갑 포함): TryCombine {ok} · 지갑 {walletCount} → {after}(기대 −1)");

        // 보물위치공개
        var hunt = TreasureHunt.Instance;
        if (hunt == null) { sb.AppendLine("   ❌ TreasureHunt 없음"); return sb.ToString(); }
        hunt.OnRoundStarted(10);   // 상자 7개를 숨긴다(씬 구역 안)
        SkillData reveal = minkyu.GetComponent<UnitIdentity>().Data.skills.First(s => s.skillName.StartsWith("보물위치공개"));
        // 김민규를 상자 하나 근처로 옮긴 뒤 시전 — 상자 자리를 모르니 가장 가까운 구역 중심 근처로
        var chestsField = typeof(TreasureHunt).GetField("chests", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (System.Collections.Generic.List<Vector2>)chestsField.GetValue(hunt);
        if (list.Count > 0) { var c = list[0]; if (minkyu.TryGetComponent(out UnityEngine.AI.NavMeshAgent ag)) ag.Warp(new Vector3(c.x, minkyu.transform.position.y, c.y)); else minkyu.transform.position = new Vector3(c.x, minkyu.transform.position.y, c.y); }
        CastLevel.Invoke(minkyu, new object[] { reveal.levels[0], reveal.levels[0].WorldRange, null, 0f });
        int beams = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "보물빛기둥");
        sb.AppendLine($"   보물위치공개: 상자 {list.Count}개 중 반경 {reveal.levels[0].WorldRange:F0}(월드) 안 {minkyu.LastRevealCount}곳 · 빛기둥 {beams}개 · 미니맵 점 {hunt.RevealMarks.Count}개 (기대 ≥1, 같은 수) · 상자는 안 열림 {list.Count}개 그대로");
        return sb.ToString();
    }
}
