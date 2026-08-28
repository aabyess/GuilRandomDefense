using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [SerializeField] float dragThreshold = 4f;
    [SerializeField] Color boxColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
    [SerializeField] Color boxBorderColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

    Camera cam;
    readonly List<Selectable> selected = new List<Selectable>();

    Vector2 dragStart;
    bool isDragging;
    bool leftButtonHeld;
    Texture2D boxTexture;

    void Awake()
    {
        cam = Camera.main;
        boxTexture = Texture2D.whiteTexture;
    }

    void Update()
    {
        if (Mouse.current == null || cam == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragStart = Mouse.current.position.ReadValue();
            isDragging = false;
            leftButtonHeld = true;
        }

        if (leftButtonHeld && !isDragging)
        {
            Vector2 current = Mouse.current.position.ReadValue();
            if (Vector2.Distance(dragStart, current) >= dragThreshold)
                isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (isDragging)
                SelectInBox(dragStart, Mouse.current.position.ReadValue());
            else
                TrySelectAtCursor();

            isDragging = false;
            leftButtonHeld = false;
        }
    }

    void TrySelectAtCursor()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Selectable hitSelectable = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            hit.collider.TryGetComponent(out hitSelectable);

        ClearSelection();
        if (hitSelectable != null && IsSelectableByLocalPlayer(hitSelectable))
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
        s.SetSelected(true);
        selected.Add(s);
    }

    void ClearSelection()
    {
        foreach (Selectable s in selected)
            if (s != null)
                s.SetSelected(false);
        selected.Clear();
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
