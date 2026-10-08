using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 원작 이펙트 모델 반입(10-08): blender가 ~/GRD_orig_vfx_trial/&lt;모델&gt;/ 에 둔 FBX(30fps 뼈 애니)+Textures+json(unity 블록)을
/// Assets/Art/Effects/Original/&lt;모델&gt;/ 로 복사하고, 층마다 URP 파티클 재질(Additive/AlphaBlend)을 만들어
/// Assets/Resources/Effects/Original/&lt;모델&gt;.prefab(OriginalVfxPlayer 달림)을 짓는다. 다시 불러도 안전하다.
/// 호출: 브리지 `call OriginalVfxBuilder.BuildTrial` (시범 3) 또는 메뉴 Tools/이펙트/원작 이펙트 반입(시범).
/// 스킬 표 연결은 SkillVfxTableBuilder가 이 폴더의 프리팹 이름으로 한다(CSV 칸 값 「원작:모델이름」).
/// </summary>
public static class OriginalVfxBuilder
{
    public static string SourceFolder => SourceRoot;
    static readonly string SourceRoot = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_orig_vfx_trial");
    public const string ArtRoot = "Assets/Art/Effects/Original";
    public const string PrefabRoot = "Assets/Resources/Effects/Original";
    static readonly string[] Trial = { "az_firering1a", "Mdx_Effect_Railgun", "EmpyreanSigil4" };

    [MenuItem("Tools/이펙트/원작 이펙트 반입(시범)")]
    static void Menu() => Debug.Log(BuildTrial());

    public static string BuildTrial() => Build(Trial);

    /// <summary>등급별 반입(PM 지시 10-08: 초월 → 불멸 → 영원 → 하위, 등급마다 커밋·MB 보고): 그 등급 CSV들이 가리키는 모델만 반입하고 스킬 표를 다시 만든다.
    /// 호출: call OriginalVfxBuilder.BuildChowol / BuildBulmyeol / BuildYeongwon / BuildLower / BuildAssignments(전부)</summary>
    public static string BuildChowol() => BuildAssignments(f => f.StartsWith("초월"));
    public static string BuildBulmyeol() => BuildAssignments(f => f.StartsWith("불멸"));
    public static string BuildYeongwon() => BuildAssignments(f => f.StartsWith("영원"));
    public static string BuildLower() => BuildAssignments(f => !f.StartsWith("초월") && !f.StartsWith("불멸") && !f.StartsWith("영원"));
    public static string BuildAssignments() => BuildAssignments(f => true);

    [MenuItem("Tools/이펙트/원작 이펙트 배정 반입(전부)")]
    static void MenuAssign() => Debug.Log(BuildAssignments());

    static string BuildAssignments(System.Func<string, bool> gradeFilter)
    {
        var names = new SortedSet<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Docs/research"), "ORIGINAL_VFX_ASSIGN_*.csv"))
        {
            string grade = Path.GetFileNameWithoutExtension(file).Substring("ORIGINAL_VFX_ASSIGN_".Length);
            if (!gradeFilter(grade)) continue;
            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("#")) continue;
                foreach (string cell in line.Split(','))
                {
                    string v = cell.Trim().Trim('"');
                    if (v.StartsWith("원작:")) names.Add(v.Substring("원작:".Length).Trim());
                }
            }
        }
        string built = Build(names.ToArray());
        string table = SkillVfxTableBuilder.Build();
        return $"모델 {names.Count}종\n{built}{table}\n📦 Assets/Art/Effects/Original = {DirSizeMb(ArtRoot):0.0}MB · Resources/Effects/Original = {DirSizeMb(PrefabRoot):0.0}MB";
    }

    /// <summary>이미 반입한 텍스처 전부에 최대 512·보통 압축을 적용한다(wrap은 그대로).</summary>
    public static string FixTextures()
    {
        int n = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (imp == null || (imp.maxTextureSize == 512 && imp.textureCompression == TextureImporterCompression.Compressed && !imp.crunchedCompression)) continue;
                imp.maxTextureSize = 512; imp.textureCompression = TextureImporterCompression.Compressed; imp.crunchedCompression = false;
                imp.SaveAndReimport(); n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets();
        return $"✅ 텍스처 {n}장 최대 512·보통 압축 적용";
    }

    static double DirSizeMb(string assetDir)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), assetDir);
        if (!Directory.Exists(full)) return 0;
        long total = 0;
        foreach (string f in Directory.GetFiles(full, "*", SearchOption.AllDirectories)) if (!f.EndsWith(".meta")) total += new FileInfo(f).Length;
        return total / 1048576.0;
    }

    public static string Build(params string[] names)
    {
        var sb = new StringBuilder();
        foreach (string n in names) sb.AppendLine(BuildOne(n));
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    // ── 가벼운 json 읽기: JsonUtility는 배열 속 배열을 못 읽어 MiniJson 대신 좁은 구조체 + 수동 파싱 ──
    [System.Serializable] class Root { public Unity unity; }
    [System.Serializable] class Unity { public float durationSec; public bool loop; public List<Clip> clips; public List<LayerJ> layers; }
    [System.Serializable] class Clip { public string name; public float sec; public bool loop; }
    [System.Serializable] class LayerJ { public string mesh; public string blend; public string texture; public float staticAlpha = 1f; }

    static string BuildOne(string name)
    {
        string src = Path.Combine(SourceRoot, name);
        string jsonPath = Path.Combine(src, name + ".json");
        if (!File.Exists(jsonPath)) return $"❌ {name}: json 없음 ({jsonPath})";
        string jsonText = File.ReadAllText(jsonPath);
        Root root = JsonUtility.FromJson<Root>(jsonText);
        if (root == null || root.unity == null || root.unity.layers == null) return $"❌ {name}: json unity 블록 없음";
        var raw = (Dictionary<string, object>)MiniJson.Parse(jsonText);
        var rawLayers = (List<object>)((Dictionary<string, object>)raw["unity"])["layers"];

        string art = $"{ArtRoot}/{name}";
        Directory.CreateDirectory(art + "/Textures");
        Directory.CreateDirectory(PrefabRoot);
        string fbxAsset = $"{art}/{name}.fbx";
        CopyIfChanged(Path.Combine(src, name + ".fbx"), fbxAsset);
        string texSrc = Path.Combine(src, "Textures");
        if (Directory.Exists(texSrc))
            foreach (string t in Directory.GetFiles(texSrc, "*.png")) CopyIfChanged(t, $"{art}/Textures/{Path.GetFileName(t)}");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 텍스처: 알파 투명, 밉맵 없음, UV 이동 층이면 Repeat
        var uvTextures = new HashSet<string>();
        for (int i = 0; i < rawLayers.Count; i++)
            if (((Dictionary<string, object>)rawLayers[i]).ContainsKey("uvOffsetClip0")) uvTextures.Add(root.unity.layers[i].texture);
        foreach (string tex in Directory.GetFiles(art + "/Textures", "*.png"))
        {
            var imp = AssetImporter.GetAtPath(tex.Replace('\\', '/')) as TextureImporter;
            if (imp == null) continue;
            bool repeat = uvTextures.Contains(Path.GetFileName(tex));
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.maxTextureSize = 512; imp.textureCompression = TextureImporterCompression.Compressed; imp.crunchedCompression = false;   // PM 10-08: 최대 512·보통 압축(크런치 안 함)
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        // FBX 임포터: 리그 Generic, 클립 그대로
        var model = AssetImporter.GetAtPath(fbxAsset) as ModelImporter;
        if (model != null && model.animationType != ModelImporterAnimationType.Generic)
        { model.animationType = ModelImporterAnimationType.Generic; model.SaveAndReimport(); }

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAsset);
        if (fbx == null) return $"❌ {name}: FBX를 못 읽음";
        string clipName = root.unity.clips != null && root.unity.clips.Count > 0 ? root.unity.clips[0].name : null;
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxAsset).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && clipName != null && c.name.EndsWith(clipName));
        if (clip == null) clip = AssetDatabase.LoadAllAssetsAtPath(fbxAsset).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        var wrapper = new GameObject(name);
        var player = wrapper.AddComponent<OriginalVfxPlayer>();
        GameObject model3d = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        model3d.transform.SetParent(wrapper.transform, false);
        Animator animator = model3d.GetComponent<Animator>();
        if (animator == null) animator = model3d.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        player.animator = animator; player.clip = clip;
        // 연출용: json의 모든 시퀀스를 이름으로 찾아 담는다(첫 시퀀스만 쓰던 기존 재생과 같은 prefab에서 둘 다 된다)
        var clipEntries = new List<OriginalVfxPlayer.ClipEntry>();
        if (root.unity.clips != null)
        {
            var allClips = AssetDatabase.LoadAllAssetsAtPath(fbxAsset).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var rawClips = (List<object>)((Dictionary<string, object>)raw["unity"])["clips"];
            for (int ci = 0; ci < root.unity.clips.Count; ci++)
            {
                Clip cj = root.unity.clips[ci];
                AnimationClip ac = allClips.FirstOrDefault(c => c.name.EndsWith(cj.name));
                if (ac == null) continue;
                var rc = (Dictionary<string, object>)rawClips[ci];
                float startMs = rc.TryGetValue("mdxStartMs", out object sm) ? System.Convert.ToSingle(sm) : 0f;
                clipEntries.Add(new OriginalVfxPlayer.ClipEntry { name = cj.name, clip = ac, startSec = startMs / 1000f, lengthSec = cj.sec, looping = cj.loop });
            }
        }
        player.clips = clipEntries.ToArray();
        player.duration = root.unity.durationSec > 0f ? root.unity.durationSec : (clip != null ? clip.length : 1f);
        player.loop = root.unity.loop;

        var sb = new StringBuilder();
        var layers = new List<OriginalVfxPlayer.Layer>();
        Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < root.unity.layers.Count; i++)
        {
            LayerJ lj = root.unity.layers[i];
            var dict = (Dictionary<string, object>)rawLayers[i];
            Renderer r = renderers.FirstOrDefault(x => RendererMatches(x, lj.mesh));
            if (r == null) { sb.Append($" ⚠️층 {lj.mesh}: 렌더러 못 찾음"); continue; }
            Material mat = MakeMaterial(name, i, lj, $"{art}/Textures/{lj.texture}");
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var layer = new OriginalVfxPlayer.Layer { renderer = r, staticAlpha = lj.staticAlpha };
            if (dict.TryGetValue("alphaCurveClip0Sec", out object ac)) ReadPairs((List<object>)ac, out layer.alphaTimes, out layer.alphaValues);
            if (dict.TryGetValue("alphaCurveGlobalSec", out object ag) && ag is List<object> agl && agl.Count > 0) ReadPairs(agl, out layer.globalAlphaTimes, out layer.globalAlphaValues);
            if (dict.TryGetValue("uvOffsetClip0", out object uv))
            {
                var keys = (List<object>)((Dictionary<string, object>)uv)["keys"];
                layer.uvTimes = new float[keys.Count]; layer.uvOffsets = new Vector2[keys.Count];
                for (int k = 0; k < keys.Count; k++)
                {
                    var row = (List<object>)keys[k];
                    layer.uvTimes[k] = System.Convert.ToSingle(row[0]);
                    layer.uvOffsets[k] = new Vector2(System.Convert.ToSingle(row[1]), -System.Convert.ToSingle(row[2]));   // Unity offsetV = -v
                }
            }
            layers.Add(layer);
        }
        player.layers = layers.ToArray();

        string prefabPath = $"{PrefabRoot}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
        Object.DestroyImmediate(wrapper);
        return $"✅ {name}: 층 {layers.Count}/{root.unity.layers.Count} · 클립 {(clip != null ? clip.name + $" {clip.length:0.00}s" : "없음")} · {root.unity.durationSec:0.00}s{sb} → {prefabPath}";
    }

    static bool RendererMatches(Renderer x, string mesh)
    {
        if (x.gameObject.name == mesh) return true;
        SkinnedMeshRenderer smr = x as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh != null && smr.sharedMesh.name == mesh) return true;
        MeshFilter mf = x.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null && mf.sharedMesh.name == mesh;
    }

    static void ReadPairs(List<object> list, out float[] times, out float[] values)
    {
        times = new float[list.Count]; values = new float[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var row = (List<object>)list[i];
            times[i] = System.Convert.ToSingle(row[0]); values[i] = System.Convert.ToSingle(row[1]);
        }
    }

    static Material MakeMaterial(string model, int index, LayerJ lj, string texPath)
    {
        string matPath = $"{ArtRoot}/{model}/Mat_{index}_{Path.GetFileNameWithoutExtension(lj.mesh)}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        bool additive = lj.blend != null && lj.blend.StartsWith("Additive");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_Cull", 0f);   // 양면(원작 모델은 뒷면 컬링 없이 쓴다)
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) m.EnableKeyword("_BLENDMODE_ADD"); else m.DisableKeyword("_BLENDMODE_ADD");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_Cull", (int)CullMode.Off);
        if (isNew) AssetDatabase.CreateAsset(m, matPath); else EditorUtility.SetDirty(m);
        return m;
    }

    static void CopyIfChanged(string from, string assetPath)
    {
        string to = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (File.Exists(to) && new FileInfo(to).Length == new FileInfo(from).Length && File.GetLastWriteTimeUtc(to) >= File.GetLastWriteTimeUtc(from)) return;
        File.Copy(from, to, true);
    }
}

/// <summary>json을 Dictionary/List/double/string/bool/null로 읽는 아주 작은 파서(에디터 전용).</summary>
static class MiniJson
{
    public static object Parse(string s) { int i = 0; return Value(s, ref i); }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++;
            Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i); string key = Str(s, ref i); Ws(s, ref i); i++;   // ':'
                d[key] = Value(s, ref i); Ws(s, ref i);
                if (s[i++] == '}') return d;
            }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++;
            Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i)); Ws(s, ref i);
                if (s[i++] == ']') return l;
            }
        }
        if (c == '"') return Str(s, ref i);
        if (s.Substring(i, 4) == "true") { i += 4; return true; }
        if (s.Substring(i, 5) == "false") { i += 5; return false; }
        if (s.Substring(i, 4) == "null") { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), System.Globalization.CultureInfo.InvariantCulture);
    }

    static string Str(string s, ref int i)
    {
        var sb = new StringBuilder(); i++;
        while (s[i] != '"')
        {
            if (s[i] == '\\')
            {
                i++;
                switch (s[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append((char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                    default: sb.Append(s[i]); break;
                }
            }
            else sb.Append(s[i]);
            i++;
        }
        i++;
        return sb.ToString();
    }
}
