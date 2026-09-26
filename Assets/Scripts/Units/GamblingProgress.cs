using System.Collections.Generic;
using UnityEngine;

// 돈 도박 진행 상태. 옵션마다 제약이 다르다(예: 중급도박은 평생 10회, 고급도박은 보스 처치로
// 해금) — SupportShop의 쿨다운 딕셔너리와 같은 결로 옵션 에셋을 키로 삼는다. 옵션이 늘어나도
// 필드를 안 늘린다. GoldWallet/ResourceWallet/UnitInventory와 나란히 PlayerContext에 붙는다.
public class GamblingProgress : MonoBehaviour
{
    readonly Dictionary<GamblingOptionData, int> usesSoFar = new Dictionary<GamblingOptionData, int>();
    readonly HashSet<GamblingOptionData> unlockedOptions = new HashSet<GamblingOptionData>();

    public int UsesSoFar(GamblingOptionData option)
    {
        return option != null && usesSoFar.TryGetValue(option, out int count) ? count : 0;
    }

    public void RecordUse(GamblingOptionData option)
    {
        if (option == null) return;

        usesSoFar.TryGetValue(option, out int count);
        usesSoFar[option] = count + 1;
    }

    public bool IsUnlocked(GamblingOptionData option)
    {
        return option != null && unlockedOptions.Contains(option);
    }

    // 해금 조건(예: 10라운드 보스 처치)이 성립했을 때 그 시스템이 이걸 부른다.
    // 그 시스템이 아직 없어 지금은 아무도 안 부른다 — requiresUnlock인 옵션은 계속 잠겨 있다.
    public void Unlock(GamblingOptionData option)
    {
        if (option == null || !unlockedOptions.Add(option)) return;
        // 해금 순간부터 재고가 시작 값에서 충전된다(원작 H0AZ usin 0 — R10 보스를 잡은 순간 0개, 12초 뒤 1개).
        stocks[option] = new StockState { count = option.stockInitial, lastCharge = Time.time };
    }

    // ── 상점 재고(2026-09-26) — 워크3 usma/usrg/usin. 옵션마다 「지금 개수 · 마지막 충전 시각」 ──
    //    ⚠️ 멀티(구현담당2 GamblingProgress.ApplyReplicated): 복제할 새 상태는 옵션별 StockState(count int · lastCharge float),
    //       옵션별 누적 지급액(cumulativePayout int), 졸업 여부(Graduated bool).
    public struct StockState { public int count; public float lastCharge; }
    readonly Dictionary<GamblingOptionData, StockState> stocks = new Dictionary<GamblingOptionData, StockState>();
    readonly Dictionary<GamblingOptionData, int> cumulativePayout = new Dictionary<GamblingOptionData, int>();
    float startTime;

    void Awake() => startTime = Time.time;

    StockState Advance(GamblingOptionData option)
    {
        if (!stocks.TryGetValue(option, out StockState s))
            s = new StockState { count = option.stockInitial, lastCharge = startTime };   // 해금 없는 옵션은 판 시작부터 충전
        if (option.stockRegenSeconds > 0f)
        {
            if (s.count >= option.stockMax) s.lastCharge = Time.time;   // 가득 차 있는 동안엔 충전 시계가 안 돈다(워크3와 같다)
            else
                while (s.count < option.stockMax && Time.time - s.lastCharge >= option.stockRegenSeconds)
                {
                    s.count++;
                    s.lastCharge += option.stockRegenSeconds;
                }
        }
        stocks[option] = s;
        return s;
    }

    /// <summary>지금 재고. stockMax 0인 옵션은 제한 없음(int.MaxValue).</summary>
    public int Stock(GamblingOptionData option)
    {
        if (option == null || option.stockMax <= 0) return int.MaxValue;
        if (option.requiresUnlock && !IsUnlocked(option)) return 0;
        return Advance(option).count;
    }

    /// <summary>다음 1개가 차기까지 남은 초. 가득 찼거나 재고가 없는 옵션이면 0.</summary>
    public float SecondsToNextStock(GamblingOptionData option)
    {
        if (option == null || option.stockMax <= 0 || option.stockRegenSeconds <= 0f) return 0f;
        if (option.requiresUnlock && !IsUnlocked(option)) return 0f;
        StockState s = Advance(option);
        return s.count >= option.stockMax ? 0f : Mathf.Max(0f, option.stockRegenSeconds - (Time.time - s.lastCharge));
    }

    public void ConsumeStock(GamblingOptionData option)
    {
        if (option == null || option.stockMax <= 0) return;
        StockState s = Advance(option);
        // 가득 찬 상태에서 하나 쓰면 그 순간부터 충전 시계가 돈다.
        if (s.count >= option.stockMax) s.lastCharge = Time.time;
        s.count = Mathf.Max(0, s.count - 1);
        stocks[option] = s;
    }

    // ── 졸업(원작 돈도박 고급 누적 35,000) ──
    public int CumulativePayout(GamblingOptionData option) => option != null && cumulativePayout.TryGetValue(option, out int v) ? v : 0;
    public void AddPayout(GamblingOptionData option, int amount)
    {
        if (option == null) return;
        cumulativePayout[option] = CumulativePayout(option) + amount;
    }

    public bool Graduated { get; private set; }
    public event System.Action OnGraduated;
    public void Graduate()
    {
        if (Graduated) return;
        Graduated = true;
        OnGraduated?.Invoke();
    }
}
