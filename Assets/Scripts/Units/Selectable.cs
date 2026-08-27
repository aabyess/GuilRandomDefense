using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField] GameObject selectedIndicator;

    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
    }
}
