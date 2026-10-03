using UnityEngine;

/// <summary>
/// 정의문 뒤 「해군대장 저지」 퀘스트(Trig_Red_dog)가 거는 팀 전체 영구 버프 — 원작은 SetPlayerTechResearchedSwap(R025/R026/R027)로
/// 연구소 오라(h06S의 A05F·A0QC·A03S, 범위 9999)의 requirement를 풀어 켠다. 비영웅 유닛의 능력 레벨은 1이라 값은 1레벨 값이다(w3a 직접 디코드):
///   R025 강렬함 = 아군 공격력 +5%(Cac1) · R027 신속함 = 아군 공격속도 +4%(Oae2) · R026 냉철함 = 적 이동속도 −6%(Oae1).
/// 서버만 켠다(전투 계산이 서버). 판이 바뀌면 Reset.
/// </summary>
public static class TeamBuffs
{
    public const float IntensePercent = 0.05f;   // 강렬함
    public const float SwiftPercent = 0.04f;     // 신속함
    public const float CalmSlow = 0.06f;         // 냉철함

    public static float AttackPowerPercent { get; private set; }
    public static float AttackSpeedPercent { get; private set; }
    public static float EnemySlowMultiplier { get; private set; } = 1f;

    public static void ActivateIntense() => AttackPowerPercent = IntensePercent;
    public static void ActivateSwift() => AttackSpeedPercent = SwiftPercent;

    public static void ActivateCalm()
    {
        EnemySlowMultiplier = 1f - CalmSlow;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
            if (enemy != null) enemy.RefreshSlow();
    }

    public static void Reset()
    {
        AttackPowerPercent = 0f;
        AttackSpeedPercent = 0f;
        EnemySlowMultiplier = 1f;
    }
}
