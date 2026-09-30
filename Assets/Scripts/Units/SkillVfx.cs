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
    // ⚠️ MP RPC가 int로 싣는다 — 새 종류는 맨 뒤에만 붙인다.
    //   StunImpact: 스턴이 걸리는 순간 발구르기 한 번(Attach(Stun)이 로컬로 부른다 — Played 안 알림, 친구 화면도 자기 Attach에서 튼다).
    //   Smash: 강타·범위 폭발(1.2.2 정의만 — 강타 데이터 연결 때 부른다).
    public enum Kind { Hit, SpellHit, ArmorBreak, Stun, Slow, Buff, StunImpact, Smash }

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
        currentEntry = null;
        return previous;
    }

    /// <summary>스킬 시전 문맥(09-30): 등급 게이트 + 이 스킬의 이펙트 표 칸(SkillVfxTable) — EndCast까지 적중 이펙트를 그 스킬 것으로 바꾼다.</summary>
    public static bool BeginCast(UnitData caster, SkillData skill)
    {
        bool previous = BeginCast(caster);
        currentEntry = CasterAllowsVfx ? Table?.Find(skill) : null;
        return previous;
    }

    public static void EndCast(bool previous) { CasterAllowsVfx = previous; currentEntry = null; }

    /// <summary>등급 게이트만 잠깐 바꾼다(스킬 표 칸은 그대로) — 시전 안에서 동기로 도는 여러 번 때리기 첫 타가 EndCast로 칸을 지우지 않게.</summary>
    public static bool SetCasterGate(bool allowed) { bool previous = CasterAllowsVfx; CasterAllowsVfx = allowed; return previous; }

    // ── 스킬별 이펙트(09-30, 무료 팩 Assets/ThirdParty + Docs/research/SKILL_VFX_MAPPING.csv) ──────────────
    static SkillVfxTable table;
    static bool tableLoaded;
    static SkillVfxTable.Entry currentEntry;

    static SkillVfxTable Table
    {
        get
        {
            if (!tableLoaded) { table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable"); tableLoaded = true; }
            return table;
        }
    }

    // 크기 기준(세계 단위 지름): 적중은 적 몸보다 조금 크게, 시전자는 사람 키 둘, 범위는 반경×2(너무 작으면 사람 키 둘).
    const float HitDiameter = 1.4f * Unit;
    const float CasterDiameter = 2f * Unit;
    const float MinAreaDiameter = 2f * Unit;
    const int PrefabPoolCap = 16;

    /// <summary>MP: 팩 프리팹 이펙트를 실제로 띄울 때(번호·위치·지름) — 호스트의 NetGameState가 친구 화면으로 넘긴다.</summary>
    public static event System.Action<int, Vector3, float, bool> PlayedPrefab;

    static readonly Dictionary<int, List<GameObject>> prefabPools = new Dictionary<int, List<GameObject>>();

    /// <summary>시전 한 번에 한 번: 범위 중심 땅 위(반경에 맞춰)와 시전자 발밑. UnitAttacker.CastSkillLevel이 부른다.</summary>
    public static void CastAt(Vector3 aoeCenter, Vector3 casterPosition, float worldRange)
    {
        if (!Enabled || !CasterAllowsVfx || currentEntry == null) return;
        if (currentEntry.area.IsSet)
            PlaySlot(currentEntry.area, aoeCenter, Mathf.Max(MinAreaDiameter, worldRange * 2f), ground: true);
        if (currentEntry.caster.IsSet)
            PlaySlot(currentEntry.caster, casterPosition, CasterDiameter, ground: true);
    }

    static void PlaySlot(SkillVfxTable.Slot slot, Vector3 position, float diameter, bool ground)
    {
        if (slot.prefab >= 0) PlayPrefab(slot.prefab, position, diameter, ground, notify: true);
        else if (slot.kind >= 0) Burst((Kind)slot.kind, ground ? position + Vector3.up * (0.6f * Unit) : position, notify: true);
    }

    /// <summary>팩 프리팹 하나를 띄운다(풀). NetGameState가 친구 화면에서도 부른다(notify 끔).</summary>
    public static void PlayPrefab(int index, Vector3 position, float diameter, bool ground, bool notify)
    {
        if (!Enabled) return;
        SkillVfxTable t = Table;
        if (t == null || index < 0 || index >= t.prefabs.Count || t.prefabs[index] == null) return;
        if (!prefabPools.TryGetValue(index, out List<GameObject> pool)) prefabPools[index] = pool = new List<GameObject>();
        pool.RemoveAll(g => g == null);

        GameObject free = null;
        foreach (GameObject g in pool)
            if (!g.activeSelf || !AnyAlive(g)) { free = g; break; }
        if (free == null)
        {
            if (pool.Count >= PrefabPoolCap) return;
            free = MakePrefabInstance(t.prefabs[index]);
            pool.Add(free);
        }
        if (notify) PlayedPrefab?.Invoke(index, position, diameter, ground);

        Vector3 at = position;
        if (ground) at = FeetBelow(position + Vector3.up * (0.6f * Unit)) + Vector3.up * 0.5f;
        else
        {
            Camera cam = Camera.main;
            if (cam != null) at += (cam.transform.position - at).normalized * (0.5f * Unit);
        }
        float native = index < t.nativeSizes.Count && t.nativeSizes[index] > 0.05f ? t.nativeSizes[index] : 1f;
        free.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        free.transform.localScale = Vector3.one * (diameter / native * ZoomScale());
        free.SetActive(true);
        foreach (ParticleSystem ps in free.GetComponentsInChildren<ParticleSystem>(true))
            ps.Clear(false);
        foreach (ParticleSystem ps in free.GetComponentsInChildren<ParticleSystem>(true))
            if (ps.transform == free.transform || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null) ps.Play(true);
    }

    static bool AnyAlive(GameObject g)
    {
        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
            if (ps.IsAlive(false)) return true;
        return false;
    }

    // 팩 프리팹 사본: 끝나도 스스로 지우거나 끄지 않게(풀에서 다시 쓴다), 크기는 부모 배율을 따르게, 반복은 끔.
    //   Cartoon FX의 CFXR_Effect(카메라 흔들기·빛·자동 삭제)는 뗀다 — 우리 카메라를 흔들면 안 된다.
    static GameObject MakePrefabInstance(GameObject prefab)
    {
        GameObject g = Object.Instantiate(prefab, Root);
        foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && mb.GetType().Name.StartsWith("CFXR")) Object.Destroy(mb);
        foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.loop = false;
            main.playOnAwake = false;
        }
        return g;
    }

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

    // 메시 이펙트(1.2.2): blender 세션 메시 5종(Assets/Art/Effects/Meshes, README) — SkillVfxMaterials.BuildMeshes가
    //   Resources/Effects/Mesh_<이름>.asset으로 복사한다. 메시 1 = 대표 치수 1(게임 단위), Y-up. 없으면 그 층만 빠진다.
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    static Mesh MeshAsset(string name)
    {
        if (!meshes.TryGetValue(name, out Mesh m))
        {
            m = Resources.Load<Mesh>("Effects/Mesh_" + name);
            meshes[name] = m;
        }
        return m;
    }

    // 발밑(1.2.2): 원판·벽·기둥·땅 폭발은 바닥 가운데가 원점이다. Burst는 몸 점만 받으니 아래로 땅을 찾는다.
    //   땅 전용 레이어가 없어(전부 Default) WorldPick.TryHitGround처럼 트리거 무시 + 캐릭터(유닛·적) 건너뛰기, 가장 가까운 것.
    //   호스트·친구 화면이 같은 계산을 하므로 Played/RPC는 몸 점 하나로 충분하다.
    static readonly RaycastHit[] feetHits = new RaycastHit[16];

    static Vector3 FeetBelow(Vector3 from)
    {
        const float Up = 2f, Down = 3f * Unit;
        int count = Physics.RaycastNonAlloc(from + Vector3.up * Up, Vector3.down, feetHits, Up + Down, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 feet = from - Vector3.up * (0.6f * Unit);   // 못 찾으면 대충 몸 60% 아래(EnemyDummy가 VfxTop×0.6에서 부른다)
        for (int i = 0; i < count; i++)
        {
            Collider c = feetHits[i].collider;
            if (c == null || c.GetComponentInParent<Selectable>() != null || c.GetComponentInParent<EnemyDummy>() != null) continue;
            if (feetHits[i].distance >= best) continue;
            best = feetHits[i].distance;
            feet = feetHits[i].point;
        }
        return feet;
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
    struct BaseSize { public float size, radius, speed; public Vector3 aspect; }
    static readonly Dictionary<ParticleSystem, BaseSize> baseSizes = new Dictionary<ParticleSystem, BaseSize>();

    static void ApplyZoom(ParticleSystem first)
    {
        float z = ZoomScale();
        foreach (ParticleSystem ps in first.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!baseSizes.TryGetValue(ps, out BaseSize b)) continue;
            ParticleSystem.MainModule main = ps.main;
            if (main.startSize3D)
            {
                main.startSizeX = b.size * b.aspect.x * z;
                main.startSizeY = b.size * b.aspect.y * z;
                main.startSizeZ = b.size * b.aspect.z * z;
            }
            else main.startSize = new ParticleSystem.MinMaxCurve(b.size * 0.85f * z, b.size * 1.15f * z);
            main.startSpeed = b.speed * z;
            ParticleSystem.ShapeModule shape = ps.shape;
            if (shape.enabled) shape.radius = b.radius * z;
        }
    }

    static Transform Root
    {
        get
        {
            if (root == null)
            {
                root = new GameObject("SkillVfx").transform;
                root.gameObject.AddComponent<SkillVfxFollower>();   // 붙는 이펙트 따라가기·풀 반납(1.2.2)
            }
            return root;
        }
    }

    /// <summary>한 번 터지고 끝나는 이펙트(적중·방깎). 풀이 다 차 있으면 건너뛴다 — 수십 기가 한꺼번에 때려도 안 쌓인다.</summary>
    public static void Burst(Kind kind, Vector3 position) => Burst(kind, position, notify: true);

    // 발밑 층(메시 원판·벽·기둥)의 Transform — 풀에서 다시 쓸 때마다 이번 발밑으로 옮긴다.
    static readonly Dictionary<ParticleSystem, List<Transform>> feetLayers = new Dictionary<ParticleSystem, List<Transform>>();

    static void Burst(Kind kind, Vector3 position, bool notify)
    {
        if (!Enabled || !CasterAllowsVfx) return;
        // 스킬 시전 중의 적중(EnemyDummy가 피해마다 Hit/SpellHit를 부른다) — 이 스킬 표의 적중 칸으로 바꾼다.
        if ((kind == Kind.Hit || kind == Kind.SpellHit) && currentEntry != null && currentEntry.hit.IsSet)
        {
            if (currentEntry.hit.prefab >= 0) { PlayPrefab(currentEntry.hit.prefab, position, HitDiameter, ground: false, notify: notify); return; }
            if (currentEntry.hit.kind != (int)kind) kind = (Kind)currentEntry.hit.kind;
        }
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
        if (notify) Played?.Invoke(kind, position);
        Vector3 body = position;
        // 적 몸 가운데서 터지면 몸이 앞을 가린다 — 카메라 쪽으로 반 몸만큼 당긴다(워크3 이펙트처럼 몸 앞에 보이게).
        Camera cam = Camera.main;
        if (cam != null) position += (cam.transform.position - position).normalized * (0.5f * Unit);
        free.transform.position = position;
        // 발밑 층은 당기지 않은 몸 점 아래 땅에 — 바닥 판이 공중에 뜨거나 카메라 쪽으로 밀리지 않게.
        if (feetLayers.TryGetValue(free, out List<Transform> feet) && feet.Count > 0)
        {
            Vector3 ground = FeetBelow(body) + Vector3.up * 0.5f;   // 땅과 깊이가 겹쳐 깜빡이지 않게 조금 위
            foreach (Transform t in feet) if (t != null) t.position = ground;
        }
        ApplyZoom(free);
        free.Play(true);
    }

    /// <summary>켜져 있는 동안 따라다니는 이펙트(스턴·이감·버프). 돌려받은 것을 Stop으로 끈다.</summary>
    // 붙는 이펙트 풀(1.2.2) — 스턴·이감을 걸 때마다 GameObject 2~3개 + ParticleSystem을 새로 만들던 것을 다시 쓴다
    //   (157기 아레나에서 이펙트 몫 평균 +8ms, 947a0de3 측정). 종류당 AttachPoolCap개까지 만들고, 넘으면 이펙트만 안 띄운다(판정은 그대로).
    //   친구 화면 거울(NetEntity.SetAttached)도 같은 Attach/Stop을 타서 같은 풀을 쓴다.
    //   붙는 이펙트는 대상의 **자식이 아니다** — 루트 밑에 두고 SkillVfxFollower가 매 프레임 대상 위로 옮긴다. 스턴·이감 걸린 채
    //   적이 죽으면 Stop이 안 불려(EnemyDummy에 그 길이 없다) 자식이면 적과 같이 파괴돼 풀이 새던 것을 막는다 — 대상이 사라지면 스스로 반납.
    const int AttachPoolCap = 48;
    static readonly List<(ParticleSystem ps, Transform target, float height)> attachFollow = new List<(ParticleSystem, Transform, float)>();
    const float AttachFadeSeconds = 1.5f;
    static readonly Dictionary<Kind, List<ParticleSystem>> attachAll = new Dictionary<Kind, List<ParticleSystem>>();
    static readonly Dictionary<Kind, List<ParticleSystem>> attachFree = new Dictionary<Kind, List<ParticleSystem>>();
    static readonly Dictionary<ParticleSystem, Kind> attachKind = new Dictionary<ParticleSystem, Kind>();
    static readonly List<(ParticleSystem ps, float at)> attachReturning = new List<(ParticleSystem, float)>();

    public static GameObject Attach(Kind kind, Transform target, float height)
    {
        if (target == null || !Enabled || !CasterAllowsVfx) return null;
        ReclaimAttached();
        if (!attachAll.TryGetValue(kind, out List<ParticleSystem> all)) attachAll[kind] = all = new List<ParticleSystem>();
        if (!attachFree.TryGetValue(kind, out List<ParticleSystem> free)) attachFree[kind] = free = new List<ParticleSystem>();
        all.RemoveAll(p => p == null);   // 붙은 채 대상과 같이 파괴된 것(Stop 전에 적이 사라짐)
        free.RemoveAll(p => p == null);

        ParticleSystem ps;
        if (free.Count > 0) { ps = free[free.Count - 1]; free.RemoveAt(free.Count - 1); }
        else
        {
            if (all.Count >= AttachPoolCap) return null;
            ps = Build(kind, Root, loop: true);
            if (ps == null) return null;
            all.Add(ps);
            attachKind[ps] = kind;
        }

        ps.transform.SetParent(Root, false);
        ps.transform.rotation = Quaternion.identity;
        ps.transform.position = target.position + Vector3.up * height;
        attachFollow.Add((ps, target, height));
        ps.gameObject.SetActive(true);
        ps.Clear(true);
        ApplyZoom(ps);
        ps.Play(true);

        // 스턴은 걸리는 순간 발구르기 한 번(벽 + 원판). 로컬로만 — 친구 화면은 자기 Attach(거울)에서 똑같이 튼다.
        if (kind == Kind.Stun) Burst(Kind.StunImpact, target.position + Vector3.up * (0.6f * Unit), notify: false);
        return ps.gameObject;
    }

    public static void Stop(GameObject attached)
    {
        if (attached == null) return;
        ParticleSystem ps = attached.GetComponent<ParticleSystem>();
        foreach (ParticleSystem layer in attached.GetComponentsInChildren<ParticleSystem>())
        {
            layer.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (layer.main.startLifetime.constantMax > 100f) layer.Clear(false);   // persistent(스턴 별) — 풀리면 바로 사라진다
        }
        if (ps == null || !attachKind.ContainsKey(ps)) { Object.Destroy(attached, AttachFadeSeconds); return; }
        attachFollow.RemoveAll(f => f.ps == ps);   // 남은 입자는 그 자리에서 흐려진다
        attachReturning.Add((ps, Time.time + AttachFadeSeconds));
    }

    /// <summary>SkillVfxFollower.LateUpdate가 부른다 — 붙은 이펙트를 대상 위로 옮기고, 대상이 사라진 것은 멈춰 반납, 흐려지기 끝난 것은 풀로.</summary>
    public static void TickAttached()
    {
        for (int i = attachFollow.Count - 1; i >= 0; i--)
        {
            (ParticleSystem ps, Transform target, float height) = attachFollow[i];
            if (ps == null) { attachFollow.RemoveAt(i); continue; }
            if (target == null || !target.gameObject.activeInHierarchy) { Stop(ps.gameObject); continue; }
            ps.transform.position = target.position + Vector3.up * height;
        }
        ReclaimAttached();
    }

    // 흐려지기가 끝난 것을 빈 풀로 — MonoBehaviour 없이 다음 Attach 때 걷는다.
    static void ReclaimAttached()
    {
        for (int i = attachReturning.Count - 1; i >= 0; i--)
        {
            (ParticleSystem ps, float at) = attachReturning[i];
            if (ps != null && Time.time < at) continue;
            attachReturning.RemoveAt(i);
            if (ps == null || !attachKind.TryGetValue(ps, out Kind kind)) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!attachFree.TryGetValue(kind, out List<ParticleSystem> free)) attachFree[kind] = free = new List<ParticleSystem>();
            free.Add(ps);
        }
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
                    Layer("star_07", new Color(1f, 0.9f, 0.6f), 0.7f, 0.5f * Unit, 6, 1.6f * Unit, Shape.Sphere),
                    // 1.2.2 메시: 누운 초승달 검기가 −60°→+60° 쓸고 지나간다(원작 검기 적중 mihawkslashhit·slash-gold)
                    Layer("vfx_slash_strip", new Color(1f, 0.93f, 0.65f), 0.3f, 30f, 1, 0f, Shape.None, mesh: "초승달_검기", spinY: 120f, randomYaw: true));
            case Kind.SpellHit:
                // 마법(AP) — 푸른 번쩍 + 번개
                return Layered(parent, loop, "SpellHit",
                    Layer("star_09", new Color(0.2f, 0.45f, 1f, 0.95f), 0.45f, 1.2f * Unit, 1, 0f, Shape.None, alpha: true),
                    Layer("star_09", new Color(0.45f, 0.7f, 1f), 0.45f, 1.1f * Unit, 1, 0f, Shape.None),
                    Layer("spark_05", new Color(0.7f, 0.85f, 1f), 0.5f, 1.1f * Unit, 2, 0f, Shape.None, randomRotation: true),
                    // 1.2.2 메시: 발밑에서 솟는 번개 기둥(폭 1→0.3) + 작은 충격파 원판(원작 MonsoonBoltTarget)
                    Layer("trace_06", new Color(0.75f, 0.9f, 1f), 0.4f, 80f, 1, 0f, Shape.None, mesh: "번개_기둥", feet: true, shrinkWidth: true),
                    Layer("circle_02", new Color(0.7f, 0.85f, 1f), 0.4f, 35f, 1, 0f, Shape.None, mesh: "충격파_원판", feet: true, growFrom: 0.3f));
            case Kind.ArmorBreak:
                // 붉은 할퀸 자국 + 퍼지는 붉은 고리
                return Layered(parent, loop, "ArmorBreak",
                    Layer("scratch_01", new Color(0.85f, 0.08f, 0.05f, 1f), 0.7f, 1.2f * Unit, 1, 0f, Shape.None, randomRotation: true, alpha: true),
                    Layer("circle_02", new Color(1f, 0.15f, 0.1f, 0.9f), 0.7f, 0.5f * Unit, 1, 0f, Shape.None, grow: 3f, alpha: true),
                    // 1.2.2 메시: 초승달 둘을 X자로(Y 45°·−45°, 원작 az-slash-red)
                    Layer("vfx_slash_strip", new Color(1f, 0.25f, 0.2f), 0.35f, 34f, 1, 0f, Shape.None, mesh: "초승달_검기", yaw: 45f),
                    Layer("vfx_slash_strip", new Color(1f, 0.25f, 0.2f), 0.35f, 34f, 1, 0f, Shape.None, mesh: "초승달_검기", yaw: -45f));
            case Kind.StunImpact:
                // 스턴 걸리는 순간 발구르기(원작 AOws) — 바깥으로 벌어진 먼지벽 + 흰 충격파 고리
                return Layered(parent, loop, "StunImpact",
                    Layer("vfx_wall_fade", new Color(0.92f, 0.82f, 0.62f), 0.6f, 45f, 1, 0f, Shape.None, mesh: "충격파_벽", feet: true, growFrom: 0.4f),
                    Layer("circle_03", new Color(1f, 1f, 0.95f), 0.5f, 55f, 1, 0f, Shape.None, mesh: "충격파_원판", feet: true, growFrom: 0.3f));
            case Kind.Smash:
                // 강타·범위 폭발(정의만, 1.2.2) — 땅 폭발 + 벽 + 원판, 주황(원작 E_WarStompCaster·NewDirtEXNofire). 흙먼지는 데이터 연결 때
                return Layered(parent, loop, "Smash",
                    Layer("vfx_spike", new Color(1f, 0.55f, 0.15f), 0.4f, 38f, 1, 0f, Shape.None, mesh: "땅_폭발", feet: true, growFrom: 0.2f),
                    Layer("vfx_wall_fade", new Color(1f, 0.7f, 0.35f), 0.6f, 45f, 1, 0f, Shape.None, mesh: "충격파_벽", feet: true, growFrom: 0.4f),
                    Layer("circle_03", new Color(1f, 0.8f, 0.5f), 0.5f, 60f, 1, 0f, Shape.None, mesh: "충격파_원판", feet: true, growFrom: 0.3f));
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
                    Layer("trace_06", new Color(0.6f, 0.85f, 1f), 0.6f, 0.35f * Unit, 0, -0.8f * Unit, Shape.Ring, rate: loop ? 6f : 0f),
                    // 1.2.2 메시: 가는 고리 원판이 느리게 퍼진다(원작 AHtc 천둥박수 둔화) — 붙는 자리가 이미 발밑이라 feet 불필요
                    Layer("circle_02", new Color(0.45f, 0.75f, 1f), 1.2f, 50f, 0, 0f, Shape.None, rate: loop ? 0.8f : 0f, mesh: "충격파_원판", growFrom: 0.3f));
            case Kind.Buff:
                // 몸을 감고 오르는 초록·금빛 소용돌이
                return Layered(parent, loop, "Buff",
                    Layer("twirl_01", new Color(0.15f, 0.8f, 0.2f, 0.9f), 0.8f, 0.9f * Unit, loop ? 0 : 2, 0.6f * Unit, Shape.None, rate: loop ? 2f : 0f, randomRotation: true, alpha: true),
                    Layer("circle_03", new Color(1f, 0.85f, 0.3f, 0.6f), 1.0f, 1.3f * Unit, loop ? 0 : 1, 0f, Shape.None, rate: loop ? 1f : 0f, ground: true),
                    // 1.2.2 메시: 번개 기둥을 낮고 넓게(높이 40·폭 1.5배), 초록
                    Layer("trace_06", new Color(0.45f, 1f, 0.45f), 0.5f, 80f, loop ? 0 : 1, 0f, Shape.None, rate: loop ? 1f : 0f, mesh: "번개_기둥", feet: !loop, shrinkWidth: true, aspect: new Vector3(1.5f, 0.5f, 1.5f)));
        }
        return null;
    }

    enum Shape { None, Sphere, Ring }

    struct LayerSpec
    {
        public string texture; public Color color; public float lifetime; public float size; public int burst;
        public float speed; public Shape shape; public float rate; public bool ground; public bool randomRotation;
        public float grow; public float orbit; public bool persistent; public bool alpha;
        // 1.2.2 메시 층: mesh = Resources/Effects/Mesh_<이름>. feet = 발밑 땅에(Burst). spinY = 수명 동안 Y축으로 도는 총 각(가운데 기준 ±반).
        //   yaw = 시작 Y각(도), randomYaw = 시작 Y각 무작위. growFrom = 크기 growFrom→1. shrinkWidth = 폭(X·Z)만 1→0.3. aspect = 축별 크기 배율.
        public string mesh; public bool feet; public float spinY, yaw; public bool randomYaw; public float growFrom; public bool shrinkWidth; public Vector3 aspect;
    }

    static LayerSpec Layer(string texture, Color color, float lifetime, float size, int burst, float speed, Shape shape,
                           float rate = 0f, bool ground = false, bool randomRotation = false, float grow = 0f, float orbit = 0f,
                           bool persistent = false, bool alpha = false, string mesh = null, bool feet = false, float spinY = 0f,
                           float yaw = 0f, bool randomYaw = false, float growFrom = 0f, bool shrinkWidth = false, Vector3? aspect = null) =>
        new LayerSpec { texture = texture, color = color, lifetime = lifetime, size = size, burst = burst, speed = speed,
                        shape = shape, rate = rate, ground = ground, randomRotation = randomRotation, grow = grow, orbit = orbit,
                        persistent = persistent, alpha = alpha, mesh = mesh, feet = feet, spinY = spinY, yaw = yaw, randomYaw = randomYaw,
                        growFrom = growFrom, shrinkWidth = shrinkWidth, aspect = aspect ?? Vector3.one };

    static ParticleSystem Layered(Transform parent, bool loop, string name, params LayerSpec[] layers)
    {
        ParticleSystem first = null;
        Transform holder = null;
        foreach (LayerSpec spec in layers)
        {
            Material mat = Mat(spec.texture, spec.alpha);
            if (mat == null) return null;   // 재질이 아직 안 만들어졌다 — 이펙트 없이 게임은 그대로.
            Mesh layerMesh = spec.mesh != null ? MeshAsset(spec.mesh) : null;
            if (spec.mesh != null && layerMesh == null) continue;   // 메시가 아직 복사 안 됐다(SkillVfxMaterials.Build) — 그 층만 뺀다

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

            if (layerMesh != null)
            {
                // 3D 크기·회전(메시는 빌보드가 아니다). 크기는 ApplyZoom이 startSizeX/Y/Z로 넣는다.
                main.startSize3D = true;
                main.startRotation3D = true;
                float yawDeg = spec.yaw - spec.spinY * 0.5f;
                main.startRotationX = 0f;
                main.startRotationY = spec.randomYaw ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : yawDeg * Mathf.Deg2Rad;
                main.startRotationZ = 0f;
                if (spec.spinY != 0f)
                {
                    ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                    rot.enabled = true;
                    rot.separateAxes = true;
                    rot.x = 0f; rot.z = 0f;
                    rot.y = spec.spinY * Mathf.Deg2Rad / Mathf.Max(0.05f, spec.lifetime);
                }
                if (spec.growFrom > 0f || spec.shrinkWidth)
                {
                    ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                    size.enabled = true;
                    size.separateAxes = true;
                    AnimationCurve grow = AnimationCurve.EaseInOut(0f, spec.growFrom > 0f ? spec.growFrom : 1f, 1f, 1f);
                    AnimationCurve width = spec.shrinkWidth ? AnimationCurve.Linear(0f, 1f, 1f, 0.3f) : grow;
                    size.x = new ParticleSystem.MinMaxCurve(1f, width);
                    size.y = new ParticleSystem.MinMaxCurve(1f, grow);
                    size.z = new ParticleSystem.MinMaxCurve(1f, width);
                }
            }

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.renderMode = spec.ground ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            if (layerMesh != null)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = layerMesh;
                r.alignment = ParticleSystemRenderSpace.World;   // 메시 축 그대로(Y-up) — View면 카메라를 따라 눕는다
            }

            baseSizes[ps] = new BaseSize { size = spec.size, radius = ps.shape.radius, speed = spec.speed, aspect = spec.aspect };
            if (first == null) { first = ps; holder = go.transform; }
            if (spec.feet)   // 첫 층(= 묶음 뿌리)이 발밑이어도 — 뿌리를 옮기면 다른 층도 따라가고, 발밑 층은 다시 같은 자리에 놓인다
            {
                if (!feetLayers.TryGetValue(first, out List<Transform> list)) feetLayers[first] = list = new List<Transform>();
                list.Add(go.transform);
            }
        }
        return first;
    }
}
