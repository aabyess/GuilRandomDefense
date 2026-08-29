using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceType
{
    Wood,
    Token,
    LuckyToken,
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
