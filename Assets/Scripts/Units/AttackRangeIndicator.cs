using UnityEngine;

// 선택한 유닛의 공격 범위를 발밑에 원으로 그린다. 선택했을 때만 보인다 —
// 항상 켜두면 유닛 수십 기가 선 레인이 원으로 뒤덮여 아무것도 안 보인다.
//
// 발밑 고리(SelectionIndicator)와는 다른 것이다. 그쪽은 몸집만 한 작은 원으로 등급을
// 말하고, 이쪽은 사거리만 한 큰 원으로 "어디까지 때리는가"를 말한다.
[RequireComponent(typeof(LineRenderer))]
public class AttackRangeIndicator : MonoBehaviour
{
    const int Segments = 48;          // 사거리 원은 크다. 32이면 각져 보인다.
    const float LineWidth = 0.35f;
    const float HeightOffset = 0.08f; // 발밑 고리(0.05)보다 살짝 위 — 겹칠 때 이쪽이 보인다.

    static readonly Color RangeColor = new Color(1f, 1f, 1f, 0.35f);
    // A(공격) 대기 중엔 진하게 — 「이만큼 때린다」를 보고 찍게(사장님 10-03 「a키 누르면 공격이잖아 유닛 공격 범위 추가」).
    static readonly Color AttackModeColor = new Color(1f, 0.45f, 0.2f, 0.85f);
    static readonly Color PulseColor = new Color(1f, 0.6f, 0.3f, 0.6f);
    // 선 굵기는 반지름에 비례 — 이 맵 사거리는 100~250 단위라 고정 0.35·0.6은 실밥처럼 보였다(10-03 사진).
    const float WidthPerRadius = 0.005f;
    const float AttackModeWidthPerRadius = 0.014f;
    float Width(float perRadius, float min) => Mathf.Max(min, drawnRadius * perRadius);

    // SelectionIndicator와 같은 이유로 한 장을 모두가 함께 쓴다 — 유닛마다 머티리얼을
    // 만들면 드로우콜이 그만큼 늘고 해제되지도 않는다.
    static Material sharedMaterial;

    static Material LineMaterial
    {
        get
        {
            if (sharedMaterial != null) return sharedMaterial;

            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");

            sharedMaterial = new Material(shader) { name = "AttackRangeIndicator (shared)" };
            return sharedMaterial;
        }
    }

    LineRenderer line;
    LineRenderer pulse;   // 공격 대기 중에만 — 바깥에서 안쪽으로 줄어드는 물결(TargetAreaIndicator와 같은 말투)
    UnitAttacker attacker;
    NetEntity mirror;     // MP 클라 겉모습엔 UnitAttacker가 없다 — 거울이 싣고 온 호스트 사거리(GameHud 정보칸과 같은 길)
    float drawnRadius = -1f;
    bool attackMode;
    bool wanted;          // 선택돼 켜 달라는 상태 — 사거리를 못 읽은 프레임에도 기억해 둔다

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.sharedMaterial = LineMaterial;
        line.widthMultiplier = LineWidth;
        line.startColor = RangeColor;
        line.endColor = RangeColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    // 🔴 10-03: 예전엔 켜는 순간 사거리를 한 번 읽고 0이면 원을 꺼 버렸다. Awake에서 찾은 UnitAttacker가 비어 있던 탓에
    //    (Awake 캐시 함정 — memory spawn-order-awake-cache) 선택해도 원이 한 번도 안 그려졌다(실측: 켜짐 False · 점 전부 0).
    //    이제 켜 달라는 상태만 기억하고 매 프레임 사거리를 다시 읽어 그린다.
    public void SetVisible(bool visible)
    {
        wanted = visible;
        if (!visible)
        {
            line.enabled = false;
            if (pulse != null) pulse.enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!wanted) return;
        Rebuild();   // 선택해 둔 채 강화·버프로 사거리가 바뀌어도 따라간다(같으면 바로 돌아간다)
        if (!line.enabled) { if (pulse != null) pulse.enabled = false; return; }
        // 사장님 10-04: 사거리 원은 A(공격 대기)를 눌렀을 때만 — 그냥 선택만 했을 땐 안 그린다.
        if (!SelectionManager.AttackModeActive) { line.enabled = false; if (pulse != null) pulse.enabled = false; return; }

        bool wantAttack = SelectionManager.AttackModeActive;
        if (wantAttack != attackMode)
        {
            attackMode = wantAttack;
            Color c = attackMode ? AttackModeColor : RangeColor;
            line.startColor = c;
            line.endColor = c;
            line.widthMultiplier = attackMode ? Width(AttackModeWidthPerRadius, 0.6f) : Width(WidthPerRadius, LineWidth);
            if (pulse != null) pulse.widthMultiplier = Width(AttackModeWidthPerRadius, 0.6f) * 0.6f;
            if (attackMode && pulse == null) pulse = CreatePulse();
        }
        if (pulse != null) pulse.enabled = attackMode;

        if (attackMode && pulse != null && drawnRadius > 0f)
        {
            float t = Mathf.Repeat(Time.unscaledTime / 1.2f, 1f);
            float r = drawnRadius * Mathf.Lerp(1f, 0.2f, t);
            for (int i = 0; i < Segments; i++)
            {
                float angle = (float)i / Segments * Mathf.PI * 2f;
                pulse.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, HeightOffset, Mathf.Sin(angle) * r));
            }
            Color c = PulseColor;
            c.a *= 1f - t;
            pulse.startColor = c;
            pulse.endColor = c;
        }
    }

    LineRenderer CreatePulse()
    {
        GameObject obj = new GameObject("Pulse", typeof(LineRenderer));
        obj.transform.SetParent(transform, false);
        LineRenderer p = obj.GetComponent<LineRenderer>();
        p.useWorldSpace = false;
        p.loop = true;
        p.positionCount = Segments;
        p.sharedMaterial = LineMaterial;
        p.widthMultiplier = Width(AttackModeWidthPerRadius, 0.6f) * 0.6f;
        p.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        p.receiveShadows = false;
        return p;
    }

    float CurrentRange()
    {
        // 늦게 붙는 부품이 있어 비어 있으면 그때 다시 찾는다(Awake 캐시 금지).
        if (attacker == null) attacker = GetComponentInParent<UnitAttacker>(true);
        if (attacker == null && mirror == null) mirror = GetComponentInParent<NetEntity>(true);
        if (attacker != null) return attacker.AttackRange;
        // MP: 거울이 방금 사라진 프레임엔 [Networked] 값을 읽으면 예외다(GameHud와 같은 가드).
        if (mirror != null && mirror.Object != null && mirror.Object.IsValid) return mirror.AttackRange;
        return 0f;
    }

    // 사거리는 강화·버프로 바뀐다. 켤 때마다 지금 값으로 다시 그린다 —
    // 한 번 그리고 캐시하면 강화한 뒤에도 옛 원이 남는다.
    void Rebuild()
    {
        float radius = CurrentRange();
        line.enabled = radius > 0f;
        if (radius <= 0f) return;

        if (Mathf.Approximately(radius, drawnRadius)) return;
        drawnRadius = radius;
        line.widthMultiplier = attackMode ? Width(AttackModeWidthPerRadius, 0.6f) : Width(WidthPerRadius, LineWidth);

        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius, HeightOffset, Mathf.Sin(angle) * radius));
        }
    }
}
