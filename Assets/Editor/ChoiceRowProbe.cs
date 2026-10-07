using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>흔함 선택 줄 점검(10-07) — call ChoiceRowProbe.List : 「흔함선택_」으로 시작하는 오브젝트 전부의 이름·부모·x·z.</summary>
static class ChoiceRowProbe
{
    static string List()
    {
        var sb = new StringBuilder();
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.StartsWith("흔함선택")).OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToList();
        sb.Append($"흔함선택* {all.Count}개");
        foreach (Transform t in all) sb.Append($"\n   {t.name} · 부모 {(t.parent != null ? t.parent.name : "-")} · x {t.position.x:0.0} z {t.position.z:0.0} y {t.position.y:0.0}");
        return sb.ToString();
    }

    static string Near()
    {
        var sb = new StringBuilder();
        float[] xs = { -1657.3f, -1584.7f, -1512.0f, -1439.4f, -1366.8f, -1294.1f, -1221.5f, -1148.9f, -1076.3f };
        var hits = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => !t.name.StartsWith("흔함선택") && t.position.z > -370f && t.position.z < -300f && xs.Any(x => Mathf.Abs(t.position.x - x) < 6f) && (t.parent == null || !t.parent.name.StartsWith("흔함선택"))).OrderBy(t => t.position.x).ToList();
        sb.Append($"근처 오브젝트 {hits.Count}개");
        foreach (Transform t in hits) sb.Append($"\n   {t.name} · 부모 {(t.parent != null ? t.parent.name : "-")} · x {t.position.x:0.0} z {t.position.z:0.0}");
        return sb.ToString();
    }

    static string Cam()
    {
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        cam.MoveTo(new Vector3(-1366f, 8f, -380f));
        return "카메라를 흔함 선택 줄 가운데로";
    }
}
