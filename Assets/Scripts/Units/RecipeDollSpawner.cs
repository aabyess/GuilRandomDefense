using System.Collections.Generic;
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
        public GameObject prefab;
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
                if (doll == null || doll.prefab == null) continue;
                GameObject figure = Instantiate(doll.prefab, staging.transform);
                figure.name = doll.name;
                StripToDoll(figure);
                figure.transform.SetParent(parent, false);
                figure.transform.SetPositionAndRotation(doll.position, doll.rotation);
                figure.transform.localScale = doll.scale;
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
