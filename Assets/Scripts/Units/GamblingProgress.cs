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

    /// <summary>MP: 멀티 클라가 호스트의 값을 받아 적는다(NetPlayer) — 도박소 칸의 해금·남은 횟수 표시용 복제.
    /// 싱글·호스트는 부르지 않는다.</summary>
    public void ApplyReplicated(GamblingOptionData option, int uses, bool unlocked)
    {
        if (option == null) return;
        usesSoFar[option] = uses;
        if (unlocked) unlockedOptions.Add(option); else unlockedOptions.Remove(option);
    }
}
