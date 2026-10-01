using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 부가 이펙트 빌더(2026-10-01) — Tools/sphere_art/effects/&lt;로스터&gt;.json(flatten_effects.py 산출, Blender effects.json을 납작하게 편 것)을 읽어
///   Resources/Effects/Sphere/&lt;별칭&gt;_&lt;슬롯&gt;.prefab 한 벌(슬롯 = always·attack·skill)과 Tools/sphere_art/extra.tsv 줄을 만든다.
///   · 슬롯 한 벌이 prefab 하나 — UnitSphereArt가 그 프리팹을 통째로 켜고 끈다(보이는 때가 같은 부품끼리).
///   · 붙는 곳 = 「body」(유닛 루트, 몸 발밑 원점) — 원작 effects.json의 위치가 몸 좌표(발밑 기준)라 그대로 쓴다. 뼈 따라가기는 안 한다.
///   · 크기 = 게임 키(30) ÷ 그 모델 몸 키(m)를 모든 위치·크기·속도에 곱한다(모델마다 몸 키가 달라 부품마다 bodyHeightM).
///   · 파티클 = ParticleSystem 자작(가산 재질은 Skill_star_07.mat 사본 — 같은 URP Particles/Unlit 설정), 메시 = FBX 오브젝트 하나만 남겨 알파컷 재질.
/// 쓰기: `call SphereArtBuilder.BuildAll` → python3 Tools/sphere_art/build_table.py
/// </summary>
static class SphereArtBuilder
{
    const float GameBodyHeight = 30f;
    const string ResDir = "Assets/Resources/Effects/Sphere";
    const string MatDir = ResDir + "/Materials";
    const string AdditiveBase = "Assets/Resources/Effects/Skill_star_07.mat";

    [System.Serializable] class Part
    {
        public string kind, name, slot, model, texture, fbx, fbxObject, shape, states, attach, spinAxis;
        public float spinDeg;
        public float trail, width, widthEnd, bodyHeightM, rate, life, speed, speedVar, cone, gravity, mid;
        public float[] pos, box, size, rgb, alpha, euler, offset;
        public bool additive, cutout, blend;
        public int rows, cols;
    }
    [System.Serializable] class Roster { public string roster, alias; public bool single; public List<Part> parts; }

    static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

    static string BuildAll()
    {
        string dir = Path.Combine(ProjectRoot, "Tools/sphere_art/effects");
        if (!Directory.Exists(dir)) return "❌ Tools/sphere_art/effects 없음 — flatten_effects.py 먼저";
        var report = new StringBuilder();
        var tsv = new StringBuilder("# 생성: SphereArtBuilder.BuildAll (Assets/Editor) — 손으로 고치지 말 것. 한 줄 = 한 슬롯 프리팹 통째(붙는 곳 body = 유닛 루트)\n");
        EnsureDir(MatDir);
        foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, System.StringComparer.Ordinal))
        {
            Roster r = JsonUtility.FromJson<Roster>(File.ReadAllText(file, Encoding.UTF8));
            string rosterName = r.roster.Normalize(NormalizationForm.FormC);
            // 같은 아트 키 접두의 옛 프리팹을 지우고 다시 만든다(그룹 이름이 바뀌어도 낡은 게 안 남게)
            if (!r.single)
                foreach (string old in AssetDatabase.FindAssets("t:Prefab", new[] { ResDir }).Select(AssetDatabase.GUIDToAssetPath).Where(a => Path.GetFileName(a).StartsWith(r.alias + "_")).ToList())
                    AssetDatabase.DeleteAsset(old);
            if (r.single && r.parts.Count == 0) { report.AppendLine($"{r.alias}: 부품 없음(원작에서 안 켜지는 모델) — 임시 모양 유지"); continue; }
            if (r.single)
            {
                // 상시 오라: 모델 통째 한 프리팹 <별칭>.prefab — 별칭 = 원작 파일 이름 소문자(SphereArtTable.Create가 그 이름으로 찾는다)
                string exact = $"{ResDir}/{r.alias}.prefab";
                if (File.Exists(Path.Combine(ProjectRoot, exact))) AssetDatabase.DeleteAsset(exact);
                report.AppendLine(BuildPrefab(r.alias, r.alias, r.alias, r.parts, aura: true));
                continue;
            }
            // 보이는 때(states)와 붙는 뼈(attach)가 같은 부품끼리 프리팹 하나
            foreach (IGrouping<(string states, string attach), Part> g in r.parts.GroupBy(p => (p.states, p.attach)))
            {
                string when = g.Key.states.Split('|').Length == 4 ? "항상" : g.Key.states;
                string letters = string.Concat(g.Key.states.Split('|').Select(s => s == "대기" ? "i" : s == "이동" ? "m" : s == "공격" ? "a" : "s"));
                string key = $"{r.alias}_{letters}_{g.Key.attach.Replace(",", "")}";
                report.AppendLine(BuildPrefab(rosterName, r.alias, key, g.ToList()));
                tsv.AppendLine($"{rosterName}\t{key}\tfollow:{g.Key.attach}\t\t\t1\t{when}");
            }
        }
        File.WriteAllText(Path.Combine(ProjectRoot, "Tools/sphere_art/extra.tsv"), tsv.ToString(), new UTF8Encoding(false));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report.ToString().TrimEnd();
    }

    static string BuildPrefab(string roster, string alias, string key, List<Part> parts, bool aura = false)
    {
        var root = new GameObject(key);
        // 손에 쥐는 부품: 첫 부품의 원작 pivot(몸 좌표)을 쥐는 점으로(SphereArtHold) — 손 뼈 붙임 프리팹 전부
        if (parts.Count > 0 && parts[0].attach != null && parts[0].attach.StartsWith("hand,") && parts[0].pos != null && parts[0].pos.Length >= 3)
        {
            float kHold = GameBodyHeight / Mathf.Max(0.1f, parts[0].bodyHeightM);
            root.AddComponent<SphereArtHold>().pivot = new Vector3(parts[0].pos[0], parts[0].pos[1], parts[0].pos[2]) * kHold;
        }
        int meshes = 0, systems = 0;
        var notes = new StringBuilder();
        try
        {
            foreach (Part p in parts)
            {
                float k = GameBodyHeight / Mathf.Max(0.1f, p.bodyHeightM);
                if (p.kind == "mesh") { if (AddMesh(root.transform, roster, alias, p, k, notes)) meshes++; }
                else if (p.kind == "particle") { AddParticles(root.transform, alias, p, k); systems++; }
                else if (p.kind == "ribbon") { AddRibbon(root.transform, alias, p, k); systems++; }
            }
            PrefabUtility.SaveAsPrefabAsset(root, $"{ResDir}/{key}.prefab");
        }
        finally { Object.DestroyImmediate(root); }
        return $"{roster} · {key}: 메시 {meshes} · 파티클 {systems}{notes}";
    }

    static void AddSpin(GameObject go, Part p)
    {
        var spin = go.AddComponent<SphereArtSpin>();
        spin.axis = p.spinAxis == "X" ? Vector3.right : p.spinAxis == "Z" ? Vector3.forward : Vector3.up;
        spin.degPerSecond = p.spinDeg;
    }

    // ── 메시 ──────────────────────────────────────────────────────────────
    static bool AddMesh(Transform parent, string roster, string alias, Part p, float k, StringBuilder notes)
    {
        string fbxPath = $"{ResDir}/Src/{roster}/{p.fbx}";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (source == null) { notes.Append($" ⚠️ FBX 없음 {fbxPath}"); return false; }
        // 🔴 배율은 껍데기(wrapper)에 준다 — FBX 오브젝트의 로컬 위치(원작 pivot, 미터)도 같이 게임 단위로 커져야 한다.
        //    인스턴스 자체에 주면 위치는 미터 그대로라 별빛이 발밑에 앉았다(10-01 최상호_AD, PM 사진 지적).
        var wrapper = new GameObject(p.name);
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.localScale = Vector3.one * k;
        if (p.spinDeg != 0f) AddSpin(wrapper, p);
        if (p.euler != null && p.euler.Length >= 3) wrapper.transform.localRotation = Quaternion.Euler(p.euler[0], p.euler[1], p.euler[2]);
        if (p.offset != null && p.offset.Length >= 3) wrapper.transform.localPosition = new Vector3(p.offset[0], p.offset[1], p.offset[2]) * GameBodyHeight;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.transform.SetParent(wrapper.transform, false);
        string fbxName = instance.name.Replace("(Clone)", "");   // 이름 대조용(루트 자체가 메시인 FBX는 렌더러 이름 = 이 이름)
        instance.name = p.name + "_fbx";
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        bool found = false;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            string rn = r.gameObject == instance ? fbxName : r.name;
            if (rn != p.fbxObject && !p.fbxObject.StartsWith(rn + "_L")) { Object.DestroyImmediate(r.gameObject); continue; }   // 유니티가 재질 접미(_L0_add)를 뗀 이름으로 읽을 때가 있다(lb_jimbe_g0)
            found = true;
            r.sharedMaterial = MeshMaterial(roster, alias, p);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        if (!found) { notes.Append($" ⚠️ 오브젝트 {p.fbxObject} 없음(FBX 안: {string.Join(", ", source.GetComponentsInChildren<Renderer>(true).Select(x => x.name))})"); Object.DestroyImmediate(wrapper); return false; }
        Bounds b = default; bool first = true;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        if (p.attach.StartsWith("limb,"))
        {
            // 소매: 어깨 쪽 끝 = 안쪽·위, 손 쪽 끝 = 바깥쪽·아래(경계 상자 근사) — 옆(몸 좌우)은 중심 x의 부호로 안/바깥을 정한다.
            float sign = b.center.x >= 0f ? 1f : -1f;
            var limb = parent.GetComponent<SphereArtLimb>() ?? parent.gameObject.AddComponent<SphereArtLimb>();
            limb.start = new Vector3(b.center.x - sign * b.extents.x, b.max.y, b.center.z);
            limb.end = new Vector3(b.center.x + sign * b.extents.x, b.min.y, b.center.z);
            limb.left = p.attach.EndsWith("left");
            notes.Append($" [소매 어깨끝 {limb.start:F1} → 손끝 {limb.end:F1}]");
        }
        notes.Append($" [{p.name} 경계 {b.min:F1}~{b.max:F1}]");
        return true;
    }

    static Material MeshMaterial(string roster, string alias, Part p)
    {
        string path = $"{MatDir}/{alias}_{p.fbxObject}.mat";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResDir}/Src/{roster}/{p.texture}");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (p.additive || p.blend)
        {
            // 가산·알파혼합 = 파티클 URP Unlit 사본(메시에도 그대로 쓰인다). 혼합은 Skill_*.mat 방식(_Blend 0 · Src/Dst 알파).
            if (m == null) { AssetDatabase.CopyAsset(AdditiveBase, path); m = AssetDatabase.LoadAssetAtPath<Material>(path); }
            if (!p.blend)   // 가산으로 되돌린다(이전 빌드가 블렌드로 바꿔 둔 재질이 남아 있을 수 있다)
            {
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            if (p.blend)
            {
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            m.SetFloat("_Cull", 0f);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Cull", 0f);                                   // 양면
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Metallic", 0f);
        if (p.cutout) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ── 파티클 ────────────────────────────────────────────────────────────
    static void AddParticles(Transform parent, string alias, Part p, float k)
    {
        var go = new GameObject(p.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(p.pos[0], p.pos[1], p.pos[2]) * k;
        // 반구·원뿔은 로컬 +Z로 쏜다 — 원작 「노드 로컬 위」 = 유니티 +Y라 눕혀 맞춘다
        if (p.shape != "sphere") go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = p.life;
        float speed = p.speed * k;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * (1f - p.speedVar), speed * (1f + p.speedVar));
        float maxSize = Mathf.Max(p.size[0], p.size[1], p.size[2]);
        main.startSize = maxSize * k;
        main.startColor = Color.white;
        main.gravityModifier = p.gravity * k;
        main.maxParticles = Mathf.CeilToInt(p.rate * p.life * 2f) + 8;

        var emission = ps.emission;
        emission.rateOverTime = p.rate;

        var shape = ps.shape;
        float radius = Mathf.Max(0.02f, Mathf.Max(p.box[0], p.box[2]) * 0.5f) * k;
        shape.shapeType = p.shape == "sphere" ? ParticleSystemShapeType.Sphere : p.shape == "hemisphere" ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Cone;
        shape.radius = radius;
        if (p.shape == "cone") shape.angle = Mathf.Clamp(p.cone, 1f, 89f);

        float mid = Mathf.Clamp(p.mid, 0.05f, 0.95f);
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(p.rgb[0], p.rgb[1], p.rgb[2]), 0f), new GradientColorKey(new Color(p.rgb[3], p.rgb[4], p.rgb[5]), mid), new GradientColorKey(new Color(p.rgb[6], p.rgb[7], p.rgb[8]), 1f) },
            new[] { new GradientAlphaKey(p.alpha[0], 0f), new GradientAlphaKey(p.alpha[1], mid), new GradientAlphaKey(p.alpha[2], 1f) });
        color.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, p.size[0] / maxSize), new Keyframe(mid, p.size[1] / maxSize), new Keyframe(1f, p.size[2] / maxSize)));

        if (p.rows * p.cols > 1)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = p.cols; sheet.numTilesY = p.rows;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = 1;
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = ParticleMaterial(alias, p);
    }

    // 리본 = TrailRenderer — 붙은 뼈가 움직일 때만 궤적이 그려진다(검 휘두름·이동)
    static void AddRibbon(Transform parent, string alias, Part p, float k)
    {
        if (p.spinDeg != 0f)
        {   // 같은 회전 뼈에 붙은 궤적 — 돌리는 부모 아래에 둬서 원을 그리게 한다(Ora_siki 리본)
            var spinner = new GameObject(p.name + "_spin");
            spinner.transform.SetParent(parent, false);
            AddSpin(spinner, p);
            parent = spinner.transform;
        }
        var go = new GameObject(p.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(p.pos[0], p.pos[1], p.pos[2]) * k;
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.05f, p.trail);
        trail.startWidth = p.width * k; trail.endWidth = p.widthEnd * k;
        trail.minVertexDistance = 0.3f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(p.rgb[0], p.rgb[1], p.rgb[2]), 0f), new GradientColorKey(new Color(p.rgb[0], p.rgb[1], p.rgb[2]), 1f) },
                  new[] { new GradientAlphaKey(p.alpha[0], 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.sharedMaterial = ParticleMaterial(alias, p);
    }

    static Material ParticleMaterial(string alias, Part p)
    {
        string texName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(p.texture));
        string safe = new string(texName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        bool blendMode = !p.additive;   // 원작 필터 「블렌드」는 텍스처 알파로 그린다 — 가산으로 그리면 사각 바탕이 비친다(10-01 flames&smoke)
        string path = $"{MatDir}/{alias}_{safe}{(blendMode ? "_blend" : "")}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            AssetDatabase.CopyAsset(AdditiveBase, path);
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        if (blendMode)
        {
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        // 원본 텍스처 폴더 = Src/<로스터>/ — 같은 이름이 로스터마다 다를 일은 없으니 별칭 폴더를 훑는다.
        Texture2D tex = AssetDatabase.FindAssets("t:Texture2D " + texName, new[] { $"{ResDir}/Src" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(a => a.EndsWith(Path.GetFileName(p.texture)))
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
        if (tex != null) m.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureDir(string path)
    {
        string full = Path.Combine(ProjectRoot, path);
        if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        AssetDatabase.Refresh();
    }
}
