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

    // 혼자 하기 coinsound: 10엔 도박(소리 남) → 메뉴 소리 끔 → 10엔 도박(안 남) → 소리 켬(기억값 되돌림).
    public void ScheduleCoin(float at)
    {
        if (at < 0f) return;
        StartCoroutine(At(at, () => Gamble("소리 켠 채")));
        StartCoroutine(At(at + 2f, () => FindFirstObjectByType<GameHud>()?.ToggleSound()));
        StartCoroutine(At(at + 3f, () =>
        {
            Gamble("소리 끈 채");
            // 도박이 재고로 막혀도 음소거 자체를 본다 — 끈 동안엔 「[소리] … 재생」 줄이 없어야 한다.
            Debug.Log("[MP] 혼자 하기 동전 테스트: 소리 끈 채 GameSound.PlayFor 직접 호출");
            GameSound.PlayFor(LocalPlayer.LocalPlayerId, GameSoundId.Coin);
        }));
        StartCoroutine(At(at + 4f, () =>
        {
            Debug.Log("[MP] 혼자 하기 동전 테스트: 다시 켜고 직접 호출");
            if (!GameSound.Enabled) FindFirstObjectByType<GameHud>()?.ToggleSound();
            GameSound.PlayFor(LocalPlayer.LocalPlayerId, GameSoundId.Coin);
        }));
    }

    public void ScheduleMenuMain(float at, string shot)
    {
        if (at < 0f) return;
        StartCoroutine(At(at, () => FindFirstObjectByType<GameHud>()?.OpenGameMenu()));
        if (!string.IsNullOrEmpty(shot)) StartCoroutine(ShotAt(at + 0.5f, shot));
    }

    // 확인 못 한 세 길(PM 09-27): 500엔 당첨만 소리 · 특성 포인트 구매 · 목재 구입. 테스트 전용으로 돈·해금·재고·졸업을 채운다.
    public void ScheduleShopSounds(float at)
    {
        if (at >= 0f) StartCoroutine(ShopSoundsAt(at));
    }

    IEnumerator ShopSoundsAt(float at)
    {
        while (Time.realtimeSinceStartup < at) yield return null;
        PlayerContext context = PlayerContext.Get(LocalPlayer.LocalPlayerId);
        GamblingShop shop = null;
        foreach (GamblingShop s in FindObjectsByType<GamblingShop>(FindObjectsSortMode.None))
            if (s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == LocalPlayer.LocalPlayerId) { shop = s; break; }
        var field = typeof(GamblingShop).GetField("moneyOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var options = shop != null && field != null ? field.GetValue(shop) as List<GamblingOptionData> : null;
        if (context == null || shop == null || options == null) { Debug.LogWarning("[MP] 상점 소리 테스트: 준비물 없음"); yield break; }
        GamblingOptionData high = options.Find(o => o != null && o.optionName == "500엔 도박");
        GamblingOptionData wood = options.Find(o => o != null && o.optionName == "목재 구입");
        context.GoldWallet.Add(60000);

        // 1. 500엔: 해금 + 재고 7. 당첨(받은 돈 ≥ 500 → 순이익 ≥ 0)이면 소리, 실패(환급 300~400)면 없어야 한다.
        context.GamblingProgress.ApplyReplicated(high, 0, true, high.stockMax, 0f, 0);
        int wins = 0, winSounds = 0, fails = 0, failSounds = 0;
        for (int i = 0; i < high.stockMax; i++)
        {
            int gold = context.GoldWallet.Gold, plays = GameSound.PlayCount;
            bool ok = shop.TryRoll(high, out string reason);
            int net = context.GoldWallet.Gold - gold;
            bool sounded = GameSound.PlayCount > plays;
            bool win = net >= 0;
            if (ok) { if (win) { wins++; if (sounded) winSounds++; } else { fails++; if (sounded) failSounds++; } }
            Debug.Log($"[MP] 상점 소리 테스트 500엔 {i + 1}: {(ok ? (win ? "당첨" : "실패") : "안 됨 " + reason)} 순이익 {net}엔 · 소리 {(sounded ? "남" : "없음")}");
            yield return new WaitForSecondsRealtime(0.4f);
        }
        Debug.Log($"[MP] 상점 소리 테스트 500엔 합계: 당첨 {wins}번 중 소리 {winSounds} · 실패 {fails}번 중 소리 {failSounds}(0이어야 함)");

        // 2. 특성 포인트 구매(칸 1 = W).
        int before = GameSound.PlayCount;
        bool bought = shop.TryUse(1, default, out string traitReason);
        Debug.Log($"[MP] 상점 소리 테스트 특성 포인트: {(bought ? "구매" : "안 됨 " + traitReason)} · 소리 {(GameSound.PlayCount > before ? "남" : "없음")}");
        yield return new WaitForSecondsRealtime(0.4f);

        // 3. 졸업 뒤 목재 구입.
        context.GamblingProgress.Graduate();
        int woodBefore = context.ResourceWallet.Get(ResourceType.Wood);
        before = GameSound.PlayCount;
        bool bought2 = wood != null && shop.TryRoll(wood, out string woodReason);
        Debug.Log($"[MP] 상점 소리 테스트 목재 구입: {(bought2 ? "구매" : "안 됨")} 목재 {woodBefore}→{context.ResourceWallet.Get(ResourceType.Wood)} · 소리 {(GameSound.PlayCount > before ? "남" : "없음")}");
    }

    static void Gamble(string label)
    {
        GamblingShop shop = null;
        foreach (GamblingShop s in FindObjectsByType<GamblingShop>(FindObjectsSortMode.None))
            if (s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == LocalPlayer.LocalPlayerId) { shop = s; break; }
        bool ok = shop != null && shop.TryUse(0, default, out string reason);
        Debug.Log($"[MP] 혼자 하기 동전 테스트({label}): 10엔 도박 → {(ok ? "됨" : "안 됨")}");
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
