using UnityEngine;

/// <summary>
/// 퇴치 의뢰 미니보스의 겉모습(2026-10-08 사장님: 새 모델 없이 자리표시 모델의 크기·색으로 라인몹과 구별) —
/// 몸 틴트(의뢰마다 다른 색) + 발밑 원판. 크기는 PirateQuestManager가 곱한다(QuestMobScale).
/// 색은 광폭화(붉은 틴트·붉은 원판)와 안 헷갈리게 붉은·주황 계열을 쓰지 않는다.
/// 호스트 실물(PirateQuestManager가 붙임)과 MP 클라 겉모습(NetEntity.QuestMobTint = 의뢰 번호+1) 둘 다에 붙는다.
/// </summary>
public class QuestMobLook : MonoBehaviour
{
    public const float QuestMobScale = 1.5f;   // 일반 라인몹(~39) < 퀘스트 미니보스(~58) < 광폭화(~72) < 보스(목표 96)
    const float TintAmount = 0.6f;
    const float DiscWorldRadius = 12f;

    // 의뢰 이름 → 색(순서가 곧 번호: NetEntity.QuestMobTint = 번호+1, 0 = 해당 없음). 7종 모두 붉은 계열(색상 0~0.08, 0.95~1)을 피한다.
    static readonly (string quest, Color color)[] Palette =
    {
        ("신림패거리",     new Color(0.10f, 0.85f, 0.85f)),   // 청록
        ("김선우",         new Color(0.20f, 0.40f, 1.00f)),   // 파랑
        ("허브수경비원",   new Color(0.25f, 0.90f, 0.25f)),   // 초록
        ("이영용",         new Color(0.65f, 0.30f, 1.00f)),   // 보라
        ("조규룡",         new Color(1.00f, 0.85f, 0.15f)),   // 금색
        ("박성호",         new Color(1.00f, 0.45f, 0.85f)),   // 분홍보라
        ("배고픈황정기",   new Color(0.95f, 0.95f, 0.95f)),   // 흰색
    };

    /// <summary>의뢰 이름 → 틴트 번호(1~7). 표에 없으면 1(청록).</summary>
    public static byte TintIndexFor(string questName)
    {
        for (int i = 0; i < Palette.Length; i++) if (Palette[i].quest == questName) return (byte)(i + 1);
        return 1;
    }

    public byte TintIndex { get; private set; } = 1;
    Color tint => Palette[Mathf.Clamp(TintIndex, 1, Palette.Length) - 1].color;

    public QuestMobLook Begin(byte tintIndex) { TintIndex = tintIndex; return this; }

    void Start()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            foreach (Material m in r.materials)
                foreach (string prop in new[] { "_BaseColor", "_Color" })
                    if (m.HasProperty(prop)) m.SetColor(prop, Color.Lerp(m.GetColor(prop), tint, TintAmount));
        }
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "QuestMobDisc";
        quad.transform.SetParent(transform, false);
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
        quad.transform.localPosition = new Vector3(0f, 0.4f / parentScale, 0f);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = Vector3.one * (DiscWorldRadius * 2f / parentScale);
        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color c = tint;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, d >= 1f ? 0f : Mathf.Lerp(0.7f, 0.2f, d)));
            }
        tex.Apply();
        var rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial = new Material(sh) { mainTexture = tex, name = "QuestMobDisc" };
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }
}
