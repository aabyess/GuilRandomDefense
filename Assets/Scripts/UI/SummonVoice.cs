using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 상위 등급 유닛 획득 음성(사장님 지시 2026-09-30) — 원작 「초월 루피를 뽑으면 나오는 소리」.
///
/// 원작(war3map_new.j): 채팅 언락 트리거가 유닛을 만든 직후
/// <c>set udg_UnitSound=CreateSound("Sound\\Luffy.mp3",false,false,false,10,10,"")</c> → <c>StartSound</c>.
/// 2D이고 GetLocalPlayer 검사가 없다 = **전원에게** 난다. Trig_Eternal_*(초월함 28) · Trig_IM_*(불멸의 11) ·
/// Trig_Forever_*(영원한 8) · Hidden22_balra·Hidden8Ryuma(초월 둘) 49곳.
/// 파일은 맵 안에 없다 — 원랜디 확장팩(Sound 폴더, 제작자 배포)에 있다. Assets/Audio/Original/SOURCE.txt 참고.
///
/// 표의 열쇠는 로스터 에셋 이름. 「원작」 줄은 트리거의 유닛 ID → Docs/reference/MASTER_UID_ROSTER_MAP.csv 대응 그대로
/// (한 로스터가 원작 유닛 둘을 받으면 음성도 둘 — 그중 하나를 무작위로). 「PM 배정」 줄은 **원작 근거가 없다**:
/// 원작에 소환 음성이 없는 다른세계·랜덤전용·제한됨과, 대응 uid가 없는 초월 넷에 PM이 남는 음성·스킬 음성을 골라 붙였다.
/// 바꾸려면 이 표만 고치면 된다(파일은 Resources/Sounds/Summon/).
///
/// 획득 한 점(UnitIdentity.OnAcquired — 호스트·싱글에서만 난다)을 구독해 이 PC에서 내고, 멀티 호스트는
/// <see cref="Broadcast"/>를 구독한 NetGameState가 클라 전원에게 넘긴다(이 파일은 Fusion을 모른다).
/// </summary>
public static class SummonVoice
{
    const string Folder = "Sounds/Summon/";

    static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
    {
        { "불멸_고도현", new[] { "Siro", "Siki" } },
        { "불멸_김용태", new[] { "Roger" } },
        { "불멸_박은석", new[] { "Z" } },
        { "불멸_신지우", new[] { "Gaban", "kaido" } },
        { "불멸_이승우", new[] { "Garp" } },
        { "불멸_이이삭", new[] { "Sengoku" } },
        { "불멸_정윤식", new[] { "Lailey" } },
        { "불멸_정준영", new[] { "dragon", "bigmam" } },
        { "영원_김정래", new[] { "bugi", "Uta" } },
        { "영원_문필환", new[] { "vivi" } },
        { "영원_서민성", new[] { "oden_sound" } },
        { "영원_윤현모", new[] { "Ace" } },
        { "영원_이지원", new[] { "Nika" } },
        { "영원_조세민", new[] { "hancock" } },
        { "영원_최상호", new[] { "mihawk", "cavendish" } },
        { "초월_강재규_AP", new[] { "Robin" } },
        { "초월_강주혁_AP", new[] { "Nami", "Zoro_2" } },
        { "초월_구주호_AD", new[] { "kizaru", "jinbe", "yamato" } },   // 원작 + PM 추가 · 10-06 사장님: 료쿠규 ↔ 징베 맞바꿈(최상호 AD와)
        { "초월_김경현_AP", new[] { "snakeman" } },
        { "초월_김만경_AD", new[] { "Akainu" } },
        { "초월_김민준_AP", new[] { "tashigi" } },
        { "초월_노태현_AP", new[] { "Brook" } },
        { "초월_두유찬_AD", new[] { "sabo" } },
        { "초월_박기찬_AD", new[] { "Franky" } },
        { "초월_박민수_AD", new[] { "Minsu_boss" } },   // 10-07 사장님 녹음 파일(원작 Zoro 대신, Assets/Audio/Custom)
        { "초월_배성령_AD", new[] { "Sanji_2" } },
        { "초월_신문철_AP", new[] { "Luffy" } },
        { "초월_양재모_AD", new[] { "Law" } },
        { "초월_유재헌_ADAP", new[] { "Shira" } },
        { "초월_이태훈_AP", new[] { "huji" } },
        { "초월_임장혁_AD", new[] { "lucci" } },
        { "초월_임채민_AP", new[] { "Teach" } },
        { "초월_조성진_AD", new[] { "Usop" } },
        { "초월_최상호_AD", new[] { "ryokugyu" } },   // 10-06 사장님: 징베 ↔ 료쿠규 맞바꿈(구주호 AD와)
        { "초월_최상호_AP", new[] { "DP" } },
        { "초월_황준석_ADAP", new[] { "kid" } },
        { "영원_김영원", new[] { "hancock" } },   // PM 배정
        { "초월_김건_AP", new[] { "Choppa" } },   // PM 배정
        { "초월_박민석_ADAP", new[] { "Shanks" } },   // PM 배정
        { "초월_엄태웅_AD", new[] { "Sanji" } },   // PM 배정
        { "초월_이재윤_AD", new[] { "hawkins" } },   // PM 배정
        { "제한_전법규", new[] { "enel" } },   // PM 배정
        { "다른세계_김건부", new[] { "Aokiji" } },   // PM 배정
        { "다른세계_고죠_사토루", new[] { "KujoJotrao_S" } },   // PM 배정
        { "다른세계_나나미_치아키", new[] { "Yukari_D2" } },   // PM 배정
        { "다른세계_모리야_스와코", new[] { "Kikyo_E02" } },   // PM 배정
        { "다른세계_무면허_라이더", new[] { "KujoJotaroE1" } },   // PM 배정
        { "다른세계_브로리", new[] { "ichigo_dark3" } },   // PM 배정
        { "다른세계_올마이트", new[] { "Byakuya02A" } },   // PM 배정
        { "다른세계_한마_유지로", new[] { "smoker" } },   // PM 배정
        { "다른세계_호시노_루비", new[] { "uta_s2" } },   // PM 배정
        { "랜덤_미도리야_이즈쿠", new[] { "HigmaW_Shot_Long" } },   // PM 배정
        { "랜덤_손오공", new[] { "Yukari_R2" } },   // PM 배정
        { "랜덤_이민형", new[] { "TatsumakiT3B2" } },   // PM 배정
        { "랜덤_이즈미_신이치", new[] { "Kikyo_T00" } },   // PM 배정
        { "랜덤_이타도리_유지", new[] { "MinatoE3" } },   // PM 배정
        { "랜덤_카마도_탄지로", new[] { "RyougiR2" } },   // PM 배정
        { "랜덤_한마_바키", new[] { "Byakuya02B" } },   // PM 배정
        { "랜덤_모몬가", new[] { "ichigo_dark3" } },   // PM 배정
        { "랜덤_호시노_아이", new[] { "uta_s1" } },   // PM 배정
    };

    /// <summary>표에 나오는 모든 클립(이름순) — 멀티에서 번호로 넘긴다. 양쪽 빌드가 같은 표를 싣고 있다.</summary>
    static readonly string[] Clips = Table.Values.SelectMany(v => v).Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToArray();

    /// <summary>MP: 호스트에서 난 획득 음성(클립 번호). 싱글에선 구독자가 없다.</summary>
    public static event System.Action<int> Broadcast;

    /// <summary>이 PC에서 실제로 낸 횟수(테스트용).</summary>
    public static int PlayCount { get; private set; }

    static readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>();
    static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        UnitIdentity.OnAcquired -= HandleAcquired;   // 도메인 리로드 없이 다시 들어와도 한 번만
        UnitIdentity.OnAcquired += HandleAcquired;
    }

    public static bool HasVoice(UnitData data) => data != null && Table.ContainsKey(Key(data));

    static string Key(UnitData data) => data.name.Normalize(System.Text.NormalizationForm.FormC);

    static void HandleAcquired(UnitIdentity unit, UnitInventory inventory)
    {
        if (unit == null || unit.Data == null || !Table.TryGetValue(Key(unit.Data), out string[] voices)) return;
        int index = System.Array.IndexOf(Clips, voices[Random.Range(0, voices.Length)]);
        if (index < 0) return;
        Play(index);
        Broadcast?.Invoke(index);
    }

    /// <summary>이 PC에서 바로 낸다(호스트가 넘겨준 번호도 여기로). 음성끼리는 겹쳐 난다 — 원작도 소환마다 새 소리 핸들이다.</summary>
    public static void Play(int index)
    {
        if (!GameSound.Enabled || index < 0 || index >= Clips.Length) return;
        string name = Clips[index];
        if (!loaded.TryGetValue(name, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning($"[소리] 획득 음성 {name}: Resources/{Folder}{name} 를 못 찾았습니다.");
            loaded[name] = clip;
        }
        if (clip == null) return;
        if (source == null)
        {
            GameObject host = new GameObject("[SummonVoice]");
            Object.DontDestroyOnLoad(host);
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 2D — 원작 CreateSound(..., is3D=false)
        }
        source.PlayOneShot(clip, GameSound.DefaultVolume);
        PlayCount++;
        if (playsLogged++ < 20) Debug.Log($"[소리] 획득 음성 {name}({clip.length:0.0}초)");
    }

    static int playsLogged;
}
