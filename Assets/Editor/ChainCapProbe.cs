using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 연쇄(SkillTargetKind.ChainEnemies)·맞는 수 상한(SkillEffect.maxTargets) 실측(2026-09-30, PM 요청) — 표적 열을 한 줄(간격 20)로 세우고
// 줄 끝에 유닛 하나. 표적별 깎인 체력 ÷ (시전 수 × 기본 피해)로 「몇 번째 표적까지, 몇 배로 맞았나」를 낸다.
// 평타가 섞이지 않게 공격력만 0.001로 낮춘 복제 UnitData를 쓴다(스킬 목록은 그대로).
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:ChainCapProbe.Arena wait:60 call:ChainCapProbe.Report
static class ChainCapProbe
{
    // (로스터, 기본 피해, 기대) — 키자루 희귀: 연쇄 8마리·튈 때마다 ×1.1 / 마르코 희귀·특별: 시전자에서 가까운 순 7마리.
    static readonly (string unit, float baseDamage, string expect)[] Cases =
    {
        ("희귀함_박은석", 6000f, "연쇄 8 · ×1.1씩"),
        ("희귀함_이승우", 12000f, "가까운 7"),
        ("특별함_최준우", 1500f, "가까운 7"),
    };
    const int TargetCount = 10;
    const float Spacing = 20f;

    class Slot { public UnitData data; public float baseDamage; public string label; public readonly List<EnemyDummy> targets = new List<EnemyDummy>(); public readonly List<float> hp0 = new List<float>(); }
    static readonly List<Slot> slots = new List<Slot>();

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData enemy = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R01"));
        if (spawner == null || lane == null || enemy == null) return "❌ 준비 실패";
        slots.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        for (int c = 0; c < Cases.Length; c++)
        {
            UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Cases[c].unit}.asset");
            if (source == null) continue;
            UnitData data = Object.Instantiate(source);
            data.name = source.name + "_probe";
            data.attackPower = 0.001f; data.critChance = 0f;
            data.attackSplashRadius = 0f; data.attackCleaveFactor = 0f;
            var slot = new Slot { data = data, baseDamage = Cases[c].baseDamage, label = $"{Cases[c].unit}({Cases[c].expect})" };
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, c * 360f / Cases.Length, 0f) * Vector3.forward * 2600f;
            for (int i = 0; i < TargetCount; i++)
            {
                GameObject go = Object.Instantiate(enemy.prefab, home + Vector3.right * (15f + i * Spacing), Quaternion.identity);
                if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
                if (!go.TryGetComponent(out EnemyDummy dummy)) continue;
                dummy.Initialize(enemy, 1e8f / Mathf.Max(1f, enemy.hp));
                dummy.SetLane(-1);
                slot.targets.Add(dummy);
                slot.hp0.Add(dummy.Hp);
            }
            spawner.Spawn(data, home, 0);
            slots.Add(slot);
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        Time.timeScale = 2f;
        return $"배치 {slots.Count} · 표적 {TargetCount}마리 간격 {Spacing}(세계) · {enemy.name}";
    }

    static string Report()
    {
        Time.timeScale = 1f;
        var sb = new StringBuilder("\n표적별 = 깎인 체력 ÷ (시전 수 × 기본 피해) — 유닛에서 가까운 순");
        foreach (Slot s in slots)
        {
            int casts = SkillTelemetry.CastLog.Count(e => e.unit == s.data);
            List<float> lost = s.targets.Select((t, i) => t == null ? s.hp0[i] : s.hp0[i] - t.Hp).ToList();
            float unit = Mathf.Max(1f, casts * s.baseDamage);
            sb.Append($"\n{s.label}: 시전 {casts} · 맞은 표적 {lost.Count(x => x > unit * 0.01f)} · 표적별 {string.Join(" ", lost.Select(x => (x / unit).ToString("0.00")))}");
        }
        return sb.ToString();
    }
}
