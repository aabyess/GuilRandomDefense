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
    public static string Skip0() { PachinkoSettings.DebugSkipSeconds = 0f; return "skip 0"; }
    public static string Vote() { PachinkoController.Begin(null); return "투표 시작"; }
    // 모니터 사진용: 가짜 원격 슬롯 둘
    public static string FakeMonitor()
    {
        PachinkoOverlay.DebugAddMonitor(2, PachinkoPlan.Roll("win"));
        PachinkoOverlay.DebugAddMonitor(3, PachinkoPlan.Roll("miss"));
        return "가짜 모니터 2기";
    }
}
