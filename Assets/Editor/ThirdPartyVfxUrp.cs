using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 외부 무료 이펙트 팩(Assets/ThirdParty) 재질을 URP로 옮긴다(2026-09-30, 사장님 「이펙트 팩 넣어줘」).
// Hovl Magic Effects FREE는 빌트인 셰이더(Particles/Standard Unlit·Surface, Standard)를 써서 URP에선 분홍색이 된다.
// 유료 「Hovl 지원팩」 없이, 텍스처·색·섞기 방식만 URP Particles/Unlit(또는 Lit)으로 옮긴다.
// Cartoon FX Remaster는 자체 URP 셰이더(.cfxrshader)라 손대지 않는다.
// 부르는 법: ClaudeBridge `call ThirdPartyVfxUrp.ConvertHovl` · 몇 번 돌려도 같은 결과(이미 URP면 건너뜀).
public static class ThirdPartyVfxUrp
{
    const string HovlRoot = "Assets/ThirdParty/Hovl Studio";

    // Hovl 색은 블룸 전제의 HDR(최대 6.3배)이다 — 우리 화면엔 블룸이 없어 그대로 두면 하얗게 탄다.
    const float MaxColorIntensity = 1.5f;

    [MenuItem("Tools/이펙트/외부 팩 재질 URP로 (Hovl)")]
    static void Menu() => Debug.Log(ConvertHovl());

    public static string ConvertHovl()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (unlit == null || lit == null) return "❌ URP 셰이더를 못 찾음";

        var sb = new StringBuilder();
        int converted = 0, skipped = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { HovlRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) continue;
            string from = m.shader != null ? m.shader.name : "(없음)";
            if (from.StartsWith("Universal Render Pipeline")) { skipped++; continue; }

            Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 2f;

            float peak = Mathf.Max(color.r, color.g, color.b);
            if (peak > MaxColorIntensity)
            {
                float k = MaxColorIntensity / peak;
                color = new Color(color.r * k, color.g * k, color.b * k, color.a);
            }

            if (from == "Standard")
            {
                m.shader = lit;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
            }
            else
            {
                m.shader = unlit;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
                ApplyTransparent(m, mode);
            }
            EditorUtility.SetDirty(m);
            converted++;
            sb.AppendLine($"  {System.IO.Path.GetFileName(path)}: {from} → {m.shader.name} (mode {mode})");
        }
        AssetDatabase.SaveAssets();
        return $"✅ Hovl 재질 {converted}개 URP로 · 이미 URP {skipped}개\n" + sb;
    }

    // 빌트인 Particles/Standard의 _Mode: 0 불투명 · 1 컷아웃 · 2 페이드 · 3 투명 · 4 더하기 · 5 빼기 · 6 곱하기.
    // URP Particles의 _Blend: 0 알파 · 1 곱(Premultiply) · 2 더하기 · 3 곱하기.
    static void ApplyTransparent(Material m, float mode)
    {
        bool additive = mode >= 3.5f && mode < 5.5f;
        bool multiply = mode >= 5.5f;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : multiply ? 3f : 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_BLENDMODE_ADD");
        m.DisableKeyword("_BLENDMODE_MULTIPLY");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (additive)
        {
            m.EnableKeyword("_BLENDMODE_ADD");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.One);
        }
        else if (multiply)
        {
            m.EnableKeyword("_BLENDMODE_MULTIPLY");
            m.SetInt("_SrcBlend", (int)BlendMode.DstColor);
            m.SetInt("_DstBlend", (int)BlendMode.Zero);
        }
        else
        {
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }
    }
    public static string ShowCfxr() => Showcase("vfx_cfxr", "Assets/ThirdParty/JMO Assets", "0", "30", "0.35");
    public static string ShowHovl() => Showcase("vfx_hovl", "Assets/ThirdParty/Hovl Studio", "0", "30", "0.6");

    // 점검 사진: 폴더의 이펙트 프리팹을 미리보기 씬에 격자로 세우고 t초만큼 시뮬레이션해 찍는다(씬 무수정).
    //   call ThirdPartyVfxUrp.Showcase <파일> <폴더> <시작> <개수> <초>  → ClaudeBridge/shots/<파일>.png
    public static string Showcase(string file, string folder, string start, string count, string seconds)
    {
        int s0 = int.Parse(start), n = int.Parse(count);
        float t = float.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal)
            .Skip(s0).Take(n).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToList();
        if (prefabs.Count == 0) return "❌ 프리팹 없음";
        Scene preview = EditorSceneManager.NewPreviewScene();
        var table = new StringBuilder();
        const int perRow = 5; const float cell = 7f;
        int rows = (prefabs.Count + perRow - 1) / perRow;
        GameObject cam = new GameObject("ShowcaseCamera");
        RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        Texture2D img = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], preview);
                go.transform.position = new Vector3((i % perRow) * cell, 0f, -(i / perRow) * cell);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                        ps.Simulate(t, true, true, true);
                int pink = go.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Count(m => m == null || m.shader == null || m.shader.name.Contains("Error")
                        || (!m.shader.name.StartsWith("Universal") && !m.shader.name.Contains("CFXR") && !m.shader.name.StartsWith("Hidden")));
                table.AppendLine($"   {i / perRow + 1}행 {i % perRow + 1}열 = {prefabs[i].name}{(pink > 0 ? $"  ⚠️ URP 아닌 재질 {pink}" : "")}");
            }
            SceneManager.MoveGameObjectToScene(cam, preview);
            Camera c = cam.AddComponent<Camera>();
            c.scene = preview;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.18f, 0.2f, 0.22f);
            Vector3 center = new Vector3((perRow - 1) * cell * 0.5f, 1f, -(rows - 1) * cell * 0.5f);
            float span = Mathf.Max(perRow, rows * 1.6f) * cell;
            c.transform.position = center + new Vector3(0f, span * 0.45f, span * 0.75f);
            c.transform.LookAt(center);
            c.fieldOfView = 50f; c.nearClipPlane = 0.1f; c.farClipPlane = 1000f;
            c.targetTexture = rt;
            c.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            img.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); img.Apply();
            RenderTexture.active = prev;
            c.targetTexture = null;
            string path = System.IO.Path.Combine(Application.dataPath, "..", "ClaudeBridge", "shots", file + ".png");
            System.IO.File.WriteAllBytes(path, img.EncodeToPNG());
            return $"✅ {path}\n" + table;
        }
        finally
        {
            c_cleanup(rt, img);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void c_cleanup(RenderTexture rt, Texture2D img)
    {
        Object.DestroyImmediate(img);
        rt.Release(); Object.DestroyImmediate(rt);
    }
}
