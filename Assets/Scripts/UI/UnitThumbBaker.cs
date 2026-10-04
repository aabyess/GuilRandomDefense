using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛별 64px 초상 썸네일(조합 검색 서랍용). 처음 필요할 때 대기열에 넣고 **프레임당 몇 개씩** 구워 캐시한다 — 한 프레임에 몰아 굽지 않는다(프레임 튐 방지).
/// WispIconBaker와 같은 방식: 모델을 복제해 먼 곳(y −13000, 레이어 31)에 세우고 카메라로 한 장 찍은 뒤 바로 지운다.
/// 구도는 PortraitStage와 같은 결 — 서 있는 모습(높이/폭 ≥ 1.35)이면 머리~허리(위쪽 절반), 아니면 전신. (PortraitStage는 선택 초상이 쓰는 한 개짜리 무대라 따로 둔다.)
/// 순수 겉모습이다: 복제의 게임 스크립트·콜라이더·NavMeshAgent는 PortraitStage.Strip이 떼고, 꺼진 부모 아래서 만들어 Awake가 안 돈다.
/// </summary>
public static class UnitThumbBaker
{
    public const int Size = 64;
    const int PerFrame = 2;
    const float BustFraction = 0.5f;
    const float StandingAspect = 1.35f;
    static readonly Vector3 Origin = new Vector3(0f, -13000f, 0f);
    static readonly Vector3 ViewDirection = Quaternion.Euler(-12f, 25f, 0f) * Vector3.forward;

    static readonly Dictionary<UnitData, Sprite> cache = new Dictionary<UnitData, Sprite>();
    static readonly HashSet<UnitData> queued = new HashSet<UnitData>();
    static readonly Queue<UnitData> queue = new Queue<UnitData>();
    static Driver driver;

    /// <summary>하나 이상 구워졌을 때(프레임당 한 번) — 서랍이 그림을 다시 끼운다.</summary>
    public static event Action Baked;

    /// <summary>구운 그림(없으면 null — 대기열에 넣어 두고 나중에 Baked로 알린다). 모델이 없는 유닛은 영원히 null.</summary>
    public static Sprite Get(UnitData unit)
    {
        if (unit == null) return null;
        if (cache.TryGetValue(unit, out Sprite sprite)) return sprite;
        if (queued.Add(unit))
        {
            queue.Enqueue(unit);
            EnsureDriver();
        }
        return null;
    }

    static void EnsureDriver()
    {
        if (driver != null) return;
        var go = new GameObject("[썸네일 굽기]");
        go.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(go);
        driver = go.AddComponent<Driver>();
    }

    class Driver : MonoBehaviour
    {
        void Update()
        {
            int done = 0;
            while (done < PerFrame && queue.Count > 0)
            {
                UnitData unit = queue.Dequeue();
                Sprite sprite = null;
                try { sprite = Bake(unit); }
                catch (Exception e) { Debug.LogWarning($"[썸네일] {unit.name} 굽기 실패: {e.Message}"); }
                cache[unit] = sprite;   // 실패(null)도 기억한다 — 매번 다시 시도하지 않는다
                done++;
            }
            if (done > 0) Baked?.Invoke();
        }
    }

    static Sprite Bake(UnitData unit)
    {
        if (unit.prefab == null) return null;

        GameObject stage = new GameObject("UnitThumbStage");
        stage.transform.position = Origin;
        stage.SetActive(false);   // 꺼진 부모 아래서 복제 — 게임 스크립트 Awake·NavMeshAgent 경고가 안 돈다
        try
        {
            GameObject clone = UnityEngine.Object.Instantiate(unit.prefab, stage.transform, false);
            clone.name = "Thumb";
            PortraitStage.Strip(clone);
            PortraitStage.SetLayerRecursively(clone, PortraitStage.Layer);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            stage.SetActive(true);
            foreach (Animator animator in clone.GetComponentsInChildren<Animator>())
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0.05f);   // Idle 자세를 한 번 잡는다(T자로 안 나오게)
                animator.enabled = false;
            }

            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y < 0.001f) return null;

            float horizontal = Mathf.Max(b.size.x, b.size.z);
            bool standing = b.size.y / Mathf.Max(horizontal, 0.001f) >= StandingAspect;
            Vector3 center = b.center;
            float half = Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f;
            if (standing)
            {
                float height = b.size.y * BustFraction;
                center = new Vector3(b.center.x, b.max.y - height * 0.5f, b.center.z);
                half = Mathf.Max(height * 0.5f, horizontal * 0.5f);
            }

            GameObject camObject = new GameObject("ThumbCamera");
            camObject.transform.SetParent(stage.transform, false);
            Camera cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = half * 1.12f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.23f, 1f);   // GameHud SlotColor — 초상칸과 같은 바탕
            cam.cullingMask = 1 << PortraitStage.Layer;
            cam.enabled = false;
            float reach = b.extents.magnitude * 3f + 1f;
            camObject.transform.position = center + ViewDirection * reach;
            camObject.transform.rotation = Quaternion.LookRotation(-ViewDirection, Vector3.up);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = reach + b.extents.magnitude * 2f;

            var rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stage);   // 즉시 — 같은 프레임의 다음 굽기에 이 복제가 비치지 않게
        }
    }
}
