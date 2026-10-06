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

    static readonly string[] Names = { "도박소", "유닛강화소", "다른세계강화소", "영원함강화소", "공격타입강화소", "도움소", "항해일지" };

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
}
