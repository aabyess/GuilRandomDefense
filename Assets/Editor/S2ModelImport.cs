using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S2 원작 모델 반입(10-18 구현담당3). 입력: ~/grd/GRD_s2_models/<폴더>/(blender 산출 FBX + Textures/*.png + <이름>.json)를 Assets/Resources/S2/Models/<이름>/로 복사한 뒤
/// 이 도구가 재질(URP Lit, 텍스처 연결·알파 컷/가산)·애니(레거시 Animation 루프)·크기(최대 변 맞춤)를 넣어 Resources/S2/<프리팹>.prefab을 만든다.
///   call:S2ModelImport.Run    전부(복사 포함) — 에디터 전용, 여러 번 돌려도 같은 결과
/// 만드는 것: Saint_Warcury/Saturn/Nasjuro(오로성 실루엣) · GoldenBell · RumbaSoul · ElevatorDisc · KineticField · Egghead,
/// 그리고 적 건물용 Mob_GoldenBell · Mob_RumbaSoul · Mob_Egghead(스토리 건물 프리팹의 몸을 이 모델로 바꾼 것 — EnemyDummy 그대로).
/// Enemy_Story14_에그헤드.asset의 prefab도 Mob_Egghead로 바꾼다.
/// </summary>
public static class S2ModelImport
{
    static readonly string SrcRoot = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd/GRD_s2_models");
    const string DstRoot = "Assets/Resources/S2";
    const string StoryMobPrefab = "Assets/Prefabs/Generated/Mob_Story13_쉬었음.prefab";

    // 폴더 → (FBX 이름, 프리팹 이름, 목표 크기, 기준 'max'=최대 변 / 'height'=키, 애니 FBX(없으면 원본 FBX의 클립))
    struct Spec { public string folder, fbx, prefab; public float size; public bool byHeight; public string animFbx; }
    static readonly Spec[] Specs =
    {
        new Spec { folder = "war3mapImported__Saint_Warcury", fbx = "war3mapImported__Saint_Warcury", prefab = "Saint_Warcury", size = 120f },
        new Spec { folder = "war3mapImported__Saint_Saturn",  fbx = "war3mapImported__Saint_Saturn",  prefab = "Saint_Saturn",  size = 120f },
        new Spec { folder = "war3mapImported__Saint_Nasjuro", fbx = "war3mapImported__Saint_Nasjuro", prefab = "Saint_Nasjuro", size = 120f },
        new Spec { folder = "D__modelPR (10)", fbx = "D__modelPR (10)", prefab = "GoldenBell", size = 70f },
        new Spec { folder = "D__SmallLight", fbx = "D__SmallLight", prefab = "RumbaSoul", size = 40f },
        new Spec { folder = "gen_ElevatorPuzzle", fbx = "ElevatorPuzzle", prefab = "ElevatorDisc", size = 40f },
        new Spec { folder = "gen_kineticfield", fbx = "kineticfield", prefab = "KineticField", size = 24f },
        new Spec { folder = "gen_Egghead", fbx = "Egghead", prefab = "Egghead", size = 76f, byHeight = true, animFbx = "Egghead@Idle" },
    };

    public static string Run()
    {
        var sb = new System.Text.StringBuilder();
        Directory.CreateDirectory(DstRoot + "/Models");
        foreach (Spec s in Specs) CopyFolder(s, sb);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (Spec s in Specs) { try { Build(s, sb); } catch (System.Exception e) { sb.AppendLine($"   ❌ {s.prefab}: {e.Message}"); } }
        try { BuildMobs(sb); } catch (System.Exception e) { sb.AppendLine($"   ❌ Mob: {e.Message}"); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return sb.ToString();
    }

    static void CopyFolder(Spec s, System.Text.StringBuilder sb)
    {
        string src = Path.Combine(SrcRoot, s.folder);
        string dst = Path.Combine(DstRoot, "Models", s.prefab);
        if (!Directory.Exists(src)) { sb.AppendLine($"   ❌ 원본 폴더 없음: {src}"); return; }
        Directory.CreateDirectory(dst);
        foreach (string f in Directory.GetFiles(src, "*.fbx")) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (string f in Directory.GetFiles(src, "*.json")) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)) + ".txt", true);
        string tex = Path.Combine(src, "Textures");
        if (Directory.Exists(tex))
        {
            Directory.CreateDirectory(Path.Combine(dst, "Textures"));
            foreach (string f in Directory.GetFiles(tex, "*.png")) File.Copy(f, Path.Combine(dst, "Textures", Path.GetFileName(f)), true);
        }
    }

    static void SetLegacyLoop(string fbxPath)
    {
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) return;
        imp.animationType = ModelImporterAnimationType.Legacy;
        imp.importAnimation = true;
        var clips = imp.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips) { c.loopTime = true; c.wrapMode = WrapMode.Loop; }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static Dictionary<string, (string tex, string filter)> ReadMeshTable(string jsonTxt)
    {
        var map = new Dictionary<string, (string, string)>();
        if (!File.Exists(jsonTxt)) return map;
        var root = MiniJson.Parse(File.ReadAllText(jsonTxt)) as Dictionary<string, object>;
        if (root == null || !root.TryGetValue("meshes", out object meshes) || !(meshes is List<object> list)) return map;
        foreach (object o in list)
        {
            var d = o as Dictionary<string, object>;
            if (d == null || !d.ContainsKey("mesh")) continue;
            string tex = d.ContainsKey("textureFile") ? d["textureFile"] as string : null;
            string filter = d.ContainsKey("filter") ? d["filter"] as string : "none";
            map[(d["mesh"] as string ?? "").ToLowerInvariant()] = (tex ?? "", filter ?? "none");
        }
        return map;
    }

    static Material MakeMaterial(string name, string dir, Texture2D tex, string filter)
    {
        string path = $"{dir}/Materials/{name}.mat";
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), dir, "Materials"));
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex); }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
        if (filter == "additive" || filter == "add")
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_EmissionColor", Color.white * 0.6f); m.EnableKeyword("_EMISSION");
        }
        else if (filter == "none")   // 불투명 — 알파 칸이 다른 값을 담았을 수 있어 알파를 끈다(ArtBinder.MakeOpaqueSlot과 같은 이유)
        {
            m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = -1;
        }
        else   // transparent·blend: 알파 컷
        {
            m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        m.doubleSidedGI = true;
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // 판자형(깃발·날개)이 뒷면에서 사라지지 않게 양면
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Build(Spec s, System.Text.StringBuilder sb)
    {
        string dir = $"{DstRoot}/Models/{s.prefab}";
        string fbxPath = $"{dir}/{s.fbx}.fbx";
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) { sb.AppendLine($"   ❌ {s.prefab}: FBX를 못 읽음 {fbxPath}"); return; }
        SetLegacyLoop(fbxPath);
        string animPath = s.animFbx != null ? $"{dir}/{s.animFbx}.fbx" : fbxPath;
        if (s.animFbx != null) SetLegacyLoop(animPath);

        var table = ReadMeshTable($"{dir}/{s.fbx}.json.txt");
        var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { dir + "/Textures" }).Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).ToList();

        GameObject inst = (GameObject)Object.Instantiate(fbx);
        inst.name = s.prefab;
        int slotsDone = 0, noTex = 0;
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
        {
            Material[] src = r.sharedMaterials; var mats = new Material[Mathf.Max(1, src.Length)];
            for (int i = 0; i < mats.Length; i++)
            {
                string slot = i < src.Length && src[i] != null ? src[i].name : r.name;
                string key = slot.ToLowerInvariant();
                (string tex, string filter) entry = table.TryGetValue(key, out var e) ? e : table.FirstOrDefault(kv => key.Contains(kv.Key) || kv.Key.Contains(key)).Value;
                Texture2D t = !string.IsNullOrEmpty(entry.tex) ? textures.FirstOrDefault(x => x.name + ".png" == entry.tex || x.name == Path.GetFileNameWithoutExtension(entry.tex)) : null;
                if (t == null) t = textures.OrderByDescending(x => x.name.Length).FirstOrDefault(x => key.Contains(Path.GetFileNameWithoutExtension(x.name).ToLowerInvariant()) || Path.GetFileNameWithoutExtension(x.name).ToLowerInvariant().Contains(key));
                if (t == null && key.Contains("star")) t = textures.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("star"));
                if (t == null && key.Contains("ring")) t = textures.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("ring"));
                if (t == null && textures.Count == 1) t = textures[0];
                if (t == null) noTex++;
                string filt = string.IsNullOrEmpty(entry.filter) ? (key.EndsWith("_add") ? "additive" : "none") : entry.filter;
                mats[i] = MakeMaterial($"{s.prefab}_{slot}".Replace(' ', '_'), dir, t, filt);
                slotsDone++;
            }
            r.sharedMaterials = mats;
        }

        // 크기: 최대 변(또는 키)을 목표에 맞춘다. 배율은 **루트(holder)에 건다** — 애니 클립이 모델 노드의 localScale 곡선을 들고 있으면(에그헤드 실측: 건물이 점만 하게 나왔다)
        // 모델 노드에 건 배율을 덮어쓴다.
        Bounds b = Bounds(inst);
        float cur = s.byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.y, b.size.z);
        float factor = cur > 0.0001f ? s.size / cur : 1f;
        inst.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // 발을 바닥(0)에, 가운데를 원점에(배율 전 단위)
        var holder = new GameObject(s.prefab);
        holder.transform.localScale = Vector3.one * factor;
        inst.transform.SetParent(holder.transform, false);
        // 애니(레거시 루프)
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(animPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        string animNote = "정적";
        if (clip != null && s.prefab != "ElevatorDisc")
        {
            clip.legacy = true; clip.wrapMode = WrapMode.Loop;
            var anim = inst.AddComponent<Animation>();   // 모델 노드에 단다 — 클립이 루트 변환 곡선을 들고 있어도 holder의 배율은 안 덮인다 anim.clip = clip; anim.AddClip(clip, clip.name); anim.playAutomatically = true; anim.wrapMode = WrapMode.Loop;
            animNote = $"애니 {clip.name} {clip.length:F2}초";
        }
        PrefabUtility.SaveAsPrefabAsset(holder, $"{DstRoot}/{s.prefab}.prefab");
        Object.DestroyImmediate(holder);
        sb.AppendLine($"   ✅ {s.prefab}: 재질 {slotsDone}(텍스처 못 찾음 {noTex}) · 크기 {cur:F2}→{s.size} · {animNote}");
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds); return b;
    }

    // 적 건물 프리팹: 스토리 건물(Mob_Story13)의 몸(상자)을 걷고 모델을 형제 노드로 붙인다(EnemyDummy.CacheSkinNode가 렌더러 있는 형제를 스킨으로 인식).
    static void BuildMobs(System.Text.StringBuilder sb)
    {
        foreach ((string model, string mob) in new[] { ("GoldenBell", "Mob_GoldenBell"), ("RumbaSoul", "Mob_RumbaSoul"), ("Egghead", "Mob_Egghead") })
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{DstRoot}/{model}.prefab");
            if (modelPrefab == null) { sb.AppendLine($"   ❌ {mob}: {model}.prefab 없음"); continue; }
            GameObject root = PrefabUtility.LoadPrefabContents(StoryMobPrefab);
            try
            {
                // 스토리 건물 프리팹엔 「몸」 말고도 원작 스킨(나뭇잎 광장 등)이 중첩 프리팹으로 붙어 있다 — 몸 외 자식은 전부 걷는다.
                for (int c = root.transform.childCount - 1; c >= 0; c--)
                    if (root.transform.GetChild(c).name != "몸") Object.DestroyImmediate(root.transform.GetChild(c).gameObject);
                Transform body = root.transform.Find("몸");
                if (body != null)
                    foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
                    {
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        Object.DestroyImmediate(r);
                        if (mf != null) Object.DestroyImmediate(mf);
                    }
                GameObject skin = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
                skin.name = "S2Model";
                skin.transform.localPosition = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(root, $"{DstRoot}/{mob}.prefab");
                sb.AppendLine($"   ✅ {mob}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // 맵 목표물 적 자산(멀티 카탈로그에 들어가야 클라에 보인다 — 런타임 ScriptableObject는 카탈로그 밖이라 클라에 안 보였다, 10-18 구현담당2 실측)
        EnemyData template = AssetDatabase.LoadAssetAtPath<EnemyData>($"{DstRoot}/Enemy_Story14_에그헤드.asset");
        foreach ((string file, string label, float hp, string mob) in new[] { ("Enemy_S2_GoldenBell", "샨도라 황금 종", S2MapObjectives.BellHp, "Mob_GoldenBell"), ("Enemy_S2_RumbaSoul", "룸바 해적단의 영혼", S2MapObjectives.SoulHp, "Mob_RumbaSoul") })
        {
            string path = $"{DstRoot}/{file}.asset";
            EnemyData e = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (e == null) { e = Object.Instantiate(template); AssetDatabase.CreateAsset(e, path); }
            e.enemyName = label; e.hp = hp; e.armor = 0f; e.hpRegenPerSecond = 0f; e.isBoss = false; e.goldReward = 0; e.moveSpeed = 0f; e.visualScale = 1f;
            e.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{DstRoot}/{mob}.prefab");
            EditorUtility.SetDirty(e); sb.AppendLine($"   ✅ {file}(체력 {hp:N0})");
        }
        // 에그헤드 적 자산에 새 프리팹 연결
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>($"{DstRoot}/Enemy_Story14_에그헤드.asset");
        var egg = AssetDatabase.LoadAssetAtPath<GameObject>($"{DstRoot}/Mob_Egghead.prefab");
        if (enemy != null && egg != null) { enemy.prefab = egg; enemy.visualScale = 1f; enemy.bossModelHeight = 76f; EditorUtility.SetDirty(enemy); sb.AppendLine("   ✅ Enemy_Story14_에그헤드.prefab = Mob_Egghead(키 76)"); }
    }
}
