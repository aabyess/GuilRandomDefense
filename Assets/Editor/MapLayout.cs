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
    // ⚠️ 원작 3256×2712는 **레인 섬 전체가 아니라 적이 도는 방어구역(p1_life_zone) 자체**의
    // 크기다(PM 정정 2026-09-23). 우리는 앞치마(상점 줄 26 + 유닛 우리 20)를 레인 섬 안에
    // 넣어 두고 그 **전체**를 3256×2712에 맞췄던 탓에, 정작 필드가 781.4×459.2(비 1.70)로
    // 납작해져 있었다. 필드 쪽을 781.4×650.9(비 1.20)로 놓고 앞치마를 그 아래에 덧붙인다.
    const float LaneFieldSizeX = 781.4f;               // 원작 3256 ÷ Scale
    const float LaneFieldSizeZ = 650.9f;               // 원작 2712 ÷ Scale — 비 1.20
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
    // 있어 레인은 왼쪽으로 펼치는 쪽이 낫다"). 아래로는 GachaIsland·StoryZone이 전부 z<63에
    // 있어 필드 바닥을 z=100에서 시작하면 안 걸린다. MapGenerator.cs:3508 Overlaps()와 같은
    // 식으로 Warehouses·SealIslands·Zones 전체와 대조해 0건 확인했다(2026-09-23, 별도 계산).
    const float LaneRightEdgeX = -220f;
    const float LaneRightColumnX = LaneRightEdgeX - LaneSizeX * 0.5f;   // -610.7
    const float LaneLeftColumnX = LaneRightColumnX - LaneSpacingX;     // -1622.5
    // ⚠️ 앞치마는 필드 **아래로** 199.4만큼 뻗는다(ApronGap 7.7 + 우리 83.3 + 상점 108.3).
    // 겹침 검사(MapGenerator.CheckOverlaps)는 Lanes 배열 = 필드만 보므로 앞치마는 손으로
    // 확인해야 한다. 아래 줄 레인의 앞치마 바닥이 뽑기섬 윗변(z=62.5)을 안 넘게 필드 아래변을
    // z=300에 둔다 → 앞치마 바닥 z=100.6, 뽑기섬과 38.1 여유(2026-09-23 계산 확인).
    // 위쪽은 레인1·2 꼭대기까지 아무것도 없다(바다 반폭 3334).
    const float LaneFieldBottomZ = 300f;
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
        // (2026-09-23, 원작 비율 4단계, PM 지시) 옛 90×26/90×50은 SlotSpacing이 6이던 시절
        // 기준이다 — SlotSpacing이 61.4로 커지면서 초월 25종이 한 줄에 6.1칸(가로 430)밖에
        // 못 들어가 7종×4줄이 필요해졌고(세로 246+여유), 불멸 8종 원형도 반지름 최소 78.2
        // (지름 230+여유)가 필요해졌다. PM이 준 목표(최종 크기, Scale 적용 후) 초월 460×280·
        // 불멸 280×280을 이 리터럴(=목표÷Scale)로 옮겼다. z중심도 같이 옮겼다 — 커진 크기가
        // 옛 z(52·8)를 그대로 쓰면 CombineTable과 겹친다. CombineTable 윗변(z=-70.8, Scale
        // 후)에서 40 띄우고 쌓아 올렸고, MapGenerator.cs:3508 Overlaps()와 같은 식으로 다른
        // 16개 섬 전부와 대조해 0건 확인했다(레인 때와 같은 방식, 별도 계산).
        new Island("ImmortalDisplay",  150f * Scale, 102.990f * Scale, 67.195f * Scale, 67.195f * Scale, "display"),
        new Island("TranscendDisplay", 150f * Scale,  26.197f * Scale, 110.391f * Scale, 67.195f * Scale, "display"),
        // 1.5배로 키운 값(원래 120x100). 여유가 빠듯하다 — 봉인섬과 z로 12,
        // 뽑기섬과 x로 10밖에 안 남으니 더 키우려면 이웃을 먼저 옮겨야 한다.
        new Island("StoryZone",       -290f * Scale, -80f * Scale, 180f * Scale, 150f * Scale, "story"),
        // 오른쪽 전시 칸이 다른세계 조합식 한 줄(재료 6칸 + 비용 3칸)을 담아야 해서 폭을 넓혔다.
        // (2026-09-23, 원작 비율 4단계) SlotSpacing 6→61.4로 오른쪽 전시 칸이 줄당 11칸에서
        // 4칸으로 줄어, 랜덤유닛 14종이 2줄에서 4줄이 됐다. 그 아래 다른세계 조합식 14줄까지
        // 더하면 깊이 827이 필요한데 예전 섬(깊이 752)으로는 75가 모자란다(생성 보고문의
        // "⚠️ 모자람"이 뜨는 자리다). **위쪽은 안 건드리고 아래로만 160 늘렸다** — 위로 늘리면
        // 바로 위 레인3·4(아래변 z=100)와 겹친다. 그래서 size_z는 +160/Scale, center_z는
        // −80/Scale만큼 내렸다(윗변 z=62.5 고정). 새 여유는 85다.
        new Island("GachaIsland",      -82f * Scale, -99.20f * Scale, 136f * Scale, 228.40f * Scale, "gacha"),
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
        new Island("CombineTable",     137.99f * Scale, -197.54f * Scale, 275.98f * Scale, 361f * Scale, "combine"),
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

    /// <summary>이 레인의 순찰 경로·흙길 inset(x, z). 둘이 같은 값을 봐야 적이 흙길 위를 걷는다.</summary>
    public static Vector2 LaneTrackInset(Island lane)
    {
        Island field = LaneField(lane);
        return new Vector2(field.size.x * TrackInsetRatioX, field.size.y * TrackInsetRatioZ);
    }

    public static Vector3[] LaneLoop(Island lane)
    {
        Island field = LaneField(lane);
        Vector2 inset = LaneTrackInset(lane);
        float halfX = field.size.x * 0.5f - inset.x;
        float halfZ = field.size.y * 0.5f - inset.y;
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
