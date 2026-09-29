using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 스킬 기본 이펙트(2026-09-29, 「스킬 전체 살리기」) — 스킬마다 이펙트를 따로 달지 않고 **효과 종류별 공용 이펙트**를
// 코드로 만든다. 텍스처는 Kenney Particle Pack(CC0, Assets/Art/Effects/Kenney), 재질은 Resources/Effects/Skill_*.mat
// (에디터 SkillVfxMaterials.Build가 만든다). 재질이 없으면 조용히 아무것도 안 한다 — 게임 동작엔 영향 없다.
//
// 붙는 자리는 EnemyDummy(받는 쪽)다: 스킬 피해·스턴·이감·방깎. 누가 걸었든(유닛 스킬·도움소) 같은 모양이 뜬다.
// ⚠️ MP: 이 함수들은 판정이 도는 호스트에서만 불린다 — 친구 화면엔 아직 안 뜬다(거울에 신호를 실어야 함).
public static class SkillVfx
{
    public enum Kind { Hit, SpellHit, ArmorBreak, Stun, Slow, Buff }

    // 세계 단위 크기 기준 — 사람 키(유닛) 30, 레인 적 22.5(ArtBinder).
    const float Unit = 22.5f;
    const int PoolPerKind = 24;

    // 사장님(09-29): 「원랜디는 특별함 정도부터 스킬 임팩트가 있다」 — 시전자 등급이 특별함 미만이면 이펙트를 안 띄운다.
    // UnitAttacker가 시전 앞뒤로 BeginCast/EndCast를 부른다. 시전 문맥 밖(도움소 주문 등)은 늘 띄운다.
    public static bool CasterAllowsVfx { get; private set; } = true;

    /// <summary>이펙트 전체 스위치 — 끄면 Burst·Attach가 아무것도 안 한다(성능 비교 점검 VfxProbe.ArenaStartNoVfx, 나중엔 설정 메뉴).</summary>
    public static bool Enabled = true;

    public static bool BeginCast(UnitData caster)
    {
        bool previous = CasterAllowsVfx;
        CasterAllowsVfx = caster == null || caster.grade >= UnitGrade.Special;
        return previous;
    }

    public static void EndCast(bool previous) => CasterAllowsVfx = previous;

    // MP: 한 번 터지는 이펙트를 실제로 띄울 때 알린다(등급 게이트·스로틀·풀을 다 지난 뒤) — 호스트의 NetGameState가 받아
    //     친구 화면으로 넘긴다. 위치는 카메라 쪽으로 당기기 전 값이다(당기기는 보는 사람 카메라 기준이라 받는 쪽이 한다).
    public static event System.Action<Kind, Vector3> Played;

    static readonly Dictionary<Kind, List<ParticleSystem>> pools = new Dictionary<Kind, List<ParticleSystem>>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static Transform root;

    static Material Mat(string texture, bool alpha = false)
    {
        string key = alpha ? texture + "|alpha" : texture;
        if (!materials.TryGetValue(key, out Material m))
        {
            m = Resources.Load<Material>("Effects/Skill_" + texture);
            // 09-29 가독성: 더하기 섞기(Skill_*.mat, _Blend 2)는 밝은 풀밭 위에서 색이 하얗게 날아가 대비가 약했다(기본 높이 374 실측).
            //   알파 섞기 사본을 실행 중에 만든다 — URP Particles/Unlit은 섞기가 _SrcBlend/_DstBlend 속성이라 키워드 없이 바뀐다.
            if (m != null && alpha)
            {
                m = new Material(m) { name = m.name + "_Alpha" };
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            }
            materials[key] = m;
        }
        return m;
    }

    // 카메라가 높을수록 크게(09-29): 진열 높이 220에서 맞춘 크기가 기본 게임 높이 374에선 점만 했다. 1~2배.
    const float ReferenceCameraHeight = 220f;
    const float MaxZoomScale = 2f;

    static float ZoomScale()
    {
        Camera cam = Camera.main;
        return cam != null ? Mathf.Clamp(cam.transform.position.y / ReferenceCameraHeight, 1f, MaxZoomScale) : 1f;
    }

    // 층마다 처음 크기(시작 크기·원 반지름·속도) — 풀에서 다시 쓸 때 그때 카메라 높이로 다시 곱한다.
    struct BaseSize { public float size, radius, speed; }
    static readonly Dictionary<ParticleSystem, BaseSize> baseSizes = new Dictionary<ParticleSystem, BaseSize>();

    static void ApplyZoom(ParticleSystem first)
    {
        float z = ZoomScale();
        foreach (ParticleSystem ps in first.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!baseSizes.TryGetValue(ps, out BaseSize b)) continue;
            ParticleSystem.MainModule main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(b.size * 0.85f * z, b.size * 1.15f * z);
            main.startSpeed = b.speed * z;
            ParticleSystem.ShapeModule shape = ps.shape;
            if (shape.enabled) shape.radius = b.radius * z;
        }
    }

    static Transform Root
    {
        get
        {
            if (root == null) root = new GameObject("SkillVfx").transform;
            return root;
        }
    }

    /// <summary>한 번 터지고 끝나는 이펙트(적중·방깎). 풀이 다 차 있으면 건너뛴다 — 수십 기가 한꺼번에 때려도 안 쌓인다.</summary>
    public static void Burst(Kind kind, Vector3 position)
    {
        if (!Enabled || !CasterAllowsVfx) return;
        if (!pools.TryGetValue(kind, out List<ParticleSystem> pool)) pools[kind] = pool = new List<ParticleSystem>();
        pool.RemoveAll(p => p == null);

        ParticleSystem free = null;
        foreach (ParticleSystem p in pool)
            if (!p.IsAlive(true)) { free = p; break; }
        if (free == null)
        {
            if (pool.Count >= PoolPerKind) return;
            free = Build(kind, Root, loop: false);
            if (free == null) return;
            pool.Add(free);
        }
        Played?.Invoke(kind, position);
        // 적 몸 가운데서 터지면 몸이 앞을 가린다 — 카메라 쪽으로 반 몸만큼 당긴다(워크3 이펙트처럼 몸 앞에 보이게).
        Camera cam = Camera.main;
        if (cam != null) position += (cam.transform.position - position).normalized * (0.5f * Unit);
        free.transform.position = position;
        ApplyZoom(free);
        free.Play(true);
    }

    /// <summary>켜져 있는 동안 따라다니는 이펙트(스턴·이감·버프). 돌려받은 것을 Stop으로 끈다.</summary>
    public static GameObject Attach(Kind kind, Transform target, float height)
    {
        if (target == null || !Enabled || !CasterAllowsVfx) return null;
        ParticleSystem ps = Build(kind, target, loop: true);
        if (ps == null) return null;
        ps.transform.localPosition = Vector3.up * height / Mathf.Max(0.0001f, target.lossyScale.y);
        ApplyZoom(ps);
        ps.Play(true);
        return ps.gameObject;
    }

    public static void Stop(GameObject attached)
    {
        if (attached == null) return;
        foreach (ParticleSystem ps in attached.GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(attached, 1.5f);
        baseSizes.Remove(attached.GetComponent<ParticleSystem>());
        foreach (ParticleSystem ps in attached.GetComponentsInChildren<ParticleSystem>()) baseSizes.Remove(ps);
    }

    // ── 모양 ─────────────────────────────────────────────────────────────

    static ParticleSystem Build(Kind kind, Transform parent, bool loop)
    {
        switch (kind)
        {
            case Kind.Hit:
                // 금빛 번쩍 + 흩어지는 별 조각 — 09-29: 짙은 금빛 판(알파)을 밑에 깔아 풀밭 위에서도 모양이 선다
                return Layered(parent, loop, "Hit",
                    Layer("star_09", new Color(1f, 0.62f, 0.1f, 0.95f), 0.45f, 1.2f * Unit, 1, 0f, Shape.None, alpha: true),
                    Layer("star_09", new Color(1f, 0.8f, 0.35f), 0.45f, 1.1f * Unit, 1, 0f, Shape.None),
                    Layer("star_07", new Color(1f, 0.9f, 0.6f), 0.7f, 0.5f * Unit, 6, 1.6f * Unit, Shape.Sphere));
            case Kind.SpellHit:
                // 마법(AP) — 푸른 번쩍 + 번개
                return Layered(parent, loop, "SpellHit",
                    Layer("star_09", new Color(0.2f, 0.45f, 1f, 0.95f), 0.45f, 1.2f * Unit, 1, 0f, Shape.None, alpha: true),
                    Layer("star_09", new Color(0.45f, 0.7f, 1f), 0.45f, 1.1f * Unit, 1, 0f, Shape.None),
                    Layer("spark_05", new Color(0.7f, 0.85f, 1f), 0.5f, 1.1f * Unit, 2, 0f, Shape.None, randomRotation: true));
            case Kind.ArmorBreak:
                // 붉은 할퀸 자국 + 퍼지는 붉은 고리
                return Layered(parent, loop, "ArmorBreak",
                    Layer("scratch_01", new Color(0.85f, 0.08f, 0.05f, 1f), 0.7f, 1.2f * Unit, 1, 0f, Shape.None, randomRotation: true, alpha: true),
                    Layer("circle_02", new Color(1f, 0.15f, 0.1f, 0.9f), 0.7f, 0.5f * Unit, 1, 0f, Shape.None, grow: 3f, alpha: true));
            case Kind.Stun:
                // 머리 위를 도는 노란 별 셋 — 09-29: 한 번에 셋을 **고르게** 뿌려 끝까지 돌린다(persistent). 전엔 초당 3.5개를
                //   0.9초 수명으로 흘려 보내 어느 순간이든 2~3개가 한쪽에 몰려 「왼쪽 위로 치우친」 것처럼 보였다.
                return Layered(parent, loop, "Stun",
                    Layer("symbol_02", new Color(1f, 0.85f, 0.05f, 1f), 0.9f, 0.9f * Unit, 3, 0f, Shape.Ring, orbit: 5f, persistent: loop, alpha: true),
                    // 같은 자리·같은 궤도에 더하기 섞기 별을 한 겹 더 — 알파 노랑만으론 풀밭 초록과 섞여 탁했다(09-29 기본 높이 실측).
                    Layer("symbol_02", new Color(1f, 1f, 0.6f), 0.9f, 0.75f * Unit, 3, 0f, Shape.Ring, orbit: 5f, persistent: loop),
                    Layer("star_07", new Color(1f, 0.95f, 0.5f), 0.5f, 0.6f * Unit, 0, 0f, Shape.Ring, rate: loop ? 4f : 0f));
            case Kind.Slow:
                // 발밑 푸른 마법진 + 떨어지는 서리
                return Layered(parent, loop, "Slow",
                    Layer("circle_03", new Color(0.1f, 0.45f, 1f, 0.9f), 1.0f, 1.8f * Unit, 0, 0f, Shape.None, rate: loop ? 1.5f : 0f, ground: true, alpha: true),
                    Layer("magic_01", new Color(0.5f, 0.85f, 1f, 1f), 1.0f, 1.8f * Unit, 0, 0f, Shape.None, rate: loop ? 1.2f : 0f, ground: true),
                    Layer("trace_06", new Color(0.6f, 0.85f, 1f), 0.6f, 0.35f * Unit, 0, -0.8f * Unit, Shape.Ring, rate: loop ? 6f : 0f));
            case Kind.Buff:
                // 몸을 감고 오르는 초록·금빛 소용돌이
                return Layered(parent, loop, "Buff",
                    Layer("twirl_01", new Color(0.15f, 0.8f, 0.2f, 0.9f), 0.8f, 0.9f * Unit, loop ? 0 : 2, 0.6f * Unit, Shape.None, rate: loop ? 2f : 0f, randomRotation: true, alpha: true),
                    Layer("circle_03", new Color(1f, 0.85f, 0.3f, 0.6f), 1.0f, 1.3f * Unit, loop ? 0 : 1, 0f, Shape.None, rate: loop ? 1f : 0f, ground: true));
        }
        return null;
    }

    enum Shape { None, Sphere, Ring }

    struct LayerSpec
    {
        public string texture; public Color color; public float lifetime; public float size; public int burst;
        public float speed; public Shape shape; public float rate; public bool ground; public bool randomRotation;
        public float grow; public float orbit; public bool persistent; public bool alpha;
    }

    static LayerSpec Layer(string texture, Color color, float lifetime, float size, int burst, float speed, Shape shape,
                           float rate = 0f, bool ground = false, bool randomRotation = false, float grow = 0f, float orbit = 0f,
                           bool persistent = false, bool alpha = false) =>
        new LayerSpec { texture = texture, color = color, lifetime = lifetime, size = size, burst = burst, speed = speed,
                        shape = shape, rate = rate, ground = ground, randomRotation = randomRotation, grow = grow, orbit = orbit,
                        persistent = persistent, alpha = alpha };

    static ParticleSystem Layered(Transform parent, bool loop, string name, params LayerSpec[] layers)
    {
        ParticleSystem first = null;
        Transform holder = null;
        foreach (LayerSpec spec in layers)
        {
            Material mat = Mat(spec.texture, spec.alpha);
            if (mat == null) return null;   // 재질이 아직 안 만들어졌다 — 이펙트 없이 게임은 그대로.

            GameObject go = new GameObject(first == null ? "Vfx_" + name : spec.texture);
            if (first == null) go.transform.SetParent(parent, false);
            else go.transform.SetParent(holder, false);
            // 부모(적)의 배율에 안 끌려가게 월드 크기로 산다.
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            // persistent: 처음 한 번 뿌린 입자가 Stop(→ 1.5초 뒤 Destroy)까지 산다(스턴 별처럼 걸려 있는 동안 계속 보여야 하는 것).
            main.loop = loop && !spec.persistent;
            main.duration = loop && !spec.persistent ? 1f : Mathf.Max(0.1f, spec.lifetime);
            main.startLifetime = spec.persistent ? 3600f : spec.lifetime;
            main.startSpeed = spec.speed;
            main.startSize = new ParticleSystem.MinMaxCurve(spec.size * 0.85f, spec.size * 1.15f);
            main.startColor = spec.color;
            main.startRotation = spec.randomRotation ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : 0f;
            main.simulationSpace = spec.orbit > 0f ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 32;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = spec.rate;
            if (spec.burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.burst) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = spec.shape != Shape.None;
            if (spec.shape == Shape.Sphere) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f * Unit; }
            if (spec.shape == Shape.Ring)
            {
                shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.45f * Unit; shape.rotation = new Vector3(90f, 0f, 0f); shape.radiusThickness = 0f;
                // 한 번에 뿌리는 입자를 원 둘레에 같은 간격으로(셋이면 120°씩) — 무작위면 한쪽에 몰린다.
                if (spec.burst > 1) shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            }

            if (spec.orbit > 0f)
            {
                ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.orbitalY = spec.orbit;
            }

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = !spec.persistent;   // 3600초 수명에 서서히 나타나기를 걸면 몇 분 동안 안 보인다
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            if (spec.grow > 0f)
            {
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, spec.grow));
            }

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.renderMode = spec.ground ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;

            baseSizes[ps] = new BaseSize { size = spec.size, radius = ps.shape.radius, speed = spec.speed };
            if (first == null) { first = ps; holder = go.transform; }
        }
        return first;
    }
}
