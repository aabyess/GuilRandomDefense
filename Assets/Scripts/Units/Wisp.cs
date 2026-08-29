using UnityEngine;

// 위습: 플레이어가 조종하는 유닛. 선택/이동은 기존 Selectable·UnitMover·OwnedByPlayer를 그대로 재사용한다.
public class Wisp : MonoBehaviour
{
    [SerializeField] WispData data;

    public WispData Data => data;

    public void SetData(WispData wispData)
    {
        data = wispData;
    }
}
