using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 노태현 「반사회적인격 Lv.2」 값 정하기(구현담당1, 10-08 밤) — 불멸 8기·노태현 3상태의 신 난이도 합계 DPS를 단일 보스 / 라인몹 5기 무리로 잰다.
// 노태현 3상태: ⓐ 강화 전 ⓑ 최윤서 강화(방무만, Lv.2 값 0) ⓒ 최윤서 강화 + Lv.2(현재 에셋 값). Lv.2 값은 Setup 뒤 SetFixed로 바꿔 다시 잴 수 있다(메모리 안에서만).
// gameshot x.png 1 1920x1080 click?:신 mode:신 wait:2 call:NotaeLv2Probe.SetupBoss wait:1 call:NotaeLv2Probe.Mark wait:20 call:NotaeLv2Probe.Report
//         (무리: SetupSwarm) — 표적은 매 프레임 채우고 피해를 double로 누적(TranscendBossProbe와 같은 방식).
static class NotaeLv2Probe
{
    const float Refill = 1e9f;
    static readonly string[] Immortals = { "불멸_이이삭", "불멸_정준영", "불멸_이승우", "불멸_신지우", "불멸_정윤식", "불멸_고도현", "불멸_김용태", "불멸_박은석" };
    class Row { public string label; public UnitAttacker unit; public UnitData data; public List<EnemyDummy> targets = new List<EnemyDummy>(); public double dealt, atMark; public int hits0; }
    static readonly List<Row> rows = new List<Row>();
    static float markT;
    static readonly FieldInfo HpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);

    static UnitData Load(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string Build(bool swarm)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        EnemyData mob = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        if (spawner == null || boss == null || mob == null || LaneMarker.Get(0) == null) return "❌ 준비 실패";
        var mode = DifficultyManager.Instance.Current;
        if (!swarm) { rows.Clear(); SkillTelemetry.Reset(); SkillTelemetry.Enabled = true; }

        var scenarios = new List<(string label, string unit, bool buff, bool lv2)>();
        string extraPath = "/private/tmp/claude-501/-Users-sang-GitHub-GuilRandomDefense/b7572603-8e06-4558-82cd-42b0cfe68a1a/scratchpad/extra_units.txt";
        if (System.IO.File.Exists(extraPath))
        {
            // 한 줄에 로스터 이름 하나 — 있으면 불멸·노태현 대신 이 유닛들만 잰다(초월 문 이식 전후 비교용).
            foreach (string line in System.IO.File.ReadAllLines(extraPath)) if (line.Trim().Length > 0) scenarios.Add((line.Trim(), line.Trim(), false, false));
        }
        else {
        foreach (string n in Immortals) scenarios.Add((n, n, false, false));
        scenarios.Add(("노태현_강화전", "초월_노태현_AP", false, true));
        scenarios.Add(("노태현_강화(방무만)", "초월_노태현_AP", true, false));
        scenarios.Add(("노태현_강화+Lv2", "초월_노태현_AP", true, true));
        }

        int i = 0;
        int firstRow = rows.Count;
        foreach (var sc in scenarios)
        {
            UnitData d = Load(sc.unit);
            if (d == null) { i++; continue; }
            Vector3 p = LaneMarker.Get(0).LaneCenter + new Vector3(-1000f + (i % 6) * 220f, 0f, 200f + (i / 6) * 520f + (swarm ? 1800f : 0f));
            var go = spawner.Spawn(d, p, 0);
            var u = go.GetComponent<UnitAttacker>();
            var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) ag.Warp(p);
            if (sc.buff) u.SetYoonseoEnhanced();
            var row = new Row { label = sc.label + (swarm ? "·무리" : "·보스"), unit = u, data = d };
            int count = swarm ? 5 : 1;
            for (int k = 0; k < count; k++)
            {
                EnemyData ed = swarm ? mob : boss;
                Vector3 tp = p + new Vector3((k % 3) * 8f - 8f, 0f, 22f + (k / 3) * 8f);
                GameObject eg = Object.Instantiate(ed.prefab, tp, Quaternion.identity);
                if (eg.TryGetComponent(out WaypointMover m)) m.enabled = false;
                var e = eg.GetComponent<EnemyDummy>(); e.Initialize(ed, 1f); e.SetLane(0);
                e.DifficultyArmorBonus = swarm ? DifficultyTable.MobArmorBonus(mode, 45) : DifficultyTable.BossArmorBonus(mode);
                HpField.SetValue(e, Refill);
                row.targets.Add(e);
            }
            rows.Add(row);
            i++;
        }
        // Lv.2 끄기(강화+방무만 상태): 이 유닛만 Lv.2 스킬을 못 쓰게 한다 — 에셋은 공유라 메모리에서 값만 0으로 두면 다른 노태현에게도 번지므로, 유닛마다 버프로 가른다:
        //   ⓐ 강화 전 = 버프 없음(Lv.1 방무·Lv.2 둘 다 꺼짐) ⓑ 방무만 = Lv.2 값 0이 필요 → 별도 유닛 데이터가 없어 SetFixed 0으로 한 번 더 잰다(아래 Report 주석).
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return $"세움 {rows.Count}기 · {(swarm ? "라인몹 5기 R45" : "보스 R60")} · 난이도 {mode} · 표적 실효방어 {rows[0].targets[0].EffectiveArmor:F1}";
    }

    static string SetupBoss() => Build(false);
    static string SetupSwarm() => Build(true);

    // 다른세계 9기 공속 +25% 확인: 기본 간격 ÷ 실제 간격이 1.25여야(다른 공속 요인이 없는 새 유닛).
    static string OtherWorldCheck()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var sb = new StringBuilder();
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        int i = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d == null || d.grade != UnitGrade.OtherWorld) continue;
            var u = spawner.Spawn(d, c + new Vector3(i * 15f, 0f, 0f), 0).GetComponent<UnitAttacker>();
            sb.AppendLine($"{d.name}: 공속 배율 {u.CurrentAttackSpeedMultiplier:F3} · 간격 {u.AttackInterval:F3}");
            i++;
        }
        var other = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/전설적인_김용태.asset");
        if (other != null) { var u = spawner.Spawn(other, c + new Vector3(-30f, 0f, 0f), 0).GetComponent<UnitAttacker>(); sb.AppendLine($"(대조) {other.name}: 공속 배율 {u.CurrentAttackSpeedMultiplier:F3} · 간격 {u.AttackInterval:F3}"); }
        return $"다른세계 {i}기\n{sb}";
    }


    // 물품 지원 실제 구매: 재고를 2로 채우고(리플렉션) 골드 200 → W 칸(1)을 두 번 눌러 골드 변화·새 유닛을 본다.
    static string StarterRoll()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var shop = Object.FindObjectsByType<GamblingShop>(FindObjectsSortMode.None).First(g => g.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 0);
        var ctx = PlayerContext.Get(0);
        var opt = AssetDatabase.LoadAssetAtPath<GamblingOptionData>("Assets/Data/Gambling/Gambling_물품 지원.asset");
        var stocks = typeof(GamblingProgress).GetField("stocks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ctx.GamblingProgress);
        var st = stocks.GetType().GetProperty("Item");
        object state = System.Activator.CreateInstance(typeof(GamblingProgress).GetNestedType("StockState"));
        typeof(GamblingProgress).GetNestedType("StockState").GetField("count").SetValue(state, 2);
        typeof(GamblingProgress).GetNestedType("StockState").GetField("lastCharge").SetValue(state, Time.time);
        st.SetValue(stocks, state, new object[] { opt });
        ctx.GoldWallet.Add(200);
        int unitsBefore = Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None).Length;
        int g0 = ctx.GoldWallet.Gold;
        bool a = shop.TryUse(1, default, out string ra);
        int g1 = ctx.GoldWallet.Gold;
        bool b = shop.TryUse(1, default, out string rb);
        bool c = shop.TryUse(1, default, out string rc);
        int unitsAfter = Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None).Length;
        return $"1회 {a} 골드 {g0}→{g1}(기대 −70) · 2회 {b} · 3회 {c} ({rc}) · 유닛 {unitsBefore}→{unitsAfter}(기대 +2) · 재고 {ctx.GamblingProgress.Stock(opt)}";
    }


    // 평타 안 치는 원인 추적: 김용태·박은석(+대조 정윤식)을 보스 바로 앞에 세우고 상태를 찍는다. Mark 후 Report 대신 AtkReport로 읽는다.
    static readonly List<(UnitAttacker u, EnemyDummy t, UnitCombat c)> atk = new List<(UnitAttacker, EnemyDummy, UnitCombat)>();
    static string AtkSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        atk.Clear();
        int i = 0;
        foreach (string n in new[] { "불멸_김용태", "불멸_박은석", "불멸_정윤식", "초월_김만경_AD" })
        {
            Vector3 p = LaneMarker.Get(0).LaneCenter + new Vector3(-600f + i * 220f, 0f, 200f);
            var go = spawner.Spawn(Load(n), p, 0);
            var u = go.GetComponent<UnitAttacker>();
            var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            GameObject bg = Object.Instantiate(boss.prefab, go.transform.position + new Vector3(0f, 0f, 22f), Quaternion.identity);
            if (bg.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = bg.GetComponent<EnemyDummy>(); e.Initialize(boss, 1f); e.SetLane(0); HpField.SetValue(e, Refill);
            atk.Add((u, e, go.GetComponent<UnitCombat>()));
            i++;
        }
        EditorApplication.update -= AtkTick; EditorApplication.update += AtkTick;
        return "세움";
    }
    static void AtkTick() { if (!Application.isPlaying) { EditorApplication.update -= AtkTick; return; } foreach (var a in atk) if (a.t != null) HpField.SetValue(a.t, Refill); }
    static string AtkReport()
    {
        var sb = new StringBuilder();
        foreach (var a in atk)
        {
            var ag = a.u.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float dist = a.t != null ? Vector3.Distance(a.u.transform.position, a.t.transform.position) : -1f;
            string agInfo = ag == null ? "없음" : "on=" + ag.isOnNavMesh + " enabled=" + ag.enabled + (ag.isOnNavMesh ? " stopped=" + ag.isStopped : "");
            sb.AppendLine($"{a.u.name}: 평타 {a.u.BasicHitCount}타 · 간격 {a.u.AttackInterval:F3} · 사거리 {a.u.AttackRange:F1} · 표적 거리 {dist:F1} · 에이전트 {agInfo} · 공격력 {a.u.AttackDamage:F0}");
        }
        EditorApplication.update -= AtkTick;
        return sb.ToString();
    }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        foreach (var r in rows)
            foreach (var e in r.targets)
            {
                if (e == null) continue;
                r.dealt += Refill - e.Hp;
                HpField.SetValue(e, Refill);
            }
    }

    static string Mark()
    {
        markT = Time.time;
        foreach (var r in rows) { r.atMark = r.dealt; r.hits0 = r.unit.BasicHitCount; }
        return "표식";
    }

    static string Report()
    {
        float span = Mathf.Max(0.01f, Time.time - markT);
        var sb = new StringBuilder($"\n게임 시간 {span:0.0}초 · 난이도 {DifficultyManager.Instance.Current}");
        foreach (var r in rows)
        {
            double dmg = r.dealt - r.atMark;
            string ch = string.Join(" ", SkillTelemetry.ChannelsOf(r.data).Select(c => $"{c} {SkillTelemetry.DamageOf(r.data, c):N0}"));
            sb.Append($"\n{r.label,-26} 평타 {r.unit.BasicHitCount - r.hits0}타({(r.unit.BasicHitCount - r.hits0) / span:F2}/초) · 피해 {dmg:N0} ({dmg / span:N0}/초)");
        }
        EditorApplication.update -= Tick;
        return sb.ToString();
    }
}
