using UnityEngine;

// 불꽃 소방대 파칭코 시험(2026-10-16) — gameshot call:PachinkoFireProbe.<이름> 으로 부른다. 투표를 건너뛰고 테마 2로 바로 시작한다.
public static class PachinkoFireProbe
{
    static string Start(string mode, int theme = 2)
    {
        PachinkoOverlay.Theme = theme;
        PachinkoController.Begin(new[] { mode });
        return "불꽃 파칭코 시작: " + mode;
    }
    public static string Jackpot() => Start("jackpot");
    public static string Win() => Start("win");
    public static string Reach() => Start("reach");
    public static string Miss() => Start("miss");
    public static string SlimeJackpot() => Start("jackpot", 3);
    public static string SlimeWin() => Start("win", 3);
    public static string SlimeMiss() => Start("miss", 3);
    public static string SlimeReach() => Start("reach", 3);
    public static string PushFail() { PachinkoPlan.ForcePush = false; return "PUSH 실패 고정"; }
    public static string PushSuccess() { PachinkoPlan.ForcePush = true; return "PUSH 성공 고정"; }
    public static string PushRandom() { PachinkoPlan.ForcePush = null; return "PUSH 확률"; }
    // 시험 시간 단축: 불꽃 테마 시각을 앞쪽 구간으로 압축(룰렛 4~8초, PUSH 14초, 끝 19초)
    public static string Compress()
    {
        VideoThemeSpec v = PachinkoSettings.VideoTheme(2);
        v.rouletteStart = 4f; v.rouletteStop = 8f; v.pushAt = 14f; v.videoEnd = 19f;
        return "불꽃 시각 압축";
    }
    public static string Skip25() { PachinkoSettings.DebugSkipSeconds = 25f; return "skip 25"; }
    public static string Skip85() { PachinkoSettings.DebugSkipSeconds = 85f; return "skip 85"; }
    public static string Skip140() { PachinkoSettings.DebugSkipSeconds = 140f; return "skip 140"; }
    public static string Skip215() { PachinkoSettings.DebugSkipSeconds = 215f; return "skip 215"; }
    public static string Skip65() { PachinkoSettings.DebugSkipSeconds = 65f; return "skip 65"; }
    public static string Skip75() { PachinkoSettings.DebugSkipSeconds = 75f; return "skip 75"; }
    public static string Skip100() { PachinkoSettings.DebugSkipSeconds = 100f; return "skip 100"; }
    public static string Skip196() { PachinkoSettings.DebugSkipSeconds = 196f; return "skip 196"; }
    public static string Skip200() { PachinkoSettings.DebugSkipSeconds = 200f; return "skip 200"; }
    public static string Skip214() { PachinkoSettings.DebugSkipSeconds = 214f; return "skip 214"; }
    public static string Skip0() { PachinkoSettings.DebugSkipSeconds = 0f; return "skip 0"; }
    public static string SlimeSuccessFx() { PachinkoOverlay.DebugSlimeSuccessFx(); return "성공 효과"; }
    public static string AutoPush() { PachinkoController.AutoPushForTest = true; return "자동 누름"; }
    public static string TokyoJackpot() => Start("jackpot", 0);
    public static string RezeroJackpot() => Start("jackpot", 1);
    public static string TokyoWin() => Start("win", 0);
    public static string TokyoMiss() => Start("miss", 0);
    public static string RezeroWin() => Start("win", 1);
    public static string RezeroMiss() => Start("miss", 1);
    public static string CompressSlime()
    {
        VideoThemeSpec v = PachinkoSettings.VideoTheme(3);
        v.rouletteStart = 4f; v.rouletteStop = 8f; v.pushAt = 14f; v.videoEnd = 19f;
        return "전생슬 시각 압축";
    }
    public static string Vote() { PachinkoController.Begin(null); return "투표 시작"; }
    // 모니터 사진용: 가짜 원격 슬롯 둘
    public static string FakeMonitor()
    {
        PachinkoOverlay.DebugAddMonitor(2, PachinkoPlan.Roll("win"));
        PachinkoOverlay.DebugAddMonitor(3, PachinkoPlan.Roll("miss"));
        return "가짜 모니터 2기";
    }
    // 관전 사진용: 내 판은 첫 룰렛 꽝, 가짜 원격 슬롯 둘은 크리티컬·러쉬까지 판 — 내 판이 끝나면 관전 화면이 떠야 한다
    public static string SpectateTest()
    {
        PachinkoOverlay.Theme = 2;
        PachinkoController.Begin(new[] { "miss" });   // Begin이 모니터를 비우므로 가짜 슬롯은 그 뒤에 올린다
        PachinkoOverlay.DebugAddMonitor(2, PachinkoPlan.Roll("win"));
        PachinkoOverlay.DebugAddMonitor(3, PachinkoPlan.Roll("reach"));
        return "관전 시험";
    }
    public static string SpecAudio() => PachinkoOverlay.DebugSpecAudio();
    // 신 R30 보스 시험: 레인 보스를 실제 사망 경로(TakeDamage → GrantKillReward → GrantBossReward)로 잡고 파칭코 상태를 문자열로 낸다
    public static string KillLaneBosses()
    {
        int n = 0; string info = "";
        foreach (EnemyDummy e in new System.Collections.Generic.List<EnemyDummy>(EnemyDummy.Active))
        {
            if (e == null || !e.IsBoss || e.LaneIndex < 0) continue;
            info += $"{e.name}(R{e.SpawnRound},레인{e.LaneIndex}) ";
            e.TakeDamage(1e12f, DamageType.AD, AttackType.Normal, e.LaneIndex, 1f, false, 1f, false, true);
            n++;
        }
        return $"보스 {n}기 처치 시도: {info}";
    }
    public static string PachinkoState() => PachinkoController.DebugState();
    // 두 번째 판 시험: 같은 앱 실행 안에서 씬을 다시 불러온다(타이틀 → 다시 시작과 같은 static 상태)
    public static string ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        return "씬 다시 불러옴 · " + PachinkoController.DebugState();
    }
    // 파칭코 도중(투표 중) 씬을 다시 불러온다 — 실제 판처럼 결과 고정 없이 시작
    public static string VoteThenReload() { PachinkoController.Begin(null); return ReloadScene(); }
    public static string PickGod() { if (DifficultyManager.Instance == null) return "난이도 매니저 없음"; DifficultyManager.Instance.SelectMode(DifficultyMode.God); return "신 선택"; }
}
