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

    [Serializable]
    class ResourceCap
    {
        public ResourceType type;
        public int cap;   // 0이면 무제한
    }

    // 원작 도움소 유닛(h08A, war3map.w3u) 확인값 — umpm(최대 마나)=1000, umpi(시작 마나)=15.
    // 골드·목재·행운토큰엔 원작에도 이런 상한이 없다 — 마나에만 적용한다.
    //
    // 씬의 4개 ResourceWallet 인스턴스는 지금 이 값을 caps/startingAmounts 리스트에 안 넣어뒀다
    // (씬 파일은 이번 작업 대상 밖이라 손 안 댐). 그래서 여기 코드 기본값으로 우선 적용하고,
    // 리스트에 해당 타입 항목을 넣으면 그 값이 이 기본값을 덮어쓴다 — 나중에 "도움소 잠그기"
    // 항법으로 상한을 100으로 낮추는 것도 상수를 고치는 게 아니라 그 리스트에 항목을
    // 추가하는 것으로 되게 하기 위함이다(항법 시스템 자체는 아직 없어서 지금은 안 넣는다).
    const int DefaultManaCap = 1000;
    const int DefaultManaStart = 15;

    [SerializeField] List<StartingAmount> startingAmounts = new List<StartingAmount>();
    [SerializeField] List<ResourceCap> caps = new List<ResourceCap>();

    readonly Dictionary<ResourceType, int> amounts = new Dictionary<ResourceType, int>();
    readonly Dictionary<ResourceType, int> capByType = new Dictionary<ResourceType, int>();

    public event Action<ResourceType, int> OnResourceChanged;

    void Awake()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            amounts[type] = 0;

        amounts[ResourceType.Mana] = DefaultManaStart;
        foreach (StartingAmount entry in startingAmounts)
            amounts[entry.type] = entry.amount;

        capByType[ResourceType.Mana] = DefaultManaCap;
        foreach (ResourceCap entry in caps)
            capByType[entry.type] = entry.cap;
    }

    public int Get(ResourceType type)
    {
        return amounts.TryGetValue(type, out int value) ? value : 0;
    }

    // 0이면 무제한. Add()가 상한을 넘는 지급을 자를 때 쓴다.
    public int GetCap(ResourceType type)
    {
        return capByType.TryGetValue(type, out int cap) ? cap : 0;
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

        int newValue = Get(type) + amount;
        int cap = GetCap(type);

        if (cap > 0 && newValue > cap)
        {
            // 조용히 버리면 나중에 "왜 안 모이지"가 된다 — 상한에 걸려 잘려나간 양을 남긴다.
            Debug.Log($"ResourceWallet: {type} 상한({cap})을 넘어 {newValue - cap}만큼 버려졌습니다.");
            newValue = cap;
        }

        amounts[type] = newValue;
        OnResourceChanged?.Invoke(type, newValue);
    }
}
