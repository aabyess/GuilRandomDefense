using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 접속자 한 명당 하나. 호스트가 스폰하고 그 접속자에게 입력 권한을 준다.
/// 싣는 것: 슬롯(레인) · 대기실 정보(닉네임·준비). 뒤 단계에서 요청 RPC 창구와
/// 플레이어별 [Networked] 상태(골드·목재 등)가 여기에 붙는다.
///
/// 로비(NetBoot)에서 생겨 게임 씬으로 넘어가야 하므로 씬 전환에서 살아남게 한다.
/// </summary>
public class NetPlayer : NetworkBehaviour
{
    public const int MaxNicknameLength = 12;
    public const string NicknamePrefsKey = "GuilRandomDefense.Nickname";

    /// <summary>레인 번호 = PlayerContext.playerId. 호스트가 스폰 직전에 정한다(NetSession).</summary>
    [Networked] public int Slot { get; set; }

    /// <summary>방을 연 사람의 것인가. 호스트는 [준비] 없이 늘 준비된 것으로 본다.</summary>
    [Networked] public NetworkBool IsHost { get; set; }

    [Networked] public NetworkString<_16> Nickname { get; set; }

    [Networked] public NetworkBool Ready { get; set; }

    // ───── 게임 중 복제(1-d): 호스트가 PlayerContext에서 읽어 쓰고, 클라가 자기 쪽 같은 컴포넌트에 적는다 ─────
    // 클라 GameHud는 지금처럼 PlayerContext의 지갑을 읽으면 된다(파일 무수정).
    public const int ResourceSlots = 8;
    [Networked] public int Gold { get; set; }
    [Networked, Capacity(ResourceSlots)] public NetworkArray<int> Resources => default;
    [Networked] public NetworkBool Dead { get; set; }

    // 2단계 ③: 항법 선택 · 도박 해금/사용 횟수(도박소 칸 표시). 도박 선택지는 NetCatalog.gamblingOptions 순서.
    [Networked] public byte Navigation { get; set; }
    [Networked] public int GambleUnlockedMask { get; set; }
    [Networked, Capacity(16)] public NetworkArray<short> GambleUses => default;
    // 돈 도박 충전식 재고·누적 지급·졸업(구현담당1 a9b6a7c3). 재고 -1 = 재고 없는 옵션, 충전은 「다음까지 남은 초」.
    [Networked, Capacity(16)] public NetworkArray<short> GambleStock => default;
    [Networked, Capacity(16)] public NetworkArray<float> GambleNextSeconds => default;
    [Networked, Capacity(16)] public NetworkArray<int> GamblePayout => default;
    [Networked] public NetworkBool GambleGraduated { get; set; }

    // UnitUpgrades(특성 포인트·해금 특성·강화 레벨) — 특성 버튼·강화소 칸 표시용. 특성은 카탈로그 번호+1(0=빈칸).
    public const int MaxReplicatedTraits = 32;
    [Networked] public int TraitPoints { get; set; }
    [Networked] public int TraitGrantMask { get; set; }
    [Networked, Capacity(MaxReplicatedTraits)] public NetworkArray<short> UnlockedTraits => default;
    [Networked, Capacity(MaxReplicatedTraits)] public NetworkArray<byte> TraitRepeats => default;
    [Networked, Capacity(16)] public NetworkArray<byte> GradeLevels => default;
    [Networked, Capacity(8)] public NetworkArray<byte> AttackTypeLevels => default;

    public bool IsReadyForStart => IsHost || Ready;

    public string DisplayName
    {
        get
        {
            string nick = Nickname.ToString();
            return string.IsNullOrWhiteSpace(nick) ? $"플레이어 {Slot + 1}" : nick;
        }
    }

    static readonly List<NetPlayer> all = new List<NetPlayer>();

    public static IReadOnlyList<NetPlayer> All => all;

    /// <summary>이 PC의 NetPlayer. 아직 안 생겼으면 null.</summary>
    public static NetPlayer Local { get; private set; }

    // Despawned에서는 [Networked] 값을 못 읽을 수 있다(hasState=false) — 받아 둔 값을 쓴다.
    int cachedSlot = -1;

    public override void Spawned()
    {
        all.Add(this);
        cachedSlot = Slot;
        Runner.MakeDontDestroyOnLoad(gameObject);
        MatchConfig.AddSlot(Slot);

        if (HasInputAuthority)
        {
            Local = this;
            LocalPlayer.LocalPlayerId = Slot;
            RPC_SetNickname(NetLauncher.Instance != null ? NetLauncher.Instance.InitialNickname : LoadNickname());
            if (!Runner.IsServer) NetSaves.SubmitOwn(this);   // 호스트 자신은 자기 파일을 그대로 읽는다
        }

        Debug.Log($"[MP] NetPlayer 생성: 슬롯 {Slot} (접속자 {Object.InputAuthority}, 내 것 {HasInputAuthority}, 호스트 {IsHost}) · 현재 슬롯 {{{string.Join(",", MatchConfig.OccupiedSlots)}}}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        PlayerContext context = PlayerContext.Get(Slot);
        if (context == null) return;   // 대기실(게임 씬 전)

        if (context.GoldWallet != null) Gold = context.GoldWallet.Gold;
        if (context.ResourceWallet != null)
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
                if ((int)type < ResourceSlots) Resources.Set((int)type, context.ResourceWallet.Get(type));
        Dead = context.IsDead;

        if (context.NavigationState != null) Navigation = (byte)context.NavigationState.Choice;

        NetCatalog catalog = NetLauncher.Catalog;
        if (catalog != null && context.GamblingProgress != null)
        {
            int mask = 0;
            for (int i = 0; i < catalog.gamblingOptions.Count && i < 16; i++)
            {
                GamblingOptionData option = catalog.gamblingOptions[i];
                if (context.GamblingProgress.IsUnlocked(option)) mask |= 1 << i;
                GambleUses.Set(i, (short)context.GamblingProgress.UsesSoFar(option));
                GambleStock.Set(i, (short)(option.stockMax > 0 ? Mathf.Min(short.MaxValue, context.GamblingProgress.Stock(option)) : -1));
                GambleNextSeconds.Set(i, context.GamblingProgress.SecondsToNextStock(option));
                GamblePayout.Set(i, context.GamblingProgress.CumulativePayout(option));
            }
            GambleUnlockedMask = mask;
            GambleGraduated = context.GamblingProgress.Graduated;
        }

        UnitUpgrades upgrades = context.UnitUpgrades;
        if (catalog != null && upgrades != null)
        {
            TraitPoints = upgrades.TraitPoints;
            TraitGrantMask = upgrades.GrantedPointMask;
            int n = 0;
            foreach (UnitTraitData trait in upgrades.UnlockedTraits)
            {
                if (n >= MaxReplicatedTraits) break;
                int index = catalog.IndexOf(trait);
                if (index < 0) continue;
                UnlockedTraits.Set(n, (short)(index + 1));
                TraitRepeats.Set(n, (byte)Mathf.Min(255, upgrades.RepeatablePurchaseCount(trait)));
                n++;
            }
            for (; n < MaxReplicatedTraits; n++) { UnlockedTraits.Set(n, 0); TraitRepeats.Set(n, 0); }
            for (int i = 0; i < catalog.gradeTracks.Count && i < 16; i++) GradeLevels.Set(i, (byte)upgrades.Level(catalog.gradeTracks[i]));
            for (int i = 0; i < catalog.attackTypeTracks.Count && i < 8; i++) AttackTypeLevels.Set(i, (byte)upgrades.Level(catalog.attackTypeTracks[i]));
        }
    }

    public override void Render()
    {
        if (HasStateAuthority) return;   // 호스트는 실물이 곧 값이다

        PlayerContext context = PlayerContext.Get(Slot);
        if (context == null) return;

        if (context.GoldWallet != null) context.GoldWallet.ApplyReplicated(Gold);
        if (context.ResourceWallet != null)
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
                if ((int)type < ResourceSlots) context.ResourceWallet.ApplyReplicated(type, Resources[(int)type]);
        if (Dead && !context.IsDead) context.MarkDead();

        // 항법은 한 번 고르면 끝 — 클라 쪽 상태에도 같은 선택을 걸어 모달·표시가 맞게 한다(효과는 호스트에서만 의미).
        if (Navigation != 0 && context.NavigationState != null && !context.NavigationState.HasChosen)
            context.NavigationState.TrySelect((NavigationChoice)Navigation);

        NetCatalog catalog = NetLauncher.Catalog;
        if (catalog != null && context.GamblingProgress != null)
            for (int i = 0; i < catalog.gamblingOptions.Count && i < 16; i++)
                context.GamblingProgress.ApplyReplicated(catalog.gamblingOptions[i], GambleUses[i], (GambleUnlockedMask & (1 << i)) != 0,
                    GambleStock[i], GambleNextSeconds[i], GamblePayout[i]);
        // 졸업은 한 번 — Graduate()를 불러야 도박소가 돈 칸 캐시를 바꾼다(구현담당1 안내).
        if (GambleGraduated && context.GamblingProgress != null && !context.GamblingProgress.Graduated)
            context.GamblingProgress.Graduate();

        UnitUpgrades upgrades = context.UnitUpgrades;
        if (catalog != null && upgrades != null)
        {
            upgrades.ApplyReplicatedPoints(TraitPoints, TraitGrantMask);
            // 해금 특성: 호스트 목록에 있는 것만 켜고, 전에 켰는데 빠진 것은 끈다(해금은 되돌리지 않지만 안전하게).
            replicatedTraitBuffer.Clear();
            for (int n = 0; n < MaxReplicatedTraits; n++)
            {
                int stored = UnlockedTraits[n];
                if (stored <= 0) continue;
                UnitTraitData trait = stored - 1 < catalog.traits.Count ? catalog.traits[stored - 1] : null;
                if (trait == null) continue;
                replicatedTraitBuffer.Add(trait);
                upgrades.ApplyReplicatedTrait(trait, true, TraitRepeats[n]);
            }
            for (int i = 0; i < catalog.gradeTracks.Count && i < 16; i++) upgrades.ApplyReplicatedLevel(catalog.gradeTracks[i], GradeLevels[i]);
            for (int i = 0; i < catalog.attackTypeTracks.Count && i < 8; i++) upgrades.ApplyReplicatedLevel(catalog.attackTypeTracks[i], AttackTypeLevels[i]);
        }
    }

    readonly List<UnitTraitData> replicatedTraitBuffer = new List<UnitTraitData>();

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        all.Remove(this);
        MatchConfig.RemoveSlot(cachedSlot);
        if (Local == this) Local = null;

        Debug.Log($"[MP] NetPlayer 제거: 슬롯 {cachedSlot}");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetNickname(string nickname)
    {
        Nickname = SanitizeNickname(nickname);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        // 판이 시작된 뒤엔 준비 상태를 바꿀 일이 없다.
        if (NetGameState.Instance != null && NetGameState.Instance.Started) return;
        Ready = ready;
    }

    // ───────────── 게임 중 요청(1-c~) ─────────────

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Move(NetworkId target, Vector3 groundPoint)
    {
        NetCommands.ExecuteMove(this, target, groundPoint);
    }

    /// <summary>호스트 → 이 접속자: 그 사람 앞으로 온 안내(PlayerNotification)를 넘긴다. 등급색 태그 그대로.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_Notify(string message, float duration)
    {
        PlayerNotification.Show(LocalPlayer.LocalPlayerId, message, duration);
        if (notificationsLogged++ < 10) Debug.Log($"[MP] 알림 받음: {message}");
    }

    static int notificationsLogged;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_UnitCommand(NetworkId unit, byte command, NetworkId enemy, Vector3 point)
    {
        NetCommands.ExecuteUnitCommand(this, unit, (NetUnitCommand)command, enemy, point);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Combine(short recipe, NetworkId caster)
    {
        NetCommands.ExecuteCombine(this, recipe, caster);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ShopUse(short shop, byte slot, byte targetKind, Vector3 point, NetworkId target)
    {
        NetCommands.ExecuteShopUse(this, shop, slot, targetKind, point, target);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_HudUnitAction(NetworkId unit, byte action, byte argument)
    {
        NetCommands.ExecuteHudUnitAction(this, unit, (NetHudAction)action, argument);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_TraitTarget(short trait, NetworkId target)
    {
        NetCommands.ExecuteTraitTarget(this, trait, target);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SubmitSave(int point, int clear, int best, int level)
    {
        NetSaves.Receive(this, point, clear, best, level);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_SaveResult(int point, int clear, int best, int level)
    {
        NetSaves.WriteResult(point, clear, best, level);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Chat(string text)
    {
        NetCommands.ExecuteChat(this, text);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Warehouse(NetworkId unit)
    {
        NetCommands.ExecuteWarehouse(this, unit);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Navigation(byte choice)
    {
        NetCommands.ExecuteNavigation(this, (NavigationChoice)choice);
    }

    public static string LoadNickname()
    {
        try { return PlayerPrefs.GetString(NicknamePrefsKey, ""); }
        catch { return ""; }
    }

    public static void SaveNickname(string nickname)
    {
        try
        {
            PlayerPrefs.SetString(NicknamePrefsKey, SanitizeNickname(nickname));
            PlayerPrefs.Save();
        }
        catch { /* 편의값이라 못 저장해도 판에는 영향 없다 */ }
    }

    public static string SanitizeNickname(string nickname)
    {
        string trimmed = (nickname ?? "").Trim().Replace("\n", "").Replace("\r", "");
        return trimmed.Length > MaxNicknameLength ? trimmed.Substring(0, MaxNicknameLength) : trimmed;
    }
}
