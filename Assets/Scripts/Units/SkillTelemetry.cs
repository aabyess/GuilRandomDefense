using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 스킬 발동 계측(2026-09-29, 「스킬 전체 살리기」 1단계) — 유닛 종류별로 스킬 시전 횟수와
// 평타·치명·스킬이 실제로 깎은 체력(방어·감폭 뒤, 적 hp 차이)을 모은다.
// 꺼져 있으면(기본) 아무것도 안 한다 — 점검 프로브가 Enabled를 켜고 Report를 읽는다.
public static class SkillTelemetry
{
    public static bool Enabled;

    class UnitStats
    {
        public readonly Dictionary<string, int> casts = new Dictionary<string, int>();
        public readonly Dictionary<string, float> damage = new Dictionary<string, float>();
        // 시전까지 못 간 판정이 어느 자리에서 빠졌나(「스킬이름: 사유」 → 횟수). 2026-09-29 미발동 원인 규명.
        public readonly Dictionary<string, int> gates = new Dictionary<string, int>();
        public int splashHits;   // 평타 광역(스플래시·클리브)이 주 대상 말고 맞힌 적 수 누계
        public int hits;   // 평타가 맞아 스킬 판정(TryCastOnHitSkill)에 들어간 횟수 — 게이지·저확률 판정의 표본 수
    }

    static readonly Dictionary<UnitData, UnitStats> stats = new Dictionary<UnitData, UnitStats>();

    // 시전 시각 기록(2026-09-30 마나 재생 판 측정) — 게이지 스킬의 발동 간격을 재려면 횟수만으론 모자라다.
    // hits는 그 시전이 나간 평타가 그 유닛 종류의 몇 번째 판정이었나(간격을 타수로도 읽게).
    public readonly struct CastEvent
    {
        public readonly UnitData unit; public readonly string skill; public readonly float time; public readonly int hits;
        public CastEvent(UnitData unit, string skill, float time, int hits) { this.unit = unit; this.skill = skill; this.time = time; this.hits = hits; }
    }
    public static readonly List<CastEvent> CastLog = new List<CastEvent>();

    public static void Reset() { stats.Clear(); CastLog.Clear(); }

    /// <summary>계측에 잡힌 유닛 종류 전부와, 한 유닛의 피해 채널 이름들. 밸런스 판 요약용(2026-09-30).</summary>
    public static IEnumerable<UnitData> TrackedUnits => stats.Keys;
    public static IEnumerable<string> ChannelsOf(UnitData unit) =>
        unit != null && stats.TryGetValue(unit, out UnitStats s) ? (IEnumerable<string>)s.damage.Keys : System.Array.Empty<string>();

    public static int HitsOf(UnitData unit) => unit != null && stats.TryGetValue(unit, out UnitStats s) ? s.hits : 0;

    static UnitStats For(UnitData unit)
    {
        if (!stats.TryGetValue(unit, out UnitStats s)) stats[unit] = s = new UnitStats();
        return s;
    }

    public static void Cast(UnitData unit, SkillData skill)
    {
        if (!Enabled || unit == null) return;
        string key = skill != null ? (string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName) : "?";
        Dictionary<string, int> casts = For(unit).casts;
        casts.TryGetValue(key, out int n);
        casts[key] = n + 1;
        CastLog.Add(new CastEvent(unit, key, Time.time, For(unit).hits));
    }

    public static void Hit(UnitData unit)
    {
        if (!Enabled || unit == null) return;
        For(unit).hits++;
    }

    public static void SplashHit(UnitData unit)
    {
        if (!Enabled || unit == null) return;
        For(unit).splashHits++;
    }

    public static int SplashHitsOf(UnitData unit) => unit != null && stats.TryGetValue(unit, out UnitStats s) ? s.splashHits : 0;

    /// <summary>스킬 판정이 시전 전에 빠진 자리를 센다(UnitAttacker의 continue·return마다 한 줄). skill이 null이면 유닛 단위 사유.</summary>
    public static void Gate(UnitData unit, SkillData skill, string reason)
    {
        if (!Enabled || unit == null) return;
        string key = (skill != null ? (string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName) : "(유닛)") + ": " + reason;
        Dictionary<string, int> gates = For(unit).gates;
        gates.TryGetValue(key, out int n);
        gates[key] = n + 1;
    }

    // hpBefore는 TakeDamage 직전의 적 체력 — 실제로 깎인 양만 센다(넘친 피해는 안 셈).
    public static void Damage(UnitData unit, string channel, EnemyDummy target, float hpBefore)
    {
        if (!Enabled || unit == null || target == null) return;
        float dealt = hpBefore - Mathf.Max(0f, target.Hp);
        if (dealt <= 0f) return;
        Dictionary<string, float> damage = For(unit).damage;
        damage.TryGetValue(channel, out float d);
        damage[channel] = d + dealt;
    }

    /// <summary>한 유닛이 채널(「스킬」「스킬%HP」「치명」 등)로 낸 누적 피해. 탐침용.</summary>
    public static float DamageOf(UnitData unit, string channel) =>
        unit != null && stats.TryGetValue(unit, out UnitStats s) && s.damage.TryGetValue(channel, out float d) ? d : 0f;

    public static string Report(IEnumerable<UnitData> fielded)
    {
        StringBuilder sb = new StringBuilder();
        List<UnitData> withSkill = fielded.Where(u => u != null && ((u.skill != null) || (u.skills != null && u.skills.Any(s => s != null))))
                                          .Distinct().OrderBy(u => (int)u.grade).ThenBy(u => u.name).ToList();
        int fired = 0;
        float totalBasic = 0f, totalSkill = 0f;
        foreach (UnitData u in withSkill)
        {
            stats.TryGetValue(u, out UnitStats s);
            int casts = s != null ? s.casts.Values.Sum() : 0;
            float basic = s != null ? s.damage.Where(kv => !kv.Key.StartsWith("스킬")).Sum(kv => kv.Value) : 0f;
            float skill = s != null ? s.damage.Where(kv => kv.Key.StartsWith("스킬")).Sum(kv => kv.Value) : 0f;
            totalBasic += basic; totalSkill += skill;
            if (casts > 0) fired++;
            string castText = s != null && s.casts.Count > 0 ? string.Join(", ", s.casts.Select(kv => $"{kv.Key}×{kv.Value}")) : "—";
            float share = basic + skill > 0f ? skill / (basic + skill) : 0f;
            sb.AppendLine($"   {(casts > 0 ? "✅" : "❌")} [{u.grade.KoreanName()}] {u.name} · 시전 {casts} ({castText}) · 판정 {(s != null ? s.hits : 0)}타 · 평타 {basic:F0} · 스킬 {skill:F0} ({share:P0})");
            if (s != null && s.gates.Count > 0)
                sb.AppendLine("        ↳ 빠진 자리: " + string.Join(" · ", s.gates.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
        }
        float totalShare = totalBasic + totalSkill > 0f ? totalSkill / (totalBasic + totalSkill) : 0f;
        return $"   스킬 가진 유닛 {withSkill.Count}종 중 발동 {fired}종 · 전체 피해 중 스킬 {totalShare:P1} (평타 {totalBasic:F0} / 스킬 {totalSkill:F0})\n" + sb;
    }
}
