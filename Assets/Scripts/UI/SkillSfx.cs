using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 효과음(사장님 지시 2026-09-30 「사운드 다 적용」) — 원작 맵에 임포트돼 있던 스킬 소리.
///
/// 원작(war3map_new.j): InitSounds의 <c>gg_snd_X=CreateSound("X.mp3", …3D…)</c>를 스킬 트리거가
/// <c>PlaySoundOnUnitBJ</c>·<c>PlaySoundAtPointBJ</c>로 시전 자리에서 낸다(볼륨·피치·끊김 거리는 소리마다 다르다).
/// 표는 Tools/skill_sfx/build_table.py가 만든 Resources/Sounds/SkillSfxTable.txt — 근거는 Docs/research/SKILL_SFX_MAPPING.csv.
///   · S줄: 그 소리를 내는 원작 트리거가 우리 스킬 에셋 설명에 적혀 있는 것 → 그 스킬이 나갈 때.
///   · R줄: 트리거 앞머리가 가리키는 캐릭터의 로스터 → S에 없는 스킬을 쓸 때 그 캐릭터 소리 묶음에서 하나
///     (어느 스킬인지는 우리 데이터로 못 가린다 — 「그 캐릭터의 소리」까지만 원작 근거. 그래서 <see cref="RosterInterval"/>로 드물게).
///
/// 3D 감쇠는 유니티 3D 오디오 대신 손으로 센다: 화면 가운데가 닿는 땅 점과 소리 자리의 수평 거리가
/// 원작 최소 거리 안이면 제 볼륨, 끊김 거리 밖이면 안 난다(멀티에서 남의 레인 스킬이 제 볼륨으로 들리지 않게).
/// 호스트·싱글에서 <see cref="Cast"/>가 불리고, 멀티 호스트는 <see cref="Broadcast"/>를 구독한 NetGameState가 클라에 넘긴다.
/// </summary>
public static class SkillSfx
{
    const string TablePath = "Sounds/SkillSfxTable";
    const float RosterInterval = 2.5f;     // 캐릭터 단위 소리는 로스터 하나가 이보다 자주 안 낸다
    const float SameClipInterval = 0.3f;   // 같은 소리 연타 막기 하한(원작은 소리 핸들 하나 = 동시에 하나)
    const int Voices = 4;                  // 동시에 나는 스킬 소리 상한(10-08 8→4: 합쳐진 소리가 배경음을 덮지 않게)
    // 10-08 사장님 「스킬 사운드가 너무 크다, 배경음보다 작게」: 스킬 전용 −14dB(×0.2). 가장 큰 소리도 0.7×1.0×0.2 = 0.14 < 배경음 0.20.
    public const float SkillVolumeScale = 0.2f;
    const float DefaultMinDistance = 600f; // 원작 단위 — SetSoundDistances가 없는 소리
    const float DefaultCutoff = 3000f;

    struct Clip { public string name, folder; public float volume, pitch, min, cutoff; }
    struct Shot { public int clip; public float volume; }

    static Clip[] clips;
    static Dictionary<string, Shot[]> bySkill, byRoster;
    static readonly Dictionary<string, float> rosterNext = new Dictionary<string, float>();
    static float[] clipNext;
    static AudioClip[] loaded;
    static bool[] loadTried;
    static AudioSource[] sources;
    static int playsLogged;

    /// <summary>MP: 호스트에서 난 스킬 소리(클립 번호, 볼륨 배수, 자리). 싱글에선 구독자가 없다.</summary>
    public static event System.Action<int, float, Vector3> Broadcast;

    /// <summary>이 PC에서 실제로 낸 횟수(테스트용).</summary>
    public static int PlayCount { get; private set; }

    /// <summary>표에 걸려 소리를 고른 횟수 — 감쇠·연타 제한으로 안 난 것도 센다(테스트용).</summary>
    public static int CastCount { get; private set; }

    /// <summary>스킬이 나간 순간(호스트·싱글). 표에 없으면 아무 일도 없다.</summary>
    public static void Cast(UnitData caster, SkillData skill, Vector3 position)
    {
        if (skill == null) return;
        EnsureTable();
        Shot[] shots;
        if (!bySkill.TryGetValue(Key(skill.name), out shots))
        {
            if (caster == null) return;
            string roster = Key(caster.name);
            if (!byRoster.TryGetValue(roster, out shots)) return;
            if (rosterNext.TryGetValue(roster, out float next) && Time.unscaledTime < next) return;
            rosterNext[roster] = Time.unscaledTime + RosterInterval;
        }
        Shot shot = shots[Random.Range(0, shots.Length)];
        CastCount++;
        Play(shot.clip, shot.volume, position);
        Broadcast?.Invoke(shot.clip, shot.volume, position);
    }

    /// <summary>이 PC에서 낸다(호스트가 넘겨준 것도 여기로).</summary>
    public static void Play(int index, float volumeScale, Vector3 position)
    {
        if (!GameSound.Enabled) return;
        EnsureTable();
        if (index < 0 || index >= clips.Length) return;
        if (Time.unscaledTime < clipNext[index]) return;
        Clip c = clips[index];
        float heard = Attenuation(position, c.min, c.cutoff);
        if (heard <= 0f) return;
        AudioClip audio = Load(index);
        if (audio == null) return;
        AudioSource source = FreeSource();
        if (source == null) return;
        // 매 타 도는 스킬(09-30 실측: darkslash_02가 25초에 65번 골라짐)이 같은 소리를 끊어 가며 겹치지 않게 — 70%는 듣고 다음 것.
        clipNext[index] = Time.unscaledTime + Mathf.Max(SameClipInterval, 0.7f * audio.length / Mathf.Max(0.1f, c.pitch));
        source.clip = audio;
        source.pitch = c.pitch;
        source.volume = GameSound.DefaultVolume * SkillVolumeScale * AudioPrefs.SfxVolume * c.volume * Mathf.Clamp01(volumeScale) * heard;
        source.Play();
        PlayCount++;
        if (playsLogged++ < 20) Debug.Log($"[소리] 스킬 효과음 {c.name}(볼륨 {source.volume:0.00}, 피치 {c.pitch:0.0})");
    }

    // 화면 가운데가 닿는 땅 점(y=0)에서 잰 수평 거리 → 0~1. 카메라가 없으면(배치 테스트) 제 볼륨.
    static float Attenuation(Vector3 position, float min, float cutoff)
    {
        Camera camera = Camera.main;
        if (camera == null) return 1f;
        Transform t = camera.transform;
        Vector3 focus = t.position;
        if (t.forward.y < -0.01f) focus += t.forward * (t.position.y / -t.forward.y);
        float near = (min > 0f ? min : DefaultMinDistance) / WorldScale.Value;
        float far = (cutoff > 0f ? cutoff : DefaultCutoff) / WorldScale.Value;
        if (far <= near) far = near + 1f;
        float dx = position.x - focus.x, dz = position.z - focus.z;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        return 1f - Mathf.Clamp01((distance - near) / (far - near));
    }

    static AudioSource FreeSource()
    {
        if (sources == null || sources[0] == null)
        {
            GameObject host = new GameObject("[SkillSfx]");
            Object.DontDestroyOnLoad(host);
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = host.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;   // 감쇠는 Attenuation이 센다
            }
        }
        foreach (AudioSource source in sources) if (!source.isPlaying) return source;
        return null;
    }

    static AudioClip Load(int index)
    {
        if (!loadTried[index])
        {
            loadTried[index] = true;
            string path = "Sounds/" + clips[index].folder + "/" + clips[index].name;
            loaded[index] = Resources.Load<AudioClip>(path);
            if (loaded[index] == null) Debug.LogWarning($"[소리] 스킬 효과음 {clips[index].name}: Resources/{path} 를 못 찾았습니다.");
        }
        return loaded[index];
    }

    static string Key(string name) => name.Normalize(System.Text.NormalizationForm.FormC);

    static void EnsureTable()
    {
        if (clips != null) return;
        List<Clip> list = new List<Clip>();
        Dictionary<string, int> indexOf = new Dictionary<string, int>();
        bySkill = new Dictionary<string, Shot[]>();
        byRoster = new Dictionary<string, Shot[]>();
        TextAsset text = Resources.Load<TextAsset>(TablePath);
        if (text == null) Debug.LogWarning($"[소리] 스킬 효과음 표 Resources/{TablePath} 가 없습니다.");
        else
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (string raw in text.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f[0] == "C" && f.Length >= 7)
                {
                    indexOf[f[1]] = list.Count;
                    list.Add(new Clip
                    {
                        name = f[1], folder = f[2], volume = float.Parse(f[3], inv), pitch = float.Parse(f[4], inv),
                        min = float.Parse(f[5], inv), cutoff = float.Parse(f[6], inv),
                    });
                }
                else if ((f[0] == "S" || f[0] == "R") && f.Length >= 3)
                {
                    List<Shot> shots = new List<Shot>();
                    foreach (string part in f[2].Split(','))
                    {
                        int colon = part.LastIndexOf(':');
                        if (colon <= 0 || !indexOf.TryGetValue(part.Substring(0, colon), out int clip)) continue;
                        shots.Add(new Shot { clip = clip, volume = float.Parse(part.Substring(colon + 1), inv) });
                    }
                    if (shots.Count > 0) (f[0] == "S" ? bySkill : byRoster)[Key(f[1])] = shots.ToArray();
                }
            }
        }
        clips = list.ToArray();
        clipNext = new float[clips.Length];
        loaded = new AudioClip[clips.Length];
        loadTried = new bool[clips.Length];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        clips = null;
        sources = null;
        rosterNext.Clear();
        PlayCount = 0;
        CastCount = 0;
        playsLogged = 0;
    }
}
