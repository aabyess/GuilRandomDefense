using System.Text;
using UnityEngine;

// 정의문 사슬 점검(10-03) — gameshot call:JusticeQuestProbe.Break wait:3 call:JusticeQuestProbe.Report call:JusticeQuestProbe.Kill wait:3 call:JusticeQuestProbe.Report
static class JusticeQuestProbe
{
    static string State()
    {
        var sb = new StringBuilder();
        foreach (PlayerContext c in PlayerContext.Occupied)
            sb.AppendLine($"  P{c.PlayerId} 골드 {c.GoldWallet.Gold} · 특성 {c.UnitUpgrades.TraitPoints} · 세이브(판) {c.PersistentSave.SessionPoints} · 죽음 {c.IsDead}");
        sb.AppendLine($"  팀버프 공격력 +{TeamBuffs.AttackPowerPercent:P0} · 공속 +{TeamBuffs.AttackSpeedPercent:P0} · 적 이속 ×{TeamBuffs.EnemySlowMultiplier:F2}");
        int dogs = 0;
        foreach (EnemyDummy e in EnemyDummy.Active)
            if (e != null && e.Data != null && e.Data.enemyName.StartsWith("[퀘스트]")) { dogs++; sb.AppendLine($"  제독 「{e.Data.enemyName}」 HP {e.Hp:N0}/{e.MaxHp:N0} 위치 {e.transform.position:F1} 방어 {e.Data.armor}"); }
        sb.AppendLine($"  제독 수 {dogs}");
        sb.Append(Effects());
        foreach (EnemyDummy e in EnemyDummy.Active)
            if (e != null && e.LaneIndex < 0) sb.AppendLine($"  [레인-1] {e.name} 「{(e.Data != null ? e.Data.enemyName : "데이터없음")}」 {e.transform.position:F1}");
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.StartsWith("Mob_유재헌") || go.name.StartsWith("Mob_김정래") || go.name.StartsWith("Mob_박예원")) sb.AppendLine($"  씬 객체 {go.name} {go.transform.position:F1} active={go.activeInHierarchy}");
        JusticeGateQuest q = Object.FindFirstObjectByType<JusticeGateQuest>();
        sb.AppendLine(q == null ? "  JusticeGateQuest 없음" : $"  JusticeGateQuest {q.transform.position:F1} enabled={q.enabled}");
        return sb.ToString();
    }

    static string Break1() { JusticeGateQuest.ForcedKind = 1; return Break(); }
    static string Break2() { JusticeGateQuest.ForcedKind = 2; return Break(); }
    static string Break3() { JusticeGateQuest.ForcedKind = 3; return Break(); }

    // 버프가 실제로 먹는 값 — 공격력·공격 간격(내 유닛 하나)·적 이속 배율(적 하나, 레인 적)
    static string Effects()
    {
        var sb = new StringBuilder();
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        { sb.AppendLine($"  내 유닛 {a.name} 공격력 {a.AttackDamage:F2} · 공격간격 {a.AttackInterval:F4}s"); break; }
        foreach (EnemyDummy e in EnemyDummy.Active)
            if (e != null && e.LaneIndex >= 0) { sb.AppendLine($"  적 {e.name} 이속배율 {e.EffectiveSlowMultiplier:F4} · 실효 이속 {e.MoveSpeed:F2}"); break; }
        return sb.ToString();
    }

    static string Break()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder("문 부수기 전\n" + State());
        DestructibleGate gate = null;
        foreach (DestructibleGate g in DestructibleGate.Active) gate = g;
        if (gate == null) return sb + "❌ 활성 문 없음";
        sb.AppendLine($"문 HP {gate.Hp:N0}/{gate.MaxHp:N0}");
        gate.TakeDamage(float.MaxValue);
        sb.AppendLine("부숨 → 문 IsBroken=" + gate.IsBroken);
        return sb.ToString();
    }

    static string Kill()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        foreach (EnemyDummy e in EnemyDummy.Active)
            if (e != null && e.Data != null && e.Data.enemyName.StartsWith("[퀘스트]"))
            {
                e.TakeDamage(float.MaxValue, DamageType.AD, AttackType.Normal, 0, 1f, false);
                return $"제독 「{e.Data.enemyName}」 처치 시도 → IsDead={e.IsDead}";
            }
        return "❌ 제독 없음";
    }

    static string Report() => State();
}
