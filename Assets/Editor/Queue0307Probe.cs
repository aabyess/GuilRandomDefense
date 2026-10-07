using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>10-07 큐 ③⑤⑥ 점검 — gameshot call:Queue0307Probe.Multi / StoryShow / StoryShowOld / RelicTest.</summary>
static class Queue0307Probe
{
    static UnitIdentity Spawn(string name, Vector3 pos)
    {
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        return Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, pos, 0).GetComponent<UnitIdentity>();
    }

    static string Multi()
    {
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        string[] names = { "흔함_박민수", "안흔함_박민수", "전설적인_최상호", "초월_임채민_AP", "희귀함_박민수", "불멸_고도현", "흔함_최상호" };
        var units = names.Select((n, i) => Spawn(n, c + new Vector3(i * 25f, 0f, 0f))).ToList();
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(units[0].GetComponent<Selectable>());
        var add = typeof(SelectionManager).GetMethod("AddToSelection", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 1; i < units.Count; i++) add.Invoke(sel, new object[] { units[i].GetComponent<Selectable>() });
        return $"선택 {sel.Selected.Count}기(고른 순서: {string.Join(", ", names)})";
    }

    static string StoryShow() => Story(1f);
    static string StoryShowOld() => Story(1f / 0.7f);
    static string Story(float extra)
    {
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_Story06_구일고등학교.asset");
        Transform spawn = GameObject.Find("스토리_등장지점").transform;
        GameObject go = Object.Instantiate(data.prefab, spawn.position, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        go.transform.GetChild(0).localScale *= extra;
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        cam.MoveTo(spawn.position);
        return $"스토리6 건물 스폰 · 배율 {extra:0.00} · 위치 {spawn.position:F0}";
    }

    static string RelicTest()
    {
        var sb = new StringBuilder();
        var rd = Object.FindFirstObjectByType<RewardDistributor>();
        PlayerContext ctx = PlayerContext.Get(0);
        var inv = ctx.ItemInventory;
        foreach (ItemData i in inv.Items.ToList()) inv.Remove(i);
        // ① 드랍 풀: 안 가진 유물만 — 1000번 뽑아 중복·널 센다
        var got = new List<string>();
        int dup = 0;
        for (int k = 0; k < 6; k++)
        {
            ItemData r = rd.PickMissingRelic(ctx);
            if (r == null) { got.Add("(없음)"); break; }
            if (inv.Items.Contains(r)) dup++;
            inv.Add(r); got.Add(r.itemName);
        }
        sb.Append($"\n   유물 드랍 순차: {string.Join(" → ", got)} · 중복 {dup}회 · 다 가진 뒤 PickMissingRelic {(rd.PickMissingRelic(ctx) == null ? "null(드랍 없음)" : "❌ 나옴")}");
        // ② 드랍 1000번 강제(0.5% 대신 확률 1): 가진 상태에서 0건이어야
        int extra = 0;
        for (int k = 0; k < 1000; k++) if (rd.PickMissingRelic(ctx) != null) extra++;
        sb.Append($"\n   다 가진 뒤 1000번 시도 → 새로 나온 유물 {extra}개");
        // ③ 스토리 보상 중복: 거울을 이미 가진 상태에서 GrantItemDrop 호출
        var grant = typeof(RewardDistributor).GetMethod("GrantItemDrop", BindingFlags.NonPublic | BindingFlags.Instance);
        var mirror = inv.Items.First(i => i.name.Contains("부서진손거울"));
        int g0 = ctx.GoldWallet.Gold, n0 = inv.Items.Count;
        grant.Invoke(rd, new object[] { ctx, 1f, new List<EnemyItemDrop> { new EnemyItemDrop { item = mirror, weight = 1f, message = "" } } });
        sb.Append($"\n   스토리 거울 보상(다 가진 상태): 유물 수 {n0}→{inv.Items.Count} · 금화 {g0}→{ctx.GoldWallet.Gold}(+3000이면 대체)");
        // 하나 비우고 다시: 안 가진 다른 유물로 대체되는지
        var paper = inv.Items.First(i => i.name.Contains("종이비행기"));
        inv.Remove(paper);
        grant.Invoke(rd, new object[] { ctx, 1f, new List<EnemyItemDrop> { new EnemyItemDrop { item = mirror, weight = 1f, message = "" } } });
        sb.Append($"\n   종이비행기만 비운 뒤 거울 보상: 지금 가진 것 {string.Join(", ", inv.Items.Select(i => i.itemName))}");
        return sb.ToString();
    }
}
