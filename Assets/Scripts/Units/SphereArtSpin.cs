using UnityEngine;

/// <summary>상시 오라 메시·궤적 묶음의 회전(원작 Dummy 뼈 회전) — SphereArtBuilder가 단다. 축은 로컬, 초당 도.</summary>
public class SphereArtSpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degPerSecond;
    void Update() => transform.Rotate(axis, degPerSecond * Time.deltaTime, Space.Self);
}
