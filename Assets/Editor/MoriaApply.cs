using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원작 모리아 자리(h00B 겟코 모리아 특별함 — A113 그림자그림자 열매)는 **안흔함_이호준**이다(사장님 10-07 확정, 임채준 아님). raiseOnKill 3필드를 이호준으로 옮기고 임채준에선 지운다.
/// 값: 처치한 일반 적 100% 좀비 1기 · 10초(원작 Nba3). 부르기: call MoriaApply.Apply · 시험: gameshot call:MoriaApply.TestSetup wait:25 call:MoriaApply.TestReport
/// </summary>
static class MoriaApply
{
    const string IhoPath = "Assets/Data/Units/Roster/안흔함_이호준.asset";
    const string OldPath = "Assets/Data/Units/Roster/특별함_임채준.asset";

    static string Apply()
    {
        var iho = AssetDatabase.LoadAssetAtPath<UnitData>(IhoPath);
        var old = AssetDatabase.LoadAssetAtPath<UnitData>(OldPath);
        if (iho == null || old == null) return "❌ 에셋 없음";
        UnitData zombie = old.raiseOnKillUnit != null ? old.raiseOnKillUnit : iho.raiseOnKillUnit;
        if (zombie == null) return "❌ 좀비 유닛 참조가 어디에도 없음";
        iho.raiseOnKillUnit = zombie; iho.raiseOnKillChancePercent = 100f; iho.raiseOnKillLifetimeSeconds = 10f;
        old.raiseOnKillUnit = null; old.raiseOnKillChancePercent = 0f; old.raiseOnKillLifetimeSeconds = 0f;
        EditorUtility.SetDirty(iho); EditorUtility.SetDirty(old);
        AssetDatabase.SaveAssets();
        return $"이호준 raiseOnKill {iho.raiseOnKillUnit.name} {iho.raiseOnKillChancePercent}% {iho.raiseOnKillLifetimeSeconds}초 · 임채준 {(old.raiseOnKillUnit == null ? "비움" : "남음")}";
    }

    // ───── 시험(플레이 중) ─────
    class Seen { public float first, last; public int owner; public string name; }
    static readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>();
    static float testStart;
    static int phase2At = -1;
    static readonly List<EnemyDummy> phase1 = new List<EnemyDummy>();
    static EnemyDummy bossEnemy, storyEnemy;

    static EnemyDummy MakeEnemy(string asset, Vector3 pos, float hpMult)
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{asset}.asset");
        GameObject go = Object.Instantiate(data.prefab, pos, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (go.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.enabled = false;
        var dummy = go.GetComponent<EnemyDummy>();
        dummy.Initialize(data, hpMult);
        dummy.SetLane(-1);
        return dummy;
    }

    static string TestSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        seen.Clear(); phase1.Clear(); bossEnemy = storyEnemy = null;
        testStart = Time.time;
        LaneMarker lane = LaneMarker.Get(0);
        Vector3 home = lane.LaneCenter + Vector3.forward * 3000f;
        var iho = AssetDatabase.LoadAssetAtPath<UnitData>(IhoPath);
        GameObject unit = Object.FindFirstObjectByType<UnitSpawner>().Spawn(iho, home, 0);
        SkillTelemetry.Enabled = true;
        // 1단계: 일반 적 10마리(체력 아주 작게 — 평타 한 방)를 이호준 앞 반원에 세운다
        for (int i = 0; i < 10; i++)
        {
            Vector3 p = home + Quaternion.Euler(0f, -80f + i * 18f, 0f) * (Vector3.forward * 22f);
            phase1.Add(MakeEnemy("Enemy_R01_박진웅", p, 0.0002f));
        }
        EditorApplication.update -= Sample; EditorApplication.update += Sample;
        phase2At = -1;
        return $"이호준 {unit.name} 사거리 {iho.attackRange} · 공 {iho.attackPower} · 일반 적 10마리 세움";
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        foreach (UnitIdentity u in UnitIdentity.Active)
            if (u != null && u.Data != null && u.Data.unitName == "좀비")
            {
                int id = u.GetInstanceID();
                if (!seen.TryGetValue(id, out Seen s)) seen[id] = s = new Seen { first = Time.time, owner = u.OwnerId, name = u.name };
                s.last = Time.time;
            }
        // 2단계(6초 뒤): 보스·스토리 적(PV≥200)을 같은 자리에 세운다 — 좀비가 안 생겨야 한다
        if (phase2At < 0 && phase1.Count > 0 && phase1.All(e => e == null || e.IsDead) && Time.time - testStart > 1f)
        {
            phase2At = seen.Count;
            LaneMarker lane = LaneMarker.Get(0);
            Vector3 home = lane.LaneCenter + Vector3.forward * 3000f;
            bossEnemy = MakeEnemy("Enemy_R60_정윤식", home + Vector3.forward * 20f, 0.0000002f);
            storyEnemy = MakeEnemy("Enemy_Story01_하이츠", home + Vector3.back * 20f, 0.0000002f);
        }
    }

    static string TestReport()
    {
        EditorApplication.update -= Sample;
        var sb = new StringBuilder();
        int afterBoss = seen.Count - phase2At;
        int dead1 = phase1.Count(e => e == null || e.IsDead);
        sb.Append($"\n   1단계 일반 적 {dead1}/{phase1.Count}마리 처치 → 좀비 {(phase2At < 0 ? seen.Count : phase2At)}기(2단계는 {(phase2At < 0 ? "아직 시작 안 함" : "시작")}) · 보스 {(bossEnemy == null || bossEnemy.IsDead ? "처치" : "생존")}·스토리 {(storyEnemy == null || storyEnemy.IsDead ? "처치" : "생존")} · 2단계(보스+스토리 PV≥200) 추가 생성 {(phase2At < 0 ? 0 : afterBoss)}기");
        sb.Append($"\n   주인: {string.Join(",", seen.Values.Select(s => s.owner).Distinct())}(0=내 유닛) · 수명(마지막으로 본 시각−처음) 평균 {(seen.Count > 0 ? seen.Values.Average(s => s.last - s.first) : 0f):0.0}초 최대 {(seen.Count > 0 ? seen.Values.Max(s => s.last - s.first) : 0f):0.0}초");
        int alive = UnitIdentity.Active.Count(u => u != null && u.Data != null && u.Data.unitName == "좀비");
        sb.Append($"\n   지금 살아 있는 좀비 {alive}기(10초가 지났으면 0)");
        return sb.ToString();
    }
}
