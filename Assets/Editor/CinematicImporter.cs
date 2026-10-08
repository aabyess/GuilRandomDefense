using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 원작 스킬 연출 반입(10-09): ~/GRD_scenes/scripts/&lt;id&gt;.json(blender 대본) → Assets/Resources/Cinematics/&lt;id&gt;.asset(CinematicScript)
/// + 쓰는 모델마다 Resources/Cinematics/Models/&lt;모델&gt;.prefab(= 반입한 이펙트 프리팹 복사 + PRE2 입자 자식 ParticleSystem·Pre2Driver).
/// 호출: call CinematicImporter.ImportAll (4개 전부) / ImportOne(file ClaudeBridge/g2_scene.txt). 다시 불러도 안전하다.
/// </summary>
public static class CinematicImporter
{
    static readonly string ScriptRoot = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_scenes/scripts");
    const string OutRoot = "Assets/Resources/Cinematics";
    const string ModelOut = "Assets/Resources/Cinematics/Models";
    const string MatOut = "Assets/Art/Effects/Original/_pre2";

    [MenuItem("Tools/이펙트/연출 대본 반입(전부)")]
    static void Menu() => Debug.Log(ImportAll());

    public static string ImportAll()
    {
        var sb = new StringBuilder();
        foreach (string f in Directory.GetFiles(ScriptRoot, "*.json").OrderBy(x => x)) sb.AppendLine(Import(f));
        return sb.ToString();
    }

    public static string ImportOne() => Import(Path.Combine(ScriptRoot, File.ReadAllText("ClaudeBridge/g2_scene.txt").Trim() + ".json"));

    static float F(Dictionary<string, object> d, string k, float def = 0f) => d != null && d.TryGetValue(k, out object v) && v != null && !(v is string) && !(v is bool) && !(v is List<object>) && !(v is Dictionary<string, object>) ? System.Convert.ToSingle(v) : def;
    static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out object v) && v is string s ? s : "";
    static Dictionary<string, object> D(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out object v) ? v as Dictionary<string, object> : null;
    static List<object> L(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out object v) ? v as List<object> : null;

    static string Import(string file)
    {
        var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(file));
        string id = S(root, "id");
        Directory.CreateDirectory(OutRoot); Directory.CreateDirectory(ModelOut); Directory.CreateDirectory(MatOut);
        var script = ScriptableObject.CreateInstance<CinematicScript>();
        script.scriptId = id; script.title = S(root, "title"); script.duration = F(root, "durationSec", 3f);
        var notes = new StringBuilder();

        // 모델
        var models = D(root, "models");
        foreach (var kv in models)
        {
            var m = (Dictionary<string, object>)kv.Value;
            string folder = S(m, "folder");
            var mr = new CinematicScript.ModelRef { name = kv.Key };
            if (!string.IsNullOrEmpty(folder)) mr.prefab = BuildModelPrefab(kv.Key, folder, L(m, "pre2"), notes);
            else SubstituteModel(mr, root, notes);
            script.models.Add(mr);
        }

        // 시간표
        foreach (object o in L(root, "timeline"))
        {
            var e = (Dictionary<string, object>)o;
            string op = S(e, "op");
            var ev = new CinematicScript.Ev { t = F(e, "t"), id = S(e, "id") };
            switch (op)
            {
                case "spawn":
                    ev.op = CinematicScript.Op.Spawn; ev.model = S(e, "model");
                    ev.baseScale = F(e, "baseScale", 1f); ev.scalePercent = F(e, "scalePercent", 100f); ev.flyHeight = F(e, "flyHeight");
                    ev.lifeSec = e.TryGetValue("lifeSec", out object ls) && ls != null ? System.Convert.ToSingle(ls) : -1f;
                    ev.deathSec = F(e, "deathSec", 0.1f); ev.anim = S(e, "anim"); ev.timescale = F(e, "timescale", 1f); ev.vertexAlpha = F(e, "vertexAlpha", 1f);
                    var at = D(e, "at");
                    ev.anchor = ParseAnchor(S(at, "anchor")); ev.anchorShip = S(at, "ship");
                    var polar = D(at, "polar");
                    if (polar != null) { ev.hasPolar = true; ev.polarRadius = F(polar, "radius"); ev.polarAngleDeg = F(polar, "angleDeg"); }
                    string facing = e.TryGetValue("facing", out object fo) ? fo as string : null;
                    ev.facingRandom = facing == "random";
                    var move = D(e, "moveTo");
                    if (move != null) { ev.hasMove = true; ev.moveAnchor = ParseAnchor(S(move, "anchor")); ev.moveSpeed = F(move, "speedPerSec"); }
                    break;
                case "set":
                    ev.op = CinematicScript.Op.Set;
                    if (e.ContainsKey("scalePercent")) { ev.hasScaleSet = true; ev.scalePercent = F(e, "scalePercent", 100f); }
                    if (e.ContainsKey("timescale")) { ev.hasTimescaleSet = true; ev.timescale = F(e, "timescale", 1f); }
                    break;
                case "ramp": ev.op = CinematicScript.Op.Ramp; ev.rampTo = F(e, "to"); ev.rampRate = F(e, "ratePerSec", 1f); break;
                case "kill": ev.op = CinematicScript.Op.Kill; break;
                case "camera_shake": ev.op = CinematicScript.Op.CameraShake; ev.shakeDuration = F(e, "durationSec"); ev.shakeMagnitude = F(e, "magnitude"); break;
                default: continue;   // note·damage는 재생기에서 안 쓴다
            }
            script.events.Add(ev);
        }

        string path = $"{OutRoot}/{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CinematicScript>(path);
        if (existing == null) AssetDatabase.CreateAsset(script, path);
        else { EditorUtility.CopySerialized(script, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(script); }
        AssetDatabase.SaveAssets();
        return $"✅ {id}: 이벤트 {script.events.Count} · 모델 {models.Count}{notes}";
    }

    // 스킬 → 대본(blender ourSkill 확정 10-09; 샹크스는 초월_황준석 패기 계열 더미채널 79행)
    static readonly (string skill, string script)[] SkillLinks =
    {
        ("SkillData_사장님_제한_전법규_마나스킬", "enel_eltor"),
        ("SkillData_사장님_불멸_정준영_범퍼숨통조이기", "dragon_storm"),
        ("SkillData_사장님_불멸_고도현_무중생유", "shiki_fleet"),
        ("SkillData_더미채널_초월_황준석_ADAP_79행_10000", "shanks_haki"),
    };

    public static string LinkSkills()
    {
        var table = AssetDatabase.LoadAssetAtPath<SkillCinematicTable>($"{OutRoot}/SkillCinematicTable.asset");
        bool isNew = table == null;
        if (isNew) table = ScriptableObject.CreateInstance<SkillCinematicTable>();
        table.scripts.Clear(); table.entries.Clear();
        var sb = new StringBuilder();
        foreach (var (skillName, scriptId) in SkillLinks)
        {
            var script = AssetDatabase.LoadAssetAtPath<CinematicScript>($"{OutRoot}/{scriptId}.asset");
            string[] guids = AssetDatabase.FindAssets(skillName + " t:SkillData", new[] { "Assets/Data/UnitSkills" });
            SkillData skill = guids.Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(s => s != null && s.name == skillName);
            if (script == null || skill == null) { sb.AppendLine($"   ❌ {skillName} → {scriptId}: {(script == null ? "대본 없음" : "스킬 없음")}"); continue; }
            int index = table.scripts.IndexOf(script); if (index < 0) { table.scripts.Add(script); index = table.scripts.Count - 1; }
            table.entries.Add(new SkillCinematicTable.Entry { skill = skill, script = index });
            sb.AppendLine($"   ✅ {skillName} → {scriptId}");
        }
        if (isNew) AssetDatabase.CreateAsset(table, $"{OutRoot}/SkillCinematicTable.asset"); else EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        return "연출 표 연결:\n" + sb;
    }

    // 워3 기본 모델(맵에 없음)의 대체 — 대본 spawn.substitute{path,lengthWc3}가 있으면 그것(시키 전함 = 우리 해적선), 없으면 대본 note의 대체 설명(Tranquility = 고리 이펙트)
    static void SubstituteModel(CinematicScript.ModelRef mr, Dictionary<string, object> root, StringBuilder notes)
    {
        string path = null; float length = 0f;
        foreach (object o in L(root, "timeline"))
        {
            var e = (Dictionary<string, object>)o;
            if (S(e, "model") != mr.name) continue;
            var sub = D(e, "substitute");
            if (sub != null) { path = S(sub, "path"); length = F(sub, "lengthWc3"); break; }
        }
        if (path == null && mr.name == "Tranquility") { path = $"{OriginalVfxBuilder.PrefabRoot}/az_firering1a.prefab"; length = 300f; }
        GameObject prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        if (prefab == null) { notes.Append($" ⚠️{mr.name}: 대체 프리팹 없음({path})"); return; }
        mr.prefab = prefab; mr.substituteLengthWc3 = length;
        mr.substituteNativeLength = prefab.name == "az_firering1a" ? 6.4f : NativeLength(prefab);   // 고리: 뼈 애니로 커져 편집 모드 경계가 낡는다 — 게임 실측 최대(6.4m)
        notes.Append($" · {mr.name}→{System.IO.Path.GetFileName(path)} 길이 {length} (원래 {mr.substituteNativeLength:F2})");
    }

    // 대체 프리팹의 가로 길이(프리팹 단위): 렌더러 경계 합집합의 x·z 큰 쪽. 뼈 애니 이펙트(고리)는 재생 중 최대 크기에 가깝게 반 지점으로 샘플한다.
    static float NativeLength(GameObject prefab)
    {
        var inst = (GameObject)Object.Instantiate(prefab); inst.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var player = inst.GetComponent<OriginalVfxPlayer>();
            float best = 0f;
            foreach (float f in new[] { 0.3f, 0.6f, 0.9f })
            {
                if (player != null) player.SampleAt(player.duration * f);
                Bounds? b = null;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>()) { if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; } }
                if (b != null) best = Mathf.Max(best, b.Value.size.x, b.Value.size.z);
            }
            return best > 0.01f ? best : 1f;
        }
        finally { Object.DestroyImmediate(inst); }
    }

    static CinematicScript.Anchor ParseAnchor(string a) => a == "target" ? CinematicScript.Anchor.Target : a == "ship" ? CinematicScript.Anchor.Ship : CinematicScript.Anchor.Caster;

    // ── 모델 프리팹: 반입한 이펙트 프리팹 복사 + 입자 자식 ─────────────────────────────────────
    static GameObject BuildModelPrefab(string modelName, string folder, List<object> pre2, StringBuilder notes)
    {
        string baseDir = Path.Combine(OriginalVfxBuilder.SourceFolder, folder);
        string srcPrefab = $"{OriginalVfxBuilder.PrefabRoot}/{folder}.prefab";
        if (File.Exists(Path.Combine(baseDir, folder + ".fbx")) || File.Exists(Path.Combine(baseDir, folder + ".json")))
        {
            OriginalVfxBuilder.Build(folder);   // 매번 다시 지어 최신 규칙(알파 없는 텍스처 가산 등)을 반영
        }
        string outPath = $"{ModelOut}/{SafeName(modelName)}.prefab";
        GameObject contents;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(srcPrefab) != null) contents = PrefabUtility.LoadPrefabContents(srcPrefab);
        else
        {   // 메시 없는 입자 전용 모델(orgia mode red 등) — 재생기만 달린 빈 루트
            contents = new GameObject(modelName);
            contents.AddComponent<OriginalVfxPlayer>();
            notes.Append($" · {modelName}: 입자 전용 빈 루트");
        }
        try
        {
            // 옛 입자 자식 제거 후 다시 만든다
            foreach (Transform c in contents.transform.Cast<Transform>().ToList()) if (c.name.StartsWith("pre2_")) Object.DestroyImmediate(c.gameObject);
            int n = 0;
            if (pre2 != null)
                foreach (object po in pre2)
                    if (Pre2Builder.Build(contents.transform, (Dictionary<string, object>)po, folder, n++, MatOut)) { }
            PrefabUtility.SaveAsPrefabAsset(contents, outPath);
            if (n > 0) notes.Append($" · {modelName}: 입자 {n}");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(outPath);
    }

    static bool NeedsRebuild(string srcPrefab)
    {
        // 시퀀스 목록(clips)이 없는 옛 반입은 연출용으로 다시 만든다
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(srcPrefab);
        var p = go != null ? go.GetComponent<OriginalVfxPlayer>() : null;
        return p == null || p.clips == null || p.clips.Length == 0;
    }

    static string SafeName(string n) => new string(n.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
}

/// <summary>PRE2 한 묶음 → 자식 ParticleSystem + Pre2Driver. 로컬 단위는 미터(워3 1=0.01m) — 모델 루트 스케일이 월드 크기를 정한다.</summary>
static class Pre2Builder
{
    static float F(Dictionary<string, object> d, string k, float def = 0f) => d != null && d.TryGetValue(k, out object v) && v != null && !(v is string) && !(v is bool) && !(v is List<object>) && !(v is Dictionary<string, object>) ? System.Convert.ToSingle(v) : def;
    static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out object v) && v is string s ? s : "";
    static float[] Arr(Dictionary<string, object> d, string k, int n)
    {
        var l = d.TryGetValue(k, out object v) ? v as List<object> : null;
        var r = new float[n];
        for (int i = 0; i < n; i++) r[i] = l != null && i < l.Count ? System.Convert.ToSingle(l[i]) : 0f;
        return r;
    }

    public static bool Build(Transform parent, Dictionary<string, object> p, string folder, int index, string matDir)
    {
        string texFile = S(p, "textureFile");
        string texPath = $"{OriginalVfxBuilder.ArtRoot}/{folder}/Textures/{texFile}";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { Debug.LogWarning($"[연출] {folder} 입자 {index}: 텍스처 없음 {texPath}"); return false; }
        string filter = S(p, "filterMode");
        float speed = F(p, "speed"), variation = F(p, "variation"), latitude = F(p, "latitude"), gravity = F(p, "gravity");
        float life = Mathf.Max(0.05f, F(p, "lifespanSec", 1f)), rate = F(p, "emissionRate");
        float width = F(p, "width"), length = F(p, "length");
        int rows = Mathf.Max(1, (int)F(p, "rows", 1)), cols = Mathf.Max(1, (int)F(p, "cols", 1));
        float mid = F(p, "midTime", 0.5f); if (mid <= 0.02f || mid >= 0.98f) mid = 0.5f;
        float[] pivot = Arr(p, "pivot", 3);
        var col = p.TryGetValue("colorsRGB01", out object co) ? co as List<object> : null;
        float[] alpha = Arr(p, "alpha255", 3), scale = Arr(p, "scale3", 3);
        bool tail = S(p, "headOrTail") == "tail"; float tailLength = Mathf.Abs(F(p, "tailLength"));

        var go = new GameObject($"pre2_{index}_{S(p, "node")}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pivot[0], pivot[2], pivot[1]) * 0.01f;   // 워3 z-up → Unity y-up, 미터
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);                      // 방출 축(+Z)이 위(+Y)를 향하게
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 5f; main.loop = true; main.playOnAwake = false;
        main.startLifetime = life;
        float v = speed * 0.01f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, v * (1f - variation)), v * (1f + variation));
        float size0 = Mathf.Max(scale[0], 0.01f) * 0.01f;
        if (scale[0] < 0.01f) size0 = Mathf.Max(scale[1], scale[2], 0.01f) * 0.01f;
        main.startSize = size0;
        main.startColor = new Color(1f, 1f, 1f, 0.6f);   // 가산 겹침이 화면을 하얗게 덮지 않게 연출 입자는 6할 세기(PM 10-09)
        main.maxParticles = 2000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape;
        float radius = Mathf.Max(width, length) * 0.5f * 0.01f;
        if (latitude >= 90f) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = Mathf.Max(0.0001f, radius); }
        else if (width > 0f || length > 0f) { shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(width * 0.01f, length * 0.01f, 0.0001f); }
        else { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = Mathf.Clamp(latitude, 0f, 89f); shape.radius = 0.0001f; }
        if ((width > 0f || length > 0f) && latitude > 0.5f && latitude < 90f)
        {   // 면 방출 + 각도: 콘(반지름=면 크기)
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = Mathf.Clamp(latitude, 0f, 89f); shape.radius = Mathf.Max(0.0001f, radius);
        }
        // 색·알파 3구간
        var grad = new Gradient();
        Color c0 = Rgb(col, 0), c1 = Rgb(col, 1), c2 = Rgb(col, 2);
        grad.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, mid), new GradientColorKey(c2, 1f) },
                     new[] { new GradientAlphaKey(alpha[0] / 255f, 0f), new GradientAlphaKey(alpha[1] / 255f, mid), new GradientAlphaKey(alpha[2] / 255f, 1f) });
        var col2 = ps.colorOverLifetime; col2.enabled = true; col2.color = grad;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        float s0 = Mathf.Max(scale[0], 0.01f) > 0.01f ? scale[0] : Mathf.Max(scale[1], scale[2], 0.01f);
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, scale[0] / s0), new Keyframe(mid, scale[1] / s0), new Keyframe(1f, scale[2] / s0)));
        if (rows * cols > 1)
        {
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = cols; sheet.numTilesY = rows;
            sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = tail ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (tail) { r.lengthScale = Mathf.Clamp(1f + tailLength * 2f, 1f, 8f); r.velocityScale = 0f; }
        r.sharedMaterial = Material(tex, texPath, filter, matDir);
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;

        var drv = go.AddComponent<Pre2Driver>();
        drv.baseRate = rate; drv.war3Gravity = gravity; drv.sequenceStartMs = F(p, "sequenceStartMs");
        var tracks = p.TryGetValue("tracks", out object to) ? to as Dictionary<string, object> : null;
        if (tracks != null)
        {
            if (tracks.TryGetValue("KP2E", out object ke)) { drv.rateKeys = Keys(ke, out drv.rateKeysLinear); drv.burstOnly = p.TryGetValue("squirt", out object sq) && sq is bool sb && sb; }
            if (tracks.TryGetValue("KP2V", out object kv)) drv.visKeys = Keys(kv, out drv.visKeysLinear);
        }
        return true;
    }

    static Color Rgb(List<object> col, int i)
    {
        if (col == null || i >= col.Count || !(col[i] is List<object> c) || c.Count < 3) return Color.white;
        return new Color(System.Convert.ToSingle(c[0]), System.Convert.ToSingle(c[1]), System.Convert.ToSingle(c[2]));
    }

    static Pre2Driver.Key[] Keys(object track, out bool linear)
    {
        linear = false;
        var t = track as Dictionary<string, object>;
        if (t == null) return new Pre2Driver.Key[0];
        linear = t.TryGetValue("interp", out object ip) && System.Convert.ToInt32(ip) != 0;
        var list = t.TryGetValue("keysMsAbs", out object k) ? k as List<object> : null;
        if (list == null) return new Pre2Driver.Key[0];
        var keys = new List<Pre2Driver.Key>();
        foreach (object o in list)
        {
            var row = (List<object>)o; var vals = row[1] as List<object>;
            keys.Add(new Pre2Driver.Key { ms = System.Convert.ToSingle(row[0]), value = vals != null && vals.Count > 0 ? System.Convert.ToSingle(vals[0]) : 0f });
        }
        return keys.ToArray();
    }

    static Material Material(Texture2D tex, string texPath, string filter, string dir)
    {
        if ((filter == "blend" || filter == "alphakey") && OriginalVfxBuilder.TextureOpaque(texPath)) filter = "additive";   // 알파 없는 근사 텍스처 → 검은 네모 방지
        string name = $"{Path.GetFileNameWithoutExtension(texPath)}_{filter}".Replace(' ', '_');
        string path = $"{dir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", tex);
        bool additive = filter == "additive";
        bool multiply = filter == "modulate" || filter == "modulate2x";
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : multiply ? 3f : 0f);
        m.SetFloat("_Cull", 0f); m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_BLENDMODE_ADD"); m.DisableKeyword("_BLENDMODE_MULTIPLY");
        if (additive) m.EnableKeyword("_BLENDMODE_ADD");
        if (multiply) m.EnableKeyword("_BLENDMODE_MULTIPLY");
        m.SetInt("_SrcBlend", multiply ? (int)BlendMode.DstColor : (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", additive ? (int)BlendMode.One : multiply ? (int)BlendMode.Zero : (int)BlendMode.OneMinusSrcAlpha);
        if (filter == "alphakey") { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.75f); m.EnableKeyword("_ALPHATEST_ON"); }
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }
}
