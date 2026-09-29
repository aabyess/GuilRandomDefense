using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 스킬 미발동 원인 규명(2026-09-29) 2차 판 — 전원 교전판(ClaudeCommands.SkillProbeArena)은 157기가 한꺼번에 서서
// 유닛당 평타 판정이 2~25타뿐이라, 게이지(33~145타)·저확률(≤5%) 스킬은 「안 나간다」와 「아직 안 나갔다」를 못 가른다.
// 의심 유닛만 세워 더 오래 돌린다. gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:SkillGateProbe.Arena wait:150 call:SkillGateProbe.Report
static class SkillGateProbe
{
    static readonly string[] Suspects =
    {
        "히든_최윤서", "히든_호치킨", "히든_황정기", "전설적인_진연서", "제한_김강민",
        "초월_강재규_AP", "초월_김만경_AD", "초월_두유찬_AD", "초월_박기찬_AD", "초월_양재모_AD",
        "영원_이지원", "영원_조세민", "랜덤_손오공", "랜덤_이민형", "랜덤_이즈미_신이치", "랜덤_카마도_탄지로",
        "희귀함_이태훈", "랜덤_가사이_유노",
        // 발동은 했는데 스킬 피해가 0이던 강타(ACbh)형 — 추가 피해가 있는 것만
        "안흔함_박민수", "안흔함_이재윤", "특별함_이지원", "희귀함_노수신", "희귀함_이재윤",
    };

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        if (spawner == null || lane == null || dummyData == null) return "❌ UnitSpawner·레인·표적 적 없음";

        Vector3 c = lane.LaneCenter;
        for (int i = 0; i < 12; i++)
        {
            Vector3 at = c + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * 8f;
            GameObject go = Object.Instantiate(dummyData.prefab, at, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (go.TryGetComponent(out EnemyDummy dummy)) { dummy.Initialize(dummyData, 1e6f); dummy.SetLane(-1); }
        }

        List<UnitData> units = Suspects
            .Select(n => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset"))
            .Where(d => d != null && d.prefab != null).ToList();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        int spawned = 0;
        for (int i = 0; i < units.Count; i++)
        {
            // 둘레에 고르게 — 전원 교전판과 같은 거리(20~40)
            Vector3 at = c + Quaternion.Euler(0f, i * 360f / units.Count, 0f) * Vector3.forward * 30f;
            if (spawner.Spawn(units[i], at, 0) != null) spawned++;
        }
        Time.timeScale = 4f;
        return $"표적 12 · 의심 유닛 {spawned}/{Suspects.Length}기 · 4배속";
    }

    static string Report()
    {
        Time.timeScale = 1f;
        List<UnitData> fielded = UnitIdentity.Active.Where(i => i != null && i.OwnerId == 0).Select(i => i.Data).ToList();
        string report = SkillTelemetry.Report(fielded);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../ClaudeBridge/shots/skill_gate_report.txt"), report);
        return report;
    }
}
