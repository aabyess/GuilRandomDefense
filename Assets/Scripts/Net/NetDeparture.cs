using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 판 도중 나간 사람의 뒷정리(호스트 전용). 원작 `Gone1~4`(war3map.j L6136~6159, TriggerRegisterPlayerEventLeave)를 따른다:
///   1. 모두에게 「〇〇님이 게임에서 나가셨습니다」
///   2. 그 플레이어 소유 유닛을 맵 전체에서 제거(RemoveUnit — 킬이 아니라 보상 없음)
///   3. 그 플레이어 레인(life_zone) 안의 적 제거
///   4. udg_PlayerDeath[n] += 1 — 우리 쪽은 PlayerContext.MarkDead(패배와 같은 표식: 그 레인은 더 스폰·보상 안 함)
///   5. R01G +1 — 스토리·퀘스트 미니보스·크립 최대 체력 −10%/레벨(최대 5). EnemyDummy.RaiseStoryHpReduction
/// </summary>
public static class NetDeparture
{
    public static void Apply(int slot, string displayName)
    {
        if (!GameAuthority.IsServer) return;

        PlayerContext context = PlayerContext.Get(slot);
        if (context == null)
        {
            Debug.Log($"[MP] 퇴장 정리: 슬롯 {slot}의 PlayerContext가 없습니다(게임 씬 전).");
            return;
        }

        // 1. 알림 — 원작 색(주황 이름 · 빨강 문구). 남은 사람에게만 뜬다(나간 사람 알림은 넘길 곳이 없다).
        string message = $"<color=#FF8200>{displayName}</color> <color=#FF0000>님이 게임에서 나가셨습니다. 스토리 체력이 인원수에 맞게 감소합니다.</color>";
        foreach (PlayerContext other in PlayerContext.Occupied)
            if (other.PlayerId != slot) PlayerNotification.Show(other.PlayerId, message, 5f);

        // 2. 소유 유닛 전부 — 인벤토리(창고에 있는 것 포함)와 필드 위습.
        int units = 0, wisps = 0, enemies = 0;
        if (context.UnitInventory != null)
        {
            foreach (UnitIdentity unit in new List<UnitIdentity>(context.UnitInventory.Members))
                if (unit != null) { unit.Consume(); units++; }
        }
        foreach (Wisp wisp in new List<Wisp>(Wisp.Active))
        {
            // MarkConsumed는 안 부른다 — 소모 이벤트(막간 문 등)는 포탈에 넣었을 때의 뜻이지 퇴장 정리가 아니다.
            if (wisp != null && wisp.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId == slot)
            { Object.Destroy(wisp.gameObject); wisps++; }
        }

        // 3. 그 레인의 적 — TakeDamage를 안 거친다(킬이 아니라 보상이 나가면 안 된다. RoundManager.HandlePlayerDefeated와 같은 이유).
        foreach (EnemyDummy enemy in new List<EnemyDummy>(EnemyDummy.Active))
            if (enemy != null && enemy.LaneIndex == slot) { Object.Destroy(enemy.gameObject); enemies++; }

        // 4. 패배와 같은 표식.
        context.MarkDead();

        // 5. R01G +1(원작은 Player(5)에 업그레이드 — 대상 = 스토리·퀘스트 미니보스·크립, 서 있는 것도 즉시).
        var before = new List<(EnemyDummy enemy, float max, float hp)>();
        foreach (EnemyDummy enemy in EnemyDummy.Active)
            if (enemy != null && enemy.IsStoryHpTarget) before.Add((enemy, enemy.MaxHp, enemy.Hp));
        EnemyDummy.RaiseStoryHpReduction();
        foreach (var entry in before)
            if (entry.enemy != null)
                Debug.Log($"[MP] 스토리 체력 감소: {entry.enemy.name} 최대 {entry.max:F0}→{entry.enemy.MaxHp:F0}, 현재 {entry.hp:F0}→{entry.enemy.Hp:F0}");

        Debug.Log($"[MP] 퇴장 정리: 슬롯 {slot}({displayName}) — 유닛 {units} · 위습 {wisps} · 레인 적 {enemies} 제거, 사망 표식, 스토리 체력 감소 레벨 {EnemyDummy.StoryHpReductionLevel}");
    }
}
