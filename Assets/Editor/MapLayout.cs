using UnityEngine;

/// <summary>
/// 원랜디 맵 배치 수치. Docs/reference/MAP_LAYOUT.md의 미니맵을 좌표로 옮긴 것이다.
/// 생성기(MapGenerator)와 분리해 둔 이유는, 배치를 바꿀 때 생성 로직을 건드리지 않기 위해서다.
/// 좌표계: X = 미니맵의 좌→우, Z = 미니맵의 아래→위. 섬 윗면은 y = IslandTop.
/// </summary>
public static class MapLayout
{
    public const float SeaSize = 420f;
    public const float IslandTop = 1f;      // 섬 윗면 높이 — 바다보다 한 단 높아 지상 유닛이 넘어가지 못한다
    public const float IslandThickness = 1f;
    public const int SeaAreaIndex = 3;      // ProjectSettings/NavMeshAreas.asset 3번 = Sea

    public struct Island
    {
        public string name;
        public Vector2 center;   // (x, z)
        public Vector2 size;     // (x, z)
        public string tint;      // MapGenerator의 머티리얼 키

        public Island(string name, float x, float z, float sizeX, float sizeZ, string tint)
        {
            this.name = name;
            center = new Vector2(x, z);
            size = new Vector2(sizeX, sizeZ);
            this.tint = tint;
        }
    }

    // 메인 방어 필드 — 2×2로 붙은 레인 4개 = 플레이어 4명
    public static readonly Island[] Lanes =
    {
        new Island("Lane1", -120f, 122f, 60f, 60f, "lane"),
        new Island("Lane2",  -56f, 122f, 60f, 60f, "lane"),
        new Island("Lane3", -120f,  58f, 60f, 60f, "lane"),
        new Island("Lane4",  -56f,  58f, 60f, 60f, "lane"),
    };

    // 창고 — 플레이어별 개인 섬 (C키로 유닛을 보냄)
    public static readonly Island[] Warehouses =
    {
        new Island("Warehouse1",  80f, 122f, 28f, 28f, "warehouse"),
        new Island("Warehouse2", 116f, 122f, 28f, 28f, "warehouse"),
        new Island("Warehouse3",  80f,  86f, 28f, 28f, "warehouse"),
        new Island("Warehouse4", 116f,  86f, 28f, 28f, "warehouse"),
    };

    // 물범 섬 — 4개. 물범을 잡으면 전체 플레이어에게 목재 1개씩.
    public static readonly Island[] SealIslands =
    {
        new Island("SealIsland1", -148f, -108f, 18f, 18f, "seal"),
        new Island("SealIsland2", -122f, -108f, 18f, 18f, "seal"),
        new Island("SealIsland3",  -96f, -108f, 18f, 18f, "seal"),
        new Island("SealIsland4",  -70f, -108f, 18f, 18f, "seal"),
    };

    public static readonly Island[] Zones =
    {
        // 이벤트 존이 위, 그 아래 불멸·초월 전시가 가로로 나란히.
        new Island("PunkHazard",       150f,  20f, 84f, 36f, "event"),
        new Island("ImmortalDisplay",  128f, -30f, 40f, 36f, "display"),
        new Island("TranscendDisplay", 172f, -30f, 40f, 36f, "display"),
        new Island("StoryZone",       -120f, -46f, 54f, 54f, "story"),
        new Island("GachaIsland",      -42f, -46f, 46f, 66f, "gacha"),
        // 조합식 표는 전시 섬과 겹치지 않도록 폭을 줄이고 왼쪽으로 당겼다.
        new Island("CombineTable",      40f, -62f, 112f, 74f, "combine"),
    };

    // 조합식 표에 노출하는 등급 6종 — 표 위의 세로 칸 하나씩.
    // 히든·영원·초월·불멸·다른세계는 의도적으로 표시하지 않는다(외부 조합표를 보고 조합).
    public static readonly UnitGrade[] CombineTableGrades =
    {
        UnitGrade.Common, UnitGrade.Uncommon, UnitGrade.Special,
        UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.Limited,
    };

    // 뽑기 섬의 포탈 — 위쪽이 낮은 등급. 사용자 설명 기준("제일 위에 흔함").
    public static readonly UnitGrade[] GachaPortalGrades =
    {
        UnitGrade.Common, UnitGrade.Uncommon, UnitGrade.Special,
        UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.RandomUnit,
    };

    /// <summary>레인 둘레를 도는 순환 경로. 적은 이 경로를 계속 돈다(도착 지점이 없다).</summary>
    public static Vector3[] LaneLoop(Island lane, float inset = 8f)
    {
        float halfX = lane.size.x * 0.5f - inset;
        float halfZ = lane.size.y * 0.5f - inset;
        float x = lane.center.x;
        float z = lane.center.y;

        return new[]
        {
            new Vector3(x - halfX, IslandTop, z - halfZ),
            new Vector3(x + halfX, IslandTop, z - halfZ),
            new Vector3(x + halfX, IslandTop, z + halfZ),
            new Vector3(x - halfX, IslandTop, z + halfZ),
        };
    }
}
