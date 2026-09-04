using UnityEngine;

public class GoldWallet : MonoBehaviour
{
    [SerializeField] int startingGold = 30;   // 화폐는 엔. 맵 생성기가 씬 값도 같이 덮어쓴다

    public int Gold { get; private set; }

    // 항법(원작 route 선택) 보너스 — 처치 골드 공식 Gold_Math × (2 + Gold_Plus)의 두 번째 항이다.
    // 리서치담당이 조건(나미 전설 조합 +0.6, 영원함 나미/버기 택1 +0.2~0.3, 수배서 아이템 +0.2)을
    // 찾아뒀지만 어디서 어떻게 걸리는지는 아직 안 정해져서 항상 0이다(PM 지시, 2026-09-04) —
    // 정해지면 이 필드를 채우는 코드만 새로 붙이면 된다. 공식(RewardDistributor) 쪽은 이미 준비됨.
    public int GoldPlus { get; private set; }

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
}
