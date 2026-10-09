using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스킨 교체 동작 검수 시트(10-09): ClaudeBridge/g2_motion.txt(로스터 이름 한 줄씩)의 유닛마다 Idle·Move·Attack·Spell 클립을
/// 25/50/75% 시점으로 편집 모드에서 그려 한 장(ClaudeBridge/shots/g2_motion_&lt;이름&gt;.png)에 이어 붙인다. 경계 크기도 로그로 낸다.
/// 호출: call SwapMotionSheet.Run
/// </summary>
static class SwapMotionSheet
{
    const int Cell = 240;
    static readonly string[] Kinds = { "Idle", "Move", "Attack", "Spell" };

    static string Run()
    {
        string[] names = File.ReadAllLines("ClaudeBridge/g2_motion.txt").Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        var log = new StringBuilder();
        var camGo = new GameObject("MotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.35f, 0.4f, 0.45f); cam.fieldOfView = 30f; cam.enabled = false;
        var lightGo = new GameObject("MotLight") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; lightGo.transform.rotation = Quaternion.Euler(40, 150, 0);
        var rt = new RenderTexture(Cell, Cell, 24); cam.targetTexture = rt;
        var read = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
        Vector3 origin = new Vector3(0, 9000, 0);
        try
        {
            foreach (string name in names)
            {
                var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
                if (data == null || data.prefab == null) { log.AppendLine($"❌ {name}: 로스터/프리팹 없음"); continue; }
                GameObject g = (GameObject)Object.Instantiate(data.prefab, origin, Quaternion.identity);
                g.hideFlags = HideFlags.HideAndDontSave;
                var an = g.GetComponentInChildren<Animator>();
                var hider = g.GetComponent<SkinClipHider>();
                AnimationClip[] clips = an != null && an.runtimeAnimatorController != null ? an.runtimeAnimatorController.animationClips : new AnimationClip[0];
                var present = Kinds.Where(k => clips.Any(c => c.name == k)).ToArray();
                var sheet = new Texture2D(Cell * 3, Cell * Mathf.Max(1, Kinds.Length), TextureFormat.RGB24, false);
                for (int y = 0; y < Kinds.Length; y++)
                {
                    AnimationClip clip = clips.FirstOrDefault(c => c.name == Kinds[y]);
                    for (int x = 0; x < 3; x++)
                    {
                        Color[] fill = new Color[Cell * Cell]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.15f, 0.15f, 0.15f);
                        if (clip != null && an != null)
                        {
                            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                            if (hider != null) foreach (var e in hider.entries) if (e.clip == clip.name) foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) if (e.meshes.Contains(r.gameObject.name) || (r is SkinnedMeshRenderer s && s.sharedMesh != null && e.meshes.Contains(s.sharedMesh.name))) r.enabled = false;
                            clip.SampleAnimation(an.gameObject, clip.length * (0.25f + 0.25f * x));
                            Bounds? b = null;
                            foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) if (r.enabled) { if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; if (b == null) b = r.bounds; else { var q = b.Value; q.Encapsulate(r.bounds); b = q; } }
                            if (b != null)
                            {
                                if (x == 1) log.AppendLine($"{name} {clip.name} 경계 {b.Value.size.ToString("F1")}");
                                float rad = Mathf.Max(0.1f, b.Value.extents.magnitude);
                                float dist = rad / Mathf.Sin(15f * Mathf.Deg2Rad) * 1.05f;
                                cam.transform.position = b.Value.center + new Vector3(0.0f, 0.25f, 1f).normalized * dist;
                                cam.transform.LookAt(b.Value.center); cam.nearClipPlane = dist * 0.05f; cam.farClipPlane = dist * 4;
                                cam.Render();
                                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); read.Apply(); RenderTexture.active = null;
                                fill = read.GetPixels();
                            }
                        }
                        sheet.SetPixels(x * Cell, (Kinds.Length - 1 - y) * Cell, Cell, Cell, fill);
                    }
                    if (clip == null) log.AppendLine($"{name} {Kinds[y]} 클립 없음");
                }
                File.WriteAllBytes($"ClaudeBridge/shots/g2_motion_{name}.png", sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet); Object.DestroyImmediate(g);
            }
        }
        finally { Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(read); }
        return log.ToString();
    }
}
