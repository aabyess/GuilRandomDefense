using UnityEngine;

// 대상이 필요한가(광역 스킬 지점, 연금술 유닛)와 필요 없는가(마나포션, 도박 굴리기)를
// 상점이 스스로 답하게 한다 — HUD는 어떤 상점인지, 어떤 칸인지 몰라도 된다.
public enum LaneShopTargetKind
{
    None,   // 클릭 즉시 실행
    Ground,
    Unit,
}

// GetSlotView가 매 흐림-갱신 주기(현재 0.4초)마다 9칸씩 불리므로, 여기 담기는 값은
// 전부 상점 쪽에서 미리 캐시해둔 것을 그대로 반환해야 한다 — 이 구조체를 만드는 시점에
// 문자열을 새로 조립하면(예: 강화 레벨을 매번 보간) 분리한 의미가 없어진다.
public readonly struct LaneShopSlotView
{
    public readonly string label;
    public readonly Color color;
    public readonly bool available;
    public readonly LaneShopTargetKind targetKind;

    public LaneShopSlotView(string label, Color color, bool available,
                             LaneShopTargetKind targetKind = LaneShopTargetKind.None)
    {
        this.label = label;
        this.color = color;
        this.available = available;
        this.targetKind = targetKind;
    }

    // label이 null/빈 문자열이면 HUD는 이 칸을 빈 칸으로 취급한다(투명 처리).
    // 3칸 그리드에서 줄을 맞추려고 상점이 중간에 이 값을 끼워 넣어도 된다
    // (예: 도박소 윗줄 3칸 + Empty 2개 + 아랫줄 4칸).
    public static readonly LaneShopSlotView Empty = default;
}

public readonly struct LaneShopTarget
{
    public readonly Vector3 point;
    public readonly GameObject unit;

    LaneShopTarget(Vector3 point, GameObject unit)
    {
        this.point = point;
        this.unit = unit;
    }

    public static LaneShopTarget AtPoint(Vector3 point) => new LaneShopTarget(point, null);
    public static LaneShopTarget OnUnit(GameObject unit) => new LaneShopTarget(default, unit);
}

// 레인 건물(도움소/강화소/도박소 등) 공용 인터페이스. HUD는 이것만 알면 된다.
public interface ILaneShop
{
    int SlotCount { get; }

    LaneShopSlotView GetSlotView(int index);

    // 호버할 때만 불린다 — 문자열 조립 비용은 여기서만 든다.
    string GetSlotTooltip(int index);

    // targetKind == None인 칸은 target을 무시해도 된다(default가 넘어온다).
    // 호출 시점과 실제 실행 사이에 자원 상태가 바뀌어 실패할 수 있다 — 실패하면 false만
    // 반환하면 된다(예외 금지). 호출한 쪽(GameHud)이 대기 상태를 정리할 책임을 진다.
    bool TryUse(int index, LaneShopTarget target);
}
