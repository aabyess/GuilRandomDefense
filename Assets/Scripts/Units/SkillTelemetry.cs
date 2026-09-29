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
    }

    static readonly Dictionary<UnitData, UnitStats> stats = new Dictionary<UnitData, UnitStats>();

    public static void Reset() => stats.Clear();

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
            sb.AppendLine($"   {(casts > 0 ? "✅" : "❌")} [{u.grade.KoreanName()}] {u.name} · 시전 {casts} ({castText}) · 평타 {basic:F0} · 스킬 {skill:F0} ({share:P0})");
        }
        float totalShare = totalBasic + totalSkill > 0f ? totalSkill / (totalBasic + totalSkill) : 0f;
        return $"   스킬 가진 유닛 {withSkill.Count}종 중 발동 {fired}종 · 전체 피해 중 스킬 {totalShare:P1} (평타 {totalBasic:F0} / 스킬 {totalSkill:F0})\n" + sb;
    }
}
