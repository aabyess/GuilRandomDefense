using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원작 상시 오라·부가 이펙트(2026-10-01 사장님 확정) — 유닛이 서 있는 동안 늘 붙어 있는 그림.
///   · 등급 오라: 초월 → 발밑 HandsAura2(빨간 번개 여섯 갈래 + 빛 덩어리), 히든 → 발밑 BlightwalkerAura(꽃잎 룬 고리).
///   · 캐릭터 오라와 원작 초월 모델에 들어 있던 부가 이펙트(손의 빛 구슬·번쩍임·불꽃…): <see cref="SphereArtTable"/> 한 줄 = 한 부품.
/// 붙는 곳: origin = 발밑 땅(유닛 회전을 안 따르는 세계 정렬 판, 크기 = 원작 단위 ÷ WorldScale) · 그 밖 = 뼈(HumanBodyBones 또는 이름) —
/// 뼈를 못 찾으면 몸 높이 비율 자리. 보이는 때(항상 / 공격 모션 중 / 스킬 중)도 부품마다.
///
/// 🔴 유닛 프리팹엔 UnitIdentity·OwnedByPlayer가 없다(UnitSpawner.Spawn이 소환 때 붙인다, 멀티 거울도 같다) → Awake 캐시 금지.
///    그래서 프리팹에 안 붙이고 <see cref="Installer"/>가 UnitIdentity.Active를 훑어 Data가 정해진 유닛에 이 컴포넌트를 단다.
///    소환·조합·멀티 거울 모두 같은 경로라 NetEntity는 건드리지 않는다(장식이라 판정과 무관).
/// </summary>
public class UnitSphereArt : MonoBehaviour
{
    /// <summary>끄면 새로 안 붙고 붙은 것도 숨긴다(성능 비교 점검용 — 나중엔 설정 메뉴).</summary>
    public static bool Enabled = true;

    const float GroundLift = 0.12f;          // 땅·선택 표시와 같은 높이로 겹쳐 깜박이지 않게 살짝 띄운다
    const float ChestRatio = 0.70f, HandRatio = 0.50f, FootRatio = 0.05f, SpriteRatio = 0.60f;   // 뼈를 못 찾을 때 몸 높이 비율
    const float BodyArtRatio = 0.28f;        // 임시 모양(손·가슴)의 지름 = 몸 높이 × 이 값

    class Part
    {
        public GameObject go;
        public SphereArtTable.When when;
        public bool origin;
        public bool placeholder;             // 임시 모양(코드로 그린 판) — 진짜 모델은 프리팹 회전 그대로
        public float diameter;               // 원점 오라 세계 지름
        public string attachedTo = "";       // Describe용 — 실제로 붙은 곳(뼈 이름 · body · 「뼈 못 찾음」)
    }

    readonly List<Part> parts = new List<Part>();
    UnitData data;
    Animator animator;
    bool hasSpeed;
    static readonly int SpeedHash = Animator.StringToHash(CharacterAnimator.SpeedParam);
    float skillUntil;
    bool visible = true;

    public static int AttachedUnits { get; private set; }
    public static int AttachedParts { get; private set; }

    /// <summary>스킬이 나간 동안 「스킬 중」 부품을 켠다(UnitAttacker가 시전 때 부르는 자리 — 파일럿 뒤 연결).</summary>
    public void PulseSkill(float seconds) => skillUntil = Time.time + seconds;

    void Setup(UnitData d)
    {
        data = d;
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter prm in animator.parameters) if (prm.nameHash == SpeedHash) hasSpeed = true;
        List<SphereArtTable.Art> arts = SphereArtTable.For(d);
        if (arts.Count == 0) return;
        float height = BodyHeight();
        foreach (SphereArtTable.Art art in arts) AddPart(art, height);
        if (parts.Count > 0) { AttachedUnits++; AttachedParts += parts.Count; }
    }

    void OnDestroy()
    {
        if (parts.Count > 0) { AttachedUnits--; AttachedParts -= parts.Count; }
        foreach (Part p in parts) if (p.go != null) Destroy(p.go);   // 뼈에 붙은 건 유닛과 함께 가지만 땅 판은 따로 둔다
    }

    float BodyHeight()
    {
        float top = transform.position.y, found = 0f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r.GetComponentInParent<SphereArtBillboard>() != null) continue;
            found = Mathf.Max(found, r.bounds.max.y - transform.position.y);
        }
        return found > 1f ? found : 30f;   // 유닛 기준 키 30(ArtBinder)
    }

    void AddPart(SphereArtTable.Art art, float height)
    {
        bool origin = art.attach == "origin";
        GameObject go = SphereArtTable.Create(art.key, flat: origin);
        Part part = new Part { go = go, when = art.when, origin = origin, placeholder = !SphereArtTable.HasRealModel(art.key) };

        if (origin)
        {
            // 발밑 땅 — 부모는 유닛 루트(함께 움직이되 LateUpdate에서 회전을 세계로 되돌린다). 크기 = 원작 단위 ÷ WorldScale.
            part.diameter = SphereArtTable.OriginDiameterMapUnits(art.key) / WorldScale.Value * (art.scale > 0f ? art.scale : 1f);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = art.pos + Vector3.up * GroundLift;
        }
        else
        {
            // 「follow:뼈」 = 부가 이펙트 프리팹(몸 좌표·발밑 원점, SphereArtBuilder) — 몸 자리에 세운 뒤 그 뼈에 월드 자리를 지킨 채 옮겨 붙여 뼈를 따라가게 한다.
            bool follow = art.attach.StartsWith("follow:");
            string spot = follow ? art.attach.Substring(7) : art.attach;
            // 「limb,left|right」 = 팔에 씌우는 메시 — 어깨→손 선분을 우리 팔에 맞춰(SphereArtLimb) 위팔 뼈에 붙인다
            bool limb = follow && spot.StartsWith("limb,");
            Transform bone = limb ? null : FindBone(spot);
            bool real = SphereArtTable.HasRealModel(art.key);
            if (limb && go.TryGetComponent(out SphereArtLimb fit))
            {
                string side = spot.Substring(5);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = art.pos;
                part.attachedTo = FitLimb(go.transform, fit, side == "left");
            }
            else if (follow)
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = art.pos;
                if (bone != null) go.transform.SetParent(bone, true);
                part.attachedTo = bone != null ? bone.name : (spot == "body" ? "body" : $"body(뼈 {spot} 못 찾음)");
            }
            else if (bone != null)
            {
                go.transform.SetParent(bone, false);
                go.transform.localPosition = art.pos;
                go.transform.localRotation *= Quaternion.Euler(art.euler);
                part.attachedTo = bone.name;
            }
            else
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = FallbackPosition(spot, height) + art.pos;
                part.attachedTo = $"몸 비율({spot})";
            }
            // 진짜 모델은 Blender가 정한 크기를 그대로(배율만 곱한다). 임시 모양은 몸 높이에 비례한 빛 구슬.
            // 뼈 자식의 lossyScale에 휘둘리지 않게 세계 크기로 맞춘다.
            float size = real ? 1f : height * BodyArtRatio;
            part.diameter = size * (art.scale > 0f ? art.scale : 1f);
        }
        if (!part.attachedTo.Contains("소매 맞춤")) ApplyScale(part);
        SetShown(part, (part.when & SphereArtTable.When.Idle) != 0 && visible);
        parts.Add(part);
    }

    // 소매 맞춤: 어깨 끝→손 끝 선분을 우리 팔(위팔 뼈→손 뼈)에 회전·크기로 맞추고 위팔 뼈에 월드 자리 유지 채 붙인다. 못 맞추면 body에 둔다.
    string FitLimb(Transform t, SphereArtLimb fit, bool left)
    {
        if (animator == null || !animator.isHuman) return "body(Humanoid 아님 — 소매 못 맞춤)";
        Transform upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        // 아래팔(팔꿈치→손)에 씌운다 — 위팔에 붙이면 팔꿈치를 굽혔을 때 소매가 팔을 따로 놀았다(10-01 김만경 사진)
        Transform root = lower != null ? lower : upper;
        if (root == null || hand == null) return "body(팔 뼈 못 찾음)";
        Vector3 s0 = t.TransformPoint(fit.start), h0 = t.TransformPoint(fit.end);
        Vector3 s1 = root.position, h1 = hand.position;
        Quaternion q = Quaternion.FromToRotation(h0 - s0, h1 - s1);
        float f = (h1 - s1).magnitude / Mathf.Max(1e-3f, (h0 - s0).magnitude) * fit.lengthBoost;
        t.rotation = q * t.rotation;
        t.position = s1 + q * (t.position - s0) * f;
        t.localScale *= f;
        t.SetParent(root, true);
        return root.name + "(소매 맞춤 ×" + f.ToString("0.00") + ")";
    }

    static void ApplyScale(Part p)
    {
        Transform t = p.go.transform;
        Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
        float inv = 1f / Mathf.Max(1e-4f, (parentScale.x + parentScale.y + parentScale.z) / 3f);
        t.localScale = Vector3.one * p.diameter * inv;
    }

    static void SetShown(Part p, bool on)
    {
        if (p.go != null && p.go.activeSelf != on) p.go.SetActive(on);
    }

    Transform FindBone(string attach)
    {
        if (attach == "body") return null;   // 유닛 루트(발밑 원점) — 부가 이펙트 프리팹은 몸 좌표 그대로(SphereArtBuilder)
        // ① 사람형 아바타가 있으면 HumanBodyBones
        HumanBodyBones? hb = HumanBone(attach);
        if (hb.HasValue && animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman)
        {
            Transform b = animator.GetBoneTransform(hb.Value);
            // Chest·UpperChest는 선택 뼈 — 믹사모 아바타엔 없을 때가 있다(10-01 구주호). 가까운 뼈로 대신한다.
            if (b == null && hb.Value == HumanBodyBones.Chest) b = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            if (b != null) return b;
        }
        // ② Generic(고유 동작 유닛 등)은 뼈 이름으로 찾는다 — 이름이 같거나 그 조각을 담은 첫 뼈
        string needle = NameNeedle(attach);
        if (needle == null) return null;
        Transform best = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Renderer>() != null || t.GetComponentInParent<SphereArtBillboard>() != null) continue;
            string n = t.name.Replace(" ", "").Replace("_", "").Replace(":", "").ToLowerInvariant();
            if (n == needle) return t;
            if (best == null && n.Contains(needle)) best = t;
        }
        return best;
    }

    static HumanBodyBones? HumanBone(string attach)
    {
        switch (attach)
        {
            case "chest": return HumanBodyBones.Chest;
            case "hand,left": return HumanBodyBones.LeftHand;
            case "hand,right": case "weapon": return HumanBodyBones.RightHand;
            case "foot,right": return HumanBodyBones.RightFoot;
        }
        return System.Enum.TryParse(attach, true, out HumanBodyBones b) && attach != "LastBone" ? b : (HumanBodyBones?)null;
    }

    // 뼈 이름으로 찾을 때의 조각 — 원작 붙는 곳 이름을 믹사모·Bip 이름 흔한 꼴로 옮긴다. 직접 뼈 이름을 적었으면 그대로.
    static string NameNeedle(string attach)
    {
        switch (attach)
        {
            case "origin": case "sprite": case "body": return null;
            case "chest": return "spine2";
            case "hand,left": return "lefthand";
            case "hand,right": case "weapon": return "righthand";
            case "foot,right": return "rightfoot";
        }
        return attach.Replace(" ", "").Replace("_", "").Replace(":", "").ToLowerInvariant();
    }

    static Vector3 FallbackPosition(string attach, float height)
    {
        switch (attach)
        {
            case "body": return Vector3.zero;
            case "chest": return new Vector3(0f, height * ChestRatio, 0f);
            case "hand,left": return new Vector3(-height * 0.18f, height * HandRatio, 0f);
            case "hand,right": case "weapon": return new Vector3(height * 0.18f, height * HandRatio, 0f);
            case "foot,right": return new Vector3(height * 0.08f, height * FootRatio, 0f);
            default: return new Vector3(0f, height * SpriteRatio, 0f);
        }
    }

    void LateUpdate()
    {
        bool want = Enabled;
        if (want != visible)
        {
            visible = want;
            foreach (Part p in parts) SetShown(p, want && Shown(p));
        }
        if (!visible) return;

        bool attacking = false, moving = false, skilling = Time.time < skillUntil;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            attacking = animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
            if (hasSpeed) moving = animator.GetFloat(SpeedHash) > 0.05f;
        }
        SphereArtTable.When now = attacking ? SphereArtTable.When.Attack : moving ? SphereArtTable.When.Move : SphereArtTable.When.Idle;
        if (skilling) now |= SphereArtTable.When.Skill;
        foreach (Part p in parts)
        {
            if (p.go == null) continue;
            if (p.origin) p.go.transform.rotation = p.placeholder ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;   // 유닛 회전을 안 따른다(세계 정렬)
            if (p.when == SphereArtTable.When.Always) continue;
            SetShown(p, (p.when & now) != 0);
        }
    }

    bool Shown(Part p) => (p.when & SphereArtTable.When.Idle) != 0;

    /// <summary>점검용 한 줄(call:UnitSphereArt.Describe).</summary>
    static string Describe()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"상시 오라: 켜짐 {Enabled} · 붙은 유닛 {AttachedUnits} · 부품 {AttachedParts}");
        foreach (UnitSphereArt a in FindObjectsByType<UnitSphereArt>(FindObjectsSortMode.None))
            sb.Append($"\n  {(a.data != null ? a.data.name : "?")}: " + string.Join(", ", a.parts.ConvertAll(p => p.go != null ? $"{p.go.name}@{(p.attachedTo.Length > 0 ? p.attachedTo : p.go.transform.parent != null ? p.go.transform.parent.name : "-")}{(p.go.activeSelf ? "" : "(숨김)")}" : "(없어짐)")));
        return sb.ToString();
    }

    // ── 유닛에 다는 곳 ─────────────────────────────────────────────────────
    public class Installer : MonoBehaviour
    {
        const float Interval = 0.4f;
        float next;
        static readonly List<UnitIdentity> scratch = new List<UnitIdentity>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<Installer>() != null) return;
            GameObject host = new GameObject("[UnitSphereArt]");
            DontDestroyOnLoad(host);
            host.AddComponent<Installer>();
        }

        void Update()
        {
            if (!Enabled || Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;
            scratch.Clear();
            scratch.AddRange(UnitIdentity.Active);
            foreach (UnitIdentity id in scratch)
            {
                if (id == null || id.Data == null || id.TryGetComponent(out UnitSphereArt _)) continue;
                // 창고·조합표 인형 같은 장식 유닛엔 안 단다(UnitIdentity가 없다) — 판 위의 유닛만.
                id.gameObject.AddComponent<UnitSphereArt>().Setup(id.Data);
            }
        }
    }
}
