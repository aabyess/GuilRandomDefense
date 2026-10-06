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
    // 지점을 찍는 칸이 미치는 반경(게임 단위). 0보다 크면 HUD가 찍는 동안 커서에 범위 원을 그린다.
    public readonly float targetRadius;
    // 이 칸의 단축키. '\0'이면 HUD가 칸 위치로 워크3 격자 키(Q W E R / A S D F / Z X C V)를 준다.
    public readonly char hotkey;
    // 워크3 쿨다운 표시(사장님 10-04): 남은 초·전체 초. cooldownTotal이 0이거나 남은 게 0이면 덮개 없음.
    // HUD가 받은 순간 「끝나는 시각」으로 바꿔 매 프레임 비율을 돌린다(0.4초 갱신 사이를 부드럽게) — 상점은 평소처럼 캐시 값만 넘기면 된다.
    public readonly float cooldownRemaining;
    public readonly float cooldownTotal;

    public LaneShopSlotView(string label, Color color, bool available,
                             LaneShopTargetKind targetKind = LaneShopTargetKind.None, float targetRadius = 0f,
                             char hotkey = '\0', float cooldownRemaining = 0f, float cooldownTotal = 0f)
    {
        this.label = label;
        this.color = color;
        this.available = available;
        this.targetKind = targetKind;
        this.targetRadius = targetRadius;
        this.hotkey = hotkey;
        this.cooldownRemaining = cooldownRemaining;
        this.cooldownTotal = cooldownTotal;
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
// 한 건물이 칸 묶음(쪽)을 바꿔 보여 주는 상점(도박소 ↔ 해적단 퀘스트). 쪽은 **화면(클라)마다 따로**라 서버는 모른다 —
// 그래서 HUD가 칸을 누르면 ① TryChangePage로 쪽 넘김 칸인지 먼저 묻고(맞으면 네트워크로 안 보낸다)
// ② 아니면 ToNetSlot으로 「쪽과 상관없는 절대 번호」로 바꿔 TryUse·RPC에 싣는다. TryUse(절대 번호)는 서버·호스트·싱글 모두 같다.
// GetSlotView·GetSlotTooltip의 index는 화면 칸 번호(현재 쪽 기준)다.
public interface IPagedLaneShop : ILaneShop
{
    bool TryChangePage(int visibleIndex);
    int ToNetSlot(int visibleIndex);
    void ResetPage();
}

public interface ILaneShop
{
    int SlotCount { get; }

    // 못 쓰는 칸(available == false)을 눌렀을 때 띄울 이유(사장님 10-06 「눌렀는데 아무 반응 없으면 먹통으로 보인다」).
    // 판정만 하고 아무것도 바꾸지 않는다. 모르면 null — HUD가 일반 문구를 띄운다.
    string GetUnavailableReason(int index) => null;

    LaneShopSlotView GetSlotView(int index);

    // 호버할 때만 불린다 — 문자열 조립 비용은 여기서만 든다.
    string GetSlotTooltip(int index);

    // targetKind == None인 칸은 target을 무시해도 된다(default가 넘어온다).
    // 호출 시점과 실제 실행 사이에 자원 상태가 바뀌어 실패할 수 있다 — 실패하면 false만
    // 반환하면 된다(예외 금지). 호출한 쪽(GameHud)이 대기 상태를 정리할 책임을 진다.
    //
    // failReason: false일 때 화면에 띄울 문구. 상점이 "왜 안 되는지" 이미 알고 있는 경우
    // (골드 부족·재고 없음·조건 미달 등)만 채우고, 그 자리에서 직접 PlayerNotification을
    // 부르지 않는다 — 표시는 호출부(GameHud) 몫으로 한 곳에 모아둔다(안 그러면 상점이
    // 알림을 띄우고 GameHud가 또 일반 문구를 띄워 두 번 뜬다). null이면 "플레이어가 봐도
    // 고칠 수 없는 사유"(배선 오류 등)라는 뜻 — 호출부가 일반 문구로 대신한다.
    // true일 때는 무시된다.
    bool TryUse(int index, LaneShopTarget target, out string failReason);
}
