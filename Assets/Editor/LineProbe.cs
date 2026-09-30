using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 장풍 직선(SkillEffect.lineLength) 실측(2026-09-30, 구조 칸 백로그 5번).
// · 합성: 평타 0.001짜리 복제 유닛에 「매 타 선 피해(길이 1000 · 반경 200→400)」 스킬 하나. 표적을 (앞으로, 옆으로) 자리에 세우고 맞았나만 본다.
// · 로스터: 선이 든 로스터 셋을 한 줄 표적(앞으로 150 간격 여덟)과 세워 몇 번째까지 맞는지.
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:LineProbe.Arena wait:40 call:LineProbe.Report
static class LineProbe
{
    // (앞으로, 옆으로, 맞아야 하나) — 원작 단위. 첫 줄이 주 대상.
    static readonly (float along, float across, bool expect)[] Spots =
    {
        (60f, 0f, true), (600f, 0f, true), (950f, 0f, true), (1100f, 0f, false), (500f, 250f, true), (500f, 350f, false),
        (100f, 250f, false), (-100f, 0f, false), (950f, 380f, true), (950f, -380f, true), (950f, 420f, false),
    };
    static readonly string[] Rosters = { "영원_최상호", "희귀함_서민성", "초월_임장혁_AD" };

    static readonly List<(EnemyDummy t, float hp0)> spots = new List<(EnemyDummy, float)>();
    static readonly List<(UnitData data, List<EnemyDummy> targets, List<float> hp0)> fielded = new List<(UnitData, List<EnemyDummy>, List<float>)>();

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData enemy = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R01"));
        UnitData baseData = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/흔함_강주혁.asset");
        if (spawner == null || lane == null || enemy == null || baseData == null) return "❌ 준비 실패";
        spots.Clear(); fielded.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        float k = 1f / WorldScale.Value;

        EnemyDummy Target(Vector3 at)
        {
            GameObject go = Object.Instantiate(enemy.prefab, at, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
            dummy.Initialize(enemy, 1e9f / Mathf.Max(1f, enemy.hp));
            dummy.SetLane(-1);
            return dummy;
        }

        Vector3 home = lane.LaneCenter + Vector3.forward * 3000f;
        foreach (var s in Spots)
        {
            EnemyDummy t = Target(home + Vector3.right * s.along * k + Vector3.forward * s.across * k);
            spots.Add((t, t.Hp));
        }
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "LineProbe"; skill.triggerType = SkillTriggerType.OnHitChance;
        skill.levels = new List<SkillLevel> { new SkillLevel { triggerChance = 1f, effects = new List<SkillEffect> { new SkillEffect {
            kind = SkillEffectKind.Damage, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells,
            multiplier = 1000f, lineLength = 1000f, lineStartRadius = 200f, lineEndRadius = 400f } } } };
        UnitData data = Object.Instantiate(baseData);
        data.name = "LineProbe"; data.attackPower = 0.001f; data.critChance = 0f; data.skill = null; data.skills = new List<SkillData> { skill };
        data.attackSplashRadius = 0f; data.attackCleaveFactor = 0f;
        spawner.Spawn(data, home, 0);

        for (int u = 0; u < Rosters.Length; u++)
        {
            UnitData roster = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Rosters[u]}.asset");
            if (roster == null) continue;
            Vector3 at = lane.LaneCenter + Quaternion.Euler(0f, 90f + u * 90f, 0f) * Vector3.forward * 3000f;
            var targets = new List<EnemyDummy>();
            for (int i = 0; i < 8; i++) targets.Add(Target(at + Vector3.right * (60f + i * 150f) * k));
            spawner.Spawn(roster, at, 0);
            fielded.Add((roster, targets, targets.Select(t => t.Hp).ToList()));
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        Time.timeScale = 2f;
        return $"합성 표적 {spots.Count} · 로스터 {fielded.Count}";
    }

    static string Report()
    {
        Time.timeScale = 1f;
        var sb = new StringBuilder("\n합성(앞,옆 → 맞음/기대): ");
        int wrong = 0;
        for (int i = 0; i < spots.Count; i++)
        {
            bool hit = spots[i].t == null || spots[i].hp0 - spots[i].t.Hp > 500f;
            if (hit != Spots[i].expect) wrong++;
            sb.Append($"({Spots[i].along:0},{Spots[i].across:0}) {(hit ? "맞음" : "안 맞음")}/{(Spots[i].expect ? "맞음" : "안 맞음")} · ");
        }
        sb.Append($"어긋남 {wrong}");
        foreach (var f in fielded)
        {
            List<float> lost = f.targets.Select((t, i) => t == null ? f.hp0[i] : f.hp0[i] - t.Hp).ToList();
            float top = Mathf.Max(1f, lost.Max());
            sb.Append($"\n{f.data.name}: 시전 {SkillTelemetry.CastLog.Count(e => e.unit == f.data)} · 한 줄 표적(60부터 150 간격) 깎인 체력 ÷ 최대 {string.Join(" ", lost.Select(x => (x / top).ToString("0.00")))}");
        }
        return sb.ToString();
    }
}
