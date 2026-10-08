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
    [Networked, OnChangedRender(nameof(OnSlotRender))] public int Slot { get; set; }

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
    /// <summary>패배 사유(원작 문구, 색 없음) — 클라 패배 화면이 「왜 졌는지」를 쓴다(GAP 09-27 46).</summary>
    [Networked] public NetworkString<_128> DefeatMessage { get; set; }

    // 2단계 ③: 항법 선택 · 도박 해금/사용 횟수(도박소 칸 표시). 도박 선택지는 NetCatalog.gamblingOptions 순서.
    [Networked] public byte Navigation { get; set; }
    /// <summary>원딜 잠금(GAP 6, 패왕의길 — 네 등급 한 기 뒤 잠김). 클라 HUD 표시용.</summary>
    [Networked] public NetworkBool OneDealLocked { get; set; }
    /// <summary>변화(A0KJ) 남은 회수(플레이어당 2회) — 클라 HUD 표시용, 판정은 호스트.</summary>
    [Networked] public byte TransformUsesLeft { get; set; } = PlayerContext.TransformUsesPerGame;
    [Networked] public int GambleUnlockedMask { get; set; }
    [Networked, Capacity(16)] public NetworkArray<short> GambleUses => default;
    /// <summary>보유 아이템(카탈로그 인덱스+1, 0=빈 칸, 소유 순서) — 친구 화면 아이템 칸용. 호스트가 쓰고 클라가 PlayerContext.ItemInventory에 옮겨 적는다.</summary>
    [Networked, Capacity(16)] public NetworkArray<short> HeldItems => default;
    // 돈 도박 충전식 재고·누적 지급·졸업(구현담당1 a9b6a7c3). 재고 -1 = 재고 없는 옵션, 충전은 「다음까지 남은 초」.
    [Networked, Capacity(16)] public NetworkArray<short> GambleStock => default;
    [Networked, Capacity(16)] public NetworkArray<float> GambleNextSeconds => default;
    [Networked, Capacity(16)] public NetworkArray<int> GamblePayout => default;
    [Networked] public NetworkBool GambleGraduated { get; set; }
    [Networked] public float GambleSwapLock { get; set; }   // 졸업 직후 목재 구입 잠금 남은 초(호스트 기준) — 클라 칸이 같이 잠긴다
    // 도움소 스킬별 쿨다운·재고 충전 남은 초 + 탐색(보물찾기) 남은 쿨타임 — 클라 상점 칸 덮개·글자용(10-04). 도박소와 같이 호스트가 쓰고 클라가 자기 시계로 되짚는다.
    // 스킬 칸 수(12)에 맞춰 최소. 재고 −1 = 재고 없는 스킬.
    [Networked, Capacity(SupportShop.MaxReplicatedSkills)] public NetworkArray<float> SupportCooldown => default;
    [Networked, Capacity(SupportShop.MaxReplicatedSkills)] public NetworkArray<short> SupportStock => default;
    [Networked, Capacity(SupportShop.MaxReplicatedSkills)] public NetworkArray<float> SupportNextSeconds => default;
    [Networked] public float TreasureCooldown { get; set; }
    SupportShop supportShopCache;

    // UnitUpgrades(특성 포인트·해금 특성·강화 레벨) — 특성 버튼·강화소 칸 표시용. 특성은 카탈로그 번호+1(0=빈칸).
    public const int MaxReplicatedTraits = 32;
    [Networked] public int TraitPoints { get; set; }
    [Networked] public int TraitGrantMask { get; set; }
    [Networked, Capacity(MaxReplicatedTraits)] public NetworkArray<short> UnlockedTraits => default;
    [Networked, Capacity(MaxReplicatedTraits)] public NetworkArray<byte> TraitRepeats => default;
    [Networked, Capacity(32)] public NetworkArray<byte> GradeLevels => default;   // 10-06 16→32: 등급 트랙 10 + 영원함 유닛 전용 트랙 8 = 18 > 16(트랙 순서 = NetCatalog.gradeTracks)
    [Networked, Capacity(8)] public NetworkArray<byte> AttackTypeLevels => default;

    /// <summary>끊김 유예 남은 초(0 = 연결돼 있음). 호스트 NetSession이 쓴다 — 팀판 「연결 끊김 N초」.</summary>
    [Networked] public float GraceLeft { get; set; }

    /// <summary>퇴치 의뢰 타이머 글(구매자 본인 PC의 QuestTimerPanel이 읽는다) — 호스트 PirateQuestManager가 초 단위로 써 준다.</summary>
    [Networked] public NetworkString<_128> QuestTimerText { get; set; }

    public bool IsReadyForStart => IsHost || Ready;

    /// <summary>그 슬롯이 끊김 유예 중이면 남은 초(올림), 아니면 0. GameHud 팀판이 읽는다.</summary>
    public static int GraceSecondsFor(int slot)
    {
        foreach (NetPlayer player in all)
            if (player != null && player.Slot == slot && player.GraceLeft > 0f) return Mathf.CeilToInt(player.GraceLeft);
        return 0;
    }

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

    // 대기실 자리 이동(사장님 10-06 「자리를 오갈 수 있게」): 호스트가 Slot을 바꾸면 모든 PC가 이 값을 따라간다 — 좌석 집합·내 번호·자리별 캐시.
    void OnSlotRender() => ApplySlotChange(Slot);

    public void ApplySlotChange(int newSlot)
    {
        if (cachedSlot < 0 || newSlot == cachedSlot) return;
        int old = cachedSlot;
        MatchConfig.RemoveSlot(old);
        cachedSlot = newSlot;
        MatchConfig.AddSlot(newSlot);
        if (Runner != null && Runner.IsServer) MatchConfig.MoveSubmittedSave(old, newSlot);
        supportShopCache = null;
        if (HasInputAuthority) LocalPlayer.LocalPlayerId = newSlot;
        Debug.Log($"[자리] 슬롯 {old} → {newSlot} (내 것 {HasInputAuthority}, 호스트 {IsHost}) · 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots)}}}");
    }

    public override void Spawned()
    {
        all.Add(this);
        cachedSlot = Slot;
        Runner.MakeDontDestroyOnLoad(gameObject);
        MatchConfig.AddSlot(Slot);

        // 재접속(판 도중에 들어옴): 게임 씬이 먼저 떠서 PlayerContext.Awake가 이 좌석을 비어 있다고 봤다 — 지금 앉힌다.
        // 판정은 둘 중 하나로: 게임 씬이 이미 있거나(PlayerContext가 있음) 판이 시작됐다고 복제돼 있거나 — 생성 순서가 어느 쪽이어도.
        PlayerContext seat = PlayerContext.Get(Slot);
        bool lateJoin = seat != null || (NetGameState.Instance != null && NetGameState.Instance.Started);
        if (seat != null && !seat.IsOccupied) seat.SetOccupied(true);

        if (HasInputAuthority)
        {
            Local = this;
            LocalPlayer.LocalPlayerId = Slot;
            if (!lateJoin)
            {
                RPC_SetNickname(NetLauncher.Instance != null ? NetLauncher.Instance.InitialNickname : LoadNickname());
                if (!Runner.IsServer) NetSaves.SubmitOwn(this);   // 호스트 자신은 자기 파일을 그대로 읽는다
            }
            else
            {
                // 이름·세이브 제출값은 호스트가 이미 들고 있다. 카메라는 씬이 뜰 때 슬롯 0 레인을 봤다 — 내 레인으로.
                RtsCameraController cam = FindFirstObjectByType<RtsCameraController>();
                if (cam != null) cam.FocusOnLocalLane();
            }
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
        string questTimer = PirateQuestManager.Instance != null ? PirateQuestManager.Instance.TimerText(Slot) : "";
        if (QuestTimerText.ToString() != questTimer) QuestTimerText = questTimer;
        string reason = context.DefeatMessage ?? "";
        if (DefeatMessage.ToString() != reason) DefeatMessage = reason;

        if (context.NavigationState != null)
        {
            Navigation = (byte)context.NavigationState.Choice;
            if (OneDealLocked != context.NavigationState.OneDealLocked) OneDealLocked = context.NavigationState.OneDealLocked;
        }
        if (TransformUsesLeft != context.TransformUsesLeft) TransformUsesLeft = (byte)context.TransformUsesLeft;   // 변화 남은 회수(클라 HUD용)
        if (context.ItemInventory != null && NetLauncher.Catalog != null)
        {
            var heldNow = context.ItemInventory.Items;
            for (int i = 0; i < 16; i++)
            {
                short code = i < heldNow.Count && heldNow[i] != null ? (short)(NetLauncher.Catalog.IndexOf(heldNow[i]) + 1) : (short)0;
                if (HeldItems[i] != code) HeldItems.Set(i, code);
            }
        }

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
            float lockNow = context.GamblingProgress.SwapLockRemaining;
            if (!Mathf.Approximately(GambleSwapLock, lockNow)) GambleSwapLock = lockNow;
        }

        // 도움소·탐색 쿨다운(호스트가 씀). 값이 바뀐 칸만 쓴다(네트워크 쓰기 최소) — 카운트다운 중엔 틱마다 바뀐다.
        if (supportShopCache == null) supportShopCache = SupportShop.For(Slot);
        if (supportShopCache != null)
        {
            int skillCount = Mathf.Min(supportShopCache.SlotCount, SupportShop.MaxReplicatedSkills);
            for (int i = 0; i < skillCount; i++)
            {
                float cooldown = supportShopCache.ReplicatedCooldownRemaining(i);
                if (SupportCooldown[i] != cooldown) SupportCooldown.Set(i, cooldown);
                supportShopCache.ReplicatedStock(i, out int stock, out float next);
                if (SupportStock[i] != (short)stock) SupportStock.Set(i, (short)stock);
                if (SupportNextSeconds[i] != next) SupportNextSeconds.Set(i, next);
            }
        }
        if (TreasureHunt.Instance != null)
        {
            float treasure = TreasureHunt.Instance.CooldownRemaining(Slot);
            if (TreasureCooldown != treasure) TreasureCooldown = treasure;
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
            for (int i = 0; i < catalog.gradeTracks.Count && i < 32; i++) GradeLevels.Set(i, (byte)upgrades.Level(catalog.gradeTracks[i]));
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
        if (Dead && !context.IsDead) context.MarkDead(DefeatMessage.ToString());

        // 항법은 한 번 고르면 끝 — 클라 쪽 상태에도 같은 선택을 걸어 모달·표시가 맞게 한다(효과는 호스트에서만 의미).
        if (Navigation != 0 && context.NavigationState != null && !context.NavigationState.HasChosen)
            context.NavigationState.TrySelect((NavigationChoice)Navigation);
        if (!HasStateAuthority && TransformUsesLeft != context.TransformUsesLeft) context.ApplyReplicatedTransformUses(TransformUsesLeft);   // 클라: 변화 남은 회수 옮겨 적기
        if (OneDealLocked && context.NavigationState != null && !context.NavigationState.OneDealLocked)
            context.NavigationState.ApplyReplicatedOneDeal(true);

        NetCatalog catalog = NetLauncher.Catalog;
        if (catalog != null && context.GamblingProgress != null)
            for (int i = 0; i < catalog.gamblingOptions.Count && i < 16; i++)
                context.GamblingProgress.ApplyReplicated(catalog.gamblingOptions[i], GambleUses[i], (GambleUnlockedMask & (1 << i)) != 0,
                    GambleStock[i], GambleNextSeconds[i], GamblePayout[i]);
        // 클라: 도움소·탐색 쿨다운을 호스트 값으로 되짚는다(클라 로컬 시계로 따로 돌던 경로를 끊는다).
        if (supportShopCache == null) supportShopCache = SupportShop.For(Slot);
        if (supportShopCache != null)
        {
            int skillCount = Mathf.Min(supportShopCache.SlotCount, SupportShop.MaxReplicatedSkills);
            for (int i = 0; i < skillCount; i++)
                supportShopCache.ApplyReplicated(i, SupportCooldown[i], SupportStock[i], SupportNextSeconds[i]);
        }
        if (TreasureHunt.Instance != null) TreasureHunt.Instance.ApplyReplicatedCooldown(Slot, TreasureCooldown);
        // 졸업은 한 번 — Graduate()를 불러야 도박소가 돈 칸 캐시를 바꾼다(구현담당1 안내).
        if (GambleGraduated && context.GamblingProgress != null && !context.GamblingProgress.Graduated)
            context.GamblingProgress.Graduate();
        if (!HasStateAuthority && context.GamblingProgress != null) context.GamblingProgress.ApplyReplicatedSwapLock(GambleSwapLock);   // 클라: 졸업을 늦게 봐도 잠금은 호스트 남은 초를 따른다
        // 클라: 호스트가 복제한 보유 아이템을 내 아이템 칸에 옮겨 적는다(예전엔 친구 화면 아이템 칸이 항상 비어 있었다).
        if (!HasStateAuthority && catalog != null && context.ItemInventory != null)
        {
            var held = new List<ItemData>(16);
            for (int i = 0; i < 16; i++)
            {
                int idx = HeldItems[i] - 1;
                if (idx >= 0 && idx < catalog.items.Count && catalog.items[idx] != null) held.Add(catalog.items[idx]);
            }
            context.ItemInventory.ApplyReplicated(held);
        }

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
            for (int i = 0; i < catalog.gradeTracks.Count && i < 32; i++) upgrades.ApplyReplicatedLevel(catalog.gradeTracks[i], GradeLevels[i]);
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

    /// <summary>[나가기] 직전에 클라가 보낸다 — 호스트는 이 사람의 퇴장을 끊김이 아닌 나감으로 보고 바로 정리한다.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_LeavingOnPurpose()
    {
        if (Runner.TryGetComponent(out NetSession session)) session.MarkLeavingOnPurpose(Object.InputAuthority);
    }

    /// <summary>대기실에서 빈 자리로 옮겨 달라는 요청 — 호스트가 판이 시작 전·빈 자리인지 확인하고 옮긴다(동시에 온 요청은 먼저 처리된 쪽만).</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestSlot(int slot)
    {
        if (NetGameState.Instance != null && NetGameState.Instance.Started) { Debug.Log($"[자리] 호스트: {Object.InputAuthority}의 {slot + 1}번 요청 거절 — 판이 시작됨"); return; }
        if (Runner.TryGetComponent(out NetSession session)) session.TryMoveSlot(Object.InputAuthority, slot);
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

    /// <summary>호스트 → 이 접속자: 그 사람이 받은 처치 골드 「+N」 글자(원작은 받은 플레이어에게만 보임).</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_KillGold(Vector3 worldPos, int amount, bool wood)
    {
        KillGoldPopup.ShowLocal(worldPos, amount, wood);
    }

    /// <summary>호스트 → 이 접속자: 그 사람에게만 나는 소리(GameSound.PlayFor가 원격 슬롯이면 여기로).</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_PlaySound(byte id)
    {
        GameSound.Play((GameSoundId)id);
    }

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
    public void RPC_UseItem(byte useKind)
    {
        NetCommands.ExecuteUseItem(this, useKind);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_HudUnitAction(NetworkId unit, byte action, byte argument)
    {
        NetCommands.ExecuteHudUnitAction(this, unit, (NetHudAction)action, argument);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_CastActiveOnAlly(NetworkId caster, NetworkId ally)
    {
        NetCommands.ExecuteCastActiveOnAlly(this, caster, ally);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_CastActiveAtPoint(NetworkId caster, Vector3 point)
    {
        NetCommands.ExecuteCastActiveAtPoint(this, caster, point);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_CastActiveOnEnemy(NetworkId caster, NetworkId enemy)
    {
        NetCommands.ExecuteCastActiveOnEnemy(this, caster, enemy);
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
    public void RPC_SaveResult(int point, int clear, int best, int level, NetworkBool cleared)
    {
        NetSaves.WriteResult(point, clear, best, level, cleared);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Pause(NetworkBool pause)
    {
        NetCommands.ExecutePause(this, pause);
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
