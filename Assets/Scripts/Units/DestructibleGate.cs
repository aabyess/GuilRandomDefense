using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부술 수 있는 문(정의문). 체력이 0이 되면 길이 열린다.
/// 적(EnemyDummy)과 달리 사라지지 않고 그 자리에 부서진 채로 남아, 지나갈 수 있게만 바뀐다.
/// </summary>
public class DestructibleGate : MonoBehaviour
{
    [SerializeField] float maxHp = 5000f;
    [SerializeField] Transform openTarget;   // 부서지면 이 위치로 내려앉는다(비면 아래로 가라앉힘)

    public static readonly List<DestructibleGate> Active = new List<DestructibleGate>();

    float hp;
    bool isBroken;

    public float Hp => hp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
    public bool IsBroken => isBroken;

    void Awake()
    {
        hp = maxHp;
    }

    void OnEnable()
    {
        if (!isBroken) Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    public void TakeDamage(float amount)
    {
        if (isBroken) return;

        hp -= amount;
        if (hp > 0f) return;

        // 보상·상태 변경은 서버가 정한다. 클라이언트가 각자 부수면 이벤트가 여러 번 발생한다.
        if (!GameAuthority.IsServer) return;

        Break();
    }

    void Break()
    {
        isBroken = true;
        hp = 0f;
        Active.Remove(this);

        // 콜라이더만 꺼도 길은 열리지만, NavMesh는 굽는 시점의 지오메트리를 쓴다.
        // 문을 아래로 내려 실제로 비켜줘야 이미 구워진 NavMesh에서도 통과가 된다.
        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        transform.position = openTarget != null
            ? openTarget.position
            : transform.position + Vector3.down * (transform.localScale.y + 1f);

        Debug.Log($"{name}이(가) 부서졌습니다.");
    }
}
