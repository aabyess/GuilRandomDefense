using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

// 상점 이름표(ShopNameplateLayer) 촬영용 점검 — gameshot `call:ShopNameplateProbe.FrameLane0Shops` 로 카메라를
// 0번 레인 상점 줄 가운데로 옮기고, `call:ShopNameplateProbe.Describe` 로 떠 있는 이름표 글자·화면 자리를 적는다.
static class ShopNameplateProbe
{
    static string FrameLane0Shops()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var shops = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(b => b is ILaneShop && b.name.StartsWith("Lane1_")).ToList();
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (shops.Count == 0 || cam == null) return "❌ 0번 레인 상점이나 카메라가 없다";
        Vector3 center = shops.Aggregate(Vector3.zero, (sum, s) => sum + s.transform.position) / shops.Count;
        cam.MoveTo(center);
        return $"카메라 → 0번 레인 상점 {shops.Count}곳 가운데 {center:F0}";
    }

    static string Describe()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        GameObject layer = GameObject.Find("ShopNameplateLayer");
        if (layer == null) return "❌ ShopNameplateLayer가 없다";
        var labels = layer.GetComponentsInChildren<TMP_Text>(false);
        StringBuilder sb = new StringBuilder($"   떠 있는 이름표 {labels.Length}장:\n");
        foreach (TMP_Text t in labels.OrderBy(t => t.transform.position.x))
            sb.AppendLine($"   「{t.text}」 화면 ({t.transform.position.x:F0}, {t.transform.position.y:F0})");
        return sb.ToString();
    }
}
