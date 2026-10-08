using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

// 헤더 바로 아래 띠 레이캐스트 진단(사장님 10-08 「버그 기록 오른쪽 클릭·드래그 안 먹음」, 구현담당2) — gameshot call:UiStripProbe.Scan
// 1920×1080 기준 y(위에서) 줄마다 x를 훑어 EventSystem 레이캐스트 맨 위 히트 오브젝트 경로·캔버스를 찍는다. 아무것도 안 맞으면 「·」(게임 클릭이 통과).
// Pick: 같은 지점에서 SelectionManager가 쓰는 IsPointerOverGameObject와 같은 판정이 필요하면 호출 직전 마우스가 그 자리에 있어야 해서, 여기선 RaycastAll로 대신한다.
static class UiStripProbe
{
    static string PathOf(GameObject go)
    {
        var sb = new StringBuilder(go.name);
        for (Transform t = go.transform.parent; t != null; t = t.parent) sb.Insert(0, t.name + "/");
        return sb.ToString();
    }

    static string Scan()
    {
        if (!Application.isPlaying || EventSystem.current == null) return "❌ 플레이 중에만";
        var sb = new StringBuilder($"화면 {Screen.width}×{Screen.height}");
        int free = 0, total = 0; var blockers = new HashSet<string>();
        foreach (int y in new[] { 60, 100, 120, 140, 160, 180, 200, 230, 260, 300 })
        {
            sb.Append($"\ny={y}:");
            string last = null; int run = 0; int startX = 0;
            for (int x = 0; x <= 1920; x += 40)
            {
                var data = new PointerEventData(EventSystem.current) { position = new Vector2(x / 1920f * Screen.width, Screen.height - y / 1080f * Screen.height) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(data, hits);
                string name = "·";
                foreach (var h in hits)
                {
                    if (h.gameObject == null) continue;
                    name = PathOf(h.gameObject);
                    var g = h.gameObject.GetComponent<UnityEngine.UI.Graphic>();
                    var rt = h.gameObject.transform as RectTransform;
                    if (g != null && rt != null) name += $" <{g.GetType().Name} a={g.color.a:F2} {rt.rect.width * rt.lossyScale.x:F0}×{rt.rect.height * rt.lossyScale.y:F0}px>";
                    break;
                }
                if (y >= 60 && y <= 300 && x >= 0 && x <= 1160) { total++; if (name == "·") free++; else blockers.Add(name); }   // 왼쪽 맵 영역(우상단 열 x≥1240 제외) — 드래그 시작이 되는 점
                if (name != last) { if (last != null) sb.Append($" [{startX}~{x - 40}] {last}"); last = name; startX = x; }
            }
            sb.Append($" [{startX}~1920] {last}");
        }
        sb.Append($"\n▶ 왼쪽 맵 영역(y 60~300 · x 0~1160) 표본 {total}점 중 UI가 안 막는(드래그 선택이 시작되는) 점 {free}점 · 막는 것: {string.Join(" | ", blockers)}");
        return sb.ToString();
    }
}
