using UnityEngine;

/// <summary>
/// 게임 씬으로 넘어갈 때 로딩 화면을 띄우는 자리(사장님 요청 09-26: 3초 로딩 · 전체 이미지 · 진행률 · 「구랜디」).
/// 로딩 화면 본체는 PM이 main에 넣는 LoadingScreen(Show 한 번이면 씬이 올라오고 최소 3초 뒤 스스로 걷힌다).
/// 부르는 곳 셋: ① 혼자 하기 직전 ② 방장 [시작](runner.LoadScene 직전) ③ 클라가 호스트의 씬 전환을 받는 순간(OnSceneLoadStart).
/// 한 번의 전환에 두 번 부르지 않게 막는다(방장은 ②와 ③이 둘 다 온다).
/// </summary>
public static class NetLoadingHook
{
    static float lastShownAt = -99f;

    public static void Show(string from)
    {
        if (Time.unscaledTime - lastShownAt < 2f) return;   // 같은 전환에서 두 번째 호출(방장 ②+③)
        lastShownAt = Time.unscaledTime;
        Debug.Log($"[MP] 로딩 화면({from})");
        // LoadingScreen(main)이 들어오면 여기 한 줄: LoadingScreen.Show();
    }
}
