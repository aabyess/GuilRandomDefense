using System.Collections.Generic;
using UnityEngine;

// 아이템이 실제로 효과를 내게 하는 자리 — 2026-09-09 아이템 감사(④ 효과 층) 후속.
//
// 그 전까지 아이템은 **이름표와 설명문**이었다. `ItemData`에 수치 필드 자체가 없어서
// "아군 체력회복0.3증가" 같은 게 `tooltipText` 문자열로만 있었고, 8개 필드 중 코드가
// 읽는 건 `itemName` 하나뿐이었다. 구현담당2가 `ItemEffect`(kind+value)를 신설하고
// 원작 w3a에서 **필드로 직접 검증된 10건**을 채웠다. 이 컴포넌트가 그걸 집행한다.
//
// 왜 유닛에 밀어넣지 않고 여기서 관리하나 — 유닛은 계속 소환되고 팔린다. 아이템을
// 얻은 시점의 유닛에만 걸면 그 뒤에 뽑은 유닛은 혜택을 못 받는다. 그래서 인벤토리
// 두 개(아이템·유닛) 양쪽 변화를 듣고 매번 **다시 맞춘다**(reconcile).
//
// ⚠️ 10건 중 6건만 건다. 나머지 4건은 "안 넣은 게 아니라 **걸 자리가 없다**":
//   · HealthRegenPerSecond (I00M·I00W·I00Y) — 아군 유닛에는 HP 개념 자체가 없다.
//     HP·방어력은 EnemyData에만 있고, UnitData.hp는 HUD 표시에만 쓰인다
//     (UnitAttacker.cs의 ApplyToAlly 주석이 같은 이유로 ArmorBonus/HealOverTime을 막는다).
//     아군 피격이 생기기 전까지는 붙일 곳이 없다.
//   · StackDamagePercent (I005 포식한 영혼) — "1스택마다 영혼포식 데미지 1%"는 빅맘
//     전용 스킬에 붙는 값이지 범용 스탯이 아니다. 그 스킬이 우리에 생겨야 뜻이 통한다.
// 이걸 조용히 버리면 나중에 "값이 있는데 안 돈다"로 또 헤맨다([[no-numbers-means-three-things]]).
// 그래서 Awake에서 **한 번 경고로 남긴다.**
[RequireComponent(typeof(PlayerContext))]
public class ItemEffectApplier : MonoBehaviour
{
    PlayerContext context;

    ItemInventory subscribedItems;
    UnitInventory subscribedUnits;
    bool dirty = true;

    // 유닛마다 "우리가 건 값"을 기억한다 — 뺄 때 정확히 그 값을 빼야 다른 출처의 버프
    // (SupportShop 등)를 건드리지 않는다. UnitAttacker.RemoveAttackPowerBuff(v)가
    // 리스트에서 v 하나를 지우는 방식이라 값이 정확히 일치해야 한다.
    readonly Dictionary<UnitAttacker, float> appliedPower = new Dictionary<UnitAttacker, float>();
    readonly Dictionary<UnitAttacker, float> appliedSpeed = new Dictionary<UnitAttacker, float>();
    readonly List<UnitAttacker> reconcileScratch = new List<UnitAttacker>();

    void Awake()
    {
        context = GetComponent<PlayerContext>();
    }

    void OnDisable()
    {
        if (subscribedItems != null) subscribedItems.OnInventoryChanged -= MarkDirty;
        if (subscribedUnits != null) subscribedUnits.OnInventoryChanged -= MarkDirty;
        subscribedItems = null;
        subscribedUnits = null;

        // 붙여둔 버프를 회수한다 — 안 그러면 이 컴포넌트가 꺼진 뒤에도 배율이 남는다.
        ClearApplied();
    }

    void MarkDirty() => dirty = true;

    // 재계산은 서버 권위 상태다(ResourceWallet.Update·PirateQuestShop.Update와 같은 규칙).
    // 클라이언트가 따로 돌리면 배율이 서버와 어긋난다.
    void Update()
    {
        if (!GameAuthority.IsServer) return;

        ItemInventory items = context != null ? context.ItemInventory : null;
        UnitInventory units = context != null ? context.UnitInventory : null;

        if (items != subscribedItems)
        {
            if (subscribedItems != null) subscribedItems.OnInventoryChanged -= MarkDirty;
            subscribedItems = items;
            if (subscribedItems != null) subscribedItems.OnInventoryChanged += MarkDirty;
            dirty = true;
        }

        if (units != subscribedUnits)
        {
            if (subscribedUnits != null) subscribedUnits.OnInventoryChanged -= MarkDirty;
            subscribedUnits = units;
            if (subscribedUnits != null) subscribedUnits.OnInventoryChanged += MarkDirty;
            dirty = true;
        }

        if (!dirty) return;
        dirty = false;
        Reconcile();
    }

    /// <summary>
    /// 지금 들고 있는 아이템으로 배율을 다시 계산해, 이 플레이어의 모든 유닛에 맞춘다.
    ///
    /// 여러 아이템이 같은 종류를 주면 **곱**으로 쌓는다 — UnitAttacker의 버프 레지스트리가
    /// 원래 곱셈이고(AttackPowerMultiplier가 리스트를 전부 곱한다), SupportShop 버프도
    /// 그렇게 쌓인다. 아이템만 덧셈으로 두면 같은 화면에서 두 규칙이 섞인다.
    /// </summary>
    void Reconcile()
    {
        float power = 1f;
        float speed = 1f;
        float manaRegen = 0f;

        if (subscribedItems != null)
        {
            foreach (ItemData item in subscribedItems.Items)
            {
                if (item == null || item.effects == null) continue;

                foreach (ItemEffect effect in item.effects)
                {
                    if (effect == null) continue;

                    switch (effect.kind)
                    {
                        case ItemEffectKind.AttackPowerPercent:
                            power *= 1f + effect.value;
                            break;
                        case ItemEffectKind.AttackSpeedPercent:
                            speed *= 1f + effect.value;
                            break;
                        case ItemEffectKind.ManaRegenPerSecond:
                            manaRegen += effect.value;
                            break;

                        // 걸 자리가 없는 둘(위 주석). 조용히 버리면 나중에 "값이 있는데
                        // 안 돈다"로 헤매므로, 실제로 그 아이템을 손에 넣었을 때 한 번만
                        // 남긴다 — 안 들고 있으면 아무 소리도 안 난다.
                        case ItemEffectKind.HealthRegenPerSecond:
                        case ItemEffectKind.StackDamagePercent:
                            WarnUnsupported(item, effect.kind);
                            break;
                    }
                }
            }
        }

        if (context != null && context.ResourceWallet != null)
        {
            context.ResourceWallet.SetManaRegenBonus(manaRegen);
        }

        // 지금 살아 있는 유닛 목록을 먼저 뜬다(순회 중에 컬렉션이 바뀔 수 있다).
        reconcileScratch.Clear();
        if (subscribedUnits != null)
        {
            foreach (UnitIdentity identity in subscribedUnits.Members)
            {
                if (identity == null) continue;
                if (identity.TryGetComponent(out UnitAttacker attacker)) reconcileScratch.Add(attacker);
            }
        }

        // 사라진 유닛에 걸어둔 기록을 먼저 정리한다 — Dictionary에 죽은 참조가 쌓이면
        // 판이 길어질수록 늘어나기만 한다.
        PruneMissing(appliedPower, reconcileScratch);
        PruneMissing(appliedSpeed, reconcileScratch);

        foreach (UnitAttacker attacker in reconcileScratch)
        {
            Retune(attacker, appliedPower, power,
                   attacker.RemoveAttackPowerBuff, attacker.AddAttackPowerBuff);
            Retune(attacker, appliedSpeed, speed,
                   attacker.RemoveAttackSpeedBuff, attacker.AddAttackSpeedBuff);
        }
    }

    readonly HashSet<ItemEffectKind> warnedKinds = new HashSet<ItemEffectKind>();

    void WarnUnsupported(ItemData item, ItemEffectKind kind)
    {
        if (!warnedKinds.Add(kind)) return;

        string why = kind == ItemEffectKind.HealthRegenPerSecond
            ? "아군 유닛에 HP 개념이 없다(HP·방어력은 EnemyData에만 있고 UnitData.hp는 표시용)"
            : "빅맘 전용 스킬(영혼포식)에 붙는 값이라 범용 스탯이 아니다";

        Debug.LogWarning($"[아이템] {item.itemName}의 {kind} 효과를 적용하지 못했습니다 — {why}. " +
                         "값이 없어서가 아니라 걸 자리가 없어서입니다 " +
                         "(Docs/reference/ITEM_SYSTEM_AUDIT_2026-09-09.md ④).", this);
    }

    // 배율 1은 "아무 효과 없음"이라 아예 안 건다 — 버프 레지스트리에 1을 잔뜩 넣어봐야
    // 곱해도 그대로고, 개수만 세는 UI(AddBuff/RemoveBuff)에는 없는 버프가 뜬다.
    static void Retune(UnitAttacker attacker, Dictionary<UnitAttacker, float> applied, float target,
                       System.Action<float> remove, System.Action<float> add)
    {
        bool had = applied.TryGetValue(attacker, out float current);
        if (had && Mathf.Approximately(current, target)) return;

        if (had)
        {
            remove(current);
            applied.Remove(attacker);
        }

        if (target > 1f)
        {
            add(target);
            applied[attacker] = target;
        }
    }

    static void PruneMissing(Dictionary<UnitAttacker, float> applied, List<UnitAttacker> alive)
    {
        if (applied.Count == 0) return;

        List<UnitAttacker> gone = null;
        foreach (KeyValuePair<UnitAttacker, float> entry in applied)
        {
            if (entry.Key != null && alive.Contains(entry.Key)) continue;
            (gone ??= new List<UnitAttacker>()).Add(entry.Key);
        }

        if (gone == null) return;
        foreach (UnitAttacker attacker in gone) applied.Remove(attacker);
    }

    void ClearApplied()
    {
        foreach (KeyValuePair<UnitAttacker, float> entry in appliedPower)
            if (entry.Key != null) entry.Key.RemoveAttackPowerBuff(entry.Value);
        appliedPower.Clear();

        foreach (KeyValuePair<UnitAttacker, float> entry in appliedSpeed)
            if (entry.Key != null) entry.Key.RemoveAttackSpeedBuff(entry.Value);
        appliedSpeed.Clear();

        if (context != null && context.ResourceWallet != null)
            context.ResourceWallet.SetManaRegenBonus(0f);
    }
}
