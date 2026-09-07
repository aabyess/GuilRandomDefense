using System.Collections.Generic;
using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] float hp = 10f;

    // MobPrefab 루트(콜라이더·NavMeshAgent가 걸린 곳)의 자식 — 메시만 들고 있는 시각 부위
    // ("몸"). EnemyData.visualScale은 여기에만 곱한다(Initialize 참고) — 루트를 키우면
    // 콜라이더까지 커져 유닛이 밀려나 사거리 밖으로 나갈 수 있다.
    [SerializeField] Transform visualRoot;
    Vector3 baseVisualScale = Vector3.one;

    EnemyData data;
    bool isDead;

    // 01번 영웅 스탯(사장님 결정 2026-09-06) — UnitAttacker가 자기 타격이 숨통을 끊었는지
    // 판정하려면(원작 "적을 죽일 때마다 AddHeroXP") TakeDamage 앞뒤로 이 값을 비교해야 한다.
    public bool IsDead => isDead;

    public static readonly List<EnemyDummy> Active = new List<EnemyDummy>();

    // 라운드 보스(EnemyData.isBoss)가 죽으면 몇 라운드였는지를 실어 알린다. 보스가 여러
    // 라운드에 걸쳐 여럿이라(현재 9종 예정) "보스가 죽었다"만으로는 부족해서 라운드 번호를 싣는다.
    // TakeDamage의 사망 처리 흐름 안에서는 이 신호만 보내고, 실제 처리(도박소 해금 등)는
    // 구독하는 쪽이 한다 — 사망 처리는 가벼워야 한다.
    public static event System.Action<int> OnBossKilled;

    static RoundManager roundManagerCache;
    static RoundManager RoundManagerRef => roundManagerCache != null
        ? roundManagerCache
        : roundManagerCache = FindFirstObjectByType<RoundManager>();

    public int SpawnRound { get; private set; }

    public float Hp => hp;
    public float MaxHp { get; private set; }

    // 대상 이동속도 — SkillEffectBasis.TargetMoveSpeed가 읽는다(2026-09-06). data가 private
    // 이라 노출만 새로 뚫었다. data가 아직 없으면 0 — 그 경우 그 basis는 bonus만 남는다.
    // ⚠️ 2026-09-07 추가(TraitEffectKind.SlowOnHit 배선, PM 승인) — moveSpeedShredPercent를
    // 곱해서 뺀 실효값이다. 워크3 엔진은 유닛 이동속도를 220~522로 하드 클램프한다(맵
    // 설정으로 못 바꾸는 엔진 상수 — DefenseArmor처럼 맵 파일 확인이 필요한 값이 아니다,
    // 블리자드 공식 문서·수십 년째 불변으로 알려진 값). 원작 `AOae`류(자석자석·모래모래·
    // 뭉게뭉게, `Oae1=-0.07`) 3개가 전부 이 최소치 밑으로 못 내려가는 게 전제라 우리도
    // 같은 하한을 그대로 가져왔다 — MoveSpeedFloor 참고.
    public float MoveSpeed => data != null
        ? Mathf.Max(MoveSpeedFloor, data.moveSpeed * (1f - moveSpeedShredPercent))
        : 0f;

    // 워크3 엔진 하드 클램프(맵 설정 불가, 전 버전 공통) — 유닛 이동속도는 220 밑으로도
    // 522 위로도 못 간다. 여기선 하한만 쓴다(감속만 다루는 축이라 상한은 대상이 아니다).
    public const float MoveSpeedFloor = 220f;

    // TraitEffectKind.SlowOnHit(이감부여) 누적치 — ArmorShred(armorShred)와 같은 패턴:
    // 영구 누적, 위 MoveSpeedFloor에서 잘리므로 무한히 걸려도 효과는 유계다. 0(기본)이면
    // MoveSpeed가 원본 그대로라 회귀 없음.
    float moveSpeedShredPercent;

    /// <summary>이동속도 감소(%, 0.07=7%)를 건다. 음수를 넣으면 되돌린다(지속시간 있는
    /// 감속이 생기면 그렇게 쓴다) — AddArmorShred와 같은 관례.</summary>
    public void AddMoveSpeedShred(float amount) => moveSpeedShredPercent += amount;
    public float HpRatio => MaxHp > 0f ? Mathf.Clamp01(hp / MaxHp) : 0f;

    // 라운드 보스 여부(OnBossKilled와 같은 판단 기준). 도움소 흡수(즉사기)가 보스를 못 잡게
    // 거르는 데도 쓴다 — 원작 "보스, 스토리 적용X".
    public bool IsBoss => data != null && data.isBoss;

    // 어느 레인에 스폰됐는지. 팀 현황판이 플레이어별 적 수를 세는 데 쓴다.
    // -1은 레인에 속하지 않는 적(물범·스토리 건물 등) — 도움소 흡수가 "스토리 적용X"를
    // 넓게 해석해 이 값도 같이 거른다.
    public int LaneIndex { get; private set; } = -1;

    // 스토리 건물은 변신 전까지 죽지 않는다. 피해는 그대로 쌓이고, 변신할 때 남은 체력이 보스 체력이 된다.
    bool invulnerable;

    // ⚠️ 2026-09-06 추가(신세계 사이드보스, ORIGINAL_BOSS_COMBAT_SPEC.md §⑤) — 위
    // invulnerable("1 밑으로 안 내려간다", 피해는 그대로 hp에서 깎인다)과는 **완전히 다른
    // 뜻**이다. 원작 무적(Avul)은 "피해가 아예 안 들어간다" — 체력바가 100%에서 꿈쩍도
    // 안 한다. 기존 invulnerable을 이 용도로 재사용하면 사이드보스가 시전 중에도 계속
    // 깎이는 조용한 버그가 된다(리서치담당·PM 확인) — 그래서 별도 필드로 뗀다. 기존
    // invulnerable·StoryManager 쪽은 이 필드와 무관하게 그대로 돈다.
    bool trueInvulnerable;

    /// <summary>완전 무적을 켜고 끈다 — 켜져 있으면 TakeDamage가 피해 계산 자체를 안 한다
    /// (hp가 조금도 안 움직인다). 기존 invulnerable/SetInvulnerable과는 별개 축이다.</summary>
    public void SetTrueInvulnerable(bool value) => trueInvulnerable = value;
    public bool IsTrueInvulnerable => trueInvulnerable;

    // 스턴·구속을 거는 쪽이 각자 "원래 켜져 있었나"를 기억했다가 되돌리면, 효과가 겹쳤을 때
    // 나중에 끝나는 쪽이 "꺼져 있었다"를 복원해 적이 영영 멈춘다. 겹침 수만 세고,
    // 0이 될 때만 다시 움직이게 한다.
    int freezeCount;
    WaypointMover mover;

    // ⚠️ 2026-09-06 추가(신세계 사이드보스 §⑥) — 스턴게이지를 미는 데 "지금 스턴이 걸려
    // 있는가"를 밖에서 물어야 한다. 원작은 특정 버프(B07H) 하나를 보지만, 우리는 스턴
    // 소스가 여럿이라도(AddFreeze/RemoveFreeze가 겹침 횟수로 이미 안전하게 합쳐준다)
    // freezeCount>0이면 뜻이 같다 — "지금 이 유닛은 스턴 상태다."
    public bool IsStunned => freezeCount > 0;

    public void AddFreeze()
    {
        freezeCount++;
        ApplyFreeze();
    }

    public void RemoveFreeze()
    {
        freezeCount = Mathf.Max(0, freezeCount - 1);
        ApplyFreeze();
    }

    void ApplyFreeze()
    {
        if (mover != null) mover.enabled = freezeCount == 0;
    }

    // 이감(이속 감소) 배수. AddFreeze처럼 "겹친 것"을 기억하는 구조지만, 감속은 걸린 값마다
    // 세기가 달라 개수만으론 부족해서 실제 배수를 목록으로 들고 있는다.
    readonly List<float> slowMultipliers = new List<float>();

    // 하한(0.01, 99% 감속 — 원작 최대 -0.99 Oae1과 일치)은 WaypointMover.MinSlowMultiplier에
    // 정의돼 있다 — 정의는 한 곳만 두고 여기서는 그 값을 그대로 참조한다.
    /// <summary>
    /// 이감을 건다. 배수는 <b>1보다 작은 값</b>(예: 0.5 = 이속 50%)이다.
    /// 여러 개가 겹치면 <b>가장 강한 것 하나만</b> 적용한다(최솟값) — 곱으로 쌓으면 약한 감속
    /// 여럿이 사실상 정지(AddFreeze의 영역)를 침범하게 되고, 원작이 쓰는 워크3 오라도
    /// 같은 종류(Endurance Aura)는 겹치지 않고 가장 강한 것만 반영하는 규칙을 따른다.
    /// </summary>
    public void AddSlow(float multiplier)
    {
        slowMultipliers.Add(Mathf.Clamp(multiplier, WaypointMover.MinSlowMultiplier, 1f));
        ApplySlow();
    }

    /// <summary>이감을 되돌린다. AddSlow에 넣은 것과 같은 값을 넣어야 그 인스턴스가 빠진다.</summary>
    public void RemoveSlow(float multiplier)
    {
        slowMultipliers.Remove(Mathf.Clamp(multiplier, WaypointMover.MinSlowMultiplier, 1f));
        ApplySlow();
    }

    void ApplySlow()
    {
        float effective = 1f;
        foreach (float m in slowMultipliers)
            effective = Mathf.Min(effective, m);
        if (mover != null) mover.SetSlowMultiplier(effective);
    }

    public void SetInvulnerable(bool value)
    {
        invulnerable = value;
    }

    public void SetLane(int laneIndex)
    {
        LaneIndex = laneIndex;
    }

    public static int CountInLane(int laneIndex)
    {
        int count = 0;
        foreach (EnemyDummy enemy in Active)
            if (enemy.LaneIndex == laneIndex)
                count++;
        return count;
    }

    /// <summary>
    /// caster와 "같은 편"인 적들 — 우리는 적끼리 진영을 안 가르니 caster를 뺀 Active 전체다.
    /// 원작 오라의 atar가 "friend"(캐스터 편)를 뜻하지 플레이어 편을 뜻하지 않는다는 점이
    /// 중요하다 — 보스가 캐스터인 오라는 이걸로, 플레이어 유닛이 캐스터면
    /// <see cref="UnitIdentity.AlliesOf"/>(소유자 기준)로 모아야 한다. notself를 반영해
    /// caster 자신은 뺀다. range&lt;=0이면 거리 제한 없이 전체를 반환한다.
    /// </summary>
    public static List<EnemyDummy> AlliesOf(EnemyDummy caster, float range)
    {
        List<EnemyDummy> result = new List<EnemyDummy>();
        if (caster == null) return result;

        float sqrRange = range * range;
        foreach (EnemyDummy enemy in Active)
        {
            if (enemy == null || enemy == caster) continue;
            if (range > 0f && (enemy.transform.position - caster.transform.position).sqrMagnitude > sqrRange) continue;
            result.Add(enemy);
        }
        return result;
    }

    public void Initialize(float maxHp)
    {
        hp = maxHp;
        MaxHp = maxHp;
    }

    // WaveSpawner가 EnemyData 전체를 넘겨줄 수 있게 되면 이 오버로드로 전환 — 보상 지급에 필요한 데이터를 함께 보관한다.
    //
    // ⚠️ 2026-09-06 추가(신세계 사이드보스 §⑧ 정산) — startHpMultiplier 기본값 1f는 기존
    // 호출부 전부와 동작이 완전히 같다(회귀 0). R65/70/75 라운드 보스만 SideBossManager가
    // 저장해둔 사이드보스전 결과를 이 값으로 넘긴다(WaveSpawner.BossStartHpMultiplierProvider
    // 참고).
    public void Initialize(EnemyData enemyData, float startHpMultiplier = 1f)
    {
        data = enemyData;
        if (enemyData != null)
        {
            hp = enemyData.hp * startHpMultiplier;
            MaxHp = enemyData.hp * startHpMultiplier;

            // 콜라이더가 걸린 루트가 아니라 시각 부위에만 곱한다(위 visualRoot 주석 참고).
            if (visualRoot != null)
            {
                visualRoot.localScale = baseVisualScale * Mathf.Max(0.01f, enemyData.visualScale);
            }
        }

        // Instantiate는 동기 호출이라(Awake가 그 안에서 바로 돈다) Update가 끼어들 틈이 없다 —
        // 지금 CurrentRound가 곧 이 적을 내보낸 웨이브의 라운드다. WaveSpawner를 거치지 않고
        // 이렇게 읽어서, 라운드 번호를 실어 나르려고 그 파일을 고칠 필요가 없다.
        SpawnRound = RoundManagerRef != null ? RoundManagerRef.CurrentRound : 0;

        // 보스 오라(04번) — 모든 적이 MobPrefab 하나를 공유해서 프리팹에 미리 못 붙여두므로
        // 여기서 데이터를 보고 동적으로 붙인다. 대부분의 적은 auraSkills가 비어 있어 아무
        // 일도 안 한다.
        if (enemyData.auraSkills != null)
        {
            foreach (SkillData auraSkill in enemyData.auraSkills)
            {
                if (auraSkill == null) continue;
                gameObject.AddComponent<EnemyAuraCaster>().Initialize(auraSkill);
            }
        }
    }

    void Awake()
    {
        mover = GetComponent<WaypointMover>();

        // Initialize()를 거치지 않고 인스펙터 기본값(hp)만으로 씬에 배치된 경우를 위한 폴백 —
        // 이게 없으면 MaxHp가 0으로 남아 체력바가 항상 빈 채로 표시된다.
        if (MaxHp <= 0f)
        {
            MaxHp = hp;
        }

        // Initialize보다 먼저 돈다(Instantiate가 동기 호출이라) — visualScale을 곱하기 전
        // 프리팹 원본 비율을 여기서 찍어둔다. 그대로 곱하지 않고 원본에 곱하는 이유:
        // Initialize가 두 번 불릴 일은 없지만(스폰마다 새 인스턴스), 있다 해도 누적 배율이
        // 안 생기게 하려면 항상 "원본 × 이번 배율"이어야 한다.
        if (visualRoot != null)
        {
            baseVisualScale = visualRoot.localScale;
        }
    }

    // 오라 등이 거는 추가 회복량(초당). data.hpRegenPerSecond(에셋 고정값)와 별개로 더해진다 —
    // armorShred와 같은 누적 방식(더했다가 나중에 그대로 빼서 되돌린다).
    float regenBonus;

    /// <summary>초당 회복 보너스를 건다(원작 A11T 오라 Uau2=350000.0류). 음수를 넣으면 되돌린다.</summary>
    public void AddRegenBonus(float amount) => regenBonus += amount;

    /// <summary>AddRegenBonus로 건 것을 되돌린다 — 같은 값을 넣어야 정확히 상쇄된다.</summary>
    public void RemoveRegenBonus(float amount) => regenBonus -= amount;

    // 자연회복(EnemyData.hpRegenPerSecond + regenBonus). 기본 0이라 대부분의 적은 아무 일도
    // 안 한다. isDead를 먼저 거른다 — TakeDamage의 사망 확정과 같은 프레임에 순서가 겹치면
    // "죽었는데 되살아나는" 꼴이 나기 때문이다(사망 프레임엔 이미 Destroy가 걸려 있어
    // 다음 Update가 안 도는 게 보통이지만, 그 보장에 기대지 않고 명시적으로 막는다).
    // invulnerable(스토리 건물)도 막는다 — 그쪽은 변신 전까지 hp를 최소 1로만 눌러두고
    // 누적 피해를 그대로 보존해야 하는데, 회복이 끼면 그 누적이 깎여나간다.
    void Update()
    {
        if (isDead || invulnerable) return;

        float regen = (data != null ? data.hpRegenPerSecond : 0f) + regenBonus;
        if (regen <= 0f) return;

        hp = Mathf.Min(MaxHp, hp + regen * Time.deltaTime);
    }

    // ---- 유닛 능력(스킬)의 아군 오라 효과를 받는 자리(04번, 원작 A153/A11T) — 아직 아무도
    // 안 부른다. 실제 시전 루프(언제 걸고 언제 떼는지, 범위 밖으로 나가면 어떻게 하는지)는
    // 보스 쪽 캐스터가 04번에서 만든다. 여기는 add/remove 한 쌍만 제공한다 — Damage 말고
    // 아직 값 의미가 없는 kind(Stun 등)는 조용히 무시한다. ----

    /// <summary>오라 효과 하나를 건다. 캐스터가 매 틱 새로 걸 때는 같은 값으로 반드시
    /// RemoveAllyAuraEffect도 불러야 한다 — 안 그러면 armorShred/regenBonus가 무한히 쌓인다.</summary>
    public void ApplyAllyAuraEffect(SkillEffect effect)
    {
        if (effect == null) return;
        switch (effect.kind)
        {
            case SkillEffectKind.ArmorBonus:
                AddArmorShred(-effect.multiplier);
                break;
            case SkillEffectKind.HealOverTime:
                AddRegenBonus(effect.multiplier);
                break;
        }
    }

    /// <summary>ApplyAllyAuraEffect로 건 것을 정확히 상쇄한다 — 같은 effect(같은 multiplier)를 넣을 것.</summary>
    public void RemoveAllyAuraEffect(SkillEffect effect)
    {
        if (effect == null) return;
        switch (effect.kind)
        {
            case SkillEffectKind.ArmorBonus:
                AddArmorShred(effect.multiplier);
                break;
            case SkillEffectKind.HealOverTime:
                RemoveRegenBonus(effect.multiplier);
                break;
        }
    }

    // ---- 버프 레지스트리(2026-09-06, PM 지시) — UnitAttacker.activeBuffs와 같은 모양이다
    // (원작 디버프, 예: 핸콕 석화가 대상 Aegr를 올리는 것 같은 게 이 자리를 쓸 것이다).
    // ⚠️ 지금은 "걸 수 있는 그릇"까지다 — 이 값을 읽어서 실제로 뭔가를 바꾸는 코드(예:
    // MitigatedDamage가 특정 디버프를 보고 배율을 바꾸는 것)는 아직 없다. UnitAttacker
    // 쪽과 클래스를 공유하지 않은 이유: 두 타입이 상속 관계가 아니라 각자 독립된
    // MonoBehaviour라 공통 베이스를 새로 만드는 것보다 지금은 이 정도 중복이 싸다.
    class ActiveBuff
    {
        public string id;
        public float expiresAt; // Time.time 기준. <=0이면 영구(RemoveBuff로만 없어진다).
    }

    readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    public void AddBuff(string id, float duration)
    {
        if (string.IsNullOrEmpty(id)) return;
        activeBuffs.Add(new ActiveBuff { id = id, expiresAt = duration > 0f ? Time.time + duration : -1f });
    }

    public void RemoveBuff(string id)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].id == id) { activeBuffs.RemoveAt(i); return; }
        }
    }

    public bool HasBuff(string id) => !string.IsNullOrEmpty(id) && ActiveBuffCount(id) > 0;
    public bool LacksBuff(string id) => !HasBuff(id);

    int ActiveBuffCount(string id)
    {
        PruneExpiredBuffs();
        int count = 0;
        foreach (ActiveBuff b in activeBuffs)
            if (b.id == id) count++;
        return count;
    }

    void PruneExpiredBuffs()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            if (activeBuffs[i].expiresAt > 0f && Time.time >= activeBuffs[i].expiresAt)
                activeBuffs.RemoveAt(i);
        }
    }

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    [SerializeField] DamageTable damageTable;

    // 방깎이 방어력을 여기까지만 밀 수 있다. 하한이 없으면 방깎 유닛을 쌓을수록 무한히
    // 세져서 밸런싱이 무너진다.
    //
    // **근거가 한 번 바뀌었다. 값은 그대로다.**
    //
    // 옛 근거(틀림): "원작 서술 — 방어력 −20이면 71% 추가 피해, 그 이상은 불필요".
    //   71%는 `2 − 0.94^20 = 1.71`로 **감폭 상수가 0.06일 때만 나오는 수**인데 이 맵은 0.02다.
    //   맵 텍스트(wts·war3map.j·툴팁)를 전수로 뒤져도 그 문구가 **0건**이다 — 일반 워크3 상식이었다.
    //
    // 새 근거(맞음): **방깎 최대 70**(원작 `Iarp`)이 중반 라인몹 방어를 딱 이 근처로 민다.
    //   R51 방어 47 → −23 · **R54 방어 50 → −20**(정확히 일치) · R59 방어 54 → −16.
    //   ⚠️ 다만 **R64(방어 70) 이후엔 방깎을 다 걸어도 0 아래로 못 간다** — 후반엔 안 통한다.
    //
    // 참고: 이 맵의 상수(0.02)에서 −20의 배율은 1.71이 아니라 **1.33**이다.
    public const float ArmorFloor = -20f;

    // 방어력 1당 감폭량. **원작 `war3mapMisc.txt` 24행 `DefenseArmor=0.02`** 그대로다.
    // 워크3 기본값은 0.06인데 **원작 맵이 3분의 1로 낮춰놨다** — 그만큼 방어력이 덜 아프다.
    // (2026-09-04 원본 확인. 그전까지 0.06을 써서 후반 피해가 2.2~2.8배 낮게 나왔다.)
    public const float DefenseArmor = 0.02f;

    // 도움소·보스 오라 등 "임의의 값을 걸었다 되돌리는" 방깎(원작 `Iarp`류, 일반 플레이어
    // 유닛 트레잇) — ApplyAllyAuraEffect/RemoveAllyAuraEffect(위)와 UnitAttacker의 일반
    // ArmorShred 트레잇이 이걸 쓴다. 아래 A0TK/A0VI/A0VJ와는 완전히 다른 축이다 — 이건
    // "누구나 걸 수 있는 값"이고, 아래는 "카이도·핸콕 전용 스킬이 올리는 특정 능력의
    // 레벨"이다. EffectiveArmor에서 둘 다 뺀다.
    float armorShred;

    // ⚠️ 2026-09-05 정정(2차): 처음엔 이 셋을 "우리 유닛의 방깎 트레잇이 범용으로 올리는
    // 표"로 오해했다(1차 정정, 04③ 최초 커밋). PM이 트리거를 다시 뒤져 **레벨을 올리는
    // 곳이 캐릭터 딱 둘뿐**이라는 걸 확인했다 — 능력 이름 자체가 그렇게 말하고 있었다
    // ("방어력감소-카이도", "방어력감소-핸콕"):
    //   A0TK ← Trig_Kaido_Attack                              (카이도 평타)
    //   A0VI ← Trig_Kaido_Attack, Trig_Kaido_Skill_1_8         (카이도 평타 + 전용 스킬)
    //   A0VJ ← Trig_Legend14han_petrification1/2/3             (핸콕 석화)
    // 그래서 **세 표를 하나의 스택으로 합쳐 읽으면 안 된다** — 카이도 평타 한 번에
    // -70(A0TK)+-3(A0VI)+-5(A0VJ)=-78이 한꺼번에 걸리는 건 원작에 없는 일이다. 실제로는
    // 트리거마다 자기 표(들)만 올라간다. 그래서 스택을 표별로 분리한다.
    // 117기·98기·99기가 이 능력을 미리 갖고 있던 이유도 이걸로 설명된다 — 워크3는 대상에
    // 능력이 이미 붙어 있어야 레벨을 세팅할 수 있어서 수신기로 미리 심어둔 것이다("범용
    // 시스템"이 아니라 "수신기").
    // 세 표 다 레벨1(스택0)은 0(효과없음)에서 시작한다 — A0TK만 첫 스택이 유난히 크다
    // (-70, 그 뒤로는 -5씩). 등간격으로 보간하면 안 되는 이유가 그거다.
    static readonly float[] ArmorShredLevelsA0TK = { 0f, -70f, -75f, -80f, -85f, -90f, -95f, -100f, -105f, -110f, -115f };
    static readonly float[] ArmorShredLevelsA0VI = { 0f, -3f, -6f, -9f, -12f, -15f, -18f, -21f, -24f, -27f, -30f };
    static readonly float[] ArmorShredLevelsA0VJ = { 0f, -5f, -10f, -15f, -20f, -25f, -30f, -35f, -40f };

    // 표별 독립 스택. 카이도 평타는 tk/vi를 같이 올리고(같은 트리거), 카이도의 전용 스킬은
    // vi만, 핸콕의 석화는 vj만 올린다 — 그래서 세 카운터가 서로 따로 논다.
    int kaidoAttackTkStacks;
    int kaidoViStacks;
    int hancockPetrificationVjStacks;

    /// <summary>카이도 평타(Trig_Kaido_Attack)가 적중할 때 부른다 — A0TK와 A0VI를 함께
    /// 올린다. 우리 로스터에 카이도에 해당하는 유닛이 아직 없어 지금은 호출부가 없다
    /// (06번 특성 배선 이후, 사장님이 배정하면 연결). 영구 누적 — 표 길이에서 자동으로
    /// 멈추므로 상한을 넘겨도 안전하다.</summary>
    public void AddKaidoAttackStack()
    {
        kaidoAttackTkStacks++;
        kaidoViStacks++;
    }

    /// <summary>카이도의 전용 스킬(Trig_Kaido_Skill_1_8)이 적중할 때 부른다 — A0VI만 올린다.</summary>
    public void AddKaidoSkillViStack() => kaidoViStacks++;

    /// <summary>핸콕의 석화(Trig_Legend14han_petrification)가 적중할 때 부른다 — A0VJ만 올린다.</summary>
    public void AddHancockPetrificationStack() => hancockPetrificationVjStacks++;

    /// <summary>표별 스택을 각자의 표 인덱스로 읽어 이 개체에 적용되는 방어력감소 합을 낸다
    /// (전부 0 이하 — 그대로 armor에 더하면 깎인다).
    /// data.armorShredBuildingOnly(건물)면 카이도의 A0VI와 핸콕의 A0VJ는 안 통하고
    /// A0TK(카이도 평타의 "큰 쪽")만 통한다 — 원작 실측(PM): "핸콕의 석화는 건물에 안
    /// 통하고, 카이도도 건물엔 큰 쪽만 통한다".</summary>
    float TableStackedArmorShred()
    {
        float total = ArmorShredLevelsA0TK[Mathf.Clamp(kaidoAttackTkStacks, 0, ArmorShredLevelsA0TK.Length - 1)];

        if (data == null || !data.armorShredBuildingOnly)
        {
            total += ArmorShredLevelsA0VI[Mathf.Clamp(kaidoViStacks, 0, ArmorShredLevelsA0VI.Length - 1)];
            total += ArmorShredLevelsA0VJ[Mathf.Clamp(hancockPetrificationVjStacks, 0, ArmorShredLevelsA0VJ.Length - 1)];
        }

        return total;
    }

    /// <summary>방깎을 적용한 실효 방어력. 하한 -20.</summary>
    public float EffectiveArmor =>
        Mathf.Max(ArmorFloor, (data != null ? data.armor : 0f) - armorShred + TableStackedArmorShred());

    public ArmorType ArmorType => data != null ? data.armorType : ArmorType.Normal;

    // ⚠️ 2026-09-06 신설(TARGET_SIDE_AXES.md·STACK_AXES_INCREMENT_LIST.md, 리서치담당) —
    // 원작 대상측 세 축(Aegr·AIsr·A11S)은 전부 "레벨 카운터 + 스킬이 +N씩 올림 + 상한"
    // 구조이고 셋 다 곱이다. Aegr·A11S는 이미 아래(위)에 필드가 있었고 값도 맞았지만
    // "스킬이 레벨을 올리는" 스택 부분이 없었다 — 여기서 그 스택만 추가한다. AIsr은
    // 필드 자체가 없어서 새로 만든다(원작 스폰 시 세팅이 없어 모든 적이 레벨1=증폭0%에서
    // 시작한다).
    //
    // ⚠️ 뿌리 ㊲ 정정(2026-09-06, a6dd7da): 처음엔 "레벨당 증분"과 "구간 끝값"이 안 맞아서
    // (실측 3점과 정확히 맞는) 레벨당 증분 쪽을 신뢰하고 그 기울기를 최대레벨까지 외삽했다.
    // 틀렸다 — 리서치담당이 war3map.w3a를 직접 디코드해서 확인한 결과, 증분이 최대레벨까지
    // 안 간다. 도중에 **꺾여서 평평해진다(플래토)**:
    //   Aegr(Def5): 레벨1=0.85부터 레벨당 +0.01(등차) → 레벨31=1.15에서 멈춤 → 레벨32~45는
    //               전부 1.15로 평평. 실측 3점(L6=0.90·L11=0.95·L16=1.00)은 전부 이 등차구간
    //               "안"이었다 — 기울기 자체는 맞았지만 꺾이는 지점(31)이 관측 범위 밖에
    //               있어서 못 봤다. 상수로 박을 건 최대레벨(45)이 아니라 꺾임레벨(31)이다.
    //   AIsr(isr2): 레벨1=0.00부터 레벨당 -0.01(등차, 원문에 음수로 저장) → 레벨26=-0.25에서
    //               멈춤 → 26~36(11레벨) 전부 -0.25 고정. 2026-09-06 재조회(d2f198b)로 확정:
    //               처음엔 "레벨29~36에 개별 엔트리가 없다"고 봤는데, 검색이 objectId
    //               접두사 붙은 키("AIsr"+"isr2")만 찾아서 레벨29부터 접두사 없이 bare로
    //               저장된 엔트리를 놓친 것이었다(뿌리 ㉓ 네 번째, c101c9a — "데이터가
    //               없다"고 결론 내리기 전에 접두사 있는/없는 두 형태로 다시 찾아볼 것).
    //               레벨1~36 전부 원문에 있고, 26~36 고정이 확인됐다 — 더 이상 추정이
    //               아니다. Aegr도 같은 방식으로 재확인: 레벨1~45 전부 존재, 31~45(15레벨)
    //               전부 1.15 고정. 전체 원문은 `Docs/reference/TARGET_SIDE_AXES_RAW_DUMP.md`
    //               참고 — 다시 재지 말고 그 표를 인용할 것.
    // AIsr 부호 확정(2026-09-06, 리서치담당 재조회): "증폭" 방향이 맞다 — 이 코드가 짠 그대로다.
    // 근거 셋: ① `AIsr` 리터럴 23곳 전부 `SetUnitAbilityLevelSwapped` 레벨증가 호출뿐이고
    // 이 값을 읽어 부호를 뒤집거나 데미지에 곱하는 JASS가 원천적으로 없다(엔진 네이티브가
    // 처리). ② `anam`(툴팁명)이 "마법 데미지 증폭". ③ 실측 교차검증 — 도움소 대지진
    // (`AOeq`) +5가 툴팁 "마법데미지 5% 증폭"과 정확히 일치. ⚠️ 다만 레벨36을 실제로 줘서
    // 피해를 재는 100% 엔진 실측까지는 아니다 — 근거 셋이 겹친 판정이라는 점은 남겨둔다.

    const float AegrLevelStep = 0.01f;   // Def5, 등차구간 레벨당(실측 3점과 일치)
    const int AegrKinkLevel = 31;        // 여기서 꺾여 평평해진다(원문 디코드 확인) — 값 계산에 씀
    const int AegrCapLevel = 45;         // alev 최대 레벨(스택 누적 자체의 상한, 꺾임 이후는 값이 안 바뀜)
    int aegrStackLevels;   // 난이도 기본 레벨 위에 스킬이 추가로 올린 레벨(스택)만 센다

    // data.magicArmorMultiplier(Def5)에서 난이도 기본 레벨을 역산한다 — 0.85+(L-1)*0.01의 역함수.
    // 난이도 기본 레벨(6/11/16)은 전부 꺾임(31) 안쪽이라 이 역산은 그대로 유효하다.
    int AegrBaseLevel => Mathf.RoundToInt(1f + ((data != null ? data.magicArmorMultiplier : 1f) - 0.85f) / AegrLevelStep);

    /// <summary>원작 Aegr 스택 — 스킬이 이 적의 Aegr 레벨을 N만큼 올릴 때 부른다. 스택 누적
    /// 자체의 상한(45)은 "난이도 기본 레벨 + 스택" 총합 기준이라, 기본 레벨을 역산해 정확히
    /// 자른다 — 실제 배율 계산은 꺾임레벨(31)에서 따로 한 번 더 잘린다(아래 참고).</summary>
    public void AddAegrStack(int amount)
    {
        int cap = Mathf.Max(0, AegrCapLevel - AegrBaseLevel);
        aegrStackLevels = Mathf.Clamp(aegrStackLevels + amount, 0, cap);
    }

    const float AisrLevelStep = 0.01f;  // isr2 등차구간 레벨당(원문 절대값 기준)
    const int AisrKinkLevel = 26;       // 여기서 꺾여 평평해진다(26~28 세 레벨 확인, 29~36은 추정 [미확인])
    const int AisrCapLevel = 36;        // alev 최대 레벨(스택 누적 자체의 상한)
    int aisrStackLevels;   // 레벨1(증폭 0%) 기준 추가 레벨. 스폰 시 세팅이 없어 항상 0에서 시작.

    /// <summary>원작 AIsr(마법 데미지 증폭) 스택 — 스킬이 이 적의 AIsr 레벨을 N만큼 올릴 때
    /// 부른다. 스택 누적 자체의 상한(36)은 레벨1 시작 기준이라 스택을 0~35로 자르면 된다 —
    /// 실제 배율 계산은 꺾임레벨(26)에서 따로 한 번 더 잘린다(아래 참고).</summary>
    public void AddAisrStack(int amount)
    {
        aisrStackLevels = Mathf.Clamp(aisrStackLevels + amount, 0, AisrCapLevel - 1);
    }

    /// <summary>원작 AIsr(마법 데미지 증폭). 레벨1(스택0)이면 배율 1.0(증폭 없음) — 원작에
    /// 스폰 시 세팅이 없어 모든 적이 여기서 시작한다. 스택마다 isr2가 절대값 0.01씩 커지는데
    /// 레벨26(스택25)에서 꺾여 0.25로 평평해진다(위 뿌리 ㊲ 참고) — 그 이상 스택은 값에
    /// 영향이 없다. 우리는 반대 부호(피해를 더 받는 증폭)로 표현한다: 배율 = 1 + min(스택,
    /// 꺾임스택)×step.</summary>
    public float EffectiveMagicDamageAmplifier =>
        1f + Mathf.Min(aisrStackLevels, AisrKinkLevel - 1) * AisrLevelStep;

    // A11S: 원작 공식 "0.20 + 0.05×레벨" (EnemyData.percentDamageTaken 주석 참고) —
    // 레벨14→0.90(일반)·레벨16→1.00(보스)로 이미 실측 확인됨. 상한 23.
    const float A11SLevelConst = 0.20f;
    const float A11SLevelStep = 0.05f;
    const int A11SCapLevel = 23;
    int a11sStackLevels;

    // data.percentDamageTaken(=0.20+0.05×기본레벨)에서 기본 레벨을 역산한다.
    int A11SBaseLevel => Mathf.RoundToInt(((data != null ? data.percentDamageTaken : 1f) - A11SLevelConst) / A11SLevelStep);

    /// <summary>원작 A11S 스택 — 스킬이 이 적의 A11S 레벨을 N만큼 올릴 때 부른다. 상한(23)은
    /// "기본 레벨(14 또는 16) + 스택" 총합 기준이라 기본 레벨을 역산해 정확히 자른다.</summary>
    public void AddA11SStack(int amount)
    {
        int cap = Mathf.Max(0, A11SCapLevel - A11SBaseLevel);
        a11sStackLevels = Mathf.Clamp(a11sStackLevels + amount, 0, cap);
    }

    /// <summary>스킬 피해 전반(%체력·고정값 basis 가리지 않음)에 곱하는 대상별 감수성 계수
    /// (원작 A11S) — 평타(일반 피해)엔 안 쓴다. ⚠️ 2026-09-05 정정: 처음엔 "%비례 피해
    /// 전용"으로 알았는데, A11S 사용처 43곳 중 게이트 없는 7곳이 고정 피해에도 같은 계수를
    /// 곱혀서(리서치담당 재조사) 스킬 피해 전반으로 넓혔다 — UnitAttacker.DealSkillDamage가
    /// basis를 안 가리고 곱한다. EnemyData.percentDamageTaken 참고.
    ///
    /// 2026-09-06: 원작 취약도 스택(맞을수록 계수가 오르는 디버프, 예: Trig_Hidden9 +2/
    /// carrot_skill_2 +1/Uta_skill_3_mana +5)을 여기 붙였다 — AddA11SStack이 올린 레벨을
    /// 공식에 다시 넣어 재계산한다.
    /// ⚠️ 스택 0일 때 data.percentDamageTaken을 그대로 돌려준다(재계산 안 함) — 정수 레벨로
    /// 역산했다 되돌리면 float 반올림 오차가 생긴다(실측: 0.9f가 0.9000000357627869f로
    /// 어긋남, 2026-09-06 런타임 검증). 스택 0에서 원래 값과 비트까지 같아야 한다는
    /// 요구(PM)를 만족시키려면 이 지름길이 필요하다.</summary>
    public float PercentDamageTakenMultiplier => a11sStackLevels == 0
        ? (data != null ? data.percentDamageTaken : 1f)
        : A11SLevelConst + A11SLevelStep * (A11SBaseLevel + a11sStackLevels);

    /// <summary>이 적이 %체력 비례 스킬 피해(TargetMaxHpPercent/TargetCurrentHpPercent)를
    /// 받는가 — 원작 GetUnitPointValue(대상)&lt;200 게이트(리서치담당 재조사, 2026-09-05).
    /// 보스(라운드보스·신세계사이드보스·거대해왕류, 원작 포인트값 200 이상)는 이 분기를
    /// 아예 안 탄다. EnemyData.takesPercentDamage 참고.</summary>
    public bool TakesPercentDamage => data == null || data.takesPercentDamage;

    /// <summary>원작 GetUnitPointValue(이 적). 2026-09-06 신설(PM 지시, "대상 조건 게이트") —
    /// UnitAttacker.ApplyToEnemy가 SkillEffect.targetCondition 3단 분기(<·==·>=)를 여기 값과
    /// 비교한다. EnemyData.pointValue가 0f(미조사 센티널)면 그대로 0을 돌려준다 — 조건이
    /// 걸린 효과가 아직 없어(62파일 배선 전) 지금은 이 값이 어디서도 안 읽힌다.</summary>
    public float PointValue => data != null ? data.pointValue : 0f;

    // 마방깍 누적. 마법 방어는 배율이라, 깎으면 배율이 **올라간다**(피해를 더 받는다).
    float magicArmorShred;

    /// <summary>마법(AP) 피해에 곱할 배율. 1.0이 감소 없음, 1.0 초과면 더 받는다.
    /// 2026-09-06: 원작 Aegr 스택(aegrStackLevels)을 Def5에 가산하고, AIsr(원작에서 Aegr와
    /// 곱인 별개 축)을 곱했다 — 둘 다 스택 0이면 각각 무변화·배율 1.0이라 기존 값과
    /// 정확히 같다(아래 EffectiveMagicDamageAmplifier 참고). Aegr 스택은 꺾임레벨(31)에서
    /// 멈춘다 — AegrBaseLevel부터 31까지 남은 만큼만 반영하고, 그 이상 쌓인 스택은 값에
    /// 영향이 없다(뿌리 ㊲).</summary>
    public float EffectiveMagicMultiplier =>
        Mathf.Max(0f, (data != null ? data.magicArmorMultiplier : 1f) + magicArmorShred
            + Mathf.Min(aegrStackLevels, Mathf.Max(0, AegrKinkLevel - AegrBaseLevel)) * AegrLevelStep)
        * EffectiveMagicDamageAmplifier;

    /// <summary>마방깍을 건다. 조합표의 `마방깍오라(9%)`가 0.09로 들어온다.</summary>
    public void AddMagicArmorShred(float amount) => magicArmorShred += amount;

    /// <summary>방깎을 건다. 음수를 넣으면 되돌린다(지속시간 있는 방깎이 생기면 그렇게 쓴다).</summary>
    public void AddArmorShred(float amount) => armorShred += amount;

    /// <summary>
    /// 워크래프트3 방어력 공식. 원작이 워크3 시스템을 쓰되 <b>상수만 바꿔놨다</b> —
    /// <see cref="DefenseArmor"/>가 0.06이 아니라 <b>0.02</b>다(`war3mapMisc.txt` 24행).
    ///
    /// ⚠️ 여기 원래 *"「방어력 -20이면 71% 추가 피해」가 2 − 0.94^20 = 1.7099와 정확히 맞는다"*고
    /// 적혀 있었는데, <b>그 검산이 이 맵을 확인해 준 게 아니었다.</b> 0.94는 0.06에서 나온 수라
    /// 공식이 0.06이라는 가정을 스스로 되풀이했을 뿐이다. 맵이 0.02라는 건 맵 파일이 말해준다.
    /// </summary>
    /// <summary>
    /// 최종 피해. 순서: 방깎 → (방어력+마법저항 감폭, 능력 피해의 AP면 둘 다 생략) → 배율표.
    ///
    /// ⚠️ 2026-09-05, 원작 확정(ORIGINAL_DAMAGE_TYPING.csv 715건 전수, PM):
    /// **공격타입(상성표 행)과 피해타입(방어 무시 여부)은 완전히 독립된 축이다.**
    /// `CHAOS+UNIVERSAL` 96건·`MAGIC+UNIVERSAL` 62건·`HERO+UNIVERSAL` 19건처럼
    /// "물리 상성 행을 타면서 방어는 무시"하는 조합이 실재한다 — 예전엔 "AP=마법=Spells행만
    /// 방어 무시"로 좁게 봤는데 그 전제가 틀렸다. **우리 축에서 `DamageType.AP`가 곧 원작
    /// `DAMAGE_TYPE_UNIVERSAL`이다** — 어느 attackType 행을 타든(물리·마법 상관없이)
    /// `AP`면 방어를 무시한다.
    ///
    /// ⚠️ 2026-09-05 정정(PM 지적): 이 무시는 <b>능력(스킬 효과·도움소·스토리 이벤트) 피해에만</b>
    /// 유효하다 — <paramref name="isAbilityDamage"/>가 그 구분이다. **원작 유닛 평타에는
    /// UNIVERSAL이 없다**(평타는 전부 자기 `ua1t` 공격타입 + 물리 피해). 우리 로스터
    /// `UnitData.damageType`은 "화력이 스킬에서 나온다"는 조합표 표시일 뿐 원작 UNIVERSAL
    /// 평타를 뜻하지 않는다 — `type`만 보고 방어를 무시하면, 훗날 조합표 반영 중 어느 유닛이
    /// AP로 바뀌는 순간 그 유닛 평타가 방어를 통째로 무시하게 된다(지금 로스터는 순수 AP 0종이라
    /// 우연히 안 터질 뿐, 데이터가 안전한 것이지 코드가 안전한 게 아니다). 그래서 평타 경로
    /// (<c>UnitAttacker</c>의 일반 공격·Bash 크리티컬)는 <paramref name="isAbilityDamage"/>를
    /// 명시적으로 <c>false</c>로 넘긴다.
    /// </summary>
    float MitigatedDamage(float amount, DamageType type, AttackType attackType, float armorIgnoreRatio,
                          bool isAbilityDamage)
    {
        // AP = 원작 UNIVERSAL — 물리 방어력과 마법저항을 둘 다 무시한다(엔진 규칙 확정,
        // PM 2026-09-05). attackType은 안 본다 — 어느 상성표 행이든(물리·마법 모두)
        // UNIVERSAL은 그 앞의 방어 축 자체를 건너뛴다. 상성표(아래)는 그래도 탄다 —
        // "방어 무시"와 "상성표"는 별개 축이다(원작 엔진 문서 확정).
        //
        // isAbilityDamage가 false(평타·Bash)면 type이 AP여도 무시하지 않는다 — 원작 평타엔
        // UNIVERSAL이 없어서다(위 요약 참고). AD/AP 혼합(우리 로스터 9종)은 지금도 기존과
        // 같이 이 조건이 거짓이라(type이 순수 AP가 아니므로) 안 샌다.
        bool bypassPhysicalArmor = isAbilityDamage && type == DamageType.AP;

        if (!bypassPhysicalArmor)
        {
            // ⚠️ 2026-09-05 리서치담당 확인(DAMAGE_TYPING_AND_MAGIC_AXIS.md): 원작에
            // DAMAGE_TYPE_MAGIC이 0건이다 — 적에게 붙는 Aegr는 "마법 저항"이 아니라
            // 공격타입별 감쇄(파생 A0P9의 Def1=0.85·Def5=0.90)이고, 난이도+스킬 스택으로
            // 레벨만 오르내리는 별개 축이다. **우리 EffectiveMagicMultiplier가 모델한
            // "마법 저항"은 원작 대응이 없다** — 필드·코드는 안 지운다(사장님 지시).
            // 되살릴 일이 있으면 이 자리가 아니라 Aegr 레벨(난이도+스킬 스택) 축으로 다시
            // 설계할 것. 지금은 AP(=UNIVERSAL)일 때 이 배율도 같이 건너뛴다 — 우리 평타는
            // 전부 AD라 결과적으로 이 배율이 아무 데도 안 걸리는데, 그게 원작과 일치하는
            // 상태다.
            amount *= EffectiveMagicMultiplier;

            // 방무뎀은 전부/전무가 아니라 비율이다. 피해를 둘로 갈라 한쪽만 감폭시킨다.
            float ignored = Mathf.Clamp01(armorIgnoreRatio);
            amount = amount * (1f - ignored) * ArmorMultiplier(EffectiveArmor)
                   + amount * ignored;
        }

        // 상성표는 **방어 무시 여부와 무관하게 항상 탄다** — 원작이 그렇다(공격타입과
        // 피해타입이 독립 축이라, UNIVERSAL이어도 물리/마법 상성 행은 그대로 적용된다).
        // ⚠️ 예전엔 RowMatches가 "AP엔 magic/spells 행만"으로 짝을 강제했는데, 그 전제
        // (AP=마법)가 위 주석대로 깨졌다 — 물리 행 + 방어 무시 조합이 원작에 실재해서
        // RowMatches는 이제 막지 않는다(DamageTable.cs 참고). Unassigned만 배율 1.0으로
        // 빠진다(Multiplier 자체가 그렇게 처리한다).
        if (damageTable != null && DamageTable.RowMatches(type, attackType))
        {
            amount *= damageTable.Multiplier(attackType, ArmorType);
        }
        else if (damageTable != null && !loggedRowMismatch)
        {
            // ⚠️ 확인: RowMatches가 지금 항상 true를 돌려주므로(DamageTable.cs) 이 분기는
            // 현재 코드에서 도달 불가능한 죽은 코드다. 지우지 않고 남겨두는 것뿐이다 —
            // "경고 배선을 유지했다"는 이 분기 자체가 지금 실행된다는 뜻이 아니라, RowMatches가
            // 나중에 다시 무언가를 막게 될 때 이 자리만 고치면 되게 남겨뒀다는 뜻이다(PM 지시).
            loggedRowMismatch = true;
            Debug.LogWarning($"{name}: {type} 피해에 {attackType} 행이 상성표 검사를 건너뛰었다.", this);
        }

        return amount;
    }

    // 짝이 안 맞는 조합을 처음 봤을 때만 경고한다(타격마다 찍으면 콘솔이 도배된다).
    static bool loggedRowMismatch;

    public static float ArmorMultiplier(float armor) =>
        armor >= 0f
            ? 1f - (DefenseArmor * armor) / (1f + DefenseArmor * armor)
            : 2f - Mathf.Pow(1f - DefenseArmor, -armor);

    /// <summary>
    /// 피해를 받는다. <b>감폭은 여기서 한다 — 때리는 쪽이 아니다.</b>
    /// 피해원이 여럿이라(평타·도움소 범위·지속딜) 공격자 쪽에 두면 새 피해원이 생길 때마다
    /// 다시 구현해야 하고, 언젠가 하나가 빠진다. 방어력은 적이 가진 것이니 적이 적용한다.
    ///
    /// <paramref name="type"/>에 기본값을 두지 않은 것도 같은 이유다 —
    /// 기본 AD로 두면 새 피해원이 조용히 물리로 들어가고 나중에 원인을 못 찾는다.
    /// </summary>
    /// <param name="armorIgnoreRatio">
    /// 방무뎀. <b>0~1 비율</b>이다 — 원작 용어집이 "평타가 방어를 무시하는 %"라고 정의한다
    /// (`UNIT_STATS_RESEARCH.md`). 즉 `방무뎀(30%)`은 "방어력을 30% 무시"가 아니라
    /// <b>피해의 30%가 감폭을 건너뛴다</b>는 뜻이다. 배율표는 건너뛴 몫에도 그대로 적용된다.
    /// 기본 0은 "방무뎀 없음"이라 안전하다 — <paramref name="type"/>과 달리 조용히 틀릴 여지가 없다.
    /// </param>
    /// <param name="attackType">
    /// 배율표에서 <b>어느 행을 탈지</b>. 물리면 normal/pierce/siege/hero/chaos,
    /// 마법이면 magic(평타가 마법)이나 spells(능력 피해)다.
    /// <paramref name="type"/>과 직교한다 — 저쪽은 "무엇으로 감폭하느냐", 이쪽은 "어느 행이냐"다.
    /// <b>둘의 짝이 안 맞으면 상성표를 건너뛰고 경고한다</b>(<c>DamageTable.RowMatches</c>).
    /// </param>
    /// <param name="isAbilityDamage">
    /// ⚠️ 2026-09-05 추가(PM 지적) — <c>type==DamageType.AP</c>의 방어·마법저항 무시가
    /// 유효한 피해원인가. 기본 <c>true</c> — 스킬 효과·도움소·스토리 이벤트 등 "능력" 경로가
    /// 대부분이라 그쪽을 안 건드리는 게 안전하다. <b>평타(및 Bash 크리티컬)만 명시적으로
    /// <c>false</c>를 넘긴다</b> — 원작 유닛 평타엔 UNIVERSAL이 없어서, 로스터 표시상 AP인
    /// 유닛이 평타로 방어를 통째로 무시하면 안 된다(위 <c>MitigatedDamage</c> 요약 참고).
    /// </param>
    public void TakeDamage(float amount, DamageType type, AttackType attackType,
                           int killerPlayerId, float armorIgnoreRatio = 0f, bool isAbilityDamage = true)
    {
        if (isDead) return;

        // §⑤ 완전 무적 — 피해 계산 자체를 안 한다(hp가 조금도 안 움직인다). 아래
        // invulnerable("1 밑으로 안 내려간다")과는 다른 축이라 여기서 먼저, 별도로 거른다.
        if (trueInvulnerable) return;

        hp -= MitigatedDamage(amount, type, attackType, armorIgnoreRatio, isAbilityDamage);

        if (invulnerable)
        {
            hp = Mathf.Max(1f, hp);   // 다 깎여도 남겨둔다 — 변신할 때 최소 1로 시작
            return;
        }

        if (hp <= 0f)
        {
            if (!GameAuthority.IsServer) return;

            // Destroy는 프레임 끝에야 실제로 처리되므로, 같은 프레임에 다른 유닛이 또 때려서
            // 보상이 중복 지급되지 않도록 죽음 확정 시점에 바로 플래그를 세우고 등록도 해제한다.
            isDead = true;
            Active.Remove(this);

            // 파괴보다 먼저 부른다 — Destroy가 걸린 뒤엔 재생될 틈이 없다.
            // (지금은 즉시 파괴라 사실상 안 보이지만, 사망 연출을 넣을 자리를 여기로 정해둔다.)
            GetComponent<CharacterAnimator>()?.PlayDeath();

            // 처치 골드는 보통 킬러가 아니라 이 적이 걷던 레인의 주인에게 간다(원작 그대로).
            // 예외는 레인에 안 속한 적(크립·퀘스트 미니보스)뿐이고, 그 판단은 분배기가 한다
            // (EnemyData.rewardsKillerOnly) — 그래서 killerPlayerId를 여기서 넘겨준다.
            if (data != null && RewardDistributor.Instance != null)
            {
                RewardDistributor.Instance.GrantKillReward(data, LaneIndex, SpawnRound, killerPlayerId);
            }

            // 01번 영웅 XP(사장님 결정, war3map.j:14734 ForGroup(udg_Exp_Hero_Group[라인
            // 주인], AddHeroXP(...,1))) — 골드와 같은 자리, 같은 변수(LaneIndex)를 쓴다.
            // 누가 죽였는지는 안 본다 — killerPlayerId가 아니라 LaneIndex다.
            UnitAttacker.GrantHeroKillExperienceToLane(LaneIndex);

            // 신호만 보낸다 — 실제 처리(도박소 해금 등)는 구독하는 쪽 몫이다.
            if (data != null && data.isBoss)
            {
                OnBossKilled?.Invoke(SpawnRound);
            }

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 도움소 흡수(즉사기) 전용 — 원작의 RemoveUnit과 같다. TakeDamage를 안 거쳐서
    /// 방어력·저항·무적 플래그와 무관하게 무조건 사라지고, 킬 보상(GrantKillReward)도
    /// 안 나간다·OnBossKilled도 안 뜬다(원작이 그렇다 — 애초에 이 메서드는 IsBoss==false인
    /// 대상에만 불려야 한다, 호출부가 걸러야 할 몫이다).
    /// </summary>
    public void RemoveInstantly()
    {
        if (isDead) return;

        isDead = true;
        Active.Remove(this);
        Destroy(gameObject);
    }
}
