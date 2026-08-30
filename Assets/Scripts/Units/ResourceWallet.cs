using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceType
{
    Wood,
    Token,
    LuckyToken,
    // 도움소(레인마다 있는 상점형 건물)가 광역 스킬을 쓰는 데 소모한다.
    // 반드시 맨 뒤에 둘 것 — 앞에 끼우면 이미 저장된 자원 값이 전부 밀린다.
    Mana,
}

public class ResourceWallet : MonoBehaviour
{
    [Serializable]
    class StartingAmount
    {
        public ResourceType type;
        public int amount;
    }

    [SerializeField] List<StartingAmount> startingAmounts = new List<StartingAmount>();

    readonly Dictionary<ResourceType, int> amounts = new Dictionary<ResourceType, int>();

    public event Action<ResourceType, int> OnResourceChanged;

    void Awake()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            amounts[type] = 0;

        foreach (StartingAmount entry in startingAmounts)
            amounts[entry.type] = entry.amount;
    }

    public int Get(ResourceType type)
    {
        return amounts.TryGetValue(type, out int value) ? value : 0;
    }

    public bool TrySpend(ResourceType type, int amount)
    {
        if (amount < 0 || Get(type) < amount) return false;

        amounts[type] -= amount;
        OnResourceChanged?.Invoke(type, amounts[type]);
        return true;
    }

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        amounts[type] = Get(type) + amount;
        OnResourceChanged?.Invoke(type, amounts[type]);
    }
}
