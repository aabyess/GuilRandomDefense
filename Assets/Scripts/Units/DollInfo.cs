using UnityEngine;

/// <summary>
/// 조합표 인형 표지 — 클릭하면 어떤 유닛인지 HUD에 보인다(친구 베타 피드백 ⑤, 2026-09-26).
/// RecipeDollSpawner가 인형을 세울 때 붙인다(트리거 BoxCollider와 함께). 게임 로직은 없다 — 조작도 안 된다.
/// </summary>
[DisallowMultipleComponent]
public class DollInfo : MonoBehaviour
{
    [SerializeField] UnitData unit;
    public UnitData Unit => unit;
    public void SetUnit(UnitData value) => unit = value;
}
