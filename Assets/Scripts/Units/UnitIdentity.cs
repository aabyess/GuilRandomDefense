using UnityEngine;

// 스폰된 인스턴스가 자신의 원본 UnitData를 들고 있게 한다. UI 등에서 이름/등급/기준 스탯 조회용.
// 소속 UnitInventory도 여기서 들고 있는다 — 인벤토리가 인스턴스 등록부라서, 이 개체가 사라지면
// 등록도 같이 사라져야 한다.
public class UnitIdentity : MonoBehaviour
{
    [SerializeField] UnitData data;

    UnitInventory inventory;

    public UnitData Data => data;

    public void SetData(UnitData unitData)
    {
        data = unitData;

        // 발밑 고리는 등급 색으로 그려지는데, Selectable은 Awake에서 고리를 만든다 —
        // 그때는 아직 이 메서드가 안 불려서 등급을 모른다. 여기서 다시 칠하게 한다.
        // 이걸 빠뜨리면 모든 유닛이 조용히 "등급 없음" 색으로 나온다.
        if (TryGetComponent(out Selectable selectable)) selectable.RefreshIndicatorColor();
    }

    /// <summary>
    /// 주인의 인벤토리에 등록한다. <see cref="UnitSpawner.Spawn"/>이 부른다 —
    /// Instantiate 시점엔 아직 주인도 데이터도 안 정해져서 Awake/OnEnable에서 스스로 등록할 수 없다.
    /// </summary>
    public void RegisterTo(UnitInventory owner)
    {
        if (inventory == owner) return;

        if (inventory != null) inventory.Unregister(this);

        inventory = owner;

        if (inventory != null) inventory.Register(this);
    }

    /// <summary>
    /// 이 유닛을 소모한다 — 인벤토리에서 빼고 필드에서도 없앤다.
    /// Destroy만 부르면 OnDestroy가 프레임 끝에야 돌아서, 그 사이 인벤토리에 유령이 남는다
    /// (Wisp.MarkConsumed가 존재하는 것과 같은 이유). 두 가지를 따로 부르게 두면 언젠가 한쪽을
    /// 빠뜨리므로 하나로 묶어둔다.
    /// </summary>
    public void Consume()
    {
        if (inventory != null) inventory.Unregister(this);
        inventory = null;

        Destroy(gameObject);
    }

    // 해제를 파괴 자체에 묶는다. 파괴 경로(조합·연금술·앞으로 추가될 사망)마다 손으로 넣으면
    // 언젠가 반드시 빠뜨리고, 그때 인벤토리에 유령이 남는다.
    // OnDisable이 아니라 OnDestroy인 이유: 창고는 유닛을 비활성화하지 않고 워프시키는데,
    // 나중에 누가 비활성화를 넣으면 창고 유닛이 인벤토리에서 조용히 사라진다.
    void OnDestroy()
    {
        if (inventory != null) inventory.Unregister(this);
        inventory = null;
    }
}
