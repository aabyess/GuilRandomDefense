using UnityEngine;

// 돈 도박 누적 성공 골드. GoldWallet/ResourceWallet/UnitInventory와 같은 결로
// PlayerContext에 나란히 붙인다. GAMBLING.md: 초급·중급·고급 통틀어 35,000골드
// 획득 시 "졸업"(더 이상 돈 도박을 못 함).
public class GamblingProgress : MonoBehaviour
{
    [SerializeField] int moneyGamblingGraduationCap = 35000;

    public int MoneyGamblingWon { get; private set; }
    public bool IsMoneyGamblingGraduated => MoneyGamblingWon >= moneyGamblingGraduationCap;

    public void AddMoneyGamblingWinnings(int amount)
    {
        if (amount <= 0) return;
        MoneyGamblingWon += amount;
    }
}
