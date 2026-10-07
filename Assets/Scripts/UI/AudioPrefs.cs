using UnityEngine;

/// <summary>
/// 소리 크기 설정(사장님 10-06 「첫 화면에 설정 — 사운드 조절」). 전체 소리 = AudioListener.volume, 배경음악 = GameBgm이 곡 볼륨에 곱한다.
/// PlayerPrefs에 저장하고 첫 씬 전에 다시 적용한다(ScreenMode와 같은 방식). 효과음 켜고 끄기(GameSound.Enabled)와는 별개다.
/// </summary>
public static class AudioPrefs
{
    const string MasterKey = "GuilRandomDefense.MasterVolume";
    const string MusicKey = "GuilRandomDefense.MusicVolume";
    const string SfxKey = "GuilRandomDefense.SfxVolume";

    static float master = 1f, music = 1f, sfx = 1f;
    public static float MasterVolume => master;
    public static float MusicVolume => music;
    /// <summary>효과음 크기(10-08 F10 메뉴 슬라이더) — GameSound·SkillSfx·SummonVoice·문 소리가 소리를 낼 때 곱한다.</summary>
    public static float SfxVolume => sfx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        try
        {
            master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
            music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
        }
        catch { }
        AudioListener.volume = master;
    }

    public static void SetMaster(float value)
    {
        master = Mathf.Clamp01(value);
        AudioListener.volume = master;
        Save(MasterKey, master);
    }

    public static void SetMusic(float value)
    {
        music = Mathf.Clamp01(value);
        Save(MusicKey, music);
    }

    public static void SetSfx(float value)
    {
        sfx = Mathf.Clamp01(value);
        Save(SfxKey, sfx);
    }

    static void Save(string key, float value)
    {
        try { PlayerPrefs.SetFloat(key, value); PlayerPrefs.Save(); } catch { }
    }
}
