using UnityEngine;

/// <summary>유닛에 붙여 첫 프레임(Start)에 날개를 단다 — 클라 거울은 비활성 칸에서 만들어져 그때까진 렌더러 크기를 못 잰다.</summary>
public class WingAttacher : MonoBehaviour
{
    public UnitData data;
    void Start() { UnitWings.Attach(gameObject, data); Destroy(this); }
}
