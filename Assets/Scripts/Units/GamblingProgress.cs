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
        if (option != null) unlockedOptions.Add(option);
    }
}
