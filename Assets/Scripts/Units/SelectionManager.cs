using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [SerializeField] float dragThreshold = 4f;
    [SerializeField] int maxSelection = 12;   // 원작·워크3와 동일. 하단 카드 칸 수와 맞춘다.
    [SerializeField] Color boxColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
    [SerializeField] Color boxBorderColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

    Camera cam;
    readonly List<Selectable> selected = new List<Selectable>();

    public IReadOnlyList<Selectable> Selected => selected;

    Vector2 dragStart;
    bool isDragging;

    // 2026-09-26 A 공격: A(또는 명령칸 「공격」)를 누르면 다음 좌클릭이 공격 대상이 된다.
    // 적을 찍으면 그 적을 치고, 땅을 찍으면 공격 이동. 우클릭·Esc로 취소.
    // 2026-09-29 M 이동(워크3 콘솔 개편): 같은 틀로 다음 좌클릭 땅이 이동 목적지. 커서만 초록 화살표.
    enum TargetMode { None, Attack, Move, Patrol }
    TargetMode targeting;
    int attackCancelFrame = -1;
    // 우클릭 취소는 같은 프레임의 우클릭 이동(UnitMover)도 막아야 한다 — 스크립트 실행 순서와 상관없이.
    //    이름은 옛것 그대로지만 M 이동 대기도 포함한다(둘 다 우클릭 = 취소).
    public bool IsAttackTargeting => targeting != TargetMode.None || attackCancelFrame == Time.frameCount;
    // A(공격) 대기 중인가 — 사거리 원을 진하게 그리는 쪽(AttackRangeIndicator)이 읽는다. 이동(M) 대기는 아니다.
    public static bool AttackModeActive { get; private set; }
    const float AttackPickTolerancePixels = 36f;
    // 좌클릭 살펴보기(적 정보)용 — 공격 대상 고르기보다 넉넉히. 1080 기준 픽셀, 화면 높이에 비례해 늘린다.
    const float InspectPickTolerancePixels = 56f;
    bool leftButtonHeld;
    bool ignoreCurrentPress;

    // 더블클릭 / Ctrl(맥 Cmd)+클릭 = 화면 안 같은 종류 전부 선택 — 워크3 기본 조작(2026-09-26 사장님 「둘 다 ㄱㄱ」).
    //    흔함은 한 점에 완전히 겹쳐 서서(원작도 충돌 0) 몇 기인지 안 보였다. 같은 종류를 다 고르면 아래 카드가 개수만큼 뜬다.
    //    더블클릭은 **같은 개체**가 아니라 **같은 종류**로 본다 — 겹친 무더기는 두 번째 클릭이 다른 개체에 맞기 쉽다.
    const float DoubleClickSeconds = 0.35f;
    const float DoubleClickPixels = 8f;
    Object lastClickType;
    float lastClickTime = -1f;
    Vector2 lastClickScreen;
    Texture2D boxTexture;

    void Awake()
    {
        cam = Camera.main;
        boxTexture = Texture2D.whiteTexture;
    }

    void Update()
    {
        PruneDestroyed();

        // 채팅 입력 중엔 클릭·드래그·명령 단축키를 전부 죽인다 — Input System은 텍스트
        // 필드 포커스와 무관하게 Keyboard.current를 그대로 읽어서, 안 막으면 채팅으로
        // "v"를 치는 순간 유닛이 모인다(ChatInputGate.cs 참고, 사장님 지시 2026-09-05).
        if (ChatInputGate.IsOpen) { targeting = TargetMode.None; return; }

        HandleCommandKeys();
        HandleControlGroups();

        if (Mouse.current == null || cam == null) return;

        if (targeting != TargetMode.None && HandleAttackTargeting()) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 하단 HUD 등 uGUI 위에서 누른 클릭은 월드 선택으로 취급하지 않는다.
            ignoreCurrentPress = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            // 상점 칸(보물찾기 탐색 등)의 지점을 찍는 클릭은 선택이 아니다 — 찍은 뒤에도 건물 선택이 남게.
            // GameHud가 먼저 돌면 대기가 이미 풀렸으므로 「이 프레임에 소비됨」도 같이 본다.
            if (GameHud.ShopTargetingPending || GameHud.ShopClickConsumedFrame == Time.frameCount)
                ignoreCurrentPress = true;

            if (!ignoreCurrentPress)
            {
                dragStart = Mouse.current.position.ReadValue();
                isDragging = false;
            }
            leftButtonHeld = !ignoreCurrentPress;
        }

        if (leftButtonHeld && !isDragging)
        {
            Vector2 current = Mouse.current.position.ReadValue();
            if (Vector2.Distance(dragStart, current) >= dragThreshold)
                isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (!ignoreCurrentPress)
            {
                if (isDragging)
                    SelectInBox(dragStart, Mouse.current.position.ReadValue());
                else
                    TrySelectAtCursor();
            }

            isDragging = false;
            leftButtonHeld = false;
            ignoreCurrentPress = false;
        }
    }

    // ── 동맹 보기 전용 선택(10-08): 남의 유닛을 누르면 그 하나만 「보기 전용」으로 고른다 — 초상·정보창·툴팁은 뜨고 명령은 안 나간다.
    Selectable viewOnlyPick;
    public bool IsViewOnlySelection => selected.Count == 1 && selected[0] != null && selected[0] == viewOnlyPick && !AllianceShare.CanControlLocal(selected[0].gameObject);

    /// <summary>보기 전용이면 이유를 알리고 true — 명령 진입점(단축키·명령 카드)이 부른다.</summary>
    public bool BlockedByViewOnly()
    {
        if (!IsViewOnlySelection) return false;
        PlayerNotification.Show(LocalPlayer.LocalPlayerId, AllianceShare.ViewOnlyMessage, 2f);
        return true;
    }

    /// <summary>A 키·명령칸 「공격」. 싸울 수 있는 유닛이 선택돼 있을 때만 들어간다.</summary>
    public void BeginAttackTargeting()
    {
        if (BlockedByViewOnly()) return;
        foreach (Selectable s in selected)
        {
            // MP: 클라의 유닛 겉모습은 UnitCombat을 떼고 UnitIdentity만 남긴다(NetReplicaBuilder.KeepTypes) — 그것도 싸우는 유닛이다.
            //     이게 없으면 친구는 A·「공격」 칸으로 대기에 못 들어갔다(09-29 두 창 실측). 명령은 UnitCommands가 호스트로 보낸다.
            if (s != null && (s.GetComponent<UnitCombat>() != null
                              || (!GameAuthority.IsServer && s.GetComponent<UnitIdentity>() != null)))
            {
                targeting = TargetMode.Attack;
                return;
            }
        }
    }

    /// <summary>P 키·명령칸 「반복」. 싸울 수 있는 유닛이 선택돼 있을 때만 들어간다.</summary>
    public void BeginPatrolTargeting()
    {
        if (BlockedByViewOnly()) return;
        foreach (Selectable s in selected)
            if (s != null && (s.GetComponent<UnitCombat>() != null || (!GameAuthority.IsServer && s.GetComponent<UnitIdentity>() != null)))
            {
                targeting = TargetMode.Patrol;
                return;
            }
    }

    /// <summary>M 키·명령칸 「이동」. 움직일 수 있는 유닛(UnitMover)이 선택돼 있을 때만 들어간다.</summary>
    public void BeginMoveTargeting()
    {
        if (BlockedByViewOnly()) return;
        foreach (Selectable s in selected)
        {
            if (s != null && s.GetComponent<UnitMover>() != null)
            {
                targeting = TargetMode.Move;
                return;
            }
        }
    }

    // 공격 대상 고르는 중. 이번 프레임 입력을 여기서 다 썼으면 true(선택·드래그로 넘기지 않는다).
    bool HandleAttackTargeting()
    {
        if (selected.Count == 0 || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || Mouse.current.rightButton.wasPressedThisFrame)
        {
            targeting = TargetMode.None;
            attackCancelFrame = Time.frameCount;
            return false;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame) return false;
        // HUD를 누르면 대기를 푼다(롤처럼) — 명령칸 다른 명령·메뉴 버튼은 그대로 눌린다. 「공격」칸은 떼는 순간 다시 들어온다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            targeting = TargetMode.None;
            return false;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        if (targeting == TargetMode.Move)
        {
            // 우클릭 이동(UnitMover.TryMoveToCursor)과 같은 본체를 탄다 — 적 위를 찍어도 공격이 아니라 그 자리로 간다(워크3 이동).
            if (WorldPick.TryHitGround(cam, screen, out RaycastHit moveHit))
            {
                int moved = 0;
                foreach (Selectable s in selected)
                {
                    if (s == null || !s.TryGetComponent(out UnitMover mover)) continue;
                    if (!GameAuthority.IsServer) NetCommands.RequestMove(mover, moveHit.point);   // MP: 클라=요청
                    else mover.MoveToGroundPoint(moveHit.point, moveHit.collider.name);
                    moved++;
                }
                Debug.Log($"[명령] 이동 — 유닛 {moved}기가 {moveHit.point}로 갑니다.");
            }
        }
        else if (targeting == TargetMode.Patrol)
        {
            if (WorldPick.TryHitGround(cam, screen, out RaycastHit patrolHit))
            {
                int n = UnitCommands.Patrol(selected, patrolHit.point);
                Debug.Log($"[명령] 반복 — 유닛 {n}기가 지금 자리와 {patrolHit.point} 사이를 오갑니다.");
            }
        }
        else
        {
            EnemyDummy enemy = WorldPick.TryPickEnemy(cam, screen, AttackPickTolerancePixels);
            if (enemy != null)
            {
                int n = UnitCommands.AttackTarget(selected, enemy);
                Debug.Log($"[명령] 공격 — 유닛 {n}기가 {enemy.name}을(를) 칩니다.");
            }
            else if (WorldPick.TryHitGround(cam, screen, out RaycastHit hit))
            {
                int n = UnitCommands.AttackMove(selected, hit.point);
                Debug.Log($"[명령] 공격 이동 — 유닛 {n}기가 {hit.point}로 가며 싸웁니다.");
            }
        }

        targeting = TargetMode.None;
        // 이 누름은 명령으로 썼다 — 뗄 때 선택이 바뀌지 않게 막는다.
        ignoreCurrentPress = true;
        leftButtonHeld = false;
        isDragging = false;
        return true;
    }

    // 명령 단축키. 선택 목록을 들고 있는 쪽에서 받는 게 자연스럽다 —
    // 우하단 명령 카드도 같은 UnitCommands를 부른다.
    // 2026-09-26: 워크3 배치 — A 공격 · S 정지 · H 홀드 · V 모으기 · C 정렬.
    void HandleCommandKeys()
    {
        if (Keyboard.current == null || selected.Count == 0) return;
        // 상점 건물을 고른 동안은 같은 글자가 상점 칸 단축키다(GameHud.RefreshShopHotkeys) — 유닛 명령으로 안 받는다.
        if (GameHud.ShopSelected) return;
        if (IsViewOnlySelection) return;   // 보기 전용: 단축키 명령 없음(명령 카드에서 누르면 알림)

        if (Keyboard.current.aKey.wasPressedThisFrame)
            BeginAttackTargeting();

        if (Keyboard.current.mKey.wasPressedThisFrame)
            BeginMoveTargeting();

        if (Keyboard.current.pKey.wasPressedThisFrame)
            BeginPatrolTargeting();

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            targeting = TargetMode.None;
            int stopped = UnitCommands.Stop(selected);
            if (stopped > 0) Debug.Log($"[명령] 정지 — 유닛 {stopped}기가 멈췄습니다.");
        }

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            int moved = UnitCommands.Gather(selected);
            if (moved > 0) Debug.Log($"[명령] 모으기 — 유닛 {moved}기를 불러 모았습니다.");
        }

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            targeting = TargetMode.None;
            int held = UnitCommands.Hold(selected);
            if (held > 0) Debug.Log($"[명령] 홀드 — 유닛 {held}기가 자리를 지킵니다(사거리 안의 적은 칩니다).");
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            int sent = UnitCommands.SendToPen(selected);
            if (sent > 0) Debug.Log($"[명령] 정렬 — 유닛 {sent}기를 우리로 보냈습니다.");
        }
    }

    // 선택된 채로 파괴되는 대상이 있다(포탈에 들어간 위습, 죽은 유닛). 목록에 남겨두면
    // 이 목록을 읽는 쪽이 파괴된 오브젝트에 접근해 MissingReferenceException을 낸다.
    void PruneDestroyed()
    {
        for (int i = selected.Count - 1; i >= 0; i--)
            if (selected[i] == null)
                selected.RemoveAt(i);
        // 동맹 공유가 꺼지면(10-08) 그 순간 조종하던 남의 유닛은 선택에서 빠진다 — 보기 전용으로 고른 것만 남는다.
        for (int i = selected.Count - 1; i >= 0; i--)
            if (selected[i] != viewOnlyPick && !AllianceShare.CanControlLocal(selected[i].gameObject))
            {
                selected[i].SetSelected(false);
                selected.RemoveAt(i);
            }
    }

    void TrySelectAtCursor()
    {
        Selectable hitSelectable = null;
        if (WorldPick.TryHit(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
        {
            hit.collider.TryGetComponent(out hitSelectable);
            // 위습이 칸마다 같은 자리에 겹쳐 나온다(사장님 10-08) — 맨 위가 남의 것이어도 같은 자리의 「내 것(또는 공유 받은 것)」을 우선 고른다.
            if (hitSelectable != null && !AllianceShare.CanControlLocal(hitSelectable.gameObject) && WorldPick.TryHitControllable(cam, Mouse.current.position.ReadValue(), hit.distance, out RaycastHit mine))
            {
                mine.collider.TryGetComponent(out hitSelectable);
                hit = mine;
            }
        }

        ClearSelection();
        InspectTarget.Clear();

        if (hitSelectable == null)
        {
            // 적·조합표 인형은 조작은 못 해도 정보는 보인다(친구 베타 피드백 ⑤, 2026-09-26) — 선택이 아니라 「살펴보기」로 둔다.
            GameObject inspect = InspectTarget.FindFrom(hit.collider);
            // 적은 화면에서 작고 콜라이더가 몸보다 가늘어 정확히 찍기 어렵다(09-26 사장님 「적 유닛 클릭할 수 있는 범위가 적은 듯, 키워 줘」).
            //    콜라이더에 안 맞았으면 커서 둘레의 가장 가까운 적을 살펴본다 — A 공격 대상 고르기(TryPickEnemy)와 같은 방식, 범위만 넉넉히.
            if (inspect == null)
            {
                float tolerance = InspectPickTolerancePixels * Mathf.Max(1f, Screen.height / 1080f);
                EnemyDummy near = WorldPick.TryPickEnemy(cam, Mouse.current.position.ReadValue(), tolerance);
                if (near != null) inspect = near.gameObject;
            }
            if (inspect != null) { InspectTarget.Set(inspect); return; }
            if (hit.collider != null)
                Debug.Log($"[선택] {hit.collider.name} 을(를) 눌렀지만 선택할 수 있는 대상이 아닙니다.");
            return;
        }

        // 남의 유닛을 눌렀을 때 아무 반응이 없으면 클릭이 안 먹은 것처럼 보인다. 이유를 남긴다.
        if (!IsSelectableByLocalPlayer(hitSelectable))
        {
            // 남의 유닛은 보기 전용으로 하나만 고른다(건물·위습은 그대로 못 고른다).
            if (AllianceShare.IsViewOnly(hitSelectable.gameObject))
            {
                viewOnlyPick = hitSelectable;
                AddToSelection(hitSelectable);
                return;
            }
            int ownerId = hitSelectable.TryGetComponent(out OwnedByPlayer other) ? other.OwnerId : -1;
            Debug.Log($"[선택] {hitSelectable.name} 은(는) 플레이어 {ownerId}의 것이라 고를 수 없습니다 " +
                      $"(나는 플레이어 {LocalPlayer.LocalPlayerId}).");
            return;
        }

        Vector2 clickScreen = Mouse.current.position.ReadValue();
        Object type = TypeKey(hitSelectable);
        bool ctrl = Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed
                                                 || Keyboard.current.leftCommandKey.isPressed || Keyboard.current.rightCommandKey.isPressed);
        bool doubleClick = type != null && type == lastClickType && Time.unscaledTime - lastClickTime <= DoubleClickSeconds
                           && Vector2.Distance(clickScreen, lastClickScreen) <= DoubleClickPixels;
        lastClickType = type;
        lastClickTime = Time.unscaledTime;
        lastClickScreen = clickScreen;

        if (type != null && (ctrl || doubleClick))
        {
            SelectSameTypeOnScreen(hitSelectable, type);
            lastClickType = null;   // 세 번째 클릭이 또 「더블클릭」이 되지 않게
            return;
        }

        AddToSelection(hitSelectable);
    }

    // 같은 종류의 기준 — 유닛은 UnitData, 위습은 WispData. 둘 다 아니면 null(종류 선택 안 함).
    static Object TypeKey(Selectable s)
    {
        if (s == null) return null;
        if (s.TryGetComponent(out UnitIdentity identity) && identity.Data != null) return identity.Data;
        if (s.TryGetComponent(out Wisp wisp) && wisp.Data != null) return wisp.Data;
        return null;
    }

    void SelectSameTypeOnScreen(Selectable seed, Object type)
    {
        ClearSelection();
        InspectTarget.Clear();
        AddToSelection(seed);   // 누른 개체가 첫 카드(초상화·정보칸 대상)
        foreach (Selectable candidate in Selectable.All)
        {
            if (candidate == null || candidate == seed || !IsSelectableByLocalPlayer(candidate)) continue;
            if (TypeKey(candidate) != type) continue;
            Vector3 viewport = cam.WorldToViewportPoint(candidate.transform.position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
            AddToSelection(candidate);
        }
    }

    void SelectInBox(Vector2 screenStart, Vector2 screenEnd)
    {
        Rect box = GetScreenRect(screenStart, screenEnd);

        ClearSelection();
        InspectTarget.Clear();
        foreach (Selectable candidate in Selectable.All)
        {
            if (!IsSelectableByLocalPlayer(candidate)) continue;
            // 사장님 09-29: 드래그는 유닛(·위습)만 — 건물(상점)은 클릭 한 번으로만 고른다.
            if (!candidate.TryGetComponent(out UnitIdentity _) && !candidate.TryGetComponent(out Wisp _)) continue;

            Vector3 screenPos = cam.WorldToScreenPoint(candidate.transform.position);
            if (screenPos.z < 0f) continue;
            if (box.Contains(new Vector2(screenPos.x, screenPos.y)))
                AddToSelection(candidate);
        }
    }

    // OwnedByPlayer가 없는 오브젝트는 소유권 미지정(중립/디버그용)으로 간주해 선택 가능하게 둔다.
    static bool IsSelectableByLocalPlayer(Selectable candidate)
    {
        return AllianceShare.CanControlLocal(candidate.gameObject);   // 내 것 + 동맹 공유로 받은 유닛(남의 유닛 보기 전용 선택은 TrySelectAtCursor가 따로)
    }

    void AddToSelection(Selectable s)
    {
        if (maxSelection > 0 && selected.Count >= maxSelection) return;

        s.SetSelected(true);
        selected.Add(s);
    }

    // ── 10-06 친구 피드백(워크3 부대 지정): Shift(또는 Ctrl/Cmd)+숫자 = 지금 고른 유닛·건물을 그 번호에 지정,
    //    숫자만 = 그 번호를 다시 고른다(카메라는 안 움직인다 — 클릭한 것처럼 선택만). 죽거나 사라진 것은 빠진다.
    readonly List<Selectable>[] controlGroups = new List<Selectable>[10];
    static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

    void HandleControlGroups()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (HotkeyAlias.AltHeld(kb)) return;   // Option+숫자는 F키 대체(F5/F10/F11) — 부대 호출로 새지 않게
        bool assign = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed || kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed
                      || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        for (int d = 0; d < DigitKeys.Length; d++)
        {
            if (!kb[DigitKeys[d]].wasPressedThisFrame) continue;
            if (assign)
            {
                if (selected.Count == 0) return;
                controlGroups[d] = new List<Selectable>(selected);
                PlayerNotification.Show(LocalPlayer.LocalPlayerId, $"{d}번에 지정 ({selected.Count})", 2f);
            }
            else
            {
                List<Selectable> group = controlGroups[d];
                if (group == null) return;
                group.RemoveAll(x => x == null || !x.isActiveAndEnabled);
                if (group.Count == 0) return;
                ClearSelection();
                foreach (Selectable x in group)
                    if (IsSelectableByLocalPlayer(x)) AddToSelection(x);
            }
            return;
        }
    }

    public void ClearSelection()
    {
        viewOnlyPick = null;
        targeting = TargetMode.None;   // 고를 유닛이 없어졌다 — 공격 대기도 끝
        foreach (Selectable s in selected)
            if (s != null)
                s.SetSelected(false);
        selected.Clear();
    }

    // 다중 선택 카드 Shift+클릭 — 그 유닛만 선택 무리에서 뺀다. 하나만 남으면 단일 선택처럼 보인다(목록이 1이 되는 것뿐).
    public void RemoveFromSelection(Selectable target)
    {
        if (target == null || !selected.Remove(target)) return;
        target.SetSelected(false);
    }

    // 다중 선택 카드 그리드에서 카드 하나를 클릭했을 때, 그 유닛만 선택 상태로 바꾼다.
    public void SelectOnly(Selectable target)
    {
        ClearSelection();
        if (target != null && IsSelectableByLocalPlayer(target))
            AddToSelection(target);
    }

    // ── 2026-09-29 배포판 피드백: A 공격 대기 중엔 커서를 빨간 칼로(롤처럼) ──
    //    대기가 풀리는 길(적·땅 클릭·우클릭·Esc·S/H·선택 해제·HUD 클릭·채팅·이 컴포넌트 꺼짐)이 여럿이라
    //    길마다 커서를 되돌리지 않는다. 매 프레임 끝에 targeting 하나만 보고 맞춘다.
    //    09-29 M 이동도 같은 틀 — 초록 화살표.
    TargetMode cursorShown;
    static Texture2D attackCursorTexture;
    static Texture2D moveCursorTexture;

    // 공격 대기 상태도 여기서 내건다 — 사거리 원(AttackRangeIndicator)이 진하게 바뀐다(한 프레임 늦어도 원 색만 늦는다).
    void LateUpdate() { SyncAttackCursor(targeting); AttackModeActive = targeting == TargetMode.Attack; }
    void OnDisable() { SyncAttackCursor(TargetMode.None); AttackModeActive = false; }

    void SyncAttackCursor(TargetMode want)
    {
        if (want == cursorShown) return;
        cursorShown = want;
        if (want == TargetMode.Attack)
        {
            if (attackCursorTexture == null) attackCursorTexture = BuildAttackCursorTexture();
            Cursor.SetCursor(attackCursorTexture, new Vector2(1f, 1f), CursorMode.Auto);   // 칼끝(왼쪽 위)이 클릭 점
        }
        else if (want == TargetMode.Move || want == TargetMode.Patrol)
        {
            if (moveCursorTexture == null) moveCursorTexture = BuildMoveCursorTexture();
            Cursor.SetCursor(moveCursorTexture, new Vector2(1f, 1f), CursorMode.Auto);     // 화살 끝(왼쪽 위)이 클릭 점
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    // 32×32 초록 화살표 — 끝이 왼쪽 위. 칼 커서와 같은 그리기(선분 두께 + 어두운 테두리 1px)로 삼각 머리 + 꼬리.
    static Texture2D BuildMoveCursorTexture()
    {
        const int size = 32;
        Color fill = new Color(0.35f, 0.95f, 0.35f, 1f);
        Color outline = new Color(0f, 0.12f, 0f, 1f);
        Color[] px = new Color[size * size];
        bool[] filled = new bool[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);   // 커서 기준(왼쪽 위 원점, 아래로 +y)
                // 머리: (1,1)·(1,20)·(20,1) 삼각형 / 꼬리: (8,8)→(26,26) 두께 3
                bool head = p.x >= 1f && p.y >= 1f && p.x + p.y <= 21f;
                Vector2 a = new Vector2(8f, 8f), ab = new Vector2(18f, 18f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                bool tail = Vector2.Distance(p, a + ab * t) <= 3f;
                if (!head && !tail) continue;
                int i = (size - 1 - y) * size + x;
                px[i] = fill;
                filled[i] = true;
            }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (filled[i]) continue;
                for (int dy = -1; dy <= 1 && !filled[i] && px[i].a == 0f; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size || !filled[ny * size + nx]) continue;
                        px[i] = outline;
                        break;
                    }
            }
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MoveCursor", filterMode = FilterMode.Point };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // 32×32 빨간 칼 — 칼끝이 왼쪽 위. 좌표는 커서 기준(왼쪽 위 원점, 아래로 +y), 어두운 테두리 1px.
    static Texture2D BuildAttackCursorTexture()
    {
        const int size = 32;
        Color blade = new Color(1f, 0.22f, 0.18f, 1f);
        Color guard = new Color(0.75f, 0.08f, 0.06f, 1f);
        Color grip = new Color(0.45f, 0.05f, 0.04f, 1f);
        Color outline = new Color(0.08f, 0f, 0f, 1f);
        Color[] px = new Color[size * size];
        bool[] filled = new bool[size * size];

        void Stroke(Vector2 a, Vector2 b, float halfWidth, Color c)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    if (Vector2.Distance(p, a + ab * t) > halfWidth) continue;
                    int i = (size - 1 - y) * size + x;   // 텍스처는 아래쪽이 y=0
                    px[i] = c;
                    filled[i] = true;
                }
        }

        Stroke(new Vector2(1.5f, 1.5f), new Vector2(19f, 19f), 1.6f, blade);     // 날
        Stroke(new Vector2(14f, 24f), new Vector2(24f, 14f), 1.5f, guard);       // 코등이
        Stroke(new Vector2(20f, 20f), new Vector2(26.5f, 26.5f), 1.3f, grip);    // 손잡이
        Stroke(new Vector2(27.5f, 27.5f), new Vector2(28.5f, 28.5f), 1.8f, guard); // 폼멜(두 점이 같으면 0으로 나눈다)

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (filled[i]) continue;
                for (int dy = -1; dy <= 1 && !filled[i]; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size || !filled[ny * size + nx]) continue;
                        px[i] = outline;
                        break;
                    }
            }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AttackCursor", filterMode = FilterMode.Point };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Rect GetScreenRect(Vector2 a, Vector2 b)
    {
        float xMin = Mathf.Min(a.x, b.x);
        float xMax = Mathf.Max(a.x, b.x);
        float yMin = Mathf.Min(a.y, b.y);
        float yMax = Mathf.Max(a.y, b.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    void OnGUI()
    {
        if (targeting != TargetMode.None && Mouse.current != null)
        {
            // 커서 옆에 지금 무엇을 고르는지 알려 준다(워크3의 공격 커서 대신).
            Vector2 m = Mouse.current.position.ReadValue();
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            bool move = targeting == TargetMode.Move || targeting == TargetMode.Patrol;
            style.normal.textColor = move ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(m.x + 18, Screen.height - m.y - 8, 320, 24),
                      targeting == TargetMode.Patrol ? "반복 — 오갈 땅을 클릭 (우클릭 취소)" : move ? "이동 — 땅을 클릭 (우클릭 취소)" : "공격 — 적 또는 땅을 클릭 (우클릭 취소)", style);
        }

        if (!isDragging) return;

        Vector2 current = Mouse.current.position.ReadValue();
        Rect screenRect = GetScreenRect(dragStart, current);
        Rect guiRect = new Rect(screenRect.xMin, Screen.height - screenRect.yMax, screenRect.width, screenRect.height);

        Color prevColor = GUI.color;
        GUI.color = boxColor;
        GUI.DrawTexture(guiRect, boxTexture);
        GUI.color = boxBorderColor;
        GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMin, guiRect.width, 1), boxTexture);
        GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMax - 1, guiRect.width, 1), boxTexture);
        GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMin, 1, guiRect.height), boxTexture);
        GUI.DrawTexture(new Rect(guiRect.xMax - 1, guiRect.yMin, 1, guiRect.height), boxTexture);
        GUI.color = prevColor;
    }
}
