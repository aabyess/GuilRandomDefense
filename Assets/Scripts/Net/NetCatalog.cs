using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 네트워크로 「무슨 유닛/적/위습인가」를 번호 하나로 보내기 위한 목록. 호스트와 클라가 같은 빌드면 같은 순서다.
/// 에디터 NetSetup.BuildCatalog가 채운다(에셋 GUID 순 — 생성기가 로스터를 다시 만들어도 .meta가 살아 있으면 순서가 안 바뀐다).
/// 로스터는 안 건드린다 — 이 파일에만 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "GuilRandomDefense/Net Catalog")]
public class NetCatalog : ScriptableObject
{
    public List<UnitData> units = new List<UnitData>();
    public List<EnemyData> enemies = new List<EnemyData>();
    public List<WispData> wisps = new List<WispData>();
    public List<GamblingOptionData> gamblingOptions = new List<GamblingOptionData>();
    public List<UnitTraitData> traits = new List<UnitTraitData>();
    public List<UnitUpgradeTrackData> gradeTracks = new List<UnitUpgradeTrackData>();
    public List<AttackTypeUpgradeTrackData> attackTypeTracks = new List<AttackTypeUpgradeTrackData>();
    public List<UniqueRerollAbilityData> rerollAbilities = new List<UniqueRerollAbilityData>();

    Dictionary<Object, int> index;

    /// <summary>접속 때 호스트·클라 빌드가 같은지 대 볼 값(개수+이름). string.GetHashCode는 실행마다 같다는
    /// 보장이 없어 FNV-1a를 직접 돈다.</summary>
    public int Fingerprint
    {
        get
        {
            unchecked
            {
                uint hash = 2166136261;
                void Add(string text)
                {
                    foreach (char ch in text) { hash ^= ch; hash *= 16777619; }
                    hash ^= '|'; hash *= 16777619;
                }
                foreach (UnitData u in units) Add(u != null ? u.name : "-");
                foreach (EnemyData e in enemies) Add(e != null ? e.name : "-");
                foreach (WispData w in wisps) Add(w != null ? w.name : "-");
                foreach (GamblingOptionData g in gamblingOptions) Add(g != null ? g.name : "-");
                foreach (UnitTraitData t in traits) Add(t != null ? t.name : "-");
                foreach (UnitUpgradeTrackData t in gradeTracks) Add(t != null ? t.name : "-");
                foreach (AttackTypeUpgradeTrackData t in attackTypeTracks) Add(t != null ? t.name : "-");
                foreach (UniqueRerollAbilityData r in rerollAbilities) Add(r != null ? r.name : "-");
                return (int)hash;
            }
        }
    }

    public int IndexOf(Object data)
    {
        if (data == null) return -1;
        if (index == null)
        {
            index = new Dictionary<Object, int>();
            for (int i = 0; i < units.Count; i++) if (units[i] != null) index[units[i]] = i;
            for (int i = 0; i < enemies.Count; i++) if (enemies[i] != null) index[enemies[i]] = i;
            for (int i = 0; i < wisps.Count; i++) if (wisps[i] != null) index[wisps[i]] = i;
            for (int i = 0; i < traits.Count; i++) if (traits[i] != null) index[traits[i]] = i;
        }
        return index.TryGetValue(data, out int i2) ? i2 : -1;
    }

    public UnitData Unit(int i) => i >= 0 && i < units.Count ? units[i] : null;
    public EnemyData Enemy(int i) => i >= 0 && i < enemies.Count ? enemies[i] : null;
    public WispData Wisp(int i) => i >= 0 && i < wisps.Count ? wisps[i] : null;
}
