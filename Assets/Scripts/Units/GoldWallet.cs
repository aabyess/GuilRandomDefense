using UnityEngine;

public class GoldWallet : MonoBehaviour
{
    [SerializeField] int startingGold = 30;   // 화폐는 엔. 맵 생성기가 씬 값도 같이 덮어쓴다

    public int Gold { get; private set; }

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
