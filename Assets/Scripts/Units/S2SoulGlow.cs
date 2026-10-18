using UnityEngine;

/// <summary>룸바 영혼(원작 SmallLight) 모델용: 원작 이펙트 재생기의 「탄생」 시퀀스(빛 판이 0→1로 떠오름 + 입자)를 느리게 반복해 숨 쉬듯 빛나게 한다.
/// 원작 Stand 시퀀스는 알파 0이라 그대로 두면 아무것도 안 보인다(10-18 실측: 흰 판만 보였다 → 판 재질도 가산/알파로 교체).</summary>
public class S2SoulGlow : MonoBehaviour
{
    void Start()
    {
        // 위에서 내려다보는 카메라에선 가산 빛 판 하나가 옅다 — 영혼 색 점광원을 하나 더해 바닥을 물들인다(원작도 영혼 빛이 땅을 비췄다).
        var lightGo = new GameObject("영혼빛");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.75f, 0.85f, 1f); l.intensity = 40f; l.range = 14f / Mathf.Max(0.01f, transform.localScale.x) * 4f;
        foreach (OriginalVfxPlayer player in GetComponentsInChildren<OriginalVfxPlayer>(true))
            player.Play("birth", 0.35f, true);
    }

    // 원작 SmallLight의 빛 판은 세워진 판(원작 카메라는 비스듬해 보였다)이라 우리 위에서 내려다보는 카메라에선 모서리만 보였다(10-18 실측) → 카메라 쪽으로 돌려 세운다.
    [SerializeField] float yawOffset;
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(0f, yawOffset, 0f);
    }
}
