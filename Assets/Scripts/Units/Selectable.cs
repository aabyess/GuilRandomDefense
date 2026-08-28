using System.Collections.Generic;
using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField] GameObject selectedIndicator;

    public static readonly List<Selectable> All = new List<Selectable>();

    public bool IsSelected { get; private set; }

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
    }
}
