using UnityEngine;

// 선택 위습 부스의 유닛 발밑 빛 고리(10-20 사장님 「선택 위습 이렇게」, 원랜디 시즌6 사진): 평소 파랑, 그 칸 포탈이 위습을 받아 유닛을 지급한 순간 1.5초 분홍.
// 순수 겉모습 — 지급은 UnitPortal이 하고 여기는 알림만 듣는다(서버에서만 알림이 오므로 멀티 손님 화면에선 번쩍이지 않는다).
public class BoothRing : MonoBehaviour
{
    [SerializeField] UnitPortal portal;
    [SerializeField] Renderer ring;

    static readonly Color Blue = new Color(0.25f, 0.55f, 1f, 1f);
    static readonly Color Pink = new Color(1f, 0.35f, 0.75f, 1f);
    const float FlashSeconds = 1.5f;

    MaterialPropertyBlock block;
    float flashUntil;

    public void Bind(UnitPortal owner, Renderer target)
    {
        portal = owner;
        ring = target;
    }

    void OnEnable()
    {
        UnitPortal.Granted += OnGranted;
        Apply(Blue);
    }

    void OnDisable() => UnitPortal.Granted -= OnGranted;

    void OnGranted(UnitPortal granted)
    {
        if (granted != portal) return;
        flashUntil = Time.time + FlashSeconds;
        Apply(Pink);
    }

    void Update()
    {
        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            flashUntil = 0f;
            Apply(Blue);
        }
    }

    void Apply(Color color)
    {
        if (ring == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        ring.GetPropertyBlock(block);
        block.SetColor("_Tint", color);
        block.SetColor("_EdgeColor", Color.Lerp(color, Color.white, 0.5f));
        ring.SetPropertyBlock(block);
    }
}
