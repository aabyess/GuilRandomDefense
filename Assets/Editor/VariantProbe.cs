using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>변형 클립 무작위 재생 확인(플레이 중): 로스터 프리팹 24개를 세워 PlayAttack/PlaySpell을 부르고 0.3초 뒤 상태(클립 이름) 분포를 센다.
/// gameshot `call:VariantProbe.Fire wait:0.4 call:VariantProbe.Report` · 대상은 ClaudeBridge/g2_variant.txt(로스터 이름)</summary>
static class VariantProbe
{
    static readonly List<GameObject> spawned = new List<GameObject>();

    static readonly Dictionary<GameObject, HashSet<string>> seen = new Dictionary<GameObject, HashSet<string>>();
    static bool hooked;

    static void Sample()
    {
        foreach (GameObject g in spawned)
        {
            if (g == null) continue;
            var an = g.GetComponentInChildren<Animator>();
            if (an == null) continue;
            if (!seen.TryGetValue(g, out var set)) seen[g] = set = new HashSet<string>();
            foreach (var c in an.GetCurrentAnimatorClipInfo(0)) set.Add(c.clip.name);
            foreach (var c in an.GetNextAnimatorClipInfo(0)) set.Add(c.clip.name);
        }
    }

    static string Fire()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string name = System.IO.File.ReadAllText("ClaudeBridge/g2_variant.txt").Trim();
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        if (data == null || data.prefab == null) return "❌ 로스터 없음";
        foreach (GameObject g in spawned) if (g != null) Object.Destroy(g);
        spawned.Clear(); seen.Clear();
        if (!hooked) { UnityEditor.EditorApplication.update += Sample; hooked = true; }
        for (int i = 0; i < 24; i++)
        {
            var g = (GameObject)Object.Instantiate(data.prefab, new Vector3(9000 + i * 60, 0, 9000), Quaternion.identity);
            foreach (Animator a in g.GetComponentsInChildren<Animator>()) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 화면 밖이면 상태가 안 넘어간다
            spawned.Add(g);
        }
        return $"{name} 24기 세움";
    }

    static string FireAttack()
    {
        foreach (var kv in seen) kv.Value.Clear();
        foreach (GameObject g in spawned) if (g != null) g.GetComponent<CharacterAnimator>()?.PlayAttack();
        return "평타 호출";
    }

    static string FireSpell()
    {
        foreach (var kv in seen) kv.Value.Clear();
        foreach (GameObject g in spawned) if (g != null) g.GetComponent<CharacterAnimator>()?.PlaySpell();
        return "스킬 호출";
    }

    static string Report()
    {
        var count = new SortedDictionary<string, int>();
        foreach (GameObject g in spawned)
        {
            if (g == null) continue;
            string key = seen.TryGetValue(g, out var set) ? string.Join("+", set.Where(x => x != "Idle").OrderBy(x => x)) : "?";
            if (key.Length == 0) key = "(Idle만)";
            count[key] = count.TryGetValue(key, out int n) ? n + 1 : 1;
        }
        var sb = new StringBuilder();
        foreach (var kv in count) sb.Append($"{kv.Key}={kv.Value} ");
        return sb.ToString();
    }
}
