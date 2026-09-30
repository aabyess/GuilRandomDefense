using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 마나 재생 축(32a10cfa, 원작 umpr·umpm) 판 측정 — 마나 게이지 스킬의 발동 간격(게임 시간 초)과 그 사이 평타 수.
// 유닛마다 떨어진 자리에 죽지 않는 표적 셋을 세우고, 같은 유닛을 둘 세운다: 에셋 그대로(재생 켬)와
// 재생 필드 다섯을 0으로 한 실행 중 복제본(재생 끔 = 09-30 이전 동작 — 타수만 세는 게이지).
// 간격은 SkillTelemetry.CastLog(시전 시각·그때까지 판정 타수)에서 읽는다.
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:ManaRegenProbe.Arena wait:350 call:ManaRegenProbe.Report
// ArenaAll·ArenaAll1x(2026-09-30 체력 재생·시작값·평타 타이머): 마나 다섯 + 체력 게이지·시작 마나 로스터 전부, 2배속/1배속.
// ⚠️ R10 보스 제한 패배(게임 시간 약 458초)가 나면 평타가 멎는다 — 그 안에 끝낸다.
static class ManaRegenProbe
{
    // 징베 h03G · 스모커(Legend9) h02V · 우타 h067 · 코알라 h03V · 상디 H09G
    static readonly string[] Units = { "전설적인_정윤식", "전설적인_정준영", "영원_김정래", "히든_황정기", "초월_배성령_AD" };
    static readonly string[] LifeUnits =
    {
        "랜덤_미도리야_이즈쿠", "랜덤_이즈미_신이치", "불멸_신지우", "불멸_이이삭", "영원_이지원", "전설적인_임채현", "전설적인_진연서",
        "전설적인_최상호", "전설적인_홍인창", "제한_김민규", "제한_이유범", "초월_강재규_AP", "초월_임채민_AP", "초월_구주호_AD",
    };
    const float RingRadius = 2000f;   // BossPercentProbe와 같은 거리 — 이웃 간격 2π·2000/10 ≈ 1257 > 사거리·범위

    class Slot { public float firstHit = -1f, lastHit; public int firstHits, lastHits; public string label; public UnitData data; public UnitAttacker attacker; public Vector3 home; public readonly List<EnemyDummy> targets = new List<EnemyDummy>(); }

    static readonly List<Slot> slots = new List<Slot>();
    static EnemyData dummyData;
    static float startTime;
    static int frames;

    static string Arena() => Run(Units, 2f);
    static string ArenaAll() => Run(Units.Concat(LifeUnits).ToArray(), 2f);
    static string ArenaAll1x() => Run(Units.Concat(LifeUnits).ToArray(), 1f);

    // 마나 재생 오라(2026-09-30) — 네 배치: 징베 혼자 / 징베(오라 끔) 혼자 / 징베+상디 / 징베+코알라+상디.
    // 배치마다 유닛은 이름 다른 복제본(계측이 UnitData별이라). 기대(우리 평타 주기): 징베 27.8 · 끔 54.4 · 곁 상디 22.1 · 둘 곁 상디 13.6.
    static string ArenaAura()
    {
        string[][] groups =
        {
            new[] { "전설적인_정윤식" },
            new[] { "전설적인_정윤식!" },   // ! = 오라 필드 0
            new[] { "전설적인_정윤식", "초월_배성령_AD" },
            new[] { "전설적인_정윤식", "히든_황정기", "초월_배성령_AD" },
            new[] { "초월_배성령_AD" },
        };
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f && e.name.Contains("R2"));
        if (spawner == null || lane == null || dummyData == null) return "❌ UnitSpawner·레인·표적 적 없음";
        slots.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        for (int g = 0; g < groups.Length; g++)
        {
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, g * 360f / groups.Length, 0f) * Vector3.forward * RingRadius;
            for (int m = 0; m < groups[g].Length; m++)
            {
                bool auraOff = groups[g][m].EndsWith("!");
                UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{groups[g][m].TrimEnd('!')}.asset");
                if (source == null) continue;
                UnitData data = Object.Instantiate(source);
                data.name = $"배치{g + 1}({string.Join("+", groups[g].Select(x => x.Split('_')[1]))}) {source.name}";
                if (auraOff) data.manaAuraRegenPerSecond = 0f;
                var slot = new Slot { label = data.name, data = data, home = home };
                if (m == 0) for (int i = 0; i < 3; i++) slot.targets.Add(SpawnTarget(home, i));
                GameObject go = spawner.Spawn(data, home + Vector3.right * 10f * m, 0);
                if (go != null) slot.attacker = go.GetComponentInChildren<UnitAttacker>();
                slots.Add(slot);
            }
        }
        SetDeathCount(false);
        startTime = Time.time;
        frames = 0;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        Time.timeScale = 2f;
        return $"오라 배치 {groups.Length} · 유닛 {slots.Count(x => x.attacker != null)}/{slots.Count} · 2배속";
    }

    static string Run(string[] Units, float timeScale)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f && e.name.Contains("R2"));
        if (spawner == null || lane == null || dummyData == null) return "❌ UnitSpawner·레인·표적 적 없음";

        slots.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        Vector3 c = lane.LaneCenter;
        var sb = new StringBuilder();
        int n = Units.Length * 2;
        for (int u = 0; u < Units.Length; u++)
        {
            UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Units[u]}.asset");
            if (source == null) { sb.Append($"{Units[u]} 없음 · "); continue; }
            for (int v = 0; v < 2; v++)
            {
                UnitData data = source;
                if (v == 1)
                {
                    data = Object.Instantiate(source);
                    data.name = source.name + "(재생끔)";
                    data.manaRegenPerSecond = 0f; data.manaMax = 0f; data.manaGaugePerMana = 0f;
                    data.lifeGaugeRegenPerSecond = 0f; data.lifeGaugeMax = 0f;
                    data.manaGaugeStart = 0f; data.lifeGaugeStart = 0f; data.lifeGaugeCustomHitGain = false; data.lifeGaugeHitGain = 0f;
                }
                var slot = new Slot { label = data.name, data = data };
                slot.home = c + Quaternion.Euler(0f, (u * 2 + v) * 360f / n, 0f) * Vector3.forward * RingRadius;
                for (int i = 0; i < 3; i++) slot.targets.Add(SpawnTarget(slot.home, i));
                GameObject go = spawner.Spawn(data, slot.home, 0);
                if (go != null) { slot.attacker = go.GetComponentInChildren<UnitAttacker>(); sb.Append($"{data.name} · "); }
                slots.Add(slot);
            }
        }
        SetDeathCount(false);
        startTime = Time.time;
        frames = 0;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        Time.timeScale = timeScale;
        return $"표적 {dummyData.name}×3(체력 ×1e6, 죽으면 다시 세움) · 유닛 {slots.Count(x => x.attacker != null)}/{n} · {timeScale}배속";
    }

    static EnemyDummy SpawnTarget(Vector3 home, int i)
    {
        Vector3 at = home + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 4f;
        GameObject go = Object.Instantiate(dummyData.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
        dummy.Initialize(dummyData, 1e6f);
        dummy.SetLane(-1);
        return dummy;
    }

    static void Watch()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Watch; return; }
        frames++;
        foreach (Slot s in slots)
        {
            // 평타 주기 = (마지막 타 시각 − 첫 타 시각) ÷ 그 사이 타수 — 틱마다 타수가 늘었는지 본다(오차 ≤ 1틱).
            int h = SkillTelemetry.HitsOf(s.data);
            if (h > s.lastHits) { if (s.firstHit < 0f) { s.firstHit = Time.time; s.firstHits = h; } s.lastHit = Time.time; s.lastHits = h; }
        }
        foreach (Slot s in slots)
            for (int i = 0; i < s.targets.Count; i++)
                if (s.targets[i] == null || s.targets[i].IsDead) s.targets[i] = SpawnTarget(s.home, i);
    }

    static readonly System.Reflection.FieldInfo DeathCountField =
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    static readonly System.Reflection.FieldInfo ManaCounterField =
        typeof(UnitAttacker).GetField("manaGaugeCounter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    static void SetDeathCount(bool enabled)
    {
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        if (rm != null && DeathCountField != null) DeathCountField.SetValue(rm, enabled);
    }

    // 그 유닛의 게이지 스킬 이름(CastLog 키)과 문턱(바닥 모드면 바닥).
    static IEnumerable<(string key, int threshold)> ManaGaugeSkills(UnitData u)
    {
        for (int s = 0; s < u.SkillCount; s++)
        {
            SkillData skill = u.SkillAt(s);
            if (skill == null || skill.triggerType != SkillTriggerType.OnHitCount) continue;
            foreach (SkillLevel lv in skill.levels)
                if (lv.hitCountThreshold > 0 || lv.hitCountFloor > 0)
                {
                    yield return ((lv.gaugeKind == SkillGaugeKind.Mana ? "[마나] " : "[체력] ") + (string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName),
                                  lv.hitCountThreshold > 0 ? lv.hitCountThreshold : lv.hitCountFloor);
                    break;
                }
        }
    }

    static string Report()
    {
        EditorApplication.update -= Watch;
        SetDeathCount(true);
        Time.timeScale = 1f;
        float elapsed = Mathf.Max(0.01f, Time.time - startTime);
        var sb = new StringBuilder($"\n게임 시간 {elapsed:0.0}초 · 에디터 틱 {frames}(평균 {elapsed / Mathf.Max(1, frames) * 1000f:0.0}ms 게임시간/틱) · 간격 = 연달은 두 시전 사이 게임 시간");
        foreach (Slot s in slots)
        {
            UnitData u = s.data;
            int hits = SkillTelemetry.HitsOf(u);
            float interval = s.attacker != null ? s.attacker.AttackInterval : 0f;
            int gauge = s.attacker != null && ManaCounterField != null ? (int)ManaCounterField.GetValue(s.attacker) : -1;
            // ⚠️ 판 전체 시간 ÷ 타수는 쓰지 않는다 — 판 도중 패배 처리로 평타가 멎으면 주기가 부풀어 보인다(첫 판에서 1.6배). 주기는 시전 간격 ÷ 타수로 읽는다.
            float period = s.lastHits > s.firstHits ? (s.lastHit - s.firstHit) / (s.lastHits - s.firstHits) : 0f;
            sb.Append($"\n{s.label}: 판정 {hits}타 · 평타 주기 설정 {interval:0.0000}초 / 실측 {period:0.0000}초({(interval > 0f ? (period / interval - 1f) * 100f : 0f):+0.0;-0.0}%) · 체력 재생 {u.lifeGaugeRegenPerSecond}/초 상한 {u.lifeGaugeMax} 시작 {u.lifeGaugeStart} · 마나 시작 {u.manaGaugeStart} · 오라 {u.manaAuraRegenPerSecond}/{u.manaAuraRange} · 재생 {u.manaRegenPerSecond}/초 · 상한 {u.manaMax} · 환산 {u.manaGaugePerMana} · 끝날 때 게이지 {gauge}");
            // 공유 게이지라 같은 타에 여럿이 같이 나간다 — 문턱이 같은 것 중 확률 판정 없는 첫 스킬의 시전만 센다.
            foreach ((string key, int threshold) in ManaGaugeSkills(u))
            {
                List<SkillTelemetry.CastEvent> casts = SkillTelemetry.CastLog.Where(e => e.unit == u && e.skill == key.Substring(5)).ToList();
                sb.Append($"\n    「{(key.Length > 40 ? key.Substring(0, 40) : key)}」 문턱 {threshold} · 시전 {casts.Count}");
                if (casts.Count == 0) continue;
                sb.Append($" · 첫 시전 {casts[0].time - startTime:0.00}초({casts[0].hits}타째)");
                if (casts.Count < 2) continue;
                var gaps = new List<float>(); var hitGaps = new List<int>();
                for (int i = 1; i < casts.Count; i++) { gaps.Add(casts[i].time - casts[i - 1].time); hitGaps.Add(casts[i].hits - casts[i - 1].hits); }
                sb.Append($" · 간격 평균 {gaps.Average():0.00}초(최소 {gaps.Min():0.00}·최대 {gaps.Max():0.00}) · 타수 {string.Join("/", hitGaps.Distinct().OrderBy(x => x))} · 실측 평타 주기 {gaps.Sum() / Mathf.Max(1, hitGaps.Sum()):0.0000}초");
            }
        }
        string report = sb.ToString();
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../ClaudeBridge/shots/mana_regen_report.txt"), report);
        return report;
    }
}
