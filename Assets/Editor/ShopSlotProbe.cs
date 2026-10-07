using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 도움소 먹통 점검(10-06 사장님 「도박소부터 항해일지까지 하나도 작동이 안 됨」) — gameshot call:로 판 안에서 부른다.
///   call:ShopSlotProbe.Colliders   내 레인 상점 전부의 콜라이더·컴포넌트·선택 가능 여부
///   call:ShopSlotProbe.Pick        다음 상점을 고른다(부를 때마다 하나씩)
///   call:ShopSlotProbe.Check       고른 상점의 칸마다 화면 가운데에서 EventSystem 레이캐스트 → 맨 위가 그 칸인가, 클릭 처리는 되나
/// 가상 마우스 클릭 경로 전체가 아니라 「UI가 클릭을 받을 수 있나」를 본다(ExecuteHierarchy = EventSystem이 실제로 하는 것).
/// </summary>
public static class ShopSlotProbe
{
    static int next;
    static string current = "";

    static readonly string[] Names = { "도박소", "유닛강화소", "영원함강화소", "공격타입강화소", "도움소", "항해일지" };

    static bool Mine(Selectable s) =>
        s != null && (!s.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId == LocalPlayer.LocalPlayerId);

    public static string Colliders()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string n in Names)
        {
            Selectable s = Selectable.All.Where(Mine).FirstOrDefault(x => x.name.Contains(n));
            if (s == null) { sb.AppendLine($"   ❌ {n}: Selectable 없음(씬에 있는 이름: {string.Join(",", Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b => b is ILaneShop).Select(b => b.name).Take(20))})"); continue; }
            Collider c = s.GetComponent<Collider>();
            bool shop = s.GetComponent<ILaneShop>() != null;
            sb.AppendLine($"   {(c != null && c.enabled && shop ? "✅" : "❌")} {s.name}: ILaneShop {shop} · 콜라이더 {(c == null ? "없음" : $"{c.GetType().Name} 켜짐 {c.enabled} 크기 {c.bounds.size.x:F0}×{c.bounds.size.y:F0}×{c.bounds.size.z:F0} 레이어 {LayerMask.LayerToName(s.gameObject.layer)}")} · 위치 {s.transform.position:F0}");
        }
        return sb.ToString();
    }

    public static string Pick()
    {
        SelectionManager selection = Object.FindFirstObjectByType<SelectionManager>();
        if (selection == null) return "❌ SelectionManager 없음";
        string n = Names[next++ % Names.Length];
        Selectable s = Selectable.All.Where(Mine).FirstOrDefault(x => x.name.Contains(n));
        if (s == null) { current = ""; return $"❌ {n} 없음"; }
        selection.SelectOnly(s);
        current = n;
        return $"선택 {s.name}";
    }

    public static string Check()
    {
        if (EventSystem.current == null) return "❌ EventSystem 없음";
        StringBuilder sb = new StringBuilder($"   [{current}]\n");
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(b => b.name.StartsWith("UnitCommandSlot") && b.IsActive())
            .OrderBy(b => int.TryParse(b.name.Substring("UnitCommandSlot".Length), out int i) ? i : 99).ToList();
        int blocked = 0, shown = 0;
        foreach (Button b in buttons)
        {
            string label = (string.Concat(b.GetComponentsInChildren<Text>().Select(t => t.text)) + string.Concat(b.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text))).Replace("\n", " ");
            Image img = b.GetComponent<Image>();
            if (string.IsNullOrEmpty(label) && (img == null || img.color.a < 0.05f)) continue;   // 빈 칸
            shown++;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));
            PointerEventData ped = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            GameObject top = hits.Count > 0 ? hits[0].gameObject : null;
            bool ok = top != null && (top == b.gameObject || top.transform.IsChildOf(b.transform));
            if (!ok) blocked++;
            sb.AppendLine($"   {(ok ? "✅" : "❌")} {b.name}「{label}」 interactable {b.interactable} · 화면 {screen:F0} · 맨 위 {(top != null ? top.name : "없음")}{(ok ? "" : " ← 가로챔")}");
        }
        sb.AppendLine($"   칸 {shown}개 중 가로채인 것 {blocked}");
        return sb.ToString();
    }

    // ── 10-06 PM: 실제 구입·사용까지(「칸이 뜬다」만으론 「작동한다」가 아니다) ──
    //   call:ShopSlotProbe.Fund      내 지갑에 돈 100만·목재 500·토큰 50·마나 500
    //   call:ShopSlotProbe.Pick → call:ShopSlotProbe.ClickAll   고른 상점의 칸을 진짜 클릭 경로(EventSystem 포인터 클릭 → GameHud)로 하나씩 누르고
    //                                                          돈·자원·유닛 수·아이템 수·알림 변화를 적는다. 대상 지정 칸은 「대상 대기」로 적고 넘어간다.
    public static string Fund()
    {
        PlayerContext me = PlayerContext.Local;
        if (me == null) return "❌ PlayerContext.Local 없음";
        me.GoldWallet.Add(1000000);
        me.ResourceWallet.Add(ResourceType.Wood, 500);
        me.ResourceWallet.Add(ResourceType.Token, 50);
        me.ResourceWallet.Add(ResourceType.LuckyToken, 50);
        me.ResourceWallet.Add(ResourceType.Mana, 500);
        return $"지갑: 돈 {me.GoldWallet.Gold} · 목재 {me.ResourceWallet.Get(ResourceType.Wood)} · 토큰 {me.ResourceWallet.Get(ResourceType.Token)} · 마나 {me.ResourceWallet.Get(ResourceType.Mana)}";
    }

    static string State(PlayerContext me) =>
        $"돈 {me.GoldWallet.Gold} 목재 {me.ResourceWallet.Get(ResourceType.Wood)} 토큰 {me.ResourceWallet.Get(ResourceType.Token)} 행운 {me.ResourceWallet.Get(ResourceType.LuckyToken)} 마나 {me.ResourceWallet.Get(ResourceType.Mana)} 유닛 {me.UnitInventory.Units.Count} 아이템 {(me.ItemInventory != null ? me.ItemInventory.Items.Count : -1)} 위습 {Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None).Length}";

    public static string ClickAll()
    {
        PlayerContext me = PlayerContext.Local;
        if (me == null || EventSystem.current == null) return "❌ 판 준비 안 됨";
        StringBuilder sb = new StringBuilder($"   [{current}] 시작 {State(me)}\n");
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(b => b.name.StartsWith("UnitCommandSlot") && b.IsActive())
            .OrderBy(b => int.TryParse(b.name.Substring("UnitCommandSlot".Length), out int i) ? i : 99).ToList();
        var notes = new List<string>();
        System.Action<int, string, float> hook = (pid, msg, d) => notes.Add(msg);
        PlayerNotification.Shown += hook;
        try
        {
            foreach (Button b in buttons)
            {
                string label = (string.Concat(b.GetComponentsInChildren<Text>().Select(t => t.text)) + string.Concat(b.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text))).Replace("\n", " ").Trim();
                if (string.IsNullOrEmpty(label)) continue;
                if (label.Contains("해적단 ▶") || label.Contains("◀ 뒤로")) { sb.AppendLine($"   {b.name}「{label}」 쪽 넘김 — 안 누름"); continue; }
                string before = State(me);
                notes.Clear();
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));
                PointerEventData ped = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
                string after = State(me);
                string pending = "";
                GameHud hud = Object.FindFirstObjectByType<GameHud>();
                if (hud != null)
                {
                    var f = typeof(GameHud).GetField("pendingSlotIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null && (int)f.GetValue(hud) >= 0) { pending = " · 대상 대기"; f.SetValue(hud, -1); }
                }
                sb.AppendLine($"   {b.name}「{label}」 interactable {b.interactable}{pending}\n      전 {before}\n      후 {(after == before ? "(변화 없음)" : after)}{(notes.Count > 0 ? "\n      알림: " + string.Join(" / ", notes) : "")}");
            }
        }
        finally { PlayerNotification.Shown -= hook; }
        return sb.ToString();
    }
}
