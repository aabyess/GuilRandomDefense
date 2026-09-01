using UnityEngine;

/// <summary>
/// 원랜디 맵 배치 수치. Docs/reference/MAP_LAYOUT.md의 미니맵을 좌표로 옮긴 것이다.
/// 생성기(MapGenerator)와 분리해 둔 이유는, 배치를 바꿀 때 생성 로직을 건드리지 않기 위해서다.
/// 좌표계: X = 미니맵의 좌→우, Z = 미니맵의 아래→위. 섬 윗면은 y = IslandTop.
/// </summary>
public static class MapLayout
{
    public const float SeaSize = 720f;
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
        new Island("Lane1", -236f, 236f, 110f, 110f, "lane"),
        new Island("Lane2", -120f, 236f, 110f, 110f, "lane"),
        new Island("Lane3", -236f, 120f, 110f, 110f, "lane"),
        new Island("Lane4", -120f, 120f, 110f, 110f, "lane"),
    };

    // 창고 — 플레이어별 개인 섬 (C키로 유닛을 보냄)
    public static readonly Island[] Warehouses =
    {
        new Island("Warehouse1",  94f, 266f, 44f, 44f, "warehouse"),
        new Island("Warehouse2", 146f, 266f, 44f, 44f, "warehouse"),
        new Island("Warehouse3",  94f, 214f, 44f, 44f, "warehouse"),
        new Island("Warehouse4", 146f, 214f, 44f, 44f, "warehouse"),
    };

    // 물범 섬 — 4개. 물범을 잡으면 전체 플레이어에게 목재 1개씩.
    public static readonly Island[] SealIslands =
    {
        new Island("SealIsland1", -280f, -180f, 26f, 26f, "seal"),
        new Island("SealIsland2", -246f, -180f, 26f, 26f, "seal"),
        new Island("SealIsland3", -212f, -180f, 26f, 26f, "seal"),
        new Island("SealIsland4", -178f, -180f, 26f, 26f, "seal"),
    };

    public static readonly Island[] Zones =
    {
        // 이벤트 존이 위, 그 아래 불멸·초월 전시가 가로로 나란히.
        new Island("PunkHazard",         0f, 130f, 90f, 60f, "event"),
        new Island("ImmortalDisplay",  150f, 120f, 90f, 50f, "display"),
        new Island("TranscendDisplay", 150f,  55f, 90f, 50f, "display"),
        new Island("StoryZone",       -250f, -80f, 120f, 100f, "story"),
        new Island("GachaIsland",      -90f, -80f, 110f, 190f, "gacha"),
        // 조합식 표는 전시 섬과 겹치지 않도록 폭을 줄이고 왼쪽으로 당겼다.
        new Island("CombineTable",     150f,  -90f, 340f, 170f, "combine"),
    };

    // 조합식 표에 노출하는 등급 6종 — 표 위의 세로 칸 하나씩.
    // 히든·영원·초월·불멸·다른세계는 의도적으로 표시하지 않는다(외부 조합표를 보고 조합).
    // 흔함은 조합으로 만들어지지 않는 기초 등급이라 표에 열이 없다.
    public static readonly UnitGrade[] CombineTableGrades =
    {
        UnitGrade.Uncommon, UnitGrade.Special,
        UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.Limited,
    };

    // 뽑기 섬 왼쪽 열: 등급 내 랜덤으로 지급하는 포탈.
    // 흔함은 "선택"이라 섬 위쪽 가로줄이 따로 담당하므로 여기 없다.
    public static readonly UnitGrade[] GachaRandomGrades =
    {
        UnitGrade.Uncommon, UnitGrade.Special, UnitGrade.Rare,
        UnitGrade.Legendary, UnitGrade.RandomUnit,
    };

    // 뽑기 섬 오른쪽 열: 조합식 없이 캐릭터만 전시하는 등급.
    // 조합식 표(하단 우측)에는 올리지 않기로 확정된 등급들이다.
    public static readonly UnitGrade[] GachaDisplayGrades =
    {
        UnitGrade.RandomUnit, UnitGrade.OtherWorld,
    };

    /// <summary>
    /// 레인 둘레를 도는 순환 경로. 적은 이 경로를 계속 돈다(도착 지점이 없다).
    /// 왼쪽 위에서 출발해 아래로 내려간 뒤 반시계 방향으로 돈다.
    /// 화면 기준 위쪽이 +Z이므로 왼쪽 위 = (-x, +z).
    /// </summary>
    public static Vector3[] LaneLoop(Island lane, float inset = 14f)
    {
        float halfX = lane.size.x * 0.5f - inset;
        float halfZ = lane.size.y * 0.5f - inset;
        float x = lane.center.x;
        float z = lane.center.y;

        return new[]
        {
            new Vector3(x - halfX, IslandTop, z + halfZ),   // 왼쪽 위 — 출발
            new Vector3(x - halfX, IslandTop, z - halfZ),   // 왼쪽 아래
            new Vector3(x + halfX, IslandTop, z - halfZ),   // 오른쪽 아래
            new Vector3(x + halfX, IslandTop, z + halfZ),   // 오른쪽 위
        };
    }
}
