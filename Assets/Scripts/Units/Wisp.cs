using UnityEngine;

// 위습: 플레이어가 조종하는 유닛. 선택/이동은 기존 Selectable·UnitMover·OwnedByPlayer를 그대로 재사용한다.
public class Wisp : MonoBehaviour
{
    [SerializeField] WispData data;

    public WispData Data => data;

    // Destroy는 프레임 끝에야 처리되므로, 같은 프레임에 여러 트리거에 겹쳐 들어가도
    // 중복 소모되지 않도록 확정 시점에 바로 마킹한다 (EnemyDummy.isDead와 같은 패턴).
    public bool IsConsumed { get; private set; }

    public void SetData(WispData wispData)
    {
        data = wispData;
    }

    // 위습이 포탈에 소모될 때 알린다. 소모하는 쪽(UnitPortal/ResourcePortal)이 서로를 몰라도
    // 되게 하려고 여기서 한 곳으로 모은다 — 예: 백수생활 특수 칸 3개가 서로 참조 없이
    // "같은 위습 종류가 소모됐다"만 각자 구독해서 안다(InterludeGate 참고).
    public static event System.Action<Wisp> OnConsumed;

    public void MarkConsumed()
    {
        IsConsumed = true;
        OnConsumed?.Invoke(this);
    }
}
