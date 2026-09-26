using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 클라의 「겉모습」 만들기(설계 §3). 호스트의 실물과 같은 프리팹을 세우되 게임 로직은 떼어 낸다.
///
///  1. **비활성 부모 아래**에 Instantiate — Unity는 꺼진 컴포넌트에도 Awake를 돌려서 enabled=false로는 못 막는다.
///     활성화 전에 떼면 Awake 자체가 안 돈다.
///  2. 우리 게임 코드(MonoBehaviour)는 **남길 목록에 있는 것만** 남긴다(화이트리스트). 블랙리스트로 하면
///     새로 생기는 로직 컴포넌트가 클라에서 조용히 돈다. 엔진 기본 컴포넌트(렌더러·애니메이터·콜라이더…)는 둔다.
///  3. NavMeshAgent는 UnitMover가 RequireComponent라 끄기만 한다. 끄면 CharacterAnimator가
///     「위치 변화로 속도 재기」로 떨어져 걷기 애니가 그대로 돈다.
///  4. 제거 목록은 프리팹마다 처음 한 번 로그로 남긴다(검증용).
/// </summary>
public static class NetReplicaBuilder
{
    // 겉모습에 남기는 우리 컴포넌트. 선택·표시·애니·소유자·데이터 조회만 — 판정·이동 로직은 없다.
    // UnitMover는 남긴다: 1-c에서 클라의 우클릭을 「이동 요청 RPC」로 바꾸는 자리다.
    // EnemyDummy는 복제 모드(IsReplica)로 남는다: 체력바·미니맵이 EnemyDummy.Active를 돈다.
    static readonly HashSet<Type> KeepTypes = new HashSet<Type>
    {
        typeof(CharacterAnimator),
        typeof(Selectable),
        typeof(SelectionIndicator),
        typeof(OwnedByPlayer),
        typeof(UnitIdentity),
        typeof(Wisp),
        typeof(UnitMover),
        typeof(AttackRangeIndicator),
        typeof(EnemyDummy),
    };

    static readonly HashSet<GameObject> loggedPrefabs = new HashSet<GameObject>();

    static Transform holder;

    public static GameObject Build(NetEntity entity)
    {
        NetCatalog catalog = NetLauncher.Catalog;
        if (catalog == null)
        {
            Debug.LogWarning("[MP] 카탈로그가 없어 겉모습을 못 세웁니다.");
            return null;
        }

        GameObject prefab = null;
        UnitData unitData = null;
        EnemyData enemyData = null;
        WispData wispData = null;
        switch (entity.EntityKind)
        {
            case NetEntityKind.Unit: unitData = catalog.Unit(entity.CatalogIndex); prefab = unitData != null ? unitData.prefab : null; break;
            case NetEntityKind.Enemy: enemyData = catalog.Enemy(entity.CatalogIndex); prefab = enemyData != null ? enemyData.prefab : null; break;
            case NetEntityKind.Wisp: wispData = catalog.Wisp(entity.CatalogIndex); prefab = wispData != null ? wispData.prefab : null; break;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[MP] {entity.EntityKind} 카탈로그 {entity.CatalogIndex}번의 프리팹이 없어 겉모습을 못 세웁니다.");
            return null;
        }

        GameObject visual = UnityEngine.Object.Instantiate(prefab, InactiveHolder, false);
        visual.name = prefab.name + " (거울)";
        Strip(visual, prefab);

        if (entity.EntityKind != NetEntityKind.Enemy)
        {
            // Awake 전에 소유자를 박는다 — UnitIdentity.Awake가 OwnedByPlayer를 잡아 둔다.
            if (!visual.TryGetComponent(out OwnedByPlayer owner)) owner = visual.AddComponent<OwnedByPlayer>();
            owner.SetOwner(entity.Owner);
        }

        // 거울 루트(NetEntity)가 위치·회전·스케일을 싣는다 — 겉모습은 그 자식으로 제자리에.
        Transform t = visual.transform;
        t.SetParent(entity.transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // 활성화는 부모를 옮긴 뒤 — 이때 남긴 컴포넌트들의 Awake/OnEnable이 돈다(Selectable 등록·체력바 등록).

        switch (entity.EntityKind)
        {
            case NetEntityKind.Unit:
                if (visual.TryGetComponent(out UnitIdentity identity))
                {
                    identity.SetData(unitData);
                    // 클라의 UnitInventory가 곧 「내 유닛 복제본 목록」이 된다 — 겉모습이 생기고 사라지는 것이 복제다.
                    identity.RegisterTo(PlayerContext.Get(entity.Owner)?.UnitInventory);
                }
                break;
            case NetEntityKind.Enemy:
                if (visual.TryGetComponent(out EnemyDummy enemy)) enemy.InitializeReplica(enemyData, entity.Hp, entity.MaxHp);
                break;
            case NetEntityKind.Wisp:
                if (visual.TryGetComponent(out Wisp wisp)) wisp.SetData(wispData);
                break;
        }

        return visual;
    }

    static Transform InactiveHolder
    {
        get
        {
            if (holder == null)
            {
                GameObject go = new GameObject("[MP] 겉모습 준비칸");
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                holder = go.transform;
            }
            return holder;
        }
    }

    static void Strip(GameObject visual, GameObject prefab)
    {
        List<string> removed = null;
        bool log = loggedPrefabs.Add(prefab);
        if (log) removed = new List<string>();

        // 뒤에서부터 지운다 — RequireComponent로 딸려 붙은 것은 보통 요구한 쪽보다 앞에 있어서,
        // 요구한 쪽이 먼저 지워져야 지울 수 있다.
        Component[] components = visual.GetComponentsInChildren<Component>(true);
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component c = components[i];
            if (c == null || c is Transform) continue;

            if (c is NavMeshAgent agent) { agent.enabled = false; continue; }
            if (c is NavMeshObstacle obstacle) { obstacle.enabled = false; continue; }
            if (c is Rigidbody body) { body.isKinematic = true; continue; }

            if (!(c is MonoBehaviour)) continue;               // 엔진 기본 컴포넌트는 둔다
            Type type = c.GetType();
            if (KeepTypes.Contains(type)) continue;
            string ns = type.Namespace ?? "";
            if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue; // UI·TMP 같은 엔진 쪽 스크립트

            removed?.Add(type.Name);
            UnityEngine.Object.DestroyImmediate(c);
        }

        if (log)
            Debug.Log($"[MP] 겉모습 {prefab.name}: 뗀 컴포넌트 {(removed.Count == 0 ? "없음" : string.Join(", ", removed))}");
    }
}
