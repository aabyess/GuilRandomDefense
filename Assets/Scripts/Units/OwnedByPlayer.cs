using UnityEngine;

public class OwnedByPlayer : MonoBehaviour
{
    [SerializeField] int ownerId;

    public int OwnerId => ownerId;

    public void SetOwner(int playerId)
    {
        ownerId = playerId;
    }
}
