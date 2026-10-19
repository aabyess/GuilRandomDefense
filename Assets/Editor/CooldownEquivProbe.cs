using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// UnitAttacker 쿨다운·오라 스킬 목록 캐시(perf/1020 445466607)가 옛 경로(매 프레임 처음부터 풀기)와 같은지 보는 탐침.
//  ① 구조 일치: 매 샘플마다 캐시 목록(스킬·레벨)이 처음부터 다시 푼 결과와 같은가(DebugCacheMatchesFresh) — 정확히 같아야 한다.
//  ② 쿨타임 CSV: 5초 동안 0.5초마다 각 스킬 남은 쿨타임을 기록. 캐시 켬/끔 두 판을 따로 돌려 Diff로 비교(프레임 흔들림 때문에 ±0.15초 안이면 같다고 본다).
//  ③ Unlock: 특성(스킬 레벨·교체) 해금 직후에도 일치하는가(UnitUpgrades.Version 무효화 확인).
// gameshot 예(두 판, 각각 따로):
//   gameshot a.png 1 1920x1080 mode:보통 wait:2 call:CooldownEquivProbe.Arena wait:1 call:CooldownEquivProbe.RunOn wait:7 call:CooldownEquivProbe.Unlock wait:2 call:CooldownEquivProbe.Report
//   gameshot b.png 1 1920x1080 mode:보통 wait:2 call:CooldownEquivProbe.Arena wait:1 call:CooldownEquivProbe.RunOff wait:7 call:CooldownEquivProbe.Report
//   (그 뒤) call CooldownEquivProbe.Diff
static class CooldownEquivProbe
{
    const int UnitCount = 10;
    const float SampleEvery = 0.5f, Duration = 5f;
    static readonly List<UnitAttacker> units = new List<UnitAttacker>();
    static UnitAttacker traitUnit;
    static UnitTraitData trait;
    static readonly List<string> rows = new List<string>();
    static string mode = "";
    static double t0, next;
    static bool running, allMatch = true;
    static string firstMismatch = "";

    static string OutPath(string m) => Path.Combine(Application.dataPath, "../ClaudeBridge/outbox/cooldown_equiv_" + m + ".csv");

    static bool HasCdSkill(UnitData d)
    {
        for (int i = 0; i < d.SkillCount; i++)
        {
            SkillData s = d.SkillAt(i);
            if (s == null || (s.triggerType != SkillTriggerType.CooldownAutoCast && s.triggerType != SkillTriggerType.Aura)) continue;
            if (s.levels != null && s.levels.Count > 0 && s.levels[0].effects != null && s.levels[0].effects.Count > 0) return true;
        }
        return false;
    }

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        if (spawner == null || lane == null) return "❌ UnitSpawner·레인 없음";
        units.Clear(); traitUnit = null; trait = null;
        List<UnitData> pool = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.prefab != null && HasCdSkill(d)).OrderBy(d => d.name).ToList();
        // 스킬 레벨 올리는 특성이 걸린 유닛을 맨 앞에 — Unlock 시험용
        UnitData withTrait = pool.FirstOrDefault(d => d.trait != null && (d.trait.skillLevelUnlockIndex > 0 || d.trait.replacementSkill != null));
        var pick = new List<UnitData>();
        if (withTrait != null) pick.Add(withTrait);
        foreach (UnitData d in pool) { if (pick.Count >= UnitCount) break; if (!pick.Contains(d)) pick.Add(d); }
        for (int i = 0; i < pick.Count; i++)
        {
            Vector3 at = lane.LaneCenter + new Vector3((i % 5) * 8f - 16f, 0f, (i / 5) * 8f - 4f);
            GameObject go = spawner.Spawn(pick[i], at, 0);
            if (go != null && go.TryGetComponent(out UnitAttacker a)) units.Add(a);
        }
        if (withTrait != null && units.Count > 0) { traitUnit = units[0]; trait = withTrait.trait; }
        return $"유닛 {units.Count}기(쿨다운·오라 스킬 보유) · 특성 시험 유닛 {(withTrait != null ? withTrait.name + " / " + withTrait.trait.name : "없음")}";
    }

    static string RunOn() => Start("on");
    static string RunOff() => Start("off");

    static string Start(string m)
    {
        if (!Application.isPlaying || units.Count == 0) return "❌ 플레이 중 Arena 먼저";
        mode = m; rows.Clear(); allMatch = true; firstMismatch = "";
        UnitAttacker.UseCooldownCache = m == "on";
        rows.Add("t,unit,skill,remaining,cacheMatchesFresh");
        t0 = EditorApplication.timeSinceStartup; next = t0; running = true;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return $"시작({m}) — {Duration}초간 {SampleEvery}초마다 기록";
    }

    static void Tick()
    {
        if (!Application.isPlaying) { running = false; EditorApplication.update -= Tick; return; }
        double now = EditorApplication.timeSinceStartup;
        if (now < next) return;
        next += SampleEvery;
        Sample((float)(now - t0));
        if (now - t0 >= Duration) { Finish(); }
    }

    static void Sample(float t)
    {
        foreach (UnitAttacker u in units)
        {
            if (u == null) continue;
            string name = u.GetComponent<UnitIdentity>() != null && u.GetComponent<UnitIdentity>().Data != null ? u.GetComponent<UnitIdentity>().Data.name : u.name;
            bool ok = u.DebugCacheMatchesFresh(out string detail);
            if (!ok) { allMatch = false; if (firstMismatch == "") firstMismatch = name + ": " + detail; }
            foreach (var (skill, remaining) in u.DebugCooldowns())
                rows.Add($"{t:0.0},{name},{skill},{remaining:0.000},{ok}");
        }
    }

    static void Finish()
    {
        running = false; EditorApplication.update -= Tick;
        Directory.CreateDirectory(Path.GetDirectoryName(OutPath(mode)));
        File.WriteAllLines(OutPath(mode), rows, Encoding.UTF8);
        UnitAttacker.UseCooldownCache = true;
    }

    static string Report()
    {
        if (running) return $"기록 중({mode}) …";
        return $"{mode}: 행 {rows.Count - 1} · 구조 일치 {(allMatch ? "전부 ✅" : "❌ " + firstMismatch)} · CSV {OutPath(mode)}";
    }

    // 특성 해금 직후 같은 프레임에서 캐시가 다시 풀렸는가
    static string Unlock()
    {
        if (!Application.isPlaying || traitUnit == null || trait == null) return "❌ Arena 먼저(특성 유닛 없음이면 이 시험은 건너뜀)";
        PlayerContext ctx = PlayerContext.Get(0);
        if (ctx == null || ctx.UnitUpgrades == null) return "❌ 플레이어 0 강화 없음";
        traitUnit.DebugCacheMatchesFresh(out string before);
        List<(string, float)> cdBefore = traitUnit.DebugCooldowns().Select(x => (x.name, x.remaining)).ToList();
        ctx.UnitUpgrades.Unlock(trait);
        bool ok = traitUnit.DebugCacheMatchesFresh(out string after);
        var names = traitUnit.DebugCooldowns().Select(x => x.name).ToList();
        return $"해금 {trait.name}: 전 [{string.Join(",", cdBefore.Select(x => x.Item1))}] → 후 [{string.Join(",", names)}] · 일치 {(ok ? "✅" : "❌")} {after}";
    }

    static string Diff()
    {
        string a = OutPath("on"), b = OutPath("off");
        if (!File.Exists(a) || !File.Exists(b)) return "❌ on·off CSV 둘 다 필요";
        string[] la = File.ReadAllLines(a), lb = File.ReadAllLines(b);
        int n = Mathf.Min(la.Length, lb.Length), over = 0, keyDiff = 0; float max = 0f;
        for (int i = 1; i < n; i++)
        {
            string[] x = la[i].Split(','), y = lb[i].Split(',');
            if (x[1] != y[1] || x[2] != y[2]) { keyDiff++; continue; }
            float d = Mathf.Abs(float.Parse(x[3]) - float.Parse(y[3]));
            max = Mathf.Max(max, d); if (d > 0.15f) over++;
        }
        return $"행 on {la.Length - 1} · off {lb.Length - 1} · 유닛/스킬 키 불일치 {keyDiff} · 쿨타임 최대 차 {max:0.000}초 · 0.15초 초과 {over}행 → {(keyDiff == 0 && over == 0 && la.Length == lb.Length ? "같음 ✅" : "확인 필요 ❌")}";
    }
}
