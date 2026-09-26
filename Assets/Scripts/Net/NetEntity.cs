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
    /// <summary>실물에 붙은 희귀함 리롤 능력(카탈로그 번호+1, 0=없음). 도박 성공 때 런타임에 붙어서 매 틱 본다.</summary>
    [Networked] public short RerollAbility { get; set; }
    // 정보칸 숫자(PM 09-26: 친구 화면에 공격력·사거리·공격속도가 「-」였다 — 겉모습은 UnitAttacker를 떼어 낸다).
    // 호스트 실물의 **실제 값**(강화·특성 반영)을 싣는다. 바뀔 때만 쓴다.
    [Networked] public float AttackDamage { get; set; }
    [Networked] public float AttackRange { get; set; }
    [Networked] public float AttackInterval { get; set; }
    // 적: 방깎·이감·난이도가 반영된 실효 방어력과 이동속도(겉모습은 에셋 기준값밖에 모른다).
    [Networked] public float EnemyArmor { get; set; }
    [Networked] public float EnemyMoveSpeed { get; set; }

    public NetEntityKind EntityKind => (NetEntityKind)Kind;

    /// <summary>호스트: 따라가는 실물.</summary>
    public GameObject Real { get; set; }

    /// <summary>클라: 세운 겉모습.</summary>
    public GameObject Visual { get; private set; }

    EnemyDummy replicaEnemy;
    EnemyDummy realEnemy;
    UnitAttacker realAttacker;
    CharacterAnimator realAnimator;
    CharacterAnimator visualAnimator;
    int seenAttackSeq;

    public static int ClientVisualCount { get; private set; }

    // 한 틱에 이만큼 넘게 움직이면 걸은 게 아니라 옮긴 것이다(가장 빠른 유닛도 한 틱에 몇 단위).
    static float TeleportThreshold => 20f * WorldScale.Value;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            if (Real != null)
            {
                Real.TryGetComponent(out realEnemy);
                Real.TryGetComponent(out realAttacker);
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
        // 창고·스토리 포탈·모으기·우리로는 실물을 순간이동시킨다. 그대로 적으면 클라가 보간해서 맵을 가로질러
        // 미끄러지는 게 보인다 — 한 틱에 크게 뛰면 Teleport로 알려 클라가 끊어서 옮기게 한다.
        if ((real.position - transform.position).sqrMagnitude > TeleportThreshold * TeleportThreshold && TryGetComponent(out NetworkTransform nt))
            nt.Teleport(real.position, real.rotation);
        else
            transform.SetPositionAndRotation(real.position, real.rotation);
        transform.localScale = real.localScale;

        if (realEnemy != null)
        {
            Hp = realEnemy.Hp;
            MaxHp = realEnemy.MaxHp;
            if (EnemyArmor != realEnemy.EffectiveArmor) EnemyArmor = realEnemy.EffectiveArmor;
            if (EnemyMoveSpeed != realEnemy.MoveSpeed) EnemyMoveSpeed = realEnemy.MoveSpeed;
        }

        if (realAttacker != null)
        {
            if (AttackDamage != realAttacker.AttackDamage) AttackDamage = realAttacker.AttackDamage;
            if (AttackRange != realAttacker.AttackRange) AttackRange = realAttacker.AttackRange;
            if (AttackInterval != realAttacker.AttackInterval) AttackInterval = realAttacker.AttackInterval;
        }

        if (RerollAbility == 0 && EntityKind == NetEntityKind.Unit && Real.TryGetComponent(out UniqueRerollAbility reroll) && NetLauncher.Catalog != null)
            RerollAbility = (short)(NetLauncher.Catalog.rerollAbilities.IndexOf(reroll.Data) + 1);
    }

    public override void Render()
    {
        if (replicaEnemy != null)
        {
            replicaEnemy.SetReplicaHp(Hp, MaxHp);
            replicaEnemy.SetReplicaStats(EnemyArmor, EnemyMoveSpeed);
        }

        // 호스트 실물에 리롤 능력이 붙었으면 겉모습에도 붙인다 — GameHud가 그 컴포넌트로 리롤 버튼을 띄운다(실행은 요청).
        if (!HasStateAuthority && RerollAbility > 0 && Visual != null && !Visual.TryGetComponent(out UniqueRerollAbility _) && NetLauncher.Catalog != null
            && RerollAbility - 1 < NetLauncher.Catalog.rerollAbilities.Count)
            UniqueRerollAbility.Attach(Visual, NetLauncher.Catalog.rerollAbilities[RerollAbility - 1], null);

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
