using UnityEngine;

/// <summary>
/// 포탈 마법진(Blender `포탈_마법진_*`)의 룬 고리를 돌린다 — 바깥 고리는 시계, 안쪽은 반시계.
///
/// 고리는 맵 생성기(StructureDresser)가 자식 이름 `…_회전_시계`·`…_회전_반시계`로 찾아 미리 넣어 둔다.
/// 실행 중에 이름으로 찾지 않는 이유 — 맥에서 가져온 한글 이름은 정규화(NFC/NFD)가 달라 비교가 틀릴 수 있다.
/// 고리의 원점이 가운데가 아니어도 되게 마법진 원점을 축으로 돈다.
/// </summary>
public class MagicCircleSpin : MonoBehaviour
{
    [SerializeField] Transform[] clockwise = new Transform[0];
    [SerializeField] Transform[] counterClockwise = new Transform[0];
    [SerializeField] float degreesPerSecond = 12f;

    public void SetRings(Transform[] clockwiseRings, Transform[] counterClockwiseRings)
    {
        clockwise = clockwiseRings;
        counterClockwise = counterClockwiseRings;
    }

    void Update()
    {
        float angle = degreesPerSecond * Time.deltaTime;
        Vector3 pivot = transform.position;

        // 위에서 볼 때 시계 방향 = 월드 +Y 축 기준 양의 회전(유니티는 왼손 좌표계).
        foreach (Transform ring in clockwise)
            if (ring != null) ring.RotateAround(pivot, Vector3.up, angle);
        foreach (Transform ring in counterClockwise)
            if (ring != null) ring.RotateAround(pivot, Vector3.up, -angle * 0.7f);
    }
}
