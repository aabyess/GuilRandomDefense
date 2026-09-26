using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 위습 → 포탈 소모가 어느 단계에서 끊기는지 판 안에서 잰다(2026-09-25, 구현담당1).
/// gameshot의 call:로 부른다 — 이미 플레이 중인 판에 붙어야 해서 메뉴가 아니라 정적 함수다.
///   call:WispPortalProbe.Arm       소모 이벤트 구독 + 0.25초마다 내 위습 자리·목적지 기록 시작, 지금 모습 한 장
///   call:WispPortalProbe.Snapshot  지금 모습: 포탈·위습 collider.bounds, 겹침, 조준점이 실제로 찍는 땅
///   call:WispPortalProbe.Report    Arm 이후 위습마다 궤적 요약(가장 가까이 간 포탈 거리)과 소모 기록
/// 에디터 전용 진단이다. 게임 상태를 바꾸지 않는다.
/// </summary>
public static class WispPortalProbe
{
    class Track
    {
        public string name;
        public Vector3 first, last;
        public Vector3 destination;
        public float firstTime, lastTime;
        public float closestPortal = float.MaxValue;
        public string closestPortalName;
        public int samples;
        public bool consumed;
    }

    static readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
    static readonly List<string> consumedLog = new List<string>();
    static double nextSample;
    static bool armed;
    static float armTime;

    public static string Arm()
    {
        tracks.Clear();
        consumedLog.Clear();
        if (!armed)
        {
            Wisp.OnConsumed += OnConsumed;
            EditorApplication.update += Sample;
            EditorApplication.playModeStateChanged += Disarm;
            armed = true;
        }
        armTime = Time.time;
        Sample();
        return "   🧪 탐침 켬(t=" + armTime.ToString("F1") + ")\n" + Snapshot();
    }

    static void Disarm(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingPlayMode) return;
        Wisp.OnConsumed -= OnConsumed;
        EditorApplication.update -= Sample;
        EditorApplication.playModeStateChanged -= Disarm;
        armed = false;
    }

    static IEnumerable<Collider> PortalColliders() =>
        Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c => c != null && c.isTrigger && c.enabled && c.gameObject.activeInHierarchy &&
                        (c.GetComponent<UnitPortal>() != null || c.GetComponent<ResourcePortal>() != null));

    static IEnumerable<Wisp> MyWisps() =>
        Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None)
            .Where(w => w != null && !w.IsConsumed && (!w.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == 0));

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static void Sample()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + 0.25;
        List<Collider> portals = PortalColliders().ToList();
        foreach (Wisp w in MyWisps())
        {
            int id = w.GetInstanceID();
            if (!tracks.TryGetValue(id, out Track t))
                tracks[id] = t = new Track { name = $"{w.name}#{id}({w.Data?.wispName})", first = w.transform.position, firstTime = Time.time };
            t.last = w.transform.position;
            t.lastTime = Time.time;
            t.samples++;
            if (w.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh && agent.hasPath) t.destination = agent.destination;
            foreach (Collider p in portals)
            {
                float d = Flat(w.transform.position, p.bounds.center);
                if (d < t.closestPortal) { t.closestPortal = d; t.closestPortalName = p.name; }
            }
        }
    }

    static void OnConsumed(Wisp w)
    {
        if (w == null) return;
        if (tracks.TryGetValue(w.GetInstanceID(), out Track t)) t.consumed = true;
        Collider wc = w.GetComponent<Collider>();
        string where = wc == null ? "(콜라이더 없음)" : string.Join(", ", PortalColliders().Where(p => p.bounds.Intersects(wc.bounds)).Select(p => p.name));
        consumedLog.Add($"t={Time.time:F1} {w.name}#{w.GetInstanceID()}({w.Data?.wispName}) @ {w.transform.position:F1} · 겹친 포탈 bounds: {(where.Length > 0 ? where : "없음")}");
    }

    public static string Snapshot()
    {
        StringBuilder sb = new StringBuilder();
        Camera cam = Camera.main;
        List<Collider> portals = PortalColliders().OrderBy(c => c.name).ToList();
        sb.AppendLine($"   🧪 t={Time.time:F1} 포탈 {portals.Count}개 · 카메라 {(cam != null ? cam.transform.position.ToString("F0") : "없음")}");
        foreach (Collider p in portals)
        {
            Bounds b = p.bounds;
            string shape = p is CapsuleCollider cap ? $"캡슐 r{cap.radius * Mathf.Max(p.transform.lossyScale.x, p.transform.lossyScale.z):F1} h{cap.height * p.transform.lossyScale.y:F1}" : p.GetType().Name;
            // 도구의 @rc:는 bounds.center를 화면에 옮겨 누른다. 그 화면점이 실제로 찍는 땅이 포탈 원 안인지 본다.
            // 사람은 바닥의 원판(transform.position)을 찍는다 — 둘을 나란히 재야 도구 탓인지 게임 탓인지 갈린다.
            sb.AppendLine($"      {p.name}: {shape} · bounds y {b.min.y:F1}~{b.max.y:F1} x {b.min.x:F1}~{b.max.x:F1} z {b.min.z:F1}~{b.max.z:F1} · 중심 {b.center:F1} · 원판 {p.transform.position:F1}");
            sb.AppendLine("         도구 조준(bounds.center) " + AimReport(cam, b.center, p));
            sb.AppendLine("         사람 조준(원판 중심)   " + AimReport(cam, p.transform.position, p));
        }

        List<Wisp> wisps = MyWisps().ToList();
        sb.AppendLine($"   🧪 내 위습 {wisps.Count}개");
        foreach (Wisp w in wisps)
        {
            Collider wc = w.GetComponent<Collider>();
            string wb = wc != null ? $"bounds y {wc.bounds.min.y:F1}~{wc.bounds.max.y:F1} 중심 {wc.bounds.center:F1} 반지름 {wc.bounds.extents.x:F1}" : "콜라이더 없음";
            string agentText = "에이전트 없음";
            if (w.TryGetComponent(out NavMeshAgent a))
                agentText = a.isOnNavMesh
                    ? $"목적지 {(a.hasPath ? a.destination.ToString("F1") : "-")} 남음 {(a.hasPath ? a.remainingDistance.ToString("F1") : "-")} 경로 {a.pathStatus} 속도 {a.velocity.magnitude:F1} 멈춤 {a.isStopped}"
                    : $"NavMesh 밖(켜짐 {a.isActiveAndEnabled})";
            Collider nearest = portals.OrderBy(p => Flat(w.transform.position, p.transform.position)).FirstOrDefault();
            string near = nearest == null ? "-" :
                $"가장 가까운 포탈 {nearest.name} 수평 {Flat(w.transform.position, nearest.transform.position):F1}(반지름 {nearest.bounds.extents.x:F1}) · y 겹침 {(wc != null && wc.bounds.max.y >= nearest.bounds.min.y && wc.bounds.min.y <= nearest.bounds.max.y ? "O" : "X")} · bounds 교차 {(wc != null && wc.bounds.Intersects(nearest.bounds) ? "O" : "X")}";
            // 실제 판정은 모양끼리다(bounds는 상자라 캡슐보다 넓다) — 위습 구에 닿는 트리거를 물리로 직접 묻는다.
            string touching = "-";
            if (wc is SphereCollider sc)
            {
                float radius = sc.radius * Mathf.Max(w.transform.lossyScale.x, Mathf.Max(w.transform.lossyScale.y, w.transform.lossyScale.z));
                Collider[] hits = Physics.OverlapSphere(sc.bounds.center, radius, ~0, QueryTriggerInteraction.Collide);
                touching = string.Join(", ", hits.Where(h => h.isTrigger && h != wc && (h.GetComponent<UnitPortal>() || h.GetComponent<ResourcePortal>())).Select(h => h.name));
                if (touching.Length == 0) touching = "없음";
            }
            sb.AppendLine($"      {w.name}#{w.GetInstanceID()}({w.Data?.wispName}) 자리 {w.transform.position:F1} · {wb} · {agentText}\n         {near} · 모양 겹침(물리) {touching}");
        }
        return sb.ToString().TrimEnd();
    }

    // 화면점 → UnitMover와 같은 경로(TryHitGround → SamplePosition 8.3)로 목적지를 구해 포탈 원 안인지 본다.
    static string AimReport(Camera cam, Vector3 world, Collider portal)
    {
        if (cam == null) return "카메라 없음";
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return "카메라 뒤";
        bool onScreen = sp.x >= 0f && sp.x <= cam.pixelWidth && sp.y >= 0f && sp.y <= cam.pixelHeight;
        if (!WorldPick.TryHitGround(cam, sp, out RaycastHit hit)) return $"화면 ({sp.x:F0},{sp.y:F0}) → 땅 없음";
        float r = portal.bounds.extents.x;
        float off = Flat(hit.point, portal.transform.position);
        bool sampled = NavMesh.SamplePosition(hit.point, out NavMeshHit nh, 8.3f, NavMesh.AllAreas);
        float navOff = sampled ? Flat(nh.position, portal.transform.position) : -1f;
        return $"화면 ({sp.x:F0},{sp.y:F0}){(onScreen ? "" : " 화면밖")} → 땅 {hit.collider.name} {hit.point:F1} · 원 중심에서 수평 {off:F1}(반지름 {r:F1}) · 목적지 {(sampled ? navOff.ToString("F1") : "NavMesh 없음")} → {(sampled && navOff < r ? "✅ 원 안" : "❌ 원 밖")}";
    }

    // 좌클릭·드래그가 왜 안 먹는지 — SelectionManager 내부 상태, 가상 마우스, 화면 몇 점의 uGUI 적중.
    public static string InputDiag()
    {
        StringBuilder sb = new StringBuilder("   🧪 입력 진단\n");
        SelectionManager sel = Object.FindFirstObjectByType<SelectionManager>();
        if (sel != null)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            foreach (string f in new[] { "leftButtonHeld", "isDragging", "ignoreCurrentPress", "dragStart", "dragThreshold", "maxSelection", "cam" })
            {
                var fi = typeof(SelectionManager).GetField(f, flags);
                sb.AppendLine($"      SelectionManager.{f} = {(fi != null ? fi.GetValue(sel) : "(필드 없음)")}");
            }
            sb.AppendLine($"      선택 {sel.Selected.Count(x => x != null)}기: {string.Join(", ", sel.Selected.Where(x => x != null).Select(x => x.name).Take(6))} · 켜짐 {sel.isActiveAndEnabled}");
        }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        sb.AppendLine($"      Mouse.current {(mouse != null ? $"{mouse.name}#{mouse.deviceId} 위치 {mouse.position.ReadValue()} 왼쪽 눌림 {mouse.leftButton.isPressed} 오른쪽 눌림 {mouse.rightButton.isPressed}" : "없음")} · 장치 {string.Join(", ", UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Select(m => $"{m.name}#{m.deviceId}"))}");
        // 입력 멈춤 단서(09-25 판 F·H3: 게임이 가상 마우스 누름을 못 받았다) — 창 포커스·배경 동작·장치 마지막 갱신 시각.
        var settings = UnityEngine.InputSystem.InputSystem.settings;
        sb.AppendLine($"      포커스 Application.isFocused {Application.isFocused} · runInBackground {Application.runInBackground} · 배경 동작 {settings.backgroundBehavior} · 에디터 플레이 입력 {settings.editorInputBehaviorInPlayMode} · 갱신 방식 {settings.updateMode}");
        foreach (var m in UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>())
            sb.AppendLine($"      장치 {m.name}#{m.deviceId} 켜짐 {m.enabled} · 마지막 갱신 {m.lastUpdateTime:F1}초(지금 {UnityEngine.InputSystem.LowLevel.InputState.currentTime:F1}) · 추가됨 {m.added}");
        var es = UnityEngine.EventSystems.EventSystem.current;
        sb.AppendLine($"      EventSystem {(es != null ? es.name + " 모듈 " + es.currentInputModule?.GetType().Name : "없음")} · 포인터가 UI 위(IsPointerOverGameObject) {(es != null && es.IsPointerOverGameObject())}");
        Camera cam = Camera.main;
        if (es != null && cam != null)
        {
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            foreach (Vector2 p in new[] { new Vector2(0.2f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.8f, 0.5f), new Vector2(0.35f, 0.47f), new Vector2(0.65f, 0.6f) })
            {
                Vector2 s = new Vector2(p.x * cam.pixelWidth, p.y * cam.pixelHeight);
                hits.Clear();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = s }, hits);
                sb.AppendLine($"      화면 ({s.x:F0},{s.y:F0}) UI 적중 {hits.Count}: {string.Join(", ", hits.Take(4).Select(h => $"{h.gameObject.name}(캔버스 {h.gameObject.GetComponentInParent<Canvas>()?.name})"))}");
            }
        }
        return sb.ToString().TrimEnd();
    }

    public static string Report()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"   🧪 궤적 {tracks.Count}개 · 소모 {consumedLog.Count}번 (Arm t={armTime:F1} → 지금 t={Time.time:F1})");
        foreach (Track t in tracks.Values)
            sb.AppendLine($"      {t.name}: {t.first:F0} → {t.last:F0} (t {t.firstTime:F1}~{t.lastTime:F1}, 표본 {t.samples}) · 마지막 목적지 {t.destination:F0} · 가장 가까이 간 포탈 {t.closestPortalName} 수평 {t.closestPortal:F1} · 소모 {(t.consumed ? "O" : "X")}");
        foreach (string line in consumedLog) sb.AppendLine("      🍽 " + line);
        return sb.ToString().TrimEnd() + "\n" + Snapshot();
    }
}

/// <summary>섬(MapLayout.Island) 안에 들어와 있는 남의 물건 — 섬을 키운 뒤 고정 좌표 물건이 먹히는지 본다(2026-09-25 창고 원작화).</summary>
public static class IslandIntruderProbe
{
    public static string Warehouses()
    {
        var sb = new System.Text.StringBuilder();
        foreach (MapLayout.Island island in MapLayout.Warehouses)
        {
            Rect r = new Rect(island.center.x - island.size.x * 0.5f, island.center.y - island.size.y * 0.5f, island.size.x, island.size.y);
            var found = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(x => x != null && r.Contains(new Vector2(x.bounds.center.x, x.bounds.center.z)))
                .Select(x => x.transform.root == x.transform ? x.name : $"{x.transform.parent?.name}/{x.name}")
                .Where(n => !n.Contains(island.name) && !n.StartsWith("Nature/"))
                .GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key).Take(15).ToList();
            sb.AppendLine($"{island.name} x {r.xMin:F0}~{r.xMax:F0} z {r.yMin:F0}~{r.yMax:F0}: {(found.Count > 0 ? string.Join(", ", found) : "남의 물건 없음")}");
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>내 유닛 전부의 자리·목적지·경로 상태·발밑 NavMesh 영역(2026-09-25 — 지상 유닛이 바다 위에 서 있었다).</summary>
public static class UnitWhereProbe
{
    public static string Mine()
    {
        var sb = new System.Text.StringBuilder("   🧪 내 유닛 자리\n");
        int sea = NavMesh.GetAreaFromName("Sea");
        foreach (UnitIdentity u in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None))
        {
            if (u == null || (u.TryGetComponent(out OwnedByPlayer o) && o.OwnerId != 0)) continue;
            string area = NavMesh.SamplePosition(u.transform.position, out NavMeshHit h, 3f, NavMesh.AllAreas)
                ? (h.mask == (1 << sea) ? "바다(Sea)" : $"영역마스크 {h.mask}") + $" 거리 {Vector3.Distance(h.position, u.transform.position):F1}"
                : "NavMesh 없음(3 안)";
            string agent = u.TryGetComponent(out NavMeshAgent a)
                ? $"에이전트 켜짐 {a.enabled} · NavMesh 위 {a.isOnNavMesh} · 마스크 {a.areaMask} · 목적지 {(a.isOnNavMesh && a.hasPath ? a.destination.ToString("F0") : "-")} · 경로 {(a.isOnNavMesh ? a.pathStatus.ToString() : "-")}"
                : "에이전트 없음";
            sb.AppendLine($"      {u.name} {u.transform.position:F0} · 발밑 {area} · {agent}");
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>조합표 인형 수 — 씬 축소 전후 비교(2026-09-25). 이름이 재료_/결과_/흔함_이고 렌더러가 있는 것(받침·색 큐브 구분).</summary>
public static class RecipeDollProbe
{
    public static string Count()
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>().Where(t => t != null && t.gameObject.scene.IsValid()).Select(t => t.gameObject).ToList();
        bool IsSlot(GameObject g) => (g.name.StartsWith("재료_") || g.name.StartsWith("결과_") || g.name.StartsWith("흔함_")) && !g.name.EndsWith("_받침");
        var slots = all.Where(IsSlot).ToList();
        int skinned = slots.Count(g => g.GetComponentInChildren<SkinnedMeshRenderer>(true) != null || g.GetComponentsInChildren<MeshRenderer>(true).Length > 1);
        int cubes = slots.Count(g => g.GetComponent<MeshFilter>() != null && g.transform.childCount == 0);
        int pedestals = all.Count(g => g.name.EndsWith("_받침") && (g.name.StartsWith("재료_") || g.name.StartsWith("결과_") || g.name.StartsWith("흔함_")));
        var spawners = Object.FindObjectsByType<RecipeDollSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        string spawn = string.Join(", ", spawners.Select(s => $"{s.transform.parent?.name}/{s.name} 목록 {s.Dolls.Count}"));
        return $"조합표 칸 {slots.Count}개(인형 {skinned} · 색 큐브 {cubes}) · 받침 {pedestals} · 스포너 {spawners.Length}개({spawn}) · 마지막 실행 세우기 {RecipeDollSpawner.LastSpawnCount}기 {RecipeDollSpawner.LastSpawnMs:F0}ms";
    }
}

/// <summary>친구 베타 피드백 확인(2026-09-26) — HUD 정보칸 글자 · 살펴보기 대상 · PM ③ 명령(홀드·정지·공격).</summary>
public static class BetaFeedbackProbe
{
    public static string HudInfo()
    {
        var hud = Object.FindFirstObjectByType<GameHud>();
        var f = typeof(GameHud).GetField("unitInfoText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var text = hud != null && f != null ? f.GetValue(hud) as TMPro.TMP_Text : null;
        GameObject t = InspectTarget.Current;
        return $"살펴보기 대상 {(t != null ? t.name : "없음")} · 정보칸 「{(text != null ? text.text.Replace("\n", " / ") : "(없음)")}」";
    }

    static EnemyDummy targeted;
    static Vector3 holdPos;
    static Selectable subject;

    // 1단계: 내 흔함 1기를 골라 홀드 — 위치를 기억한다.
    public static string Hold()
    {
        subject = Selectable.All.FirstOrDefault(x => x != null && x.name.Contains("Unit_흔함") && (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == 0));
        if (subject == null) return "흔함 없음";
        holdPos = subject.transform.position;
        int n = UnitCommands.Hold(new List<Selectable> { subject });
        subject.TryGetComponent(out UnitCombat c);
        return $"홀드 {n}기({subject.name}) · IsHolding {c?.IsHolding} · 위치 {holdPos:F0}";
    }

    // 2단계: 몇 초 뒤 — 위치가 그대로이고 사거리 안 적을 치는지(표적 있음).
    public static string HoldCheck()
    {
        if (subject == null) return "대상 없음";
        subject.TryGetComponent(out UnitCombat c);
        float moved = Vector3.Distance(subject.transform.position, holdPos);
        return $"홀드 뒤 이동 {moved:F1} · IsHolding {c?.IsHolding} · 표적 {(c != null && c.CurrentTarget != null ? c.CurrentTarget.name : "없음")}";
    }

    public static string Stop()
    {
        if (subject == null) return "대상 없음";
        int n = UnitCommands.Stop(new List<Selectable> { subject });
        subject.TryGetComponent(out UnitCombat c);
        return $"정지 {n}기 · IsHolding {c?.IsHolding} · 표적 {(c != null && c.CurrentTarget != null ? c.CurrentTarget.name : "없음")}";
    }

    // 3단계: 레인의 적 하나를 공격 대상으로 — 그 적이 죽을 때까지 표적이 유지되는지.
    public static string Attack()
    {
        if (subject == null) return "대상 없음";
        targeted = EnemyDummy.Active.Where(e => e != null && e.LaneIndex == 0).OrderBy(e => (e.transform.position - subject.transform.position).sqrMagnitude).FirstOrDefault();
        if (targeted == null) return "레인 적 없음";
        int n = UnitCommands.AttackTarget(new List<Selectable> { subject }, targeted);
        return $"공격 대상 {n}기 → {targeted.name}(체력 {targeted.Hp:F0})";
    }

    public static string AttackCheck()
    {
        if (subject == null) return "대상 없음";
        subject.TryGetComponent(out UnitCombat c);
        string cur = c != null && c.CurrentTarget != null ? c.CurrentTarget.name : "없음";
        bool same = c != null && targeted != null && c.CurrentTarget == targeted;
        return $"지정한 적 {(targeted != null ? $"살아 있음 체력 {targeted.Hp:F0}" : "죽음")} · 지금 표적 {cur} · 지정 적 유지 {same}";
    }
}

/// <summary>도박 옵션 에셋 점검(2026-09-26 충전식 재고) — 새 필드가 에셋에서 제대로 읽히는지.</summary>
public static class GambleAssetProbe
{
    public static string List()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:GamblingOptionData", new[] { "Assets/Data/Gambling" }))
        {
            var o = UnityEditor.AssetDatabase.LoadAssetAtPath<GamblingOptionData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (o == null) continue;
            sb.AppendLine($"{o.optionName} · 재고 {o.stockMax}/{o.stockRegenSeconds}초/시작 {o.stockInitial} · 졸업 {o.graduateAtCumulative} · 졸업시 사라짐 {o.retiredOnGraduation} · 졸업 뒤 {o.requiresGraduation} · 지급 자원 {o.payoutResourceAmount} · 둘째확률 {o.secondaryChancePercent} · 스토리 {o.requiresStoriesCleared} · 보너스 {(o.bonusUnit != null ? o.bonusUnit.unitName : "-")} {o.bonusChancePercent}% · 실패환급 {o.failureGoldMin}~{o.failureGoldMax} · 횟수 {o.maxUses}");
        }
        return sb.ToString().TrimEnd();
    }
}
