using System.Collections;
using UnityEngine;

// 신세계 사이드보스 전투 상태기계(R62·66·71 전용) — Docs/reference/ORIGINAL_BOSS_COMBAT_SPEC.md
// §④~⑥·§203의 상태기계를 그대로 옮겼다.
//
// ⚠️ 원작은 플레이어별로 완전히 독립이다(트리거 sin_boss_skill1~4 4벌, 모든 변수가
// [플레이어] 배열, 스펙 §②) — "보스 하나를 넷이 같이 때린다"가 아니라 각자 자기 보스를
// 상대한다. 그래서 이 컴포넌트는 전역 싱글턴이 아니라 **보스 인스턴스 하나(=플레이어
// 하나)당 하나씩** 붙는다. 스폰·트리거는 SideBossManager가 맡고, 여기는 그 인스턴스
// 하나의 상태기계만 돈다.
//
[RequireComponent(typeof(EnemyDummy))]
public class SideBossEncounter : MonoBehaviour
{
    public enum Stage { Casting, Recharging, Done }

    EnemyDummy self;
    float castProgress;   // 0~100 (§④)
    float stunGauge;      // 0~100, 플레이어별 영속값을 이어받는다(§⑥)
    int stunStack;
    Stage stage = Stage.Done;

    System.Action<float> onFinished;         // §⑧ 정산용 — 최종 사이드보스 체력%(0~100)
    System.Action<float> onStunGaugeChanged; // 영속값 저장 콜백(플레이어별)

    public Stage CurrentStage => stage;
    public float CastProgress => castProgress;
    public float StunGauge => stunGauge;

    /// <summary>스폰 직후 SideBossManager가 부른다.</summary>
    public void BeginEncounter(float startingStunGauge, System.Action<float> onFinishedCallback,
                                System.Action<float> onStunGaugeChangedCallback)
    {
        self = GetComponent<EnemyDummy>();
        stunGauge = startingStunGauge;
        stunStack = 0;
        castProgress = 0f;
        onFinished = onFinishedCallback;
        onStunGaugeChanged = onStunGaugeChangedCallback;

        // §⑤ "유닛 데이터에 처음부터 박혀 있다" — 트리거가 켜는 게 아니라 스폰 시점부터 무적.
        self.SetTrueInvulnerable(true);

        stage = Stage.Casting;
        StartCoroutine(CastingLoop());
    }

    // ── Stage1: 시전(§④·§⑥ 감소) ──────────────────────────────────────────
    IEnumerator CastingLoop()
    {
        while (stage == Stage.Casting)
        {
            yield return new WaitForSeconds(0.20f);
            if (self == null) yield break; // OnDestroy가 정산을 처리한다.

            castProgress += 5f;

            // §⑥ 감소 — 스턴이 걸려 있는 틱마다 스택 +1, 게이지 = max(0, floor(게이지 − 스택×0.15)).
            // 스택은 스턴이 끊겨도 안 줄어들고, 게이지가 부서질 때만 0으로 돌아간다.
            if (self.IsStunned)
            {
                stunStack++;
                stunGauge = Mathf.Max(0f, Mathf.Floor(stunGauge - stunStack * 0.15f));
                onStunGaugeChanged?.Invoke(stunGauge);
            }

            if (stunGauge < 2f)
            {
                // §⑥ 파괴 — 진행도 −5, 스택 0, 무적 해제. 시전은 재개되지 않고 취소된다.
                castProgress = Mathf.Max(0f, castProgress - 5f);
                stunStack = 0;
                self.SetTrueInvulnerable(false);
                Debug.Log($"{name}: 사이드보스 게이지 파괴 — 시전 취소, 4초 딜 창 시작.");

                stage = Stage.Recharging;
                yield return new WaitForSeconds(0.05f);
                StartCoroutine(RechargingLoop());
                yield break;
            }

            if (castProgress >= 100f)
            {
                yield return new WaitForSeconds(0.20f);
                if (self == null) yield break;
                SpawnBerserkMob();
                Finish();
                yield break;
            }
        }
    }

    // ── Stage3: 재충전(파괴 뒤 4초 딜 창, §⑥) ──────────────────────────────
    IEnumerator RechargingLoop()
    {
        int counter = 0;
        while (stage == Stage.Recharging)
        {
            yield return new WaitForSeconds(0.08f);
            if (self == null) yield break; // 딜 창에서 죽었다 — OnDestroy가 0%로 정산한다.

            counter += 2;
            stunGauge = Mathf.Min(101f, counter);

            if (stunGauge > 99f)
            {
                stunGauge = 100f;
                self.SetTrueInvulnerable(true);
                // ⚠️ §⑨ 기존 조사 오류 정정 — "라운드를 넘어 누적된다"는 부수기 전까지만이고,
                // 게이지를 부수면 저장값이 100으로 리셋된다. 부순 대가로 누적을 잃는다.
                onStunGaugeChanged?.Invoke(100f);
                yield return new WaitForSeconds(0.08f);
                Finish();
                yield break;
            }
        }
    }

    // §⑦ 광폭화 소환 — 자리만 만든다(EnemyData·버프 배선은 콘텐츠 담당 몫, 지어내지 않는다).
    void SpawnBerserkMob()
    {
        Debug.Log($"{name}: 시전 완료 — 광폭화 몬스터 소환 자리(구현 예정, §⑦).");
    }

    void Finish()
    {
        if (stage == Stage.Done) return;
        stage = Stage.Done;
        float pct = self != null ? self.HpRatio * 100f : 0f;
        onFinished?.Invoke(pct);
        if (self != null) Destroy(self.gameObject); // 보스·바 숨김(Stage4).
    }

    // 코루틴은 GameObject가 파괴되면 같이 죽어서 루프 안의 self==null 체크가 못 도는 경우가
    // 있다(예: TakeDamage의 사망 처리가 이 프레임에 바로 Destroy를 건다) — OnDestroy가
    // 그 경로를 전부 받는다. Finish()가 이미 stage를 Done으로 바꿔놓으므로 정상 종료 시엔
    // 중복 호출되지 않는다.
    void OnDestroy()
    {
        if (stage == Stage.Done) return;
        stage = Stage.Done;
        onFinished?.Invoke(0f); // §⑧ else 분기 — 죽었으면 기여 0%.
    }
}
