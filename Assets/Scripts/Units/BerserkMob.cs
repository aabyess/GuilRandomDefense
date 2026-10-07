using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광폭화 유닛(2026-10-07 사장님 사양, 원작 신세계 광폭화 몹 §⑦를 일부러 바꾼 것) — R61~ 레인 적 중 가끔 일반 적 한 기 대신 나온다.
/// 레인 왼쪽 세로 변만 왕복하며 두 오라를 낸다: ① 주변 적 방어력 +5(원작 A125, 반경 850) ② 주변 적 초당 최대체력 N% 회복(원작 A14I 모드-체젠, 반경 955; N은 신 R61~75 환산 1%).
/// 이 유닛이 죽으면(또는 사라지면) 오라가 풀려 적이 다시 약해진다. 자기도 회복 오라를 받는다(방어 +25는 안 줌).
/// B06B(신세계 광폭화) 꼬리표를 달아 몹삭제·회유 같은 일반 적 전용 스킬 대상에서 빠진다(UnitAttacker.IsNormalEnemy). **서버(호스트)만 붙인다.**
/// 오라는 EnemyDummy.ApplyAllyAuraEffect/RemoveAllyAuraEffect 한 쌍으로 건다 — 범위를 벗어나거나 이 유닛이 사라지면 반드시 짝을 맞춰 뗀다.
/// </summary>
public class BerserkMob : MonoBehaviour
{
    public const string BuffId = "B06B";

    // 원작 반경(w3a aare)을 우리 맵 거리로: 850 ÷ WorldScale, 955 ÷ WorldScale.
    const float DefenseRadiusOriginal = 850f;
    const float RegenRadiusOriginal = 955f;
    const float TickSeconds = 0.5f;

    float defenseBonus = 5f;
    float regenFraction = 0.01f;

    EnemyDummy self;
    SkillEffect defenseEffect, regenEffect;
    readonly HashSet<EnemyDummy> defenseTargets = new HashSet<EnemyDummy>();
    readonly HashSet<EnemyDummy> regenTargets = new HashSet<EnemyDummy>();
    readonly List<EnemyDummy> scratch = new List<EnemyDummy>();
    float tickTimer;
    bool applied;

    public static readonly List<BerserkMob> Active = new List<BerserkMob>();
    public float DefenseRadius => BerserkLook.DefenseRadiusWorld;
    public float RegenRadius => RegenRadiusOriginal / WorldScale.Value;

    /// <summary>WaveSpawner가 스폰 직후 부른다 — 회복 비율(초당 최대체력 분율)·방어 오라 값을 받는다.</summary>
    public void Begin(float regenFractionPerSecond, float defenseAura)
    {
        regenFraction = regenFractionPerSecond;
        defenseBonus = defenseAura;
    }

    void Awake() { self = GetComponent<EnemyDummy>(); }

    void OnEnable() { Active.Add(this); }

    void Start()
    {
        if (self == null) self = GetComponent<EnemyDummy>();
        if (self == null) { enabled = false; return; }
        self.AddBuff(BuffId, 0f);
        self.NameOverride = "광폭화 " + (self.Data != null && !string.IsNullOrEmpty(self.Data.enemyName) ? self.Data.enemyName : "적");
        defenseEffect = new SkillEffect { kind = SkillEffectKind.ArmorBonus, buffId = "A125", multiplier = defenseBonus };
        regenEffect = new SkillEffect { kind = SkillEffectKind.HealPercentOverTime, buffId = "A14I", multiplier = regenFraction };
        // 자기 몫: 회복 오라만(A14I는 self 포함). 방어는 라인 몹과 같다 — 원작 A11U(+25)는 안 쓴다(PM: 원랜디갤 「광폭화는 라인몹과 방어력이 같다」·사장님 사양에 없음).
        self.ApplyAllyAuraEffect(regenEffect);
        applied = true;
        if (!TryGetComponent(out BerserkLook _)) gameObject.AddComponent<BerserkLook>();
    }

    void Update()
    {
        if (!applied || self == null || self.IsDead) return;
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0f) return;
        tickTimer = TickSeconds;
        Refresh(defenseTargets, defenseEffect, DefenseRadius);
        Refresh(regenTargets, regenEffect, RegenRadius);
    }

    // 범위 안 적에게는 한 번씩만 걸고, 범위를 벗어났거나 죽은(사라진) 적에게서는 한 번씩만 뗀다.
    void Refresh(HashSet<EnemyDummy> affected, SkillEffect effect, float radius)
    {
        scratch.Clear();
        foreach (EnemyDummy e in EnemyDummy.AlliesOf(self, radius)) if (e != null && !e.IsDead && !e.HasBuff(BuffId)) scratch.Add(e);
        foreach (EnemyDummy e in scratch) if (affected.Add(e)) e.ApplyAllyAuraEffect(effect);

        List<EnemyDummy> drop = null;
        foreach (EnemyDummy e in affected)
            if (e == null || e.IsDead || !scratch.Contains(e)) (drop ??= new List<EnemyDummy>()).Add(e);
        if (drop == null) return;
        foreach (EnemyDummy e in drop)
        {
            affected.Remove(e);
            if (e != null) e.RemoveAllyAuraEffect(effect);
        }
    }

    void Release(HashSet<EnemyDummy> affected, SkillEffect effect)
    {
        foreach (EnemyDummy e in affected) if (e != null) e.RemoveAllyAuraEffect(effect);
        affected.Clear();
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (!applied) return;
        Release(defenseTargets, defenseEffect);
        Release(regenTargets, regenEffect);
        applied = false;
    }
}
