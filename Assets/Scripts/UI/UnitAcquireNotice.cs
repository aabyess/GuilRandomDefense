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
/// UnitIdentity.OnAcquired(모든 획득 경로가 지나는 한 점)를 구독한다. **한 기에 한 줄** — 원작도 포탈이 유닛마다
/// 「X 획득!」을 따로 띄워 쌓인다. 처음엔 넷 이상이면 「흔함 5기」로 뭉쳤는데, 위습 5개를 한 번에 넣으면 이름이 하나도 안 보여
/// 사장님 요청(「뭐 얻었는지」)을 못 채웠다(09-26 실측). <see cref="Window"/>초 안에 온 것은 모아 두었다가
/// <see cref="MaxLines"/>줄까지 이름으로 띄우고, 넘치는 몫만 「외 N기」로 줄인다(화면이 알림으로 덮이지 않게).
/// 시스템 유닛(해적단 퀘스트 토큰 등)은 알리지 않는다.
/// </summary>
public class UnitAcquireNotice : MonoBehaviour
{
    const float Window = 0.35f;
    const float Duration = 3f;
    const int MaxLines = 8;

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
            List<string> lines = Lines(p.units);
            foreach (string line in lines) PlayerNotification.Show(pair.Key, line, Duration);
            Debug.Log($"[획득 알림] 플레이어 {pair.Key}: {string.Join(" / ", lines)}");   // 알림은 3초면 사라진다 — 로그로도 남겨 판 기록에서 셀 수 있게
            p.units.Clear();
        }
    }

    static string Tint(UnitGrade grade, string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(grade.Color())}>{text}</color>";

    /// <summary>한 기에 한 줄 「이름 - 등급 획득!」. MaxLines를 넘으면 마지막 줄을 「외 N기 획득!」(등급별 수)로.</summary>
    public static List<string> Lines(List<UnitData> units)
    {
        int named = units.Count <= MaxLines ? units.Count : MaxLines - 1;
        List<string> lines = units.Take(named)
            .Select(u => Tint(u.grade, $"{u.unitName} - {u.grade.KoreanName()}") + " 획득!").ToList();
        if (units.Count > named)
            lines.Add("외 " + string.Join(" · ", units.Skip(named).GroupBy(u => u.grade).OrderByDescending(g => g.Key.Tier())
                          .Select(g => Tint(g.Key, $"{g.Key.KoreanName()} {g.Count()}기"))) + " 획득!");
        return lines;
    }
}
