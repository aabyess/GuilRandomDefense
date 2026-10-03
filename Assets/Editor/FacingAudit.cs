using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 모델 정면 점검(읽기 전용 — Assets에 아무것도 안 쓴다, 결과는 ClaudeBridge/outbox/facing_audit.txt).
/// 게임은 유닛 정면을 +Z로 가정한다(LookRotation). Assets/Prefabs/Generated의 Unit_*·Mob_* 프리팹을 임시로 세워 루트 회전 0 상태에서
/// 어느 쪽을 보는지 잰다. 쓰기: `call FacingAudit.Run` (판 없는 에디터에서).
///   · Humanoid 아바타가 있으면 오른 팔·왼 팔 뼈 위치로: 정면 = Cross(오른쪽 벡터, 위) (유니티 좌표: 오른쪽 +X · 위 +Y → 정면 +Z).
///   · 없으면 뼈 이름(오른/왼 위팔·허벅지·어깨·발)에서 같은 식으로. 그래도 못 찾으면 「?」.
/// 판정: +Z(정상) · −Z(180° 어긋남) · +X/−X(옆) · ?(못 잼).
/// </summary>
public static class FacingAudit
{
    const string Folder = "Assets/Prefabs/Generated";

    static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 이름\t종류\t방법\t정면\t판정");
        int total = 0, ok = 0, back = 0, side = 0, unknown = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            string kind = name.StartsWith("Unit_") ? "유닛" : name.StartsWith("Mob_") ? "적" : "기타";
            if (kind == "기타") continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            GameObject inst = Object.Instantiate(prefab);
            inst.transform.SetPositionAndRotation(new Vector3(0f, -20000f, 0f), Quaternion.identity);
            try
            {
                string method;
                Vector3? forward = Measure(inst, out method);
                string verdict = "?";
                string text = "-";
                if (forward.HasValue)
                {
                    Vector3 f = forward.Value; f.y = 0f;
                    if (f.sqrMagnitude < 1e-6f) { verdict = "?"; }
                    else
                    {
                        f.Normalize(); text = f.ToString("F2");
                        verdict = f.z > 0.7f ? "+Z" : f.z < -0.7f ? "-Z" : f.x > 0f ? "+X" : "-X";
                    }
                }
                total++;
                if (verdict == "+Z") ok++; else if (verdict == "-Z") back++; else if (verdict == "?") unknown++; else side++;
                sb.AppendLine($"{name}\t{kind}\t{method}\t{text}\t{verdict}");
            }
            finally { Object.DestroyImmediate(inst); }
        }
        sb.Insert(0, $"# 총 {total} · +Z {ok} · -Z {back} · 옆 {side} · 못 잼 {unknown}\n");
        string outPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ClaudeBridge/outbox/facing_audit.txt");
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        return $"모델 정면 점검: 총 {total} · +Z {ok} · -Z {back} · 옆 {side} · 못 잼 {unknown} → ClaudeBridge/outbox/facing_audit.txt";
    }

    static Vector3? Measure(GameObject root, out string method)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            Transform r = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform l = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (r == null || l == null) { r = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg); l = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); }
            if (r != null && l != null) { method = "Humanoid"; return Vector3.Cross(r.position - l.position, Vector3.up); }
        }
        // 이름으로: 같은 뼈 종류의 오른/왼 쪽 쌍
        string[] kinds = { "upperarm", "uparm", "shoulder", "clavicle", "thigh", "upleg", "upperleg", "arm", "leg", "foot" };
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (string kind in kinds)
        {
            Transform r = null, l = null;
            foreach (Transform t in all)
            {
                string n = t.name.ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "");
                if (!n.Contains(kind)) continue;
                if (n.Contains("twist") || n.Contains("roll") || n.Contains("fore") || n.Contains("lower") || n.Contains("hand") || n.Contains("toe")) continue;
                bool right = n.StartsWith("r") && !n.StartsWith("right") ? n.StartsWith("r" + kind) || n.EndsWith("r") : n.Contains("right");
                bool left = n.StartsWith("l") && !n.StartsWith("left") ? n.StartsWith("l" + kind) || n.EndsWith("l") : n.Contains("left");
                if (right && r == null) r = t;
                else if (left && l == null) l = t;
            }
            if (r != null && l != null && (r.position - l.position).sqrMagnitude > 1e-6f)
            { method = "이름:" + kind; return Vector3.Cross(r.position - l.position, Vector3.up); }
        }
        method = "없음";
        return null;
    }
}
