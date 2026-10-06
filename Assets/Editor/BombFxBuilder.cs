using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 초월 엄태웅 「폭탄제조」 폭발 이펙트(10-06) — blender 시안 Tools/blender/gen_bomb_fx.py(~/GRD_bomb_fx/bomb_sheet.png)를 파티클로 옮긴 것.
// 구성(시안 순서): 지면 섬광 → 불덩이(노랑→주황→암적으로 식음) → 충격파 고리 → 파편 → 연기 기둥.
// 프리팹 단위: 피해 반경 = 1(지름 2). SkillVfxTable 측정(0.4초 뒤 가로 지름)이 ≈2가 되게 고리를 0.4초에 반경 1로 맞췄다.
// 텍스처는 무료 팩(Hovl GlowFree1·SmokeFree1, Kenney circle_02). 스킬 이펙트 표가 Assets/Art/Effects/Bomb도 찾는다(SkillVfxTableBuilder).
public static class BombFxBuilder
{
    const string Folder = "Assets/Art/Effects/Bomb";
    const string PrefabPath = Folder + "/Bomb_Explosion_Blender.prefab";
    const string Hovl = "Assets/ThirdParty/Hovl Studio/Magic effects pack/Textures/";

    [MenuItem("Tools/이펙트/폭탄 이펙트 만들기")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Effects", "Bomb");
        Material glowAdd = Mat("Bomb_Glow_Add", Hovl + "GlowFree1.png", additive: true);
        Material smokeAlpha = Mat("Bomb_Smoke_Alpha", Hovl + "SmokeFree1.png", additive: false);
        Material fireAdd = Mat("Bomb_Fire_Add", Hovl + "SmokeFree1.png", additive: true);
        Material ringAdd = Mat("Bomb_Ring_Add", "Assets/Art/Effects/Kenney/circle_02.png", additive: true);
        if (glowAdd == null || smokeAlpha == null || fireAdd == null || ringAdd == null) return "❌ 텍스처를 못 찾음";

        var root = new GameObject("Bomb_Explosion_Blender");
        // 뿌리 = 지면 섬광(납작하게 바닥에 깔린 빛, 반경 안쪽 0.8)
        ParticleSystem flash = Add(root, null, "Flash", glowAdd, 0f, 0.3f, 1.6f, 1, 0f, 0f);
        Flat(flash, 0.02f);
        Fade(flash, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.7f, 0.3f), 0.9f);
        Grow(flash, 0.5f, 1f);

        // 불덩이 속(더하기 — 발광): 노랑 → 주황 → 암적
        ParticleSystem core = Add(root, flash.transform, "FireCore", glowAdd, 0f, 0.8f, 0.9f, 6, 0.35f, 0.3f);
        Sphere(core, 0.2f);
        Gradient(core, new[] { (new Color(1f, 0.92f, 0.45f), 0f), (new Color(1f, 0.5f, 0.12f), 0.4f), (new Color(0.5f, 0.1f, 0.03f), 1f) },
                 new[] { (1f, 0f), (0.9f, 0.4f), (0f, 1f) });
        Grow(core, 0.5f, 1.2f);

        // 불덩이 몸(울퉁불퉁 덩이 — 연기 텍스처를 주황으로, 무작위 회전)
        ParticleSystem body = Add(root, flash.transform, "FireBody", fireAdd, 0.02f, 1.0f, 0.75f, 10, 0.45f, 0.35f);
        Sphere(body, 0.25f);
        RandomSpin(body);
        Gradient(body, new[] { (new Color(1f, 0.85f, 0.35f), 0f), (new Color(1f, 0.42f, 0.08f), 0.35f), (new Color(0.4f, 0.06f, 0.02f), 1f) },
                 new[] { (1f, 0f), (0.85f, 0.5f), (0f, 1f) });
        Grow(body, 0.6f, 1.3f);

        // 충격파 고리: 바닥에 누운 고리가 0.4초에 반경 1(피해 반경)까지 퍼지고 사라진다
        ParticleSystem ring = Add(root, flash.transform, "Shockwave", ringAdd, 0.05f, 0.55f, 2.2f, 1, 0f, 0.03f);
        Flat(ring, 0.03f);
        Fade(ring, new Color(1f, 0.9f, 0.7f), new Color(1f, 0.75f, 0.45f), 1f);
        GrowCurve(ring, new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.65f, 0.92f), new Keyframe(1f, 1f)));

        // 파편 40: 위로 흩어져 떨어지는 불티
        ParticleSystem debris = Add(root, flash.transform, "Debris", glowAdd, 0.05f, 0.8f, 0.07f, 40, 2.0f, 0.3f);
        ParticleSystem.ShapeModule ds = debris.shape;
        ds.enabled = true; ds.shapeType = ParticleSystemShapeType.Hemisphere; ds.radius = 0.2f;
        ParticleSystem.MainModule dm = debris.main;
        dm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        dm.gravityModifier = 0.35f;   // 프리팹 단위(반경 1)에 맞춘 중력 — 배율은 Hierarchy라 크기와 같이 커진다
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        Fade(debris, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.1f), 1f);

        // 연기 기둥: 불덩이가 식을 무렵(0.35초) 피어올라 위로
        ParticleSystem smoke = Add(root, flash.transform, "Smoke", smokeAlpha, 0.35f, 1.5f, 0.7f, 8, 0.15f, 0.45f);
        Sphere(smoke, 0.3f);
        RandomSpin(smoke);
        ParticleSystem.VelocityOverLifetimeModule v = smoke.velocityOverLifetime;
        v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
        v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(0.9f); v.z = new ParticleSystem.MinMaxCurve(0f);
        Gradient(smoke, new[] { (new Color(0.35f, 0.32f, 0.3f), 0f), (new Color(0.45f, 0.43f, 0.42f), 1f) },
                 new[] { (0f, 0f), (0.75f, 0.2f), (0.5f, 0.6f), (0f, 1f) });
        Grow(smoke, 0.6f, 1.5f);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return "✅ 폭탄 이펙트 " + PrefabPath + "\n" + AddToTable();
    }

    // 스킬 이펙트 표에 폭탄 한 칸만 덧붙인다 — 표 전체를 CSV에서 다시 만들면 CSV 경로가 낡은 19칸이 빠진다(10-06 확인).
    //   원래 지름은 설계값 2(피해 반경 1) — 0.4초 측정은 연기·불티가 섞여 들쭉날쭉하다.
    const string SkillPath = "Assets/Data/UnitSkills/SkillData_사장님_초월_엄태웅_AD_폭탄제조.asset";
    const string TablePath = "Assets/Resources/Effects/SkillVfxTable.asset";

    public static string AddToTable()
    {
        SkillVfxTable table = AssetDatabase.LoadAssetAtPath<SkillVfxTable>(TablePath);
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (table == null || skill == null || prefab == null) return "❌ 표·스킬·프리팹 중 없음";
        int index = table.prefabs.IndexOf(prefab);
        if (index < 0)
        {
            table.prefabs.Add(prefab);
            index = table.prefabs.Count - 1;
        }
        while (table.nativeSizes.Count < table.prefabs.Count) table.nativeSizes.Add(1f);
        table.nativeSizes[index] = 2f;
        table.entries.RemoveAll(e => e.skill == skill);
        var entry = new SkillVfxTable.Entry { skill = skill };
        entry.area.prefab = index;
        table.entries.Add(entry);
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssetIfDirty(table);
        return $"✅ 스킬 이펙트 표 [{index}] 폭탄제조 범위 칸";
    }

    static Material Mat(string name, string texPath, bool additive)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) return null;
        string path = $"{Folder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive)
        {
            m.SetFloat("_Blend", 2f);
            m.EnableKeyword("_BLENDMODE_ADD");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.One);
        }
        else
        {
            m.SetFloat("_Blend", 0f);
            m.DisableKeyword("_BLENDMODE_ADD");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }
        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    // 한 층: 버스트 한 번, 루프 없음, 크기는 부모 배율을 따른다(SkillVfx.PlayPrefab이 반경에 맞춰 키운다).
    static ParticleSystem Add(GameObject root, Transform parent, string name, Material mat, float delay, float lifetime,
                              float size, int burst, float speed, float height)
    {
        GameObject go;
        if (parent == null) go = root;
        else { go = new GameObject(name); go.transform.SetParent(parent, false); }
        go.transform.localPosition = parent == null ? Vector3.zero : Vector3.up * height;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startDelay = delay;
        main.startLifetime = lifetime;
        main.startSize = size;
        main.startSpeed = speed;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.stopAction = ParticleSystemStopAction.None;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (parent == null) go.transform.localPosition = Vector3.up * height;
        return ps;
    }

    static void Flat(ParticleSystem ps, float lift)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        ps.transform.localPosition = Vector3.up * lift;
    }

    static void Sphere(ParticleSystem ps, float radius)
    {
        ParticleSystem.ShapeModule s = ps.shape;
        s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = radius;
    }

    static void RandomSpin(ParticleSystem ps)
    {
        ParticleSystem.MainModule m = ps.main;
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    }

    static void Fade(ParticleSystem ps, Color from, Color to, float startAlpha) =>
        Gradient(ps, new[] { (from, 0f), (to, 1f) }, new[] { (startAlpha, 0f), (startAlpha, 0.3f), (0f, 1f) });

    static void Gradient(ParticleSystem ps, (Color c, float t)[] colors, (float a, float t)[] alphas)
    {
        var g = new UnityEngine.Gradient();
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
        var ak = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
        g.SetKeys(ck, ak);
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void Grow(ParticleSystem ps, float from, float to) =>
        GrowCurve(ps, new AnimationCurve(new Keyframe(0f, from / Mathf.Max(from, to)), new Keyframe(1f, to / Mathf.Max(from, to))));

    static void GrowCurve(ParticleSystem ps, AnimationCurve curve)
    {
        ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }
}
