using System.Collections.Generic;
using UnityEngine;

// 원작 "채팅 코드 입력" 초월/불멸/영원 획득(war3map.j Trig_Eternal_*/Trig_Forever_*/
// Trig_IM_*, Docs/reference/ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv 47행 + Nika 1행).
// Hidden 23종은 여기 없다 — 채팅이 재료 조합의 확인 트리거일 뿐이라 다른 진입점이 필요하다
// (PM 지시 2026-09-05: CombineSystem에 합치지 말고 판정 로직만 재사용, 진입점은 따로).
//
// "규칙 → 확인 → UI" 순서로 짰다 — TryUnlock(playerId, ChatUnlockData)가 UI와 무관한
// 판정·지급 전부이고, 입력창은 GameChatBox 하나가 이 매니저와 HiddenCombineManager를 같이
// 물고 있다(사장님 지시 2026-09-05: 채팅 코드와 히든 조합을 같은 입력창 하나로). 나중에
// 다른 UI(버튼 등)가 필요해져도 TryUnlock을 그대로 부르면 된다.
public class ChatUnlockManager : MonoBehaviour
{
    [SerializeField] List<ChatUnlockData> unlocks = new List<ChatUnlockData>();
    [SerializeField] UnitSpawner unitSpawner;

    // 공유 1회 게이트(원작 udg_Tech_Onedill) — Eternal·Forever·Immortal 47개가 전부 이
    // 게이트 하나를 같이 쓴다. 플레이어가 이 중 아무거나 하나를 받으면 나머지 46개가
    // 전부 잠긴다 — "유닛마다 1회"가 아니라 "이 47개를 통틀어 1회"다.
    //
    // ⚠️ 저장하지 않는다 — 한 판 한정이다. 원작 `udg_Tech_Onedill`은 InitGlobals에서
    // false로 시작하고 SavePlayer 목록에 없다(PM이 원문에서 직접 확인, 2026-09-05) —
    // 원작이 의도적으로 매판 리셋한다. 11번(영속 저장, 구현담당2 작업 중)이 생겨도
    // **이 값은 거기 실으면 안 된다.**
    readonly Dictionary<int, bool> hasClaimedShared = new Dictionary<int, bool>();

    public bool HasClaimedSharedSlot(int playerId) =>
        hasClaimedShared.TryGetValue(playerId, out bool claimed) && claimed;

    // 항법(원작 route) "패왕의 길" 전제 — 사장님 판단 대기라 항법 시스템 자체가 아직 없다.
    // 기본값을 열림(true)으로 둔다: 이 게이트가 될 시스템이 "정해지지 않았을 뿐 존재는 할
    // 것"이라, 지금 열어두면 원작보다 살짝 후해질 뿐이다. 반대로 잠가두면 47개가 통째로
    // 하나도 안 나와 테스트 자체가 안 된다. 항법이 정해지면 이 메서드를 실제 조회로 바꾼다.
    public virtual bool HasChosenConquerorPath(int playerId) => true;

    // Nika 전제(원작 udg_Nika_Johab_Bool+udg_Nika_Item_Bool, 루피 기어5 계열로 보이는
    // 별도 퀘스트) — 우리 게임엔 그 퀘스트 자체가 없다. 기본값을 잠김(false)으로 둔다:
    // 항법과 반대로, 이건 "생길 시스템"이 아니라 "아직 아예 없는 전제"라 열어두면 없는
    // 조건을 통과시켜 공짜로 나가버린다. 판단 기준은 "나중에 생기는가"가 아니라
    // "지금 열면 규칙이 깨지는가"다.
    public virtual bool HasNikaPrerequisite(int playerId) => false;

    /// <summary>채팅 문구로 찾아 시도한다 — GameChatBox(통합 입력창)가 이걸 쓴다. 대소문자
    /// 구분 없이, 영문·한글 문구 둘 다 받는다(원작 CSV가 항상 "영문 / 한글" 쌍이다).
    /// <paramref name="message"/>는 문구가 이 매니저 소관으로 인식됐을 때만 채워진다(성공이든
    /// 실패든) — null이면 "이 매니저는 이 문구를 모른다"는 뜻이라, 호출부(GameChatBox)가
    /// 다음 판정기(HiddenCombineManager)로 넘겨야 한다는 신호로 쓴다.</summary>
    public bool TryUnlockByPhrase(int playerId, string text, out string message)
    {
        ChatUnlockData match = FindByPhrase(text);
        if (match == null) { message = null; return false; }

        bool unlocked = TryUnlock(playerId, match);
        message = lastResultMessage;
        return unlocked;
    }

    public string LastResultMessage => lastResultMessage;

    ChatUnlockData FindByPhrase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string trimmed = text.Trim();

        foreach (ChatUnlockData data in unlocks)
        {
            if (data == null) continue;
            if (string.Equals(data.phraseEnglish, trimmed, System.StringComparison.OrdinalIgnoreCase)) return data;
            if (string.Equals(data.phraseKorean, trimmed, System.StringComparison.OrdinalIgnoreCase)) return data;
        }
        return null;
    }

    /// <summary>어떤 UI(텍스트 입력이든 버튼이든)든 데이터를 직접 골랐으면 이걸 부른다.</summary>
    public bool TryUnlock(int playerId, ChatUnlockData data)
    {
        if (!GameAuthority.IsServer) return false;
        if (data == null || data.result == null) return false;

        if (!CanUnlock(playerId, data, out string reason))
        {
            lastResultMessage = $"{data.displayName}: {reason}";
            return false;
        }

        PlayerContext context = PlayerContext.Get(playerId);
        if (context == null) return false;

        if (data.woodCost > 0 && context.ResourceWallet != null)
        {
            if (!context.ResourceWallet.TrySpend(ResourceType.Wood, data.woodCost))
            {
                lastResultMessage = $"{data.displayName}: 목재 부족";
                return false;
            }
        }

        // 공유 게이트는 Eternal/Forever/Immortal만 잠근다 — Nika는 별도 전제라 안 건드린다.
        if (data.category != ChatUnlockCategory.Nika)
        {
            hasClaimedShared[playerId] = true;
        }

        SpawnResult(context, data);
        lastResultMessage = $"{data.displayName} 획득!";
        return true;
    }

    bool CanUnlock(int playerId, ChatUnlockData data, out string reason)
    {
        reason = "";

        if (data.category == ChatUnlockCategory.Nika)
        {
            if (!HasNikaPrerequisite(playerId))
            {
                reason = "전제 조건 미충족(니카 전용 퀘스트, 우리 게임엔 아직 없음)";
                return false;
            }
        }
        else
        {
            if (!HasChosenConquerorPath(playerId))
            {
                reason = "항법(패왕의 길) 미선택";
                return false;
            }
            if (HasClaimedSharedSlot(playerId))
            {
                reason = "이미 초월·불멸·영원 계열 중 하나를 받음(판당 1개)";
                return false;
            }
        }

        PlayerContext context = PlayerContext.Get(playerId);

        // Forever 전용(원작 "게임 클리어 누적 횟수"). 2026-09-05 11번(PersistentSave) 이후
        // CombineRecipe.requiredSaveCount와 같은 소스로 검사한다(CombineSystem.SaveCountConditionMet
        // 참고) — 세이브 시스템이 없다는 전제는 이제 깨졌다.
        if (data.requiredSaveCount > 0)
        {
            PersistentSave save = context?.PersistentSave;
            if (save == null || save.Data.cumulativeClearCount < data.requiredSaveCount)
            {
                reason = $"클리어 {data.requiredSaveCount}회 필요";
                return false;
            }
        }

        if (data.woodCost > 0 && (context?.ResourceWallet == null || context.ResourceWallet.Get(ResourceType.Wood) < data.woodCost))
        {
            reason = "목재 부족";
            return false;
        }

        return true;
    }

    void SpawnResult(PlayerContext context, ChatUnlockData data)
    {
        if (unitSpawner == null)
        {
            Debug.LogWarning($"ChatUnlockManager: unitSpawner가 비어있어 {data.displayName}을(를) 소환하지 못했습니다 " +
                              "(자원은 이미 소모됐습니다).", this);
            return;
        }

        LaneMarker lane = LaneMarker.Get(context.PlayerId);
        Vector3 position = lane != null ? lane.TakeSpawnPosition(data.result) : transform.position;
        unitSpawner.Spawn(data.result, position, context.PlayerId);
    }

    // 입력창은 GameChatBox(통합) 소관이다 — 여기는 판정·지급 결과 메시지만 들고 있는다.
    string lastResultMessage = "";
}
