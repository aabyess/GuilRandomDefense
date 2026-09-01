using UnityEngine;
using UnityEngine.UI;

// 미니맵 위에 유닛·위습·적을 점으로 찍는다. 점마다 오브젝트를 만들지 않고 OnPopulateMesh에서
// 한 번에 그린다 — 적이 수백 마리가 될 수 있는 게임이라 오브젝트 개수를 늘리면 안 된다.
public class MinimapBlips : MaskableGraphic
{
    const float RefreshesPerSecond = 10f;
    const float BlipSize = 3.5f;

    static readonly Color MineColor = new Color(0.2f, 0.9f, 0.3f, 1f);   // 내 유닛·위습
    static readonly Color AllyColor = new Color(0.3f, 0.55f, 0.95f, 1f); // 다른 플레이어 유닛
    static readonly Color EnemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);  // 적

    MinimapCamera minimapCamera;
    float nextRefreshTime;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false; // 미니맵 클릭 이동을 가리면 안 된다.
    }

    // GameHud가 부모에 붙인 뒤 명시적으로 넘겨준다 — Awake는 SetParent보다 먼저 돌아서 스스로는 못 찾는다.
    public void SetMinimap(MinimapCamera camera)
    {
        minimapCamera = camera;
    }

    void Update()
    {
        // 매 프레임 SetVerticesDirty()를 부르면 캔버스 전체가 다시 빌드된다. 초당 10회로 제한한다.
        if (Time.time < nextRefreshTime) return;
        nextRefreshTime = Time.time + 1f / RefreshesPerSecond;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (minimapCamera == null) return;

        foreach (Selectable selectable in Selectable.All)
        {
            if (selectable == null) continue;

            Color color = MineColor;
            if (selectable.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId != LocalPlayer.LocalPlayerId)
                color = AllyColor;

            AddBlip(vh, selectable.transform.position, color);
        }

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null) continue;
            AddBlip(vh, enemy.transform.position, EnemyColor);
        }
    }

    void AddBlip(VertexHelper vh, Vector3 worldPosition, Color color)
    {
        Vector2 center = minimapCamera.WorldToMinimapLocal(worldPosition);
        float half = BlipSize * 0.5f;

        int startIndex = vh.currentVertCount;

        vh.AddVert(new Vector3(center.x - half, center.y - half), color, Vector2.zero);
        vh.AddVert(new Vector3(center.x - half, center.y + half), color, Vector2.zero);
        vh.AddVert(new Vector3(center.x + half, center.y + half), color, Vector2.zero);
        vh.AddVert(new Vector3(center.x + half, center.y - half), color, Vector2.zero);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }
}
