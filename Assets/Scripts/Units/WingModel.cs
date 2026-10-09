using UnityEngine;

/// <summary>날개 프리팹이 싣는 값(WingBuilder가 채운다): 휴식 자세 폭(m), 가슴 기준 오프셋(m, 유닛 로컬), 회전.</summary>
public class WingModel : MonoBehaviour
{
    public float restSpan = 3.7f;
    public Vector3 chestOffset = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;
}
