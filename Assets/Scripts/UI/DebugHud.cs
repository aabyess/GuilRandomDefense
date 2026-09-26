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
    [SerializeField] Warehouse warehouse;
    [SerializeField] SelectionManager selectionManager;

    SelectionManager Selection => selectionManager != null
        ? selectionManager
        : selectionManager = FindFirstObjectByType<SelectionManager>();

    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    UnitInventory Inventory => unitInventory != null ? unitInventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;
    Warehouse Warehouse => warehouse != null ? warehouse : PlayerContext.Local != null ? PlayerContext.Local.Warehouse : null;

    // 정식 HUD가 골드·라운드·적 수·인벤토리를 모두 보여주므로 기본은 접어둔다.
    // 지우지 않고 남기는 이유: 유닛 스탯이 적용됐는지, 사거리 안인지 같은 건
    // 화면만 봐서는 알 수 없고, 실제로 이 패널로 여러 번 원인을 찾았다.
    bool visible = false;

    // OnGUI는 화면 픽셀 좌표(원점 **좌상단**)이고 uGUI는 원점이 좌하단이라 좌표계가 뒤집혀 있다.
    //
    // 🔴 2026-09-23: 처음엔 "GameHud 상단 바가 화면 위 5%"라는 **숫자를 베껴 와서** 그 아래에
    //    뒀는데 사장님 화면에서 여전히 겹쳤다. 베낀 숫자는 상대가 바뀌면 조용히 틀린다 —
    //    그래서 이제 **GameHud의 실제 사각형을 런타임에 재서** 그 아래로 내려간다.
    //    한 번 잰 값을 캐시하고, 처음 한 번은 두 사각형 수치를 로그로 남긴다(화면 캡처 없이
    //    겹침 여부를 수치로 확인할 수 있게 — PM 지시).
    const float LabelWidth = 200f;
    const float LabelHeight = 20f;
    const float Margin = 8f;

    float cachedTopOffset = -1f;
    int cachedForHeight = -1;

    float TopOffset
    {
        get
        {
            if (cachedTopOffset >= 0f && cachedForHeight == Screen.height) return cachedTopOffset;
            cachedForHeight = Screen.height;
            cachedTopOffset = MeasureTopOffset();
            return cachedTopOffset;
        }
    }

    /// <summary>
    /// 화면 왼쪽 위에서 GameHud가 차지한 맨 아래 지점(OnGUI 좌표) + 여백.
    /// GameHud 캔버스의 자식 패널들을 실제로 재서, 내 라벨이 놓일 가로 띠와 겹치는 것만 본다.
    /// </summary>
    float MeasureTopOffset()
    {
        float bottom = 0f;
        string hit = "없음";

        GameHud hud = FindFirstObjectByType<GameHud>();
        if (hud != null)
        {
            Vector3[] corners = new Vector3[4];
            foreach (RectTransform panel in hud.GetComponentsInChildren<RectTransform>(false))
            {
                if (panel == hud.transform) continue;
                panel.GetWorldCorners(corners);

                // ScreenSpaceOverlay 캔버스는 월드 좌표가 곧 화면 픽셀이다. y만 뒤집어 맞춘다.
                float left = corners[0].x;
                float right = corners[2].x;
                float guiTop = Screen.height - corners[1].y;
                float guiBottom = Screen.height - corners[0].y;

                // 내 라벨이 설 자리(왼쪽 위 가로 띠)와 x가 겹치는 것만 센다.
                if (right < 0f || left > LabelWidth + Margin) continue;
                if (guiBottom <= 0f) continue;
                // 화면 위쪽 1/3 밖까지 내려가는 큰 패널(하단 바 등)은 대상이 아니다.
                if (guiTop > Screen.height / 3f) continue;

                if (guiBottom > bottom) { bottom = guiBottom; hit = panel.name; }
            }
        }

        float offset = bottom + Margin;
        Debug.Log($"[디버그HUD] 라벨 자리 계산: 화면 {Screen.width}×{Screen.height}, " +
                  $"왼쪽 위에서 GameHud가 내려온 끝 {bottom:F1}px(가장 아래 패널 '{hit}') " +
                  $"→ 라벨 y {offset:F1}~{offset + LabelHeight:F1}. 겹치면 이 값이 0에 가깝다.");
        return offset;
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        // 2026-09-25 베타 빌드: 디버그 창(F1)과 첫 조합 치트(F2)는 에디터·개발 빌드에서만.
        if (!Application.isEditor && !Debug.isDebugBuild) { visible = false; return; }

        // 정식 HUD가 골드·라운드·적 수를 이미 보여준다. 화면을 덮는 게 거슬릴 때 F1로 접는다.
        if (Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;

        // V는 게임 명령(모으기)이라 겹친다 — F1 옆인 F2로 옮겼다. 디버그 조합은 이 창이
        // 떠 있을 때만 받는다 — 안 그러면 유닛을 모으려다 조합이 같이 돌아간다.
        if (visible && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            TryCombineFirst();
        }
    }

    // OnGUI는 한 프레임에 두 번(Layout/Repaint) 불리고 그 안에서 두 곳이 이 목록을 쓴다.
    // 그대로 두면 프레임당 4번, 레시피 199개를 매번 훑는다.
    const float RecipeCacheInterval = 0.4f;

    readonly List<CombineRecipe> cachedRecipes = new List<CombineRecipe>();
    float nextRecipeCacheTime;

    List<CombineRecipe> CachedRecipes()
    {
        if (combineSystem == null) return cachedRecipes;
        if (Time.unscaledTime < nextRecipeCacheTime) return cachedRecipes;

        nextRecipeCacheTime = Time.unscaledTime + RecipeCacheInterval;

        // GetAvailableRecipes는 재사용 버퍼를 돌려준다 — 들고 있으려면 복사해야 한다.
        cachedRecipes.Clear();
        cachedRecipes.AddRange(combineSystem.GetAvailableRecipes());
        return cachedRecipes;
    }

    void TryCombineFirst()
    {
        if (combineSystem == null) return;

        List<CombineRecipe> available = CachedRecipes();
        if (available.Count == 0) return;

        combineSystem.TryCombine(available[0]);
    }

    RtsCameraController cameraRef;

    RtsCameraController CameraRef => cameraRef != null
        ? cameraRef
        : cameraRef = FindFirstObjectByType<RtsCameraController>();

    void OnGUI()
    {
        // 릴리스에서 안 먹는 키 안내(「F1: 디버그 정보」)가 보이던 것 — Update의 막기와 같은 조건을 OnGUI에도(PM 09-26, v1.1.0 캡처).
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        if (!visible)
        {
            GUI.Label(new Rect(10, TopOffset, LabelWidth, LabelHeight), "F1: 디버그 정보");
            return;
        }

        GoldWallet wallet = Wallet;
        UnitInventory inventory = Inventory;

        GUILayout.BeginArea(new Rect(10, TopOffset, 320, 400));

        GUILayout.Label($"골드: {(wallet != null ? wallet.Gold.ToString() : "-")}");

        if (roundManager != null)
        {
            GUILayout.Label($"라운드: {roundManager.CurrentRound}  남은시간: {roundManager.RoundTimeLeft:F1}s");

            // 데스카운트가 레인(플레이어)마다 따로 돌아서(2026-09-03) 전역 숫자 하나가 없다 —
            // 로컬 플레이어 것만 보여준다.
            PlayerContext local = PlayerContext.Local;
            string deathLabel = local != null && local.IsDead
                ? "사망"
                : local != null
                    ? roundManager.DeathCountFor(local.PlayerId).ToString()
                    : "-";
            GUILayout.Label($"내 데스카운트: {deathLabel}{(roundManager.IsGameOver ? " (게임 종료)" : "")}");
        }

        GUILayout.Label($"필드 몹 수: {EnemyDummy.Active.Count}");

        // 방향키와 가장자리 밀기가 정말 같은 속도인지 확인용. 둘 다 같은 CurrentSpeed로 이어져야 한다.
        if (CameraRef != null)
        {
            GUILayout.Label($"카메라 높이 {CameraRef.transform.position.y:F0} " +
                            $"(높이 배율 {CameraRef.CurrentSpeed / Mathf.Max(0.01f, CameraRef.KeyboardAxis.magnitude + CameraRef.EdgeAxis.magnitude):F0} 기준)");
            GUILayout.Label($"카메라 입력  방향키 {CameraRef.KeyboardAxis}  가장자리 {CameraRef.EdgeAxis}");
            GUILayout.Label($"카메라 속도  {CameraRef.CurrentSpeed:F0}/초");
        }

        Warehouse warehouseRef = Warehouse;
        GUILayout.Label($"창고: {(warehouseRef != null ? warehouseRef.Stored.Count.ToString() : "-")}개");

        GUILayout.Space(10);
        GUILayout.Label("인벤토리");

        if (inventory != null)
        {
            Dictionary<UnitData, int> counts = CountByUnit(inventory.Units);

            foreach (KeyValuePair<UnitData, int> entry in counts)
            {
                GUILayout.Label($"{entry.Key.unitName} x{entry.Value}");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("조합 가능한 레시피 (F1 켠 상태에서 V: 첫 번째 조합)");

        if (combineSystem != null)
        {
            foreach (CombineRecipe recipe in CachedRecipes())
            {
                string resultName = recipe.result != null ? recipe.result.unitName : "?";
                GUILayout.Label($"[{recipe.commandId}] → {resultName}");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("보유 위습 (G키: 테스트용 위습 1개 지급)");

        foreach (KeyValuePair<string, int> entry in CountOwnedWisps())
        {
            GUILayout.Label($"{entry.Key} x{entry.Value}");
        }

        GUILayout.EndArea();

        DrawSelectionPanel();
    }

    // 선택한 대상의 실제 런타임 스탯을 보여준다. 스탯이 붙지 않았거나 사거리가 모자라서
    // 적이 안 죽는 경우를 화면에서 바로 구분하기 위한 임시 패널이다.
    void DrawSelectionPanel()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 330, TopOffset, 320, 300));

        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count == 0)
        {
            GUILayout.Label("선택된 대상 없음 (좌클릭으로 선택)");
        }
        else
        {
            Selectable first = selection.Selected[0];
            if (first == null)
            {
                GUILayout.Label("선택 대상이 사라졌습니다");
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label($"선택: {first.name}  (총 {selection.Selected.Count}개)");

            if (first.TryGetComponent(out UnitAttacker attacker))
            {
                float distance = attacker.DistanceToClosestEnemy();
                GUILayout.Label($"공격력 {attacker.AttackDamage}  사거리 {attacker.AttackRange}  간격 {attacker.AttackInterval:F2}s");
                GUILayout.Label(float.IsPositiveInfinity(distance)
                    ? "가장 가까운 적: 없음"
                    : $"가장 가까운 적: {distance:F1}m  →  {(distance <= attacker.AttackRange ? "사거리 안 (때리는 중)" : "사거리 밖")}");

                if (attacker.AttackDamage <= 0f)
                    GUILayout.Label("⚠️ 공격력이 0이라 절대 죽지 않습니다");
            }
            else
            {
                GUILayout.Label("⚠️ UnitAttacker가 없습니다 — 위습이거나 공격 못 하는 오브젝트");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("필드 몹 체력 (최대 8마리)");
        int shown = 0;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (shown++ >= 8) break;
            GUILayout.Label($"  hp {enemy.Hp:F1}");
        }

        GUILayout.EndArea();
    }

    // 위습은 인벤토리가 아니라 필드에 존재하는 유닛이라, 씬에서 내 소유 위습을 직접 세어 표시한다.
    // OnGUI는 한 프레임에 두 번(Layout/Repaint) 불리므로, 여기서 씬 전체를 훑으면 프레임당 2회 전수 조사가 된다.
    // 디버그 표시라 초당 4회면 충분하다.
    const float WispCountInterval = 0.25f;

    readonly Dictionary<string, int> wispCounts = new Dictionary<string, int>();
    float nextWispCountTime;

    Dictionary<string, int> CountOwnedWisps()
    {
        if (Time.unscaledTime < nextWispCountTime) return wispCounts;
        nextWispCountTime = Time.unscaledTime + WispCountInterval;

        Dictionary<string, int> counts = wispCounts;
        counts.Clear();
        int localPlayerId = LocalPlayer.LocalPlayerId;

        foreach (Wisp wisp in FindObjectsByType<Wisp>(FindObjectsSortMode.None))
        {
            if (wisp.Data == null) continue;
            if (wisp.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId != localPlayerId) continue;

            string name = wisp.Data.wispName;
            counts.TryGetValue(name, out int count);
            counts[name] = count + 1;
        }

        return counts;
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
