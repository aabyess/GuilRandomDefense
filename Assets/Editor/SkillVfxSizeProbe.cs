using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>원작 모델 이펙트(OriginalVfxPlayer) 프리팹의 실제 가로 지름을 재서 SkillVfxTable.nativeSizes와 비교한다(10-14 황준석 외교관 이펙트가 맵을 덮은 건 조사).
/// call SkillVfxSizeProbe.Report → 표와 실측 차이 / call SkillVfxSizeProbe.Fix → 표의 nativeSizes를 실측으로 덮고 저장.</summary>
static class SkillVfxSizeProbe
{
    static string Run(bool fix)
    {
        var table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable");
        if (table == null) return "❌ 표 없음";
        var sb = new StringBuilder();
        int bad = 0, changed = 0;
        for (int i = 0; i < table.prefabs.Count; i++)
        {
            GameObject pf = table.prefabs[i];
            if (pf == null || pf.GetComponent<OriginalVfxPlayer>() == null) continue;
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            var player = g.GetComponent<OriginalVfxPlayer>();
            float d = player.clips != null && player.clips.Length > 0 ? player.clips[0].lengthSec : player.duration;
            float maxW = 0f, maxH = 0f;
            for (float t = 0f; t <= Mathf.Max(d, 0.1f) + 1e-4f; t += Mathf.Max(d, 0.1f) / 8f)
            {
                player.SampleAt(t);
                Bounds b = new Bounds(g.transform.position, Vector3.zero); bool any = false;
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (any) { maxW = Mathf.Max(maxW, Mathf.Max(b.size.x, b.size.z)); maxH = Mathf.Max(maxH, b.size.y); }
            }
            Object.DestroyImmediate(g);
            float tableSize = i < table.nativeSizes.Count ? table.nativeSizes[i] : 0f;
            float ratio = maxW > 0.01f ? tableSize / maxW : 0f;
            bool off = maxW > 0.01f && (ratio > 1.5f || ratio < 0.67f);
            if (off) bad++;
            if (off || maxW > 0.01f && Mathf.Abs(tableSize - 8f) < 0.01f)
                sb.AppendLine($"[{i}] {pf.name}: 표 {tableSize:F2} 실측 가로 {maxW:F2} 높이 {maxH:F2} 길이 {d:F2}s loop={player.loop}{(off ? "  ⚠️ 어긋남" : "")}");
            if (fix && maxW > 0.01f && Mathf.Abs(tableSize - maxW) > 0.01f) { table.nativeSizes[i] = maxW; changed++; }
        }
        if (fix) { EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
        return $"원작 모델 이펙트 어긋남 {bad}건, 수정 {changed}건\n" + sb;
    }
    static string Report() => Run(false);
    static string Fix() => Run(true);
}
