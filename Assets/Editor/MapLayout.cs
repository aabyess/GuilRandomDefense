using UnityEngine;

/// <summary>
/// 원랜디 맵 배치 수치. Docs/reference/MAP_LAYOUT.md의 미니맵을 좌표로 옮긴 것이다.
/// 생성기(MapGenerator)와 분리해 둔 이유는, 배치를 바꿀 때 생성 로직을 건드리지 않기 위해서다.
/// 좌표계: X = 미니맵의 좌→우, Z = 미니맵의 아래→위. 섬 윗면은 y = IslandTop.
/// </summary>
public static class MapLayout
{
    // 원작 비율 작업(PM 지시 2026-09-23, "맵·속도·사거리를 원작 비율로 맞추기"). 레인·창고·
    // 물범섬·존의 좌표·크기가 전부 이 값을 거친다. 1단계에서 Scale=1(무변화)로 리팩터만
    // 검증했고, 2단계부터 실제 배율(정본 배율 4.167, 근거: 충돌 크기 기준·"레인 폭÷유닛
    // 지름" 기준이 둘 다 4.167로 나온다 — 속도 기준 23.73은 안 쓴다, PM 지시 참고)을 켠다.
    public const float Scale = 4.167f;

    // 섬 전체는 X -310~320, Z -193~291 (630×484). 바다는 그보다 훨씬 커야 한다 —
    // 카메라를 가장자리까지 밀었을 때 바다 밖 회색이 보이면 맵이 끊긴 것처럼 읽힌다.
    // 최대 높이(420)에서 경계 끝까지 밀면 가로로 약 680이 보인다.
    // 카메라 경계(±380)에 그 절반을 더한 720까지 바다가 있어야 밖이 안 보인다.
    // (2026-09-23, 2단계) 위 수치는 Scale=1 시절 기준이다 — SeaSize는 이제 Scale을 타서
    // 맵 전체 배율과 같이 커진다(MapGenerator.SetUpCamera의 maxHeight·CameraMargin도 같은
    // 배율로 맞췄다). IslandTop·IslandThickness는 Y축(높이·두께)이라 그대로 둔다.
    public const float SeaSize = 1600f * Scale;
    public const float IslandTop = 1f;      // 섬 윗면 높이 — 바다보다 한 단 높아 지상 유닛이 넘어가지 못한다
    public const float IslandThickness = 1f;
    public const int SeaAreaIndex = 3;      // ProjectSettings/NavMeshAreas.asset 3번 = Sea

    public struct Island
    {
        public string name;
        public Vector2 center;   // (x, z)
        public Vector2 size;     // (x, z)
        public string tint;      // MapGenerator의 머티리얼 키

        /// <summary>섬 윗면 한가운데의 월드 좌표.</summary>
        public Vector3 Center3 => new Vector3(center.x, IslandTop, center.y);

        public Island(string name, float x, float z, float sizeX, float sizeZ, string tint)
        {
            this.name = name;
            center = new Vector2(x, z);
            size = new Vector2(sizeX, sizeZ);
            this.tint = tint;
        }
    }

    // 메인 방어 필드 — 2×2로 붙은 레인 4개 = 플레이어 4명.
    //
    // (2026-09-23, 2단계, PM 지시) 레인만 원작 대각선 비율(가로가 긴 1.20)로 바꾼다. 목표
    // 크기 = 원작 레인 평균 3256×2712 ÷ Scale(4.167) = 781.4×650.9. 다른 섬처럼 "기존 리터럴
    // × Scale"로는 안 나온다 — 기존 165×211(세로가 긴 0.782)은 원작과 반대 비율이라 리터럴
    // 자체를 갈아야 한다(그래서 아래는 Scale을 다시 안 곱인다 — 781.4를 Scale로 나눈 값에
    // 또 Scale을 곱이면 의미 없는 이중연산이다). 목표값을 리터럴로 고정했다.
    //
    // ⚠️ "레인 간격"(원작 x 960·z 1408 ÷ Scale = 230.4·337.9, PM 지시)의 뜻을 확정 못 했다.
    // 중심간 거리로 읽으면 레인 크기(781.4)의 절반(390.7)보다 작아서 2×2 인접 배치가
    // 수학적으로 불가능하다 — 그래서 "섬 가장자리 사이 간격"으로 해석했다: 중심간 거리 =
    // 크기 + 이 간격. PM 확인 대기 중(2026-09-23 보고).
    const float LaneSizeX = 781.4f;
    const float LaneSizeZ = 650.9f;
    const float LaneGapX = 230.4f;
    const float LaneGapZ = 337.9f;
    const float LaneSpacingX = LaneSizeX + LaneGapX;   // 1011.8 — 레인 중심간 x거리
    const float LaneSpacingZ = LaneSizeZ + LaneGapZ;   // 988.8  — 레인 중심간 z거리

    // 오른쪽 열(레인2·4)의 오른쪽 끝을 -220에 둔다 — Scale=4.167 기준 PunkHazard 오른쪽 끝이
    // x=187.5라 32.5 여유를 두고 확실히 비껴간다(사장님 지시 "오른쪽에 펑크해저드·창고가
    // 있어 레인은 왼쪽으로 펼치는 쪽이 낫다"). 아래로는 GachaIsland·StoryZone이 전부 z<63에
    // 있어 필드 바닥을 z=100에서 시작하면 안 걸린다. MapGenerator.cs:3508 Overlaps()와 같은
    // 식으로 Warehouses·SealIslands·Zones 전체와 대조해 0건 확인했다(2026-09-23, 별도 계산).
    const float LaneRightEdgeX = -220f;
    const float LaneRightColumnX = LaneRightEdgeX - LaneSizeX * 0.5f;   // -610.7
    const float LaneLeftColumnX = LaneRightColumnX - LaneSpacingX;     // -1622.5
    const float LaneBottomRowZ = 100f + LaneSizeZ * 0.5f;              // 425.45
    const float LaneTopRowZ = LaneBottomRowZ + LaneSpacingZ;           // 1414.25

    public static readonly Island[] Lanes =
    {
        new Island("Lane1", LaneLeftColumnX,  LaneTopRowZ,    LaneSizeX, LaneSizeZ, "lane"),
        new Island("Lane2", LaneRightColumnX, LaneTopRowZ,    LaneSizeX, LaneSizeZ, "lane"),
        new Island("Lane3", LaneLeftColumnX,  LaneBottomRowZ, LaneSizeX, LaneSizeZ, "lane"),
        new Island("Lane4", LaneRightColumnX, LaneBottomRowZ, LaneSizeX, LaneSizeZ, "lane"),
    };

    // 창고 — 플레이어별 개인 섬 (C키로 유닛을 보냄)
    public static readonly Island[] Warehouses =
    {
        new Island("Warehouse1",  94f * Scale, 266f * Scale, 44f * Scale, 44f * Scale, "warehouse"),
        new Island("Warehouse2", 146f * Scale, 266f * Scale, 44f * Scale, 44f * Scale, "warehouse"),
        new Island("Warehouse3",  94f * Scale, 214f * Scale, 44f * Scale, 44f * Scale, "warehouse"),
        new Island("Warehouse4", 146f * Scale, 214f * Scale, 44f * Scale, 44f * Scale, "warehouse"),
    };

    // 물범 섬 — 4개. 물범을 잡으면 전체 플레이어에게 목재 1개씩.
    // 중심 간격을 34→40으로 넓혔다(사장님 지시, 2026-09-03: "너무 따닥 붙어있음") — 빈틈이
    // 8이던 게 14(섬 크기 26의 절반쯤)가 된다. StoryZone 중심(-250)에 맞춰 다시 배치했다 —
    // 예전 배치(중심 -229)를 그대로 넓히면 스토리존 오른쪽 경계(-160, 1.5배 키운 뒤 폭
    // 180 기준)를 넘어간다. 이 배치는 양옆 다 17만큼 여유를 두고 안에 들어간다.
    public static readonly Island[] SealIslands =
    {
        new Island("SealIsland1", -362f * Scale, -180f * Scale, 26f * Scale, 26f * Scale, "seal"),
        new Island("SealIsland2", -314f * Scale, -180f * Scale, 26f * Scale, 26f * Scale, "seal"),
        new Island("SealIsland3", -266f * Scale, -180f * Scale, 26f * Scale, 26f * Scale, "seal"),
        new Island("SealIsland4", -218f * Scale, -180f * Scale, 26f * Scale, 26f * Scale, "seal"),
    };

    public static readonly Island[] Zones =
    {
        // 이벤트 존이 위, 그 아래 불멸·초월 전시가 가로로 나란히.
        new Island("PunkHazard",         0f * Scale, 130f * Scale, 90f * Scale, 60f * Scale, "event"),
        // 초월은 25종이 가로 14칸씩 두 줄로 서므로 세로가 그만큼만 있으면 된다.
        // 불멸은 화로를 둘러싼 원형이라 지름이 필요해서 50을 유지한다.
        // 둘 다 조합식 표 바로 위로 내려, 사이에 비던 자리를 없앴다.
        new Island("ImmortalDisplay",  150f * Scale,  52f * Scale, 90f * Scale, 50f * Scale, "display"),
        new Island("TranscendDisplay", 150f * Scale,   8f * Scale, 90f * Scale, 26f * Scale, "display"),
        // 1.5배로 키운 값(원래 120x100). 여유가 빠듯하다 — 봉인섬과 z로 12,
        // 뽑기섬과 x로 10밖에 안 남으니 더 키우려면 이웃을 먼저 옮겨야 한다.
        new Island("StoryZone",       -290f * Scale, -80f * Scale, 180f * Scale, 150f * Scale, "story"),
        // 오른쪽 전시 칸이 다른세계 조합식 한 줄(재료 6칸 + 비용 3칸)을 담아야 해서 폭을 넓혔다.
        new Island("GachaIsland",      -82f * Scale, -80f * Scale, 136f * Scale, 190f * Scale, "gacha"),
        // 조합식 표는 전시 섬과 겹치지 않도록 폭을 줄이고 왼쪽으로 당겼다.
        new Island("CombineTable",     137f * Scale, -110f * Scale, 274f * Scale, 186f * Scale, "combine"),
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
        // 2026-09-23 사장님 지시 — 불멸·영원도 표에 올린다. 스킨이 다 들어와 인형으로 서기 때문에
        // "조합식 없이 전시만" 하던 이유가 사라졌다. 초월은 가로줄 전시 섬이 따로 있어 그대로 둔다.
        UnitGrade.Immortal, UnitGrade.Eternal,
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
    public const float ShopStripDepth = 26f * Scale;

    /// <summary>상점 줄 바로 위, 새 유닛이 처음 서는 우리가 놓이는 줄.</summary>
    public const float UnitPenDepth = 20f * Scale;

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

    public static Vector3[] LaneLoop(Island lane, float inset = 14f * Scale)
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
