using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 평타 광역(스플래시·클리브) 실측(2026-09-30, SPLASH_ATTACK_DESIGN.md §7) — 표적 아홉을 원작 스폰 간격(0.65초 × 이속)으로 한 줄로 세우고
// 가운데 표적 옆에 유닛 하나. 표적별 깎인 체력을 주 대상 것으로 나눠 「몇 마리가 몇 배로 맞았나」를 낸다.
// · Arena: 스킬·치명을 뺀 복제 UnitData에 반경만 바꿔 넣는다(코드 축만 본다) — 주 대상 이중 타격이면 주 대상이 이웃의 2배로 깎인다.
// · ArenaRoster: 로스터 에셋 그대로(스킬 포함) — 에셋에 든 값이 판까지 닿는지. 계측 채널 「평타광역」·맞은 수로 본다.
// ⚠️ 표적 체력은 그 유닛 공격력 × 5000으로 맞춘다(float 눈금 ≈ 한 타의 0.03%) — 1e8 고정이면 흔함 평타 24의 85%가 눈금 8에 묻혔다(첫 판).
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:SplashProbe.Arena wait:30 call:SplashProbe.Report
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:SplashProbe.ArenaRoster wait:30 call:SplashProbe.Report
static class SplashProbe
{
    // (라벨, 스플래시 반경, 클리브 비율, 클리브 반경, 방어 있는 표적) — 반경은 원작 단위.
    static readonly (string label, float splash, float cleaveFactor, float cleaveRadius, bool armored)[] Cases =
    {
        ("광역 없음", 0f, 0f, 0f, false),
        ("스플래시 200", 200f, 0f, 0f, false),
        ("스플래시 275", 275f, 0f, 0f, false),
        ("스플래시 300", 300f, 0f, 0f, false),
        ("스플래시 350", 350f, 0f, 0f, false),
        ("스플래시 400", 400f, 0f, 0f, false),
        ("스플래시 300 방어 표적", 300f, 0f, 0f, true),
        ("클리브 0.85·415", 0f, 0.85f, 415f, false),
        ("클리브 0.85·415 방어 표적", 0f, 0.85f, 415f, true),
    };
    static readonly string[] RosterUnits = { "불멸_고도현", "초월_황준석_ADAP", "제한_박성호", "전설적인_이유선", "희귀함_이용민", "희귀함_장태영", "영원_최상호", "전설적인_박성호" };
    const string BaseUnit = "흔함_강주혁";
    const int TargetCount = 9;
    const float RingRadius = 2600f;

    class Slot { public string label; public UnitData data; public readonly List<EnemyDummy> targets = new List<EnemyDummy>(); public readonly List<float> hp0 = new List<float>(); public float spacing; }
    static readonly List<Slot> slots = new List<Slot>();
    static float startTime;

    static string Arena() => Build(false);
    static string ArenaRoster() => Build(true);

    static string Build(bool roster)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        List<EnemyData> enemies = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f).ToList();
        EnemyData bare = enemies.FirstOrDefault(e => e.name.Contains("R01"));
        EnemyData armored = enemies.FirstOrDefault(e => e.name.Contains("R21"));
        UnitData baseData = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{BaseUnit}.asset");
        if (spawner == null || lane == null || bare == null || armored == null || baseData == null) return "❌ 준비 실패";
        slots.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        int count = roster ? RosterUnits.Length : Cases.Length;
        for (int c = 0; c < count; c++)
        {
            UnitData data;
            bool useArmored = false;
            string label;
            if (roster)
            {
                data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{RosterUnits[c]}.asset");
                if (data == null) continue;
                label = $"{RosterUnits[c]}(스플래시 {data.attackSplashRadius} · 클리브 {data.attackCleaveFactor}/{data.attackCleaveRadius})";
            }
            else
            {
                data = Object.Instantiate(baseData);
                data.name = baseData.name + "_" + c;
                data.skill = null; data.skills = new List<SkillData>(); data.critChance = 0f;
                data.attackSplashRadius = Cases[c].splash; data.attackCleaveFactor = Cases[c].cleaveFactor; data.attackCleaveRadius = Cases[c].cleaveRadius;
                useArmored = Cases[c].armored;
                label = Cases[c].label;
            }
            EnemyData enemy = useArmored ? armored : bare;
            var slot = new Slot { label = label, data = data, spacing = 0.65f * enemy.moveSpeed };
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, c * 360f / count, 0f) * Vector3.forward * RingRadius;
            for (int i = 0; i < TargetCount; i++)
            {
                GameObject go = Object.Instantiate(enemy.prefab, home + Vector3.right * (i - TargetCount / 2) * slot.spacing, Quaternion.identity);
                if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
                if (!go.TryGetComponent(out EnemyDummy dummy)) continue;
                dummy.Initialize(enemy, Mathf.Max(1f, data.attackPower) * 5000f / Mathf.Max(1f, enemy.hp));
                dummy.SetLane(-1);
                slot.targets.Add(dummy);
                slot.hp0.Add(dummy.Hp);
            }
            spawner.Spawn(data, home + Vector3.forward * 15f, 0);
            slots.Add(slot);
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        startTime = Time.time;
        return $"배치 {slots.Count} · 표적 {bare.name}(방어 {bare.armor}) / {armored.name}(방어 {armored.armor}) · 간격 {0.65f * bare.moveSpeed:0.0}(세계) = 원작 {0.65f * bare.moveSpeed * WorldScale.Value:0}";
    }

    static string Report()
    {
        var sb = new StringBuilder($"\n게임 시간 {Time.time - startTime:0.0}초 · 표적별 = 깎인 체력 ÷ 가장 많이 깎인 표적(주 대상)");
        foreach (Slot s in slots)
        {
            List<float> lost = s.targets.Select((t, i) => t == null ? s.hp0[i] : s.hp0[i] - t.Hp).ToList();
            float top = Mathf.Max(1f, lost.Max());
            int hits = SkillTelemetry.HitsOf(s.data), splashHits = SkillTelemetry.SplashHitsOf(s.data);
            sb.Append($"\n{s.label}: 평타 {hits}타 · 광역이 맞힌 수 {splashHits}(타당 {(float)splashHits / Mathf.Max(1, hits):0.00}) · 평타 {SkillTelemetry.DamageOf(s.data, "평타"):0} · 평타광역 {SkillTelemetry.DamageOf(s.data, "평타광역"):0}"
                      + $" · 맞은 표적 {lost.Count(x => x > 0f)} · 표적별 {string.Join(" ", lost.Select(x => (x / top).ToString("0.000")))}"
                      + (s.targets.Count > 0 && s.targets[0] != null ? $" · 표적 방어 {s.targets[0].EffectiveArmor:0.0}" : ""));
        }
        return sb.ToString();
    }
}
