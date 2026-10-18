using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미션판(공유 8 + 개인 2) · 개인 퀘스트 3칸 · 히든 「포네그리프 해독」 해금 — 원랜디 S2 2.323(사장님 10-18 「S2 아이디어 넣자」).
/// 사양: ~/grd/GRD_s2_ideas/spec_missions.md(j 줄 번호). 판정·지급은 호스트(싱글), 표시는 각자(MissionBoardPanel이 View로 읽는다).
/// 멀티 동기화: 공유 마스크·플래그는 NetGameState, 개인 마스크·퀘스트는 NetPlayer가 복제한다. 리롤 요청은 NetPlayer.RPC_S2QuestReroll.
///
/// 기본안(PM 10-18, 사장님 답 오기 전):
///   · 신속한 철거 = 기존 스토리2 조기 클리어(9R) 그대로 표시 · 파괴왕 = S2대로 스토리11·30라운드 시작 전(원작 X3≤29), 보상 목재 2 + 발굴
///   · 아이템 발굴(우리 없음) → 아이템 도박 재고 +1
///   · 왜곡·신비 퀘스트 제외 · 와일드카드 → 흔함선택 2 · 로얄로더 ×2 → 특성 1 + 흔함선택 2
///   · 해군대장 미션 완료 때 S2에 있는 목재 1을 더 준다(우리 기존 보상에 빠져 있었다)
/// </summary>
public static class S2Missions
{
    public enum Mission { FastUnique = 0, Demolition, TreasureHunter, Destroyer, Creep, GoldenBell, Soul, SeaKing, JusticeGate, Admiral }
    public const int MissionCount = 10;
    public const int SharedTotal = 8;

    public class MissionInfo
    {
        public string name, shortName, desc, reward;
        public bool shared;
    }

    // 칸 순서 = S2 판 순서(j:57726~57735). 문구는 원작 그대로, 보상 줄은 우리 실제 지급(대체 포함).
    public static readonly MissionInfo[] Missions =
    {
        new MissionInfo { name = "패스트유니크", shortName = "패스트유니크", shared = false, desc = "8라운드 시작 전에 희귀 유닛을 조합하세요.", reward = "+ 흔함선택위습 1개" },
        new MissionInfo { name = "신속한 철거", shortName = "신속한 철거", shared = true, desc = "9라운드 시작 전 [2] 스토리 파괴", reward = "+ 랜덤위습 1개" },
        new MissionInfo { name = "트레져 헌터", shortName = "트레져 헌터", shared = true, desc = "'탐색' 스킬로 보물을 모두 찾으세요.", reward = "+ 2 목재" },
        new MissionInfo { name = "파괴왕", shortName = "파괴왕", shared = true, desc = "30라운드 시작 전 [11] 스토리 파괴", reward = "+ 2 목재 / + 아이템 발굴 1회" },
        new MissionInfo { name = "크립 처치", shortName = "크립 처치", shared = false, desc = "스토리 아래 지역 크립을 3단계 까지 처치하세요.", reward = "+ 2·3단계 처치 보상(크립)" },
        new MissionInfo { name = "황금 종을 울려라", shortName = "황금 종", shared = true, desc = "7시 지역 황금 종을 파괴하세요.", reward = "+ 랜덤위습 1개 / + 3000 골드 / + 아이템 발굴 1회" },
        new MissionInfo { name = "영혼 해방", shortName = "영혼 해방", shared = true, desc = "1시 지역 룸바 해적단의 영혼을 해방하세요.", reward = "+ 3000 골드 / + 1 목재 / + 아이템 발굴 1회" },
        new MissionInfo { name = "해왕류 퇴치", shortName = "해왕류 퇴치", shared = true, desc = "9시 지역 해왕류를 퇴치하세요.", reward = "+ 해왕류 보상 / + 아이템 발굴 1회" },
        new MissionInfo { name = "정의의 문 파괴", shortName = "정의의 문", shared = true, desc = "3시 지역 정의의 문을 파괴하세요.", reward = "+ 정의의 문 보상 / + 아이템 발굴 1회" },
        new MissionInfo { name = "해군대장 저지", shortName = "해군대장", shared = true, desc = "정의의 문 파괴후 해군대장을 처치하세요.", reward = "+ 특성포인트 1개 / + 1 목재" },
    };

    // ───────── 개인 퀘스트(S2 Q000~Q016, 왜곡 Q012·신비 Q017 제외) ─────────
    public enum Quest : sbyte
    {
        Archaeologist = 0, TodayOnly, WoodCollect, GambleAddict, FailLow, FailMid, FailHigh, Mvp,
        CombTranscendent, CombImmortal, CombEternal, CombLimited, CombItem, Quattro, Triple, Recycle
    }
    public const int QuestCount = 16;

    public class QuestInfo
    {
        public string name, desc, reward;
        public int goal = 1;
        public bool atRoundEnd;   // 「라운드 종료후 조건 충족시」(목재수집·리사이클)
    }

    public static readonly QuestInfo[] Quests =
    {
        new QuestInfo { name = "고고학자", desc = "유니크 아이템 2개 이상 획득", goal = 2, reward = "흔함선택위습 2개 + 랜덤위습 1개" },
        new QuestInfo { name = "오늘만산다", desc = "15라운드 전까지 선택위습 사용 11회 이상", goal = 11, reward = "흔함선택위습 3개" },
        new QuestInfo { name = "목재수집", desc = "목재 도박 횟수 9 이상", goal = 9, atRoundEnd = true, reward = "목재 3개" },
        new QuestInfo { name = "도박중독", desc = "하급,중급,고급도박 각각 4회씩 시도", goal = 4, reward = "흔함선택위습 2개" },
        new QuestInfo { name = "도박실패(하급)", desc = "하급도박 4회 실패", goal = 4, reward = "안흔함위습 1개" },
        new QuestInfo { name = "도박실패(중급)", desc = "중급도박 4회 실패", goal = 4, reward = "흔함선택위습 2개" },
        new QuestInfo { name = "도박실패(고급)", desc = "고급도박 4회 실패", goal = 4, reward = "희귀함위습 1개" },
        new QuestInfo { name = "MVP 선정", desc = "스토리 기여도 1등 4회 달성", goal = 4, reward = "흔함선택위습 1개" },
        new QuestInfo { name = "유닛조합(초월)", desc = "'초월' 등급 유닛을 1개 이상 조합", reward = "흔함선택위습 2개" },
        new QuestInfo { name = "유닛조합(불멸)", desc = "'불멸' 등급 유닛을 1개 이상 조합", reward = "흔함선택위습 2개" },
        new QuestInfo { name = "유닛조합(영원)", desc = "'영원' 등급 유닛을 1개 이상 조합", reward = "흔함선택위습 2개" },
        new QuestInfo { name = "유닛조합(제한)", desc = "'제한' 등급 유닛을 1개 이상 조합", reward = "흔함선택위습 1개" },
        new QuestInfo { name = "유닛조합(아이템)", desc = "아이템이 필요한 유닛을 1개 이상 조합", reward = "흔함선택위습 3개" },
        new QuestInfo { name = "콰트로", desc = "최상위 유닛 4개 이상 조합", goal = 4, reward = "특성포인트 1개 + 흔함선택위습 2개" },
        new QuestInfo { name = "트리플", desc = "최상위 유닛 3개 이상 조합", goal = 3, reward = "특성포인트 1개" },
        new QuestInfo { name = "리사이클", desc = "흔함 유닛 판매 수 9개 이상", goal = 9, atRoundEnd = true, reward = "흔함선택위습 2개" },
    };

    public const int RerollBeforeRound = 10;
    public const float PoneSeconds = 150f, PoneSecondsFast = 50f;

    // ───────── 호스트 상태 ─────────
    static int sharedMask;            // 공유 미션 완료 비트(1 << Mission)
    static readonly int[] personalMask = new int[RunStats.MaxPlayers];
    static readonly sbyte[,] questIds = new sbyte[RunStats.MaxPlayers, 3];
    static readonly bool[,] questDone = new bool[RunStats.MaxPlayers, 3];
    static readonly bool[] rerollUsed = new bool[RunStats.MaxPlayers];
    static readonly List<sbyte> pool = new List<sbyte>();
    static bool assigned;
    static bool hiddenUnlocked, poneDone, interpreter;
    static bool hooked;

    public static int SharedCount { get { int n = 0; for (int i = 0; i < MissionCount; i++) if ((sharedMask & (1 << i)) != 0) n++; return n; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { hooked = false; ResetForNewRun(); }

    public static void ResetForNewRun()
    {
        sharedMask = 0; assigned = false; hiddenUnlocked = poneDone = interpreter = false;
        System.Array.Clear(personalMask, 0, personalMask.Length);
        System.Array.Clear(rerollUsed, 0, rerollUsed.Length);
        for (int p = 0; p < RunStats.MaxPlayers; p++) for (int k = 0; k < 3; k++) { questIds[p, k] = -1; questDone[p, k] = false; }
        pool.Clear();
        PoneglyphSite.ResetForNewRun();
        Hook();
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        RunStats.Changed += p => EvaluateQuests(p, roundEnd: false);
        RunStats.Combined += OnCombined;
        RoundManager.RoundStartedEvent += OnRoundStarted;
        RoundManager.RoundEndedEvent += r => { foreach (PlayerContext c in PlayerContext.Occupied) EvaluateQuests(c.PlayerId, roundEnd: true); };
        RoundManager.ClearRewardHook += OnClear;
        S2Objectives.GoldenBellDestroyed += who => Complete(Mission.GoldenBell, who);
        S2Objectives.RumbarSoulFreed += who => Complete(Mission.Soul, who);
    }

    // ───────── 미션판 ─────────

    /// <summary>미션 완료(호스트). 공유는 전원, 개인은 그 플레이어. 이미 깬 것은 무시. 기존 보상이 있는 미션은 그쪽이 준 뒤 여기서 표시·추가분만.</summary>
    public static void Complete(Mission m, int playerId = -1)
    {
        if (!GameAuthority.IsServer) return;
        Hook();
        int bit = 1 << (int)m;
        MissionInfo info = Missions[(int)m];
        if (info.shared)
        {
            if ((sharedMask & bit) != 0) return;
            sharedMask |= bit;
        }
        else
        {
            if (playerId < 0 || playerId >= RunStats.MaxPlayers || (personalMask[playerId] & bit) != 0) return;
            personalMask[playerId] |= bit;
        }

        GrantMissionExtras(m);
        string head = m == Mission.GoldenBell ? "샨도라의 황금 종 울리기" : m == Mission.Soul ? "룸바 해적단의 영혼 해방" : info.name;
        string line = $"<color=#C15AF4>[도전과제]</color> {head} 완료\n<color=#FF8200>보상</color> : {info.reward.Replace("+ ", "").Replace(" / ", ", ")}";
        if (info.shared) foreach (PlayerContext c in PlayerContext.Occupied) { PlayerNotification.Show(c.PlayerId, line, 10f); GameSound.PlayFor(c.PlayerId, GameSoundId.Combine); }
        else if (m != Mission.Creep) { PlayerNotification.Show(playerId, line, 10f); GameSound.PlayFor(playerId, GameSoundId.Combine); }
        Debug.Log($"[S2 미션] {info.name} 완료(플레이어 {playerId}) · 공유 {SharedCount}/{SharedTotal}");

        if (info.shared && SharedCount >= SharedTotal && !hiddenUnlocked)
        {
            hiddenUnlocked = true;
            foreach (PlayerContext c in PlayerContext.Occupied)
                PlayerNotification.Show(c.PlayerId, "<color=#04FFC4>[시스템]</color> 모든 공유 미션을 클리어하셨습니다.\n<color=#FFD700>[히든미션]</color> '포네그리프 해독'이 해금되었습니다.", 10f);
            PoneglyphSite.OnUnlocked();
        }
    }

    // 새 미션(황금 종·영혼·파괴왕)은 여기서 전부 주고, 기존 미션은 S2와의 차이(발굴·목재)만 더한다.
    static void GrantMissionExtras(Mission m)
    {
        S2RewardTable t = S2RewardTable.Get();
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            switch (m)
            {
                case Mission.GoldenBell:
                    GrantWisp(c, t != null ? t.randomWisp : null, 1); c.GoldWallet?.Add(3000); Dig(c); break;
                case Mission.Soul:
                    c.GoldWallet?.Add(3000); Wood(c, 1); Dig(c); break;
                case Mission.Destroyer:
                    Wood(c, 2); Dig(c); break;
                case Mission.SeaKing:
                case Mission.JusticeGate:
                    Dig(c); break;
                case Mission.Admiral:
                    Wood(c, 1); break;
            }
        }
    }

    static void Dig(PlayerContext c) => c.ItemGambleState?.AddStock(1);   // 아이템 발굴 → 아이템 도박 재고 +1(기본안)
    static void Wood(PlayerContext c, int n) { c.ResourceWallet?.Add(ResourceType.Wood, n); if (n > 0) GameSound.PlayFor(c.PlayerId, GameSoundId.Wood); }
    static void GrantWisp(PlayerContext c, WispData w, int n)
    {
        if (w == null || n <= 0 || RewardDistributor.Instance == null) return;
        RewardDistributor.Instance.GrantWisps(c, new List<WispReward> { new WispReward { wisp = w, count = n } });
    }

    /// <summary>스토리 파괴(호스트, RewardDistributor.GrantStoryReward). order = 스토리 번호(1부터), earlyClear = 기존 조기 클리어 판정.</summary>
    public static void OnStoryCleared(int order, bool earlyClear)
    {
        if (order == 2 && earlyClear) Complete(Mission.Demolition);
        if (order == 11 && RunStats.BeforeRound(30)) Complete(Mission.Destroyer);   // S2: [11]드레스로자, X3≤29(j:52038~52102)
    }

    // ───────── 개인 퀘스트 ─────────

    static void OnRoundStarted(int round)
    {
        if (!GameAuthority.IsServer) return;
        if (!assigned) AssignQuests();
    }

    /// <summary>시험용: 라운드 시작을 기다리지 않고 배정.</summary>
    public static void EnsureAssigned() { if (GameAuthority.IsServer && !assigned) AssignQuests(); }

    static void AssignQuests()
    {
        assigned = true;
        pool.Clear();
        for (sbyte q = 0; q < QuestCount; q++) pool.Add(q);
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            if (c.PlayerId >= RunStats.MaxPlayers) continue;
            for (int k = 0; k < 3; k++) questIds[c.PlayerId, k] = Draw();
            PlayerNotification.Show(c.PlayerId, "<color=#F361A6>[퀘스트]</color> 미션판(기록지침)의 리롤 버튼을 클릭하여 10라운드 시작 전까지 미완료 퀘스트를 1회 변경할 수 있습니다.", 10f);
        }
    }

    // 전역 풀에서 하나 꺼내 지운다 — 한 판 안에서 같은 퀘스트가 두 번 안 나온다(S2 CT4, j:57644~57651).
    static sbyte Draw()
    {
        if (pool.Count == 0) return -1;
        int i = Random.Range(0, pool.Count);
        sbyte q = pool[i];
        pool[i] = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1);
        return q;
    }

    /// <summary>리롤(호스트). 10라운드 시작 전 · 미완료 칸 · 판에 1회(S2 BcN, j:8066~8069).</summary>
    public static void TryReroll(int playerId, int slot)
    {
        if (!GameAuthority.IsServer || playerId < 0 || playerId >= RunStats.MaxPlayers || slot < 0 || slot > 2) return;
        if (!assigned || rerollUsed[playerId] || questDone[playerId, slot] || !RunStats.BeforeRound(RerollBeforeRound) || questIds[playerId, slot] < 0)
        {
            PlayerNotification.Show(playerId, "<color=#FF0000>[오류]</color> 퀘스트 변경은 10라운드 시작 전, 미완료 퀘스트에 한해 변경할 수 있습니다.", 5f);
            return;
        }
        sbyte old = questIds[playerId, slot];
        sbyte next = Draw();
        if (next < 0) return;
        pool.Add(old);   // 새것을 먼저 뽑고 옛것을 되돌린다 — 방금 버린 것은 다시 안 나온다
        questIds[playerId, slot] = next;
        rerollUsed[playerId] = true;
        PlayerNotification.Show(playerId, "<color=#F361A6>[퀘스트]</color> 선택한 퀘스트를 새로운 퀘스트로 변경했습니다. (게임 내 변경 기회 사용 완료)", 6f);
        EvaluateQuests(playerId, roundEnd: false);
    }

    /// <summary>진행도(0~goal) — 호스트 계산. 클라는 NetPlayer 복제값.</summary>
    public static int Progress(int playerId, Quest q)
    {
        RunStats.PlayerStats s = RunStats.Of(playerId);
        if (s == null) return 0;
        switch (q)
        {
            case Quest.Archaeologist: return s.uniqueItemsAcquired;
            case Quest.TodayOnly: return s.choiceWispUsesBefore15;
            case Quest.WoodCollect: return s.woodGambleTries;
            case Quest.GambleAddict: return Mathf.Min(s.gambleTries[0], Mathf.Min(s.gambleTries[1], s.gambleTries[2]));
            case Quest.FailLow: return s.gambleFails[0];
            case Quest.FailMid: return s.gambleFails[1];
            case Quest.FailHigh: return s.gambleFails[2];
            case Quest.Mvp: return s.storyMvps;
            case Quest.CombTranscendent: return (s.combinedGradeMask & (1 << (int)UnitGrade.Transcendent)) != 0 ? 1 : 0;
            case Quest.CombImmortal: return (s.combinedGradeMask & (1 << (int)UnitGrade.Immortal)) != 0 ? 1 : 0;
            case Quest.CombEternal: return (s.combinedGradeMask & (1 << (int)UnitGrade.Eternal)) != 0 ? 1 : 0;
            case Quest.CombLimited: return (s.combinedGradeMask & (1 << (int)UnitGrade.Limited)) != 0 ? 1 : 0;
            case Quest.CombItem: return s.combinedItemRecipe ? 1 : 0;
            case Quest.Quattro: case Quest.Triple: return s.topCombines;
            case Quest.Recycle: return s.commonSells;
        }
        return 0;
    }

    static void EvaluateQuests(int playerId, bool roundEnd)
    {
        if (!GameAuthority.IsServer || !assigned || playerId < 0 || playerId >= RunStats.MaxPlayers) return;
        for (int k = 0; k < 3; k++)
        {
            sbyte id = questIds[playerId, k];
            if (id < 0 || questDone[playerId, k]) continue;
            QuestInfo info = Quests[id];
            if (info.atRoundEnd && !roundEnd) continue;
            if (Progress(playerId, (Quest)id) < info.goal) continue;
            questDone[playerId, k] = true;
            GrantQuestReward(playerId, (Quest)id);
            PlayerNotification.Show(playerId, $"<color=#F361A6>[퀘스트]</color> '{info.name}' 완료\n보상 : {info.reward}", 10f);
            Debug.Log($"[S2 퀘스트] 플레이어 {playerId} {info.name} 완료");
        }
    }

    static void GrantQuestReward(int playerId, Quest q)
    {
        PlayerContext c = PlayerContext.Get(playerId);
        S2RewardTable t = S2RewardTable.Get();
        if (c == null || t == null) return;
        switch (q)
        {
            case Quest.Archaeologist: GrantWisp(c, t.choiceWisp, 2); GrantWisp(c, t.randomWisp, 1); break;
            case Quest.TodayOnly: GrantWisp(c, t.choiceWisp, 3); break;
            case Quest.WoodCollect: Wood(c, 3); break;
            case Quest.GambleAddict: case Quest.FailMid: case Quest.CombTranscendent: case Quest.CombImmortal: case Quest.CombEternal: case Quest.Recycle:
                GrantWisp(c, t.choiceWisp, 2); break;
            case Quest.FailLow: GrantWisp(c, t.uncommonWisp, 1); break;
            case Quest.FailHigh: GrantWisp(c, t.rareWisp, 1); break;
            case Quest.Mvp: case Quest.CombLimited: GrantWisp(c, t.choiceWisp, 1); break;
            case Quest.CombItem: GrantWisp(c, t.choiceWisp, 3); break;
            case Quest.Quattro: c.UnitUpgrades?.AddTraitPoints(1); GrantWisp(c, t.choiceWisp, 2); break;
            case Quest.Triple: c.UnitUpgrades?.AddTraitPoints(1); break;
        }
    }

    static void OnCombined(int playerId, UnitData result)
    {
        // 포네그리프 해석자(S2 aJ): 로빈·오뎅 계열 조합 → 해독 50초(j:24340~24352, 32160~32170)
        if (result != null && (result.name == "초월_강재규_AP" || result.name == "영원_서민성") && !interpreter)
        {
            interpreter = true;
            Debug.Log($"[S2 미션] 포네그리프 해석자 켜짐({result.name})");
        }
    }

    // 클리어 정산(FinishRun 전): 개인 퀘스트 셋 다 → 1베리(S2 CUh j:58496~58499) · 유닛별 클리어 기록(S2 CUg j:58478)
    static void OnClear(PlayerContext c)
    {
        int p = c.PlayerId;
        // 유닛별 클리어 기록(S2 CUg): 클리어 때 가진 최상위(초월·불멸·영원·제한) 유닛마다 +1 — 같은 종류 둘이면 2(원작도 유닛마다 센다)
        if (c.UnitInventory != null && c.PersistentSave != null)
            foreach (UnitIdentity u in c.UnitInventory.Members)
                if (u != null && u.Data != null && !u.IsSummon && RunStats.IsTopGrade(u.Data.grade)) c.PersistentSave.AddUnitClear(u.Data.name);
        if (p >= 0 && p < RunStats.MaxPlayers && assigned && questDone[p, 0] && questDone[p, 1] && questDone[p, 2])
            if (Berry.Grant(p, 1, "클리어 보상", announce: false))
                PlayerNotification.Show(p, $"<color=#04FFC4>[클리어 보상]</color> 개인 미션 3개를 모두 완료하여 <color={Berry.Color}>1 베리</color> 추가 획득!", 10f);
    }

    // ───────── 포네그리프 ─────────
    public static bool HiddenUnlocked => hiddenUnlocked;
    public static bool PoneDone => poneDone;
    public static float PoneDuration => interpreter ? PoneSecondsFast : PoneSeconds;
    public static void MarkPoneDone() { poneDone = true; }

    // ───────── 읽기(호스트 상태 · 멀티 클라는 복제값) ─────────
    public static int HostSharedMask => sharedMask;
    public static int HostFlags => (hiddenUnlocked ? 1 : 0) | (poneDone ? 2 : 0) | (interpreter ? 4 : 0);
    public static int HostPersonalMask(int p) => p >= 0 && p < RunStats.MaxPlayers ? personalMask[p] : 0;
    public static sbyte HostQuestId(int p, int k) => p >= 0 && p < RunStats.MaxPlayers ? questIds[p, k] : (sbyte)-1;
    public static bool HostQuestDone(int p, int k) => p >= 0 && p < RunStats.MaxPlayers && questDone[p, k];
    public static bool HostRerollUsed(int p) => p >= 0 && p < RunStats.MaxPlayers && rerollUsed[p];
    public static int HostQuestProgress(int p, int k) { sbyte id = HostQuestId(p, k); return id < 0 ? 0 : Progress(p, (Quest)id); }

    public struct ViewData
    {
        public int sharedMask, personalMask, flags;
        public sbyte[] questIds; public bool[] questDone; public int[] questProgress; public bool rerollUsed;
        public int round;
        public float poneLeft;
    }

    /// <summary>표시용(각자) — 호스트·싱글은 실값, 멀티 클라는 NetGameState·NetPlayer 복제값.</summary>
    public static ViewData View(int playerId)
    {
        var v = new ViewData { questIds = new sbyte[3], questDone = new bool[3], questProgress = new int[3] };
        if (GameAuthority.IsServer)
        {
            v.sharedMask = sharedMask; v.flags = HostFlags; v.personalMask = HostPersonalMask(playerId); v.rerollUsed = HostRerollUsed(playerId);
            for (int k = 0; k < 3; k++) { v.questIds[k] = HostQuestId(playerId, k); v.questDone[k] = HostQuestDone(playerId, k); v.questProgress[k] = HostQuestProgress(playerId, k); }
            v.round = RunStats.RoundStarted();
            v.poneLeft = PoneglyphSite.SecondsLeft;
            return v;
        }
        NetGameState gs = NetGameState.Instance;
        if (gs != null) { v.sharedMask = gs.S2SharedMask; v.flags = gs.S2Flags; v.round = gs.Preparing ? gs.Round - 1 : gs.Round; v.poneLeft = gs.S2PoneLeft; }
        NetPlayer me = null;
        foreach (NetPlayer np in NetPlayer.All) if (np != null && np.Slot == playerId) { me = np; break; }
        if (me != null)
        {
            v.personalMask = me.S2PersonalMask; v.rerollUsed = me.S2RerollUsed;
            for (int k = 0; k < 3; k++) { v.questIds[k] = me.S2QuestIds.Get(k); v.questDone[k] = (me.S2QuestDone & (1 << k)) != 0; v.questProgress[k] = me.S2QuestProgress.Get(k); }
        }
        else for (int k = 0; k < 3; k++) v.questIds[k] = -1;
        return v;
    }

    /// <summary>포네그리프 「해독 시작」(각자) — 지금 고른 내 유닛 하나.</summary>
    public static void RequestPone(UnitIdentity unit)
    {
        if (unit == null) { PlayerNotification.Show(LocalPlayer.LocalPlayerId, "해독할 내 유닛을 먼저 고르세요.", 5f); return; }
        if (GameAuthority.IsServer) { PoneglyphSite.TryStart(LocalPlayer.LocalPlayerId, unit); return; }
        NetEntity entity = unit.GetComponentInParent<NetEntity>();
        if (entity != null && entity.Object != null && entity.Object.IsValid) NetPlayer.Local?.RPC_S2Pone(entity.Object.Id);
    }

    /// <summary>리롤 버튼(각자) — 호스트·싱글은 바로, 멀티 클라는 RPC.</summary>
    public static void RequestReroll(int slot)
    {
        if (GameAuthority.IsServer) { TryReroll(LocalPlayer.LocalPlayerId, slot); return; }
        NetPlayer.Local?.RPC_S2QuestReroll((byte)slot);
    }
}
