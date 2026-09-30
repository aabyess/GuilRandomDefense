using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 평타 다중 대상(UnitData.attackExtraTargets) 실측(2026-09-30, 구조 칸 백로그 9번) — 멀티샷이 든 로스터 일곱을 스킬·치명을 뺀 복제로 세우고
// 표적 열을 한 줄(60부터 50 간격, 전부 반경 안)로. 표적별 깎인 체력 ÷ 최대 = 맞은 표적 수가 1 + 추가 대상 수여야 한다.
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:MultishotProbe.Arena wait:20 call:MultishotProbe.Report
static class MultishotProbe
{
    static readonly string[] Rosters = { "랜덤_이즈미_신이치", "변화됨_박은석", "전설적인_이재윤", "초월_강재규_AP", "희귀함_김청운", "희귀함_노태현", "히든_최윤서", "흔함_강주혁" };
    static readonly List<(UnitData data, List<EnemyDummy> targets, List<float> hp0)> fielded = new List<(UnitData, List<EnemyDummy>, List<float>)>();

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        if (spawner == null || lane == null || enemy == null) return "❌ 준비 실패";
        fielded.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        float k = 1f / WorldScale.Value;
        for (int u = 0; u < Rosters.Length; u++)
        {
            UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Rosters[u]}.asset");
            if (source == null) continue;
            UnitData data = Object.Instantiate(source);
            data.name = source.name; data.critChance = 0f; data.skill = null; data.skills = new List<SkillData>();
            data.attackSplashRadius = 0f; data.attackCleaveFactor = 0f;
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 360f / Rosters.Length, 0f) * Vector3.forward * 3000f;
            var targets = new List<EnemyDummy>();
            for (int i = 0; i < 10; i++)
            {
                GameObject go = Object.Instantiate(enemy.prefab, home + Vector3.right * (60f + i * 50f) * k, Quaternion.identity);
                if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
                if (!go.TryGetComponent(out EnemyDummy dummy)) continue;
                dummy.Initialize(enemy, Mathf.Max(1f, data.attackPower) * 5000f / Mathf.Max(1f, enemy.hp));
                dummy.SetLane(-1);
                targets.Add(dummy);
            }
            spawner.Spawn(data, home, 0);
            fielded.Add((data, targets, targets.Select(t => t.Hp).ToList()));
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        return $"로스터 {fielded.Count}";
    }

    static string Report()
    {
        var sb = new StringBuilder();
        foreach (var f in fielded)
        {
            List<float> lost = f.targets.Select((t, i) => t == null ? f.hp0[i] : f.hp0[i] - t.Hp).ToList();
            float top = Mathf.Max(1f, lost.Max());
            sb.Append($"\n{f.data.name}(추가 {f.data.attackExtraTargets} · 반경 {f.data.attackExtraTargetRadius}): 평타 {SkillTelemetry.HitsOf(f.data)}타 · 맞은 표적 {lost.Count(x => x > top * 0.01f)}(기대 {1 + f.data.attackExtraTargets}) · 표적별 {string.Join(" ", lost.Select(x => (x / top).ToString("0.00")))}");
        }
        return sb.ToString();
    }
}
