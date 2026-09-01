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

        if (Mouse.current == null || cam == null) return;

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

        if (hitSelectable == null)
        {
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
