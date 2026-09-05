using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 원작 세이브 시스템의 우리 버전(사장님 확정 2026-09-05, 11번 — "진짜 영속 저장"). 원작은
// 세이브 코드 문자열(VJSE)로 우회했지만 우리는 실제 파일로 쓴다. 플레이어 슬롯(PlayerContext.
// PlayerId, 0~3)별로 파일이 갈린다 — 원작이 `[id]` 배열로 넷을 따로 관리하던 것과 같은 구조다.
//
// 여기서 다루는 값·문턱·지급 9곳의 근거는 전부 war3map.j 원문 직접 대조로 확인했다(PM +
// 구현담당2, 2026-09-05). 세이브 코드 인코더(MySampleSavecodeConfig, VJSE_Decode)는
// 옮기지 않는다 — 워크3 채팅창 길이 제약이 만든 우회로일 뿐, 실제 파일 저장에는 없는 제약이다.
public class PersistentSave : MonoBehaviour
{
    // GoldWallet·ResourceWallet·UnitUpgrades와 같은 결 — PlayerContext를 되짚어 참조하지
    // 않는다(이 프로젝트의 지갑류는 전부 그렇다, PlayerContext가 거꾸로 이들을 들고 있다).
    // 그래서 파일 키로 쓸 playerId를 직접 들고 있다 — MapGenerator가 PlayerContext.playerId와
    // 같은 값으로 같이 꽂아준다.
    [SerializeField] int playerId;

    public PlayerSaveData Data { get; private set; } = new PlayerSaveData();

    // 이번 판(접속) 동안 쌓인 점수 — 원작 udg_present_Save_playpoint 대응. 파일에는 안 쓴다,
    // FinishRun 때 Data.cumulativePlayPoint에 합산되고 나면 사라져도 되는 값이다.
    public int SessionPoints { get; private set; }

    string SavePath => Path.Combine(Application.persistentDataPath, "Save", $"player_{playerId}.json");

    void Awake()
    {
        Load();
    }

    void Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                Data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(SavePath)) ?? new PlayerSaveData();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PersistentSave: {SavePath} 읽기 실패, 새 데이터로 시작합니다. {e.Message}", this);
            Data = new PlayerSaveData();
        }
    }

    void WriteToDisk()
    {
        string dir = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(SavePath, JsonUtility.ToJson(Data, prettyPrint: true));
    }

    // 지급 9곳(Story2·creep_reward·door_quest·Red_dog·Quest_sky_1/2/3·treasure)이 부르는 단일
    // 창구. 한 판 최대 13점 — war3map.j의 udg_Save_playpoint/udg_present_Save_playpoint 증가
    // 지점 9곳(초기화 2곳 제외)을 전수 대조해 확인했다:
    // Story2 +1,+2 / creep_reward +1 / door_quest +3 / Red_dog +1(삼대장, 안 죽은 플레이어만) /
    // Quest_sky_1/2/3 +1씩(3곳) / treasure +2 = 13.
    //
    // ⚠️ door_quest와 Red_dog은 원작에서 한 덩어리다(PM 조사, 2026-09-05) — door_quest를
    // 깨면 붉은개·푸른꿩·노란원숭이 중 하나가 무작위로(GetRandomInt(1,3)) 나오고, 그걸 잡는
    // 순간이 Red_dog이다(특성포인트 +1과 세이브포인트 +1이 동시에 나간다). 나중에 만들 때
    // 둘을 따로 잡지 말 것.
    //
    // ✅ 2026-09-05: Quest_sky_3(거대 해왕류, o02N)이 첫 실제 호출부다 — SeaKingSpawner.
    // GrantReward가 처치 시 전 플레이어에게 +1을 부른다. 하늘섬 퀘스트 1·2는 발동 조건을
    // 아직 못 풀어서 안 만들었다(PM 지시) — Quest_sky_1/2는 여전히 미도달.
    //
    // ⚠️ 나머지(Story2·creep_reward·door_quest·Red_dog·treasure)는 여전히 대응 코드가
    // 0건이다(2026-09-05 확인 — Story는 13개 있지만 "Story2"가 우리 스토리 몇 번에
    // 대응하는지는 원작-우리 스토리 번호가 아예 별개라 결정할 근거가 없다). 각 시스템이
    // 실제로 만들어질 때 그 트리거가 이 메서드를 부르면 된다.
    public void AddSessionPoints(int amount)
    {
        if (amount <= 0) return;
        SessionPoints += amount;
    }

    // 신세계(75라운드) 완주 보너스 — war3map.j Trig_Save_sido3, 원문 그대로:
    // yuca_bonus = 10 - ((P_Counter/10)*10)/5  (정수 나눗셈, 계단형)
    // P_Counter는 원작에서 "살아있는 적 수"다. 우리는 EnemyDummy.Active 중 이 플레이어
    // 레인 소속만 세어 대응시켰다 — 라운드 시스템이 레인별이라 가장 가까운 대응이다.
    public int AddClearBonus(int aliveEnemyCount)
    {
        int p = Mathf.Max(0, aliveEnemyCount);
        int bonus = 10 - ((p / 10) * 10) / 5;
        AddSessionPoints(bonus);
        return bonus;
    }

    public static int CountAliveInLane(int laneIndex)
    {
        int alive = 0;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
            if (enemy != null && enemy.LaneIndex == laneIndex) alive++;
        return alive;
    }

    // war3map.j Trig_SaveReward_1 원문 그대로 — 자기 파괴형 1회 트리거라 "이번에 새로 세이브를
    // 불러왔을 때" 그 시점의 누적 포인트로만 판정한다(사람별 "이미 받았는지" 플래그가 원작에
    // 없다 — 매 판 로드할 때마다 그 시점 누적치로 다시 판정되는 구조다. 우리도 그대로 따른다:
    // 영구 1회 지급이 아니라 "불러올 때마다, 그 시점 누적치가 문턱을 넘으면" 주는 개시 보너스).
    // 독립 if 5개다 — 900을 넘으면 다섯 다 받는다. RewardDistributor.Start()가 PlayerContext.
    // Occupied마다 이 컨텍스트의 지갑들을 넘겨 부른다(GrantStartingTraitPoints와 같은 결) —
    // PersistentSave 자신은 지갑을 안 들고 있어서 파라미터로 받는다.
    public void ApplyLoadThresholdRewards(GoldWallet gold, ResourceWallet resources, UnitUpgrades upgrades)
    {
        int p = Data.cumulativePlayPoint;
        if (p >= 10) gold?.Add(10);
        if (p >= 100) resources?.Add(ResourceType.Wood, 1);
        if (p >= 300) upgrades?.AddTraitPoints(1);
        if (p >= 600) upgrades?.AddTraitPoints(1);
        if (p >= 900) upgrades?.AddTraitPoints(1);
    }

    // 게임 종료(신세계 완주 또는 중도 종료) 시점에 부른다. 원작 SavePlayer 그대로: 죽은
    // 플레이어는 저장하지 않는다("패배한 상태에선 더이상 세이브가 불가능합니다") — 호출부
    // (RoundManager.FinishPersistentSave)가 IsDead를 먼저 걸러서 여기까지 안 보낸다.
    // cleared가 true일 때만 누적 클리어 횟수를 올린다 — 원작이 udg_Clear_Game==1일 때만
    // Save_playcount를 올리는 것과 같다(중도 이탈은 클리어 횟수에 안 들어간다).
    public void FinishRun(bool cleared)
    {
        Data.cumulativePlayPoint += SessionPoints;
        if (cleared) Data.cumulativeClearCount += 1;
        if (SessionPoints > Data.bestRunPoint) Data.bestRunPoint = SessionPoints;

        SessionPoints = 0;
        WriteToDisk();
    }
}
