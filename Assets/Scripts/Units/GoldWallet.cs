using UnityEngine;

public class GoldWallet : MonoBehaviour
{
    [SerializeField] int startingGold = 30;   // 화폐는 엔. 맵 생성기가 씬 값도 같이 덮어쓴다

    public int Gold { get; private set; }

    // 항법(원작 route 선택) 보너스 — 처치 골드 공식 Gold_Math × (2 + Gold_Plus)의 두 번째 항이다.
    // 원작은 real이고 증가량이 전부 소수라 float이어야 한다(2026-09-06 PM 확인, int였을 때
    // 넷을 다 모아도 1.30 → int면 1로 버려져 최종 배수가 10% 과소해졌다 — 지금은 아래 넷이
    // 전부 미배선이라 항상 0이므로 이 교정 자체는 회귀가 없다).
    // 조건 넷(리서치담당 확보, 2026-09-04/06):
    //   +0.60  A04X 레벨==1 AND Nami_legend_Boolean==false
    //   +0.30  버기 포에버 AND 누적 클리어 ≥ 10
    //   +0.20  나미 이터널
    //   +0.20  포인트값 22 유닛이 아이템 I00Z 획득
    // 포에버·이터널·누적 클리어 개념이 우리에 없어 아직 배선 안 함(PM 지시) — 정해지면
    // 이 필드를 채우는 코드만 새로 붙이면 된다. 공식(RewardDistributor) 쪽은 이미 준비됨.
    // ✅ 2026-09-12: 첫 줄(+0.60 전설 나미)은 TreasureHunt.OnUnitSpawned가 부른다 — 원작도 보물찾기
    //    보너스와 같은 블록(Trig_UnitJohabCounter_Func020)에서 준다.
    public float GoldPlus { get; private set; }

    public void AddGoldPlus(float amount)
    {
        if (amount <= 0f) return;
        GoldPlus += amount;
    }

    public event System.Action<int> OnGoldChanged;

    void Awake()
    {
        Gold = startingGold;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Gold < amount) return false;

        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;

        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    // 원작 SetPlayerStateBJ(플레이어, GOLD, 0) 대응 — 보스 라운드 진입("보스 전에 다
    // 써라"는 설계, Trig_Enemy_Boss_create/sinsekai)과 플레이어 탈락(udg_PlayerDeath[i]=1)
    // 때 골드를 몰수한다. 호출부는 RewardDistributor(2026-09-06 PM 지시).
    public void ZeroOut()
    {
        if (Gold == 0) return;

        Gold = 0;
        OnGoldChanged?.Invoke(Gold);
    }
}
