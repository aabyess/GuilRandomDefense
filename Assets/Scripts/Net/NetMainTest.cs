using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 0.3.12 후보 멀티 점검(2026-10-06 구현담당1) — 두 창(호스트 + 클라)이 -mpTestMain 으로 같은 시나리오를 돈다. 로그 접두 「[M점검]」.
///   호스트: 친구 슬롯에 김용태·이재윤·유재헌·서민성을 세우고 돈을 주고, 레인 적 하나를 김용태 옆으로 옮겨 유닛회유를 걸고, 조세민 지연 조합 재료를 세운다.
///   클라: ① 내 상점 칸을 화면 칸 0(Q)으로 눌러 본다 ② 비행 유닛에 바다 점 이동 요청 ③ 회유 유닛을 거울에서 찾아 판매 요청 ④ 토토 ⑤ 서민성 강화 ⑥ 조세민 지연 조합 — 결과·알림을 로그로.
/// </summary>
public class NetMainTest : MonoBehaviour
{
    public float startAt = 20f;
    public string shotPrefix;

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(startAt);
        if (GameAuthority.IsServer) yield return HostRoutine(); else yield return ClientRoutine();
    }

    UnitData Load(string rosterName)
    {
        NetCatalog c = NetLauncher.Catalog;
        UnitData d = c != null ? c.units.Find(u => u != null && u.name == rosterName) : null;
        if (d == null) Debug.Log($"[M점검] ❌ 카탈로그에 {rosterName} 없음");
        return d;
    }

    // ───────── 호스트 ─────────
    IEnumerator HostRoutine()
    {
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        PlayerContext friend = PlayerContext.Occupied.FirstOrDefault(c => c.PlayerId != LocalPlayer.LocalPlayerId);
        if (friend == null) { Debug.Log("[M점검] 호스트: 친구 슬롯 없음(혼자)"); yield break; }
        int cs = friend.PlayerId;
        LaneMarker lane = LaneMarker.Get(cs);
        PlayerNotification.Shown += (slot, msg, dur) => Debug.Log($"[M점검] 알림 슬롯 {slot}: {msg}");

        friend.GoldWallet.Add(100000);
        friend.ResourceWallet.Add(ResourceType.Wood, 20);
        Debug.Log($"[M점검] 호스트: 친구 슬롯 {cs}에게 엔 +100000 · 목재 +20");

        UnitIdentity yongtae = Spawn(spawner, "불멸_김용태", cs, lane);
        UnitIdentity jaeyun = Spawn(spawner, "초월_이재윤_AD", cs, lane);
        Spawn(spawner, "초월_유재헌_ADAP", cs, lane);
        Spawn(spawner, "영원_서민성", cs, lane);

        // 조세민 지연 조합 재료(식을 찾아 재료 전부) — 지연을 8초로 줄여 시험
        CombineSystem system = FindFirstObjectByType<CombineSystem>();
        CombineRecipe delayed = null;
        for (int i = 0; system != null && system.RecipeAt(i) != null; i++) if (system.RecipeAt(i).resultDelaySeconds > 0f) { delayed = system.RecipeAt(i); break; }
        if (delayed != null)
        {
            delayed.resultDelaySeconds = 8f;
            delayed.requiredSaveCount = 0;   // 세이브 10회 조건은 시험에서 뺀다(친구 세이브가 비어 있다)
            foreach (RecipeIngredient ing in delayed.ingredients)
                if (ing != null && ing.unit != null)
                    for (int k = 0; k < Mathf.Max(1, ing.count); k++) spawner.Spawn(ing.unit, lane.TakeSpawnPosition(ing.unit), cs);
            Debug.Log($"[M점검] 호스트: 지연 조합 식 「{delayed.result.unitName}」 재료 {delayed.ingredients.Count}종을 친구 슬롯에 세움(지연 8초로 단축)");
        }
        else Debug.Log("[M점검] ❌ 지연 조합 식 없음");

        yield return new WaitForSecondsRealtime(6f);
        // 회유: 일반 적 하나를 김용태 옆으로 → 회유 발동
        if (yongtae != null)
        {
            EnemyDummy enemy = EnemyDummy.Active.FirstOrDefault(e => e != null && !e.IsDead && !e.IsBoss && e.PointValue < 200f);
            if (enemy != null)
            {
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Vector3 to = yongtae.transform.position + new Vector3(30f, 0f, 0f);
                if (agent != null && agent.enabled) agent.Warp(to); else enemy.transform.position = to;
            }
            else Debug.Log("[M점검] ❌ 호스트: 회유할 일반 적이 없음(라운드 적이 아직 안 나옴)");
            SkillData skill = yongtae.Data.skills.FirstOrDefault(s => s != null && s.skillName.StartsWith("유닛회유"));
            var atk = yongtae.GetComponent<UnitAttacker>();
            var m = typeof(UnitAttacker).GetMethod("RecruitNearestEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
            if (skill != null && m != null)
            {
                m.Invoke(atk, new object[] { 99999f, skill.levels[0].effects[0] });
                Debug.Log($"[M점검] 호스트: 회유 발동 — 회유 유닛 {UnitIdentity.Active.Count(u => u != null && u.IsRecruit)}기");
            }
        }
        // 비행 유닛 실물 위치를 주기로 찍는다(클라 거울과 비교)
        for (int t = 0; t < 10; t++)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (jaeyun != null) Debug.Log($"[M점검] 호스트 비행 실물 위치 {jaeyun.transform.position:F0} (y {jaeyun.transform.position.y:F1})");
        }
    }

    UnitIdentity Spawn(UnitSpawner spawner, string rosterName, int cs, LaneMarker lane)
    {
        UnitData d = Load(rosterName);
        if (d == null || spawner == null) return null;
        GameObject go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, cs);
        Debug.Log($"[M점검] 호스트: 슬롯 {cs}에 {rosterName} 세움 {(go != null ? "성공" : "실패")}");
        return go != null ? go.GetComponent<UnitIdentity>() : null;
    }

    // ───────── 클라 ─────────
    NetEntity MirrorOf(string rosterName)
    {
        int me = LocalPlayer.LocalPlayerId;
        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
        {
            if (e == null || e.Object == null || !e.Object.IsValid || e.EntityKind != NetEntityKind.Unit || e.Owner != me || e.Visual == null) continue;
            if (e.Visual.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == rosterName) return e;
        }
        return null;
    }

    IEnumerator ClientRoutine()
    {
        int me = LocalPlayer.LocalPlayerId;
        PlayerContext ctx = PlayerContext.Get(me);
        PlayerNotification.Shown += (slot, msg, dur) => Debug.Log($"[M점검] 클라 알림(슬롯 {slot}): {msg}");
        yield return new WaitForSecondsRealtime(4f);   // 호스트가 세우는 동안
        Debug.Log($"[M점검] 클라: 내 거울 — 김용태 {MirrorOf("불멸_김용태") != null} · 이재윤 {MirrorOf("초월_이재윤_AD") != null} · 유재헌 {MirrorOf("초월_유재헌_ADAP") != null} · 서민성 {MirrorOf("영원_서민성") != null} · 지갑 엔 {ctx.GoldWallet.Gold} 목재 {ctx.ResourceWallet.Get(ResourceType.Wood)}");

        // ② 비행 유닛 바다 점 이동 요청
        NetEntity fly = MirrorOf("초월_이재윤_AD");
        Vector3 sea = SeaPoint();
        if (fly != null)
        {
            Debug.Log($"[M점검] 클라: 이재윤 거울 위치 {fly.Visual.transform.position:F0} → 바다 점 {sea:F0} 이동 요청");
            NetCommands.RequestMove(fly.Visual.transform, sea);
        }
        else Debug.Log("[M점검] ❌ 클라: 이재윤 거울 없음");

        // ④ 토토
        NetEntity toto = MirrorOf("초월_유재헌_ADAP");
        if (toto != null && toto.Visual.TryGetComponent(out Selectable totoSel))
        {
            int g0 = ctx.GoldWallet.Gold;
            NetCommands.RequestHudUnitAction(NetHudAction.Toto, totoSel, 0);
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[M점검] 클라: 토토 요청 — 엔 {g0} → {ctx.GoldWallet.Gold} (−1000이면 실패 · +2000이면 금화 · 알림 줄 참고)");
        }
        else Debug.Log("[M점검] ❌ 클라: 유재헌 거울 없음");

        // ⑤ 서민성 강화
        NetEntity enh = MirrorOf("영원_서민성");
        if (enh != null && enh.Visual.TryGetComponent(out Selectable enhSel))
        {
            int g0 = ctx.GoldWallet.Gold;
            Debug.Log($"[M점검] 클라: 서민성 거울 UnitAttacker {(enh.Visual.TryGetComponent(out UnitAttacker _) ? "있음" : "없음")} · 거울 강화 레벨 {enh.EnhanceLevel} · 요청 전 엔 {g0}");
            NetCommands.RequestHudUnitAction(NetHudAction.Enhance, enhSel, 0);
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[M점검] 클라: 강화 요청 — 엔 {g0} → {ctx.GoldWallet.Gold} · 거울 강화 레벨 {enh.EnhanceLevel}(1이어야 함)");
            // 정보 칸 사진: 서민성을 고르고 한 장
            SelectionManager sel0 = FindFirstObjectByType<SelectionManager>();
            if (sel0 != null) sel0.SelectOnly(enhSel);
            yield return new WaitForSecondsRealtime(1.5f);
            if (!string.IsNullOrEmpty(shotPrefix)) ScreenCapture.CaptureScreenshot(shotPrefix + "_enhance.png");
            yield return new WaitForSecondsRealtime(0.5f);
        }
        else Debug.Log("[M점검] ❌ 클라: 서민성 거울 없음");

        // ① 상점 QWER — 내 도박소를 고르고 화면 칸 0(Q)을 누른다
        MonoBehaviour gambling = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b is GamblingShop && b.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == me);
        SelectionManager sel = FindFirstObjectByType<SelectionManager>();
        GameHud hud = FindFirstObjectByType<GameHud>();
        if (gambling != null && sel != null && hud != null && gambling.TryGetComponent(out Selectable gs))
        {
            sel.SelectOnly(gs);
            yield return new WaitForSecondsRealtime(1.2f);
            if (!string.IsNullOrEmpty(shotPrefix)) ScreenCapture.CaptureScreenshot(shotPrefix + "_shop.png");
            int g0 = ctx.GoldWallet.Gold;
            var click = typeof(GameHud).GetMethod("OnShopSlotClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            click.Invoke(hud, new object[] { 0 });   // 화면 칸 0 = Q
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[M점검] 클라: 도박소 Q칸(화면 칸 0) 누름 — 엔 {g0} → {ctx.GoldWallet.Gold}(10엔 도박이면 −10 안팎) · 선택 {sel.Selected.Count}");
        }
        else Debug.Log("[M점검] ❌ 클라: 도박소/선택/HUD 못 찾음");

        // ⑥ 지연 조합 — 영원함 식은 버튼이 아니라 채팅 코드로만 된다(IsChatOnly). 호스트가 세운 재료로 채팅 한 줄을 요청한다.
        CombineSystem system = FindFirstObjectByType<CombineSystem>();
        CombineRecipe delayed = null;
        for (int i = 0; system != null && system.RecipeAt(i) != null; i++) if (system.RecipeAt(i).resultDelaySeconds > 0f) { delayed = system.RecipeAt(i); break; }
        if (delayed != null)
        {
            string phrase = !string.IsNullOrEmpty(delayed.chatPhrase) ? delayed.chatPhrase : (delayed.commandId ?? "").Split('/')[0].Trim();
            NetCommands.RequestChat(phrase);
            Debug.Log($"[M점검] 클라: 지연 조합 「{delayed.result.unitName}」 채팅 요청 「{phrase}」 — 알림 줄에 「N초 뒤에 나타납니다」가 와야 함");
            delayedResultName = delayed.result.name;
        }

        // ③ 회유 유닛 거울 + 판매
        yield return new WaitForSecondsRealtime(2f);
        var recruits = FindObjectsByType<NetEntity>(FindObjectsSortMode.None).Where(e => IsRecruitMirror(e, me)).ToList();
        Debug.Log($"[M점검] 클라: 회유 유닛 거울 {recruits.Count}기 · 위치 {string.Join(" ", recruits.Select(r => r.Visual.transform.position.ToString("F0")))} · 몸 자식 {string.Join(",", recruits.Take(1).SelectMany(r => r.Visual.transform.Cast<Transform>().Select(c => c.name)))}");
        if (!string.IsNullOrEmpty(shotPrefix)) ScreenCapture.CaptureScreenshot(shotPrefix + "_recruit.png");
        if (recruits.Count > 0 && recruits[0].Visual.TryGetComponent(out Selectable rs))
        {
            NetCommands.RequestHudUnitAction(NetHudAction.Sell, rs, 0);
            yield return new WaitForSecondsRealtime(2.5f);
            int after = FindObjectsByType<NetEntity>(FindObjectsSortMode.None).Count(e => IsRecruitMirror(e, me));
            Debug.Log($"[M점검] 클라: 회유 유닛 판매 요청 — 거울 {recruits.Count} → {after}기");
        }

        if (delayedResultName != null)
        {
            yield return new WaitForSecondsRealtime(10f);
            Debug.Log($"[M점검] 클라: 지연 조합 결과 거울 {(MirrorOf(delayedResultName) != null ? "있음" : "없음")}({delayedResultName})");
        }

        // 비행 유닛 거울 위치 추적
        for (int t = 0; t < 8; t++)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (fly != null && fly.Visual != null) Debug.Log($"[M점검] 클라 비행 거울 위치 {fly.Visual.transform.position:F0} (y {fly.Visual.transform.position.y:F1}) · 바다 점까지 {Vector3.Distance(fly.Visual.transform.position, sea):F0}");
        }
        if (!string.IsNullOrEmpty(shotPrefix)) ScreenCapture.CaptureScreenshot(shotPrefix + "_end.png");
    }

    string delayedResultName;

    static bool IsRecruitMirror(NetEntity e, int me) =>
        e != null && e.Object != null && e.Object.IsValid && e.EntityKind == NetEntityKind.Unit && e.Owner == me && e.Visual != null
        && e.Visual.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "Summon_회유_적";

    Vector3 SeaPoint()
    {
        LaneMarker lane = LaneMarker.Get(LocalPlayer.LocalPlayerId);
        Vector3 c = lane != null ? lane.LaneCenter : Vector3.zero;
        int seaArea = UnityEngine.AI.NavMesh.GetAreaFromName("Sea");
        foreach (Vector3 off in new[] { new Vector3(0f, 0f, -700f), new Vector3(0f, 0f, 700f), new Vector3(-700f, 0f, 0f), new Vector3(700f, 0f, 0f) })
            if (seaArea >= 0 && UnityEngine.AI.NavMesh.SamplePosition(c + off, out UnityEngine.AI.NavMeshHit hit, 400f, 1 << seaArea)) return hit.position;
        return c + new Vector3(0f, 0f, -700f);
    }
}
