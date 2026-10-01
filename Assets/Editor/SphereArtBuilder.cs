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
        public string kind, name, slot, model, texture, fbx, fbxObject, shape, states, attach;
        public float bodyHeightM, rate, life, speed, speedVar, cone, gravity, mid;
        public float[] pos, box, size, rgb, alpha;
        public bool additive, cutout;
        public int rows, cols;
    }
    [System.Serializable] class Roster { public string roster, alias; public List<Part> parts; }

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
            foreach (string old in AssetDatabase.FindAssets("t:Prefab", new[] { ResDir }).Select(AssetDatabase.GUIDToAssetPath).Where(a => Path.GetFileName(a).StartsWith(r.alias + "_")).ToList())
                AssetDatabase.DeleteAsset(old);
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

    static string BuildPrefab(string roster, string alias, string key, List<Part> parts)
    {
        var root = new GameObject(key);
        int meshes = 0, systems = 0;
        var notes = new StringBuilder();
        try
        {
            foreach (Part p in parts)
            {
                float k = GameBodyHeight / Mathf.Max(0.1f, p.bodyHeightM);
                if (p.kind == "mesh") { if (AddMesh(root.transform, roster, alias, p, k, notes)) meshes++; }
                else if (p.kind == "particle") { AddParticles(root.transform, alias, p, k); systems++; }
            }
            PrefabUtility.SaveAsPrefabAsset(root, $"{ResDir}/{key}.prefab");
        }
        finally { Object.DestroyImmediate(root); }
        return $"{roster} · {key}: 메시 {meshes} · 파티클 {systems}{notes}";
    }

    // ── 메시 ──────────────────────────────────────────────────────────────
    static bool AddMesh(Transform parent, string roster, string alias, Part p, float k, StringBuilder notes)
    {
        string fbxPath = $"{ResDir}/Src/{roster}/{p.fbx}";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (source == null) { notes.Append($" ⚠️ FBX 없음 {fbxPath}"); return false; }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.transform.SetParent(parent, false);
        instance.name = p.name;
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        bool found = false;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name != p.fbxObject) { Object.DestroyImmediate(r.gameObject); continue; }
            found = true;
            r.sharedMaterial = MeshMaterial(roster, alias, p);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        if (!found) { notes.Append($" ⚠️ 오브젝트 {p.fbxObject} 없음"); Object.DestroyImmediate(instance); return false; }
        instance.transform.localScale = Vector3.one * k;
        Bounds b = default; bool first = true;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        notes.Append($" [{p.name} 경계 {b.min:F1}~{b.max:F1}]");
        return true;
    }

    static Material MeshMaterial(string roster, string alias, Part p)
    {
        string path = $"{MatDir}/{alias}_{p.fbxObject}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResDir}/Src/{roster}/{p.texture}");
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

    static Material ParticleMaterial(string alias, Part p)
    {
        string texName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(p.texture));
        string safe = new string(texName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        string path = $"{MatDir}/{alias}_{safe}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            AssetDatabase.CopyAsset(AdditiveBase, path);
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
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
