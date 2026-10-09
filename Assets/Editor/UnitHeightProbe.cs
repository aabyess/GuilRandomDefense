using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>등급 접두사 유닛의 게임 안 키 실측(사장님 10-09): 프리팹을 세워 Idle 첫 프레임에서 렌더러 경계 높이를 잰다. ClaudeBridge/g2_hprefix.txt(기본 초월_) · call UnitHeightProbe.Run</summary>
static class UnitHeightProbe
{
    static string Run()
    {
        string prefix = File.Exists("ClaudeBridge/g2_hprefix.txt") ? File.ReadAllText("ClaudeBridge/g2_hprefix.txt").Trim() : "초월_";
        var sb = new StringBuilder("유닛\t높이\t가로\t깊이\t몸비율(p97)\t몸높이\n");
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!System.Text.RegularExpressions.Regex.IsMatch(name.Normalize(NormalizationForm.FormC), "^(" + prefix.Normalize(NormalizationForm.FormC) + ")") || data == null || data.prefab == null) continue;
            GameObject g = (GameObject)Object.Instantiate(data.prefab, new Vector3(0, 9000, 0), Quaternion.identity);
            g.hideFlags = HideFlags.HideAndDontSave;
            var an = g.GetComponentInChildren<Animator>();
            if (an != null && an.runtimeAnimatorController != null)
            {
                AnimationClip idle = an.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Idle") ?? an.runtimeAnimatorController.animationClips.FirstOrDefault();
                if (idle != null) idle.SampleAnimation(an.gameObject, 0f);
            }
            Bounds? b = null;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer)) { if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true; if (b == null) b = r.bounds; else { var q = b.Value; q.Encapsulate(r.bounds); b = q; } }
            float frac = 1f;
            var ys = new System.Collections.Generic.List<float>();
            foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>()) { if (smr.sharedMesh == null || !smr.enabled) continue; foreach (var v in smr.sharedMesh.vertices) ys.Add(v.y); }
            if (ys.Count > 10) { ys.Sort(); float mn = ys[0], mx = ys[ys.Count - 1]; float p = ys[Mathf.Min(ys.Count - 1, (int)(ys.Count * 0.97f))]; frac = mx - mn > 1e-5f ? (p - mn) / (mx - mn) : 1f; }
            sb.AppendLine(b == null ? name + "\t없음" : $"{name}\t{b.Value.size.y:F1}\t{b.Value.size.x:F1}\t{b.Value.size.z:F1}\t{frac:F3}\t{b.Value.size.y * frac:F1}");
            Object.DestroyImmediate(g);
        }
        return sb.ToString();
    }
}
