using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// S2 2.323 채팅 기록 명령(사양 GRD_s2_ideas/spec_ui.md 2절) — -점수판 · -점수판흔함 · 흔함 · -스토리 · -리롤 · -문.
/// 원작처럼 **정확히 일치**할 때만 반응하고, 결과는 친 사람에게만(PlayerNotification — 원격 슬롯은 NetPlayer로 넘어간다).
/// GameChatBox.TryExecuteCode 맨 앞에서 부른다 = 호스트(권위)에서 돈다(도박·위습·스토리 집계가 호스트에만 있다).
/// 한글 자모 별칭(ㅈㅅㅍ·ㅎㅎ·gg·ff·va 등)은 보통 말과 겹쳐 넣지 않았다(사장님 답 대기, 사양 모호점 3).
/// 여러 줄은 알림 한 줄씩 보낸다 — 알림 칸은 줄 높이가 고정이라 「\n」 한 덩어리는 겹쳐 그려진다.
/// 원작 줄 중 우리에게 없는 것(모드·노드·보물 발견 횟수·-황금종)은 뺐다.
/// </summary>
public static class StatsChatCommands
{
    const string Num = "FAED7D";    // 원작 숫자 색
    const string Head = "F15F5F";   // 원작 항목 머리 색

    static string N(int n) => $"<color=#{Num}>{n}</color>";

    /// <summary>명령이 아니면 null, 명령이면 짧은 확인 문구(결과 줄은 알림으로 이미 보냈다).</summary>
    public static string TryHandle(int playerId, string text)
    {
        if (text == null) return null;
        switch (text.Trim())
        {
            case "-점수판": case "-score": Send(playerId, Score(playerId), 15f); return "점수판";
            case "-점수판흔함": case "-scorecommon": Send(playerId, ScoreCommon(playerId), 15f); return "점수판(흔함)";
            case "흔함": case "common": Send(playerId, OwnedCommons(playerId), 15f); return "흔함 통계";
            case "-스토리": case "-story": Send(playerId, Stories(), 15f); return "스토리 기록";
            case "-리롤": case "-reroll": Send(playerId, Reroll(playerId), 5f); return "희귀 리롤";
            case "-문": case "-door": case "-폐문": Send(playerId, Door(), 10f); return "정의의 문";
            default: return null;
        }
    }

    static void Send(int playerId, List<string> lines, float seconds)
    {
        foreach (string line in lines) PlayerNotification.Show(playerId, line, seconds);
    }

    // ---- 2.1 -점수판 ----
    static List<string> Score(int p)
    {
        var lines = new List<string>();
        RunStats.PlayerStats s = RunStats.Of(p) ?? new RunStats.PlayerStats();
        lines.Add($"◎ 플레이어 : {PlayerDisplayName.Of(p)}");
        lines.Add($"◎ 버전 : <color=#6799FF>{GameVersion.Label}</color>");
        lines.Add($"◎ 난이도 : {DifficultyText()}");
        lines.Add(OroSeong.ScoreboardLine ?? "◎ 오로성 : 없음");   // 구현담당3 요청: 퇴치되면 「(퇴치)」까지 OroSeong이 붙인다
        lines.Add($"◎ 최종 유닛 카운트 : {N(Mathf.Max(0, s.finalUnitCount))}");
        lines.Add($"◎ 항법 : <color=#B2EBF4>{NavigationName(p)}</color>");
        lines.Add($"◎ 최상위 유닛 : {N(s.topCombines)}개");
        lines.Add("◎ 유닛 도박");
        int lo = (int)RunStats.GambleTier.Low, mid = (int)RunStats.GambleTier.Mid, hi = (int)RunStats.GambleTier.High;
        int ow = (int)RunStats.GambleTier.OtherWorld, ab = (int)RunStats.GambleTier.Absalom;
        lines.Add($"  <color=#{Head}>▶ 하급</color> : {N(s.gambleTries[lo])}회 / 안흔함 {N(s.gambleUncommon)}개 / 흔함 {N(s.gambleCommon)}개");
        lines.Add($"  <color=#{Head}>▶ 중급</color> : {N(s.gambleTries[mid])}회 / 성공 {N(s.gambleSuccess[mid])}회");
        lines.Add($"  <color=#{Head}>▶ 고급</color> : {N(s.gambleTries[hi])}회 / 희귀 {N(s.gambleRare)}개 / 특별 {N(s.gambleSpecial)}개");
        lines.Add($"  <color=#{Head}>▶ 이세계</color> : {N(s.gambleTries[ow])}회 / 성공  {N(s.gambleSuccess[ow])}회");
        lines.Add($"  <color=#{Head}>▶ 압살롬</color> : {N(s.gambleTries[ab])}회 / 성공  {N(s.gambleSuccess[ab])}회");
        lines.Add($"◎ 목재도박 : {N(s.woodGambleTries)}회 / 성공  {N(s.woodGambleSuccess)}회");
        lines.Add($"◎ 흔함판매 : {N(s.commonSells)}회 / ◎ 선택위습 사용 : {N(s.choiceWispUses)}회");
        lines.Add($"◎ 베리 : {N(Berry.Get(p))}");
        return lines;
    }

    static string DifficultyText()
    {
        DifficultyManager d = DifficultyManager.Instance;
        if (d == null || !d.IsModeSelected) return "미선택";
        DifficultyMode mode = d.Current;
        string color = mode switch   // RoundManager.AnnounceClear와 같은 원작 udg_Mode 색
        {
            DifficultyMode.Easy => "00BFFF",
            DifficultyMode.Normal => "EE82EE",
            DifficultyMode.Hard => "FF0000",
            DifficultyMode.Hell => "9400D3",
            DifficultyMode.God => "FFD700",
            _ => "530080",
        };
        return $"<color=#{color}>{mode.KoreanName()}</color>";
    }

    static string NavigationName(int p)
    {
        PlayerContext c = PlayerContext.Get(p);
        NavigationChoice choice = c != null && c.NavigationState != null ? c.NavigationState.Choice : NavigationChoice.None;
        switch (choice)
        {
            case NavigationChoice.Hegemon: return "패왕의길";
            case NavigationChoice.Union: return "연합세력";
            case NavigationChoice.Gambler: return "도박광";
            case NavigationChoice.SupportBoost: return "도움소 강화";
            case NavigationChoice.SupportLock: return "도움소 잠금";
            default: return "미선택";
        }
    }

    // ---- 2.2 -점수판흔함 ----
    static List<string> ScoreCommon(int p)
    {
        var lines = new List<string> { "◎ '랜덤 위습' 으로 획득한 '흔함 유닛' 통계" };
        if (!UnitPortal.TryGetCommonPullCounts(p, out List<UnitData> order, out int[] counts))
        {
            lines.Add("  <color=#626262>데이터 없음</color>");
            return lines;
        }
        int total = 0; foreach (int c in counts) total += c;
        lines.Add($"  <color=#ffdead>◈</color> 총 사용한 랜덤위습 : {N(total)}개");
        for (int i = 0; i < order.Count; i++)
            lines.Add($"    <color=#{Head}>▶ {UnitName(order[i])}</color> : {N(counts[i])}개");
        return lines;
    }

    // ---- 2.3 흔함 ----
    static List<string> OwnedCommons(int p)
    {
        var lines = new List<string> { "◎ 현재 플레이어가 보유한 '흔함 유닛' 통계" };
        var have = new Dictionary<UnitData, int>();
        var seen = new List<UnitData>();
        PlayerContext c = PlayerContext.Get(p);
        if (c != null && c.UnitInventory != null)
            foreach (UnitIdentity u in c.UnitInventory.Members)
            {
                if (u == null || u.Data == null || u.Data.grade != UnitGrade.Common || u.IsSummon) continue;
                have.TryGetValue(u.Data, out int n); have[u.Data] = n + 1;
                if (!seen.Contains(u.Data)) seen.Add(u.Data);
            }
        // 순서: 랜덤위습 흔함 pool 순서(0개도 보인다, 원작 9줄 고정), 그 밖에 가진 흔함은 뒤에
        var rows = new List<UnitData>();
        if (UnitPortal.TryGetCommonPullCounts(p, out List<UnitData> order, out _)) rows.AddRange(order);
        foreach (UnitData d in seen) if (!rows.Contains(d)) rows.Add(d);
        foreach (UnitData d in rows)
        {
            if (d == null) continue;
            have.TryGetValue(d, out int n);
            lines.Add($"    <color=#{Head}>◈ {UnitName(d)}</color> : {N(n)}개");
        }
        return lines;
    }

    static string UnitName(UnitData d) => d == null ? "?" : d.DisplayName;

    // ---- 2.4 -스토리 ----
    static List<string> Stories()
    {
        var lines = new List<string> { "◎ 스토리 기록조회 (이름 / 시간 / MVP 플레이어 / 퍼센트)" };
        StoryManager sm = StoryManager.Instance;
        if (sm == null) { lines.Add("  <color=#626262>데이터 없음</color>"); return lines; }
        for (int i = 0; ; i++)
        {
            StoryData story = sm.StoryAt(i);
            if (story == null) break;
            string name = $"<color=#{Head}>{StoryManager.DisplayName(story)}</color>";
            if (!StoryRecords.TryGet(i, out StoryRecords.Record r)) { lines.Add($"  {name} / <color=#626262>데이터 없음</color>"); continue; }
            int sec = Mathf.FloorToInt(r.seconds);
            string time = $"<color=#{Num}>{sec / 60:00}분 {sec % 60:00}초</color>";
            if (r.mvpPlayer < 0) { lines.Add($"  {name} / {time} / <color=#626262>MVP 없음</color>"); continue; }
            lines.Add($"  {name} / {time} / <color=#{PlayerHex(r.mvpPlayer)}>{PlainName(r.mvpPlayer)}</color> / <color=#{Num}>{r.mvpPercent:0.000}%</color>");
        }
        return lines;
    }

    /// <summary>칭호 없는 이름(색을 입히려고) — 비면 「플레이어 N」.</summary>
    public static string PlainName(int slot)
    {
        string raw = PlayerDisplayName.RawNickname(slot);
        return string.IsNullOrWhiteSpace(raw) ? $"플레이어 {slot + 1}" : raw;
    }

    /// <summary>워크3 플레이어 색(빨강·파랑·청록·보라) — 스토리 MVP·유틸보드 이름 색.</summary>
    public static string PlayerHex(int slot)
    {
        switch (slot)
        {
            case 0: return "FF0303";
            case 1: return "0042FF";
            case 2: return "1CE6B9";
            case 3: return "8A2BE2";
            default: return "FFFFFF";
        }
    }

    // ---- 2.5 -리롤 ----
    static List<string> Reroll(int p)
    {
        PlayerContext c = PlayerContext.Get(p);
        UniqueRerollState st = c != null ? c.UniqueRerollState : null;
        int left = st != null ? Mathf.Max(0, st.Limit - st.AttemptsUsed) : 0;
        return new List<string> { $"<color=#04ffc4>[알림]</color> 현재 남은 <color=#FF8200>희귀 리롤 횟수</color>는 <color=#ff0000>{left}</color>회 입니다." };
    }

    // ---- 2.6 -문 ----
    static List<string> Door()
    {
        DestructibleGate gate = DestructibleGate.Active.Count > 0 ? DestructibleGate.Active[0]
            : Object.FindFirstObjectByType<DestructibleGate>(FindObjectsInactive.Include);
        if (gate == null) return new List<string> { $"<color=#{Head}>◈ 정의의 문</color>이 이번 판에 없습니다." };
        float pct = gate.IsBroken ? 0f : gate.HpRatio * 100f;
        return new List<string> { $"<color=#{Head}>◈ 정의의 문 남은 체력</color> : {pct:0.000}%" };
    }
}
