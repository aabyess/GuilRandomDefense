using System.Linq;
using System.Text;
using UnityEngine;

// PachinkoPlan.Roll 10만 회 표본(구현담당1, 10-16) — 테마별 결과 분포·PUSH 성공률·판 내부 일관성·Pack/Unpack 왕복
static class PachinkoPlanSample
{
    static string Run()
    {
        const int N = 100000;
        var sb = new StringBuilder();
        int bad = 0;
        for (int theme = 0; theme < 4; theme++)
        {
            PachinkoOverlay.Theme = theme;
            PachinkoPlan.ForcePush = null;
            int jackpot = 0, win = 0, reachMiss = 0, miss = 0, pushWin = 0, winOrJack = 0, packBad = 0, shapeBad = 0, themeBad = 0;
            var digit = new int[10];
            for (int i = 0; i < N; i++)
            {
                PachinkoPlan p = PachinkoPlan.Roll();
                if (p.jackpot) jackpot++;
                else if (p.win) win++;
                else if (p.reach) reachMiss++;
                else miss++;
                if (p.pushWin) pushWin++;
                if (p.win) winOrJack++;
                digit[p.left]++;
                // 일관성: 777=세 7 · 당첨=세 같은 숫자(7 제외) · 리치꽝=좌우 같고 가운데 다름 · 꽝=세 같은 숫자 아님
                bool same = p.left == p.center && p.center == p.right;
                if (p.jackpot && !(same && p.left == 7)) shapeBad++;
                else if (!p.jackpot && p.win && !(same && p.left != 7)) shapeBad++;
                else if (!p.win && p.reach && !(p.left == p.right && p.center != p.left)) shapeBad++;
                else if (!p.win && !p.reach && same) shapeBad++;
                if (p.theme != theme) themeBad++;
                PachinkoPlan q = PachinkoPlan.Unpack(p.Pack());
                if (q.win != p.win || q.reach != p.reach || q.revive != p.revive || q.jackpot != p.jackpot || q.pushWin != p.pushWin || q.theme != p.theme ||
                    q.left != p.left || q.center != p.center || q.right != p.right || q.hold != p.hold || q.yokoku != p.yokoku || q.bg != p.bg || q.cut != p.cut) packBad++;
            }
            float f(int c) => c * 100f / N;
            sb.AppendLine($"[테마 {theme}] 777 {f(jackpot):F2}% (기대 5) · 세 같은 숫자(7 제외) {f(win):F2}% (기대 25) · 같은 숫자 합 {f(winOrJack):F2}% (기대 30) · 리치꽝 {f(reachMiss):F2}% (기대 40) · 꽝 {f(miss):F2}% (기대 30) · pushWin {f(pushWin):F2}% (기대 51)");
            sb.AppendLine($"   모양 불일치 {shapeBad} · Pack 왕복 불일치 {packBad} · theme 불일치 {themeBad} · 왼쪽 숫자 1~9 분포 {string.Join("/", System.Array.ConvertAll(digit, d => (d * 100f / N).ToString("F1")))}");
            bad += shapeBad + packBad + themeBad;
        }
        sb.AppendLine(bad == 0 ? "✅ 이상 없음" : $"❌ 불일치 {bad}");
        return sb.ToString();
    }
}

// 투표 4택 표 비율 표본(PachinkoController.PickTheme) — gameshot/배치: call PachinkoVoteSample.Run
static class PachinkoVoteSample
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (var c in new[] { new[] { 0, 0, 1, 1 }, new[] { 0, 0, 2, 1 }, new[] { 1, 0, 0, 3 }, new[] { 0, 0, 0, 0 }, new[] { 1, 1, 1, 1 }, new[] { 0, 0, 1, 0 } })
        {
            var hit = new int[4]; const int N = 100000;
            for (int i = 0; i < N; i++) hit[PachinkoController.PickTheme(c)]++;
            int tot = c[0] + c[1] + c[2] + c[3];
            sb.AppendLine($"표 [{string.Join(",", c)}] → " + string.Join(" · ", System.Linq.Enumerable.Range(0, 4).Select(k => $"테마{k} {hit[k] * 100f / N:F1}%(기대 {(tot == 0 ? 25f : c[k] * 100f / tot):F1})")));
        }
        return sb.ToString();
    }
}
