using System.Collections.Generic;
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
    // ⚠️ 값 자체는 WorldScale(런타임 어셈블리)에 있다 — 길찾기 표본 반경처럼 Assets/Scripts
    // 쪽에서도 같은 배율을 써야 하는데, 런타임은 에디터 어셈블리를 못 보기 때문이다.
    // 배율을 바꿀 때는 WorldScale.Value 한 곳만 고치면 된다.
    public const float Scale = WorldScale.Value;

    // 섬 전체는 X -310~320, Z -193~291 (630×484). 바다는 그보다 훨씬 커야 한다 —
    // 카메라를 가장자리까지 밀었을 때 바다 밖 회색이 보이면 맵이 끊긴 것처럼 읽힌다.
    // 최대 높이(420)에서 경계 끝까지 밀면 가로로 약 680이 보인다.
    // 카메라 경계(±380)에 그 절반을 더한 720까지 바다가 있어야 밖이 안 보인다.
    // (2026-09-23, 2단계) 위 수치는 Scale=1 시절 기준이다 — SeaSize는 이제 Scale을 타서
    // 맵 전체 배율과 같이 커진다(MapGenerator.SetUpCamera의 maxHeight·CameraMargin도 같은
    // 배율로 맞췄다). IslandTop·IslandThickness는 Y축(높이·두께)이라 그대로 둔다.
    public const float SeaSize = 1600f * Scale;

    /// <summary>
    /// 바위 치마가 잔디보다 넓게 나오는 양(양변 합). **섬의 보이는 바깥 끝은 잔디가 아니라 이 치마다.**
    /// MapGenerator가 그리고(<c>island.size + CliffOverhang</c>), 여기서는 간격을 잴 때 쓴다 —
    /// 같은 두 섬이 「섬변끼리 38.1 / 보이는 끝끼리 34.6」으로 갈리는 이유가 이 값이다.
    /// 사람 눈에 보이는 간격을 말할 때는 **치마 기준**이 맞다.
    /// </summary>
    public const float CliffOverhang = 3.5f;

    /// <summary>치마가 한쪽 변으로 더 나오는 양.</summary>
    public const float CliffMargin = CliffOverhang * 0.5f;

    /// <summary>
    /// NavMesh 굽기 복셀 크기. MapGenerator.BuildNavMesh가 이 값을 쓴다.
    ///
    /// 🔴 **이 값과 <see cref="IslandTop"/>은 한 쌍이다. 하나만 바꾸면 조용히 깨진다.**
    /// Recast는 한 기둥에서 윗면이 가까운 두 층을 **한 층으로 합치고 영역 번호는 큰 쪽을 남긴다.**
    /// 바다 윗면(y=0, Sea 영역 3)과 섬 윗면이 복셀 두 칸 안으로 붙으면 섬 윗면까지 **바다로**
    /// 구워지고, 지상 유닛은 areaMask에서 Sea가 빠져 있어 **섬 안으로 못 들어간다.**
    ///
    /// 2026-09-23에 실제로 그랬다. 맵이 4.167배가 되며 넓이가 17배가 되자 굽기가 무거워져
    /// 복셀을 0.5 → 2.0으로 올렸는데, 섬 윗면 높이 1은 "Y축이라 안 탄다"며 그대로 뒀다.
    /// 높이 차 1 &lt; 복셀 2.0이라 두 면이 한 층으로 합쳐졌고, 레인 섬 윗면의 **89%가 바다 영역**이
    /// 됐다(땅 칸 3000개 중 걸을 수 있는 곳 344개). Y를 안 키운 것 자체는 맞았지만
    /// **복셀을 4배로 키운 것과의 관계를 아무도 안 봤다.**
    ///
    /// 그래서 IslandTop을 이 값에서 유도한다. 복셀을 또 바꾸면 섬 높이가 따라온다.
    /// </summary>
    public const float NavMeshVoxelSize = 2f;

    /// <summary>
    /// 섬 윗면 높이 — 바다보다 높아 지상 유닛이 넘어가지 못한다.
    /// 복셀 **네 칸**(합쳐지지 않는 최소는 두 칸)으로 여유를 둔다. 위 주석 참고.
    /// </summary>
    public const float IslandTop = NavMeshVoxelSize * 4f;   // 8

    public const float IslandThickness = 1f;
    public const int SeaAreaIndex = 3;      // ProjectSettings/NavMeshAreas.asset 3번 = Sea

    /// <summary>
    /// 사장님이 「더 벌려라 / 좁혀라」로 말씀하시는 그 간격들. **박아 둔 목표값이 아니라
    /// 섬 정의에서 재서 낸다** — 유도식(<see cref="DownShift"/>·<see cref="GachaCenterX"/>)이
    /// 섬 정의와 어긋나면 이 숫자가 목표에서 벗어나고, MapGenerator 보고문이 그것을 찍는다.
    /// 간격은 **치마(<see cref="CliffMargin"/>) 기준** — 사람 눈에 보이는 끝이 잔디가 아니라 치마다.
    /// </summary>
    public struct SpacingReadout
    {
        // 🔴 예전에는 `laneToIsland` 하나가 **두 관계를 같이** 재고 있었다 — 무리 최북단이
        //    전시 섬이 되자 「레인과의 거리」라는 이름으로 「레인 오른쪽에 있는 섬과의 z 차」를
        //    쟀고, −216 같은 음수가 나오는데 결함이 아니었다. 이름 하나로 두 관계를 재면
        //    **사장님이 어느 쪽을 말씀하시는지 가릴 수 없다.** 그래서 둘로 나눴다(PM 지시 09-24).
        public float punkToDisplay;     // 펑크해저드 아래변 ↔ 불멸 전시 윗변 (무리 높이를 정하는 기준)
        public float laneToIsland;      // 레인 남쪽 끝 ↔ **x가 레인과 겹치는** 옮긴 섬의 최북단
        public string laneToIslandBy;   // 그 최북단이 어느 섬인지 — 기준이 바뀌면 숫자보다 이게 먼저 보인다
        public float storyToGacha;      // 스토리존 ↔ 뽑기섬
        public float gachaToCombine;    // 뽑기섬 ↔ 조합판
        public float combineToTranscend;   // 조합판 윗변 ↔ 아래 전시 섬(초월)
        public float transcendToImmortal;  // 초월 전시 ↔ 불멸 전시
        public float islandClusterNorthZ;
    }

    /// <summary>
    /// 한 덩어리로 움직이는 섬들. LeftShift·DownShift를 타는 것이 곧 이 목록이다.
    /// ⚠️ 섬을 이 무리에 넣거나 빼면 **여기도 같이 고쳐야 한다** — 안 고치면 보고문의
    ///    「레인↔섬」이 엉뚱한 섬을 기준으로 재서 500이라고 거짓말한다.
    ///    2026-09-24에 전시 둘이 무리 밖에 있어 「저것만 안 따라왔다」가 났다.
    /// </summary>
    static readonly string[] MovedZoneNames =
        { "StoryZone", "GachaIsland", "CombineTable", "TranscendDisplay", "ImmortalDisplay" };

    static Island Find(Island[] set, string name)
    {
        foreach (Island island in set)
            if (island.name == name) return island;
        return default;
    }

    static Island Zone(string name) => Find(Zones, name);

    /// <summary>간격을 실제 섬 정의에서 재서 돌려준다.</summary>
    public static SpacingReadout MeasureSpacing()
    {
        Island story = Zone("StoryZone");
        Island gacha = Zone("GachaIsland");
        Island combine = Zone("CombineTable");
        Island transcend = Zone("TranscendDisplay");
        Island immortal = Zone("ImmortalDisplay");

        // 레인 무리가 x로 차지하는 띠. 앞치마는 레인 폭의 74%라 레인이 바깥을 정한다.
        float laneMinX = float.MaxValue, laneMaxX = float.MinValue;
        foreach (Island lane in Lanes)
        {
            laneMinX = Mathf.Min(laneMinX, lane.center.x - lane.size.x * 0.5f);
            laneMaxX = Mathf.Max(laneMaxX, lane.center.x + lane.size.x * 0.5f);
        }

        // 옮긴 섬 중 **레인 아래에 실제로 놓인 것**만 골라 최북단을 잡는다.
        // ⚠️ x가 안 겹치는 섬(전시 둘)을 섞으면 「레인과의 거리」가 레인 옆 섬의 z 차가 되어
        //    음수가 나오고, 그걸 띄우려다 레인 아래 섬까지 밀려난다. 오늘 그 일이 났다.
        float north = float.MinValue;
        string northBy = "없음";
        void Consider(Island island)
        {
            float x0 = island.center.x - island.size.x * 0.5f;
            float x1 = island.center.x + island.size.x * 0.5f;
            if (x1 <= laneMinX || x0 >= laneMaxX) return;   // 레인 띠와 x가 안 겹친다
            float top = island.center.y + island.size.y * 0.5f;
            if (top <= north) return;
            north = top;
            northBy = island.name;
        }
        foreach (Island island in SealIslands) Consider(island);
        foreach (string name in MovedZoneNames) Consider(Zone(name));
        north += CliffMargin;

        return new SpacingReadout
        {
            punkToDisplay = (PunkHazardBottomZ - CliffMargin)
                            - (immortal.center.y + immortal.size.y * 0.5f + CliffMargin),
            laneToIslandBy = northBy,
            combineToTranscend = (transcend.center.y - transcend.size.y * 0.5f - CliffMargin)
                                 - (combine.center.y + combine.size.y * 0.5f + CliffMargin),
            transcendToImmortal = (immortal.center.y - immortal.size.y * 0.5f - CliffMargin)
                                  - (transcend.center.y + transcend.size.y * 0.5f + CliffMargin),
            laneToIsland = LaneClusterSouthEdgeZ - north,
            storyToGacha = (gacha.center.x - gacha.size.x * 0.5f - CliffMargin)
                           - (story.center.x + story.size.x * 0.5f + CliffMargin),
            gachaToCombine = (combine.center.x - combine.size.x * 0.5f - CliffMargin)
                             - (gacha.center.x + gacha.size.x * 0.5f + CliffMargin),
            islandClusterNorthZ = north,
        };
    }

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
    // ⚠️ 원작 3256×2712는 **레인 섬 전체가 아니라 적이 도는 방어구역(p1_life_zone) 자체**의
    // 크기다(PM 정정 2026-09-23). 우리는 앞치마(상점 줄 26 + 유닛 우리 20)를 레인 섬 안에
    // 넣어 두고 그 **전체**를 3256×2712에 맞췄던 탓에, 정작 필드가 781.4×459.2(비 1.70)로
    // 납작해져 있었다. 필드 쪽을 781.4×650.9(비 1.20)로 놓고 앞치마를 그 아래에 덧붙인다.
    const float LaneFieldSizeX = 781.4f;               // 원작 3256 ÷ Scale
    /// <summary>
    /// 원작 비례 필드 세로. **순찰 사각형을 이 값으로 한 번 계산한 뒤 얼린다**(TrackHalfZ).
    /// 2026-09-24부터 실제 필드 세로는 이것이 아니다 — 아래 <see cref="LaneFieldSizeZ"/> 참고.
    /// </summary>
    const float LaneFieldSizeZOriginal = 650.9f;        // 원작 2712 ÷ Scale — 비 1.20

    /// <summary>
    /// 실제 필드 세로 = 북쪽 inset + 순찰 깊이 + **남쪽 초록 여백**.
    ///
    /// 🔴 2026-09-24 사장님 정정 「내가 줄이라고 한 건 흔함 들어가는 칸을 줄이는 게 아니라
    ///    **레인 하단 초록색 부분의 길이**를 줄이라고 한 거임」 → 남쪽 여백만 79.41 → SouthGreenZ로
    ///    줄인다. 북쪽 변은 고정이고(LaneFieldTopZ) 남쪽 변만 올라간다 — 앞치마가 그 변에
    ///    매달려 있으므로 상점·우리도 같이 올라온다.
    /// </summary>
    const float LaneFieldSizeZ = TrackInsetZ + TrackHalfZ * 2f + SouthGreenZ;
    // 2026-09-23 (6단계): **레인 섬 = 필드**다. 앞치마는 섬 밖 아래로 내려갔다(LaneUnitPenRow·
    // LaneShopStrip 참고) — 원작 1comZone이 필드 밖에 있는 구조를 그대로 따른 것이다.
    const float LaneSizeX = LaneFieldSizeX;
    const float LaneSizeZ = LaneFieldSizeZ;

    const float LaneGapX = 230.4f;
    const float LaneGapZ = 337.9f;
    const float LaneSpacingX = LaneSizeX + LaneGapX;   // 1011.8 — 레인 중심간 x거리
    const float LaneSpacingZ = LaneSizeZ + LaneGapZ;   // 1180.5 — 레인 중심간 z거리

    // 오른쪽 열(레인2·4)의 오른쪽 끝을 -220에 둔다 — Scale=4.167 기준 PunkHazard 오른쪽 끝이
    // x=187.5라 32.5 여유를 두고 확실히 비껴간다(사장님 지시 "오른쪽에 펑크해저드·창고가
    // 있어 레인은 왼쪽으로 펼치는 쪽이 낫다"). 아래쪽 섬들(GachaIsland·StoryZone·CombineTable·
    // 봉인섬)은 2026-09-24에 왼쪽 1200·아래 465를 먹어 전부 z<-402로 내려갔으니 더 멀어졌다.
    // MapGenerator.cs의 Overlaps()로 Warehouses·SealIslands·Zones 전체와 대조해 0건 확인했고,
    // 앞치마까지 넣은 22개 231쌍으로도 0건이다(2026-09-24 재확인, 별도 계산).
    const float LaneRightEdgeX = -220f;
    const float LaneRightColumnX = LaneRightEdgeX - LaneSizeX * 0.5f;   // -610.7
    const float LaneLeftColumnX = LaneRightColumnX - LaneSpacingX;     // -1622.5
    // ⚠️ 앞치마는 필드 **아래로** 199.4만큼 뻗는다(ApronGap 7.7 + 우리 83.3 + 상점 108.3).
    // 겹침 검사(MapGenerator.CheckOverlaps)는 Lanes 배열 = 필드만 보므로 앞치마는 손으로
    // 확인해야 한다. 앞치마 밑면에 치마 1.75를 더한 자리가 **레인 무리의 진짜 남쪽 끝**이고
    // LaneClusterSouthEdgeZ가 그 값이다.
    // 위쪽은 레인1·2 꼭대기까지 아무것도 없다(바다 반폭 3334).
    //
    // 🔴 2026-09-24 초록 줄이기 — **북쪽 변을 고정하고 남쪽 변만 올린다.**
    //    사장님이 줄이라 하신 것은 하단 초록이고, 위쪽은 말씀하지 않으셨다. 그래서 기준을
    //    「아래변 z=300」에서 **「윗변 z=950.9」**로 옮겼다(= 옛 300 + 원작 비례 세로 650.9).
    //    남쪽 변은 필드 세로에서 따라 나온다 — 초록을 줄이면 그만큼 올라오고, 앞치마도 같이 온다.
    const float LaneFieldTopZ = 300f + LaneFieldSizeZOriginal;   // 950.9 — 고정
    const float LaneFieldBottomZ = LaneFieldTopZ - LaneFieldSizeZ;
    const float LaneBottomRowZ = LaneFieldBottomZ + LaneSizeZ * 0.5f;  // 625.45
    const float LaneTopRowZ = LaneBottomRowZ + LaneSpacingZ;           // 1614.25

    public static readonly Island[] Lanes =
    {
        new Island("Lane1", LaneLeftColumnX,  LaneTopRowZ,    LaneSizeX, LaneSizeZ, "lane"),
        new Island("Lane2", LaneRightColumnX, LaneTopRowZ,    LaneSizeX, LaneSizeZ, "lane"),
        new Island("Lane3", LaneLeftColumnX,  LaneBottomRowZ, LaneSizeX, LaneSizeZ, "lane"),
        new Island("Lane4", LaneRightColumnX, LaneBottomRowZ, LaneSizeX, LaneSizeZ, "lane"),
    };

    // 창고 — 플레이어별 개인 섬 (C키로 유닛을 보냄)
    //
    // 🔴 2026-09-25 원작화: 44×Scale(183×183) → **원작 rect 그대로**(war3map warehouse1~4, ÷Scale). 원작은 한 변이
    //    430×307~322로 우리의 2.3배·1.7배였다(PM 원작 대조, map_side_by_side). 네 개의 **상대 배치(간격 77·61)도 원작**이고,
    //    묶음의 왼쪽 위 모서리만 옛 자리(x 72×Scale, z 288×Scale)에 맞춰 평행이동한다 — 다른 섬과의 거리를 새로 만들지 않으려고.
    //    ⚠️ 원작 네 칸은 크기가 조금씩 다르다(1번 437.7×307.2, 2·3번 430.0×322.5, 4번 414.7×322.5). 평균으로 뭉개지 않는다.
    static Island OriginalWarehouse(string name, float minX, float minY, float maxX, float maxY)
    {
        const float originalLeft = 3488f, originalTop = 4768f;          // 원작 네 rect의 묶음 왼쪽 위
        const float left = 72f * Scale, top = 288f * Scale;              // 옛 묶음 왼쪽 위(= 94−22, 266+22)
        float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
        return new Island(name, left + (cx - originalLeft) / Scale, top + (cy - originalTop) / Scale,
                          (maxX - minX) / Scale, (maxY - minY) / Scale, "warehouse");
    }

    public static readonly Island[] Warehouses =
    {
        OriginalWarehouse("Warehouse1", 3488f, 3456f, 5312f, 4736f),
        OriginalWarehouse("Warehouse2", 5632f, 3424f, 7424f, 4768f),
        OriginalWarehouse("Warehouse3", 3584f, 1824f, 5376f, 3168f),
        OriginalWarehouse("Warehouse4", 5632f, 1824f, 7360f, 3168f),
    };

    // 🔴 2026-09-24 사장님 지시 「레인 밑에 스토리존·위습뽑기섬·조합판 왼쪽으로 좀 이동좀하자,
    //    3레인보다 왼쪽으로 가게」 → 왼쪽으로 1200(세계 좌표).
    //
    // 1200은 임의의 값이 아니다. 3번 레인 왼쪽 끝이 x=-2013.2인데, **StoryZone을 그보다 완전히
    // 왼쪽에 놓으려면 -1179.8이 필요**하다 → 1200이 그걸 넘기는 가장 단순한 값이다(여유 20).
    // ⚠️ 그런데 **나머지 둘은 「3레인보다 왼쪽」이 될 수 없다.** 셋을 한 덩어리로 옮기는 한
    //    바다가 허락하는 최대 이동이 -1699.5(StoryZone 왼쪽 끝이 바다 -3333에 50 남기는 값)인데,
    //      뽑기섬   이 필요한 이동 -1954.9  →  255 모자라다
    //      조합판   이 필요한 이동 -3713.2  →  폭이 1700이라 **바다 폭 자체가 모자라다**
    //    즉 지시의 문자 그대로는 **기하적으로 불가능**하고, 달성되는 것은 StoryZone(+봉인섬)이다.
    //    뽑기섬은 3번 레인 **아래**에, 조합판은 그 오른쪽에 남는다 — 이동 후에도 그렇다.
    //    (2026-09-24 측정 후 PM에게 보고. 「셋 다 왼쪽」을 원하시면 이동이 아니라 **재배치**다.)
    //
    // ⚠️ **옮기는 것은 셋이 아니라 일곱이다.** 세 섬만 옮기면 제자리에 남은 봉인섬 넷과 부딪힌다:
    //    SealIsland1·2 ↔ GachaIsland(193·104), SealIsland3·4 ↔ CombineTable(146·346).
    //    겹치는 것을 집합에 넣고 그 집합으로 다시 검사하기를 **더 안 늘 때까지** 돌려 구한 최소
    //    집합이 이 일곱이다(2차에서 안 늘었다). 일곱을 다 옮기면 18개 섬 전수 겹침 0,
    //    바다 왼쪽 끝(-3333)까지 여유는 최소 550(StoryZone). 도착 구역 씬 렌더러 243개는
    //    전부 이 일곱 섬의 내용물이었다 — 부두·펑크해저드·해왕류 같은 외부 구조물은 없다.
    //
    // 세계 좌표 그대로 더한다(리터럴÷Scale로 나누지 않는다) — 「1200 옮긴다」가 코드에 그 숫자로
    // 남아야 다음 사람이 「왜 -287.98인가」를 되짚지 않는다. 섬 **크기**는 안 건드린다.
    //
    // 📌 섬 좌표에서 유도되는 것들은 저절로 따라온다(읽어서 확인, 2026-09-24):
    //    카메라 경계(SetUpCamera → MeasureIslands가 Zones·SealIslands를 훑는다) ·
    //    스토리_등장지점·스토리존 도착지점·복귀포탈(전부 StoryZone.center 기준) ·
    //    조합표 받침·칸벽·글씨 · 위습 생성 5칸(포탈 상대 거리라 상대값 불변).
    //    보물찾기 구역은 **레인 필드 넷** 기준이라 원래 이 이동과 무관하다(따라오지 않는 게 정상).
    //
    // ⚠️ 아래 주석에 적힌 **절대 좌표는 이 이동 전 값이다**(예: "조합판 왼쪽 끝 x=0", "뽑기섬
    //    오른쪽 끝 x=-58.3", "윗변 z=62.5 고정", "넓힌 구간 x 1150~1700"). 스무 개 숫자를 손으로
    //    고치면 그 과정에서 또 어긋나므로 여기 한 번만 적는다: **x는 −1200 뒤 간격 유도로 다시
    //    잡혔고(IslandGapX), z는 −465 내려갔다(DownShift).**
    //    그 주석들이 지키려던 **제약 자체는 살아 있다** — 다만 이제 그것을 지키는 것이 박아 둔
    //    좌표가 아니라 유도식이고, 지켜졌는지는 보고문의 간격 줄이 말한다.
    const float LeftShift = -1200f;

    // 🔴 2026-09-24 사장님 추가 지시 「레인이랑 스토리존·선택위습(=뽑기섬)·조합판 간격도 벌려줘」.
    //
    // ■ x — 섬 사이를 IslandGapX로 벌린다. **왼쪽 끝을 고정하고 오른쪽으로** 벌린다:
    //   StoryZone 왼쪽 끝의 바다 여유가 548뿐이라 왼쪽으로는 갈 데가 없다.
    //   아래 중심 좌표는 **간격에서 유도**한다 — 박아 둔 좌표로 두면 섬 폭이 바뀔 때
    //   간격이 조용히 어긋난다(조합판은 조합식이 열을 하나 더 먹으면 폭이 커진다).
    public const float IslandGapX = 300f;

    // ■ z — 레인 무리와 섬 무리를 LaneToIslandGapZ만큼 띄운다.
    //
    // ⚠️ **「레인 밑변」으로 재면 안 된다.** 레인 섬 밑변은 z=300인데 앞치마(우리 줄 + 상점 줄)가
    //    거기서 199.4 더 남쪽으로 뻗고, 치마가 1.75 더 나온다 → 레인 무리의 **실제 남쪽 끝은 98.9**다.
    //    섬 밑변으로 재면 237.5로 보이지만 실제 틈은 **34.6**이었다 — 7배 차이다.
    //    (씬 실측으로 확인: Lane#_상점바닥 중심 z 154.8 − 깊이 108.3/2 = 100.6 = 앞치마 밑변.)
    //
    // 무리 중 가장 북쪽은 뽑기섬(윗변 z=62.5)이라 그것을 기준으로 내린다. 일곱이 같은 양만큼
    // 내려가므로 서로의 간격은 그대로다. **달성된 간격은 MapGenerator 보고문이 매번 찍는다** —
    // 이 유도식이 섬 정의와 어긋나면 그 줄에서 드러난다(박아 둔 문턱을 믿지 않는다).
    // 🔴 남쪽 무리의 높이를 정하는 기준이 2026-09-24에 **레인에서 펑크해저드로 바뀌었다.**
    //
    // 왜: 무리의 최북단은 전시 섬(초월·불멸)인데 그것은 레인 **아래가 아니라 오른쪽**에 있다
    //     (레인 x −2013~−220 vs 전시 x −111~349 — x가 안 겹친다). 그걸 레인에서 띄우려고
    //     조합판까지 밀면 **안 보이는 것 때문에 보이는 것을 미는** 꼴이 된다.
    //     실제로 전시 섬이 부딪히는 이웃은 **펑크해저드**다(z 416.7~666.7 · x −187.5~187.5).
    //     그래서 「펑크해저드와 얼마」로 잡는다 — 사장님이 보시는 거리가 1169.9 → 753.8이 된다.
    //
    // 이 값이 오늘 지나온 길(전부 「레인과 얼마」로 재던 시절):
    //    ① 34.6  — 아무도 정한 적 없는 값(앞치마를 안 센 채 237로 알고 있었다)
    //    ② 500   — 사장님 「간격도 벌려줘」에 PM이 정함
    //    ③ 200   — 사장님 「너무 멀어졌는데 레인이랑 위에 레인이랑 좀 붙여봐」
    //    ④ 기준 자체를 펑크해저드로 (지금)
    //    ⚠️ ②→③→④는 **결함 수정이 아니다.** 500이 정답이었던 적이 없고 200도 그렇다 —
    //       사장님이 화면을 보고 정하시는 값이다. 다음 사람이 「뭐가 틀렸었나」를 찾지 않게 적는다.
    //       요점은 **상수 하나로 움직인다**는 것이고, 그래서 여기 기대는 수를 어디에도 박지 않는다.
    public const float PunkHazardToDisplayGapZ = 100f;

    /// <summary>펑크해저드 아래변. 옮기지 않는 섬이라 Zones 정의와 같은 수에서 유도한다.</summary>
    const float PunkHazardBottomZ = (130f - 60f * 0.5f) * Scale;   // 416.7

    /// <summary>레인 무리의 실제 남쪽 끝 — 앞치마와 치마까지 포함한다.</summary>
    public const float LaneClusterSouthEdgeZ = LaneFieldBottomZ - LaneApronDepth - CliffMargin;

    // 🔴 2026-09-24 사장님 지시 「초월, 불멸은 조합판 전설 위에 오게끔 해줄래?」 →
    //    두 전시 섬이 조합판 위로 쌓여 **무리의 북쪽 끝이 뽑기섬에서 불멸 전시로 바뀐다.**
    //    (그래서 아래 DownShift가 −465.4 → −1098.9로 커진다. 그 차 633.5가 「추가 하강」이다.)
    //
    // 간격 100인 근거(PM 확정): IslandGapX 300은 **나란히 놓인 섬 사이**에 정한 값이다.
    // 위아래로 얹는 것은 다른 관계이고, 사장님 말씀이 「조합판 전설 **위에** 오게끔」이라
    // **붙어 보여야** 그 말이 맞다. 300이면 떨어진 섬 셋으로 보이고 100이면 얹힌 것으로 읽힌다.
    // 그리고 하강량의 절반이 이 간격 두 겹이다 — 300이면 하강이 1033.5, 100이면 633.5다.
    public const float DisplayStackGap = 100f;

    // 위아래 순서: **불멸이 위, 초월이 아래.** 사다리대로다 —
    // UnitGradeExtensions.Tier()가 초월 9 · 제한됨 10 · **불멸 11**이다(초월이 위가 아니다).
    // 지금 씬 순서와도 같으므로, 뒤집으면 사장님이 안 시킨 변화가 화면에 난다.
    const float DisplaySizeZ = 67.195f * Scale;        // 280.0 — 두 섬 깊이가 같다
    const float TranscendSizeX = 110.391f * Scale;     // 460.0
    const float ImmortalSizeX = 67.195f * Scale;       // 280.0
    const float CombineTopZBeforeShift = -17.04f * Scale;   // −71.0 (「윗변 z=−71」)

    // 치마끼리 DisplayStackGap이 되게 양쪽 치마 몫(CliffOverhang)을 더한다 — x 간격과 같은 자다.
    const float TranscendBottomBeforeShift = CombineTopZBeforeShift + DisplayStackGap + CliffOverhang;
    const float ImmortalBottomBeforeShift = TranscendBottomBeforeShift + DisplaySizeZ
                                          + DisplayStackGap + CliffOverhang;

    // 내리기 전 무리의 북쪽 끝 = **불멸 전시 윗변**(치마 제외한 섬 변).
    // ⚠️ 예전에는 뽑기섬 윗변이었다. 무리에 더 북쪽 섬이 들어오면 **이 줄을 같이 고쳐야 한다** —
    //    안 고치면 아래 DownShift가 엉뚱한 섬을 기준으로 재고, 보고문이 목표값이라고 거짓말한다.
    const float MovedClusterNorthEdgeBeforeShift = ImmortalBottomBeforeShift + DisplaySizeZ;

    // 불멸 전시 윗변이 가야 할 자리 — 펑크해저드 아래변에서 치마 양쪽 몫을 빼고 간격만큼.
    // (다른 간격들과 같은 자다: 치마끼리가 PunkHazardToDisplayGapZ가 된다.)
    const float ImmortalTopTargetZ = PunkHazardBottomZ - CliffOverhang - PunkHazardToDisplayGapZ;
    const float DownShift = ImmortalTopTargetZ - MovedClusterNorthEdgeBeforeShift;

    // 옮기는 셋의 x 중심 — StoryZone은 앞서 정한 자리 그대로, 나머지는 간격에서 유도한다.
    const float StoryZoneSizeX = 180f * Scale;
    const float GachaSizeX = 136f * Scale;
    // 🔴 2026-09-24 사장님 지시 「흔함은 맨 왼쪽에 붙게 해줘 그리고 양 여백 있는거 보기 별로다
    //    여백은 없애줘」 → 섬 가로를 **표 자연 가로에 맞춘다**(1700.0 → 1462).
    //    옛 1700은 표가 6열이던 시절 값이라 10열 1461.3에 238.7이 남았고, 가운데 정렬이라
    //    그것이 **좌우 119.35씩 잔디로** 보였다. 그게 사장님이 보신 여백이다.
    //
    // ⚠️ 세로(CombineSizeZ)와 같은 이유로 **×Scale을 안 붙인다** — 표의 칸 폭은 이미 원작÷Scale이라
    //    배율을 안 탄다. 옛 `407.97 × Scale`은 배율을 바꾸면 섬만 커져 여백이 되돌아올 값이었다.
    //
    // ⚠️ **왜 1461.281이 아니라 1462인가** — 딱 맞추면 안 된다.
    //    BuildCombineColumns는 `totalWidth > island.size.x`면 표를 **축소**한다. 자연 가로는
    //    1461.2810…이고 부동소수 누적에 따라 섬과 같은 값이 미세하게 커질 수 있다. 그러면
    //    축소율 0.99999가 걸려 「가로 100%로 줄여 맞췄습니다」라는 **거짓 보고문**이 뜬다.
    //    0.7(자연 폭의 0.05%, 칸 한 변의 1/22)을 남겨 그 경계를 피한다 — 눈에 보이는 여백이 아니다.
    //    그리고 흔함 열이 **왼쪽 끝에 붙는 것은 이 수가 아니라 왼쪽 정렬이 보장한다**
    //    (BuildCombineColumns의 cursorX) — 이 수가 조금 어긋나도 왼쪽은 안 벌어진다.
    //
    // 🔴 같은 날 두 번 더 움직였다(사장님 지시가 이어졌다):
    //    1462 → 등급 간격 5곳 15.4씩(+77) → 히든 두 열을 한 열로(−166.0) → **1373**
    //    자연 폭 1372.304. ⚠️ PM이 준 1372는 **−0.3으로 축소가 발동한다** — 위에 적은 그
    //    거짓 보고문 함정에 정확히 다시 걸리는 값이다. 0.7을 남겨 1373으로 둔다.
    //    왼쪽 변은 이 수와 무관하다(CombineCenterX가 간격 유도식이라 폭이 바뀌어도 안 움직인다).
    const float CombineSizeX = 1373f;

    // 🔴 2026-09-24 사장님 지시 「제일 긴 열 맞춰서 세로 길이 줄여주고」.
    //
    // ⚠️ **×Scale을 안 붙인다.** 표의 칸·줄 높이(MapGenerator.RecipeSlot 15.4 = 원작 64÷Scale,
    //    RecipeRowHeight 46.2)는 **이미 Scale로 나눈 값**이라 배율을 안 탄다. 섬만 ×Scale로
    //    두면 배율을 바꿀 때 섬은 커지고 표는 그대로여서 조용히 텅 빈다. 세계 좌표로 적는다.
    //    (가로 CombineSizeX는 같은 문제를 안고 있지만 사장님이 세로만 말씀하셔서 안 건드렸다.)
    //
    // 값의 근거 — **가장 깊은 열 + 한 행**:
    //   7열(전설16 + 제한됨9) 25행 · 등급벽 1장 = 25×46.2 + 60.06 = 1215.1
    //   ⚠️ 그런데 코드가 검사하는 깊이는 이것이 아니다. BuildCombineColumns의 커서(rowZ)가
    //      마지막 줄 **아래로 한 행 더** 내려가 있어 deepest = 26×46.2 + 60.06 = **1261.3**이다.
    //      1215로 섬을 잡으면 그 자리에서 「46 모자람」이 뜬다. 두 숫자를 혼동하기 쉬워
    //      보고문에서도 이름을 갈라 찍게 했다.
    //   섬 = 1261.3 + 한 행(46.2) = 1307.5 → 보고문에 여유 46으로 찍힌다.
    //
    // ⚠️ 이 값은 **리터럴일 수밖에 없다** — 섬 크기는 표를 짓기 전에 정해지는데 깊이는 표를
    //    지어야 나온다(순환). 그래서 대신 **넘치면 보고문이 경고하고 필요한 값을 알려 준다.**
    //    사장님이 조합식을 더하시면 그 줄을 보고 이 수를 올린다.
    const float CombineSizeZ = 1307.5f;

    // 조합판 윗변 — 예전 주석들이 말하는 「윗변 z=−71」이 이 값이다(초월 전시 아래변과의 경계).
    // 🔴 **세로를 줄일 때 윗변은 그대로 두고 밑변만 올린다.** 윗변을 내리면 표 머리가 같이
    //    내려가 사장님이 보시는 자리가 바뀐다.
    // ⚠️ 2026-09-24부터 윗변은 **전시 섬 둘을 떠받치는 바닥**이기도 하다 — 초월·불멸이 여기서
    //    DisplayStackGap씩 쌓여 올라가므로, 윗변을 움직이면 전시 섬도 같이 움직이고
    //    무리의 북쪽 끝(=레인↔섬 500을 정하는 자리)까지 따라 바뀐다. DownShift가 유도식이라
    //    500은 저절로 지켜지지만, **남쪽 무리 전체가 그만큼 더 내려간다**(바다 여유를 볼 것).
    const float CombineTopZ = CombineTopZBeforeShift + DownShift;
    const float CombineCenterZ = CombineTopZ - CombineSizeZ * 0.5f;
    // ⚠️ 두 간격(x·z)은 **같은 자**를 써야 한다 — 둘 다 치마 기준이다. 안 그러면 보고문의
    //    「300」과 「500」이 서로 다른 뜻이 되고, 「x를 z만큼 벌려라」가 어긋난다.
    //    그래서 치마 양쪽 몫(CliffOverhang)을 더한다.
    const float StoryZoneCenterX = -290f * Scale + LeftShift;
    const float GachaCenterX = StoryZoneCenterX + StoryZoneSizeX * 0.5f
                             + IslandGapX + CliffOverhang + GachaSizeX * 0.5f;
    const float CombineCenterX = GachaCenterX + GachaSizeX * 0.5f
                               + IslandGapX + CliffOverhang + CombineSizeX * 0.5f;

    /// <summary>조합판 왼쪽 변 — 표가 왼쪽 정렬이라 1열(흔함) 왼쪽 끝과 같은 자리다.</summary>
    public const float CombineTableLeftX = CombineCenterX - CombineSizeX * 0.5f;

    /// <summary>
    /// 전설 열(7·8) 중심이 조합판 **왼쪽 변에서** 얼마나 오른쪽인지. 전시 섬 x가 여기 매달린다.
    ///
    /// ⚠️ **리터럴일 수밖에 없다** — 열 폭은 「그 열에 실제로 담긴 조합식의 최대 재료칸」이 정하고
    ///    그건 에셋을 읽어야 안다. 섬 정의는 그 전에 정해진다(세로·가로와 같은 순환이다).
    ///
    /// 📌 그런데 **세계 좌표로 박지 않고 「왼쪽 변 기준 오프셋」으로 둔 이유**가 있다:
    ///    이렇게 두면 조합판이 LeftShift로 움직이거나 폭이 바뀌어도 전시 섬이 **저절로 따라온다.**
    ///    119.06 같은 절대값으로 박으면 다음 이동에서 「저것만 안 따라왔다」가 또 난다 —
    ///    2026-09-24에 정확히 이 두 전시 섬에서 그 일이 났다(LeftShift에 안 들어가 남겨졌다).
    ///    오프셋이 어긋나면 BuildCombineColumns 보고문이 실측값과 나란히 찍어 고발한다.
    /// </summary>
    public const float LegendColumnCenterOffset = 978.75f;

    /// <summary>전시 섬 x 중심 — 전설 열 중심에 맞춘다(사장님 「전설 위에 오게끔」).</summary>
    public const float DisplayCenterX = CombineTableLeftX + LegendColumnCenterOffset;

    // 물범 섬 — 4개. 물범을 잡으면 전체 플레이어에게 목재 1개씩.
    // 중심 간격을 34→40으로 넓혔다(사장님 지시, 2026-09-03: "너무 따닥 붙어있음") — 빈틈이
    // 8이던 게 14(섬 크기 26의 절반쯤)가 된다. StoryZone 중심(-250)에 맞춰 다시 배치했다 —
    // 예전 배치(중심 -229)를 그대로 넓히면 스토리존 오른쪽 경계(-160, 1.5배 키운 뒤 폭
    // 180 기준)를 넘어간다. 이 배치는 양옆 다 17만큼 여유를 두고 안에 들어간다.
    public static readonly Island[] SealIslands =
    {
        new Island("SealIsland1", -362f * Scale + LeftShift, -180f * Scale + DownShift, 26f * Scale, 26f * Scale, "seal"),
        new Island("SealIsland2", -314f * Scale + LeftShift, -180f * Scale + DownShift, 26f * Scale, 26f * Scale, "seal"),
        new Island("SealIsland3", -266f * Scale + LeftShift, -180f * Scale + DownShift, 26f * Scale, 26f * Scale, "seal"),
        new Island("SealIsland4", -218f * Scale + LeftShift, -180f * Scale + DownShift, 26f * Scale, 26f * Scale, "seal"),
    };

    public static readonly Island[] Zones =
    {
        // 이벤트 존이 위, 그 아래 불멸·초월 전시가 가로로 나란히.
        new Island("PunkHazard",         0f * Scale, 130f * Scale, 90f * Scale, 60f * Scale, "event"),
        // (2026-09-23, 원작 비율 4단계, PM 지시) 옛 90×26/90×50은 SlotSpacing이 6이던 시절
        // 기준이다 — SlotSpacing이 61.4로 커지면서 초월 25종이 한 줄에 6.1칸(가로 430)밖에
        // 못 들어가 7종×4줄이 필요해졌고(세로 246+여유), 불멸 8종 원형도 반지름 최소 78.2
        // (지름 230+여유)가 필요해졌다. PM이 준 목표(최종 크기, Scale 적용 후) 초월 460×280·
        // 불멸 280×280을 이 리터럴(=목표÷Scale)로 옮겼다. z중심도 같이 옮겼다 — 커진 크기가
        // 옛 z(52·8)를 그대로 쓰면 CombineTable과 겹친다. CombineTable 윗변(z=-70.8, Scale
        // 후)에서 40 띄우고 쌓아 올렸고, MapGenerator.cs:3508 Overlaps()와 같은 식으로 다른
        // 16개 섬 전부와 대조해 0건 확인했다(레인 때와 같은 방식, 별도 계산).
        // 🔴 2026-09-24 사장님 「초월, 불멸은 조합판 전설 위에 오게끔 해줄래?」 →
        //    조합판 위로 옮겼다. 옛 자리(x 625.1 · z 429.2/109.2)는 **LeftShift에 안 들어가
        //    남겨진 자리**였다 — 남쪽 무리가 왼쪽·아래로 가는데 이 둘만 제자리에 있었다.
        //    이제 x는 DisplayCenterX(조합판 왼쪽 변 + 오프셋), z는 DownShift를 타므로
        //    **다음 이동에도 저절로 따라온다.**
        //
        //    불멸이 **위**, 초월이 **아래**다 — Tier()가 초월 9 · 불멸 11이라 사다리대로이고,
        //    지금 씬 순서와도 같다. 뒤집으면 사장님이 안 시킨 변화가 화면에 난다.
        //    ⚠️ 초월 폭 460은 전설 열 폭 362.8보다 **97.2 넓다.** 줄일 수 없으니(초월 25종이
        //       들어가야 한다) 「전설 위」는 **중심을 맞추는 것**으로 잡았다.
        //    ⚠️ **전시 모양은 그대로다** — 초월은 가로줄, 불멸은 화로 원형. 사장님이 「초월은
        //       제단 느낌」이라 하셨지만 그건 모양 판단이라 자리만 먼저 닫는다(PM 지시).
        new Island("ImmortalDisplay",  DisplayCenterX,
                   ImmortalBottomBeforeShift + DisplaySizeZ * 0.5f + DownShift,
                   ImmortalSizeX, DisplaySizeZ, "display"),
        new Island("TranscendDisplay", DisplayCenterX,
                   TranscendBottomBeforeShift + DisplaySizeZ * 0.5f + DownShift,
                   TranscendSizeX, DisplaySizeZ, "display"),
        // 1.5배로 키운 값(원래 120x100). 여유가 빠듯하다 — 봉인섬과 z로 12,
        // 뽑기섬과 x로 10밖에 안 남으니 더 키우려면 이웃을 먼저 옮겨야 한다.
        new Island("StoryZone",       StoryZoneCenterX, -80f * Scale + DownShift, StoryZoneSizeX, 150f * Scale, "story"),
        // 오른쪽 전시 칸이 다른세계 조합식 한 줄(재료 6칸 + 비용 3칸)을 담아야 해서 폭을 넓혔다.
        // (2026-09-23, 원작 비율 4단계) SlotSpacing 6→61.4로 오른쪽 전시 칸이 줄당 11칸에서
        // 4칸으로 줄어, 랜덤유닛 14종이 2줄에서 4줄이 됐다.
        // ⚠️ **이 「4줄」은 낡았다.** 같은 날 전시 격자를 SlotSpacing에서 떼어내 제 상수
        //    (MapGenerator.DisplaySlotSpacing)를 줬고, 그때부터 **줄당 6칸·3줄**이다.
        //    지금 값은 늘 코드에 있다 — 이 주석은 왜 떼어냈는지만 말한다.
        // 그 아래 다른세계 조합식 14줄까지
        // 더하면 깊이 827이 필요한데 예전 섬(깊이 752)으로는 75가 모자란다(생성 보고문의
        // "⚠️ 모자람"이 뜨는 자리다). **위쪽은 안 건드리고 아래로만 160 늘렸다** — 위로 늘리면
        // 바로 위 레인3·4(아래변 z=100)와 겹친다. 그래서 size_z는 +160/Scale, center_z는
        // −80/Scale만큼 내렸다(윗변 z=62.5 고정). 새 여유는 85다.
        // ⚠️ 2026-09-24부터 **윗변 z=62.5는 더 이상 고정이 아니다** — DownShift가 −465를 얹어
        //    실제 윗변은 −402.9다. 위로 늘려도 레인과 500 떨어져 있으니 그 제약은 풀렸지만,
        //    늘릴 땐 LaneToIslandGapZ가 줄어드는 것이므로 보고문의 간격 줄을 같이 봐야 한다.
        new Island("GachaIsland",      GachaCenterX, -99.20f * Scale + DownShift, GachaSizeX, 228.40f * Scale, "gacha"),
        // 조합식 표는 전시 섬과 겹치지 않도록 폭을 줄이고 왼쪽으로 당겼다.
        // (2026-09-23, 원작 비율 4단계, PM 지시 "섬을 넓혀라 — 52% 축소는 받지 않는다")
        // 새 칸 크기로 열을 자연 폭대로 늘어놓으면 2192가 필요한데 옛 폭은 1142라, 그대로 두면
        // RecipeScale 자동 축소가 52%로 걸려 칸이 15.4가 아니라 8.0으로 그려진다(= 사장님이
        // 지적하신 "조합판이 작다"가 절반만 해소된다). 폭 2250으로 넓혀 축소를 없앴다.
        // 세로도 775→917로 키웠다 — 옛 깊이는 여유가 37뿐이었다(필요 738).
        // ⚠️ 둘 다 **오른쪽·아래로만** 늘렸다: 왼쪽 끝은 x=0에 그대로 둬야 뽑기섬(오른쪽 끝
        //    x=-58.3)과 안 붙고, 윗변은 z=-71에 그대로 둬야 초월 전시(아래변 z=-30.8)와 안 겹친다.
        //    그래서 중심도 같이 옮겼다(폭 540·세로 220 리터럴 기준). 18개 섬 전수 대조 겹침 0건.
        // (2026-09-23 2차) 사장님 "너비가 너무 길어지는 느낌" → 열을 세로로 먼저 채우게 바꾸고
        // (BuildCombineColumns) 비용·간격 상수의 비율도 바로잡자 자연 가로가 2192 → **822**로
        // 줄었다. 섬도 880(여유 58)으로 줄인다 — 내용보다 섬이 크게 남으면 그것대로 허전하다.
        // 세로 917은 유지(깊이 909, 여유 8). 왼쪽 끝 x=0은 그대로 두고 오른쪽만 당겼다.
        // (2026-09-23 3차) 사장님 「조합판도 너무 붙어있으니깐 답답한 느낌」 → 재료 간격을
        // 칸 한 변과 같게(피치÷칸 1.31 → 2.00), 줄 높이는 칸의 3배로 벌렸다.
        // 열 수는 **6개 그대로**다 — 앞서 「너비가 너무 길어지는 느낌」이라 하셨으므로 가로는
        // 안 늘리고(자연 가로 1119) 여백을 세로로 풀었다(자연 세로 1552).
        // 섬은 1150×1625. 세로 여유가 27뿐인 건 **세로 채우기 구조상 정상**이다 —
        // 각 열을 줄이 더 안 들어갈 때까지 채우므로 남는 건 언제나 줄 높이(46.2)보다 작다.
        // 왼쪽 끝 x=0·윗변 z=-71 고정은 그대로(뽑기섬·초월 전시와의 경계). 겹침 전수 0건.
        // 🔴 2026-09-24 사장님 지시로 표가 6열 → 8열 → **9열**이 되면서 가로를 1150 → 1700으로 넓혔다.
        //    폭은 열마다 「그 열에 실제로 담긴 조합식의 최대 재료칸」이 정한다 — 등급의 최대가
        //    아니다. 같은 등급을 두 열로 가르면 5칸짜리가 한쪽에만 가서 **다른 열은 좁아진다**
        //    (특별함 17/16으로 가르니 2열은 3칸 135.2, 3열은 4칸 166.0). 그래서 자연 폭은
        //    등급별 최대로 더한 값보다 작고, **실측해야 나온다**(9열 실측 1617.0).
        //    ⚠️ **오른쪽으로만** 넓혔다(중심 137.99 → 203.98). 왼쪽·양쪽으로 넓히면 뽑기섬과
        //       각각 312·127 겹친다. 넓힌 구간(x 1150~1700)은 씬 렌더러 전수로 **바다임을 확인**했다.
        //    표 안의 것(받침 747개·칸벽·글씨)은 전부 섬 좌표에서 유도되므로 **저절로 따라온다.**
        //    여유를 83(4.9%) 남겼다 — 사장님이 「간격·크기는 앞으로 수정한다」고 하셨고,
        //    조합식 하나가 재료를 하나 더 받으면 그 열이 30.8 넓어지기 때문이다.
        // 🔴 2026-09-24 그 여유가 **없어졌다.** 흔함 열이 붙어 10열이 되며 자연 폭이 1461.3이
        //    됐는데 섬은 1700이라 좌우 119.35씩 잔디가 보였고, 사장님이 「여백은 없애줘」라
        //    하셨다 → 섬 가로 = 표 자연 폭(CombineSizeX 1462). **위 「여유 83」은 이제 0.7이다.**
        //    조합식이 늘면 이번엔 여유가 없으니 바로 축소가 걸린다 — 보고문의 「가로 N/M」을
        //    보고 이 수를 올려야 한다(세로와 같은 순환이라 자동으로 못 한다).
        new Island("CombineTable",     CombineCenterX, CombineCenterZ, CombineSizeX, CombineSizeZ, "combine"),
        // 도박소. StoryZone 서쪽, 같은 z대역이라 나란히 배치되고 40유닛 간격으로 안 겹친다.
    };

    // 조합식 표에 노출하는 등급 6종 — 표 위의 세로 칸 하나씩.
    // 히든·영원·초월·불멸·다른세계는 의도적으로 표시하지 않는다(외부 조합표를 보고 조합).
    // 히든은 원래 "맵에 표시하지 않는 등급"이었으나 피드백을 받아 표에 넣기로 했다(2026-09-01).
    //
    // ⚠️ **흔함은 여기 넣지 않는다 — 그런데 표에는 있다.** 조합으로 만들어지지 않는 기초 등급이라
    //    조합식이 0개고, 이 배열은 「조합식을 실어 올 등급」 목록이기 때문이다. 흔함 9종은
    //    `BuildCombineColumns`가 0열에 **결과 칸만** 따로 세운다(사장님 09-24 「안흔함 왼쪽에
    //    비는 거 같은데 여기에 흔함 배치하자」). 여기에 넣으면 「열 배정이 표에 없습니다」
    //    경고만 뜨고 아무것도 안 실린다.
    public static readonly UnitGrade[] CombineTableGrades =
    {
        UnitGrade.Uncommon, UnitGrade.Special,
        UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.Limited,
        UnitGrade.Hidden,
        // 2026-09-23 사장님 지시 — 불멸·영원도 표에 올린다. 스킨이 다 들어와 인형으로 서기 때문에
        // "조합식 없이 전시만" 하던 이유가 사라졌다. 초월은 가로줄 전시 섬이 따로 있어 그대로 둔다.
        //
        // 🔴 2026-09-24에 그 지시가 **두 번에 걸쳐 전부 거둬졌다.**
        //    ㉠ 불멸 — 「원래 불멸은 조합식이 안 나와 있었어. 초월·불멸은 그냥 세워두기만 할거임」
        //    ㉡ 영원 — 「조합판 안흔함 밑에 영원함 조합식 빼줘」 (이 줄이 있던 자리다)
        //    ⚠️ **전시는 그대로다** — 불멸 8종은 화로 원형에, 초월 25종은 가로줄 전시 섬에 선다.
        //       표에서 빠지는 것이지 맵에서 사라지는 게 아니다.
        //    ⚠️ 그런데 **영원은 전시가 없다** — 불멸(화로 원형)·초월(가로줄 섬)과 달리 표에서
        //       빠지면 영원 9식을 **볼 데가 아예 없어진다.** PM이 사장님께 여쭙는 중이고,
        //       지시는 「빼라」였으니 일단 뺀다. 되돌리려면 이 줄과 spread의 Eternal 항을 같이 살린다.
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

    /// <summary>
    /// 흔함 유닛의 **최소** 사거리. 2026-09-24 로스터 실값 — 9종 중 4종(강재규·강주혁·노태현·문필환)이
    /// 이 값이고, 나머지는 109.05·133.24·145.52·145.52·320.23이다.
    /// ⚠️ **리터럴이다** — MapLayout은 에셋을 못 읽는다. MapGenerator가 로스터를 읽어 실값과
    ///    견주고 어긋나면 고발한다(그 줄이 이 수의 근거다).
    /// </summary>
    public const float CommonMinAttackRange = 101.75f;

    /// <summary>
    /// 우리 자리 ↔ 순찰 경로 거리를 최소 사거리의 몇 배로 둘지.
    /// ⚠️ **1.0으로 두면 안 된다.** 사거리 판정은 중심거리 비교(UnitAttacker.FindClosestEnemyInRange)라
    ///    거리 = 사거리면 **접점 한 순간만** 사거리 안이고 가동률이 0에 가깝다. 경로를 따라 잡히는
    ///    구간은 2√(사거리² − 거리²)이므로, 0.85면 최악 종도 107.2(둘레 2156의 5.0%)을 얻는다.
    ///
    /// 🔴 2026-09-24에 0.95 → **0.85**로 내렸다. 거리를 버는 일이 우리 깊이에서 초록 땅으로
    ///    옮겨 가면서(<see cref="SouthGreenZ"/>) 더 내릴 여지가 생겼기 때문이다.
    ///    0.85를 고른 근거 셋:
    ///      ① 사장님이 보시는 것은 「하단 초록이 길다」다. 0.95면 초록이 79.4 → 47.3(−40%)인데
    ///         0.85면 **37.1(−53%)로 절반**이라 눈에 띈다.
    ///      ② 거리가 96.7 → 86.5로 **줄어드니 가동률이 나빠질 수 없다**(지금 R1 61%가 기준선).
    ///      ③ 아래 한계에 닿지 않는다 — 흙길 반폭 9가 초록 37.1 안에 들어가므로
    ///         **경로가 섬 밖으로 안 나간다**(28.1 여유). 이 검사는 보고문이 매번 찍는다.
    /// </summary>
    ///
    /// 🔴 2026-09-25 원작 대조로 **0.85 → 0.60**(거리 86.5 → 61.05). PM이 원작 보행맵(wpm)과 좌표로 잰 값:
    ///    원작 흔함 선 자리 → 순찰 경로가 평균 약 61(54~69), 최소 사거리 대비 여유 약 40.
    ///    우리는 86.5라 여유가 15뿐이었다. 0.60 × 101.75 = 61.05 → 여유 40.7로 원작과 같다.
    ///    거리가 짧아지니 최악 종의 사거리 안 구간이 107 → 163으로 늘고 가동률은 나빠질 수 없다.
    ///    ⚠️ 이 거리는 이제 **우리 한가운데가 아니라 유닛이 실제로 서는 점**(<see cref="CommonStandOffset"/>)에서 잰다.
    /// </summary>
    public const float PenToTrackRatio = 0.60f;

    /// <summary>우리 유닛 자리에서 순찰 경로 남쪽 변까지의 목표 거리.</summary>
    public const float PenToTrackDistance = CommonMinAttackRange * PenToTrackRatio;   // 61.05

    /// <summary>
    /// 흔함 유닛이 **서는 점**이 칸(우리 줄) 한가운데에서 위로(필드 쪽으로) 떨어진 거리. **지금 0 — 칸 한가운데.**
    ///
    /// 🔴 2026-09-26: 원작 기본은 칸 위 225(아래 09-25 설명), **사장님 09-26 지시로 칸 안 + 경로 당김**.
    ///    사장님 「흔함만 저기 안에 들어가게」 → 「칸 안 + 적 길 당기기」. 원작에도 칸 안 상태가 있다 — 「흔함자동꺼내기」를 끄거나
    ///    유닛 버튼 A07Y로 넣으면 Common_Loc(칸 한가운데)에 선다(Trig_Random_Base1 · Trig_house, war3map_new.j).
    ///    원작은 칸 안에서 경로까지 108~121이라 거의 못 닿지만, 우리는 **경로를 칸 쪽으로 당겨** 거리를 칸 위 225 때와 같은
    ///    <see cref="PenToTrackDistance"/>로 맞춘다 — <see cref="SouthGreenZ"/>가 유도식이라 이 값 하나로 따라온다.
    ///
    /// ── 아래는 09-25 기록(원작 기본 = 칸 위 225, 원작 225 ÷ Scale = 54.0) ──
    ///
    /// 원작은 칸이 레인 벽 뒤에 파여 있고, 뽑힌 유닛은 칸 위 225 지점 — **레인 아래 끝** — 으로 꺼내져 선다
    /// (PM 원작 대조 2026-09-25). 우리는 그동안 칸 한가운데에 세워서, 경로까지 거리를 맞추려면 초록 여백을
    /// 37로 깎아야 했다(원작 54~69). 선 자리를 원작처럼 올리면 **거리와 여백이 동시에 원작 값**이 된다.
    /// ⚠️ 칸 깊이(<see cref="UnitPenDepth"/>)는 원작 상수 그대로다 — 칸은 안 바뀌고 서는 점만 올라간다.
    /// </summary>
    public const float CommonStandOffset = 0f;

    /// <summary>유닛이 서는 점이 필드 아래 끝보다 얼마나 위인가(음수면 필드 밖 = 앞치마 위). 지금 −49.35(칸 한가운데).</summary>
    public const float StandAboveFieldBottom = CommonStandOffset - ApronGap - UnitPenDepth * 0.5f;

    /// <summary>
    /// 상점 줄 바로 위, 새 유닛이 처음 서는 우리가 놓이는 줄.
    ///
    /// 유닛은 이 줄 **한가운데**에 선다(LaneMarker.SlotPosition의 첫 줄 = unitPen.position).
    /// 그래서 적까지의 거리는 `SouthGreenZ + ApronGap + 이 값의 절반`이다.
    ///
    /// 🔴 2026-09-24에 그 거리가 **128.76**이었고, 흔함 9종 중 **5종이 사거리 밖**이라
    ///    가동률이 **0%/0%**(201 유닛·초)로 나왔다. 그때 이 값을 19.15로 줄여 고쳤고
    ///    가동률이 61~97%가 됐다 — 고침은 통했지만 **자리가 틀렸다**(아래 정정 참고).
    /// ⚠️ 그 시절 거리 128.76은 **남쪽 초록이 79.41이던 때**의 값이다. 그때는 초록이
    ///    「깎을 수 없는 몫」이라 앞치마로 살 수 있는 거리가 41.7뿐이었는데,
    ///    경로를 얼리면서 초록 자체가 깎을 수 있는 값이 됐다.
    /// ⚠️ 유닛 몸은 맵 배율을 타지 않는다(「인형은 전부 레인 유닛과 같은 키」, 발자국 지름 ≈ 12).
    ///
    /// 🔴 **같은 날 사장님이 정정하셨다** — 「내가 줄이라고 한 건 흔함 들어가는 칸을 줄이는 게
    ///    아니라 레인 하단 초록색 부분의 길이를 줄이라고 한 거임」. 우리가 선처럼 얇아져(19.15)
    ///    유닛 넷이 좁은 띠에 붙어 서 있던 화면을 보신 것이다. **원작 20 × Scale로 되돌린다.**
    ///    거리를 버는 일은 이제 <see cref="SouthGreenZ"/>가 한다 — 같은 목표 거리를
    ///    **버려지는 초록 땅**에서 빼는 것이라, 우리를 좁히지 않고 같은 값을 얻는다.
    ///    ⚠️ 그래서 이 값은 다시 **원작 상수**이고, 유도되는 쪽은 초록이다. 두 개를 동시에
    ///       유도하면 순환이 된다(둘 다 목표 거리에 매달려 있다).
    /// </summary>
    public const float UnitPenDepth = 20f * Scale;

    /// <summary>
    /// 흔함 줄이 필드 바닥에서 떨어져 있는 간격. 원작 <c>1comZone</c>이 필드(p1_life_zone)
    /// **밖**, 바닥에서 32 아래에 있다(war3map_new.j:3234~3242) → 32 ÷ Scale.
    /// </summary>
    public const float ApronGap = 32f / Scale;              // 7.68

    /// <summary>흔함 줄 폭 ÷ 레인 폭. 원작 2368/3200 = 0.74.</summary>
    public const float UnitRowWidthRatio = 0.74f;

    /// <summary>필드 아래로 내려 붙는 앞치마 전체 깊이(간격 + 우리 줄 + 상점 줄).</summary>
    public const float LaneApronDepth = ApronGap + UnitPenDepth + ShopStripDepth;

    /// <summary>
    /// 적이 도는 필드. **이제 레인 섬이 곧 필드다**(2026-09-23, 6단계).
    ///
    /// 예전에는 레인 섬 안에 앞치마(우리 줄 + 상점 줄)를 넣고 그걸 뺀 나머지를 필드로 삼았다.
    /// 그 구조 때문에 섬 전체를 원작 3256×2712에 맞추면 정작 필드가 781.4×459.2(비 1.70)로
    /// 납작해졌다. 원작 <c>p1_life_zone</c>은 그 자체가 필드이고 <c>1comZone</c>은 필드 **밖**
    /// 아래에 따로 있다 — 그래서 섬 = 필드로 두고 앞치마를 밖으로 내렸다.
    /// 호출부를 바꾸지 않으려고 함수는 남겨 둔다(지금은 항등).
    /// </summary>
    public static Island LaneField(Island lane) => lane;

    /// <summary>
    /// 앞치마 **지반** — 우리 줄·상점 줄이 올라앉는 땅. 레인 섬(=필드)이 더 이상 이들을
    /// 품지 않으므로(6단계) 따로 깔아야 한다.
    ///
    /// ⚠️ 윗변을 필드 아래변에 **딱 붙인다.** ApronGap(원작 32÷Scale)은 "우리 줄이 필드
    /// 가장자리에서 떨어진 거리"지 물길이 아니다 — 여기를 비우면 그 사이가 바다가 되고,
    /// 지상 유닛은 바다 영역을 못 지나므로(UnitSpawner의 areaMask) 우리에서 나온 유닛이
    /// 필드로 걸어 들어갈 수 없게 된다. 땅은 이어 두고 줄만 ApronGap만큼 안쪽에 세운다.
    /// </summary>
    public static Island LaneApron(Island lane)
    {
        float fieldBottom = lane.center.y - lane.size.y * 0.5f;
        return new Island(lane.name + "_앞치마",
            lane.center.x, fieldBottom - LaneApronDepth * 0.5f,
            lane.size.x * UnitRowWidthRatio, LaneApronDepth, lane.tint);
    }

    /// <summary>
    /// 유닛 우리가 놓이는 줄 — **필드 밖 아래**. 폭은 레인의 74%(원작 2368/3200),
    /// 필드 바닥에서 ApronGap만큼 띄운다.
    /// </summary>
    public static Island LaneUnitPenRow(Island lane)
    {
        float fieldBottom = lane.center.y - lane.size.y * 0.5f;
        return new Island(lane.name,
            lane.center.x, fieldBottom - ApronGap - UnitPenDepth * 0.5f,
            lane.size.x * UnitRowWidthRatio, UnitPenDepth, lane.tint);
    }

    /// <summary>
    /// 상점이 서는 줄 — 우리 줄 바로 아래. ⚠️ 원작 `.j`에는 상점 좌표가 없다(선배치로 보인다).
    /// 근거가 없으므로 배치 방식은 우리 것을 유지하고, 폭만 우리 줄과 맞춘다(PM 지시 2026-09-23).
    /// </summary>
    public static Island LaneShopStrip(Island lane)
    {
        Island pen = LaneUnitPenRow(lane);
        float penBottom = pen.center.y - pen.size.y * 0.5f;
        return new Island(lane.name,
            lane.center.x, penBottom - ShopStripDepth * 0.5f,
            pen.size.x, ShopStripDepth, lane.tint);
    }

    /// <summary>
    /// 조합소 자리 — 원작은 <c>johab1</c>이 **필드 한복판**(1536,672 = 가로 48%·세로 24%)에 있다.
    /// 우리처럼 상점 줄에 일렬로 두지 않는다(war3map_new.j:3234~3242).
    /// </summary>
    public static Vector3 LaneCombineSpot(Island lane)
    {
        Island field = LaneField(lane);
        return new Vector3(
            field.center.x - field.size.x * 0.5f + field.size.x * 0.48f,
            IslandTop,
            field.center.y - field.size.y * 0.5f + field.size.y * 0.24f);
    }

    /// <summary>
    /// 순찰 경로(=흙길)가 필드 가장자리에서 안으로 들어오는 비율. **원작 실측값**이다 —
    /// 원문 <c>war3map_new.j:3234~3242</c>의 필드 3200×2752와 <c>:6007~6014</c>의 순찰
    /// 4지점 (464,2304)(480,256)(2864,320)(2848,2336)에서 뽑았다:
    /// 가로 좌464·우336(평균 400 = 12.5%), 세로 하256·상416(평균 336 = 12.2%).
    ///
    /// ⚠️ 고정 거리(옛 14)가 아니라 **비율**이어야 한다. 고정값으로 두면 필드가 커질 때
    /// 순찰 사각형이 상대적으로 바깥으로 밀려 둘레가 길어지고, 랩 시간이 원작과 어긋난다
    /// (실제로 그랬다: 옛 14×Scale은 7.5%라 둘레 2398 → 랩 33.3초, 원작은 29.87초).
    /// 이 비율로 두면 둘레 2156 → **랩 29.94초로 원작과 맞는다(적 속도 72 그대로)**.
    ///
    /// 원작은 좌우·상하가 서로 다르지만(비대칭) 우리 경로는 사각형 하나라 축별 평균을 쓴다.
    /// </summary>
    public const float TrackInsetRatioX = 0.125f;
    public const float TrackInsetRatioZ = 0.122f;

    /// <summary>
    /// 순찰 사각형의 z쪽 치수. **비율로 매번 계산하지 않고 얼린 값이다**(PM 결정 2026-09-24).
    ///
    /// 🔴 왜 얼리나: 위 비율이 하던 일은 「원작 랩 시간을 재현하는 것」이고, 그 비율이 뜻을
    ///    가지려면 **우리 필드가 원작 비례여야** 한다. 남쪽 초록을 잘라내는 순간 우리 필드는
    ///    더 이상 원작 비례가 아니다 → 그때부터 비율을 다시 곱하면 **아무것도 재현하지 않고**
    ///    랩만 어긋난다(세로가 650.9 → 611.5면 둘레 2156 → 2077, 랩 29.94 → 28.8초).
    ///    그래서 원작 비례 세로(<see cref="LaneFieldSizeZOriginal"/>)로 한 번 계산해 **얼린다.**
    ///
    /// ⚠️ **비율을 다시 곱하지 말 것.** <see cref="TrackInsetRatioZ"/>는 지우지 않고 **이 값의
    ///    유도 근거로** 남겨 둔다 — 원작 순찰 4지점 실측이라, 지우면 다음 사람이 「왜 79.41인가」
    ///    에서 막힌다. x쪽은 필드 가로를 안 건드리므로 여전히 비율로 계산한다(그쪽은 뜻이 살아 있다).
    /// </summary>
    public const float TrackInsetZ = LaneFieldSizeZOriginal * TrackInsetRatioZ;              // 79.41

    /// <summary>순찰 사각형의 반깊이 — 얼린 값. 둘레(2156.3)와 랩 29.94초가 이 값에 달려 있다.</summary>
    public const float TrackHalfZ = LaneFieldSizeZOriginal * 0.5f - TrackInsetZ;             // 246.04

    /// <summary>
    /// 순찰 경로 남쪽 변 아래로 남기는 초록 여백. 사장님 「레인 하단 초록색 부분의 길이를 줄여라」.
    ///
    /// 옛 값은 79.41(= 얼리기 전 남쪽 inset)이었고, 그게 그냥 버려지는 땅이었다.
    /// **절대값으로 박지 않고 사거리에서 유도한다** — 우리에 선 유닛이 적에게 닿는 거리가
    /// 이 값에 그대로 실린다:
    /// <code>
    ///   우리 자리 → 순찰 경로 = SouthGreenZ + ApronGap + UnitPenDepth/2
    /// </code>
    /// 그래서 목표 거리(<see cref="PenToTrackDistance"/>)에서 역으로 뺀다. 사거리가 바뀌거나
    /// 우리 깊이를 되돌려도 거리가 유지된다 — 실제로 2026-09-24에 우리 깊이를 19.15로 줄였다가
    /// 사장님 정정으로 83.34로 되돌렸는데, 이 식이면 그 되돌림이 초록 쪽으로 옮겨 간다.
    /// </summary>
    ///
    /// 🔴 2026-09-26 **부호 정정**(구현담당1). 09-25 식 `서는 점 → 경로 = SouthGreenZ + StandAboveFieldBottom`은 거꾸로였다 —
    ///    서는 점이 필드 아래 끝보다 **위**(+)면 경로까지는 그만큼 **가깝다**. 그래서 09-25 칸 위 225 배치의 실제 거리는
    ///    61.05가 아니라 56.4 − 4.65 = **51.75**였다(보고문 「거리」도 같은 식으로 계산한 값이라 못 잡았다). 바른 식:
    /// <code>
    ///   서는 점 → 순찰 경로 = SouthGreenZ − StandAboveFieldBottom
    ///   칸 한가운데(StandAboveFieldBottom −49.35) → 초록 = 61.05 − 49.35 = 11.7 · 흙길 반폭 9를 빼도 필드 안 2.7 + 앞치마 7.68</code>
    /// </summary>
    public const float SouthGreenZ = PenToTrackDistance + StandAboveFieldBottom;

    /// <summary>
    /// 순찰 사각형(= 흙길 중심선). **경로와 흙길이 이 하나를 본다.**
    /// ⚠️ 예전에는 MapLayout.LaneLoop과 MapGenerator.DecorateLane이 같은 식을 **따로** 갖고 있어서,
    ///    한쪽만 고쳤을 때 58.3 대 14로 44만큼 어긋났다(적이 흙길 한참 안쪽을 걸었다).
    ///    같은 일이 또 나지 않게 사각형 자체를 여기서 한 번만 만든다.
    ///
    /// z는 **필드 북쪽 변에 매달린다** — 남쪽 초록을 자르면서도 사각형 크기가 안 변해야 하므로,
    /// 남쪽 변이 아니라 북쪽 변을 기준으로 잡는다.
    /// </summary>
    public static Rect LaneTrackRect(Island lane)
    {
        Island field = LaneField(lane);
        float halfX = field.size.x * 0.5f - field.size.x * TrackInsetRatioX;
        float topZ = field.center.y + field.size.y * 0.5f - TrackInsetZ;
        return Rect.MinMaxRect(field.center.x - halfX, topZ - TrackHalfZ * 2f,
                               field.center.x + halfX, topZ);
    }

    // 🔴 `LaneTrackInset`은 **지웠다**(2026-09-24). 남겨 두는 것 자체가 결함이었다.
    //
    //    경로를 얼린 그 커밋 안에서, 유일하게 남은 호출부(PenReachReport)가 **inset을 받아
    //    둘레를 다시 계산**하고 있었다 — 그래서 같은 보고문 네 줄 안에 둘레가 2156.3과
    //    2071.7로 **두 값**이 찍혔다. 주석에 「비율을 다시 곱하지 말 것」이라고 적은 커밋에서다.
    //
    //    ⚠️ **주석은 사람을 막지만 이미 있는 호출은 못 막는다.** 그래서 부를 수 없게 지웠다 —
    //       inset이 필요하면 <see cref="LaneTrackRect"/>와 섬 변의 차로 내면 되고, 그러면
    //       얼린 사각형과 어긋날 수가 없다. **하나뿐인 진실을 두 곳에서 만들 수 있게 두지 않는다.**

    /// <summary>
    /// ⚠️ 2026-09-26 사장님 「ㄱ·ㄴ 섬 같은 거 레인에 걸리적거린다, 그냥 지워」 — **벽 넷은 지웠다**(LaneCornerWalls 함수째).
    ///    아래 상수들은 스토리 포탈 자리(<see cref="LaneStoryPortalSpot"/>, 원작 Go_story)가 아직 쓴다 — 포탈은 원작 자리 그대로다.
    ///
    /// (09-25 기록) 레인 안 **ㄱ자 벽 넷**(원작 레인 안쪽 모서리). 2026-09-25 원작화 ③ — PM 원작 대조(war3map.wpm 보행맵, 칸 32).
    ///
    /// 원작 p1_life_zone(3200×2752) 안의 막힌 칸을 그대로 읽은 모양이다:
    /// <code>
    ///   ┌ 긴 막대 640×256 + 다리 256×256 (전체 640 × 512)   네 모서리에 하나씩, 꺾인 곳이 레인 모서리(바깥)를 향한다
    ///   바깥 꺾인 점 ↔ 순찰 사각형 모서리:  가로 232(좌우 똑같다) · 세로 위 176 / 아래 192 → 184로 대칭</code>
    /// 순찰 사각형(<see cref="LaneTrackRect"/>)에 매단다 — 경로와 벽 사이 거리가 원작과 같아야 「경로를 가르고 설 자리를 나누는」
    /// 역할이 같다. 값은 전부 원작 ÷ Scale이다. 우리 순찰 사각형(586×492)에서 벽 사이 틈은 가로 167 · 세로 158(원작 153.6).
    /// ⚠️ 두께 61이라 NavMesh 굽기 반지름 상한 문제(칸막이 1.4, navmesh-bake-radius-ceiling)와는 무관하다.
    /// </summary>
    public const float CornerWallOffsetX = 232f / Scale;     // 55.7
    public const float CornerWallOffsetZ = 184f / Scale;     // 44.2
    public const float CornerWallLength = 640f / Scale;      // 153.6
    public const float CornerWallThickness = 256f / Scale;   // 61.4

    /// <summary>
    /// 스토리 입장 포탈 자리 — 오른쪽 위 ┐ 벽의 **오목한 안쪽 한가운데**(원작 Go_story, 2026-09-25 사장님 (가)).
    /// 원작 Go_story 중심은 순찰 모서리에서 (132.5, 126.7) 안쪽이고, 오목한 자리(92×61.5)의 한가운데는 (163.2, 136.3)이다.
    /// 포탈 지름(62.5)이 오목한 자리 세로(61.5)와 거의 같아서 **벽에 안 박히는 쪽**인 한가운데를 쓴다.
    /// </summary>
    public static Vector3 LaneStoryPortalSpot(Island lane)
    {
        Rect track = LaneTrackRect(lane);
        float x = track.xMax - CornerWallOffsetX - (CornerWallLength + CornerWallThickness) * 0.5f;
        float z = track.yMax - CornerWallOffsetZ - CornerWallThickness * 1.5f;
        return new Vector3(x, IslandTop, z);
    }

    static Rect MinMax(float x0, float z0, float x1, float z1) =>
        Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1));

    public static Vector3[] LaneLoop(Island lane)
    {
        Rect track = LaneTrackRect(lane);

        return new[]
        {
            new Vector3(track.xMin, IslandTop, track.yMax),   // 왼쪽 위 — 출발
            new Vector3(track.xMin, IslandTop, track.yMin),   // 왼쪽 아래
            new Vector3(track.xMax, IslandTop, track.yMin),   // 오른쪽 아래
            new Vector3(track.xMax, IslandTop, track.yMax),   // 오른쪽 위
        };
    }
}
