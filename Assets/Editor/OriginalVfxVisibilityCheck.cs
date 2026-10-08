using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원작 이펙트 「보이는가」 자동 검사(PM 10-08 「거기 있다 ≠ 보인다」): Resources/Effects/Original의 프리팹마다 재생 중 다섯 시점의 모습을
/// 편집 모드에서 임시 카메라로 렌더해(회색 배경) 배경과 다른 픽셀 비율을 잰다. 가장 큰 값이 1% 미만이면 「안 보임」.
/// 결과: Docs/research/ORIGINAL_VFX_VISIBILITY.tsv · 안 보이는 모델 이름 목록 Docs/research/ORIGINAL_VFX_INVISIBLE.txt(SkillVfxTableBuilder가 이 이름 칸은 「유지」로 건너뛴다).
/// 호출: call OriginalVfxVisibilityCheck.Run
/// </summary>
public static class OriginalVfxVisibilityCheck
{
    public const string InvisiblePath = "Docs/research/ORIGINAL_VFX_INVISIBLE.txt";
    const string TsvPath = "Docs/research/ORIGINAL_VFX_VISIBILITY.tsv";
    const int Size = 384;
    const float VisibleFraction = 0.01f;

    [MenuItem("Tools/이펙트/원작 이펙트 보임 검사")]
    static void Menu() => Debug.Log(Run());

    public static string Ring() => Run("az_firering1a");
    public static string Run() => Run(null);

    static string Run(string only)
    {
        if (Application.isPlaying) return "❌ 편집 모드에서만";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { OriginalVfxBuilder.PrefabRoot });
        var tsv = new StringBuilder("모델\t최대비율\t보임\t층수\t재생시간\t경계\n");
        var invisible = new System.Collections.Generic.List<string>();
        Color bg = new Color(0.35f, 0.35f, 0.35f, 1f);
        var camGo = new GameObject("VisCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg; cam.fieldOfView = 30f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 20000f; cam.enabled = false;
        var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        Texture2D readTex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Vector3 origin = new Vector3(0f, 8000f, 0f);
        int done = 0;
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<OriginalVfxPlayer>() == null) continue;
                if (only != null && Path.GetFileNameWithoutExtension(path) != only) continue;
                GameObject go = (GameObject)Object.Instantiate(prefab, origin, Quaternion.identity);
                go.hideFlags = HideFlags.HideAndDontSave;
                OriginalVfxPlayer player = go.GetComponent<OriginalVfxPlayer>();
                // 시점마다 그때의 경계에 카메라를 맞춰(경계는 그린 뒤에야 갱신된다 — 스킨 메시) 배경과 다른 픽셀 비율을 잰다.
                float[] fractions = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };
                float best = 0f; Bounds? lastBounds = null;
                cam.transform.position = origin + new Vector3(0f, 30f, -30f); cam.transform.LookAt(origin);
                foreach (float f in fractions)
                {
                    player.SampleAt(player.duration * f);
                    cam.Render();   // 경계 갱신용
                    Bounds? b = null;
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled && r.bounds.size.sqrMagnitude > 0f) { if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; } }
                    if (b == null) continue;
                    lastBounds = b;
                    float radius = Mathf.Max(0.05f, b.Value.extents.magnitude);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                    Vector3 dir = new Vector3(0.55f, 0.75f, -0.55f).normalized;
                    cam.transform.position = b.Value.center + dir * dist;
                    cam.transform.LookAt(b.Value.center);
                    cam.nearClipPlane = Mathf.Max(0.01f, dist * 0.05f);
                    cam.Render();
                    RenderTexture.active = rt;
                    readTex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); readTex.Apply();
                    RenderTexture.active = null;
                    Color32[] px = readTex.GetPixels32();
                    int diff = 0;
                    foreach (Color32 c in px)
                        if (Mathf.Max(Mathf.Abs(c.r / 255f - bg.r), Mathf.Abs(c.g / 255f - bg.g), Mathf.Abs(c.b / 255f - bg.b)) > 0.04f) diff++;
                    best = Mathf.Max(best, diff / (float)px.Length);
                    if (only != null) Debug.Log($"[보임검사] {only} f={f} 경계 {b.Value.size} 중심 {b.Value.center} 픽셀차 {diff}/{px.Length} 카메라 {cam.transform.position} dist {dist}");
                }
                Bounds? all = lastBounds;
                string name = Path.GetFileNameWithoutExtension(path);
                bool visible = best >= VisibleFraction;
                if (!visible) invisible.Add(name);
                tsv.AppendLine($"{name}\t{best:0.0000}\t{(visible ? "보임" : "안 보임")}\t{player.layers.Length}\t{player.duration:0.00}\t{(all.HasValue ? all.Value.size.ToString("F1") : "없음")}");
                Object.DestroyImmediate(go);
                done++;
            }
        }
        finally
        {
            Object.DestroyImmediate(camGo); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(readTex);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), TsvPath), tsv.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), InvisiblePath), string.Join("\n", invisible.OrderBy(n => n)) + (invisible.Count > 0 ? "\n" : ""), new UTF8Encoding(false));
        return $"✅ 보임 검사 {done}종 · 안 보임 {invisible.Count}종 → {TsvPath}\n안 보임: {string.Join(", ", invisible)}";
    }
}
