using UnityEngine;

// SkillVfx의 붙는 이펙트(스턴·이감·버프)를 대상 위로 옮기고 풀에 되돌린다(1.2.2) — SkillVfx 루트에 하나 붙는다.
// 붙는 이펙트를 대상의 자식으로 두면 대상이 죽을 때 같이 파괴돼 풀이 샌다(SkillVfx.AttachPoolCap 주석).
public class SkillVfxFollower : MonoBehaviour
{
    void LateUpdate() => SkillVfx.TickAttached();
}
