using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 임시 디버그용, 정식 UI는 M7에서 교체 예정.
public class DebugHud : MonoBehaviour
{
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] UnitInventory unitInventory;
    [SerializeField] CombineSystem combineSystem;
    [SerializeField] RoundManager roundManager;

    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    UnitInventory Inventory => unitInventory != null ? unitInventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            TryCombineFirst();
        }
    }

    void TryCombineFirst()
    {
        if (combineSystem == null) return;

        List<UnitData> combinable = combineSystem.GetCombinableUnits();
        if (combinable.Count == 0) return;

        combineSystem.TryCombine(combinable[0]);
    }

    void OnGUI()
    {
        GoldWallet wallet = Wallet;
        UnitInventory inventory = Inventory;

        GUILayout.BeginArea(new Rect(10, 10, 320, 400));

        GUILayout.Label($"골드: {(wallet != null ? wallet.Gold.ToString() : "-")}");

        if (roundManager != null)
        {
            GUILayout.Label($"라운드: {roundManager.CurrentRound}  남은시간: {roundManager.RoundTimeLeft:F1}s");
            GUILayout.Label($"데스카운트: {roundManager.DeathCount}{(roundManager.IsGameOver ? " (게임 종료)" : "")}");
        }

        GUILayout.Label($"필드 몹 수: {EnemyDummy.Active.Count}");

        GUILayout.Space(10);
        GUILayout.Label("인벤토리 (C키: 조합 가능한 유닛 중 첫 번째 조합)");

        if (inventory != null && combineSystem != null)
        {
            List<UnitData> combinable = combineSystem.GetCombinableUnits();
            Dictionary<UnitData, int> counts = CountByUnit(inventory.Units);

            foreach (KeyValuePair<UnitData, int> entry in counts)
            {
                string label = $"{entry.Key.unitName} x{entry.Value}";
                if (combinable.Contains(entry.Key))
                {
                    label += "  [조합가능]";
                }

                GUILayout.Label(label);
            }
        }

        GUILayout.EndArea();
    }

    Dictionary<UnitData, int> CountByUnit(IReadOnlyList<UnitData> units)
    {
        Dictionary<UnitData, int> counts = new Dictionary<UnitData, int>();

        foreach (UnitData unit in units)
        {
            if (unit == null) continue;

            counts.TryGetValue(unit, out int count);
            counts[unit] = count + 1;
        }

        return counts;
    }
}
