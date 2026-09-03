using System.Collections.Generic;
using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField] GameObject selectedIndicator;

    public static readonly List<Selectable> All = new List<Selectable>();

    public bool IsSelected { get; private set; }

    SelectionIndicator indicator;
    AttackRangeIndicator rangeIndicator;

    void Awake()
    {
        // 인스펙터에 수동으로 붙여둔 표시가 있으면 그걸 우선한다. 없으면 코드로 만든다 —
        // 프리팹마다 손으로 붙이면 새 유닛이 늘어날 때 빠뜨리기 쉽다.
        //
        // 다만 **싸우는 것에만** 붙인다. 상점 건물과 위습도 Selectable이라 예전엔 고리가
        // 같이 생겼는데, 등급이 없어서 주인 색(파랑)으로 떨어졌고 화면에서는 정체 모를
        // 파란 원으로만 보였다(사장님: "이거 왜 있는거지"). 고리는 "무슨 등급인가"를
        // 말하는 표시라, 등급이 없는 것에는 뜻이 없다.
        if (selectedIndicator == null && GetComponent<UnitAttacker>() != null)
        {
            indicator = CreateIndicator();
            rangeIndicator = CreateRangeIndicator();
        }
    }

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
        else if (indicator != null)
            indicator.SetSelected(selected);

        // 사거리 원은 선택했을 때만. 항상 켜두면 레인이 원으로 덮인다.
        if (rangeIndicator != null) rangeIndicator.SetVisible(selected);
    }

    // 등급이 나중에 들어오는 경로가 있다(UnitSpawner는 Instantiate 뒤에 SetData를 부른다).
    // 고리를 다시 만들지 않고 색만 갈아끼운다.
    public void RefreshIndicatorColor()
    {
        if (indicator == null) return;

        UnitData data = TryGetComponent(out UnitIdentity identity) ? identity.Data : null;
        int ownerId = TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;
        indicator.SetColor(data != null ? data.grade.Color() : PlayerColors.Get(ownerId));
    }

    AttackRangeIndicator CreateRangeIndicator()
    {
        GameObject obj = new GameObject("AttackRangeIndicator",
                                        typeof(LineRenderer), typeof(AttackRangeIndicator));
        obj.transform.SetParent(transform, false);
        return obj.GetComponent<AttackRangeIndicator>();
    }

    SelectionIndicator CreateIndicator()
    {
        GameObject obj = new GameObject("SelectionIndicator", typeof(LineRenderer), typeof(SelectionIndicator));
        obj.transform.SetParent(transform, false);

        // 에이전트 반지름은 길찾기용이라 몸집과 무관하다(유닛은 0.28인데 키는 20이다).
        // 발밑 고리는 눈에 보이는 몸에 맞아야 하므로 콜라이더를 잰다.
        float radius = TryGetComponent(out Collider body)
            ? Mathf.Max(body.bounds.extents.x, body.bounds.extents.z)
            : 0.5f;
        if (radius < 0.05f) radius = 0.5f;
        int ownerId = TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;
        // 고리 색은 등급에서 온다. UnitIdentity가 없는 것(위습·건물)은 등급이 없으므로 null이 간다.
        UnitData data = TryGetComponent(out UnitIdentity identity) ? identity.Data : null;

        SelectionIndicator selectionIndicator = obj.GetComponent<SelectionIndicator>();
        selectionIndicator.Configure(radius, ownerId, data);
        return selectionIndicator;
    }
}
