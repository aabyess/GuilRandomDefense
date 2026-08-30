using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 위습이 생성될 칸. 등급마다 하나씩 있고, 그 등급 위습은 이 칸 안에 생긴다.
/// 칸은 사방이 막혀 있어서, 안에서 생긴 위습은 그 칸의 포탈로만 갈 수 있다.
/// </summary>
public class WispCell : MonoBehaviour
{
    [SerializeField] UnitGrade grade;

    static readonly List<WispCell> registry = new List<WispCell>();

    public UnitGrade Grade => grade;

    public void SetGrade(UnitGrade value)
    {
        grade = value;
    }

    void OnEnable() => registry.Add(this);
    void OnDisable() => registry.Remove(this);

    public static WispCell Get(UnitGrade grade)
    {
        foreach (WispCell cell in registry)
            if (cell.grade == grade)
                return cell;

        return null;
    }
}
