using UnityEngine;

// 원작 A0VX("희귀함 리롤") — 유닛이 직접 보유하는 액티브 능력(UNIQUE_REROLE_AND_SELL_FAMILY.md
// ①~⑥). h05Y(고대의 배)의 UnitData.gambleOptions와 달리 **특정 자산 하나에 고정된 능력이
// 아니다** — "가챠 등급 풀에서 나온 이름 없는 희귀함 유닛"이면 어느 로스터 자산이든 대상이
// 될 수 있어(⑦), 능력을 UnitData에 미리 박아둘 수 없다. 그래서 gambleOptions처럼 자산
// 필드가 아니라, 도박 성공 시 스폰된 "그 인스턴스"에 런타임으로 붙는다
// (GamblingShop.TryRollUnit 호출부 참고).
//
// ⚠️ gambleOptions와 소모 시점이 정반대다 — gambleOptions는 성공/실패 무관하게 캐스팅
// 유닛을 먼저 지운다(RemoveUnit 방식). A0VX는 **실패하면 유닛이 안 죽고 이 능력만
// 회수된다**(원작 UnitRemoveAbilityBJ, 유닛 자체는 KillUnit이 성공 분기에만 있다) — 그래서
// gambleOptions 구조를 그대로 재사용하지 않고 이 컴포넌트를 새로 뒀다.
[RequireComponent(typeof(UnitIdentity))]
public class UniqueRerollAbility : MonoBehaviour
{
    UniqueRerollAbilityData data;
    UnitSpawner spawner;

    /// <summary>GameHud 버튼 툴팁 표시용 — 실제 차감은 TryCast 내부에서 한다.</summary>
    public int WoodCost => data != null ? data.woodCost : 0;

    /// <summary>도박 성공 스폰 직후 그 인스턴스에 붙일 때만 쓴다 — 자산에 미리 박아두는
    /// 방식이 아니다(위 클래스 주석 참고).</summary>
    public static UniqueRerollAbility Attach(GameObject unitInstance, UniqueRerollAbilityData data, UnitSpawner spawner)
    {
        if (unitInstance == null || data == null) return null;

        UniqueRerollAbility ability = unitInstance.AddComponent<UniqueRerollAbility>();
        ability.data = data;
        ability.spawner = spawner;
        return ability;
    }

    /// <summary>능력 캐스트 1회 시도. 목재 부족·한도 소진이면 아무 것도 안 건드리고 false —
    /// 원작 "stop 명령"과 같은 취급(실패로 안 셈, Rerole_count_int·목재 둘 다 그대로).
    /// 그 관문을 통과하면(=시도가 실제로 접수되면) 원작대로 시도 횟수·목재가 성공/실패
    /// 무관하게 먼저 빠지고, 그 다음에야 실패확률을 굴린다.</summary>
    public bool TryCast(out string message)
    {
        message = null;
        if (data == null || !TryGetComponent(out UnitIdentity identity)) return false;

        int ownerId = identity.OwnerId;
        PlayerContext context = PlayerContext.Get(ownerId);
        UniqueRerollState state = context?.UniqueRerollState;
        if (context == null || state == null) return false;

        if (!state.HasAttemptsLeft)
        {
            message = "리롤회수를 소진하였습니다.";
            return false;
        }

        if (context.ResourceWallet == null || !context.ResourceWallet.TrySpend(ResourceType.Wood, data.woodCost))
        {
            message = "목재가 부족합니다!";
            return false;
        }

        // 시도 횟수 +1, 목재 차감 — 여기까지 오면 성공/실패 무관하게 이미 소모됐다(원작 순서).
        state.RecordAttempt();

        bool fail = Random.Range(0f, 100f) < state.FailChancePercent;

        if (fail)
        {
            message = "리롤을 실패하였습니다.";
            Destroy(this); // 이 유닛의 A0VX만 회수 — 유닛은 안 죽는다(원작 UnitRemoveAbilityBJ).
            return false;
        }

        UnitData replacement = data.gachaTable != null ? data.gachaTable.RollFromGrade(data.resultGrade) : null;
        Vector3 position = transform.position;

        identity.Consume(); // 원래 유닛 제거(원작 KillUnit) — 성공했을 때만.

        if (replacement != null && spawner != null)
            spawner.Spawn(replacement, position, ownerId);
        else
            Debug.LogWarning($"UniqueRerollAbility: 교체 유닛을 스폰하지 못했습니다(replacement={replacement}, spawner={spawner}).");

        message = "희귀함 리롤에 성공하였습니다.";
        return true;
    }
}
