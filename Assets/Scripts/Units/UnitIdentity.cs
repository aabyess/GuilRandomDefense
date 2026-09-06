using System.Collections.Generic;
using UnityEngine;

// 스폰된 인스턴스가 자신의 원본 UnitData를 들고 있게 한다. UI 등에서 이름/등급/기준 스탯 조회용.
// 소속 UnitInventory도 여기서 들고 있는다 — 인벤토리가 인스턴스 등록부라서, 이 개체가 사라지면
// 등록도 같이 사라져야 한다.
public class UnitIdentity : MonoBehaviour
{
    [SerializeField] UnitData data;

    UnitInventory inventory;

    // 로빈(H098) 특성강화 전용(2026-09-07) — 원작 A0FL(로빈의 손날개, 영구 블링크·사거리
    // 1500·쿨다운 10초)+A0ZP(같은 이름, 순수 시각효과 부착물)를 부여받았다는 표시.
    // UnitTraitData.targetsOtherUnit이 대상으로 지정된 이 인스턴스에 GameHud.
    // RefreshTraitTargeting이 이 값을 세운다 — UnitData(종류)가 아니라 인스턴스별
    // 상태다(같은 종류의 다른 유닛은 안 받는다).
    //
    // ⚠️ 지금은 표시만 한다 — A0FL의 실제 블링크 효과는 아직 안 켠다. 우리 엔진에
    // "플레이어가 지점을 클릭해 순간이동"하는 로직 자체가 없다(UnitData.MovementAbility.
    // Teleport가 이미 "필드만, 로직은 나중에 구현"으로 못박혀 있다 — 새 기반시설이 필요한
    // 사안이라 이번 작업 범위 밖). 그 로직이 생기면 이 값을 읽어 켜면 된다.
    public bool hasRobinWingBlessing;

    // 필드에 나와 있는 아군 유닛 등록부(EnemyDummy.Active와 같은 관례) — 유닛 스킬의
    // Allies/Self 대상, 04번 보스 회복 오라, 이감처럼 "대상을 어떻게 모으나"가 필요한
    // 곳들이 공통으로 쓴다. UnitSpawner.Spawn이 유일한 플레이어 유닛 생성 경로라(주석 참고)
    // 전투 안 하는 유닛(퀘스트 판매 토큰 등)도 여기 UnitIdentity가 항상 붙어서 같이 걸린다 —
    // 그래서 이 등록부를 UnitAttacker가 아니라 UnitIdentity에 뒀다(전투 컴포넌트가 없는
    // 프리팹도 놓치지 않는다).
    //
    // ⚠️ OnEnable/OnDisable을 쓴다 — 인벤토리 등록(아래 RegisterTo/OnDestroy)과 훅이 다른
    // 이유: 저건 "이 플레이어가 이 유닛을 소유하는가"(창고 워프 중에도 유지돼야 함)이고
    // 이건 "지금 필드에서 대상이 될 수 있는가"(비활성화되면 대상에서 빠져야 함)라 관심사가
    // 다르다. 헷갈리지 말 것.
    public static readonly List<UnitIdentity> Active = new List<UnitIdentity>();

    OwnedByPlayer owner;

    public UnitData Data => data;

    /// <summary>이 유닛의 소유 플레이어 ID. 아직 OwnedByPlayer가 안 붙었으면(생성 직후 극히
    /// 짧은 순간) -1 — UnitSpawner.Spawn이 Instantiate 직후 곧바로 붙이므로 실사용 시점엔
    /// 항상 값이 있다.</summary>
    public int OwnerId => owner != null ? owner.OwnerId : -1;

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
    }

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    /// <summary>
    /// caster와 "같은 편"인 플레이어 유닛들 — 플레이어 유닛의 편은 소유자(OwnerId)다.
    /// EnemyDummy.AlliesOf(같은 이름, 적 쪽 판정)와 짝을 이룬다 — 캐스터가 보스면 그쪽을,
    /// 플레이어 유닛이면 이걸 쓴다. notself를 반영해 caster 자신은 뺀다. range&lt;=0이면
    /// 거리 제한 없이 그 소유자의 유닛 전체를 반환한다.
    /// </summary>
    public static List<UnitIdentity> AlliesOf(UnitIdentity caster, float range)
    {
        List<UnitIdentity> result = new List<UnitIdentity>();
        if (caster == null) return result;

        int ownerId = caster.OwnerId;
        float sqrRange = range * range;
        foreach (UnitIdentity ally in Active)
        {
            if (ally == null || ally == caster) continue;
            if (ally.OwnerId != ownerId) continue;
            if (range > 0f && (ally.transform.position - caster.transform.position).sqrMagnitude > sqrRange) continue;
            result.Add(ally);
        }
        return result;
    }

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
