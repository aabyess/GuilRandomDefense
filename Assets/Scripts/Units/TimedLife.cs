using UnityEngine;

// 지속시간이 있는 소환 유닛(원작 소환수 기한) — 시간이 되면 UnitIdentity.Consume으로 인벤토리에서 빼고 지운다.
public class TimedLife : MonoBehaviour
{
    float remaining;

    public void Begin(float seconds) => remaining = seconds;

    void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining > 0f) return;
        if (TryGetComponent(out UnitIdentity identity)) identity.Consume();
        else Destroy(gameObject);
    }
}
