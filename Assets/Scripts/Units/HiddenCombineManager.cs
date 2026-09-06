using System.Collections.Generic;
using UnityEngine;

// 히든 등급 23종 — 채팅 문구가 "확인 트리거" 노릇을 하는 재료 조합(HiddenCombineData 참고).
// PM 지시(2026-09-05): CombineSystem(우리 UI 클릭 조합)에 합치지 않는다 — 원작은 입력
// 경로 자체가 다르다(유닛을 겹쳐두는 게 아니라 채팅으로 확인한다). 재료를 찾고 소모하는
// 알고리즘만 CombineSystem.TryTakeUnit과 같은 모양으로 새로 짰다(그 파일은 안 건드림 —
// 구현담당1 소유일 수 있어 겹치지 않으려 최소 중복을 선택했다).
public class HiddenCombineManager : MonoBehaviour
{
    [SerializeField] List<HiddenCombineData> combines = new List<HiddenCombineData>();
    [SerializeField] UnitSpawner unitSpawner;

    readonly List<UnitIdentity> pool = new List<UnitIdentity>();
    readonly List<UnitIdentity> toConsume = new List<UnitIdentity>();

    /// <summary>GameChatBox(통합 입력창)가 이걸 쓴다. <paramref name="message"/>는 문구가
    /// 이 매니저 소관으로 인식됐을 때만 채워진다(성공이든 실패든) — null이면 이 매니저는 이
    /// 문구를 모른다는 뜻이라, 호출부가 다음 판정기(ChatUnlockManager)로 넘겨야 한다.</summary>
    public bool TryUnlockByPhrase(int playerId, string text, out string message)
    {
        HiddenCombineData match = FindByPhrase(text);
        if (match == null) { message = null; return false; }

        bool unlocked = TryUnlock(playerId, match);
        message = lastResultMessage;
        return unlocked;
    }

    public string LastResultMessage => lastResultMessage;

    HiddenCombineData FindByPhrase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string trimmed = text.Trim();

        foreach (HiddenCombineData data in combines)
        {
            if (data == null) continue;
            if (string.Equals(data.phraseEnglish, trimmed, System.StringComparison.OrdinalIgnoreCase)) return data;
            if (string.Equals(data.phraseKorean, trimmed, System.StringComparison.OrdinalIgnoreCase)) return data;
        }
        return null;
    }

    public bool TryUnlock(int playerId, HiddenCombineData data)
    {
        if (!GameAuthority.IsServer) return false;
        if (data == null || data.result == null) return false;

        PlayerContext context = PlayerContext.Get(playerId);
        UnitInventory inventory = context?.UnitInventory;
        if (inventory == null)
        {
            lastResultMessage = $"{data.displayName}: 인벤토리를 찾지 못함";
            return false;
        }

        pool.Clear();
        pool.AddRange(inventory.Members);

        toConsume.Clear();
        if (!TryPlanIngredients(data, pool, toConsume))
        {
            lastResultMessage = $"{data.displayName}: 재료 부족";
            return false;
        }

        // 전부 확보된 뒤에만 소모한다 — 중간에 실패해서 일부만 사라지는 일이 없게
        // (CombineSystem.TryCombine과 같은 순서 원칙).
        foreach (UnitIdentity unit in toConsume)
        {
            unit.Consume();
        }

        Vector3 position = LaneMarker.Get(playerId)?.TakeSpawnPosition(data.result) ?? transform.position;
        unitSpawner?.Spawn(data.result, position, playerId);

        // 2026-09-06 — Hidden_Aokiji(히든_성탄.asset, +2)류 Damage_level_Fixed 누적.
        // 항법 "패왕의길"과 같은 카운터(DamageLevelFixedState)에 더해 합산되게 한다 —
        // 각자 따로 카운터를 만들면 원작 최대(+10 = 항법2+히든2+2+4)가 안 나온다.
        context.DamageLevelFixedState?.Add(data.damageLevelFixedBonus);

        lastResultMessage = $"{data.displayName} 조합 성공!";
        return true;
    }

    // 재료 슬롯마다 unit이 비어 있으면(사장님 배정 전) 그 슬롯은 영원히 못 채운다 — 다른
    // 유닛으로 조용히 대신 채우지 않는다. 그래서 지금은 23개 전부 재료가 비어 있어
    // 조합이 항상 실패한다(의도된 상태, 값이 채워지면 자동으로 살아난다).
    static bool TryPlanIngredients(HiddenCombineData data, List<UnitIdentity> pool, List<UnitIdentity> takenOut)
    {
        if (data.ingredients == null) return true;

        foreach (HiddenCombineIngredient ingredient in data.ingredients)
        {
            if (ingredient == null || ingredient.unit == null) return false;
            if (!TryTake(pool, ingredient.unit, Mathf.Max(1, ingredient.count), takenOut)) return false;
        }
        return true;
    }

    // CombineSystem.TryTakeUnit과 같은 모양(원작 GroupPickRandomUnit과 달리 순서대로 집지만,
    // "어느 걸 없앨지"가 필요하다는 점은 같다) — 새로 필요해질 때마다 또 만들지 않도록
    // 여기 하나만 둔다.
    static bool TryTake(List<UnitIdentity> pool, UnitData target, int count, List<UnitIdentity> takenOut)
    {
        int taken = 0;
        for (int i = 0; i < pool.Count && taken < count; )
        {
            if (pool[i] != null && pool[i].Data == target)
            {
                takenOut.Add(pool[i]);
                pool.RemoveAt(i);
                taken++;
            }
            else
            {
                i++;
            }
        }
        return taken >= count;
    }

    // 입력창은 GameChatBox(통합) 소관이다(사장님 지시 2026-09-05: 채팅 코드와 히든 조합을
    // 같은 입력창 하나로) — 여기는 판정 결과 메시지만 들고 있는다.
    string lastResultMessage = "";
}
