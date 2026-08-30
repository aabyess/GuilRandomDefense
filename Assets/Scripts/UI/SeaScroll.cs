using UnityEngine;

/// <summary>
/// 바다 머티리얼의 UV를 흘려 물결처럼 보이게 한다.
/// 셰이더를 새로 짜는 대신 기본 셰이더의 오프셋만 움직인다 — 밑면과 노멀을 다른 속도로 흘리면
/// 두 결이 어긋나면서 표면이 일렁이는 것처럼 읽힌다.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SeaScroll : MonoBehaviour
{
    [SerializeField] Vector2 baseSpeed = new Vector2(0.012f, 0.008f);
    [SerializeField] Vector2 normalSpeed = new Vector2(-0.018f, 0.014f);

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");

    Material material;

    void Awake()
    {
        // 공유 머티리얼을 흘리면 이 머티리얼을 쓰는 에셋 파일이 실행 중에 바뀐다.
        // 인스턴스를 따로 만들어 쓴다.
        material = GetComponent<Renderer>().material;
    }

    void Update()
    {
        if (material == null) return;

        float t = Time.time;
        material.SetTextureOffset(BaseMapId, baseSpeed * t);
        material.SetTextureOffset(BumpMapId, normalSpeed * t);
    }
}
