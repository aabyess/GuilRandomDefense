using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>스킬 → 연출 대본 표(10-09). Resources/Cinematics/SkillCinematicTable.asset — 에디터 CinematicImporter.LinkSkills가 만든다.</summary>
public class SkillCinematicTable : ScriptableObject
{
    [Serializable] public class Entry { public SkillData skill; public int script; }

    public List<CinematicScript> scripts = new List<CinematicScript>();
    public List<Entry> entries = new List<Entry>();

    static SkillCinematicTable cached; static bool loaded;
    public static SkillCinematicTable Instance
    {
        get { if (!loaded) { cached = Resources.Load<SkillCinematicTable>("Cinematics/SkillCinematicTable"); loaded = true; } return cached; }
    }

    public int Find(SkillData skill)
    {
        if (skill == null) return -1;
        foreach (Entry e in entries) if (e.skill == skill) return e.script;
        return -1;
    }
}
