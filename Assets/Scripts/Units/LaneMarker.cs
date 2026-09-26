using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레인 섬에 붙어 "이 섬이 몇 번 레인인가"를 런타임에 알려준다.
/// 레인 좌표는 MapLayout(에디터 전용)에만 있어서, 게임 쪽에서는 이 표식으로 찾는다.
/// </summary>
public class LaneMarker : MonoBehaviour
{
    [SerializeField] int laneIndex;

    // 흔함이 서는 우리(칸 아홉 줄)의 기준점 = 칸 한가운데 줄(2026-09-26, MapLayout.CommonStandOffset 0).
    //    흔함 아닌 유닛은 여기가 아니라 레인 가운데에 선다(CenterSlotPosition).
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

    // 로스터 조회(FindRosterSlot)는 레인 가운데 자리 배정을 절대 건드리지 않는다 — 건드리면 흔함을 몇 번
    // 뽑았느냐에 따라 흔함 아닌 유닛 자리가 밀리는, 한참 지나야 드러나는 버그가 된다(PM 지시).

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
    /// 그 외(비흔함, 또는 흔함인데 배정표에 없는 이름 — 로스터가 낡았을 때)는 **레인 가운데**(CenterSlotPosition)로 간다
    /// (2026-09-26 사장님 「조합하거나 흔함 제외 뽑기로 나온 유닛들은 레인 가운데에 배치」 — 원작도 PlayZoneLOC = 순찰 사각형 한가운데).
    /// </summary>
    public Vector3 TakeSpawnPosition(UnitData unit)
    {

        // 시스템 유닛(해적단 퀘스트 토큰 등)은 grade가 흔함이어도 로스터 배정표에 있을 리
        // 없다 — 걸러두지 않으면 매번 "로스터가 낡았다"는 진짜 경고와 구분 안 되는 가짜
        // 경고가 찍힌다(WIRING_AUDIT.md §⑧). 남는 자리로 보내는 동작 자체는 그대로 맞다.
        if (unitPen != null && unit != null && unit.grade == UnitGrade.Common && !unit.isSystemUnit)
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

    // ── 흔함 아닌 유닛 = 레인 가운데(2026-09-26 사장님) ──
    //    한 점에 포개면 하나씩 클릭하기 어려워 **육각 고리**로 몸 하나 간격(CenterSpacing)씩 퍼뜨린다 — 0번 = 한가운데,
    //    1고리 6자리, 2고리 12자리 …. 한 번 받은 자리는 그 유닛이 계속 쓴다(C 정렬 베타 피드백 fa25e228과 같은 규칙).
    //    ① 새로 나올 때: TakeSpawnPosition이 빈 자리를 **예약**(reserved)하고, UnitSpawner가 그 자리에 세운 개체를
    //       ClaimSpawnSlot으로 붙인다(자리로 짝짓는다 — 호출부 열 곳을 안 고쳐도 된다).
    //    ② 유닛이 죽거나 조합 재료로 사라지면(Unity null) 그 자리를 다시 쓴다 — 안 그러면 한 판에 수백 기가 나와 고리가 레인 밖까지 커진다.
    //       예약만 되고 30초 안에 아무도 안 붙은 자리(소환 실패·NavMesh 보정으로 딴 데 섰을 때)도 풀어 준다.
    public const float CenterSpacing = 14f;   // 유닛 몸 ≈ 14(UnitSpawner 회피 주석 실측)
    const float ReservationSeconds = 30f;

    readonly Dictionary<UnitIdentity, int> assignedFreeSlots = new Dictionary<UnitIdentity, int>();
    readonly Dictionary<int, float> reservedSlots = new Dictionary<int, float>();   // 자리 번호 → 예약 시각

    /// <summary>육각 격자 고리 index번째 점의 가운데로부터 오프셋(xz). 0 = 가운데, 1~6 = 첫 고리, 7~18 = 둘째 고리 ….</summary>
    public static Vector3 HexRingOffset(int index, float spacing)
    {
        if (index <= 0) return Vector3.zero;
        int ring = 1, first = 1;
        while (index >= first + 6 * ring) { first += 6 * ring; ring++; }
        int k = index - first;
        int side = k / ring, step = k % ring;
        float a0 = side * 60f * Mathf.Deg2Rad, a1 = (side + 1) * 60f * Mathf.Deg2Rad;
        Vector3 c0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (ring * spacing);
        Vector3 c1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (ring * spacing);
        return Vector3.Lerp(c0, c1, (float)step / ring);
    }

    /// <summary>레인 가운데 index번째 자리. 높이는 우리 기준점(섬 윗면)에 맞춘다 — 섬 오브젝트 중심은 윗면보다 낮다.</summary>
    public Vector3 CenterSlotPosition(int index)
    {
        Vector3 p = LaneCenter + HexRingOffset(index, CenterSpacing);
        if (unitPen != null) p.y = unitPen.position.y;
        return p;
    }

    int LowestFreeCenterIndex()
    {
        var used = new HashSet<int>();
        var dead = new List<UnitIdentity>();
        foreach (KeyValuePair<UnitIdentity, int> kv in assignedFreeSlots)
        {
            if (kv.Key == null) dead.Add(kv.Key); else used.Add(kv.Value);
        }
        foreach (UnitIdentity key in dead) assignedFreeSlots.Remove(key);
        var expired = new List<int>();
        foreach (KeyValuePair<int, float> kv in reservedSlots)
        {
            if (Time.time - kv.Value > ReservationSeconds) expired.Add(kv.Key); else used.Add(kv.Key);
        }
        foreach (int i in expired) reservedSlots.Remove(i);
        int index = 0;
        while (used.Contains(index)) index++;
        return index;
    }

    /// <summary>흔함 아닌 유닛에게 내줄 레인 가운데 빈 자리를 예약하고 돌려준다.</summary>
    Vector3 TakeFreeSlot()
    {
        int index = LowestFreeCenterIndex();
        reservedSlots[index] = Time.time;
        return CenterSlotPosition(index);
    }

    /// <summary>UnitSpawner가 방금 세운 개체를 그 자리의 예약에 붙인다. 예약된 자리 근처(몸 반 개)가 아니면 아무것도 안 한다.</summary>
    public void ClaimSpawnSlot(UnitIdentity unit, Vector3 spawnedAt)
    {
        if (unit == null || assignedFreeSlots.ContainsKey(unit) || reservedSlots.Count == 0) return;
        int best = -1;
        float bestDistance = CenterSpacing * 0.5f;
        foreach (int index in reservedSlots.Keys)
        {
            Vector3 p = CenterSlotPosition(index);
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(spawnedAt.x, spawnedAt.z));
            if (d < bestDistance) { bestDistance = d; best = index; }
        }
        if (best < 0) return;
        reservedSlots.Remove(best);
        assignedFreeSlots[unit] = best;
    }

    /// <summary>정렬(C)이 보낼 자리. 흔함(로스터 이름)은 자기 고정 칸, 그 외는 이 유닛이 처음 받은 레인 가운데 자리를 계속 쓴다.</summary>
    public Vector3 PenPositionFor(UnitIdentity unit)
    {
        UnitData data = unit != null ? unit.Data : null;
        if (unitPen != null && data != null && data.grade == UnitGrade.Common && !data.isSystemUnit)
        {
            int rosterSlot = FindRosterSlot(data.unitName);
            if (rosterSlot >= 0) return RosterSlotPosition(rosterSlot);
        }

        if (unit == null) return TakeFreeSlot();

        if (!assignedFreeSlots.TryGetValue(unit, out int index))
        {
            index = LowestFreeCenterIndex();
            assignedFreeSlots[unit] = index;
        }
        return CenterSlotPosition(index);
    }

    /// <summary>
    /// 자리 계산만 하고 카운터는 안 건드리는 순수 버전. TakeSpawnPosition/TakeFreeSlot이 이걸
    /// 감싸서 쓴다 — 자리를 소모하지 않고 미리 봐야 하는 곳(맵 생성기의 NavMesh 커버리지 확인
    /// 등)은 이쪽을 바로 쓴다.
    ///
    /// 한 줄은 CompartmentCount 칸 고정이다. 2026-09-26부터 흔함 아닌 유닛은 레인 가운데로 가서 이 줄을 안 쓰고,
    /// 흔함은 로스터 9칸(slot 0~8)만 쓴다 — slot 9 이상(둘째 줄)은 이제 부르는 곳이 없다.
    /// </summary>
    public static Vector3 SlotPosition(Transform unitPen, float rowWidth, int slot)
    {
        float spacing = ResolveSlotSpacing(rowWidth);

        int column = slot % CompartmentCount;
        int row = slot / CompartmentCount;

        float x = (column - (CompartmentCount - 1) * 0.5f) * spacing;
        // 넘치는 줄은 뒤(−Z)로 번갈아 놓는다(2026-09-25). 09-26부터 기준점이 칸 한가운데라 slot 9 이상은 쓰지 않는다(위 주석).
        float z = -(row % 2) * spacing;

        return unitPen.position + unitPen.right * x + unitPen.forward * z;
    }

    /// <summary>
    /// 벽으로 막힌 첫 줄(칸막이가 실제로 세워진 CompartmentCount칸)의 자리들. 소모하지 않는다 —
    /// NavMesh가 실제로 이 칸들에 깔렸는지 확인할 때만 쓴다.
    /// </summary>
    public IEnumerable<Vector3> FreeRowSlotPositions()
    {
        // 흔함 아닌 유닛이 서는 레인 가운데 자리(가운데 + 첫 고리 6) — 시작 카메라가 이 자리까지 담는다(RtsCameraController.TryGetPenBounds).
        for (int i = 0; i < 7; i++)
            yield return CenterSlotPosition(i);
    }

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
