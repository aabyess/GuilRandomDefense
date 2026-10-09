using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 강화소 연타 재현 탐침(10-09 PM 「상점에서 강화할 때 간혹 상점이 나가진다」): 상점 건물을 고른 채 명령 카드 단추를 가상 마우스(실제 입력 경로)로 N번 누르고
/// 매번 「아직 그 상점을 고르고 있나」를 본다. 풀리면 그 순간 SelectionManager.ClearSelection을 부른 호출 경로를 기록한다.
///   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:ShopSpamProbe.Begin wait:60 call:ShopSpamProbe.Report
///   ClaudeBridge/g2_shop.txt = 「상점이름조각|모드」 모드: btn(단추 중심 연타) · gap(단추 사이 틈 섞기) · key(단축키) · mix
/// </summary>
public static class ShopSpamProbe
{
    static readonly StringBuilder log = new StringBuilder();
    static int clicks, losses;

    static string Begin()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string[] spec = System.IO.File.ReadAllText("ClaudeBridge/g2_shop.txt").Trim().Split('|');
        log.Clear(); clicks = losses = 0; SelectionManager.TraceClear = true;
        var host = Object.FindFirstObjectByType<SelectionManager>();   // 에디터 어셈블리 클래스는 런타임에 AddComponent가 안 된다 — 이미 있는 MonoBehaviour로 코루틴을 돌린다
        host.StartCoroutine(Run(spec[0], spec.Length > 1 ? spec[1] : "btn"));
        return "시작 " + string.Join("|", spec);
    }

    static string Report() => $"클릭 {clicks} · 상점 풀림 {losses}\n{log}";

    static IEnumerator Run(string shopName, string mode)
    {
        yield return null;
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        var hud = Object.FindFirstObjectByType<GameHud>();
        Selectable shop = Selectable.All.FirstOrDefault(s => s != null && s.name.Contains(shopName));
        if (shop == null) { log.AppendLine("❌ 상점 없음 " + shopName); yield break; }
        Mouse mouse = InputSystem.AddDevice<Mouse>("G2ProbeMouse"); mouse.MakeCurrent();
        var disabled = new List<InputDevice>();
        foreach (Mouse m in InputSystem.devices.OfType<Mouse>().Where(m => m != mouse && m.enabled).ToList()) { InputSystem.DisableDevice(m); disabled.Add(m); }
        FieldInfo f = typeof(GameHud).GetField("unitCommandSlotButtons", BindingFlags.Instance | BindingFlags.NonPublic);
        sel.SelectOnly(shop);
        yield return new WaitForSeconds(0.3f);
        var buttons = (Button[])f.GetValue(hud);
        Camera cam = null;
        int total = 100;
        GoldWallet wallet = PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
        int spentSeen = 0;
        for (int n = 0; n < total; n++)
        {
            if (wallet != null) wallet.Add(500000);   // 강화가 실제로 일어나게(자원 부족이면 단추만 눌리고 재구성 경로가 안 돈다)
            int goldBefore = wallet != null ? wallet.Gold : 0;
            if (sel.Selected.Count != 1 || sel.Selected[0] != shop) { sel.SelectOnly(shop); yield return null; yield return null; }
            // 누를 곳: 보이는 단추 중 하나(라운드로빈), gap 모드면 5번에 한 번 단추 사이 틈
            List<Button> visible = buttons.Where(b => b != null && b.gameObject.activeInHierarchy).ToList();
            if (visible.Count == 0) { log.AppendLine("보이는 단추 없음"); break; }
            Button target = visible[n % visible.Count];
            Vector2 pos = RectCenter((RectTransform)target.transform);
            if (mode == "gap" && n % 5 == 4) pos += new Vector2(0f, -((RectTransform)target.transform).rect.height * 0.7f);   // 단추 아래 틈
            if (mode == "key")
            {
                char[] keysArr = (char[])typeof(GameHud).GetField("shopSlotHotkeys", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                var usable = keysArr.Where(c => c != '\0').ToList();
                char kc = usable.Count > 0 ? usable[n % usable.Count] : 'q';
                System.Enum.TryParse(kc.ToString(), out Key ek);
                Keyboard kb = Keyboard.current;
                InputSystem.QueueStateEvent(kb, new KeyboardState(ek)); yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null; yield return null;
            }
            else if (mode == "jump")
            {
                // 현실 재현: 직전 프레임엔 커서가 월드 위(UI 아님), 다음 프레임에 커서가 단추 위로 「이동+누름」이 한 번에 들어온다(느린 프레임에서 빠르게 움직여 클릭)
                Queue(mouse, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false); yield return null; yield return null;
                Queue(mouse, pos, true); yield return null;
                Queue(mouse, pos, false); yield return null; yield return null;
            }
            else
            {
            Queue(mouse, pos, false); yield return null;
            Queue(mouse, pos, true); yield return null;
            Queue(mouse, pos, false); yield return null; yield return null;
            }
            clicks++;
            if (wallet != null && wallet.Gold < goldBefore) spentSeen++;
            if (sel.Selected.Count != 1 || sel.Selected[0] != shop)
            {
                losses++;
                log.AppendLine($"#{n} 풀림 — 누른 곳 {target.name} (라벨 {Label(target)}) 위치 {pos} 포인터가 UI 위: {EventSystem.current.IsPointerOverGameObject()} · 직전 호출: {SelectionManager.LastClearTrace}");
            }
            yield return null;
        }
        foreach (InputDevice d in disabled) InputSystem.EnableDevice(d);
        InputSystem.RemoveDevice(mouse);
        log.AppendLine($"끝 — 자원이 실제로 쓰인 클릭 {spentSeen}");
    }

    static string Label(Button b) { var t = b.GetComponentInChildren<TMPro.TMP_Text>(true); return t != null ? t.text.Replace("\n", "/") : "-"; }

    static Vector2 RectCenter(RectTransform rt)
    {
        var corners = new Vector3[4]; rt.GetWorldCorners(corners);
        Vector3 c = (corners[0] + corners[2]) * 0.5f;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, c);
    }

    static void Queue(Mouse mouse, Vector2 pos, bool down)
    {
        var state = new MouseState { position = pos };
        if (down) state = state.WithButton(MouseButton.Left, true);
        InputSystem.QueueStateEvent(mouse, state);
    }
}
