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

    // Lv.2 고정 피해 값을 메모리에서 바꾼다(SetFixed 후 Mark부터 다시). 에셋 파일은 안 건드린다.
    static string SetFixed()
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Data/UnitSkills/SkillData_사장님_초월_노태현_AP_반사회적인격_Lv2.asset");
        float v = float.Parse(System.IO.File.ReadAllText("/private/tmp/claude-501/-Users-sang-GitHub-GuilRandomDefense/b7572603-8e06-4558-82cd-42b0cfe68a1a/scratchpad/notae_fixed.txt").Trim());
        skill.levels[0].effects[0].multiplier = v;
        return $"Lv.2 고정 피해 = {v:N0}";
    }


    // 결정론 검증: 노태현(+최윤서 강화 버프)이 Lv.2 스킬을 한 번 시전 — 보스 1기·몹 2기(범위 안)·먼 몹 1기의 체력 감소를 그대로 잰다. 고정 피해면 방어와 무관하게 값 그대로(범위 안 셋), 먼 몹은 0.
    static string Direct()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        EnemyData mob = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        var mode = DifficultyManager.Instance.Current;
        UnitData d = Load("초월_노태현_AP");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        var nt = spawner.Spawn(d, c, 0).GetComponent<UnitAttacker>();
        EnemyDummy Make(EnemyData ed, Vector3 at, float armor)
        {
            GameObject g = Object.Instantiate(ed.prefab, at, Quaternion.identity);
            if (g.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = g.GetComponent<EnemyDummy>(); e.Initialize(ed, 1f); e.SetLane(0); e.DifficultyArmorBonus = armor;
            HpField.SetValue(e, Refill); return e;
        }
        var b = Make(boss, c + new Vector3(0f, 0f, 15f), DifficultyTable.BossArmorBonus(mode));
        var m1 = Make(mob, c + new Vector3(8f, 0f, 20f), DifficultyTable.MobArmorBonus(mode, 45));
        var m2 = Make(mob, c + new Vector3(-8f, 0f, 20f), 5000f);   // 방어 5000 — 고정 피해면 영향 없음
        var far = Make(mob, c + new Vector3(0f, 0f, 400f), 0f);
        SkillData skill = d.skills.Find(x => x != null && x.name.Contains("Lv2"));
        if (skill == null) return "❌ Lv2 스킬이 노태현 목록에 없다";
        var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        // 버프 없음 → 게이트가 막는다(0이어야). 버프 있음 → 값 그대로.
        string noBuff = Cast(cast, nt, skill, b, new[] { b, m1, m2, far });
        nt.SetYoonseoEnhanced();
        string withBuff = Cast(cast, nt, skill, b, new[] { b, m1, m2, far });
        return $"Lv.2 값 {skill.levels[0].effects[0].multiplier:N0} · WorldScale {WorldScale.Value} 범위 {skill.levels[0].WorldRange:F1}\n버프 없음: {noBuff}\n버프 있음: {withBuff}  (순서: 보스 / 몹 / 방어5000 몹 / 먼 몹)";
    }

    static string Cast(MethodInfo cast, UnitAttacker nt, SkillData skill, EnemyDummy primary, EnemyDummy[] all)
    {
        foreach (var e in all) HpField.SetValue(e, Refill);
        cast.Invoke(nt, new object[] { skill.levels[0], skill.levels[0].WorldRange, primary, nt.AttackDamage });
        return string.Join(" / ", all.Select(e => $"{Refill - e.Hp:N0}"));
    }


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
