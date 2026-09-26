using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 유닛 획득 알림 — 원작 「000 획득!」(친구 베타 피드백 ④, 사장님 지시 2026-09-26).
///
/// 원작 문구(war3map_new.j): 위습 포탈이 <c>"|cffFF0000"+유닛이름+" 획득!|r"</c>를 **본인에게 3초**(24곳),
/// 유닛 도박은 전원에게 「○○님이 중급유닛 도박으로 △△ 획득 !」 10초. 조합에는 원작 알림이 없다.
/// 사장님 요청 형식 「000 - 흔함 획득!」에 맞춰 <b>「이름 - 등급 획득!」, 등급색, 본인 3초</b>로 통일한다.
///
/// UnitIdentity.OnAcquired(모든 획득 경로가 지나는 한 점)를 구독한다. 같은 순간 여러 기가 오면(도박 여러 번·조합 연쇄)
/// 한 줄씩 쌓이지 않게 <see cref="Window"/>초 안에 온 것을 모아 한 줄로 띄운다 — 셋까지는 나열, 넘으면 등급별 수.
/// 시스템 유닛(해적단 퀘스트 토큰 등)은 알리지 않는다.
/// </summary>
public class UnitAcquireNotice : MonoBehaviour
{
    const float Window = 0.35f;
    const float Duration = 3f;
    const int ListUpTo = 3;

    class Pending { public float flushAt; public readonly List<UnitData> units = new List<UnitData>(); }
    readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
    static UnitAcquireNotice instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        GameObject host = new GameObject("UnitAcquireNotice");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<UnitAcquireNotice>();
    }

    void OnEnable() => UnitIdentity.OnAcquired += HandleAcquired;
    void OnDisable() => UnitIdentity.OnAcquired -= HandleAcquired;

    void HandleAcquired(UnitIdentity unit, UnitInventory inventory)
    {
        if (unit == null || unit.Data == null || unit.Data.isSystemUnit || inventory == null) return;
        // UnitInventory는 PlayerContext의 직렬화 참조라 같은 오브젝트에 있다는 보장이 없다 — 그 인벤토리를 가진 컨텍스트를 찾는다.
        PlayerContext context = PlayerContext.All.FirstOrDefault(c => c != null && c.UnitInventory == inventory);
        if (context == null) return;
        if (!pending.TryGetValue(context.PlayerId, out Pending p)) pending[context.PlayerId] = p = new Pending();
        if (p.units.Count == 0) p.flushAt = Time.unscaledTime + Window;
        p.units.Add(unit.Data);
    }

    void Update()
    {
        foreach (KeyValuePair<int, Pending> pair in pending)
        {
            Pending p = pair.Value;
            if (p.units.Count == 0 || Time.unscaledTime < p.flushAt) continue;
            string message = Format(p.units);
            PlayerNotification.Show(pair.Key, message, Duration);
            Debug.Log($"[획득 알림] 플레이어 {pair.Key}: {message}");   // 알림은 3초면 사라진다 — 로그로도 남겨 판 기록에서 셀 수 있게
            p.units.Clear();
        }
    }

    static string Tint(UnitGrade grade, string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(grade.Color())}>{text}</color>";

    public static string Format(List<UnitData> units)
    {
        if (units.Count <= ListUpTo)
            return string.Join(" · ", units.Select(u => Tint(u.grade, $"{u.unitName} - {u.grade.KoreanName()}"))) + " 획득!";
        return string.Join(" · ", units.GroupBy(u => u.grade).OrderByDescending(g => g.Key.Tier())
                   .Select(g => Tint(g.Key, $"{g.Key.KoreanName()} {g.Count()}기"))) + " 획득!";
    }
}
