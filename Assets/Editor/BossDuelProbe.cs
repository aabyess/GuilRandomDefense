using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 보스 결투 탐침(2026-09-30, 밸런스 재측정 ⓒ) — 도구가 못 가는 등급(불멸·초월·제한됨)의 「영구 스턴」 여섯 + 대조군을
// 실제 체력·방어의 보스 하나와 단둘이 세우고 끝까지 본다: 처치 시간 · 보스 스턴·이감 가동률(게임 시계) · 유닛이 낸 채널별 피해.
// 유닛은 1기(다른 유닛·오라 없음), 보스는 움직이지 않고 사거리 안에 선다. 15초마다 부활하지 않는다 — 죽으면 그 시각을 적는다.
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:BossDuelProbe.ArenaR30 wait:75 call:BossDuelProbe.Report
static class BossDuelProbe
{
    static readonly string[] Rosters =
    {
        "초월_황준석_ADAP", "제한_최영민", "불멸_정준영", "불멸_신지우", "불멸_고도현", "초월_조성진_AD",   // 영구 스턴 여섯
        "희귀함_최상호_오타쿠의길", "희귀함_김만경", "특별함_최동준",                                        // 대조군(도구 판에서 쓰인 것)
    };

    class Duel { public UnitData data; public EnemyDummy boss; public float bornAt, stunTime, slowTime, aliveTime, killedAt = -1f; public float lastT; }
    static readonly List<Duel> duels = new List<Duel>();
    static string bossName;

    static string ArenaR20() => Build("Enemy_R20_박은석");
    static string ArenaR30() => Build("Enemy_R30_김만경");
    static string ArenaR40() => Build("Enemy_R40_김용태");
    static string ArenaR50() => Build("Enemy_R50_이태훈");

    static string Build(string enemyName)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{enemyName}.asset");
        if (spawner == null || lane == null || boss == null) return "❌ 준비 실패";
        bossName = enemyName;
        duels.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        for (int u = 0; u < Rosters.Length; u++)
        {
            UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Rosters[u]}.asset");
            if (source == null) continue;
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 360f / Rosters.Length, 0f) * Vector3.forward * 3000f;
            GameObject go = Object.Instantiate(boss.prefab, home + Vector3.forward * 4f, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (!go.TryGetComponent(out EnemyDummy dummy)) continue;
            dummy.Initialize(boss, 1f);
            dummy.SetLane(-1);
            spawner.Spawn(source, home, 0);
            duels.Add(new Duel { data = source, boss = dummy, bornAt = Time.time, lastT = Time.time });
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
        return $"결투 {duels.Count} · 보스 {enemyName}(체력 {boss.hp:0} · 방어 {boss.armor} · 저항 피부 {boss.resistantSkin})";
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        foreach (Duel d in duels)
        {
            float dt = Mathf.Min(0.25f, Time.time - d.lastT);
            d.lastT = Time.time;
            if (d.killedAt >= 0f) continue;
            if (d.boss == null || d.boss.IsDead) { d.killedAt = Time.time - d.bornAt; continue; }
            d.aliveTime += dt;
            if (d.boss.IsStunned) d.stunTime += dt;
            if (d.boss.EffectiveSlowMultiplier < 0.999f) d.slowTime += dt;
        }
    }

    static string Report()
    {
        EditorApplication.update -= Sample;
        var sb = new StringBuilder($"\n보스 {bossName} · 게임 시간 {Time.time - (duels.Count > 0 ? duels[0].bornAt : 0f):0.0}초");
        foreach (Duel d in duels)
        {
            float elapsed = Time.time - d.bornAt;
            string channels = string.Join(" ", SkillTelemetry.ChannelsOf(d.data).Select(c => $"{c} {SkillTelemetry.DamageOf(d.data, c) / Mathf.Max(1f, d.boss != null ? d.boss.MaxHp : 1f):P0}"));
            sb.Append($"\n{d.data.name}: {(d.killedAt >= 0f ? $"처치 {d.killedAt:0.0}초" : $"미처치 — 남은 체력 {(d.boss != null ? d.boss.HpRatio : 0f):P1}")}"
                      + $" · 스턴 {(d.aliveTime > 0f ? d.stunTime / d.aliveTime : 0f):P0} · 이감 {(d.aliveTime > 0f ? d.slowTime / d.aliveTime : 0f):P0}"
                      + $" · 평타 {SkillTelemetry.HitsOf(d.data)}타 · 피해(보스 최대체력 대비) {channels}");
        }
        return sb.ToString();
    }
}
