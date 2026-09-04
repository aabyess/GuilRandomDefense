using System.Collections.Generic;
using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] float hp = 10f;

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
        }

        // Instantiate는 동기 호출이라(Awake가 그 안에서 바로 돈다) Update가 끼어들 틈이 없다 —
        // 지금 CurrentRound가 곧 이 적을 내보낸 웨이브의 라운드다. WaveSpawner를 거치지 않고
        // 이렇게 읽어서, 라운드 번호를 실어 나르려고 그 파일을 고칠 필요가 없다.
        SpawnRound = RoundManagerRef != null ? RoundManagerRef.CurrentRound : 0;
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

    // 이 개체에 걸린 방깎 누적. UnitTraitData의 ArmorShred와 조합표의 `방깍(45)` 능력이 여기 쌓인다.
    float armorShred;

    /// <summary>방깎을 적용한 실효 방어력. 하한 -20.</summary>
    public float EffectiveArmor =>
        Mathf.Max(ArmorFloor, (data != null ? data.armor : 0f) - armorShred);

    public ArmorType ArmorType => data != null ? data.armorType : ArmorType.Normal;

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
    /// AP는 방어력을 무시한다(원작: "마법 데미지는 적의 방어력에 영향을 받지 않는다").
    /// </summary>
    float MitigatedDamage(float amount, DamageType type, AttackType attackType, float armorIgnoreRatio)
    {
        // 순수 마법(AP)은 방어력 자체를 안 탄다 — 비율과 무관하게 전부 통과한다.
        // AD+AP는 확정 전까지 AD와 동일하게 감폭시킨다 — "겸한다"는 것만 알고 어떻게 겸하는지
        // 모르는 상태에서 감폭을 빼면, 물리·마법 겸용이 순수 마법보다 유리해진다
        // (`ARMOR_SYSTEM_DESIGN.md` §7 질문 3).
        if (type == DamageType.AP)
        {
            // 마딜은 **물리 방어력을 무시하되 마법 방어력의 영향은 받는다** — 별개 축이다.
            // 원작이 적에게 거는 "마법 방어력"(워크3 `Aegr`)이 이 자리다.
            amount *= EffectiveMagicMultiplier;
        }
        else
        {
            // 방무뎀은 전부/전무가 아니라 비율이다. 피해를 둘로 갈라 한쪽만 감폭시킨다.
            float ignored = Mathf.Clamp01(armorIgnoreRatio);
            amount = amount * (1f - ignored) * ArmorMultiplier(EffectiveArmor)
                   + amount * ignored;

            // AD+AP에는 마법 배율을 안 건다. 지금은 AD와 똑같이 취급하는데(§7 질문 3),
            // 여기서만 마법 배율을 더하면 물리 감폭과 마법 배율을 **둘 다** 맞아 이중으로 불리해진다.
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
