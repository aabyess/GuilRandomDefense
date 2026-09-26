using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 조합표 인형을 **실행 때** 세운다(2026-09-25, 사장님 승인 — 씬 파일 축소).
///
/// 왜 — 맵 생성기가 인형을 씬에 구워 두면 스킨드 모델의 뼈 Transform마다 자세가 적혀 인형 하나가 최대 0.73MB였다.
/// 조합표 인형(재료_·결과_·흔함_)만 56.5MB로 SampleScene 89.5MB의 63%였고, 100MB를 넘으면 GitHub가 push를 거부한다.
///
/// 맵 생성기(MapGenerator.PlaceRecipeSlot)는 지금과 똑같이 인형을 세워 자세·크기·회전을 다 맞춘 **뒤**,
/// 그 결과(프리팹 · 월드 위치 · 회전 · 배율)만 여기 목록에 적고 인형은 지운다. 실행 때 이 목록대로 다시 세운다 —
/// 크기 규칙·BrokenSkinDolls·폭주 방어막은 전부 맵 생성 쪽에서 이미 거친 값이라 여기서 다시 판단하지 않는다.
///
/// 편집 화면에서 보려면 Tools/맵/조합표 인형 미리 세우기(씬에 저장되지 않는다).
/// </summary>
public class RecipeDollSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Doll
    {
        public string name;
        // 🔴 프리팹이 아니라 **UnitData**를 가리킨다(2026-09-25 판 D: 668기 중 15기만 섰다). 「모델 배선」은 Assets/Prefabs/Generated를
        //    지우고 다시 지어 GUID는 같아도 **프리팹 안쪽 fileID가 바뀐다** — 씬에 적어 둔 프리팹 참조가 전부 끊긴다.
        //    UnitData 에셋은 안 바뀌고, 배선이 매번 그 prefab 필드를 새 프리팹으로 고쳐 쓴다 → 실행 때 unit.prefab을 읽으면 늘 산다.
        public UnitData unit;
        public GameObject prefab;   // 옛 목록 호환용 — unit이 비었을 때만 쓴다
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
    }

    [SerializeField] List<Doll> dolls = new List<Doll>();

    public IReadOnlyList<Doll> Dolls => dolls;
    public void AddDoll(Doll doll) => dolls.Add(doll);

    /// <summary>마지막으로 세운 인형 수와 걸린 시간(ms) — 시작 시간 증가분을 재는 데 쓴다.</summary>
    public static int LastSpawnCount { get; private set; }
    public static double LastSpawnMs { get; private set; }

    void Awake()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int made = Spawn(transform, dolls, HideFlags.None).Count;
        watch.Stop();
        LastSpawnCount = made;
        LastSpawnMs = watch.Elapsed.TotalMilliseconds;
        Debug.Log($"[조합표 인형] {made}/{dolls.Count}기를 {LastSpawnMs:F0}ms에 세웠습니다({name}).");
        // 🔴 조용히 사라지지 않게 — 목록은 있는데 못 세운 칸을 경고로 남긴다(09-25 판 D: 15/668기였는데 로그 한 줄로만 보였다).
        if (made < dolls.Count)
        {
            var broken = dolls.Where(d => d == null || ((d.unit == null || d.unit.prefab == null) && d.prefab == null)).ToList();
            int noUnit = broken.Count(d => d == null || d.unit == null);
            Debug.LogWarning($"[조합표 인형] 인형 {made}/{dolls.Count} — 못 세운 칸 {dolls.Count - made}개 " +
                             $"(프리팹 없는 UnitData {broken.Count - noUnit}개 · UnitData 없음 {noUnit}개). 예: " +
                             string.Join(", ", broken.Take(5).Select(d => d == null ? "(빈 칸)" : $"{d.name}({(d.unit != null ? d.unit.name : "UnitData 없음")})")) +
                             " — 로스터에 프리팹이 안 배선됐거나 맵 생성 뒤 목록이 낡았다(맵을 다시 생성).", this);
        }
    }

    /// <summary>목록대로 인형을 세운다. 프리팹이 비어 있는 칸은 건너뛰고 수를 돌려준다.</summary>
    public static List<GameObject> Spawn(Transform parent, IEnumerable<Doll> list, HideFlags flags)
    {
        var made = new List<GameObject>();
        // 🔴 꺼진 부모 아래에서 만든다 — 켜진 채 만들면 유닛 프리팹의 Awake·OnEnable(선택 등록부·인벤토리·NavMeshAgent)이
        //    인형에서 한 번 돌아, 조합표 위 인형이 「내 유닛」으로 등록된다. 꺼진 채 스크립트를 걷어낸 뒤 옮겨 켠다.
        GameObject staging = new GameObject("조합표인형_준비");
        staging.hideFlags = HideFlags.HideAndDontSave;
        staging.SetActive(false);
        try
        {
            foreach (Doll doll in list)
            {
                GameObject source = doll == null ? null : doll.unit != null && doll.unit.prefab != null ? doll.unit.prefab : doll.prefab;
                if (source == null) continue;
                GameObject figure = Instantiate(source, staging.transform);
                figure.name = doll.name;
                StripToDoll(figure);
                figure.transform.SetParent(parent, false);
                figure.transform.SetPositionAndRotation(doll.position, doll.rotation);
                figure.transform.localScale = doll.scale;
                AttachInspectCollider(figure, doll.unit);
                if (flags != HideFlags.None)
                    foreach (Transform t in figure.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = flags;
                made.Add(figure);
            }
        }
        finally
        {
            if (Application.isPlaying) Destroy(staging); else DestroyImmediate(staging);
        }
        return made;
    }

    // 클릭하면 어떤 유닛인지 보이게(친구 베타 피드백 ⑤, 2026-09-26) — 렌더러 경계 크기의 **트리거** 상자 + 표지.
    //    트리거라 이동 광선(WorldPick.TryHitGround)은 지나치고, 실행 때 붙여서 굽힌 NavMesh와도 무관하다.
    static void AttachInspectCollider(GameObject figure, UnitData unit)
    {
        Renderer[] renderers = figure.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        BoxCollider box = figure.AddComponent<BoxCollider>();
        box.isTrigger = true;
        Vector3 s = figure.transform.lossyScale;
        box.center = figure.transform.InverseTransformPoint(b.center);
        box.size = new Vector3(b.size.x / Mathf.Max(Mathf.Abs(s.x), 1e-4f), b.size.y / Mathf.Max(Mathf.Abs(s.y), 1e-4f), b.size.z / Mathf.Max(Mathf.Abs(s.z), 1e-4f));
        figure.AddComponent<DollInfo>().SetUnit(unit);
    }

    // MapGenerator.TryPlaceUnitModel이 인형에서 걷어내던 것과 같은 규칙: 보이는 것(Transform·Renderer·MeshFilter)과
    // Animator만 남긴다. 스크립트를 먼저 지운다 — RequireComponent가 NavMeshAgent 같은 부품을 붙들고 있어서다.
    static void StripToDoll(GameObject figure)
    {
        for (int pass = 0; pass < 3; pass++)
            foreach (MonoBehaviour script in figure.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) DestroyImmediate(script);

        foreach (Component component in figure.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            if (component is Transform || component is Renderer || component is MeshFilter || component is Animator) continue;
            DestroyImmediate(component);
        }

        // DollIdle과 같은 규칙: 사람형만 Idle을 돌린다. Generic(배·짐승)은 실행 때 제 클립을 돌리면 돛·밧줄이 판 전체로 퍼졌다(09-15).
        foreach (Animator animator in figure.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            animator.enabled = animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
        }
    }
}
