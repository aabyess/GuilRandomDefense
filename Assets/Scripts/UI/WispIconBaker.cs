using UnityEngine;

/// <summary>
/// 위습 칸 아이콘(사장님 10-03): 원작은 워크3 기본 BTNWisp 한 장이라 종류 구분이 칸 색뿐이다 — 우리는 게임 안 위습 3D 모델을 한 번 찍어 Sprite로 쓴다.
/// 시작 때가 아니라 **살아 있는 위습이 처음 보일 때 한 번** 구워 캐시한다(매 프레임 렌더 금지). 모델을 복제해 먼 곳(레이어 31)에 세우고
/// 직교 카메라로 한 장 찍은 뒤 지운다. 플레이어 색은 복제가 가져온 MaterialPropertyBlock 그대로(내 위습 색).
/// </summary>
public static class WispIconBaker
{
    const int Size = 128;
    static Sprite cached;
    static bool failed;

    public static Sprite Get(Wisp source)
    {
        if (cached != null) return cached;
        if (failed || source == null) return null;
        cached = Bake(source.gameObject);
        if (cached == null) failed = true;
        return cached;
    }

    static Sprite Bake(GameObject source)
    {
        Vector3 origin = new Vector3(0f, -12000f, 0f);
        GameObject stage = new GameObject("WispIconStage");
        stage.transform.position = origin;
        GameObject clone = Object.Instantiate(source, stage.transform, false);
        clone.name = "WispIcon";
        PortraitStage.Strip(clone);
        PortraitStage.SetLayerRecursively(clone, PortraitStage.Layer);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = source.transform.lossyScale;

        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Object.Destroy(stage); return null; }
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        var cameraObject = new GameObject("WispIconCamera");
        cameraObject.transform.SetParent(stage.transform, false);
        Camera cam = cameraObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.15f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << PortraitStage.Layer;
        cam.enabled = false;
        Vector3 view = Quaternion.Euler(-10f, 20f, 0f) * Vector3.forward;   // 모델 앞쪽에서 살짝 위·옆
        float reach = b.extents.magnitude * 3f + 1f;
        cameraObject.transform.position = b.center + view * reach;
        cameraObject.transform.rotation = Quaternion.LookRotation(-view, Vector3.up);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = reach + b.extents.magnitude * 2f;

        var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
        tex.Apply();
        RenderTexture.active = previous;
        cam.targetTexture = null;
        rt.Release();
        Object.Destroy(rt);
        Object.Destroy(stage);
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }
}
