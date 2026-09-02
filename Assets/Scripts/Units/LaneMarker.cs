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

    // 한 줄의 칸 수는 이제 폭이 정하지 않는다 — 사장님이 직접 정한 개수로 고정한다(2026-09-02
    // 최초 9 → 11로 조정, "칸 2개 더"). 흔함 종류 수(9)와 우연히 같았던 적이 있었을 뿐, 이 값
    // 자체가 흔함 개수를 뜻하지는 않는다 — 다음에 또 바뀌어도 흔함 가짓수와 맞출 필요 없다.
    // 폭이 나중에 또 바뀌어도 칸 수는 그대로고, 칸 하나하나가 넓어지거나 좁아질 뿐이다.
    public const int CompartmentCount = 11;

    // 양 끝은 벽에 붙이지 않고 칸 하나 너비만큼 비워둔다(사장님 지시: "양쪽 끝은 비워둬야
    // 한다") — 그만큼을 나누는 값에 얹어서 간격을 계산하면, 칸 폭은 그대로 넓어지고 양 끝에만
    // 빈 공간이 남는다. 칸 수를 9→11로 늘리며 2→1로 줄였다(늘어난 칸만큼 나누는 값이 커지는
    // 걸 상쇄해서 간격이 좁아지지 않게 하려고) — 0으로 하면 여백이 아예 없어져 사장님 지시와
    // 어긋난다.
    public const int EndMarginCompartments = 1;

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

    // 다음에 내줄 "남는 자리" 번호(로스터 밖 — 비흔함, 혹은 로스터에 없는 흔함). 유닛이
    // 떠나도 줄어들지 않는다(빈 자리 재사용은 안 함) — 지금은 필드에서 유닛이 사라지는 경로
    // (연금술 분해 등)가 이 카운터를 모르기 때문에, 안전한 쪽(자리가 늘 새것)으로 단순하게 갔다.
    // 로스터 조회(FindRosterSlot)는 이 카운터를 절대 건드리지 않는다 — 건드리면 흔함을 몇 번
    // 뽑았느냐에 따라 남는자리 배정이 밀리는, 한참 지나야 드러나는 버그가 된다(PM 지시).
    int nextFreeSlot;

    static readonly List<LaneMarker> registry = new List<LaneMarker>();

    public int LaneIndex => laneIndex;

    /// <summary>
    /// 이 레인 소유 유닛이 새로 생겨날 자리. 우리가 없으면 레인 한가운데.
    ///
    /// 흔함 등급이고 이름이 배정표에 있으면 그 이름의 고정 칸(같은 이름은 항상 같은 좌표 —
    /// 여러 마리 뽑아도 겹쳐 선다, 그 칸을 보면 몇 마리인지 안다는 게 사장님 의도다).
    /// 그 외(비흔함, 또는 흔함인데 배정표에 없는 이름 — 로스터가 낡았을 때)는 남는 자리
    /// (10·11번칸부터, 넘치면 다음 줄)로 간다.
    /// </summary>
    public Vector3 TakeSpawnPosition(UnitData unit)
    {
        if (unitPen == null) return transform.position;

        if (unit != null && unit.grade == UnitGrade.Common)
        {
            int rosterSlot = FindRosterSlot(unit.unitName);
            if (rosterSlot >= 0) return SlotPosition(unitPen, unitRowWidth, rosterSlot);

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

    /// <summary>로스터 밖 유닛에게 내줄 다음 남는 자리. 카운터를 하나 태운다 — 여기서만 태운다.</summary>
    Vector3 TakeFreeSlot()
    {
        return SlotPosition(unitPen, unitRowWidth, CommonUnitRoster.Length + nextFreeSlot++);
    }

    /// <summary>
    /// 자리 계산만 하고 카운터는 안 건드리는 순수 버전. TakeSpawnPosition/TakeFreeSlot이 이걸
    /// 감싸서 쓴다 — 자리를 소모하지 않고 미리 봐야 하는 곳(맵 생성기의 NavMesh 커버리지 확인
    /// 등)은 이쪽을 바로 쓴다.
    ///
    /// 한 줄은 CompartmentCount 칸 고정이다 — 넘치면(로스터 밖 자리가 남는 칸 수를 넘거나,
    /// 같은 로스터 칸에 여러 마리가 몰리는 게 아니라 남는 자리 쪽에서 실제로 넘칠 때) 조용히
    /// 사라지는 대신 한 칸 폭만큼 더 앞(+Z, 우리가 열린 쪽)으로 다음 줄을 놓는다. 뒷줄은 필드
    /// 쪽으로 넘어가는 열린 공간이라 칸막이가 없다.
    /// </summary>
    public static Vector3 SlotPosition(Transform unitPen, float rowWidth, int slot)
    {
        float spacing = ResolveSlotSpacing(rowWidth);

        int column = slot % CompartmentCount;
        int row = slot / CompartmentCount;

        float x = (column - (CompartmentCount - 1) * 0.5f) * spacing;
        float z = row * spacing;

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
