using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 영원한 채우기 B·C 실측(2026-09-30) — 핸콕 1/14 stomp(영원_김영원)와 비비 더블샷·3연사(영원_문필환).
// 유닛마다 떨어진 자리에 죽지 않는 표적 셋. 스킬별 시전 수·간격(SkillTelemetry.CastLog)과 표적이 실제로 스턴에 걸린 시간을 낸다.
// ⚠️ 탐침 주의(09-30): ① 표적 체력 ×1e6에선 float 눈금이 1024쯤 — 작은 시험 피해는 눈금에 묻힌다 ② 데스카운트를 안 끄면
//    게임 시간 50초쯤에 판이 끝나 유닛이 사라진다 ③ %최대체력 스킬은 표적을 금방 죽인다 → 죽으면 다시 세운다(Sample).
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:EternalFillProbe.Arena wait:90 call:EternalFillProbe.Report
static class EternalFillProbe
{
    static string[] Units = Eternal;
    static readonly string[] Eternal = { "영원_문필환", "영원_김영원", "영원_최상호", "영원_조세민", "영원_김정래" };
    // 불멸 채우기(2026-09-30) — call:EternalFillProbe.ArenaImmortal. 오라는 Report의 「공격력·주기·표적 방어·이감」 줄로 본다
    // (센고쿠 A062는 맵 전체라 나머지 일곱의 공격력에 +11%가 같이 실린다).
    static readonly string[] Immortal = { "불멸_정윤식", "불멸_고도현", "불멸_이승우", "불멸_이이삭", "불멸_신지우", "불멸_박은석", "불멸_김용태", "불멸_정준영" };
    static readonly Dictionary<string, UnitAttacker> attackers = new Dictionary<string, UnitAttacker>();

    static string ArenaImmortal() { Units = Immortal; return Arena(); }
    // 초월·제한 큰 어긋남 정정(2026-09-30) — call:EternalFillProbe.ArenaTranscend. 황준석의 맵 전체 오라(방어 −8 · 이속 −5%)가 모든 표적에 실린다.
    static readonly string[] Transcend = { "초월_두유찬_AD", "초월_양재모_AD", "초월_신문철_AP", "초월_최상호_AP", "초월_황준석_ADAP", "초월_구주호_AD",
                                           "제한_강보명", "제한_김민규", "제한_이충민", "제한_박성호" };
    static string ArenaTranscend() { Units = Transcend; return Arena(); }

    class Track { public Vector3 at; public string unit; public float stunStart = -1f; public readonly List<float> stuns = new List<float>(); }
    static readonly Dictionary<EnemyDummy, Track> tracks = new Dictionary<EnemyDummy, Track>();
    static readonly List<UnitData> fielded = new List<UnitData>();
    static float startTime;

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f && e.name.Contains("R2"));
        if (spawner == null || lane == null || dummyData == null) return "❌ UnitSpawner·레인·표적 적 없음";
        tracks.Clear(); fielded.Clear(); attackers.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        for (int u = 0; u < Units.Length; u++)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Units[u]}.asset");
            if (data == null) continue;
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 360f / Units.Length, 0f) * Vector3.forward * 2000f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = home + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 4f;
                EnemyDummy dummy = SpawnTarget(at);
                if (dummy != null) tracks[dummy] = new Track { unit = Units[u], at = at };
            }
            GameObject unit = spawner.Spawn(data, home, 0);
            if (unit != null) { fielded.Add(data); if (unit.TryGetComponent(out UnitAttacker attacker)) attackers[Units[u]] = attacker; }
        }
        // 탐침은 레인 적을 안 막는다 — 데스카운트로 판이 끝나면 유닛이 사라져 평타가 멎는다(첫 판에서 50초쯤에 멎음).
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        startTime = Time.time;
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
        Time.timeScale = 2f;
        return $"유닛 {fielded.Count}/{Units.Length} · 표적 {dummyData.name}(PV {dummyData.pointValue}) · 2배속";
    }

    static EnemyData dummyData;
    static EnemyDummy SpawnTarget(Vector3 at)
    {
        GameObject go = Object.Instantiate(dummyData.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
        dummy.Initialize(dummyData, 1e6f);
        dummy.SetLane(-1);
        return dummy;
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        foreach (var kv in tracks.ToList())
        {
            if (kv.Key == null || kv.Key.IsDead)
            {
                tracks.Remove(kv.Key);
                EnemyDummy fresh = SpawnTarget(kv.Value.at);
                if (fresh != null) { kv.Value.stunStart = -1f; tracks[fresh] = kv.Value; }
                continue;
            }
            Track t = kv.Value;
            bool stunned = kv.Key.IsStunned;
            if (stunned && t.stunStart < 0f) t.stunStart = Time.time;
            else if (!stunned && t.stunStart >= 0f) { t.stuns.Add(Time.time - t.stunStart); t.stunStart = -1f; }
        }
    }

    static string Report()
    {
        EditorApplication.update -= Sample;
        Time.timeScale = 1f;
        float elapsed = Time.time - startTime;
        var sb = new StringBuilder($"\n게임 시간 {elapsed:0.0}초\n" + SkillTelemetry.Report(fielded));
        foreach (UnitData u in fielded)
            foreach (var g in SkillTelemetry.CastLog.Where(e => e.unit == u).GroupBy(e => e.skill))
            {
                List<float> times = g.Select(e => e.time).ToList();
                string gap = times.Count > 1 ? $" · 간격 평균 {(times[times.Count - 1] - times[0]) / (times.Count - 1):0.00}초" : "";
                sb.Append($"\n{u.name} 「{(g.Key.Length > 40 ? g.Key.Substring(0, 40) : g.Key)}」 시전 {times.Count} · 판정 {SkillTelemetry.HitsOf(u)}타 중 {(float)times.Count / Mathf.Max(1, SkillTelemetry.HitsOf(u)):P1}{gap}");
            }
        foreach (var kv in attackers)
        {
            if (kv.Value == null) continue;
            EnemyDummy near = tracks.Where(t => t.Value.unit == kv.Key && t.Key != null).Select(t => t.Key).FirstOrDefault();
            sb.Append($"\n{kv.Key} 공격력 {kv.Value.AttackDamage:0} · 주기 {kv.Value.AttackInterval:0.000}"
                      + (near != null ? $" · 표적 방어 {near.EffectiveArmor:0.0}(에셋 {dummyData.armor}) · 이감 {near.EffectiveSlowMultiplier:0.00}" : ""));
        }
        foreach (var g in tracks.Values.GroupBy(t => t.unit))
        {
            List<float> stuns = g.SelectMany(t => t.stuns).OrderBy(x => x).ToList();
            sb.Append($"\n{g.Key} 표적 스턴 {stuns.Count}회" + (stuns.Count > 0 ? $" (최소 {stuns[0]:0.00} · 중앙 {stuns[stuns.Count / 2]:0.00} · 최대 {stuns[stuns.Count - 1]:0.00}초) 전부: {string.Join(" ", stuns.Select(x => x.ToString("0.00")))}" : ""));
            // 표적별 — 평타 대상이 아닌 표적이 걸린 스턴은 무작위 대상(RandomEnemyInRange) 효과만 낼 수 있다(비비 더블샷 둘째 발).
            sb.Append($" · 표적별 {string.Join("/", g.Select(t => t.stuns.Count + (t.stunStart >= 0f ? 1 : 0)))}");
        }
        return sb.ToString();
    }
}
