using System.Collections.Generic;
using UnityEngine;

// 스킬별 이펙트 표(2026-09-30) — 원작 스킬 아트를 무료 팩 프리팹(Assets/ThirdParty)·우리 기본 이펙트(SkillVfx.Kind)에 짝지은 것.
// 근거: Docs/research/SKILL_VFX_MAPPING.csv(Blender, 원작 트리거·더미 능력 아트 → 모양 묶음 → 후보 프리팹).
// 에디터 SkillVfxTableBuilder가 CSV에서 만든다 → Resources/Effects/SkillVfxTable.asset. 스킬 에셋은 안 건드린다.
// 표에 없는 스킬은 지금처럼 기본 이펙트만 뜬다. 칸이 비면 그 자리도 기본대로.
public class SkillVfxTable : ScriptableObject
{
    [System.Serializable]
    public class Slot
    {
        public int prefab = -1;   // prefabs 인덱스(MP RPC가 이 번호를 싣는다)
        public int kind = -1;     // 팩 프리팹 대신 우리 기본 이펙트(SkillVfx.Kind)
        public bool IsSet => prefab >= 0 || kind >= 0;
    }

    [System.Serializable]
    public class Entry
    {
        public SkillData skill;
        public Slot hit = new Slot();      // 맞은 적마다(스킬 피해 적중 이펙트를 바꾼다)
        public Slot area = new Slot();     // 범위 중심 땅 위에 한 번(반경에 맞춰 키운다)
        public Slot caster = new Slot();   // 시전자 발밑에 한 번
    }

    public List<GameObject> prefabs = new List<GameObject>();
    // 프리팹마다 0.4초 시뮬레이션 뒤 파티클 경계의 가로 지름(프리팹 단위) — 원하는 크기로 키우는 기준.
    public List<float> nativeSizes = new List<float>();
    public List<Entry> entries = new List<Entry>();

    Dictionary<SkillData, Entry> bySkill;

    public Entry Find(SkillData skill)
    {
        if (skill == null) return null;
        if (bySkill == null)
        {
            bySkill = new Dictionary<SkillData, Entry>();
            foreach (Entry e in entries) if (e.skill != null) bySkill[e.skill] = e;
        }
        return bySkill.TryGetValue(skill, out Entry found) ? found : null;
    }
}
