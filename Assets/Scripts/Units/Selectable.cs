using System.Collections.Generic;
using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField] GameObject selectedIndicator;

    public static readonly List<Selectable> All = new List<Selectable>();

    public bool IsSelected { get; private set; }

    SelectionIndicator indicator;

    void Awake()
    {
        // 인스펙터에 수동으로 붙여둔 표시가 있으면 그걸 우선한다. 없으면 코드로 만든다 —
        // 프리팹마다 손으로 붙이면 새 유닛이 늘어날 때 빠뜨리기 쉽다.
        if (selectedIndicator == null)
            indicator = CreateIndicator();
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

        SelectionIndicator selectionIndicator = obj.GetComponent<SelectionIndicator>();
        selectionIndicator.Configure(radius, ownerId);
        return selectionIndicator;
    }
}
