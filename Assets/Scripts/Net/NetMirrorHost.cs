using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 호스트 전용. 매 틱 필드 등록부(UnitIdentity.Active · EnemyDummy.Active · Wisp.Active)를 훑어
/// 아직 거울이 없는 실물에 NetEntity를 붙인다(설계 §3 「등록부 훑기」).
/// 생성 지점 9곳(UnitSpawner·RewardDistributor·WaveSpawner·Seal·SeaKing·Story·Treasure·PirateQuest…)에
/// 한 줄씩 넣지 않는다 — 남의 파일을 안 고치고, 앞으로 생길 생성 경로도 저절로 잡힌다.
/// 거두는 쪽은 NetLink.OnDestroy(실물 파괴) 한 곳이다.
/// </summary>
public class NetMirrorHost : SimulationBehaviour
{
    NetworkObject entityPrefab;
    NetCatalog catalog;

    readonly HashSet<Object> warnedMissing = new HashSet<Object>();
    readonly List<GameObject> pending = new List<GameObject>();
    float nextReport;

    public void Setup(NetworkObject prefab, NetCatalog netCatalog)
    {
        entityPrefab = prefab;
        catalog = netCatalog;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer || entityPrefab == null || catalog == null) return;
        if (NetGameState.Instance == null || !NetGameState.Instance.Started) return;

        int created = 0;

        pending.Clear();
        foreach (UnitIdentity unit in UnitIdentity.Active)
        {
            // 조합표 인형 같은 장식은 소유자가 없다 — 플레이어 유닛만 비춘다.
            if (unit == null || unit.Data == null || unit.OwnerId < 0) continue;
            if (!unit.TryGetComponent(out NetLink _)) pending.Add(unit.gameObject);
        }
        foreach (GameObject go in pending)
        {
            UnitIdentity unit = go.GetComponent<UnitIdentity>();
            if (Mirror(go, NetEntityKind.Unit, unit.Data, unit.OwnerId)) created++;
        }

        pending.Clear();
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null || enemy.Data == null) continue;
            if (!enemy.TryGetComponent(out NetLink _)) pending.Add(enemy.gameObject);
        }
        foreach (GameObject go in pending)
        {
            EnemyDummy enemy = go.GetComponent<EnemyDummy>();
            // 적은 소유자 대신 레인 번호를 싣는다 — 클라 HUD의 「플레이어 N | 적 M」이 레인별로 센다.
            if (Mirror(go, NetEntityKind.Enemy, enemy.Data, enemy.LaneIndex)) created++;
        }

        pending.Clear();
        foreach (Wisp wisp in Wisp.Active)
        {
            if (wisp == null || wisp.Data == null || wisp.IsConsumed) continue;
            if (!wisp.TryGetComponent(out NetLink _)) pending.Add(wisp.gameObject);
        }
        foreach (GameObject go in pending)
        {
            Wisp wisp = go.GetComponent<Wisp>();
            int owner = go.TryGetComponent(out OwnedByPlayer ownedBy) ? ownedBy.OwnerId : -1;
            if (Mirror(go, NetEntityKind.Wisp, wisp.Data, owner)) created++;
        }

        if (Time.realtimeSinceStartup >= nextReport && (created > 0 || nextReport > 0f))
        {
            nextReport = Time.realtimeSinceStartup + 5f;
            Debug.Log($"[MP] 거울(호스트): 유닛 {UnitIdentity.Active.Count} · 적 {EnemyDummy.Active.Count} · 위습 {Wisp.Active.Count} · 이번 틱 새로 {created}");
        }
    }

    bool Mirror(GameObject real, NetEntityKind kind, Object data, int owner)
    {
        // 꼬리표를 먼저 단다 — 카탈로그에 없어 거울을 못 세우는 것도 다음 틱에 다시 훑지 않게.
        NetLink link = real.AddComponent<NetLink>();

        int index = catalog.IndexOf(data);
        if (index < 0)
        {
            if (warnedMissing.Add(data))
                Debug.LogWarning($"[MP] 카탈로그에 없는 {kind} 데이터 {data.name} — 클라에 안 보입니다. Tools/Net/멀티 뼈대 만들기로 카탈로그를 다시 만드세요.", real);
            return false;
        }

        Transform t = real.transform;
        NetworkObject obj;
        try
        {
            obj = SpawnEntity(real, t, kind, index, owner);
        }
        catch (System.Exception exception)
        {
            // NetEntity 프리팹은 처음 한 번 지연 로드된다 — 첫 동기 스폰이 「Failed to load prefab synchronously」로
            // 실패하고 그 사이 로드가 시작된다(09-26 실측, 15개 중 1개 누락). 꼬리표를 떼서 다음 틱에 다시 훑게 한다.
            Debug.Log($"[MP] 거울 스폰 실패 — 다음 틱에 다시: {real.name} ({exception.GetType().Name})");
            Destroy(link);
            return false;
        }

        link.Entity = obj != null ? obj.GetComponent<NetEntity>() : null;
        return link.Entity != null;
    }

    NetworkObject SpawnEntity(GameObject real, Transform t, NetEntityKind kind, int index, int owner)
    {
        return Runner.Spawn(entityPrefab, t.position, t.rotation, null, (runner, spawned) =>
        {
            NetEntity e = spawned.GetComponent<NetEntity>();
            e.Kind = (byte)kind;
            e.CatalogIndex = (short)index;
            e.Owner = (sbyte)owner;
            e.Real = real;
            spawned.transform.localScale = t.localScale;
            if (real.TryGetComponent(out EnemyDummy enemy))
            {
                e.Hp = enemy.Hp;
                e.MaxHp = enemy.MaxHp;
            }
        });
    }
}
