using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "GuilRandomDefense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public float hp;
    public float moveSpeed;
    public int goldReward;
    public bool isBoss;
    public GameObject prefab;
}
