using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 게임 화면에 내보낼 설명 글(사장님 10-06 「이상한 설명 쓸데없는 것들 — 해당 기능 설명이 아니라 이런 거 없애」).
/// 데이터의 description 칸엔 개발 메모(원작 능력 ID·트리거 이름·정정 날짜·문서 경로 …)가 섞여 있다(10-06 집계 ≈470개).
/// 에셋은 그대로 두고(메모는 개발 근거라 남긴다) **보여 줄 때만** 거른다:
///   · 스킬: 메모가 섞였으면 설명 대신 실제 수치(발동 방식·피해·확률·범위·쿨타임 …)로 만든 요약을 보인다.
///   · 그 밖(강화소·도박·도움소): 메모가 든 괄호·마디만 떼고, 남는 게 없으면 비운다.
/// 새 Apply가 메모를 또 넣어도 화면엔 안 나온다 — 고칠 곳은 이 파일 한 곳.
/// </summary>
public static class PlayerFacingText
{
    // 개발 메모의 흔적. 플레이어 설명에 나올 일이 없는 말만 둔다.
    static readonly Regex DevMark = new Regex(
        @"원작|w3a|w3u|w3q|트리거|Trig_|정정|RRD|\b[AHRIh][0-9A-Z][0-9A-Za-z]{2}\b|\bj:\d|PM\b|사장님|제안값|구현담당|리서치담당|blender|\.md\b|\.csv\b|0x[0-9a-f]|\(\d{2}-\d{2}|20\d\d-\d\d|대응표|upgr|gba|gmo|glvl|해석|실측|탐침|Apply|직렬화|에셋|필드|레벨\d블록|GetUnit|순위배정|게이트별|배정\(|\b[a-z]+[A-Z][A-Za-z0-9]+\b|\.cs\b|잘못|고쳤|가설|되돌리|확정값|\*\*|주석|이전엔|헷갈리|확인돼|미확인|안 옮|[A-Z][a-z]{2,}[A-Z]\w*/|\b[A-Z][a-z]+_|=\d|안 바꿈|별개 능력|이름만 비슷|부가 버프",
        RegexOptions.Compiled);

    public static bool HasDevNotes(string text) => !string.IsNullOrEmpty(text) && DevMark.IsMatch(text);

    /// <summary>메모가 든 괄호·마디를 뗀 글. 남는 게 없으면 "".</summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text) || !HasDevNotes(text)) return text ?? "";
        string t = Regex.Replace(text, @"\s+", " ");
        // 괄호 안에 메모가 있으면 괄호째 뺀다(안쪽 괄호부터 몇 번)
        for (int i = 0; i < 3; i++)
            t = Regex.Replace(t, @"[\(（\[][^\(\)（）\[\]]*[\)）\]]", m => DevMark.IsMatch(m.Value) ? "" : m.Value);
        var kept = new List<string>();
        foreach (string part in Regex.Split(t, @"(?<=[.。!?])\s+| — | – |\s*·\s*|;\s*"))
        {
            string p = part.Trim();
            if (p.Length == 0 || DevMark.IsMatch(p)) continue;
            kept.Add(p);
        }
        return string.Join(" ", kept).Trim();
    }

    /// <summary>강화소 칸 설명 — 메모가 섞였으면 기본 문장(수치 줄은 상점이 따로 붙인다).</summary>
    public static string TrackDescription(string text, string fallback = "해당 등급 유닛 전체를 영구히 강화합니다.")
    {
        if (!HasDevNotes(text)) return text ?? "";
        string c = Clean(text);
        return c.Length >= 8 ? c : fallback;
    }

    /// <summary>스킬 이름 — 「이름 — 메모」 꼴이면 앞만.</summary>
    public static string SkillName(SkillData skill)
    {
        string name = skill != null ? skill.skillName ?? "" : "";
        int dash = name.IndexOf('—');
        if (dash > 0) name = name.Substring(0, dash);   // 「이름 — 메모·수치」는 앞만(수치는 설명 줄에 따로 나온다)
        name = Regex.Replace(name.Trim(), @"^(?:(?:[A-Z][0-9A-Z]{3}|k\d+|Unique\d+|[A-Za-z]+_[A-Za-z0-9_]+)\s+)+", "");   // 앞머리 능력 코드
        name = Regex.Replace(name, @"\s+(?:\d+/\d+|Lv\d+|AND|\d+범위)(?=\s|$)", "");                                   // 「1/10」·「Lv2」 같은 꼬리
        name = name.TrimStart('!', ' ');
        name = Regex.Replace(name, @"^(?:cat\d+|\d+범위)\s+", "");
        if (Regex.IsMatch(name, @"^(?:\d+/\d+|Lv\d+|AND)?$")) name = "고유 능력";   // 이름이 코드뿐이던 것
        return string.IsNullOrWhiteSpace(name) ? (skill != null ? skill.skillName : "") : name.Trim();
    }

    /// <summary>스킬 설명 — 메모가 없으면 적힌 그대로, 있으면 수치 요약.</summary>
    public static string SkillDescription(SkillData skill)
    {
        if (skill == null) return "";
        if (!HasDevNotes(skill.description)) return skill.description ?? "";
        return Summary(skill);
    }

    public static string Summary(SkillData skill)
    {
        if (skill == null || skill.levels == null || skill.levels.Count == 0) return "";
        SkillLevel l = skill.levels[0];
        var sb = new StringBuilder(Trigger(skill.triggerType, l));
        var parts = new List<string>();
        if (l.effects != null)
            foreach (SkillEffect e in l.effects)
            {
                string p = Effect(e);
                if (!string.IsNullOrEmpty(p) && !parts.Contains(p)) parts.Add(p);
            }
        if (parts.Count > 0) sb.Append(sb.Length > 0 ? " — " : "").Append(string.Join(", ", parts));
        if (l.range > 0f && skill.triggerType != SkillTriggerType.Aura) sb.Append($" (범위 {l.range:0})");
        return sb.ToString();
    }

    static string Pct(float v) => $"{v * 100f:0.#}%";

    static string Trigger(SkillTriggerType t, SkillLevel l)
    {
        switch (t)
        {
            case SkillTriggerType.OnHitChance:
                return (l.triggerChance >= 0.999f ? "평타마다" : $"평타 {Pct(l.triggerChance)} 확률") + (l.cooldown > 0f ? $"(재사용 {l.cooldown:0.#}초)" : "");
            case SkillTriggerType.OnHitCount:
                return l.gaugeKind == SkillGaugeKind.Life ? $"체력 게이지 {l.hitCountThreshold} 차면" : $"마나 {l.hitCountThreshold} 차면";
            case SkillTriggerType.CooldownAutoCast: return l.cooldown > 0f ? $"{l.cooldown:0.#}초마다" : "자동";
            case SkillTriggerType.Aura: return l.range > 0f ? $"주변 {l.range:0} 오라" : "상시";
            case SkillTriggerType.OnEnemyEnterRange: return "적이 가까이 오면";
            case SkillTriggerType.ActiveButton: return l.cooldown > 0f ? $"누르는 스킬(쿨 {l.cooldown:0.#}초)" : "누르는 스킬";
        }
        return "";
    }

    static string Who(SkillEffect e)
    {
        switch (e.target)
        {
            case SkillTargetKind.Self: return "자신";
            case SkillTargetKind.Allies: return "아군";
            case SkillTargetKind.SingleTarget: return "대상";
            case SkillTargetKind.RandomEnemyInRange: return "무작위 적";
            case SkillTargetKind.ChainEnemies: return "연쇄";
            default: return "범위 적";
        }
    }

    static string Amount(SkillEffect e)
    {
        switch (e.basis)
        {
            case SkillEffectBasis.TargetMaxHpPercent: return $"최대 체력의 {Pct(e.multiplier)}";
            case SkillEffectBasis.TargetCurrentHpPercent: return $"현재 체력의 {Pct(e.multiplier)}";
            case SkillEffectBasis.TargetMissingHpPercent: return $"잃은 체력의 {Pct(e.multiplier)}";
            case SkillEffectBasis.CasterAttackPower:
                if (e.multiplier > 0f) return $"공격력의 {e.multiplier:0.##}배" + (e.bonus > 0f ? $" +{e.bonus:N0}" : "");
                return e.bonus > 0f ? $"공격력 +{e.bonus:N0}" : "";
            case SkillEffectBasis.Flat: return e.multiplier > 10f ? $"{e.multiplier:N0}" : "";   // 1·3 같은 자리표시 값은 말하지 않는다
            default: return "";
        }
    }

    static string Effect(SkillEffect e)
    {
        if (e == null) return "";
        string ignore = e.armorIgnoreRatio >= 0.999f ? " 방어 무시" : e.armorIgnoreRatio > 0f ? $" 방어 {Pct(e.armorIgnoreRatio)} 무시" : "";
        string dur = e.duration > 0f ? $" {e.duration:0.#}초" : "";
        switch (e.kind)
        {
            case SkillEffectKind.Damage: { string a = Amount(e); return a.Length == 0 ? "" : $"{Who(e)}에게 {a} 피해{ignore}"; }
            case SkillEffectKind.DamageOverTime: { string a = Amount(e); return a.Length == 0 ? "지속 피해" : $"{Who(e)}에게 지속 피해 {a}{dur}"; }
            case SkillEffectKind.Stun: return $"스턴{(e.duration > 0f ? $" {e.duration:0.#}초" : "")}";
            case SkillEffectKind.Slow: return $"적 이동속도 −{Pct(1f - e.multiplier)}{dur}";   // multiplier = 남는 속도 배율
            case SkillEffectKind.ArmorBreak: return $"적 방어력 −{e.multiplier:0.#}{dur}";
            case SkillEffectKind.ArmorBonus: return e.multiplier < 0f ? $"적 방어력 −{-e.multiplier:0.#}" : $"방어력 +{e.multiplier:0.#}";
            case SkillEffectKind.AttackSpeedBuffPercent: return $"{Who(e)} 공격속도 +{Pct(e.multiplier)}{dur}";
            case SkillEffectKind.AttackPowerBuffPercent: return $"{Who(e)} 공격력 +{Pct(e.multiplier)}{dur}";
            case SkillEffectKind.AttackPowerBuffFlat: return $"{Who(e)} 공격력 +{e.multiplier:N0}{dur}";
            case SkillEffectKind.ExtraProjectile: return $"추가 공격 {e.multiplier:0}발";
            case SkillEffectKind.BossDamageMultiplier: return $"보스에게 피해 ×{e.multiplier:0.##}";
            case SkillEffectKind.StoryDamageMultiplier: return $"스토리 적에게 피해 ×{e.multiplier:0.##}";
            case SkillEffectKind.SplashDamageMultiplier: return $"범위 피해 ×{e.multiplier:0.##}";
            case SkillEffectKind.SummonUnit: return "소환";
            case SkillEffectKind.KillNormalEnemies: return "일반 적 처치";
            case SkillEffectKind.HealOverTime: return "체력 회복";
            case SkillEffectKind.DispelAllyDebuffs: return "아군 약화 해제";
            case SkillEffectKind.Knockback: return "밀쳐내기";
            case SkillEffectKind.TeleportToPoint: return "지정한 곳으로 순간이동";
            case SkillEffectKind.GoldPlusBonus: return $"처치 골드 +{Pct(e.multiplier)}";
            case SkillEffectKind.AttackSpeedStack: return $"공격마다 공격속도 누적(최대 +{Pct(e.bonus)})";
            case SkillEffectKind.AttackDamageStack: return $"공격마다 피해 누적(최대 +{Pct(e.bonus)})";
            case SkillEffectKind.AllySkillDamageBonus: return $"아군 스킬 피해 +{Pct(e.multiplier)}";
            case SkillEffectKind.AllyMoveSpeedDebuff: return $"아군 이동속도 −{Pct(e.multiplier)}";
            case SkillEffectKind.GrantLoot: return "노획물 획득";
            case SkillEffectKind.DamagePerTargetArmorShred: return "적 방어가 깎인 만큼 피해 증가";
            case SkillEffectKind.SkillDamagePerHighGradeUnit: return "전설 이상 유닛 수만큼 범위 피해 증가";
            case SkillEffectKind.DamageGrowthOverTime: return "시간이 갈수록 피해 증가";
            case SkillEffectKind.FormChange: return "변신";
        }
        return "";   // 꼬리표·내부용 효과는 말하지 않는다
    }
}
