using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

// 조합판 등급 머리글(CombineBoardHeaders) 촬영용 점검 — gameshot `call:CombineBoardProbe.FrameTop` 으로 카메라를
// 조합판 맨 윗줄 가운데로 옮기고, `call:CombineBoardProbe.FrameLimited` 는 전설 밑 제한됨 머리글로, `call:CombineBoardProbe.Describe` 는 머리글 글자·자리를 적는다.
static class CombineBoardProbe
{
    static TextMeshPro[] Headers() =>
        GameObject.Find("조합표_머리글") is GameObject root ? root.GetComponentsInChildren<TextMeshPro>(true) : new TextMeshPro[0];

    static string FrameTop()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        TextMeshPro[] headers = Headers();
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (headers.Length == 0 || cam == null) return "❌ 머리글이나 카메라가 없다";
        float top = headers.Max(h => h.transform.position.z);
        var row = headers.Where(h => h.transform.position.z > top - 1f).ToList();
        Vector3 center = row.Aggregate(Vector3.zero, (sum, h) => sum + h.transform.position) / row.Count;
        cam.MoveTo(center);
        return $"카메라 → 조합판 윗줄 머리글 {row.Count}개 가운데 {center:F0}";
    }

    static string FrameLimited()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        TextMeshPro limited = Headers().FirstOrDefault(h => h.text == UnitGrade.Limited.KoreanName());
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (limited == null || cam == null) return "❌ 제한됨 머리글이나 카메라가 없다";
        cam.MoveTo(limited.transform.position);
        return $"카메라 → 제한됨 머리글 {limited.transform.position:F0}";
    }

    static string Describe()
    {
        TextMeshPro[] headers = Headers();
        StringBuilder sb = new StringBuilder($"   머리글 {headers.Length}개:\n");
        foreach (TextMeshPro h in headers.OrderBy(h => h.transform.position.x))
            sb.AppendLine($"   「{h.text}」 세계 {h.transform.position:F0} · 글자 크기 {h.fontSize:F0} · 실제 폭×높이 {h.bounds.size.x:F0}×{h.bounds.size.y:F0}");
        return sb.ToString();
    }
}
