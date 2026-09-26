using UnityEngine;

// 위습: 플레이어가 조종하는 유닛. 선택/이동은 기존 Selectable·UnitMover·OwnedByPlayer를 그대로 재사용한다.
public class Wisp : MonoBehaviour
{
    [SerializeField] WispData data;

    public WispData Data => data;

    // Destroy는 프레임 끝에야 처리되므로, 같은 프레임에 여러 트리거에 겹쳐 들어가도
    // 중복 소모되지 않도록 확정 시점에 바로 마킹한다 (EnemyDummy.isDead와 같은 패턴).
    public bool IsConsumed { get; private set; }

    public void SetData(WispData wispData)
    {
        data = wispData;
    }

    // 위습이 포탈에 소모될 때 알린다. 소모하는 쪽(UnitPortal/ResourcePortal)이 서로를 몰라도
    // 되게 하려고 여기서 한 곳으로 모은다 — 예: 백수생활 특수 칸 3개가 서로 참조 없이
    // "같은 위습 종류가 소모됐다"만 각자 구독해서 안다(InterludeGate 참고).
    public static event System.Action<Wisp> OnConsumed;

    public void MarkConsumed()
    {
        IsConsumed = true;
        OnConsumed?.Invoke(this);
    }

    // 주인 색(2026-09-26 사장님 「영혼 위습 색상을 각 플레이어마다 다르게 — 파랑·빨강·노랑·초록」 → 「각 플레이어 색상 있으면 그걸로」).
    //    PlayerColors = 선택 표시·미니맵·팀판과 같은 색. 위습 칸은 넷이 같이 써서, 색이 같으면 어느 게 내 것인지 모른다.
    //    칠하는 것은 몸(wisp_soul 재질)과 영혼빛(점광원)뿐이다 — 선택 고리(SelectionIndicator)는 자기 색을 따로 쓴다.
    //    재질을 복제하지 않게 MaterialPropertyBlock으로 칠한다(런타임에 생긴 개체라 씬 저장 문제는 없다).
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    const string SoulMaterialName = "wisp_soul";

    public void ApplyOwnerColor(int playerId)
    {
        Color color = PlayerColors.Get(playerId);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer body in GetComponentsInChildren<Renderer>(true))
        {
            Material shared = body.sharedMaterial;
            if (shared == null || !shared.name.StartsWith(SoulMaterialName)) continue;
            float alpha = shared.HasProperty(BaseColorId) ? shared.GetColor(BaseColorId).a : 0.55f;
            body.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, alpha));
            block.SetColor(EmissionColorId, color * 1.1f);   // wisp_soul 기본 발광 배율(1.1)과 같게
            body.SetPropertyBlock(block);
        }
        foreach (Light glow in GetComponentsInChildren<Light>(true)) glow.color = color;
    }
}
