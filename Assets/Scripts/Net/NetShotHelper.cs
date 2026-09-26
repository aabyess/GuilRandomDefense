using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>테스트 전용(-mpShotAt + 혼자 하기): 창구가 사라진 뒤에도 정해 둔 시각(실행 뒤 초)에 화면을 캡처한다.
/// 「메뉴」 확인(-mpSoloMenuAt 초 경로: 확인 창 열고 캡처 · -mpSoloHomeAt 초: [처음 화면으로] 누름)과 -mpExitAt도 여기서.</summary>
public class NetShotHelper : MonoBehaviour
{
    public void ScheduleMenu(float menuAt, string menuShot, float homeAt, float exitAt)
    {
        if (menuAt >= 0f) StartCoroutine(At(menuAt, () =>
        {
            GameHud hud = FindFirstObjectByType<GameHud>();
            if (hud != null) hud.ShowGameMenuConfirm();
            if (!string.IsNullOrEmpty(menuShot)) StartCoroutine(ShotAt(menuAt + 0.5f, menuShot));
            Debug.Log($"[MP] 혼자 하기 메뉴 테스트: 확인 창 열기(HUD {(hud != null ? "있음" : "없음")})");
        }));
        if (homeAt >= 0f) StartCoroutine(At(homeAt, () =>
        {
            GameHud hud = FindFirstObjectByType<GameHud>();
            Transform confirm = hud != null ? FindDeep(hud.transform, "ConfirmButton") : null;
            Debug.Log($"[MP] 혼자 하기 메뉴 테스트: [처음 화면으로] 누름(버튼 {(confirm != null ? "있음" : "없음")})");
            if (confirm != null && confirm.TryGetComponent(out UnityEngine.UI.Button button)) button.onClick.Invoke();
        }));
        if (exitAt >= 0f) StartCoroutine(At(exitAt, () => { Debug.Log("[MP] -mpExitAt(혼자 하기) 종료"); Application.Quit(); }));
    }

    IEnumerator At(float secondsSinceStart, System.Action action)
    {
        while (Time.realtimeSinceStartup < secondsSinceStart) yield return null;
        action();
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    public void Schedule(List<(float seconds, string path)> shots)
    {
        foreach (var (seconds, path) in shots)
            if (!string.IsNullOrEmpty(path)) StartCoroutine(ShotAt(seconds, path));
    }

    IEnumerator ShotAt(float secondsSinceStart, string path)
    {
        while (Time.realtimeSinceStartup < secondsSinceStart) yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[MP] 캡처(혼자 하기): {path}");
    }
}
