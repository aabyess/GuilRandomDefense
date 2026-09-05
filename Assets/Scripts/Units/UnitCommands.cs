using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 유닛에 내리는 명령. 단축키(V·H)와 우하단 명령 카드가 같은 함수를 부른다 —
/// 두 곳에 따로 구현하면 언젠가 한쪽만 고쳐진다.
/// </summary>
public static class UnitCommands
{
    // 모인 유닛들이 한 점에 겹치면 서로 밀어내느라 흩어진다. 몸 굵기만큼 벌려 놓는다.
    const float GatherSpacing = 5f;

    /// <summary>
    /// 선택한 유닛과 <b>같은 이름의 내 유닛 전부</b>를 그 자리로 불러 모은다.
    /// 걸어오는 게 아니라 옮겨 세운다 — 원작의 모으기가 그렇고, 레인을 가로질러 걸어오면
    /// 그 사이 적한테 다 뜯긴다.
    /// </summary>
    public static int Gather(IReadOnlyList<Selectable> selection)
    {
        UnitIdentity anchor = FirstIdentity(selection);
        if (anchor == null || anchor.Data == null) return 0;

        Vector3 center = anchor.transform.position;
        string wanted = anchor.Data.unitName;
        int owner = anchor.TryGetComponent(out OwnedByPlayer anchorOwner)
            ? anchorOwner.OwnerId
            : LocalPlayer.LocalPlayerId;

        List<UnitIdentity> crowd = new List<UnitIdentity>();
        foreach (UnitIdentity unit in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None))
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.unitName != wanted) continue;
            if (unit.TryGetComponent(out OwnedByPlayer other) && other.OwnerId != owner) continue;

            crowd.Add(unit);
        }

        int moved = 0;
        for (int i = 0; i < crowd.Count; i++)
            if (Place(crowd[i], center, i, crowd.Count)) moved++;

        // SnapTo가 NavMesh에 못 올리면 조용히 false만 돌려준다 — 예전엔 이 값을 안 봐서
        // 일부가 못 옮겨져도 "전원 모음 성공"으로 보였다. 실제 성공 개수를 돌려줘야
        // 호출부(SelectionManager)의 로그가 시도 개수가 아니라 진짜 결과를 말한다
        // (PM 지시, 2026-09-05, 버그 #8).
        if (moved < crowd.Count)
            PlayerNotification.Show(owner, $"{crowd.Count - moved}기는 자리가 없어 모이지 못했습니다.");

        return moved;
    }

    // 가운데부터 바깥으로 고리를 넓혀가며 세운다. 한 고리에 여섯씩 — 육각형으로 채우면
    // 같은 간격을 지키면서 가장 촘촘하다.
    static bool Place(UnitIdentity unit, Vector3 center, int index, int total)
    {
        Vector3 spot = center;

        if (index > 0)
        {
            int ring = 1;
            int placed = 1;
            while (placed + ring * 6 <= index) { placed += ring * 6; ring++; }

            int withinRing = index - placed;
            float angle = 360f / (ring * 6) * withinRing;
            spot = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (GatherSpacing * ring);
        }

        if (unit.TryGetComponent(out UnitCombat combat)) return combat.SnapTo(spot);

        unit.transform.position = spot;
        return true;
    }

    /// <summary>
    /// C 키. 선택한 유닛들을 각자 주인 레인의 유닛 우리(새로 뽑힌 유닛이 서는 벽쪽 가로 줄)로
    /// 보낸다. LaneMarker.TakeSpawnPosition을 그대로 써서 갓 나온 유닛과 같은 계산을 탄다 —
    /// 흔함이면 이름이 지정한 고정 칸으로, 아니면 남는 자리로 간다. 자리 계산을 따로 두지 않는다.
    /// </summary>
    public static int SendToPen(IReadOnlyList<Selectable> selection)
    {
        int moved = 0;
        int attempted = 0;
        int lastFailedOwner = -1;

        foreach (Selectable selected in selection)
        {
            if (selected == null) continue;
            if (!selected.TryGetComponent(out UnitCombat combat)) continue;

            int owner = selected.TryGetComponent(out OwnedByPlayer ownedBy)
                ? ownedBy.OwnerId
                : LocalPlayer.LocalPlayerId;

            LaneMarker lane = LaneMarker.Get(owner);
            if (lane == null) continue;

            UnitData unitData = selected.TryGetComponent(out UnitIdentity identity) ? identity.Data : null;

            attempted++;
            // SnapTo가 NavMesh에 못 올리면 조용히 false만 돌려준다 — Gather와 같은 이유로
            // 실제 성공 개수만 센다(PM 지시, 2026-09-05, 버그 #8).
            if (combat.SnapTo(lane.TakeSpawnPosition(unitData))) moved++;
            else lastFailedOwner = owner;
        }

        if (moved < attempted)
            PlayerNotification.Show(lastFailedOwner, $"{attempted - moved}기는 자리가 없어 우리로 보내지 못했습니다.");

        return moved;
    }

    /// <summary>H 키. 선택한 유닛들을 그 자리에 못박거나, 이미 박혀 있으면 푼다.</summary>
    public static int ToggleHold(IReadOnlyList<Selectable> selection)
    {
        List<UnitCombat> units = new List<UnitCombat>();
        foreach (Selectable selected in selection)
            if (selected != null && selected.TryGetComponent(out UnitCombat combat))
                units.Add(combat);

        if (units.Count == 0) return 0;

        // 하나라도 안 박혀 있으면 전부 박는다. 섞여 있을 때 절반만 바뀌면 무엇이 켜진 상태인지
        // 알 수 없어진다.
        bool hold = false;
        foreach (UnitCombat combat in units)
            if (!combat.IsHolding) { hold = true; break; }

        foreach (UnitCombat combat in units) combat.SetHold(hold);
        return units.Count;
    }

    static UnitIdentity FirstIdentity(IReadOnlyList<Selectable> selection)
    {
        foreach (Selectable selected in selection)
            if (selected != null && selected.TryGetComponent(out UnitIdentity identity))
                return identity;

        return null;
    }
}
