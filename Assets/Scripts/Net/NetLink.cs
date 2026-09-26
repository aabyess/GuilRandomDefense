using UnityEngine;

/// <summary>
/// 호스트의 실물(유닛·적·위습)에 붙는 꼬리표 — 「이것의 거울(NetEntity)은 저것」.
/// NetMirrorHost가 붙이고, 실물이 파괴되면 거울을 거둔다(파괴 경로가 조합·소모·사망 어디든 한 곳에서).
/// </summary>
public class NetLink : MonoBehaviour
{
    public NetEntity Entity { get; set; }

    void OnDestroy()
    {
        if (Entity != null) Entity.DespawnFromHost();
    }
}
