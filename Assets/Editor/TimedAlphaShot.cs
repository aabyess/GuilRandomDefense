using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 클립 중간 알파(SkinClipHider.timed) 검수 사진: ClaudeBridge/g2_timed.txt(로스터 이름 한 줄씩)의 유닛마다
/// timed가 걸린 클립별로 전환 시각 앞뒤 프레임을 그려 ClaudeBridge/shots/g2_timed_&lt;이름&gt;.png 한 장으로 잇는다(행=클립, 열=시각).
/// 호출: call TimedAlphaShot.Run
/// </summary>
static class TimedAlphaShot
{
    const int Cell = 200, Cols = 6;

    static bool Match(Renderer r, string mesh) =>
        r.gameObject.name == mesh || (r is SkinnedMeshRenderer s && s.sharedMesh != null && s.sharedMesh.name == mesh);

    static string Run()
    {
        string[] names = File.ReadAllLines("ClaudeBridge/g2_timed.txt").Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        var log = new StringBuilder();
        var camGo = new GameObject("TACam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.35f, 0.4f, 0.45f); cam.fieldOfView = 30f; cam.enabled = false;
        var lightGo = new GameObject("TALight") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; lightGo.transform.rotation = Quaternion.Euler(40, 150, 0);
        var rt = new RenderTexture(Cell, Cell, 24); cam.targetTexture = rt;
        var read = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
        try
        {
            foreach (string name in names)
            {
                var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
                if (data == null || data.prefab == null) { log.AppendLine($"❌ {name}: 로스터/프리팹 없음"); continue; }
                var g = (GameObject)Object.Instantiate(data.prefab, new Vector3(0, 9000, 0), Quaternion.identity);
                g.hideFlags = HideFlags.HideAndDontSave;
                var an = g.GetComponentInChildren<Animator>();
                var hider = g.GetComponent<SkinClipHider>();
                if (hider == null || an == null || an.runtimeAnimatorController == null) { log.AppendLine($"❌ {name}: hider/animator 없음"); Object.DestroyImmediate(g); continue; }
                var clipsWithTimed = hider.timed.Select(t => t.clip).Distinct().ToArray();
                var sheet = new Texture2D(Cell * Cols, Cell * Mathf.Max(1, clipsWithTimed.Length), TextureFormat.RGB24, false);
                var all = an.runtimeAnimatorController.animationClips;
                for (int y = 0; y < clipsWithTimed.Length; y++)
                {
                    string cn = clipsWithTimed[y];
                    AnimationClip clip = all.FirstOrDefault(c => c.name == cn);
                    var ts = new SortedSet<float>();
                    if (clip != null)
                    {
                        ts.Add(0.03f);
                        foreach (var tm in hider.timed.Where(t => t.clip == cn)) foreach (float t in tm.times) { ts.Add(Mathf.Max(0.0f, t - 0.04f)); ts.Add(Mathf.Min(clip.length, t + 0.04f)); }
                        ts.Add(clip.length - 0.03f);
                    }
                    float[] times = ts.ToArray();
                    if (times.Length > Cols) times = Enumerable.Range(0, Cols).Select(i => times[Mathf.RoundToInt(i * (times.Length - 1f) / (Cols - 1f))]).ToArray();
                    log.AppendLine($"{name} {cn}: 시각 {string.Join(",", times.Select(t => t.ToString("F2")))} / 전체 {ts.Count}");
                    for (int x = 0; x < Cols; x++)
                    {
                        Color[] fill = new Color[Cell * Cell]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.15f, 0.15f, 0.15f);
                        if (clip != null && x < times.Length)
                        {
                            float t = times[x];
                            var rends = g.GetComponentsInChildren<Renderer>(true);
                            foreach (Renderer r in rends) r.enabled = true;
                            foreach (var e in hider.entries) if (e.clip == cn) foreach (Renderer r in rends) if (e.meshes.Any(m => Match(r, m))) r.enabled = false;
                            foreach (var tm in hider.timed.Where(q => q.clip == cn))
                            {
                                bool vis = tm.startVisible;
                                for (int i = 0; i < tm.times.Length; i++) { if (tm.times[i] <= t) vis = tm.visible[i]; else break; }
                                foreach (Renderer r in rends) if (Match(r, tm.mesh)) r.enabled = vis;
                            }
                            clip.SampleAnimation(an.gameObject, t);
                            Bounds? b = null;
                            foreach (Renderer r in rends) if (r.enabled) { if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; if (b == null) b = r.bounds; else { var q = b.Value; q.Encapsulate(r.bounds); b = q; } }
                            if (b != null)
                            {
                                float rad = Mathf.Max(0.1f, b.Value.extents.magnitude);
                                float dist = rad / Mathf.Sin(15f * Mathf.Deg2Rad) * 1.05f;
                                cam.transform.position = b.Value.center + new Vector3(0f, 0.25f, 1f).normalized * dist;
                                cam.transform.LookAt(b.Value.center); cam.nearClipPlane = dist * 0.05f; cam.farClipPlane = dist * 4;
                                cam.Render();
                                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); read.Apply(); RenderTexture.active = null;
                                fill = read.GetPixels();
                            }
                        }
                        sheet.SetPixels(x * Cell, (clipsWithTimed.Length - 1 - y) * Cell, Cell, Cell, fill);
                    }
                }
                File.WriteAllBytes($"ClaudeBridge/shots/g2_timed_{name}.png", sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet); Object.DestroyImmediate(g);
            }
        }
        finally { Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(read); }
        return log.ToString();
    }
}
