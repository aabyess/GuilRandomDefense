using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>테스트 전용(-mpShotAt + 혼자 하기): 창구가 사라진 뒤에도 정해 둔 시각(실행 뒤 초)에 화면을 캡처한다.</summary>
public class NetShotHelper : MonoBehaviour
{
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
