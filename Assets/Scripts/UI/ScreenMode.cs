using UnityEngine;

/// <summary>
/// 화면 모드(창/전체 화면 + 해상도) — F10 메뉴 「화면」이 고른다(사장님 10-04, 친구 피드백).
/// 고른 값은 PlayerPrefs에 저장하고 다음 실행 첫 프레임에 다시 적용한다(BeforeSceneLoad — 배포판 첫 씬이 NetBoot라 게임 씬 전용 설치는 안 돈다).
/// 「지금 무엇이 골라졌나」는 저장값이 아니라 실제 Screen 상태로 읽는다 — 창 크기를 손으로 늘리면 아무것도 안 골라진 것으로 보인다.
/// 에디터에선 Screen.SetResolution이 Game 뷰에 안 먹으니 시작 적용은 건너뛴다(실측은 빌드에서).
/// </summary>
public static class ScreenMode
{
    public struct Option
    {
        public string label;
        public bool fullScreen;
        public int width, height;
    }

    public static readonly Option[] Options =
    {
        new Option { label = "전체 화면", fullScreen = true },
        new Option { label = "창 1920×1080", width = 1920, height = 1080 },
        new Option { label = "창 1600×900", width = 1600, height = 900 },
        new Option { label = "창 1366×768", width = 1366, height = 768 },
        new Option { label = "창 1280×720", width = 1280, height = 720 },
    };

    const string PrefsKey = "GuilRandomDefense.ScreenMode";
    // 창 모드는 제목 줄·작업 표시줄이 세로를 먹는다 — 모니터 높이에서 이만큼 뺀 안에 들어야 고를 수 있다.
    const int WindowChromeMargin = 80;

    static int MonitorWidth => Display.main.systemWidth;
    static int MonitorHeight => Display.main.systemHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        if (Application.isEditor) return;
        try
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return;   // 한 번도 안 골랐으면 ProjectSettings 기본(전체 화면 창) 그대로
            int index = PlayerPrefs.GetInt(PrefsKey, 0);
            if (index >= 0 && index < Options.Length && IsAvailable(index)) Apply(Options[index]);
        }
        catch { }
    }

    /// <summary>이 모니터에 들어가는 선택지인가(전체 화면은 항상).</summary>
    public static bool IsAvailable(int index)
    {
        Option o = Options[index];
        return o.fullScreen || (o.width <= MonitorWidth && o.height <= MonitorHeight - WindowChromeMargin);
    }

    /// <summary>지금 Screen 상태와 맞는 선택지 번호. 없으면 -1(창을 손으로 늘린 경우 등).</summary>
    public static int CurrentIndex()
    {
        bool full = Screen.fullScreenMode == FullScreenMode.FullScreenWindow || Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen;
        for (int i = 0; i < Options.Length; i++)
        {
            Option o = Options[i];
            if (o.fullScreen ? full : !full && Screen.width == o.width && Screen.height == o.height) return i;
        }
        return -1;
    }

    public static void Choose(int index)
    {
        if (index < 0 || index >= Options.Length || !IsAvailable(index)) return;
        Apply(Options[index]);
        try { PlayerPrefs.SetInt(PrefsKey, index); PlayerPrefs.Save(); } catch { }
        Debug.Log($"[화면] {Options[index].label} 선택");
    }

    static void Apply(Option o)
    {
        if (o.fullScreen) Screen.SetResolution(MonitorWidth, MonitorHeight, FullScreenMode.FullScreenWindow);
        else Screen.SetResolution(o.width, o.height, FullScreenMode.Windowed);
    }
}
