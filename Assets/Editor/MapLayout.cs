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
        public float laneToIsland;      // 레인 무리 남쪽 끝 ↔ 옮긴 섬 무리 북쪽 끝
        public float storyToGacha;      // 스토리존 ↔ 뽑기섬
        public float gachaToCombine;    // 뽑기섬 ↔ 조합판
        public float islandClusterNorthZ;
    }

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

        float north = float.MinValue;
        foreach (Island island in SealIslands)
            north = Mathf.Max(north, island.center.y + island.size.y * 0.5f);
        foreach (Island island in new[] { story, gacha, combine })
            north = Mathf.Max(north, island.center.y + island.size.y * 0.5f);
        north += CliffMargin;

        return new SpacingReadout
        {
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
    // 있어 레인은 왼쪽으로 펼치는 쪽이 낫다"). 아래쪽 섬들(GachaIsland·StoryZone·CombineTable·
    // 봉인섬)은 2026-09-24에 왼쪽 1200·아래 465를 먹어 전부 z<-402로 내려갔으니 더 멀어졌다.
    // MapGenerator.cs의 Overlaps()로 Warehouses·SealIslands·Zones 전체와 대조해 0건 확인했고,
    // 앞치마까지 넣은 22개 231쌍으로도 0건이다(2026-09-24 재확인, 별도 계산).
    const float LaneRightEdgeX = -220f;
    const float LaneRightColumnX = LaneRightEdgeX - LaneSizeX * 0.5f;   // -610.7
    const float LaneLeftColumnX = LaneRightColumnX - LaneSpacingX;     // -1622.5
    // ⚠️ 앞치마는 필드 **아래로** 199.4만큼 뻗는다(ApronGap 7.7 + 우리 83.3 + 상점 108.3).
    // 겹침 검사(MapGenerator.CheckOverlaps)는 Lanes 배열 = 필드만 보므로 앞치마는 손으로
    // 확인해야 한다. 필드 아래변을 z=300에 두면 앞치마 바닥이 z=100.6, 치마까지 98.9다 —
    // 이것이 **레인 무리의 진짜 남쪽 끝**이고 LaneClusterSouthEdgeZ가 그 값이다.
    // 아래쪽 섬들과의 간격은 이제 그 끝에서 LaneToIslandGapZ(500)로 유도한다(사장님 09-24
    // 「간격도 벌려줘」) — 예전의 「뽑기섬과 38.1 여유」는 그 지시로 없어진 값이다.
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
    public const float LaneToIslandGapZ = 500f;

    /// <summary>레인 무리의 실제 남쪽 끝 — 앞치마와 치마까지 포함한다.</summary>
    public const float LaneClusterSouthEdgeZ = LaneFieldBottomZ - LaneApronDepth - CliffMargin;

    // 내리기 전 무리의 북쪽 끝(뽑기섬 윗변 + 치마). 아래 GachaIsland 정의와 같은 수를 쓴다 —
    // 뽑기섬 z를 고치면 이 줄도 같이 고쳐야 하고, 안 고치면 보고문의 간격이 500에서 벗어난다.
    const float MovedClusterNorthEdgeBeforeShift = -99.20f * Scale + 228.40f * Scale * 0.5f + CliffMargin;
    const float DownShift = LaneClusterSouthEdgeZ - LaneToIslandGapZ - MovedClusterNorthEdgeBeforeShift;

    // 옮기는 셋의 x 중심 — StoryZone은 앞서 정한 자리 그대로, 나머지는 간격에서 유도한다.
    const float StoryZoneSizeX = 180f * Scale;
    const float GachaSizeX = 136f * Scale;
    const float CombineSizeX = 407.97f * Scale;

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
    //    내려가 사장님이 보시는 자리가 바뀌고, 올리면 레인↔섬 간격을 정하는 섬이 뽑기섬에서
    //    조합판으로 바뀐다(지금 뽑기섬 윗변 −402.9 · 조합판 −536.4).
    const float CombineTopZ = -17.04f * Scale + DownShift;
    const float CombineCenterZ = CombineTopZ - CombineSizeZ * 0.5f;
    // ⚠️ 두 간격(x·z)은 **같은 자**를 써야 한다 — 둘 다 치마 기준이다. 안 그러면 보고문의
    //    「300」과 「500」이 서로 다른 뜻이 되고, 「x를 z만큼 벌려라」가 어긋난다.
    //    그래서 치마 양쪽 몫(CliffOverhang)을 더한다.
    const float StoryZoneCenterX = -290f * Scale + LeftShift;
    const float GachaCenterX = StoryZoneCenterX + StoryZoneSizeX * 0.5f
                             + IslandGapX + CliffOverhang + GachaSizeX * 0.5f;
    const float CombineCenterX = GachaCenterX + GachaSizeX * 0.5f
                               + IslandGapX + CliffOverhang + CombineSizeX * 0.5f;

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
        new Island("ImmortalDisplay",  150f * Scale, 102.990f * Scale, 67.195f * Scale, 67.195f * Scale, "display"),
        new Island("TranscendDisplay", 150f * Scale,  26.197f * Scale, 110.391f * Scale, 67.195f * Scale, "display"),
        // 1.5배로 키운 값(원래 120x100). 여유가 빠듯하다 — 봉인섬과 z로 12,
        // 뽑기섬과 x로 10밖에 안 남으니 더 키우려면 이웃을 먼저 옮겨야 한다.
        new Island("StoryZone",       StoryZoneCenterX, -80f * Scale + DownShift, StoryZoneSizeX, 150f * Scale, "story"),
        // 오른쪽 전시 칸이 다른세계 조합식 한 줄(재료 6칸 + 비용 3칸)을 담아야 해서 폭을 넓혔다.
        // (2026-09-23, 원작 비율 4단계) SlotSpacing 6→61.4로 오른쪽 전시 칸이 줄당 11칸에서
        // 4칸으로 줄어, 랜덤유닛 14종이 2줄에서 4줄이 됐다. 그 아래 다른세계 조합식 14줄까지
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
