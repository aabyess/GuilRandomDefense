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
    bool attackTargeting;
    int attackCancelFrame = -1;
    // 우클릭 취소는 같은 프레임의 우클릭 이동(UnitMover)도 막아야 한다 — 스크립트 실행 순서와 상관없이.
    public bool IsAttackTargeting => attackTargeting || attackCancelFrame == Time.frameCount;
    const float AttackPickTolerancePixels = 36f;
    // 좌클릭 살펴보기(적 정보)용 — 공격 대상 고르기보다 넉넉히. 1080 기준 픽셀, 화면 높이에 비례해 늘린다.
    const float InspectPickTolerancePixels = 56f;
    bool leftButtonHeld;
    bool ignoreCurrentPress;
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
        if (ChatInputGate.IsOpen) return;

        HandleCommandKeys();

        if (Mouse.current == null || cam == null) return;

        if (attackTargeting && HandleAttackTargeting()) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 하단 HUD 등 uGUI 위에서 누른 클릭은 월드 선택으로 취급하지 않는다.
            ignoreCurrentPress = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

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

    /// <summary>A 키·명령칸 「공격」. 싸울 수 있는 유닛이 선택돼 있을 때만 들어간다.</summary>
    public void BeginAttackTargeting()
    {
        foreach (Selectable s in selected)
        {
            if (s != null && s.GetComponent<UnitCombat>() != null)
            {
                attackTargeting = true;
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
            attackTargeting = false;
            attackCancelFrame = Time.frameCount;
            return false;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

        Vector2 screen = Mouse.current.position.ReadValue();
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

        attackTargeting = false;
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

        if (Keyboard.current.aKey.wasPressedThisFrame)
            BeginAttackTargeting();

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            attackTargeting = false;
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
            attackTargeting = false;
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
    }

    void TrySelectAtCursor()
    {
        Selectable hitSelectable = null;
        if (WorldPick.TryHit(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
            hit.collider.TryGetComponent(out hitSelectable);

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
            int ownerId = hitSelectable.TryGetComponent(out OwnedByPlayer other) ? other.OwnerId : -1;
            Debug.Log($"[선택] {hitSelectable.name} 은(는) 플레이어 {ownerId}의 것이라 고를 수 없습니다 " +
                      $"(나는 플레이어 {LocalPlayer.LocalPlayerId}).");
            return;
        }

        AddToSelection(hitSelectable);
    }

    void SelectInBox(Vector2 screenStart, Vector2 screenEnd)
    {
        Rect box = GetScreenRect(screenStart, screenEnd);

        ClearSelection();
        InspectTarget.Clear();
        foreach (Selectable candidate in Selectable.All)
        {
            if (!IsSelectableByLocalPlayer(candidate)) continue;

            Vector3 screenPos = cam.WorldToScreenPoint(candidate.transform.position);
            if (screenPos.z < 0f) continue;
            if (box.Contains(new Vector2(screenPos.x, screenPos.y)))
                AddToSelection(candidate);
        }
    }

    // OwnedByPlayer가 없는 오브젝트는 소유권 미지정(중립/디버그용)으로 간주해 선택 가능하게 둔다.
    static bool IsSelectableByLocalPlayer(Selectable candidate)
    {
        if (!candidate.TryGetComponent(out OwnedByPlayer owner)) return true;
        return owner.OwnerId == LocalPlayer.LocalPlayerId;
    }

    void AddToSelection(Selectable s)
    {
        if (maxSelection > 0 && selected.Count >= maxSelection) return;

        s.SetSelected(true);
        selected.Add(s);
    }

    public void ClearSelection()
    {
        foreach (Selectable s in selected)
            if (s != null)
                s.SetSelected(false);
        selected.Clear();
    }

    // 다중 선택 카드 그리드에서 카드 하나를 클릭했을 때, 그 유닛만 선택 상태로 바꾼다.
    public void SelectOnly(Selectable target)
    {
        ClearSelection();
        if (target != null && IsSelectableByLocalPlayer(target))
            AddToSelection(target);
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
        if (attackTargeting && Mouse.current != null)
        {
            // 커서 옆에 지금 무엇을 고르는지 알려 준다(워크3의 공격 커서 대신).
            Vector2 m = Mouse.current.position.ReadValue();
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(m.x + 18, Screen.height - m.y - 8, 320, 24), "공격 — 적 또는 땅을 클릭 (우클릭 취소)", style);
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
