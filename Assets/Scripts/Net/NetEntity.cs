using Fusion;
using UnityEngine;

public enum NetEntityKind : byte
{
    None = 0,
    Unit = 1,
    Enemy = 2,
    Wisp = 3,
}

/// <summary>
/// 거울 한 장. 공용 NetworkObject 프리팹 하나로 유닛·적·위습을 다 비춘다(설계 §3).
///   호스트: 실물(Real)을 따라 위치·회전·스케일·체력을 매 틱 옮겨 적는다. 이 오브젝트 자체는 안 보인다.
///   클라:   Spawned에서 카탈로그로 프리팹을 찾아 로직을 뗀 「겉모습」(Visual)을 자식으로 세운다.
///           NetworkTransform이 이 루트를 보간해서 움직이고, 겉모습은 자식이라 따라간다.
/// </summary>
public class NetEntity : NetworkBehaviour
{
    [Networked] public byte Kind { get; set; }
    [Networked] public short CatalogIndex { get; set; }
    [Networked] public sbyte Owner { get; set; }
    [Networked] public float Hp { get; set; }
    [Networked] public float MaxHp { get; set; }
    /// <summary>호스트 실물이 공격 모션을 낼 때마다 +1. 클라는 바뀌면 겉모습에 PlayAttack.</summary>
    [Networked] public int AttackSeq { get; set; }

    public NetEntityKind EntityKind => (NetEntityKind)Kind;

    /// <summary>호스트: 따라가는 실물.</summary>
    public GameObject Real { get; set; }

    /// <summary>클라: 세운 겉모습.</summary>
    public GameObject Visual { get; private set; }

    EnemyDummy replicaEnemy;
    EnemyDummy realEnemy;
    CharacterAnimator realAnimator;
    CharacterAnimator visualAnimator;
    int seenAttackSeq;

    public static int ClientVisualCount { get; private set; }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            if (Real != null)
            {
                Real.TryGetComponent(out realEnemy);
                realAnimator = Real.GetComponentInChildren<CharacterAnimator>();
                if (realAnimator != null) realAnimator.AttackPlayed += OnRealAttack;
            }
            return;
        }

        seenAttackSeq = AttackSeq;

        Visual = NetReplicaBuilder.Build(this);
        if (Visual != null)
        {
            ClientVisualCount++;
            if (EntityKind == NetEntityKind.Enemy) Visual.TryGetComponent(out replicaEnemy);
            visualAnimator = Visual.GetComponentInChildren<CharacterAnimator>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (Real == null)
        {
            DespawnFromHost();
            return;
        }

        Transform real = Real.transform;
        transform.SetPositionAndRotation(real.position, real.rotation);
        transform.localScale = real.localScale;

        if (realEnemy != null)
        {
            Hp = realEnemy.Hp;
            MaxHp = realEnemy.MaxHp;
        }
    }

    public override void Render()
    {
        if (replicaEnemy != null) replicaEnemy.SetReplicaHp(Hp, MaxHp);

        if (!HasStateAuthority && AttackSeq != seenAttackSeq)
        {
            seenAttackSeq = AttackSeq;
            if (visualAnimator != null) visualAnimator.PlayAttack();
        }
    }

    void OnRealAttack() => AttackSeq++;

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (realAnimator != null) realAnimator.AttackPlayed -= OnRealAttack;

        if (Visual != null)
        {
            Destroy(Visual);
            ClientVisualCount--;
        }
        Visual = null;
    }

    /// <summary>호스트: 실물이 사라졌을 때 거울을 거둔다(NetLink.OnDestroy·FixedUpdateNetwork).</summary>
    public void DespawnFromHost()
    {
        if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid) return;
        if (!HasStateAuthority) return;
        Runner.Despawn(Object);
    }
}
