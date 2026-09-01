using UnityEngine;

/// <summary>
/// 원랜디 맵 배치 수치. Docs/reference/MAP_LAYOUT.md의 미니맵을 좌표로 옮긴 것이다.
/// 생성기(MapGenerator)와 분리해 둔 이유는, 배치를 바꿀 때 생성 로직을 건드리지 않기 위해서다.
/// 좌표계: X = 미니맵의 좌→우, Z = 미니맵의 아래→위. 섬 윗면은 y = IslandTop.
/// </summary>
public static class MapLayout
{
    // 섬 전체는 X -310~320, Z -193~291 (630×484). 바다는 그보다 훨씬 커야 한다 —
    // 카메라를 가장자리까지 밀었을 때 바다 밖 회색이 보이면 맵이 끊긴 것처럼 읽힌다.
    // 최대 높이(420)에서 경계 끝까지 밀면 가로로 약 680이 보인다.
    // 카메라 경계(±380)에 그 절반을 더한 720까지 바다가 있어야 밖이 안 보인다.
    public const float SeaSize = 1600f;
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
        // 필드를 1.5배(110→165)로 키웠다. 아래 두 줄(유닛 우리 20 + 상점 26)은 그대로다 —
        // 건물과 우리는 커질 이유가 없고, 커지면 오히려 필드에서 멀어진다.
        // 넓힌 만큼 왼쪽으로 펼쳤다. 오른쪽은 펑크해저드·창고가 있어 못 넓힌다.
        new Island("Lane1", -318f, 362f, 165f, 211f, "lane"),
        new Island("Lane2", -148f, 362f, 165f, 211f, "lane"),
        new Island("Lane3", -318f, 144f, 165f, 211f, "lane"),
        new Island("Lane4", -148f, 144f, 165f, 211f, "lane"),
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
        // 초월은 25종이 가로 14칸씩 두 줄로 서므로 세로가 그만큼만 있으면 된다.
        // 불멸은 화로를 둘러싼 원형이라 지름이 필요해서 50을 유지한다.
        // 둘 다 조합식 표 바로 위로 내려, 사이에 비던 자리를 없앴다.
        new Island("ImmortalDisplay",  150f,  52f, 90f, 50f, "display"),
        new Island("TranscendDisplay", 150f,   8f, 90f, 26f, "display"),
        new Island("StoryZone",       -250f, -80f, 120f, 100f, "story"),
        // 오른쪽 전시 칸이 다른세계 조합식 한 줄(재료 6칸 + 비용 3칸)을 담아야 해서 폭을 넓혔다.
        new Island("GachaIsland",      -82f, -80f, 136f, 190f, "gacha"),
        // 조합식 표는 전시 섬과 겹치지 않도록 폭을 줄이고 왼쪽으로 당겼다.
        new Island("CombineTable",     137f, -110f, 274f, 186f, "combine"),
        // 도박소. StoryZone 서쪽, 같은 z대역이라 나란히 배치되고 40유닛 간격으로 안 겹친다.
    };

    // 조합식 표에 노출하는 등급 6종 — 표 위의 세로 칸 하나씩.
    // 히든·영원·초월·불멸·다른세계는 의도적으로 표시하지 않는다(외부 조합표를 보고 조합).
    // 흔함은 조합으로 만들어지지 않는 기초 등급이라 표에 열이 없다.
    // 히든은 원래 "맵에 표시하지 않는 등급"이었으나 피드백을 받아 표에 넣기로 했다(2026-09-01).
    public static readonly UnitGrade[] CombineTableGrades =
    {
        UnitGrade.Uncommon, UnitGrade.Special,
        UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.Limited,
        UnitGrade.Hidden,
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
    /// 뽑기 섬 전시 칸에서 캐릭터 대신 조합식 줄로 보여줄 등급.
    /// 조합으로만 나오는데 조합식 표에는 없는 등급이라, 여기가 만드는 법을 보는 유일한 자리다.
    /// </summary>
    public static readonly UnitGrade[] GachaRecipeGrades =
    {
        UnitGrade.OtherWorld,
    };

    /// <summary>
    /// 레인 둘레를 도는 순환 경로. 적은 이 경로를 계속 돈다(도착 지점이 없다).
    /// 왼쪽 위에서 출발해 아래로 내려간 뒤 반시계 방향으로 돈다.
    /// 화면 기준 위쪽이 +Z이므로 왼쪽 위 = (-x, +z).
    /// </summary>
    /// <summary>
    /// 레인 아래에 덧댄 상점 줄의 깊이. 도박소·강화소 셋·도움소가 여기 가로로 늘어선다.
    /// 적은 여기로 안 내려온다 — 순찰 경로도 흙길도 이 줄을 뺀 필드에서만 잡는다.
    /// </summary>
    public const float ShopStripDepth = 26f;

    /// <summary>상점 줄 바로 위, 새 유닛이 처음 서는 우리가 놓이는 줄.</summary>
    public const float UnitPenDepth = 20f;

    /// <summary>필드가 아닌 아래 두 줄(우리 + 상점)의 합.</summary>
    public const float LaneApronDepth = ShopStripDepth + UnitPenDepth;

    /// <summary>적이 도는 필드. 섬에서 아래 두 줄을 뺀 나머지다.</summary>
    public static Island LaneField(Island lane)
    {
        return new Island(lane.name,
            lane.center.x, lane.center.y + LaneApronDepth * 0.5f,
            lane.size.x, lane.size.y - LaneApronDepth, lane.tint);
    }

    /// <summary>유닛 우리가 놓이는 줄. 상점 줄과 필드 사이다.</summary>
    public static Island LaneUnitPenRow(Island lane)
    {
        float bottom = lane.center.y - lane.size.y * 0.5f;
        return new Island(lane.name,
            lane.center.x, bottom + ShopStripDepth + UnitPenDepth * 0.5f,
            lane.size.x, UnitPenDepth, lane.tint);
    }

    /// <summary>상점이 서는 아래 줄.</summary>
    public static Island LaneShopStrip(Island lane)
    {
        return new Island(lane.name,
            lane.center.x, lane.center.y - (lane.size.y - ShopStripDepth) * 0.5f,
            lane.size.x, ShopStripDepth, lane.tint);
    }

    public static Vector3[] LaneLoop(Island lane, float inset = 14f)
    {
        Island field = LaneField(lane);
        float halfX = field.size.x * 0.5f - inset;
        float halfZ = field.size.y * 0.5f - inset;
        float x = field.center.x;
        float z = field.center.y;

        return new[]
        {
            new Vector3(x - halfX, IslandTop, z + halfZ),   // 왼쪽 위 — 출발
            new Vector3(x - halfX, IslandTop, z - halfZ),   // 왼쪽 아래
            new Vector3(x + halfX, IslandTop, z - halfZ),   // 오른쪽 아래
            new Vector3(x + halfX, IslandTop, z + halfZ),   // 오른쪽 위
        };
    }
}
