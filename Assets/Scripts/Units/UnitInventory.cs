using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 가진 유닛. <b>필드에 서 있는 인스턴스의 등록부다</b> — UnitData(에셋) 목록이 아니다.
///
/// 예전엔 List&lt;UnitData&gt;였는데, 그러면 인벤토리와 필드가 "같은 것을 가리키는 두 개의 진실"이 된다.
/// 실제로 조합이 재료를 인벤토리에서만 빼고 필드 오브젝트는 그대로 뒀고, 결과는 인벤토리에만 넣고
/// 필드엔 안 내보냈다. 조합할수록 필드엔 재료가 쌓이고 인벤토리엔 결과만 남아서,
/// 인벤토리를 보는 HUD와 필드를 보는 전투가 서로 다른 게임을 하게 된다.
///
/// 등록부로 만들면 어긋날 수가 없다 — 필드에 있는 것이 곧 인벤토리다.
/// 등록은 <see cref="UnitSpawner"/>가, 해제는 <see cref="UnitIdentity"/>가 파괴 시점에 한다.
/// </summary>
public class UnitInventory : MonoBehaviour
{
    readonly List<UnitIdentity> members = new List<UnitIdentity>();

    // Units는 CombineSystem.GetAvailableRecipes(OnGUI 경로)에서 초당 수백 번 읽힌다.
    // 읽을 때 만들면 그만큼 할당이 생기므로, 등록/해제로 실제 내용이 바뀔 때만 다시 채운다.
    readonly List<UnitData> dataView = new List<UnitData>();

    /// <summary>필드에 있는 실제 개체들. 어느 개체를 소모할지 고를 때 쓴다.</summary>
    public IReadOnlyList<UnitIdentity> Members => members;

    /// <summary>보유 유닛의 원본 데이터 목록. 개체 구분이 필요 없는 표시·집계용.</summary>
    public IReadOnlyList<UnitData> Units => dataView;

    public event Action OnInventoryChanged;

    public void Register(UnitIdentity unit)
    {
        if (unit == null || IndexOf(unit) >= 0) return;

        members.Add(unit);
        RebuildDataView();
    }

    /// <summary>
    /// 등록을 뺀다. Destroy는 프레임 끝에야 처리되므로 파괴를 결정한 쪽이 즉시 부르고,
    /// UnitIdentity.OnDestroy가 안전망으로 한 번 더 부른다 — 두 번 불려도 무해하다.
    /// </summary>
    public void Unregister(UnitIdentity unit)
    {
        int index = IndexOf(unit);
        if (index < 0) return;

        members.RemoveAt(index);
        RebuildDataView();
    }

    // 파괴된 Object는 Unity의 == 오버로드에서 null과 같아진다. 그대로 비교하면 파괴된 개체끼리
    // 서로 같은 것으로 취급돼 엉뚱한 자리가 지워진다. 등록부는 개체 하나를 정확히 집어야 하므로
    // 참조 동일성으로만 찾는다.
    int IndexOf(UnitIdentity unit)
    {
        for (int i = 0; i < members.Count; i++)
            if (ReferenceEquals(members[i], unit)) return i;

        return -1;
    }

    void RebuildDataView()
    {
        dataView.Clear();

        for (int i = 0; i < members.Count; i++)
        {
            UnitIdentity member = members[i];
            if (member != null && member.Data != null) dataView.Add(member.Data);
        }

        OnInventoryChanged?.Invoke();
    }
}
