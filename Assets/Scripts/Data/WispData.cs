using UnityEngine;

[CreateAssetMenu(fileName = "NewWispData", menuName = "GuilRandomDefense/Wisp Data")]
public class WispData : ScriptableObject
{
    public string wispName;
    public UnitGrade targetGrade;
    public bool isPlayerChoice;
    public GameObject prefab;
}
