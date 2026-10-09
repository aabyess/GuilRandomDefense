using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 클립 안에서 스케일이 거의 0까지 내려가는 뼈 찾기(가프 머리 사라짐 점검, 10-09). 사용: call ClipScaleProbe.Run (ClaudeBridge/g1n_clip.txt = 유닛 에셋 이름)
static class ClipScaleProbe
{
    static string Run()
    {
        string name = System.IO.File.ReadAllText("ClaudeBridge/g1n_clip.txt").Trim();
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        var anim = unit.prefab.GetComponentInChildren<Animator>(true);
        var ctrl = anim.runtimeAnimatorController;
        var sb = new StringBuilder($"{name} 컨트롤러 {ctrl.name}");
        foreach (var clip in ctrl.animationClips.Distinct())
        {
            sb.Append($"\n── 클립 {clip.name} ({clip.length:F2}초)");
            foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("m_LocalScale")))
            {
                var c = AnimationUtility.GetEditorCurve(clip, b);
                float min = c.keys.Min(k => k.value), max = c.keys.Max(k => k.value);
                if (min < 0.2f * Mathf.Max(0.0001f, max) || min < 0.05f) sb.Append($"\n   {b.path}.{b.propertyName} 최소 {min:F3} 최대 {max:F3} 키 {c.keys.Length}");
            }
        }
        return sb.ToString();
    }
}
