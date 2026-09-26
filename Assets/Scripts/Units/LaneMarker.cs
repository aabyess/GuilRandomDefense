using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레인 섬에 붙어 "이 섬이 몇 번 레인인가"를 런타임에 알려준다.
/// 레인 좌표는 MapLayout(에디터 전용)에만 있어서, 게임 쪽에서는 이 표식으로 찾는다.
/// </summary>
public class LaneMarker : MonoBehaviour
{
    [SerializeField] int laneIndex;

    // 새로 만들어진 유닛이 처음 서는 자리. 레인 아래 상점 줄 위의 우리다 —
    // 레인 한가운데에 떨어뜨리면 적 한복판에 나오고, 플레이어가 손쓸 새 없이 맞는다.
    [SerializeField] Transform unitPen;

    // MapGenerator의 BuildUnitPen이 실제로 지은 우리 폭. SetUnitRowWidth로 안 채워지면
    // 예전 UnitPenWidth 기본값(40)으로 동작해서, 배선이 안 바뀐 씬에서도 예외 없이 돈다.
    [SerializeField] float unitRowWidth = 40f;

    // 한 줄의 칸 수는 폭이 정하지 않는다. 폭이 바뀌어도 칸 수는 그대로고, 칸 하나하나가
    // 넓어지거나 좁아질 뿐이다.
    //
    // 이력: 9(2026-09-02 최초) → 11(사장님 "칸 2개 더") → **9(2026-09-23, 원작 실측)**.
    // 원작은 흔함 줄 `1comZone`에 칸이 정확히 9개다(`1com1`~`1com9`, 각 64×64 간격 256,
    // war3map_new.j:3234~3242). 흔함 유닛 가짓수(CommonUnitRoster 9종)와도 맞아떨어진다 —
    // 예전 주석은 "우연히 같았을 뿐"이라고 했지만, 원작에서는 **칸 하나가 흔함 한 종류**다
    // (`Common_Loc[포인트값 + 플레이어×10]`이 타입으로 칸을 고른다). 우연이 아니었다.
    public const int CompartmentCount = 9;

    // 2026-09-03: 0으로 되돌렸다(원래 1 — 양 끝에 칸 하나만큼 여백을 남기려던 값). 바깥 칸막이를
    // 세워 여백을 눈에 보이게 했더니 우리 벽과 어긋나 1번·11번 칸만 두 배로 넓어지는 문제가
    // 났고(BuildUnitPenPartitions 참고), 사장님이 그 바깥 칸막이 자체를 없애라고 하셨다. 바깥
    // 칸막이 없이 11칸을 똑같게 만들려면 우리 벽 자체가 1번·11번 칸의 바깥 경계여야 하고, 그러려면
    // 여백이 정확히 0이어야 한다(0이 아니면 우리 벽과 맨 끝 칸 경계 사이에 다시 틈이 생긴다).
    // 이제 "양쪽 끝은 비워둬야 한다"는 지시는 더 안 지켜진다 — 나중 지시가 우선이라는 사장님 판단.
    public const int EndMarginCompartments = 0;

    /// <summary>
    /// 실제 칸 하나의 폭(=자리 간격). 폭을 고정 칸 수 + 양 끝 여유로 나눠서 구한다 —
    /// 칸 수를 먼저 정하고 간격을 거기서 유도하는 쪽으로 뒤집었다(예전엔 간격이 상수였고
    /// 칸 수가 폭에서 나왔다). 칸막이 두께를 뺀 통행 폭이 캐릭터 지름(현재 7.2)보다 좁아지면
    /// 몸통이 칸막이를 뚫고 나온다(2026-09-02에 SlotSpacing=8일 때 실제로 이렇게 걸렸다) —
    /// 유닛 굵기를 바꾸는 쪽(현재는 프리팹 스케일뿐, 코드로 된 지름 상수는 아직 없다)을
    /// 손보면 이 값도 다시 확인할 것.
    /// </summary>
    public static float ResolveSlotSpacing(float rowWidth)
    {
        return rowWidth / (CompartmentCount + EndMarginCompartments * 2);
    }

    // 흔함 유닛 자리 배정표(2026-09-02, 사장님 지시) — 왼쪽부터 1번. 이름 문자열은 이 배열
    // 하나에만 두고 다른 곳에는 안 흩뿌린다 — 순서가 바뀌면 여기만 고치면 되게.
    static readonly string[] CommonUnitRoster =
    {
        "최상호", "노태현", "양재모", "강주혁", "강재규", "박민석", "문필환", "박민수", "임장혁",
    };

    // 흔함인데 로스터에 없는 이름을 경고할 때, 스폰마다 다시 찍으면 진짜 경고가 묻힌다 —
    // 이름 하나당 한 번만 남긴다. 로스터가 낡았다는 뜻이라 지우면 안 되는 경고다.
    static readonly HashSet<string> warnedMissingRosterNames = new HashSet<string>();

    // (2026-09-23) 고리 분산에 쓰던 값 넷 — AssumedPenWallThickness·AssumedCharacterDiameter·
    // RingCapacity·rosterOccupancy — 은 같은 이름을 겹쳐 세우기로 하면서 전부 지웠다.
    // 쓰는 곳이 하나도 안 남았다(RosterSlotPosition 주석 참고).

    // 다음에 내줄 "남는 자리" 번호(로스터 밖 — 비흔함, 혹은 로스터에 없는 흔함). 유닛이
    // 떠나도 줄어들지 않는다(빈 자리 재사용은 안 함) — 지금은 필드에서 유닛이 사라지는 경로
    // (연금술 분해 등)가 이 카운터를 모르기 때문에, 안전한 쪽(자리가 늘 새것)으로 단순하게 갔다.
    // 로스터 조회(FindRosterSlot)는 이 카운터를 절대 건드리지 않는다 — 건드리면 흔함을 몇 번
    // 뽑았느냐에 따라 남는자리 배정이 밀리는, 한참 지나야 드러나는 버그가 된다(PM 지시).
    int nextFreeSlot;

    static readonly List<LaneMarker> registry = new List<LaneMarker>();

    public int LaneIndex => laneIndex;

    // LaneMarker는 레인 섬 오브젝트 자신에 붙어 있다(MapGenerator.Generate) — 그래서 이 자리
    // 자체가 곧 레인 기하학적 한가운데다. MapLayout.Lanes[i].center와 같은 값이지만, MapLayout은
    // 에디터 전용이라 런타임 스크립트가 못 읽는다 — 여기서 다시 노출해야 하는 이유다.
    public Vector3 LaneCenter => transform.position;

    /// <summary>새 유닛이 생기는 우리(TakeSpawnPosition의 자리). 시작 카메라가 섬과 함께 이걸 화면에 담는다(RtsCameraController).</summary>
    public Transform UnitPen => unitPen;

    /// <summary>
    /// 이 레인 소유 유닛이 새로 생겨날 자리. 우리가 없으면 레인 한가운데.
    ///
    /// 흔함 등급이고 이름이 배정표에 있으면 그 이름의 고정 칸 **한가운데** — 여러 마리 뽑으면 **같은 점에 겹쳐 선다**
    /// (2026-09-23 사장님 「겹치게 해줘야 할 듯」 + 원작 Common_Loc이 유닛 타입으로만 인덱싱, 7525cacc — 아래 RosterSlotPosition 주석).
    /// ⚠️ 예전엔 여기에 「육각 고리로 벌어져 선다」고 적혀 있었는데 고리 분산을 걷어낸 뒤에도 이 요약만 남아 있었다(09-24 실측으로 발견:
    ///    흔함_박민수 2기가 둘 다 (−1432.42, 1239.45)).
    /// 그 외(비흔함, 또는 흔함인데 배정표에 없는 이름 — 로스터가 낡았을 때)는 남는 자리
    /// (10·11번칸부터, 넘치면 다음 줄)로 간다.
    /// </summary>
    public Vector3 TakeSpawnPosition(UnitData unit)
    {
        if (unitPen == null) return transform.position;

        // 시스템 유닛(해적단 퀘스트 토큰 등)은 grade가 흔함이어도 로스터 배정표에 있을 리
        // 없다 — 걸러두지 않으면 매번 "로스터가 낡았다"는 진짜 경고와 구분 안 되는 가짜
        // 경고가 찍힌다(WIRING_AUDIT.md §⑧). 남는 자리로 보내는 동작 자체는 그대로 맞다.
        if (unit != null && unit.grade == UnitGrade.Common && !unit.isSystemUnit)
        {
            int rosterSlot = FindRosterSlot(unit.unitName);
            if (rosterSlot >= 0) return RosterSlotPosition(rosterSlot);

            if (warnedMissingRosterNames.Add(unit.unitName))
                Debug.LogWarning($"LaneMarker: 흔함 유닛 '{unit.unitName}'이 자리 배정표(CommonUnitRoster)에 없습니다 — " +
                                 "남는 자리로 보냅니다. 배정표를 갱신하세요.");
        }

        return TakeFreeSlot();
    }

    /// <summary>배정표에서 이 이름의 고정 칸 번호를 찾는다. 순수 조회 — 카운터를 안 건드린다.</summary>
    static int FindRosterSlot(string unitName)
    {
        return System.Array.IndexOf(CommonUnitRoster, unitName);
    }

    /// <summary>
    /// 이 이름이 쓰는 **고정 칸의 한가운데**. 같은 이름은 몇 기가 나오든 **같은 자리에 겹쳐 선다.**
    ///
    /// 2026-09-23에 고리 분산(TakeRosterRingSlot)을 걷어냈다. 근거 둘이 같은 곳을 가리킨다:
    ///  · 사장님 지시 — "흔함 유닛 뽑았을 때 겹치게 해줘야 할 듯"
    ///  · 원작 — 흔함 자리가 <c>Common_Loc[포인트값 + 플레이어×10]</c>, 즉 **유닛 타입으로만
    ///    인덱싱되는 고정 슬롯**이라 같은 타입은 같은 점에 포개 선다(개체마다 자리를 벌리지 않는다).
    /// 겹쳐도 서로 안 밀리는 건 UnitMover가 길찾기 회피를 꺼 두기 때문이다(Rigidbody는 원래 kinematic).
    ///
    /// 카운터(rosterOccupancy)도 같이 없앴다 — 몇 번째로 섰는지가 자리에 영향을 주지 않으므로
    /// 셀 이유가 없다. 이제 이 함수는 순수 계산이다.
    /// </summary>
    Vector3 RosterSlotPosition(int rosterSlot)
    {
        return SlotPosition(unitPen, unitRowWidth, rosterSlot);
    }

    /// <summary>로스터 밖 유닛에게 내줄 다음 남는 자리. 카운터를 하나 태운다 — 여기서만 태운다.</summary>
    Vector3 TakeFreeSlot()
    {
        return FreeSlotPosition(nextFreeSlot++);
    }

    // 2026-09-26 베타 피드백 「C 누르면 위치가 이상해진다」 — 정렬(C)이 누를 때마다 남는 자리 카운터를 태워서
    // 같은 유닛이 **누를 때마다 다른 칸**으로 갔고, SlotPosition의 줄 번갈이(칸 안 ↔ 레인 아래 끝) 때문에
    // 앞줄(적 경로 쪽)과 칸 안을 오갔다. 이제 ① 남는 자리는 **늘 칸 안 한 줄**(9칸을 돌아가며, 넘치면 겹쳐 선다 —
    // 같은 이름 흔함도 겹쳐 서는 이 게임의 규칙 안) ② 한 번 받은 자리는 그 유닛이 계속 쓴다.
    readonly Dictionary<UnitIdentity, int> assignedFreeSlots = new Dictionary<UnitIdentity, int>();

    Vector3 FreeSlotPosition(int freeIndex)
    {
        // 칸 안 줄(row 1)만 쓴다 — 로스터 9칸 바로 뒤. 앞줄(row 0)은 흔함 고정 칸이라 비운다.
        return SlotPosition(unitPen, unitRowWidth, CompartmentCount + (freeIndex % CompartmentCount));
    }

    /// <summary>정렬(C)이 보낼 자리. 흔함(로스터 이름)은 자기 고정 칸, 그 외는 이 유닛이 처음 받은 남는 자리를 계속 쓴다.</summary>
    public Vector3 PenPositionFor(UnitIdentity unit)
    {
        if (unitPen == null) return transform.position;

        UnitData data = unit != null ? unit.Data : null;
        if (data != null && data.grade == UnitGrade.Common && !data.isSystemUnit)
        {
            int rosterSlot = FindRosterSlot(data.unitName);
            if (rosterSlot >= 0) return RosterSlotPosition(rosterSlot);
        }

        if (unit == null) return TakeFreeSlot();

        // 파괴된 유닛 열쇠는 가끔 치운다(Unity null이라 == null이 참).
        if (assignedFreeSlots.Count > 64)
        {
            List<UnitIdentity> dead = new List<UnitIdentity>();
            foreach (UnitIdentity key in assignedFreeSlots.Keys) if (key == null) dead.Add(key);
            foreach (UnitIdentity key in dead) assignedFreeSlots.Remove(key);
        }

        if (!assignedFreeSlots.TryGetValue(unit, out int index))
        {
            index = nextFreeSlot++;
            assignedFreeSlots[unit] = index;
        }
        return FreeSlotPosition(index);
    }

    /// <summary>
    /// 자리 계산만 하고 카운터는 안 건드리는 순수 버전. TakeSpawnPosition/TakeFreeSlot이 이걸
    /// 감싸서 쓴다 — 자리를 소모하지 않고 미리 봐야 하는 곳(맵 생성기의 NavMesh 커버리지 확인
    /// 등)은 이쪽을 바로 쓴다.
    ///
    /// 한 줄은 CompartmentCount 칸 고정이다 — 넘치면(로스터 밖 자리가 남는 칸 수를 넘거나,
    /// 같은 로스터 칸에 여러 마리가 몰리는 게 아니라 남는 자리 쪽에서 실제로 넘칠 때) 조용히
    /// 사라지는 대신 한 칸 폭만큼 **뒤(칸 안)**에 다음 줄을 놓고, 그 뒤로는 두 줄을 번갈아 쓴다
    /// (2026-09-25 — 기준점이 레인 아래 끝으로 올라가 앞줄이 적 경로 위가 됐다. SlotPosition 본문 주석).
    /// </summary>
    public static Vector3 SlotPosition(Transform unitPen, float rowWidth, int slot)
    {
        float spacing = ResolveSlotSpacing(rowWidth);

        int column = slot % CompartmentCount;
        int row = slot / CompartmentCount;

        float x = (column - (CompartmentCount - 1) * 0.5f) * spacing;
        // 🔴 2026-09-25: 다음 줄은 앞(+Z, 필드 쪽)이 아니라 **뒤 — 칸 안**으로 놓는다. 기준점(unitPen)이 원작처럼 칸 위 225
        //    (레인 아래 끝)로 올라가서(MapLayout.CommonStandOffset), 한 칸 폭(≈63) 앞은 **적 경로(56.4) 너머 흙길 위**다.
        //    칸이 9개·로스터가 9명이라 로스터 밖 유닛은 처음부터 이 줄을 쓴다. 칸 안은 한 줄 깊이뿐이라 두 줄을 번갈아 쓴다 —
        //    같은 이름 흔함도 겹쳐 서므로(RosterSlotPosition) 겹침은 이 게임의 규칙 안이다.
        float z = -(row % 2) * spacing;

        return unitPen.position + unitPen.right * x + unitPen.forward * z;
    }

    /// <summary>
    /// 벽으로 막힌 첫 줄(칸막이가 실제로 세워진 CompartmentCount칸)의 자리들. 소모하지 않는다 —
    /// NavMesh가 실제로 이 칸들에 깔렸는지 확인할 때만 쓴다.
    /// </summary>
    public IEnumerable<Vector3> FirstRowSlotPositions()
    {
        if (unitPen == null) yield break;

        for (int slot = 0; slot < CompartmentCount; slot++)
            yield return SlotPosition(unitPen, unitRowWidth, slot);
    }

    public void SetLaneIndex(int index)
    {
        laneIndex = index;
    }

    public void SetUnitPen(Transform pen)
    {
        unitPen = pen;
    }

    /// <summary>BuildUnitPen이 실제로 지은 우리 폭. 0 이하로 부르면 무시하고 기본값을 유지한다.</summary>
    public void SetUnitRowWidth(float width)
    {
        if (width > 0f) unitRowWidth = width;
    }

    void OnEnable() => registry.Add(this);
    void OnDisable() => registry.Remove(this);

    public static LaneMarker Get(int laneIndex)
    {
        foreach (LaneMarker marker in registry)
            if (marker.laneIndex == laneIndex)
                return marker;

        return null;
    }
}
