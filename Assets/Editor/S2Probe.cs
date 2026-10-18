using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// S2 규칙 9종 실측(10-18 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:S2Probe.Transfer / Fortune / Debe / Dango / Treasure / Evolution / EvolutionKills / Oro / OroReport / Modes / Egghead
/// 결과는 문자열로 outbox에 실린다. 판 시작 직후(위습·레인 준비 뒤) 부른다.
/// </summary>
public static class S2Probe
{
    static PlayerContext P(int i) { PlayerContext c = PlayerContext.Get(i); if (c != null && !c.IsOccupied) c.SetOccupied(true); return c; }
    static UnitData Roster(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static GameObject Spawn(string n, int owner) { UnitData d = Roster(n); return Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, LaneMarker.Get(owner).TakeSpawnPosition(d), owner); }
    static int Wood(int i) => P(i).ResourceWallet.Get(ResourceType.Wood);
    static int Gold(int i) => P(i).GoldWallet.Gold;
    static int WispCount() => Wisp.Active.Count;

    public static string Transfer()
    {
        var sb = new StringBuilder(); P(0); P(1);
        P(0).GoldWallet.Add(1000); P(0).ResourceWallet.Add(ResourceType.Wood, 5);
        int g0 = Gold(0), g1 = Gold(1), w0 = Wood(0), w1 = Wood(1);
        sb.AppendLine($"   전: P0 엔 {g0} 목재 {w0} / P1 엔 {g1} 목재 {w1}");
        sb.AppendLine($"   엔 500 → {ResourceTransfer.ExecuteOnAuthority(0, 1, ResourceTransfer.Kind.Gold, 500) ?? "OK"}");
        sb.AppendLine($"   목재 3 → {ResourceTransfer.ExecuteOnAuthority(0, 1, ResourceTransfer.Kind.Wood, 3) ?? "OK"}");
        sb.AppendLine($"   부족(엔 99999) → {ResourceTransfer.ExecuteOnAuthority(0, 1, ResourceTransfer.Kind.Gold, 99999)}");
        sb.AppendLine($"   자기 자신 → {ResourceTransfer.ExecuteOnAuthority(0, 0, ResourceTransfer.Kind.Gold, 1)}");
        sb.AppendLine($"   0개 → {ResourceTransfer.ExecuteOnAuthority(0, 1, ResourceTransfer.Kind.Gold, 0)}");
        sb.AppendLine($"   후: P0 엔 {Gold(0)}({Gold(0) - g0}) 목재 {Wood(0)}({Wood(0) - w0}) / P1 엔 {Gold(1)}({Gold(1) - g1}) 목재 {Wood(1)}({Wood(1) - w1})");
        ResourceTransfer.Kind k; int a;
        sb.AppendLine($"   파싱: 「-골드 500」{ResourceTransfer.TryParse("-골드 500", out k, out a)}/{k}/{a} · 「-금 7」{ResourceTransfer.TryParse("-금 7", out k, out a)}/{k}/{a} · 「-목재 2」{ResourceTransfer.TryParse("-목재 2", out k, out a)}/{k}/{a} · 「-금액」{ResourceTransfer.TryParse("-금액", out k, out a)} · 「안녕」{ResourceTransfer.TryParse("안녕", out k, out a)}");
        return sb.ToString();
    }

    public static string Fortune()
    {
        var sb = new StringBuilder(); P(0);
        S2Fortune.ResetMatch();
        // 굴림 고정: 단계별 d100 값을 순서대로 — 대길(1) / 희귀(첫째 실패·둘째 3 이하) 등을 만들려고 호출 번호로 분기한다.
        string[] names = { "대길(2%)", "희귀위습(≤3)", "특별위습(≤5)", "안흔함위습(≤12)", "랜덤위습(≤25)", "실패(99)" };
        int[] wanted = { 1, 3, 5, 12, 25, 99 };
        for (int i = 0; i < wanted.Length; i++)
        {
            S2Fortune.ResetMatch();
            int before = WispCount(), calls = 0, target = wanted[i];
            // 앞 단계는 통과 못 하게(목표보다 큰 값), 목표 단계에서 목표값 — 호출 순서: 2→3→5→12→25
            int[] thresholds = { 2, 3, 5, 12, 25 };
            int stage = System.Array.FindIndex(thresholds, t => target <= t);
            string err = S2Fortune.Cast(0, _ => (calls++ == stage ? target : 100), n => 1);
            sb.AppendLine($"   {names[i]}: 위습 {before}→{WispCount()} · 오류 {err ?? "-"} · 결과 {Strip(S2Fortune.LastResult)}");
        }
        S2Fortune.ResetMatch();
        S2Fortune.Cast(0, _ => 100, n => 2);   // 재시전 당첨
        sb.AppendLine($"   1/3 재시전: 바로 다시 → {S2Fortune.Cast(0, _ => 100, n => 1) ?? "다시 점쳐짐(쿨 없음)"}");
        sb.AppendLine($"   쿨 중: {S2Fortune.Cast(0, _ => 100, n => 1) ?? "점쳐짐"}");
        return sb.ToString();
    }

    static string Strip(string s) => s == null ? "-" : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("\n", " / ");

    public static string Debe()
    {
        var sb = new StringBuilder(); P(0); S2DebeBack.ResetMatch();
        string special = UnityEditor.AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Select(p => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(p)).First(u => u.grade == UnitGrade.Special && u.prefab != null).name;
        for (int i = 0; i < 4; i++)
        {
            GameObject go = Spawn(special, 0);
            int units = UnitIdentity.Active.Count;
            string err = S2DebeBack.Execute(go.GetComponent<Selectable>(), i == 1 ? 80 : 10);   // 10 → 패배 / 80 → 승리
            sb.AppendLine($"   {i + 1}번째 ({(i == 1 ? "승리 굴림" : "패배 굴림")}): 오류 {err ?? "-"} · 결과 {S2DebeBack.LastResult} · 남은 횟수 {S2DebeBack.TriesLeft(0)}");
        }
        GameObject normal = Spawn("흔함_박민수", 0);
        sb.AppendLine($"   특별 아닌 유닛 → {S2DebeBack.Execute(normal.GetComponent<Selectable>(), 10)}");
        return sb.ToString();
    }

    public static string Dango()
    {
        var sb = new StringBuilder(); P(0); P(1); S2Dango.ResetMatch();
        GameObject rare = Spawn("안흔함_박민수", 1) ?? Spawn("흔함_박민수", 1);
        string name = rare.GetComponent<UnitIdentity>().Data.name;
        int w0 = Wood(0), w1 = Wood(1);
        sb.AppendLine($"   요청: {S2Dango.Request(0, rare.GetComponent<Selectable>()) ?? "OK"} ({name} 주인 P1)");
        sb.AppendLine($"   두 번째 요청(동시 1건): {S2Dango.Request(2, rare.GetComponent<Selectable>())}");
        sb.AppendLine($"   엉뚱한 사람 수락: {S2Dango.TryRespond(0, "-수락")}");
        sb.AppendLine($"   P1 수락: [{S2Dango.TryRespond(1, "-수락")}] 결과 {S2Dango.LastResult} · P0 목재 {w0}→{Wood(0)} · P1 목재 {w1}→{Wood(1)}");
        int p0 = UnitIdentity.Active.Count(u => u.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 0 && u.Data != null && u.Data.name == name);
        int p1 = UnitIdentity.Active.Count(u => u.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 1 && u.Data != null && u.Data.name == name);
        sb.AppendLine($"   {name}: P0 {p0}기 · P1 {p1}기 (파괴는 프레임 끝 — P1이 1이면 아직 안 사라짐)");
        sb.AppendLine($"   스킬 재사용: {S2Dango.Request(0, rare.GetComponent<Selectable>())}");
        return sb.ToString();
    }

    public static string Treasure()
    {
        var sb = new StringBuilder(); P(0); S2Treasure.ResetMatch();
        int[] rolls = { 10, 30, 50, 97, 97, 97, 97, 97, 97 };
        foreach (int r in rolls)
        {
            int g = Gold(0), w = Wood(0), wisps = WispCount();
            string res = S2Treasure.Open(0, r);
            sb.AppendLine($"   굴림 {r} → {res} (엔 {Gold(0) - g:+#;-#;0} · 목재 {Wood(0) - w:+#;-#;0} · 위습 {WispCount() - wisps:+#;-#;0})");
        }
        sb.AppendLine($"   원피스 보유 {S2Treasure.HasOnePiece(0)} · 폭발형 추가 레벨 {S2Treasure.ExtraExplosiveLevels(0)}");
        return sb.ToString();
    }

    public static string Evolution()
    {
        var sb = new StringBuilder(); P(0); S2Evolution.Reset();
        WispData w = S2Evolution.Wisp;
        sb.AppendLine($"   Resources 진화 위습: {(w == null ? "❌ 없음" : w.wispName + " isEvolution=" + w.isEvolution)}");
        Wisp wisp = Wisp.Active.FirstOrDefault(x => x.Data != null && x.Data.isEvolution);
        sb.AppendLine($"   시작 진화 위습 필드에: {(wisp != null ? "있음" : "없음")}");
        UnitPortal any = Object.FindFirstObjectByType<UnitPortal>();
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        int units = UnitIdentity.Active.Count;
        bool ok = wisp != null && S2Evolution.OnWispUsed(0, wisp, any != null ? any.Table : null, spawner);
        sb.AppendLine($"   포탈 사용: {ok} · 유닛 {units}→{UnitIdentity.Active.Count} (흔함 1기) · 실루엣 {(GameObject.Find("진화체 실루엣") != null ? "있음" : "없음")}");
        return sb.ToString();
    }

    public static string EvolutionKills()
    {
        for (int i = 0; i < S2Evolution.NeededKills; i++) S2Evolution.OnLaneKill(0);
        return $"   처치 {S2Evolution.NeededKills}번 입력 — 3초 뒤 유닛 수 확인(실루엣 {(GameObject.Find("진화체 실루엣") != null ? "아직" : "없음")}) · 현재 유닛 {UnitIdentity.Active.Count}";
    }

    public static string Oro()
    {
        OroSeong.ForceForTest(OroSeong.Saint.Saturn, false);
        return $"   실루엣 세움: {OroSeong.Name(OroSeong.Chosen)} · 실루엣 오브젝트 {(GameObject.Find("오로성 실루엣 새턴") != null ? "있음" : "없음")}";
    }

    public static string OroFire()
    {
        OroSeong.OnNewWorldEnter();
        return "   신세계 진입 호출";
    }

    public static string OroReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"   활성 {OroSeong.Active} · 선택 {OroSeong.Name(OroSeong.Chosen)} · 점수판 {OroSeong.ScoreboardLine}");
        sb.AppendLine($"   아군 공격력 {OroSeong.AllyAttackPowerPercent:P0} · 폭발형 ×{OroSeong.AllyExplosiveFactor:F2} · 공속 ×{OroSeong.AllyAttackSpeedFactor:F2}");
        sb.AppendLine($"   적 방어 +{OroSeong.EnemyArmorBonus} · 마법 ×{OroSeong.EnemyMagicTakenFactor:F2} · 이속 ×{OroSeong.EnemyMoveSpeedFactor:F2} · 재생 +{OroSeong.EnemyRegenBonus:N0} · 체력 +{OroSeong.EnemyHpBonus(false, true):N0}(라인몹) / +{OroSeong.EnemyHpBonus(true, false):N0}(보스)");
        EnemyDummy e = EnemyDummy.Active.FirstOrDefault(x => x != null);
        if (e != null) sb.AppendLine($"   적 한 마리: MaxHp {e.MaxHp:N0}");
        return sb.ToString();
    }

    public static string OroDismiss()
    {
        OroSeong.Dismiss();
        return $"   에메트: 활성 {OroSeong.Active} · 퇴치 {OroSeong.Dismissed}";
    }

    public static string Modes()
    {
        var sb = new StringBuilder();
        GameModes.ResetMatch();
        GameModes.Choose(DifficultyMode.Normal, true, false);
        sb.AppendLine($"   보통에서 선택(무시돼야): 변질 {GameModes.Altered} 이세계 {GameModes.OtherWorld}");
        GameModes.Choose(DifficultyMode.Hell, true, false);
        sb.AppendLine($"   지옥에서 선택: 변질 {GameModes.Altered} 이세계 {GameModes.OtherWorld} · 밴 {GameModes.Banned.Count}개: {string.Join(", ", GameModes.Banned.Take(6))}…");
        CombineRecipe banned = Object.FindFirstObjectByType<CombineSystem>().Recipes.FirstOrDefault(r => GameModes.IsBanned(r));
        sb.AppendLine($"   밴 조합식 예: {(banned != null ? banned.commandId : "-")}");
        GamblingOptionData opt = UnityEditor.AssetDatabase.LoadAssetAtPath<GamblingOptionData>("Assets/Data/Gambling/Gambling_랜덤유닛 도박.asset");
        sb.AppendLine($"   이세계 도박 잠김: {GameModes.BlocksGambling(opt)}");
        return sb.ToString();
    }

    public static string Egghead()
    {
        StoryData egg = Resources.Load<StoryData>("S2/Story14_에그헤드");
        StoryManager sm = Object.FindFirstObjectByType<StoryManager>();
        int idx = -1; for (int i = 0; sm != null && sm.StoryAt(i) != null; i++) if (sm.StoryAt(i) == egg) idx = i;
        return egg == null ? "   ❌ 에그헤드 자산 없음" : $"   {egg.storyName} 순서 {egg.order} · 목록 위치 {idx} · 대기 {egg.delayAfterPreviousSeconds}초 · 신/악몽만 {egg.godNightmareOnly} · 제한 {egg.timeLimitSeconds}초 · 체력 {egg.building.hp:N0} · 방어 {egg.building.armor}";
    }

    // ── 사진용(패치노트): 카메라를 대상 쪽으로 옮긴다 ──
    static void Cam(Vector3 p) { RtsCameraController c = Object.FindFirstObjectByType<RtsCameraController>(); if (c != null) c.MoveTo(p); }
    public static string Zoom1() { RtsCameraController.TryHandleSightChat("-시야1"); return "   시야1"; }
    public static string Zoom2() { RtsCameraController.TryHandleSightChat("-시야2"); return "   시야2(기본)"; }
    public static string CamSilhouette() { GameObject g = GameObject.Find("진화체 실루엣") ?? GameObject.Find("오로성 실루엣 새턴"); if (g != null) Cam(g.transform.position); return g != null ? "   카메라 → " + g.name : "   실루엣 없음"; }
    public static string CamLane() { LaneMarker l = LaneMarker.Get(0); if (l != null) Cam(l.LaneCenter); return "   카메라 → 레인"; }
    public static string EvoWisp() { Wisp w = Wisp.Active.FirstOrDefault(x => x.Data != null && x.Data.isEvolution); if (w != null) Cam(w.transform.position); return w != null ? "   카메라 → 진화 위습" : "   진화 위습 없음"; }
    public static string SelectEvo() { Wisp w = Wisp.Active.FirstOrDefault(x => x.Data != null && x.Data.isEvolution); return w != null ? "   진화 위습 " + w.transform.position : "없음"; }

    /// 난이도 대화상자 사진용 — 이미 고른 난이도를 되돌리고(리플렉션) 대화상자를 새로 세운다. 사진만 찍고 끝낸다(판은 버림).
    public static string ModeDialog()
    {
        DifficultyManager dm = DifficultyManager.Instance;
        typeof(DifficultyManager).GetField("current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(dm, null);
        foreach (DifficultySelectHud old in Object.FindObjectsByType<DifficultySelectHud>(FindObjectsSortMode.None)) Object.Destroy(old);
        new GameObject("DifficultySelectHud(probe)").AddComponent<DifficultySelectHud>();
        return "   난이도 대화상자 새로 세움";
    }

    // ── 맵 목표물·에그헤드 ──
    public static string Objectives()
    {
        S2MapObjectives o = S2MapObjectives.Instance;
        if (o == null) return "   ❌ S2MapObjectives 없음";
        var sb = new StringBuilder();
        sb.AppendLine($"   황금 종 {o.BellPosition} · 영혼 {o.SoulPosition} · {S2MapObjectives.BellHpText()}");
        foreach (PlayerContext c in PlayerContext.All) { LaneMarker l = LaneMarker.Get(c.PlayerId); if (l != null) sb.AppendLine($"   레인{c.PlayerId} 중심 {l.LaneCenter} (종까지 {Vector3.Distance(l.LaneCenter, o.BellPosition):F0} · 영혼까지 {Vector3.Distance(l.LaneCenter, o.SoulPosition):F0})"); }
        foreach (string n in new[] { "S2목표물_샨도라 황금 종", "S2목표물_룸바 해적단의 영혼" })
        {
            GameObject g = GameObject.Find(n);
            sb.AppendLine($"   {n}: {(g != null ? "있음 " + g.transform.position : "없음")}");
            if (g != null && g.TryGetComponent(out EnemyDummy d)) sb.AppendLine($"      체력 {d.Hp:N0}/{d.MaxHp:N0} · 방어 {d.EffectiveArmor}");
        }
        return sb.ToString();
    }
    public static string CamStory() { Cam(Object.FindFirstObjectByType<StoryManager>().SpawnPosition); return "   카메라 → 스토리존"; }
    public static string CamBell() { Vector3 p = S2MapObjectives.Instance.BellPosition; Cam(p); return "   카메라 → 황금 종 " + p; }
    public static string CamSoul() { Vector3 p = S2MapObjectives.Instance.SoulPosition; Cam(p); return "   카메라 → 영혼 " + p; }
    static string hitLog = "";
    public static string KillSoul()
    {
        S2Objectives.RumbarSoulFreed += p => hitLog += "영혼:" + p + " "; S2Objectives.GoldenBellDestroyed += p => hitLog += "종:" + p + " ";
        GameObject g = GameObject.Find("S2목표물_룸바 해적단의 영혼");
        if (g == null) return "   영혼 없음";
        EnemyDummy d = g.GetComponent<EnemyDummy>();
        d.TakeDamage(20000f, DamageType.AP, AttackType.Spells, 0);
        return $"   영혼에 2만 피해 → 남은 체력 {(d != null ? d.Hp.ToString("N0") : "파괴")} · (이벤트는 다음 프레임)";
    }
    public static string KillReport() => $"   영혼 오브젝트 {(GameObject.Find("S2목표물_룸바 해적단의 영혼") != null ? "아직 있음" : "사라짐")} · 종 이벤트 로그 「{hitLog}」";
    public static string BellDamage() { EnemyDummy d = GameObject.Find("S2목표물_샨도라 황금 종").GetComponent<EnemyDummy>(); d.TakeDamage(1_300_000f, DamageType.AP, AttackType.Spells, 0); return "   황금 종 130만 피해 → " + S2MapObjectives.BellHpText(); }
    public static string EggheadSpawn()
    {
        StoryManager sm = Object.FindFirstObjectByType<StoryManager>();
        StoryData egg = Resources.Load<StoryData>("S2/Story14_에그헤드");
        typeof(StoryManager).GetMethod("Spawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sm, new object[] { egg });
        return $"   에그헤드 소환 · 제한 남은 {sm.SecondsLeftInLimit:F0}초 · 이름 {StoryManager.DisplayNameForEnemy(egg.building)}";
    }
}
