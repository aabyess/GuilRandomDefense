using UnityEngine;

/// <summary>
/// S2 목표물·실루엣·진화 포탈의 자리를 하드코딩 좌표 대신 **맵 생성기가 세운 섬 오브젝트**에서 읽는다(맵 크기·간격이 MapLayout에서 바뀌어도 따라간다).
/// 섬 오브젝트 이름 = MapLayout Zones 이름(OroSeongIsland·RumbaIsland·PoneglyphIsland·StoryZone …) — MapGenerator가 island.name으로 만든다.
/// 못 찾으면 false(호출부가 옛 자리로 되돌린다).
/// </summary>
public static class S2Anchors
{
    public static bool TryIsland(string name, out Vector3 center, out Vector2 size)
    {
        center = default; size = default;
        GameObject go = GameObject.Find(name);
        if (go == null) return false;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            center = new Vector3(b.center.x, b.max.y, b.center.z); size = new Vector2(b.size.x, b.size.z);
        }
        else { center = go.transform.position; size = new Vector2(go.transform.lossyScale.x, go.transform.lossyScale.z); }
        return true;
    }

    public static Vector3 OnGround(Vector3 p) =>
        UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 60f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : p;
}
