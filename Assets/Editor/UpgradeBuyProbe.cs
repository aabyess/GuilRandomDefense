using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 강화소 구매 점검(10-06 긴급 「희귀함 업글이 안 된다」) — gameshot: call:ShopSlotProbe.Fund wait:20 call:UpgradeBuyProbe.Setup wait:1 call:UpgradeBuyProbe.BuyAll
// 등급 대표 유닛을 세운 뒤 강화소 칸을 하나씩 눌러(TryUse = 칸을 클릭한 것과 같은 함수) 그 칸이 올리는 등급과 엔·레벨·공속·공격력 변화를 표로 낸다.
static class UpgradeBuyProbe
{
    static readonly string[] Reps = { "흔함_강재규", "안흔함_김경현", "특별함_유재헌", "희귀함_김경현", "히든_한나웅", "전설적인_김건", "특수함_임재현", "제한_김민규", "초월_김만경_AD", "불멸_김용태", "랜덤_야사카_카나코" };
    static readonly List<UnitAttacker> units = new List<UnitAttacker>();
    static UnitUpgradeShop shop;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        units.Clear();
        foreach (string n in Reps)
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            if (d == null) continue;
            var go = spawner.Spawn(d, lane.TakeSpawnPosition(d), 0);
            if (go != null) units.Add(go.GetComponent<UnitAttacker>());
        }
        shop = Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.SlotCount >= 8 && s.name.Contains("Lane1_유닛강화소"));
        return $"세움: 유닛 {units.Count}기 · 강화소 {(shop != null ? shop.name + " 칸 " + shop.SlotCount : "없음")}";
    }

    static string Snap() => string.Join(" / ", units.Select(u => $"{u.GetComponent<UnitIdentity>().Data.grade}:{u.CurrentAttackSpeedMultiplier:F2}×·{u.AttackDamage:F0}"));

    static string BuyAll()
    {
        if (shop == null) return "❌ Setup 먼저";
        var ctx = PlayerContext.Get(0);
        var sb = new StringBuilder();
        for (int i = 0; i < shop.SlotCount; i++)
        {
            string label = shop.GetSlotView(i).label.Replace("\n", " ");
            int gold0 = ctx.GoldWallet.Gold;
            string before = Snap();
            bool ok = shop.TryUse(i, default, out string why);
            string after = Snap();
            var bAr = before.Split(new[] { " / " }, System.StringSplitOptions.None); var aAr = after.Split(new[] { " / " }, System.StringSplitOptions.None);
            var changed = new List<string>();
            for (int k = 0; k < bAr.Length; k++) if (bAr[k] != aAr[k]) changed.Add($"{bAr[k]}→{aAr[k]}");
            sb.AppendLine($"  칸 {i} 「{label}」 구매 {(ok ? "성공" : "실패 " + why)} · 엔 {gold0}→{ctx.GoldWallet.Gold} · 변한 유닛: {(changed.Count == 0 ? "없음" : string.Join(" | ", changed))} · 칸 글자 지금 「{shop.GetSlotView(i).label.Replace("\n", " ")}」");
        }
        return sb.ToString();
    }

    // ── 실전투 비교: 등급 대표마다 자기 표적(체력 3천만, 이동 끔)을 옆에 세우고 N초 동안 넣은 피해·평타 수를 잰다 ──
    static readonly List<(UnitAttacker unit, EnemyDummy dummy)> pairs = new List<(UnitAttacker, EnemyDummy)>();
    static readonly Dictionary<UnitAttacker, (float hp, int hits)> mark = new Dictionary<UnitAttacker, (float, int)>();
    static EnemyData dummyData;

    static string Setup2()
    {
        string r = Setup();
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        pairs.Clear();
        int i = 0;
        foreach (UnitAttacker u in units)
        {
            Vector3 basePos = LaneMarker.Get(0).LaneCenter + new Vector3(-400f + i * 80f, 0f, 150f);
            var agent = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(basePos); else u.transform.position = basePos;
            GameObject go = Object.Instantiate(dummyData.prefab, basePos + new Vector3(0f, 0f, 18f), Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            var e = go.GetComponent<EnemyDummy>(); e.Initialize(dummyData, 1f); e.SetLane(0);
            hpField.SetValue(e, 3e7f);
            pairs.Add((u, e)); i++;
        }
        return r + $" · 표적 {pairs.Count}기";
    }

    static string Mark()
    {
        mark.Clear();
        foreach (var p in pairs) mark[p.unit] = (p.dummy != null ? p.dummy.Hp : 0f, p.unit.BasicHitCount);
        return "표식";
    }

    static string Report(string label)
    {
        var sb = new StringBuilder($"[{label}]\n");
        foreach (var p in pairs)
        {
            var (hp0, hits0) = mark[p.unit];
            float dealt = hp0 - (p.dummy != null ? p.dummy.Hp : 0f);
            int hits = p.unit.BasicHitCount - hits0;
            sb.AppendLine($"  {p.unit.GetComponent<UnitIdentity>().Data.grade,-12} 공격력 {p.unit.AttackDamage,9:F0} · 공속배율 {p.unit.CurrentAttackSpeedMultiplier:F3} · 간격 {p.unit.AttackInterval:F3}초 · 구간 평타 {hits} · 구간 피해 {dealt:N0}");
        }
        return sb.ToString();
    }
    static string ReportA() => Report("업글 0");
    static string ReportB() => Report("업글 뒤");

    static string BuyN(int n)
    {
        if (shop == null) return "❌ Setup 먼저";
        var ctx = PlayerContext.Get(0); int g0 = ctx.GoldWallet.Gold; int bought = 0;
        for (int i = 0; i < shop.SlotCount; i++) for (int k = 0; k < n; k++) if (shop.TryUse(i, default, out string _)) bought++;
        return $"각 칸 {n}번씩 — 성공 {bought}번 · 엔 {g0} → {ctx.GoldWallet.Gold}";
    }
    static string Buy5() => BuyN(5);

    // 가격 표: 칸마다 1렙·중간·마지막 가격(트랙 데이터) + 칸 툴팁 한 칸 + 그림용으로 툴팁을 띄운다
    static string PriceTable()
    {
        if (shop == null) shop = Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.SlotCount >= 8 && s.name.Contains("Lane1_유닛강화소"));
        var so = new SerializedObject(shop); var tp = so.FindProperty("tracks");
        var sb = new StringBuilder();
        for (int i = 0; i < tp.arraySize; i++)
        {
            var t = (UnitUpgradeTrackData)tp.GetArrayElementAtIndex(i).objectReferenceValue;
            int last = t.maxLevel, mid = (last + 1) / 2;
            sb.AppendLine($"  {t.trackName,-14} 최대 {last}렙 · 1렙 {t.CostForLevel(0)} · 중간({mid}렙) {t.CostForLevel(mid - 1)} · 마지막({last}렙) {t.CostForLevel(last - 1)}");
        }
        return sb.ToString();
    }
    static string ShowTooltip()
    {
        if (shop == null) shop = Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.SlotCount >= 8 && s.name.Contains("Lane1_유닛강화소"));
        Object.FindFirstObjectByType<SelectionManager>().SelectOnly(shop.GetComponent<Selectable>());
        return "선택";
    }
    static string Hover()
    {
        var hud = Object.FindFirstObjectByType<GameHud>();
        var m = typeof(GameHud).GetMethod("ShowHoveredTooltipNow", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(hud, new object[] { 2 });   // 화면 칸 2 = E (희귀함)
        return "툴팁: " + shop.GetSlotTooltip(2).Replace("\n", " | ");
    }
}
