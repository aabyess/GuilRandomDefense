using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 날개 6종(~/GRD_wings, blender) → Assets/Art/Wings/ + 프리팹 Assets/Resources/Wings/. 순서: call WingBuilder.Copy → refresh → call WingBuilder.Build.
/// 재질: wing.json의 mesh별 filter(additive/addalpha=가산 · blend=알파블렌드 · transparent=알파컷) + 텍스처. 지오셋 알파 키·UV 이동은 아직 안 쓴다(정지 알파 1).
/// </summary>
static class WingBuilder
{
    static readonly string Src = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_wings");
    const string Dst = "Assets/Art/Wings";

    static string Copy()
    {
        int n = 0;
        foreach (string dir in Directory.GetDirectories(Src).Where(d => !Path.GetFileName(d).StartsWith("_")))
        {
            string name = Path.GetFileName(dir);
            string to = $"{Dst}/{name}";
            Directory.CreateDirectory(to + "/Textures");
            File.Copy($"{dir}/{name}.fbx", $"{to}/{name}.fbx", true);
            File.Copy($"{dir}/wing.json", $"{to}/wing.json", true);
            foreach (string t in Directory.GetFiles(dir + "/Textures")) File.Copy(t, $"{to}/Textures/{Path.GetFileName(t)}", true);
            n++;
        }
        return $"{n}종 복사 — 이제 refresh 뒤 WingBuilder.Settings";
    }

    // FBX 임포트 설정: 재질 안 만들고, 클립 루프.
    static string Settings()
    {
        int n = 0;
        foreach (string fbx in Directory.GetFiles(Dst, "*.fbx", SearchOption.AllDirectories))
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx.Replace('\\', '/'));
            if (imp == null) continue;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips) c.loopTime = true;
            imp.clipAnimations = clips;
            imp.SaveAndReimport(); n++;
        }
        return $"{n}종 임포트 설정";
    }

    static Material MakeMaterial(string path, string filter, Texture2D tex)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Cull", 0f);
        if (filter == "transparent")
        {
            mat.SetFloat("_Surface", 0f); mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = 2450;
        }
        else
        {
            mat.SetFloat("_Surface", 1f); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent"); mat.renderQueue = 3000; mat.SetFloat("_ZWrite", 0f);
            bool add = filter == "additive" || filter == "addalpha";
            mat.SetFloat("_Blend", add ? 2f : 0f);
            mat.SetFloat("_SrcBlend", 5f); mat.SetFloat("_DstBlend", add ? 1f : 10f);
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static string Build()
    {
        Directory.CreateDirectory("Assets/Resources/Wings");
        var sb = new System.Text.StringBuilder();
        foreach (string dir in Directory.GetDirectories(Dst))
        {
            string name = Path.GetFileName(dir);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dst}/{name}/{name}.fbx");
            if (fbx == null) { sb.AppendLine("❌ FBX 없음 " + name); continue; }
            var json = JsonUtility.FromJson<WingJson>(File.ReadAllText($"{Dst}/{name}/wing.json"));
            Directory.CreateDirectory($"{Dst}/{name}/Materials");
            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "모델";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var spec = json.materials.FirstOrDefault(m => r.gameObject.name == m.mesh || r.gameObject.name.StartsWith(m.mesh));
                if (spec == null) { sb.AppendLine($"   ⚠ {name}: 재질 정보 없는 렌더러 {r.gameObject.name}"); continue; }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dst}/{name}/Textures/{spec.textureFile}");
                if (tex == null) { sb.AppendLine($"   ⚠ {name}: 텍스처 없음 {spec.textureFile}"); continue; }
                r.sharedMaterial = MakeMaterial($"{Dst}/{name}/Materials/{spec.mesh}.mat", spec.filter, tex);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            // 날갯짓 클립 → 컨트롤러
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{Dst}/{name}/{name}.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            var animator = model.GetComponentInChildren<Animator>() ?? root.AddComponent<Animator>();
            if (clip != null)
            {
                string cp = $"{Dst}/{name}/{name}.controller";
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(cp);
                var st = ctrl.layers[0].stateMachine.AddState("날갯짓"); st.motion = clip; ctrl.layers[0].stateMachine.defaultState = st;
                animator.runtimeAnimatorController = ctrl;
            }
            else sb.AppendLine($"   ⚠ {name}: 클립 없음");
            // 휴식 폭
            Bounds b = default; bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            var wm = root.AddComponent<WingModel>();
            wm.restSpan = any ? Mathf.Max(b.size.x, b.size.y, b.size.z) : 3.7f;
            // 유니티로 들어온 날개 축: 폭=Z(±1.86) · 위아래=X · 앞뒤 두께=Y(+가 등쪽) → 유닛 로컬로 돌린다: X→위(Y) · Y→뒤(-Z) · Z→옆(-X). 폭은 좌우 대칭이라 부호 무관.
            wm.localEuler = Quaternion.LookRotation(new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, -1f)).eulerAngles;
            PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Resources/Wings/{name}.prefab");
            sb.AppendLine($"✅ {name} · 렌더러 {model.GetComponentsInChildren<Renderer>(true).Length} · 클립 {(clip != null ? clip.name : "없음")} · 휴식 크기 {(any ? b.size.ToString("F2") : "?")} 폭 {wm.restSpan:F2}");
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    [System.Serializable] class WingJson { public MatSpec[] materials; }
    [System.Serializable] class MatSpec { public string mesh; public string filter; public string textureFile; }
}
