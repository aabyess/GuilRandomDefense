using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 소리(첫 단계, PM 09-27). 2D 원샷 · 볼륨 하나 · 켜기/끄기(PlayerPrefs 기억).
/// 원작 PlaySoundBJ처럼 「그 플레이어에게만」 — <see cref="PlayFor"/>는 로컬 플레이어가 아니면 이 PC에선 안 낸다.
/// 멀티 호스트에서 친구 앞으로 난 소리는 <see cref="RemoteRouted"/>를 구독한 네트 코드(NetGameState)가 그 친구에게 넘긴다
/// (PlayerNotification.Shown과 같은 방식 — 이 파일은 Fusion을 모른다).
/// 원작은 같은 소리를 다시 낼 때 StopSoundBJ → PlaySoundBJ(겹치지 않고 처음부터) — 소리마다 소스 하나로 같게 한다(Coin).
///
/// 작은 효과음(2026-10-06, 사장님 「괜찮은듯」) — Coin 뒤에 붙인 자리들. 파일은 Assets/Audio/Sfx/Resources/Sfx/&lt;자리&gt;/ 아래
/// 여러 개(blender ~/GRD_sfx, 출처·라이선스는 그 README · Assets/Audio/Sfx/Licenses). Coin과 달리:
///   · 자리마다 폴더의 클립을 다 읽어 두고 낼 때마다 하나를 무작위로 고른다.
///   · 자리별 볼륨(dB, DefaultVolume 기준) — 파일은 전부 피크 −1 dBFS로 같아서 많이 나는 것을 깎는다.
///   · 연타 제한 — 같은 자리 최소 간격 + 1초 안 최대 횟수(평타 셋은 한 묶음으로 센다). 한꺼번에 수십 마리가 맞고 죽어도 안 시끄럽게.
///   · 자리마다 작은 소스 묶음(목소리 몇 개) — 비어 있는 것을 쓰고, 다 차 있으면 가장 오래 쓴 것을 끊는다.
/// 낼 길은 셋: <see cref="Play"/>(이 PC) · <see cref="PlayFor"/>(그 플레이어에게만) · <see cref="PlayAll"/>(전원 — 평타·처치·라운드).
/// ⚠️ 번호는 네트로 byte째 간다(NetPlayer.RPC_PlaySound · NetGameState.RPC_GameSound) — 새 자리는 맨 뒤에만 붙일 것.
/// </summary>
public enum GameSoundId : byte
{
    Coin = 1,   // coinsound.mp3 — 돈 도박·거래(Money_Gemble_1_re · Money_Gemble_3 · Money_trade · Money_trade2)
    HitMelee,   // 평타 적중 — 근접(공격 타입 Normal·Hero 등)
    HitRanged,  // 평타 적중 — 원거리(Pierce·Siege)
    HitMagic,   // 평타 적중 — 마법(Magic·Spells 또는 damageType에 AP)
    EnemyDeath, // 일반 적 처치
    BossDeath,  // 보스(isBoss) 처치
    UiClick,    // 버튼 누름(GameHud가 한 곳에서)
    UiError,    // 「왜 안 되는지」 알림과 함께
    Combine,    // 조합 성공
    Gacha,      // 유닛 뽑기(도박소 유닛도박 · 유닛 포탈)
    Gold,       // 골드 획득(지금은 거는 곳 없음 — 돈 도박은 Coin)
    RoundStart, // 라운드 시작
    BossAppear, // 라운드 보스 등장
    Wood,       // 목재 획득
    UiRattle,   // 10-06 첫 화면 단추에 마우스를 올릴 때 「달그락」(Kenney Impact Sounds impactWood_light, CC0) — 맨 뒤에만 붙인다(MP RPC가 번호를 싣는다)
    CutinWhoosh, // 10-08 상위 등급 획득 컷인 — 슉(캐릭터 미끄러짐 0.25s·퇴장 2.20s). 파일 Resources/Sfx/cutin_whoosh/ (blender 산출 대기 — 없으면 경고만)
    CutinTing,   // 10-08 컷인 칭(흰 띠 0.83s). 파일 Resources/Sfx/cutin_ting/
}

public static class GameSound
{
    public const float DefaultVolume = 0.7f;
    const string EnabledPrefsKey = "GuilRandomDefense.SoundOn";

    /// <summary>MP: 호스트에서 원격 슬롯 앞으로 난 소리(슬롯, 소리). 싱글에선 구독자가 없다.</summary>
    public static event System.Action<int, GameSoundId> RemoteRouted;

    /// <summary>MP: 호스트에서 전원에게 난 소리(<see cref="PlayAll"/>이 연타 제한을 통과한 것만). NetGameState가 클라 전원에게 넘긴다.</summary>
    public static event System.Action<GameSoundId> Broadcast;

    // ── 효과음 자리(Coin 말고 전부) ──
    //   folder: Resources/Sfx/<folder> · db: DefaultVolume 기준 · gap: 같은 묶음 최소 간격(초) · perSecond: 1초 안 최대(0 = 제한 없음)
    //   group: 연타 제한을 같이 세는 묶음(평타 셋 = HitMelee) · voices: 동시에 나는 수
    struct Slot { public string folder; public float db, gap; public int perSecond, voices; public GameSoundId group; }

    static Slot S(string folder, float db, float gap, int perSecond, int voices, GameSoundId group)
        => new Slot { folder = folder, db = db, gap = gap, perSecond = perSecond, voices = voices, group = group };

    // 볼륨은 blender README 권장값(enemy_death −8 · ui_click −6 · hit_* −4 · 나머지 0). 10-07 친구 피드백 「보스·스토리 깰 때 터지는 소리가 크다」 → boss_death −7.
    // 연타 제한: 평타 셋 합쳐 초당 12 · 간격 0.05 / 일반 처치 초당 8 · 간격 0.05 / 나머지 간격 0.1.
    static readonly Dictionary<GameSoundId, Slot> Slots = new Dictionary<GameSoundId, Slot>
    {
        { GameSoundId.HitMelee,   S("hit_melee",   -4f, 0.05f, 12, 4, GameSoundId.HitMelee) },
        { GameSoundId.HitRanged,  S("hit_ranged",  -4f, 0.05f, 12, 4, GameSoundId.HitMelee) },
        { GameSoundId.HitMagic,   S("hit_magic",   -4f, 0.05f, 12, 4, GameSoundId.HitMelee) },
        { GameSoundId.EnemyDeath, S("enemy_death", -8f, 0.05f,  8, 4, GameSoundId.EnemyDeath) },
        { GameSoundId.BossDeath,  S("boss_death", -13f, 0.1f,   0, 2, GameSoundId.BossDeath) },
        { GameSoundId.UiClick,    S("ui_click",    -6f, 0.1f,   0, 2, GameSoundId.UiClick) },
        { GameSoundId.UiError,    S("ui_error",     0f, 0.1f,   0, 2, GameSoundId.UiError) },
        { GameSoundId.Combine,    S("combine",      0f, 0.1f,   0, 2, GameSoundId.Combine) },
        { GameSoundId.Gacha,      S("gacha",       -6f, 0.1f,   0, 2, GameSoundId.Gacha) },
        { GameSoundId.Gold,       S("gold",         0f, 0.1f,   0, 2, GameSoundId.Gold) },
        { GameSoundId.RoundStart, S("round_start", -6f, 0.1f,   0, 2, GameSoundId.RoundStart) },
        { GameSoundId.BossAppear, S("boss_appear", -6f, 0.1f,   0, 2, GameSoundId.BossAppear) },
        { GameSoundId.Wood,       S("wood",        -6f, 0.1f,   0, 2, GameSoundId.Wood) },
        { GameSoundId.UiRattle,   S("ui_rattle",   -8f, 0.06f,  0, 2, GameSoundId.UiRattle) },
        { GameSoundId.CutinWhoosh, S("cutin_whoosh", -8f, 0.1f, 0, 2, GameSoundId.CutinWhoosh) },
        { GameSoundId.CutinTing,   S("cutin_ting",   -8f, 0.1f, 0, 2, GameSoundId.CutinTing) },
    };

    class Pool { public AudioClip[] clips; public AudioSource[] sources; public int next; public float volume; }
    class Throttle { public float last = float.NegativeInfinity; public float[] window; public int head; }

    static readonly Dictionary<GameSoundId, Pool> pools = new Dictionary<GameSoundId, Pool>();
    static readonly Dictionary<GameSoundId, Throttle> throttles = new Dictionary<GameSoundId, Throttle>();

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
            if (!value)
            {
                foreach (AudioSource source in sources.Values) if (source != null) source.Stop();
                foreach (Pool pool in pools.Values) foreach (AudioSource source in pool.sources) if (source != null) source.Stop();
            }
        }
    }

    /// <summary>그 플레이어에게만 — 이 PC의 플레이어면 여기서 내고, 아니면(멀티 호스트) 그 사람에게 넘긴다.</summary>
    public static void PlayFor(int playerId, GameSoundId id)
    {
        if (playerId == LocalPlayer.LocalPlayerId) Play(id);
        else RemoteRouted?.Invoke(playerId, id);
    }

    /// <summary>
    /// 전원에게(평타 적중·처치·라운드·보스) — 호스트·싱글에서 부른다. 연타 제한을 **여기서 먼저** 보고, 통과한 것만 이 PC에서 내고
    /// 멀티 호스트면 <see cref="Broadcast"/>로 클라 전원에게 넘긴다(넘기는 양도 제한값 이하 — 평타 초당 12 + 처치 초당 8이 상한).
    /// 협동 게임이라 남의 레인 소리도 들린다(레인별로 가르려면 플레이어마다 따로 세서 보내야 해 망이 무거워진다 — 단순하게 둔다).
    /// </summary>
    public static void PlayAll(GameSoundId id)
    {
        if (!Slots.ContainsKey(id)) { Play(id); if (GameAuthority.IsServer) Broadcast?.Invoke(id); return; }
        if (!TryPass(id)) return;
        PlayVariant(id);
        if (GameAuthority.IsServer) Broadcast?.Invoke(id);
    }

    /// <summary>MP 클라: 호스트가 <see cref="PlayAll"/>에서 넘긴 소리 — 호스트가 이미 연타 제한을 봤으니 다시 안 센다
    /// (패킷이 몰려 도착하면 두 번째 제한에 걸려 소리가 빠진다). 목소리 수 제한만 탄다.</summary>
    public static void PlayBroadcast(GameSoundId id)
    {
        if (Slots.ContainsKey(id)) PlayVariant(id);
        else Play(id);
    }

    /// <summary>이 PC에서 바로 낸다(원격에서 받은 소리도 여기로).</summary>
    // 사장님 10-06 「유닛이 공격하거나 적이 죽을 때 효과음 없애자 — 배경 브금이 안 들린다」: 평타 적중 셋·일반 적 처치는 끈다(보스 처치는 남김).
    //   자리·파일은 그대로 두고 여기서만 막는다 — 다시 켜려면 이 목록에서 빼면 된다.
    static readonly HashSet<GameSoundId> Muted = new HashSet<GameSoundId> { GameSoundId.HitMelee, GameSoundId.HitRanged, GameSoundId.HitMagic, GameSoundId.EnemyDeath };

    public static void Play(GameSoundId id)
    {
        if (Muted.Contains(id)) return;
        if (Slots.ContainsKey(id))
        {
            if (TryPass(id)) PlayVariant(id);
            return;
        }
        if (!Enabled) return;
        AudioSource source = SourceFor(id);
        if (source == null || source.clip == null) return;
        source.Stop();
        source.volume = DefaultVolume * 0.5f * AudioPrefs.SfxVolume;   // 돈 소리 −6dB × 효과음 슬라이더
        source.Play();
        PlayCount++;
        if (playsLogged++ < 20) Debug.Log($"[소리] {id} 재생(볼륨 {source.volume:0.0})");
    }

    static int playsLogged;

    /// <summary>이 PC에서 실제로 낸 횟수(테스트가 「이 결과에 소리가 났나」를 짝지어 본다).</summary>
    public static int PlayCount { get; private set; }

    // 연타 제한 — 통과하면 그 시각을 적는다. 소리를 꺼 둬도 센다(호스트가 꺼 놨다고 클라에 몰아 보내지 않게).
    static bool TryPass(GameSoundId id)
    {
        Slot slot = Slots[id];
        if (!throttles.TryGetValue(slot.group, out Throttle t))
        {
            t = new Throttle();
            if (slot.perSecond > 0)
            {
                t.window = new float[slot.perSecond];
                for (int i = 0; i < t.window.Length; i++) t.window[i] = float.NegativeInfinity;
            }
            throttles[slot.group] = t;
        }
        float now = Time.unscaledTime;
        if (now - t.last < slot.gap) return false;
        // 1초 창: 고리 버퍼의 가장 오래된 칸이 1초 안이면 이미 perSecond번 났다.
        if (t.window != null && now - t.window[t.head] < 1f) return false;
        t.last = now;
        if (t.window != null) { t.window[t.head] = now; t.head = (t.head + 1) % t.window.Length; }
        return true;
    }

    static void PlayVariant(GameSoundId id)
    {
        if (Muted.Contains(id)) return;   // 친구 화면·전체 알림 경로도 같이 막는다
        if (!Enabled) return;
        Pool pool = PoolFor(id);
        if (pool.clips.Length == 0) return;
        AudioSource source = null;
        foreach (AudioSource candidate in pool.sources) if (candidate != null && !candidate.isPlaying) { source = candidate; break; }
        if (source == null)
        {
            source = pool.sources[pool.next];
            pool.next = (pool.next + 1) % pool.sources.Length;
        }
        if (source == null) return;
        source.Stop();
        source.clip = pool.clips[Random.Range(0, pool.clips.Length)];
        source.volume = pool.volume * AudioPrefs.SfxVolume;
        source.Play();
        PlayCount++;
        if (playsLogged++ < 20) Debug.Log($"[소리] {id} 재생 {source.clip.name}(볼륨 {source.volume:0.00})");
    }

    static Pool PoolFor(GameSoundId id)
    {
        if (pools.TryGetValue(id, out Pool existing) && existing.sources[0] != null) return existing;
        EnsureHost();
        Slot slot = Slots[id];
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Sfx/" + slot.folder);
        if (clips.Length == 0) Debug.LogWarning($"[소리] {id}: Resources/Sfx/{slot.folder}/ 에 클립이 없습니다.");
        Pool pool = new Pool
        {
            clips = clips,
            sources = new AudioSource[Mathf.Max(1, slot.voices)],
            volume = DefaultVolume * Mathf.Pow(10f, slot.db / 20f),
        };
        for (int i = 0; i < pool.sources.Length; i++)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 2D — Coin과 같다
            pool.sources[i] = source;
        }
        pools[id] = pool;
        return pool;
    }

    static void EnsureHost()
    {
        if (host != null) return;
        host = new GameObject("[GameSound]");
        Object.DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pools.Clear();
        throttles.Clear();
    }

    static AudioSource SourceFor(GameSoundId id)
    {
        if (sources.TryGetValue(id, out AudioSource existing) && existing != null) return existing;
        EnsureHost();
        AudioClip clip = Paths.TryGetValue(id, out string path) ? Resources.Load<AudioClip>(path) : null;
        if (clip == null) Debug.LogWarning($"[소리] {id}: Resources/{path} 를 못 찾았습니다.");
        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D — 원작 CreateSound(..., is3D=false)
        source.volume = DefaultVolume * 0.5f;   // 10-08 돈 소리(도박·업그레이드 구매) −6dB
        sources[id] = source;
        return source;
    }
}
