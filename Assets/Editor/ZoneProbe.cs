using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 주기 피해 지대(SkillDamageZone) 실측(2026-09-30, 구조 칸 백로그 8번).
// · 합성: 지대를 직접 세운다 — 한 틱짜리(기준)와 (간격·수명) 다섯 쌍. 표적이 깎인 체력 ÷ 기준 = 틱 수. 시전자 없이도 도는지도 본다.
// · 로스터: 지대가 든 로스터를 표적 셋과 세우고 계측 채널 「지대」 피해·시전 수를 낸다.
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:ZoneProbe.Arena wait:60 call:ZoneProbe.Report
static class ZoneProbe
{
    static readonly (float tick, float life, int expect)[] Synthetic = { (0.2f, 0.2f, 1), (0.2f, 1.595f, 7), (0.15f, 1.595f, 10), (0.15f, 0.595f, 3), (0.18f, 0.595f, 3), (0.15f, 3.595f, 23), (0.2f, 2.595f, 12) };
    static readonly string[] Rosters = { "초월_김만경_AD", "불멸_정준영", "영원_윤현모", "랜덤_한마_바키", "변화됨_박은석", "전설적인_임건웅", "히든_호치킨" };

    static readonly List<(EnemyDummy target, float hp0)> synthetic = new List<(EnemyDummy, float)>();
    static readonly List<(UnitData data, List<EnemyDummy> targets, List<float> hp0)> fielded = new List<(UnitData, List<EnemyDummy>, List<float>)>();
    static EnemyDummy outside;
    static float outsideHp0;

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData enemy = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R01"));
        if (spawner == null || lane == null || enemy == null) return "❌ 준비 실패";
        synthetic.Clear(); fielded.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;

        EnemyDummy Target(Vector3 at)
        {
            GameObject go = Object.Instantiate(enemy.prefab, at, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
            dummy.Initialize(enemy, 1e9f / Mathf.Max(1f, enemy.hp));
            dummy.SetLane(-1);
            return dummy;
        }

        for (int i = 0; i < Synthetic.Length; i++)
        {
            Vector3 at = lane.LaneCenter + Vector3.right * (3000f + i * 400f) + Vector3.forward * 3000f;
            EnemyDummy t = Target(at);
            synthetic.Add((t, t.Hp));
            var effect = new SkillEffect { kind = SkillEffectKind.Damage, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 100000f,
                                           duration = Synthetic[i].life, zoneTickInterval = Synthetic[i].tick, zoneRadius = 100f };
            SkillDamageZone.Spawn(at, 100f / WorldScale.Value, effect, null, 0, false);
            if (i == 0)
            {
                // 반경 밖(원작 150)에 하나 — 안 맞아야 한다.
                outside = Target(at + Vector3.forward * (150f / WorldScale.Value));
                outsideHp0 = outside.Hp;
            }
        }
        for (int u = 0; u < Rosters.Length; u++)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Rosters[u]}.asset");
            if (data == null) continue;
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 360f / Rosters.Length, 0f) * Vector3.forward * 2200f;
            var targets = new List<EnemyDummy>();
            for (int i = 0; i < 3; i++) targets.Add(Target(home + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 4f));
            spawner.Spawn(data, home, 0);
            fielded.Add((data, targets, targets.Select(t => t.Hp).ToList()));
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        Time.timeScale = 2f;
        return $"합성 {synthetic.Count} · 로스터 {fielded.Count} · 표적 {enemy.name}";
    }

    static string Report()
    {
        Time.timeScale = 1f;
        var sb = new StringBuilder();
        float one = synthetic.Count > 0 && synthetic[0].target != null ? synthetic[0].hp0 - synthetic[0].target.Hp : 0f;
        sb.Append($"\n합성(한 틱 = {one:0}): " + string.Join(" · ", synthetic.Select((s, i) =>
            $"{Synthetic[i].tick}/{Synthetic[i].life}초 → {(s.target != null ? (s.hp0 - s.target.Hp) / Mathf.Max(1f, one) : -1f):0.00}틱(기대 {Synthetic[i].expect})")));
        sb.Append($"\n반경 밖 표적 피해 {(outside != null ? outsideHp0 - outside.Hp : -1f):0} · 남은 지대 {SkillDamageZone.ActiveCount}");
        foreach (var f in fielded)
        {
            float lost = f.targets.Select((t, i) => t == null ? f.hp0[i] : f.hp0[i] - t.Hp).Sum();
            sb.Append($"\n{f.data.name}: 시전 {SkillTelemetry.CastLog.Count(e => e.unit == f.data)} · 지대 피해 {SkillTelemetry.DamageOf(f.data, "지대"):0} · 표적 셋 깎인 합 {lost:0} · 지대 몫 {SkillTelemetry.DamageOf(f.data, "지대") / Mathf.Max(1f, lost):P0}");
        }
        return sb.ToString();
    }
}
