using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 클릭하면 그 자리에서 목재를 내고 즉시 굴리는 도박 포탈. 위습을 쓰지 않는다 —
// 도박은 반복해서 돌리는 행위라(1000회 실측 통계가 있을 정도) 위습 스폰→드래그 조작을
// 넣으면 판당 마찰이 너무 크다. Docs/reference/GAMBLING.md 참고.
[RequireComponent(typeof(Collider))]
public class GamblingPortal : MonoBehaviour
{
    [SerializeField] int woodCost = 1;
    [SerializeField, Range(0f, 100f)] float successChancePercent = 85f;

    [Header("성공 시 지급 등급 — 2개면 GachaTable에 이미 설정된 weight로 가중 추첨한다")]
    [SerializeField] UnitGrade primaryResultGrade;
    [SerializeField] bool useSecondaryGrade;
    [SerializeField] UnitGrade secondaryResultGrade;

    [Header("실패 보상 — 고급·다른세계 도박만 켠다 (GAMBLING.md)")]
    [SerializeField] bool grantFailureReward;
    [SerializeField] int failureLuckyTokens = 2;
    [SerializeField] int failureWood = 1;

    [Header("결과 표시 — 성공/실패를 잠깐 색으로 보여준다")]
    [SerializeField] Color successFlashColor = new Color(0.3f, 0.95f, 0.4f);
    [SerializeField] Color failureFlashColor = new Color(0.9f, 0.25f, 0.25f);
    [SerializeField] float flashDuration = 0.6f;

    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;

    Camera cam;
    Collider portalCollider;
    Renderer portalRenderer;
    MaterialPropertyBlock propertyBlock;
    bool flashing;
    float flashUntil;

    void Awake()
    {
        cam = Camera.main;
        portalCollider = GetComponent<Collider>();
        portalRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (flashing && Time.time >= flashUntil)
        {
            flashing = false;
            // 빈 프로퍼티 블록으로 덮어써서 원래 공유 머티리얼 색으로 되돌린다.
            // 인스턴스 머티리얼을 만들면 포탈끼리 배칭이 깨진다 — MapGenerator.Paint()와 같은 이유.
            if (portalRenderer != null) portalRenderer.SetPropertyBlock(null);
        }

        if (Mouse.current == null || cam == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;
        if (hit.collider != portalCollider) return;

        TryGamble();
    }

    // TODO(멀티): 클릭 판정·굴림은 서버 권위로 이동해야 함 — 지금은 클라이언트가 직접 처리.
    void TryGamble()
    {
        if (!GameAuthority.IsServer) return;

        PlayerContext context = PlayerContext.Local;
        if (context == null || context.ResourceWallet == null) return;

        if (!context.ResourceWallet.TrySpend(ResourceType.Wood, woodCost)) return;

        bool success = Random.Range(0f, 100f) < successChancePercent;

        if (success)
        {
            UnitData reward = RollReward();
            if (reward == null)
            {
                Debug.LogWarning($"{name}: 지급할 유닛을 찾지 못했습니다 — 목재는 이미 소모됐습니다.");
                Flash(failureFlashColor);
                return;
            }

            context.UnitInventory?.Add(reward);

            if (unitSpawner != null)
                unitSpawner.Spawn(reward, ResolveSpawnPosition(context.PlayerId), context.PlayerId);

            Flash(successFlashColor);
        }
        else
        {
            if (grantFailureReward)
            {
                context.ResourceWallet.Add(ResourceType.LuckyToken, failureLuckyTokens);
                context.ResourceWallet.Add(ResourceType.Wood, failureWood);
            }

            Flash(failureFlashColor);
        }
    }

    UnitData RollReward()
    {
        if (gachaTable == null) return null;

        UnitGrade grade = primaryResultGrade;

        if (useSecondaryGrade)
        {
            float primaryWeight = FindWeight(primaryResultGrade);
            float secondaryWeight = FindWeight(secondaryResultGrade);
            float total = primaryWeight + secondaryWeight;

            if (total > 0f && Random.Range(0f, total) >= primaryWeight)
                grade = secondaryResultGrade;
        }

        return gachaTable.RollFromGrade(grade);
    }

    float FindWeight(UnitGrade grade)
    {
        if (gachaTable.entries == null) return 0f;

        foreach (GachaTable.GradeEntry entry in gachaTable.entries)
            if (entry != null && entry.grade == grade)
                return entry.weight;

        return 0f;
    }

    Vector3 ResolveSpawnPosition(int ownerId)
    {
        LaneMarker lane = LaneMarker.Get(ownerId);
        if (lane != null) return lane.transform.position;

        Debug.LogWarning($"{name}: 플레이어 {ownerId}의 레인을 찾지 못해 포탈 자리에 소환합니다.", this);
        return transform.position;
    }

    void Flash(Color color)
    {
        if (portalRenderer == null) return;

        portalRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        portalRenderer.SetPropertyBlock(propertyBlock);

        flashing = true;
        flashUntil = Time.time + flashDuration;
    }
}
