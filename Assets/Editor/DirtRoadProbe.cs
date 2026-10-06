using System.Linq;
using UnityEngine;

/// <summary>
/// 흙길 시안 사진(10-06 구현담당3) — gameshot: call:DirtRoadProbe.Small|Medium|Large 로 0번 레인 흙길을 시안으로 바꾼다(플레이 중 메모리에만 — 저장 안 함),
/// call:DirtRoadProbe.CornerSW|CornerNE|CornerSE 로 카메라를 모서리로 옮긴다(공통 사진: 전체·모서리 둘).
/// </summary>
public static class DirtRoadProbe
{
    const string Name = "시안_흙길";

    static string Show(float ratio)
    {
        MapLayout.Island lane = MapLayout.Lanes[0];
        foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith($"{lane.name}_흙길")).ToList())
            if (old.name != Name) old.gameObject.SetActive(false);
        var prev = GameObject.Find(Name);
        if (prev != null) Object.DestroyImmediate(prev);
        MapGenerator.DirtRoadRects(lane, out Rect outer, out Rect inner, out float width);
        Mesh mesh = DirtRoadBuilder.Build(outer, inner, width, ratio, lane.name.GetHashCode() & 0x7fffffff, MapGenerator.DirtUvPerUnit());
        var go = new GameObject(Name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = DirtRoadBuilder.Materials(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Map/dirt.mat"));
        return $"   시안 R={ratio}×W (W={width:F1}) 메시 정점 {mesh.vertexCount} · 바깥 {outer} · 안쪽 {inner}";
    }

    public static string Small() => Show(DirtRoadBuilder.CornerRatios[0]);
    public static string Medium() => Show(DirtRoadBuilder.CornerRatios[1]);
    public static string Large() => Show(DirtRoadBuilder.CornerRatios[2]);

    static string Look(float fx, float fz)
    {
        MapGenerator.DirtRoadRects(MapLayout.Lanes[0], out Rect outer, out Rect inner, out float width);
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 컨트롤러 없음";
        Vector3 p = new Vector3(Mathf.Lerp(outer.xMin, outer.xMax, fx), MapLayout.IslandTop, Mathf.Lerp(outer.yMin, outer.yMax, fz));
        cam.MoveTo(p);
        return $"   카메라 → {p:F0}";
    }

    public static string CornerSW() => Look(0.04f, 0.06f);
    public static string CornerNE() => Look(0.96f, 0.94f);
    public static string CornerSE() => Look(0.96f, 0.06f);
    public static string Overview() => Look(0.5f, 0.5f);
}
