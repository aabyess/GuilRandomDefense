using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 임장혁 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:JanghyukProbe.Setup   초월 임장혁 + 흔함 박민수 + 초월 노태현(디버프 오라 시전자)을 우리에 세운다
///   call:JanghyukProbe.Near    박민수·노태현을 임장혁 곁(수평 60)으로 Warp
///   call:JanghyukProbe.Report  임장혁·박민수·노태현의 공격력/공속 배율/스킬 피해 오라/이속/받는 디버프 개수/마나
///   call:JanghyukProbe.Trait   특성 레벨 2 효과를 시뮬레이션(이간질 레벨 2의 효과 0) — 전/후 박민수 공격력 비교용
/// </summary>
public static class JanghyukProbe
{
    static UnitAttacker Find(string asset) => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == asset);

    static void Spawn(string name, StringBuilder sb)
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {name} → {(go != null ? go.name : "null")}");
    }

    public static string Setup() { StringBuilder sb = new StringBuilder(); Spawn("초월_임장혁_AD", sb); Spawn("흔함_박민수", sb); Spawn("초월_노태현_AP", sb); return sb.ToString(); }

    public static string Near()
    {
        UnitAttacker j = Find("초월_임장혁_AD");
        int i = 0;
        foreach (string n in new[] { "흔함_박민수", "초월_노태현_AP" })
        {
            UnitAttacker a = Find(n);
            if (a != null && a.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.Warp(j.transform.position + new Vector3(60f, 0f, 40f * i));
            i++;
        }
        return "   박민수·노태현을 임장혁 곁으로 Warp";
    }

    static float F(object o, string prop) => (float)o.GetType().GetProperty(prop, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(o);
    static float M(object o, string method, params object[] args) => (float)o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(o, args);

    static string Line(string label, UnitAttacker a)
    {
        if (a == null) return $"   {label}: 없음\n";
        UnityEngine.AI.NavMeshAgent agent = a.GetComponent<UnityEngine.AI.NavMeshAgent>();
        float skillBonus = (float)typeof(UnitAttacker).GetMethod("AuraBonusTotal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { SkillEffectKind.AllySkillDamageBonus, true });
        return $"   {label}: 공격력 {a.AttackDamage:F0} · 공속 배율 {a.CurrentAttackSpeedMultiplier:F3} · 스킬 피해 오라 ×{skillBonus:F2} · 이속 {(agent != null ? agent.speed.ToString("F1") : "-")} · 받는 아군발 디버프 {a.CountAllyDebuffs()}개\n";
    }

    public static string Report()
    {
        UnitAttacker j = Find("초월_임장혁_AD");
        if (j == null) return "❌ 임장혁 없음";
        UnitData d = j.GetComponent<UnitIdentity>().Data;
        StringBuilder sb = new StringBuilder($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "·" + (s.skillName.Contains('—') ? s.skillName.Split('—')[1].Trim().Split('(')[0].Trim() : "")))}\n");
        sb.Append(Line("임장혁(자기)", j)).Append(Line("박민수(아군)", Find("흔함_박민수"))).Append(Line("노태현(아군·이속 디버프원)", Find("초월_노태현_AP")));
        sb.AppendLine($"   마나 최대 {d.manaMax} · 마나 오라 +{d.manaAuraRegenPerSecond}/초 반경 {d.manaAuraRange}");
        return sb.ToString();
    }

    // ---- 10-06 2차 실측 추가분(구현담당3) ----
    static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    /// 0번 레인 가운데에 죽지 않는 표적 6마리(안 움직임)를 세우고 임장혁·박민수·노태현을 그 둘레에 세운다(이미 있으면 Warp).
    public static string Arena()
    {
        StringBuilder sb = new StringBuilder();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData dummyData = UnityEditor.AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" }).Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        if (lane == null || dummyData == null) return "❌ 레인·표적 없음";
        Vector3 c = lane.LaneCenter;
        for (int i = 0; i < 6; i++)
        {
            GameObject go = Object.Instantiate(dummyData.prefab, c + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 8f, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (go.TryGetComponent(out EnemyDummy dummy)) { dummy.Initialize(dummyData, 1e9f); dummy.SetLane(-1); }
        }
        float[] ang = { 0f, 120f, 240f };
        string[] names = { "초월_임장혁_AD", "흔함_박민수", "초월_노태현_AP" };
        for (int i = 0; i < 3; i++)
        {
            UnitAttacker a = Find(names[i]);
            if (a == null) { sb.AppendLine($"   {names[i]} 없음(Setup 먼저)"); continue; }
            Vector3 p = c + Quaternion.Euler(0f, ang[i], 0f) * Vector3.forward * 30f;
            if (a.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.Warp(p); else a.transform.position = p;
        }
        return sb.Append("   표적 6마리 + 아군 3기를 레인 가운데에 세움").ToString();
    }

    static int Gauge(UnitAttacker a) => (int)typeof(UnitAttacker).GetField("manaGaugeCounter", BF).GetValue(a);
    static void SetGauge(UnitAttacker a, int v) { typeof(UnitAttacker).GetField("manaGaugeCounter", BF).SetValue(a, v); typeof(UnitAttacker).GetField("manaGaugeInitialized", BF).SetValue(a, true); }

    /// 임장혁 마나 게이지를 119로(다음 평타에 +1 → 120 → 악보완성! 발동 기대).
    public static string Gauge119() { UnitAttacker j = Find("초월_임장혁_AD"); SetGauge(j, 119); return $"   임장혁 게이지 → {Gauge(j)}"; }

    /// 게이지 초기화 표시(재생은 초기화된 뒤에만 돈다) — 둘 다 0으로.
    public static string InitGauges() { SetGauge(Find("초월_임장혁_AD"), 0); SetGauge(Find("초월_노태현_AP"), 0); return "   게이지 0으로 초기화(임장혁·노태현)"; }

    public static string Gauges()
    {
        UnitAttacker j = Find("초월_임장혁_AD"), p = Find("흔함_박민수"), n = Find("초월_노태현_AP");
        return $"   게이지 임장혁 {Gauge(j)}/120 · 노태현 {Gauge(n)}/50 · 박민수 공속 {p.CurrentAttackSpeedMultiplier:F3} · 노태현 공속 {n.CurrentAttackSpeedMultiplier:F3} · 임장혁 공속 {j.CurrentAttackSpeedMultiplier:F3}";
    }

    /// 특성(이간질 제거) 실제 구매 경로: 포인트 2 지급 → 소모 → Unlock. 전/후 박민수 공격력은 Report로.
    public static string BuyTrait()
    {
        UnitData d = Find("초월_임장혁_AD").GetComponent<UnitIdentity>().Data;
        UnitUpgrades up = PlayerContext.Get(0).UnitUpgrades;
        if (d.trait == null || up == null) return "❌ trait 또는 UnitUpgrades 없음";
        up.AddTraitPoints(2);
        bool spent = up.TrySpendTraitPoints(d.trait.costTraitPoints);
        up.Unlock(d.trait);
        return $"   특성 「{d.trait.traitName}」 비용 {d.trait.costTraitPoints}pt 소모 {spent} · 해금 {up.IsUnlocked(d.trait)} · 스킬 레벨 인덱스 {up.SkillLevelIndexFor(d)}";
    }
}
