using System.Collections.Generic;
using UnityEngine;

/// <summary>S2 실루엣(진화체·오로성)용 — 모델의 원래 재질·텍스처를 단색 재질로 바꿔 「검은 실루엣」을 만들고(0=검정 → 1=흰색 쪽), 다 차오르면 원래 재질을 되돌린다.
/// 원작은 SetUnitVertexColor로 곱했지만 우리 재질(텍스처 있는 URP Lit)에선 프로퍼티 블록 색이 안 먹어(10-18 실측: 색이 그대로 보였다) 재질을 바꾼다.</summary>
public class S2Silhouette
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Material[]> originals = new List<Material[]>();
    readonly List<Material> mine = new List<Material>();

    public S2Silhouette(Renderer[] all)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (Renderer r in all)
        {
            if (r == null || r is ParticleSystemRenderer || r.sharedMaterials.Length == 0) continue;
            renderers.Add(r);
            originals.Add(r.sharedMaterials);
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = new Material(lit != null ? lit : r.sharedMaterials[0].shader);
                m.SetColor(BaseColor, Color.black);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                mats[i] = m; mine.Add(m);
            }
            r.sharedMaterials = mats;
        }
    }

    public void Paint(float v) { foreach (Material m in mine) if (m != null) m.SetColor(BaseColor, new Color(v, v, v, 1f)); }

    public void Restore()
    {
        for (int i = 0; i < renderers.Count; i++) if (renderers[i] != null) renderers[i].sharedMaterials = originals[i];
        foreach (Material m in mine) if (m != null) Object.Destroy(m);
        mine.Clear();
    }
}
