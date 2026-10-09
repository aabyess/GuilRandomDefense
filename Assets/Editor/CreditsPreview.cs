using UnityEditor;
using UnityEngine;

/// <summary>제작진 화면 미리보기(10-09): 빌드 0번 씬에서만 뜨는 CreditsSplash를 SampleScene 플레이 중 강제로 띄운다.
///   메뉴 Tools/UI/제작진 화면 미리보기 A·B(플레이 중) · gameshot: call:CreditsPreview.ShowA / ShowB 뒤 wait → snap, 또는 ClaudeBridge/g2_credit_t.txt에 시각(초)을 쓰고 call:CreditsPreview.FreezeA/FreezeB로 한 장면 고정.</summary>
static class CreditsPreview
{
    [MenuItem("Tools/UI/제작진 화면 미리보기 A(남색)")] static void MenuA() => Debug.Log(ShowA());
    [MenuItem("Tools/UI/제작진 화면 미리보기 B(갈색)")] static void MenuB() => Debug.Log(ShowB());

    static string ShowA() { if (!Application.isPlaying) return "❌ 플레이 중에만"; CreditsSplash.FreezeAt = -1f; CreditsSplash.ShowPreview(0); return "A 시작"; }
    static string ShowB() { if (!Application.isPlaying) return "❌ 플레이 중에만"; CreditsSplash.FreezeAt = -1f; CreditsSplash.ShowPreview(1); return "B 시작"; }
    static string FreezeA() => Freeze(0);
    static string FreezeB() => Freeze(1);
    static string Freeze(int variant)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        float t = float.TryParse(System.IO.File.ReadAllText("ClaudeBridge/g2_credit_t.txt").Trim(), out float v) ? v : 2f;
        CreditsSplash.FreezeAt = t; CreditsSplash.ShowPreview(variant);
        return $"시안 {(variant == 0 ? "A" : "B")} t={t}";
    }
}
