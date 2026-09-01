using System.Collections;
using UnityEngine;

// TODO(멀티플레이): 스폰/리스폰도 서버 권위로 옮겨야 한다 — 지금은 클라이언트가 직접 처리.
public class SealSpawner : MonoBehaviour
{
    [SerializeField] EnemyData sealData;
    [SerializeField] float respawnSeconds = 30f;

    GameObject currentSeal;
    Coroutine spawnLoop;

    void Start()
    {
        if (!GameAuthority.IsServer) return;

        // 죽음마다 새 코루틴을 띄우지 않고, 하나의 루프가 스폰→대기→리스폰을 계속 돈다.
        // 그래야 리스폰 코루틴이 겹쳐 쌓여서 물범이 한꺼번에 여러 마리 나오는 일이 없다.
        spawnLoop = StartCoroutine(SpawnLoop());
    }

    void OnDestroy()
    {
        if (spawnLoop != null)
        {
            StopCoroutine(spawnLoop);
            spawnLoop = null;
        }
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnSeal();

            // Destroy는 프레임 끝에 처리되지만, 그 이후에는 currentSeal == null로 정상 감지된다.
            yield return new WaitUntil(() => currentSeal == null);
            yield return new WaitForSeconds(respawnSeconds);
        }
    }

    void SpawnSeal()
    {
        if (!GameAuthority.IsServer) return;

        if (sealData == null || sealData.prefab == null)
        {
            Debug.LogWarning("SealSpawner: sealData 또는 prefab이 비어있어 물범을 스폰하지 못했습니다.", this);
            return;
        }

        currentSeal = Instantiate(sealData.prefab, transform.position, Quaternion.identity);

        if (currentSeal.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(sealData);
            // 물범은 레인 몹이 아니다 — 레인 번호가 붙으면 패배 판정(가장 붐비는 레인 기준)에 섞여 들어간다.
            dummy.SetLane(-1);
        }

        if (currentSeal.TryGetComponent(out WaypointMover mover))
        {
            mover.enabled = false;
        }
    }
}
