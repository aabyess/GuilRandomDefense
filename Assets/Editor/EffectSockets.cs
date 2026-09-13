using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Blender 모델에 들어 있는 빈 오브젝트 「불_자리_NN」·「연기_자리_NN」에 움직이는 불꽃·연기를 붙인다.
///
/// 왜 메시가 아니라 파티클인가 — 사실적 화풍에서 멈춘 판 연기는 위에서 보면 오린 종이처럼 보였다
/// (화난 하이츠 1차 렌더, 2026-09-12). 그래서 Blender 쪽은 자리만 표시하고, 움직임은 유니티가 맡는다.
///
/// 크기는 **월드 단위**다. 소켓의 부모(FBX 루트)는 임포트 축척·맞춤 배율로 크기가 제각각이라, 붙이는 순간
/// 부모 크기를 상쇄해 두고 Hierarchy 스케일을 쓴다 — 건물이 80이든 화로가 10이든 불꽃은 같은 자로 잰다.
/// </summary>
public static class EffectSockets
{
    const string Folder = "Assets/Art/Effects";
    const string SoftDotPath = Folder + "/SoftDot.png";
    const string FireMaterialPath = Folder + "/Fire.mat";
    const string SmokeMaterialPath = Folder + "/Smoke.mat";

    const string FirePrefix = "불_자리";
    const string SmokePrefix = "연기_자리";
    const string GlowPrefix = "빛_자리";   // 포탈 막 가운데(2026-09-13). 「트로피_자리」는 파티클이 아니라 업적 트로피 자리라 건드리지 않는다.

    static string Nfc(string s) => s?.Normalize(System.Text.NormalizationForm.FormC);

    /// <summary>
    /// root 아래 소켓을 전부 찾아 효과를 붙인다. 이미 붙은 소켓은 건너뛴다. 붙인 개수를 돌려준다.
    /// glow = 빛_자리의 빛깔(포탈 막 색에 맞춘다). 안 주면 옅은 흰빛.
    /// glowLight = 빛_자리에 점광원도 달지. 뽑기 섬처럼 포탈이 수십 개 모인 곳은 끈다(URP 물체당 광원 수 한도).
    /// </summary>
    public static int Attach(GameObject root, Color? glow = null, bool glowLight = true)
    {
        if (root == null) return 0;

        List<Transform> fireSockets = new List<Transform>();
        List<Transform> smokeSockets = new List<Transform>();
        List<Transform> glowSockets = new List<Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string name = Nfc(t.name);
            if (name.StartsWith(FirePrefix)) fireSockets.Add(t);
            else if (name.StartsWith(SmokePrefix)) smokeSockets.Add(t);
            else if (name.StartsWith(GlowPrefix)) glowSockets.Add(t);
        }

        if (fireSockets.Count == 0 && smokeSockets.Count == 0 && glowSockets.Count == 0) return 0;

        EnsureAssets(out Material fire, out Material smoke);
        Color tint = glow ?? new Color(0.9f, 0.92f, 1f);

        int attached = 0;
        foreach (Transform socket in fireSockets)
            if (AttachOnce(socket, "불꽃", go => { BuildFire(go, fire); AddFireLight(go); })) attached++;
        foreach (Transform socket in smokeSockets)
            if (AttachOnce(socket, "연기", go => BuildSmoke(go, smoke))) attached++;
        foreach (Transform socket in glowSockets)
            if (AttachOnce(socket, "빛", go => { BuildGlow(go, fire, tint); if (glowLight) AddGlowLight(go, tint); })) attached++;
        return attached;
    }

    static bool AttachOnce(Transform socket, string childName, System.Action<GameObject> build)
    {
        if (socket.Find(childName) != null) return false;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(socket, false);

        // 부모 크기를 상쇄한다 — 이 아래로는 월드 단위.
        Vector3 lossy = socket.lossyScale;
        go.transform.localScale = new Vector3(SafeInverse(lossy.x), SafeInverse(lossy.y), SafeInverse(lossy.z));
        go.transform.rotation = Quaternion.identity;   // FBX 축 변환(-90°)을 따라 눕지 않게 월드 위쪽으로

        build(go);
        return true;
    }

    static float SafeInverse(float v) => Mathf.Abs(v) < 1e-6f ? 1f : 1f / v;

    // 장작불 — 짧게 살고 위로 솟으며 노랑 → 주황 → 빨강으로 사그라든다. 더해 그리기(Additive).
    static void BuildFire(GameObject go, Material material)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 0.78f, 0.35f, 1f);
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 28f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 1.4f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // 원추가 위를 보게

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.1f), new Color(0.6f, 0.1f, 0.05f));

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // 연기 — 오래 살며 천천히 부풀고 흔들리며 옅어진다. 알파 섞기.
    static void BuildSmoke(GameObject go, Material material)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.22f, 0.21f, 0.2f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 5f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 1.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.3f, 0.28f, 0.26f), 0f), new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.8f));

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.25f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // 불빛 — 둘레의 인형·땅이 주황으로 물들어야 「불이 있다」로 읽힌다. 깜빡임은 FireLightFlicker(런타임).
    static void AddFireLight(GameObject go)
    {
        GameObject lightObject = new GameObject("불빛");
        lightObject.transform.SetParent(go.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 2f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.2f);
        light.intensity = 6f;
        light.range = 28f;
        light.shadows = LightShadows.None;
        lightObject.AddComponent<FireLightFlicker>();
    }

    // 포탈 빛 — 막 가운데에서 작은 빛 알갱이가 천천히 떠돌며 사라진다. 불꽃 재질(더해 그리기)을 같이 쓴다.
    static void BuildGlow(GameObject go, Material material, Color tint)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        main.startColor = tint;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 50;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 12f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.5f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(Color.white, tint, tint);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.4f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // 포탈 둘레 땅이 막 색으로 물들게. 불과 달리 깜빡이지 않는다.
    static void AddGlowLight(GameObject go, Color tint)
    {
        GameObject lightObject = new GameObject("빛_조명");
        lightObject.transform.SetParent(go.transform, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = tint;
        light.intensity = 2.5f;
        light.range = 14f;
        light.shadows = LightShadows.None;
    }

    static Gradient Fade(Color start, Color middle, Color end)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.5f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    // 텍스처 없이 부드러운 원 한 장을 만들어 쓴다 — 외부 에셋을 기다리지 않는다.
    static void EnsureAssets(out Material fire, out Material smoke)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Effects");

        Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        if (dot == null)
        {
            const int size = 64;
            Texture2D made = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.SmoothStep(1f, 0f, d);
                made.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            File.WriteAllBytes(SoftDotPath, made.EncodeToPNG());
            Object.DestroyImmediate(made);
            AssetDatabase.ImportAsset(SoftDotPath);
            dot = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        }

        fire = LoadOrCreateParticleMaterial(FireMaterialPath, dot, additive: true);
        smoke = LoadOrCreateParticleMaterial(SmokeMaterialPath, dot, additive: false);
    }

    static Material LoadOrCreateParticleMaterial(string path, Texture2D texture, bool additive)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;   // 사장님이 만진 값은 지킨다

        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Surface", 1f);                        // 투명
        material.SetFloat("_Blend", additive ? 2f : 0f);          // 2 = Additive, 0 = Alpha
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) material.EnableKeyword("_BLENDMODE_ADD");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
