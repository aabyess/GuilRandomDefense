using UnityEngine;

// 스폰된 인스턴스가 자신의 원본 UnitData를 들고 있게 한다. UI 등에서 이름/등급/기준 스탯 조회용.
public class UnitIdentity : MonoBehaviour
{
    [SerializeField] UnitData data;

    public UnitData Data => data;

    public void SetData(UnitData unitData)
    {
        data = unitData;
    }
}
