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

    // 스턴·구속을 거는 쪽이 각자 "원래 켜져 있었나"를 기억했다가 되돌리면, 효과가 겹쳤을 때
    // 나중에 끝나는 쪽이 "꺼져 있었다"를 복원해 적이 영영 멈춘다. 겹침 수만 세고,
    // 0이 될 때만 다시 움직이게 한다.
    int freezeCount;
    WaypointMover mover;

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
    public void Initialize(EnemyData enemyData)
    {
        data = enemyData;
        if (enemyData != null)
        {
            hp = enemyData.hp;
            MaxHp = enemyData.hp;

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

    /// <summary>스킬 피해 전반(%체력·고정값 basis 가리지 않음)에 곱하는 대상별 감수성 계수
    /// (원작 A11S) — 평타(일반 피해)엔 안 쓴다. ⚠️ 2026-09-05 정정: 처음엔 "%비례 피해
    /// 전용"으로 알았는데, A11S 사용처 43곳 중 게이트 없는 7곳이 고정 피해에도 같은 계수를
    /// 곱혀서(리서치담당 재조사) 스킬 피해 전반으로 넓혔다 — UnitAttacker.DealSkillDamage가
    /// basis를 안 가리고 곱한다. EnemyData.percentDamageTaken 참고.
    ///
    /// ⚠️ 나중에 여기가 올라갈 자리 — 원작 취약도 스택(맞을수록 계수가 오르는 디버프, 예:
    /// Trig_Hidden9 +2/carrot_skill_2 +1/Uta_skill_3_mana +5). 지금은 안 만든다(PM 지시,
    /// 2026-09-05) — magicArmorShred와 같은 패턴(런타임 누적 필드 + Add 메서드)으로 나중에
    /// 이 프로퍼티에 더하면 된다.</summary>
    public float PercentDamageTakenMultiplier => data != null ? data.percentDamageTaken : 1f;

    /// <summary>이 적이 %체력 비례 스킬 피해(TargetMaxHpPercent/TargetCurrentHpPercent)를
    /// 받는가 — 원작 GetUnitPointValue(대상)&lt;200 게이트(리서치담당 재조사, 2026-09-05).
    /// 보스(라운드보스·신세계사이드보스·거대해왕류, 원작 포인트값 200 이상)는 이 분기를
    /// 아예 안 탄다. EnemyData.takesPercentDamage 참고.</summary>
    public bool TakesPercentDamage => data == null || data.takesPercentDamage;

    // 마방깍 누적. 마법 방어는 배율이라, 깎으면 배율이 **올라간다**(피해를 더 받는다).
    float magicArmorShred;

    /// <summary>마법(AP) 피해에 곱할 배율. 1.0이 감소 없음, 1.0 초과면 더 받는다.</summary>
    public float EffectiveMagicMultiplier =>
        Mathf.Max(0f, (data != null ? data.magicArmorMultiplier : 1f) + magicArmorShred);

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
    /// 최종 피해. 순서: 방깎 → 방어력 감폭 → 배율표.
    ///
    /// ⚠️ 2026-09-05, 사장님 확정(02번)으로 뒤집힘: **"AP는 방어력을 무시한다"는 우리 규칙이었지
    /// 원작이 아니었다** — 원작 플레이어 유닛 중 마법 공격타입은 하나도 없고, 워크3에서
    /// 방어력을 무시하는 건 유닛 평타가 아니라 **능력(스킬) 피해뿐**이다. 그래서 지금은
    /// <b>유닛 평타의 AP만</b> AD와 같은 물리 방어력 감폭을 타게 됐다 — <paramref name="attackType"/>가
    /// <see cref="AttackType.Spells"/>(도움소 스킬 전용 행)면 그대로 방어력을 무시한다. 이 둘을
    /// 가르는 게 이 메서드가 존재하는 이유다: <see cref="AttackType.Magic"/>(평타가 마법인 유닛)과
    /// <see cref="AttackType.Spells"/>(스킬 피해)는 <b>같은 DamageType.AP를 쓰지만 물리 방어력
    /// 취급이 다르다.</b>
    /// </summary>
    float MitigatedDamage(float amount, DamageType type, AttackType attackType, float armorIgnoreRatio)
    {
        // 스킬 피해(Spells 행)만 물리 방어력을 완전히 무시한다 — 그 외(평타의 AP 포함, AD,
        // AD+AP)는 전부 아래 감폭을 탄다.
        bool bypassPhysicalArmor = type == DamageType.AP && attackType == AttackType.Spells;

        if (type == DamageType.AP)
        {
            // 마딜은 **물리 방어력과는 별개로 마법 방어력의 영향을 받는다** — 스킬이든 평타든
            // 마찬가지다(이 축은 이번 정정과 무관하게 그대로 둔다, 사장님 지시).
            // 원작이 적에게 거는 "마법 방어력"(워크3 `Aegr`)이 이 자리다.
            amount *= EffectiveMagicMultiplier;
        }

        if (!bypassPhysicalArmor)
        {
            // 방무뎀은 전부/전무가 아니라 비율이다. 피해를 둘로 갈라 한쪽만 감폭시킨다.
            // 유닛 평타의 AP도 이제 여기로 들어온다 — AD와 완전히 같은 식이다.
            float ignored = Mathf.Clamp01(armorIgnoreRatio);
            amount = amount * (1f - ignored) * ArmorMultiplier(EffectiveArmor)
                   + amount * ignored;
        }

        // 상성표는 **물리·마법 양쪽에 다 건다** — 원작이 그렇다(물리 5행 + magic·spells 2행).
        // 마법이 마법 방어 배율과 이 표를 둘 다 타는 것도 원작 동작이다.
        //
        // 단, **행 종류가 피해 종류와 맞을 때만** 건다. 옛 버그는 "마법이 표를 탄 것"이 아니라
        // **마법에 물리 행을 먹인 것**이었다 — RowMatches가 그 짝을 지킨다.
        if (damageTable != null && DamageTable.RowMatches(type, attackType))
        {
            amount *= damageTable.Multiplier(attackType, ArmorType);
        }
        else if (damageTable != null && !loggedRowMismatch)
        {
            // 데이터가 틀린 것이라 조용히 넘기면 원인을 못 찾는다. 매 타격 찍으면 도배되니 한 번만.
            loggedRowMismatch = true;
            Debug.LogWarning($"{name}: {type} 피해에 {attackType} 행이 들어와 상성표를 건너뛴다. " +
                             "AP는 Magic/Spells, 물리는 Normal/Pierce/Siege/Hero/Chaos여야 한다.", this);
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
    public void TakeDamage(float amount, DamageType type, AttackType attackType,
                           int killerPlayerId, float armorIgnoreRatio = 0f)
    {
        if (isDead) return;

        hp -= MitigatedDamage(amount, type, attackType, armorIgnoreRatio);

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
