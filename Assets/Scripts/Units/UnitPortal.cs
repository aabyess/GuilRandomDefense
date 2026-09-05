using System.Collections.Generic;
using UnityEngine;

// 포탈에 허용된 등급의 위습이 들어오면 유닛을 지급하고 위습을 소모한다.
// 포탈 오브젝트에 Collider(isTrigger = true)가 필요하다.
[RequireComponent(typeof(Collider))]
public class UnitPortal : MonoBehaviour, ISerializationCallbackReceiver
{
    // 비어 있으면 모든 등급 허용.
    [SerializeField] List<UnitGrade> acceptedGrades = new List<UnitGrade>();

    [SerializeField] UnitData specificUnit; // 선택형 포탈(흔함 등)이면 지정, 비어있으면 등급 내 랜덤 지급

    // 받는 위습의 등급과 지급하는 유닛의 등급이 다른 포탈이 있다 —
    // 자원 칸 북쪽은 "랜덤유닛 위습"을 받아 "흔함 유닛"을 준다.
    // 끄면 예전처럼 위습 등급 그대로 뽑는다.
    [SerializeField] bool overrideRewardGrade;
    [SerializeField] UnitGrade rewardGrade;
    // 원작의 "희귀함, 특수함(3%확률) 등급유닛 전체 랜덤" 같은 칸을 위한 것.
    // 낮은 확률로 지정한 등급에서 대신 뽑는다. 0이면 쓰지 않는다.
    [SerializeField] UnitGrade bonusGrade;
    // 보너스가 특정 유닛일 때 쓴다(랜덤 포탈의 1% 상붕카). 비어 있으면 bonusGrade에서 뽑는다.
    [SerializeField] UnitData bonusUnit;
    [SerializeField, Range(0f, 100f)] float bonusChancePercent;

    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;

    // 비워두면 위습 주인의 레인 한가운데에 소환한다.
    // ⚠️ 지금 구조에서는 채워도 쓸 수 없다 — MapGenerator.ConfigurePortal이 맵을 재생성할
    // 때마다 이 필드에 무조건 null을 쓴다(인스펙터에서 손으로 채워도 다음 재생성에 지워진다).
    // 게다가 채워지면 isBonus와 무관하게 항상 이 지점으로 보내서, 2026-09-03에 들어온
    // "보너스는 레인 중앙 / 일반은 유닛 우리" 라우팅 분기(ResolveSpawnPosition)를 통째로
    // 우회한다. 살리려면 그 라우팅과 어떻게 공존할지부터 정해야 한다 — 그전까지는 지우지
    // 말고 이 상태로 둔다(PM 결정, 2026-09-05).
    [SerializeField] Transform spawnPoint;

    // 마이그레이션 전용 필드 — acceptedGrade(단일 등급) → acceptedGrades(리스트) 전환 전에 씬에 저장된
    // 값을 흡수하기 위해 이름·타입을 그대로 남겨뒀다. 새로 만드는 포탈은 이 필드를 쓰지 말고
    // acceptedGrades를 직접 채울 것 — 비워두면 최초 직렬화 시 이 필드의 기본값(0)으로 한 번 채워진다.
    [SerializeField, HideInInspector] UnitGrade acceptedGrade;
    [SerializeField, HideInInspector] bool legacyGradeMigrated;

    readonly HashSet<Wisp> loggedRejectionFor = new HashSet<Wisp>();

    // ⚠️ 2026-09-06 추가(리서치담당 원문 확인, Trig_Random_Base1) — 랜덤 위습 포탈의
    // 흔함 9종 지급은 순수 균등 랜덤이 아니라 "7번 랜덤 + 8번째 확정"의 8회 주기다:
    //   correction_Count[플레이어]==7이면 → 지금까지 가장 적게 나온 종류를 확정 지급하고
    //     카운터 리셋(동률이면 번호 작은 쪽 — 원작이 1..9 순회라 그렇다)
    //   아니면 → 카운터+1, 9종 균등 랜덤
    //   해적선(bonusChancePercent)이 나온 뽑기는 이 카운터도 종류 집계도 안 올린다
    //     (원작이 그 else 분기를 통째로 건너뛴다)
    // 집계는 플레이어별·종류별로 게임 세션 내내 누적(라운드 무관, 세이브 파일 밖).
    // 씬/MapGenerator를 새로 안 건드리려고 이 포탈 인스턴스 안에 상태를 둔다 — 이
    // 메커니즘을 쓰는 포탈이 지금 하나뿐이라 인스턴스별 상태로 충분하다.
    class CommonPityState
    {
        public int correctionCount;
        public readonly List<int> pullCounts = new List<int>();
    }
    readonly Dictionary<int, CommonPityState> commonPityByPlayer = new Dictionary<int, CommonPityState>();

    // 흔함 9종의 "종류 번호"는 원작 h001~h009 순서인데 그 9명(루피·조로·나미 등)이
    // 우리 흔함 로스터와 이름 대응이 없다(스킬 배정 4채널 어디에도 없음 — 원작에서부터
    // 능력이 없는 기본 유닛이라 CSV 자체에 안 나온다). 이름매핑은 이미 불가로 닫혀 있어,
    // MainGachaTable의 흔함(등급0) pool에 저장된 순서를 "종류 1~9"로 그대로 쓴다(PM
    // 지시: 대응이 애매하면 로스터 순서를 그대로 쓰고 사실만 남기라 — 진짜 원작 인물
    // 대응이 아니라 지급 순서의 안정적인 기준일 뿐이다).
    List<UnitData> CommonPityOrder()
    {
        if (gachaTable == null || gachaTable.entries == null) return null;
        GachaTable.GradeEntry entry = gachaTable.entries.Find(e => e != null && e.grade == UnitGrade.Common);
        return entry?.pool;
    }

    // 해적선이 아닌 정상 흔함 지급일 때만 부른다 — 카운터·집계는 여기서만 움직인다.
    UnitData RollCommonWithPity(int ownerId)
    {
        List<UnitData> order = CommonPityOrder();
        if (order == null || order.Count == 0) return gachaTable != null ? gachaTable.RollFromGrade(UnitGrade.Common) : null;

        if (!commonPityByPlayer.TryGetValue(ownerId, out CommonPityState state))
        {
            state = new CommonPityState();
            commonPityByPlayer[ownerId] = state;
        }
        while (state.pullCounts.Count < order.Count) state.pullCounts.Add(0);

        int index;
        if (state.correctionCount >= 7)
        {
            index = 0;
            for (int i = 1; i < order.Count; i++)
            {
                if (state.pullCounts[i] < state.pullCounts[index]) index = i;
            }
            state.correctionCount = 0;
        }
        else
        {
            index = Random.Range(0, order.Count);
            state.correctionCount++;
        }
        state.pullCounts[index]++;

        return order[index];
    }

    public void OnAfterDeserialize()
    {
        if (legacyGradeMigrated) return;
        legacyGradeMigrated = true;

        if (acceptedGrades == null)
        {
            acceptedGrades = new List<UnitGrade>();
        }

        if (acceptedGrades.Count == 0)
        {
            acceptedGrades.Add(acceptedGrade);
        }
    }

    public void OnBeforeSerialize() { }

    // ⚠️ 2026-09-05: MapGenerator.ConfigurePortal이 SerializedObject로 acceptedGrades를
    // 채웠는데 씬 파일에 한 번도 안 들어갔다(사장님이 맵을 두 번 재생성해서 확인, PM 재조사) —
    // 원인 후보(enumValueIndex가 리스트 원소에 안 맞는 접근자였을 가능성 등)를 확정하지
    // 못해서, 아예 SerializedProperty를 거치지 않고 이 메서드로 필드를 직접 대입하는 쪽으로
    // 바꿨다 — 씬 오브젝트라 프리팹 오버라이드 문제가 없어 안전하다. 호출부는
    // EditorUtility.SetDirty를 반드시 같이 불러야 한다(직접 대입은 SerializedObject처럼
    // 자동으로 dirty 표시가 안 된다).
    public void SetAcceptedGrade(UnitGrade grade)
    {
        acceptedGrades.Clear();
        acceptedGrades.Add(grade);
        legacyGradeMigrated = true; // 이 값이 있으니 OnAfterDeserialize의 레거시 흡수를 안 타도 된다
    }

    bool Accepts(UnitGrade grade)
    {
        return acceptedGrades == null || acceptedGrades.Count == 0 || acceptedGrades.Contains(grade);
    }

    // isBonus: 이 결과가 bonusChancePercent 확률(1% 상붕카 등)에서 나왔는지. 이름으로
    // "상붕카인지" 판단하지 않고(사장님이 유닛 이름을 자주 바꾼다) 어느 뽑기 경로를 탔는지로
    // 직접 표시한다 — 호출부가 이 값으로 자리(우리 vs 레인 한가운데)를 정한다.
    UnitData RollReward(UnitGrade grade, int ownerId, out bool isBonus)
    {
        isBonus = false;
        if (gachaTable == null) return null;

        if (bonusChancePercent > 0f && Random.Range(0f, 100f) < bonusChancePercent)
        {
            UnitData bonus = bonusUnit != null ? bonusUnit : gachaTable.RollFromGrade(bonusGrade);
            // 보너스 등급 풀이 비어 있어도 뽑기 자체가 실패하면 안 된다 — 원래 등급으로 넘어간다.
            // ⚠️ 보너스가 나온 뽑기는 아래 8회 천장 카운터·집계를 안 올린다(원작이 그 else
            // 분기를 통째로 건너뛴다) — 그래서 이 return이 RollCommonWithPity보다 먼저다.
            if (bonus != null) { isBonus = true; return bonus; }
        }

        // 흔함 등급은 8회 천장이 있는 원작 랜덤 위습 포탈(Trig_Random_Base1) 전용 분기다.
        // bonusUnit이 설정된 포탈만 이 메커니즘을 쓴다고 본다 — 지금 프로젝트에 그런
        // 포탈이 이것 하나뿐이라 별도 플래그 없이 이 조합으로 충분히 구분된다.
        if (grade == UnitGrade.Common && bonusUnit != null)
        {
            return RollCommonWithPity(ownerId);
        }

        return gachaTable.RollFromGrade(grade);
    }

    // 뽑기 섬에서 소환하면 지상 유닛은 바다에 막혀 레인까지 갈 수 없다.
    // 위습을 가져온 플레이어의 레인에 내보낸다 — 보통은 유닛 우리(칸 배정), 단 보너스로 나온
    // 유닛(1% 상붕카 등)은 우리 칸에 끼워 넣지 않고 레인 한가운데에 세운다(사장님 지시,
    // 2026-09-03) — 흔치 않은 만큼 눈에 띄어야 한다는 의도로 읽었다.
    Vector3 ResolveSpawnPosition(int ownerId, UnitData reward, bool isBonus)
    {
        if (spawnPoint != null) return spawnPoint.position;

        LaneMarker lane = LaneMarker.Get(ownerId);
        if (lane != null) return isBonus ? lane.LaneCenter : lane.TakeSpawnPosition(reward);

        Debug.LogWarning($"UnitPortal: 플레이어 {ownerId}의 레인을 찾지 못해 포탈 자리에 소환합니다.", this);
        return transform.position;
    }

    // TODO(멀티): 포탈 진입 판정·지급은 서버 권위로 이동해야 함 — 지금은 클라이언트가 직접 처리.
    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out Wisp wisp)) return;
        if (wisp.IsConsumed) return;
        if (wisp.Data == null) return;

        UnitGrade grade = wisp.Data.targetGrade;

        // ⚠️ ResourcePortal의 같은 상황(안 받는 위습)은 이미 PlayerNotification으로 알린다 —
        // 여기는 Debug.Log뿐이라 화면엔 "넣었는데 아무 일도 안 일어남"으로 보였다
        // (PM 지시, 2026-09-05, 버그 #9).
        if (!Accepts(grade))
        {
            if (loggedRejectionFor.Add(wisp))
            {
                int rejectedOwnerId = wisp.TryGetComponent(out OwnedByPlayer rejectedOwner) ? rejectedOwner.OwnerId : LocalPlayer.LocalPlayerId;
                PlayerNotification.Show(rejectedOwnerId, $"{wisp.Data.wispName}은(는) 이 포탈에 쓸 수 없습니다.");
            }
            return;
        }

        // 8회 천장 카운터가 플레이어별이라 RollReward보다 먼저 owner를 구해야 한다
        // (예전엔 소환 직전에 구했는데, 그때는 아직 뽑기를 안 한 뒤였다).
        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        // ⚠️ RollReward가 흔함 8회 천장이면 이 호출 자체가 카운터·집계를 이미 올린다 —
        // 아래에서 unitSpawner/prefab 결손으로 취소돼도 되돌리지 않는다. 설정 오류로만
        // 일어나는 경로라 실전에서는 안 걸릴 것으로 본다(정상 데이터엔 prefab이 다 있다).
        bool isBonusReward = false;
        UnitData reward = specificUnit != null
            ? specificUnit
            : RollReward(overrideRewardGrade ? rewardGrade : grade, ownerId, out isBonusReward);

        if (reward == null)
        {
            Debug.LogWarning($"UnitPortal: {grade} 등급에서 지급할 유닛을 찾지 못해 위습을 소모하지 않았습니다.");
            return;
        }

        // 소환할 수 있는지까지 확인한 뒤에 위습을 소모한다.
        // 예전엔 소환에 실패해도 인벤토리에는 들어가서 완전한 손실은 아니었는데, 인벤토리가
        // 필드 인스턴스의 등록부가 된 뒤로는 소환이 곧 지급이다 — 여기서 빠지면 위습만 사라진다.
        if (unitSpawner == null)
        {
            Debug.LogWarning("UnitPortal: unitSpawner가 비어있어 소환하지 못했습니다 — 위습을 소모하지 않았습니다.", this);
            return;
        }

        if (reward.prefab == null)
        {
            Debug.LogWarning($"UnitPortal: {reward.unitName}에 prefab이 없어 소환하지 못했습니다 — 위습을 소모하지 않았습니다.", this);
            return;
        }

        wisp.MarkConsumed();
        Destroy(wisp.gameObject);

        // Spawn이 인벤토리 등록까지 한다 — 여기서 따로 Add하면 필드에 없는 유닛이 인벤토리에 생긴다.
        unitSpawner.Spawn(reward, ResolveSpawnPosition(ownerId, reward, isBonusReward), ownerId);
    }
}
