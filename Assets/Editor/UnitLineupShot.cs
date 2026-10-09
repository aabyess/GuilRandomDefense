using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>유닛 키 비교 줄세우기 사진(10-09): ClaudeBridge/g2_lineup.txt(첫 줄=출력 이름, 나머지=로스터 이름)를 한 줄로 세워 정면 정사영으로 그린다. 간격 70, 바닥선 포함. call UnitLineupShot.Run</summary>
static class UnitLineupShot
{
    static string Run()
    {
        string[] lines = File.ReadAllLines("ClaudeBridge/g2_lineup.txt").Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        string tag = lines[0]; string[] names = lines.Skip(1).ToArray();
        const float spacing = 70f; Vector3 origin = new Vector3(0, 9000, 0);
        int W = Mathf.RoundToInt(names.Length * 110f), H = 300;
        var camGo = new GameObject("LineCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.78f, 0.82f, 0.86f); cam.enabled = false;
        float widthWorld = names.Length * spacing; cam.orthographicSize = widthWorld * H / W * 0.5f;
        cam.transform.position = origin + new Vector3(widthWorld * .5f - spacing * .5f, cam.orthographicSize - 12f, 200f); cam.transform.rotation = Quaternion.Euler(0, 180, 0);
        cam.nearClipPlane = 1f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt;
        var lightGo = new GameObject("LineLight") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; lightGo.transform.rotation = Quaternion.Euler(25, 160, 0);
        var objs = new System.Collections.Generic.List<GameObject>();
        // 눈금: 10단위 가로선, 57.6(기준) 강조
        foreach (float y in new[] { 0f, 57.6f, 86.4f })
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.hideFlags = HideFlags.HideAndDontSave; Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.position = origin + new Vector3(widthWorld * .5f - spacing * .5f, y, -60f); bar.transform.localScale = new Vector3(widthWorld, y == 0f ? 1.2f : 0.5f, 0.5f);
            bar.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = y == 0f ? Color.black : (y == 57.6f ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.1f, 0.4f, 0.8f)) };
            objs.Add(bar);
        }
        for (int i = 0; i < names.Length; i++)
        {
            var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{names[i]}.asset");
            if (data == null || data.prefab == null) continue;
            GameObject g = (GameObject)Object.Instantiate(data.prefab, origin + new Vector3(i * spacing, 0, 0), Quaternion.Euler(0, 0, 0));
            g.hideFlags = HideFlags.HideAndDontSave; objs.Add(g);
            var an = g.GetComponentInChildren<Animator>();
            if (an != null && an.runtimeAnimatorController != null) { var idle = an.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Idle") ?? an.runtimeAnimatorController.animationClips.FirstOrDefault(); if (idle != null) idle.SampleAnimation(an.gameObject, 0f); }
            foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        }
        cam.Render(); cam.Render();
        var read = new Texture2D(W, H, TextureFormat.RGB24, false);
        RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, W, H), 0, 0); read.Apply(); RenderTexture.active = null;
        File.WriteAllBytes($"ClaudeBridge/shots/g2_lineup_{tag}.png", read.EncodeToPNG());
        foreach (var o in objs) Object.DestroyImmediate(o);
        Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(read);
        return $"✅ {tag} {names.Length}기 {W}x{H}";
    }
}
