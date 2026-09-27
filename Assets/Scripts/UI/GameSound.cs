using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 소리(첫 단계, PM 09-27). 2D 원샷 · 볼륨 하나 · 켜기/끄기(PlayerPrefs 기억).
/// 원작 PlaySoundBJ처럼 「그 플레이어에게만」 — <see cref="PlayFor"/>는 로컬 플레이어가 아니면 이 PC에선 안 낸다.
/// 멀티 호스트에서 친구 앞으로 난 소리는 <see cref="RemoteRouted"/>를 구독한 네트 코드(NetGameState)가 그 친구에게 넘긴다
/// (PlayerNotification.Shown과 같은 방식 — 이 파일은 Fusion을 모른다).
/// 원작은 같은 소리를 다시 낼 때 StopSoundBJ → PlaySoundBJ(겹치지 않고 처음부터) — 소리마다 소스 하나로 같게 한다.
/// </summary>
public enum GameSoundId : byte
{
    Coin = 1,   // coinsound.mp3 — 돈 도박·거래(Money_Gemble_1_re · Money_Gemble_3 · Money_trade · Money_trade2)
}

public static class GameSound
{
    public const float DefaultVolume = 0.7f;
    const string EnabledPrefsKey = "GuilRandomDefense.SoundOn";

    /// <summary>MP: 호스트에서 원격 슬롯 앞으로 난 소리(슬롯, 소리). 싱글에선 구독자가 없다.</summary>
    public static event System.Action<int, GameSoundId> RemoteRouted;

    static readonly Dictionary<GameSoundId, string> Paths = new Dictionary<GameSoundId, string>
    {
        { GameSoundId.Coin, "Sounds/coinsound" },
    };

    static readonly Dictionary<GameSoundId, AudioSource> sources = new Dictionary<GameSoundId, AudioSource>();
    static GameObject host;
    static bool? enabled;

    public static bool Enabled
    {
        get
        {
            if (enabled == null)
            {
                try { enabled = PlayerPrefs.GetInt(EnabledPrefsKey, 1) != 0; }
                catch { enabled = true; }
            }
            return enabled.Value;
        }
        set
        {
            enabled = value;
            try { PlayerPrefs.SetInt(EnabledPrefsKey, value ? 1 : 0); PlayerPrefs.Save(); }
            catch { /* 편의값 — 못 저장해도 이번 실행엔 적용된다 */ }
            if (!value) foreach (AudioSource source in sources.Values) if (source != null) source.Stop();
        }
    }

    /// <summary>그 플레이어에게만 — 이 PC의 플레이어면 여기서 내고, 아니면(멀티 호스트) 그 사람에게 넘긴다.</summary>
    public static void PlayFor(int playerId, GameSoundId id)
    {
        if (playerId == LocalPlayer.LocalPlayerId) Play(id);
        else RemoteRouted?.Invoke(playerId, id);
    }

    /// <summary>이 PC에서 바로 낸다(원격에서 받은 소리도 여기로).</summary>
    public static void Play(GameSoundId id)
    {
        if (!Enabled) return;
        AudioSource source = SourceFor(id);
        if (source == null || source.clip == null) return;
        source.Stop();
        source.Play();
        PlayCount++;
        if (playsLogged++ < 20) Debug.Log($"[소리] {id} 재생(볼륨 {source.volume:0.0})");
    }

    static int playsLogged;

    /// <summary>이 PC에서 실제로 낸 횟수(테스트가 「이 결과에 소리가 났나」를 짝지어 본다).</summary>
    public static int PlayCount { get; private set; }

    static AudioSource SourceFor(GameSoundId id)
    {
        if (sources.TryGetValue(id, out AudioSource existing) && existing != null) return existing;
        if (host == null)
        {
            host = new GameObject("[GameSound]");
            Object.DontDestroyOnLoad(host);
        }
        AudioClip clip = Paths.TryGetValue(id, out string path) ? Resources.Load<AudioClip>(path) : null;
        if (clip == null) Debug.LogWarning($"[소리] {id}: Resources/{path} 를 못 찾았습니다.");
        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D — 원작 CreateSound(..., is3D=false)
        source.volume = DefaultVolume;
        sources[id] = source;
        return source;
    }
}
