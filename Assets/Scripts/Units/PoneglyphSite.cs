using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 히든 미션 「포네그리프 해독」(S2 j:51723~51843) — 공유 미션 8개를 다 깨면 열린다(S2Missions).
/// 원작은 맵 구석 구역(rect Cd)에 유닛을 넣는다. 우리 맵엔 대응 자리가 없어서 기록지침 창의 「해독 시작」으로 연다:
///   내 유닛 하나(전설적인 이상 — 원작도 판매 능력 있는 하위 등급은 「능력이 부족합니다」)를 골라 두고 누르면
///   그 유닛이 150초(포네그리프 해석자 = 로빈·오뎅 대응 유닛 조합 뒤엔 50초) 동안 공격·이동을 멈추고 해독한다.
///   한 번에 한 유닛 · 그 사이 유닛이 사라지면(판매·조합 재료) 실패, 다시 도전 가능 · 성공하면 전원 1베리 + 남은 보물 위치 전부 표시, 판에 한 번.
/// 판정은 호스트. 남은 시간은 NetGameState.S2PoneLeft로 클라에 보인다.
/// </summary>
public static class PoneglyphSite
{
    static Runner runner;
    static UnitIdentity decoding;
    static int decoderOwner = -1;
    public static float SecondsLeft { get; private set; }

    class Runner : MonoBehaviour { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { runner = null; ResetForNewRun(); }

    public static void ResetForNewRun() { if (runner != null) runner.StopAllCoroutines(); decoding = null; decoderOwner = -1; SecondsLeft = 0f; }

    public static void OnUnlocked()
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        Debug.Log("[S2 미션] 포네그리프 해독 해금");
        foreach (PlayerContext c in PlayerContext.Occupied)
            PlayerNotification.Show(c.PlayerId, "<color=#FFD700>[히든미션]</color> 기록지침(F9)에서 전설적인 이상 유닛을 골라 「해독 시작」을 누르세요.", 10f);
    }

    /// <summary>호스트: 해독 시작 요청.</summary>
    public static void TryStart(int playerId, UnitIdentity unit)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        if (!GameAuthority.IsServer) return;
        if (!S2Missions.HiddenUnlocked || S2Missions.PoneDone) return;
        if (SecondsLeft > 0f) { PlayerNotification.Show(playerId, "다른 유닛이 '포네그리프 해독' 중입니다.", 5f); return; }
        if (unit == null || unit.Data == null || unit.IsSummon || !unit.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId != playerId)
        {
            PlayerNotification.Show(playerId, "해독할 내 유닛을 먼저 고르세요.", 5f);
            return;
        }
        if (unit.Data.grade.Tier() < UnitGrade.Legendary.Tier())
        {
            PlayerNotification.Show(playerId, "<color=#04FFC4>[시스템]</color> 이 유닛은 포네그리프를 해독할 능력이 부족합니다.", 5f);
            return;
        }
        if (runner == null) { var go = new GameObject("PoneglyphRunner"); runner = go.AddComponent<Runner>(); }
        runner.StartCoroutine(Decode(playerId, unit));
    }

    static void SetHeld(UnitIdentity unit, bool held)
    {
        if (unit == null) return;
        if (unit.TryGetComponent(out UnitAttacker a)) a.enabled = !held;
        if (unit.TryGetComponent(out UnitMover m)) m.enabled = !held;
        if (unit.TryGetComponent(out NavMeshAgent agent) && agent.isActiveAndEnabled && agent.isOnNavMesh) { agent.isStopped = held; if (held) agent.ResetPath(); }
    }

    static IEnumerator Decode(int playerId, UnitIdentity unit)
    {
        decoding = unit; decoderOwner = playerId;
        float total = S2Missions.PoneDuration;
        SecondsLeft = total;
        string who = $"{PlayerDisplayName.RawNickname(playerId)}의 {unit.Data.DisplayName}";
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            PlayerNotification.Show(c.PlayerId, $"{who}이(가) '포네그리프 해독'을 시도합니다.\n해당 유닛은 {Mathf.RoundToInt(total)}초간 움직일 수 없습니다.", 8f);
            if (total < S2Missions.PoneSeconds) PlayerNotification.Show(c.PlayerId, "'포네그리프 해석자'에 의해 더욱 빠르게 해독됩니다.", 8f);
        }
        SetHeld(unit, true);
        while (SecondsLeft > 0f)
        {
            if (unit == null)   // 판매·조합 재료로 사라짐 = 원작 「사망」
            {
                foreach (PlayerContext c in PlayerContext.Occupied)
                    PlayerNotification.Show(c.PlayerId, "포네그리프 해독 중 유닛이 사망하여 해독에 실패했습니다.", 8f);
                decoding = null; SecondsLeft = 0f;
                yield break;
            }
            SetHeld(unit, true);   // 다른 명령이 다시 켜지 않게 매 프레임
            SecondsLeft -= Time.deltaTime;
            yield return null;
        }
        SetHeld(unit, false);
        decoding = null; SecondsLeft = 0f;
        S2Missions.MarkPoneDone();
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            PlayerNotification.Show(c.PlayerId, $"{who}이(가) '포네그리프 해독'에 성공\n포네그리프에 새겨진 비밀을 밝혀냈습니다.", 10f);
            GameSound.PlayFor(c.PlayerId, GameSoundId.Combine);
        }
        // 보상: 전원 1베리(원작 「보상 : 1 베리 획득!」, j:51760) + 남은 보물 위치 전부 표시(BeL)
        foreach (PlayerContext c in PlayerContext.Occupied)
            if (Berry.Grant(c.PlayerId, 1, null, announce: false))
                PlayerNotification.Show(c.PlayerId, $"<color=#FF8200>보상</color> : <color={Berry.Color}>1 베리</color> 획득!", 10f);
        if (TreasureHunt.Instance != null)
            foreach (PlayerContext c in PlayerContext.Occupied) TreasureHunt.Instance.RevealWithin(c.PlayerId, Vector3.zero, 1e6f, 600f);
    }
}
