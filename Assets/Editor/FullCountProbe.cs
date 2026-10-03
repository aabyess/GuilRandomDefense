using System.IO;
using System.Reflection;
using System.Text;
using System.Linq;
using UnityEngine;

// 풀카운트 점검 — 실제 코드 경로(RoundManager.AdvanceRound·EnemyDummy.OnLaneBossKilled)만 쓴다.
//   gameshot mode:어려움 call:FullCountProbe.Table → call:FullCountProbe.Entry → snap …
static class FullCountProbe
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static RoundManager Rm => Object.FindFirstObjectByType<RoundManager>();

    static string Table()
    {
        var sb = new StringBuilder("배수 표(적 수 → 배수): ");
        foreach (int n in new[] { 0, 1, 2, 10, 25, 49, 50, 51, 80 }) sb.Append($"{n}→{FullCountScore.Multiple(n)}  ");
        sb.AppendLine();
        sb.AppendLine($"시작 점수(슬롯0) {FullCountScore.HostGet(0)} · 보임 {FullCountScore.HostVisible}");
        return sb.ToString();
    }

    static void Advance(RoundManager rm, int finishedRound)
    {
        typeof(RoundManager).GetField("currentRound", Any).SetValue(rm, finishedRound);
        typeof(RoundManager).GetMethod("AdvanceRound", Any).Invoke(rm, null);
    }

    static string Score() => $"점수 {FullCountScore.HostGet(0)} (보임 {FullCountScore.HostVisible})";

    // R59 끝 → R60 시작(신세계 진입) → 64 끝(full_C 5) → 69 끝(10) → 74 끝(15) → 75 끝(클리어)
    static string Entry()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        RoundManager rm = Rm;
        string savedRoot = PersistentSave.SaveRootOverride;
        PersistentSave.SaveRootOverride = Path.Combine(Path.GetTempPath(), "grd_fullcount_probe");
        sb.AppendLine($"전체 라운드 {typeof(RoundManager).GetField("totalRounds", Any).GetValue(rm)} · 시작 {Score()}");
        Advance(rm, 59); sb.AppendLine($"R59 끝(→R60 진입) → {Score()} (기대: 1250 · 보임)");
        Advance(rm, 60); sb.AppendLine($"R60 끝 → {Score()} (기대 +250 = 50×(5+0) → 1500)");
        Advance(rm, 64); sb.AppendLine($"R64 끝 → {Score()} (기대 +250 → 1750, 그 뒤 full_C=5)");
        Advance(rm, 65); sb.AppendLine($"R65 끝 → {Score()} (기대 +50×10=500 → 2250)");
        Advance(rm, 69); sb.AppendLine($"R69 끝 → {Score()} (기대 +500 → 2750, full_C=10)");
        Advance(rm, 70); sb.AppendLine($"R70 끝 → {Score()} (기대 +50×15=750 → 3500)");
        Advance(rm, 74); sb.AppendLine($"R74 끝 → {Score()} (기대 +750 → 4250, full_C=15)");
        Advance(rm, 75); sb.AppendLine($"R75 끝(클리어) → {Score()} (기대 +50×20=1000 +500(×10) +1500(최상위 0기) → 7250)");
        PersistentSave.SaveRootOverride = savedRoot;
        return sb.ToString();
    }

    // 신세계 보스 처치: 보스가 뜨고 N초 뒤 이 함수를 부른다(빠른 판 ≤17.5초 / 느린 판).
    static string KillBoss()
    {
        EnemyDummy boss = EnemyDummy.Active.FirstOrDefault(e => e != null && e.IsBoss && e.LaneIndex == 0);
        if (boss == null) return $"❌ 0번 레인 보스 없음 · {Score()}";
        int before = FullCountScore.HostGet(0);
        boss.TakeDamage(1e12f, DamageType.AP, AttackType.Spells, 0);
        return $"보스 {boss.name} R{boss.SpawnRound} 처치 요청 · 점수 {before} → (다음 프레임 반영)";
    }

    static string Now() => Score();
}
