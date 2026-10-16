using System.Linq;
using System.Text;
using UnityEngine;

// PachinkoPlan.Roll 10만 회 표본(구현담당1, 10-16) — 테마별 결과 분포·PUSH 성공률·판 내부 일관성·Pack/Unpack 왕복
static class PachinkoPlanSample
{
    /// <summary>배치모드: Unity -batchmode -quit -projectPath . -executeMethod PachinkoPlanSample.Batch (결과 ~/grd/GRD_pachinko_stages/sample.txt)</summary>
    public static void Batch()
    {
        string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd", "GRD_pachinko_stages");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "sample.txt"), Run());
    }

    static string Run()
    {
        const int N = 100000;
        var sb = new StringBuilder();
        int bad = 0;
        for (int theme = 0; theme < 4; theme++)
        {
            PachinkoOverlay.Theme = theme;
            if (theme >= 2) { sb.AppendLine(RunVideo(theme, N, ref bad)); continue; }
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

    // 영상형 3단계(10-16): 첫 룰렛 30% · 러쉬 도달 시 크리티컬 ≈86%(불꽃 38번)/≈72%(전생슬 25번) · 전체 랜덤 유닛(PUSH 성공까지) ≈13%(불꽃) — 모양·Pack 왕복·러쉬 숫자 일관성도 본다
    static string RunVideo(int theme, int N, ref int bad)
    {
        VideoThemeSpec vt = PachinkoSettings.VideoTheme(theme);
        int first = 0, crit = 0, top = 0, packBad = 0, shapeBad = 0, rushBad = 0, themeBad = 0;
        for (int i = 0; i < N; i++)
        {
            PachinkoPlan p = PachinkoPlan.Roll();
            bool same = p.left == p.center && p.center == p.right;
            if (p.reach) first++;
            if (p.win) crit++;
            if (p.win && p.pushWin) top++;
            if (p.reach != same || (p.win && !p.reach) || p.jackpot || (p.win != (p.RushWinIndex >= 0)) || p.RushWinIndex >= vt.RushCount || (!p.reach && p.rushHit != 0)) shapeBad++;
            if (p.theme != theme) themeBad++;
            // 러쉬 숫자: 당첨 순번만 세 숫자 같음
            for (int k = 0; k < vt.RushCount; k++)
            {
                p.RushDigits(k, out byte a, out byte b, out byte c);
                bool trip = a == b && b == c;
                if (trip != (k == p.RushWinIndex) || a < 1 || a > 9 || b < 1 || b > 9 || c < 1 || c > 9) { rushBad++; break; }
            }
            PachinkoPlan q = PachinkoPlan.Unpack(p.Pack());
            if (q.win != p.win || q.reach != p.reach || q.pushWin != p.pushWin || q.theme != p.theme || q.left != p.left || q.center != p.center || q.right != p.right ||
                q.rushHit != p.rushHit || q.rushSeed != p.rushSeed) packBad++;
        }
        float f(int c) => c * 100f / N;
        float expRush = 100f * (1f - Mathf.Pow(1f - PachinkoSettings.VideoRushWinChance, vt.RushCount));
        bad += shapeBad + packBad + themeBad + rushBad;
        return $"[테마 {theme} {vt.key}] 러쉬 {vt.RushCount}번 · 첫 룰렛 당첨 {f(first):F2}% (기대 30) · 크리티컬 전체 {f(crit):F2}% (기대 {0.3f * expRush:F2}) · 러쉬 도달 중 크리티컬 {(first == 0 ? 0 : crit * 100f / first):F2}% (기대 {expRush:F2}) · 랜덤 유닛(PUSH 성공) 전체 {f(top):F2}% (기대 {0.3f * expRush * 0.51f:F2})\n   모양 불일치 {shapeBad} · 러쉬 숫자 불일치 {rushBad} · Pack 왕복 불일치 {packBad} · theme 불일치 {themeBad}";
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
