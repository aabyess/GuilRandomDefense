using UnityEngine;

[CreateAssetMenu(fileName = "NewWispData", menuName = "GuilRandomDefense/Wisp Data")]
public class WispData : ScriptableObject
{
    public string wispName;
    public UnitGrade targetGrade;
    public bool isPlayerChoice;
    public GameObject prefab;
    // S2 진화 위습(진화 포탈만 먹는다). 맨 뒤 필드.
    public bool isEvolution;
}
