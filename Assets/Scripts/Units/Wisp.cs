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

    public void MarkConsumed()
    {
        IsConsumed = true;
    }
}
