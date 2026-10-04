using UnityEditor;
using System.Text;
using UnityEngine;

// 표시류 점검(막타 청록 글자·클리어 칭호) — gameshot call:DisplayProbe.Run → wait → snap
static class DisplayProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        foreach (int n in new[] { 0, 1, 5, 6, 90, 91, 250, 251, 300, 301 })
            sb.AppendLine($"  클리어 {n}회 → 「{PlayerDisplayName.ClearTitleOf(n)}」");
        PlayerContext me = PlayerContext.Get(0);
        me.PersistentSave.Data.cumulativeClearCount = 36;
        sb.AppendLine("이름(36회): " + PlayerDisplayName.Of(0));
        PlayerNotification.Show(0, $"<color=#FF8200>{PlayerDisplayName.Of(0)}</color> <color=#FF8200>님이 스토리를 깼습니다</color>", 15f);
        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 p = Physics.Raycast(ray, out RaycastHit hit, 5000f) ? hit.point : ray.GetPoint(300f);
        sb.AppendLine($"팝업 기준점 {p:F1} (맞힌 것: {(hit.collider != null ? hit.collider.name : "없음")}) · 로컬 플레이어 {LocalPlayer.LocalPlayerId}");
        KillGoldPopup.Show(0, p + Vector3.left * 8f, 120);
        KillGoldPopup.Show(0, p + Vector3.right * 8f, 1, wood: true);
        KillGoldPopup.Show(0, p + Vector3.right * 20f, 10, wood: true);
        return sb.ToString();
    }

    // 상시 오라 근접 사진용(gameshot call: — spawn보다 먼저 도니 EditorApplication.update로 유닛이 생길 때까지 기다린다).
    // 바깥에서 비스듬히 내려다보는 보조 카메라를 메인 위에 얹는다. 이름 조각 = 대상 유닛, 거리·높이는 유닛 키 배수.
    static string AuraCloseup() => CloseupOn("김민준", 55f, -85f);
    // 정면 확인용: 이름은 ClaudeBridge/probe_name.txt, 카메라는 유닛의 +Z쪽(정면이 +Z면 얼굴이 보인다).
    static string CloseupFront()
    {
        string root = System.IO.Path.GetDirectoryName(Application.dataPath);
        return CloseupOn(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "ClaudeBridge/probe_name.txt")).Trim(), 62f, 100f, 24f);
    }
    static string CloseupPark() => CloseupOn("박민수", 30f, -90f);
    static string CloseupOn(string part, float up, float back, float lookUp = 8f)
    {
        EditorApplication.CallbackFunction tick = null;
        float started = (float)EditorApplication.timeSinceStartup;
        tick = () =>
        {
            if (!Application.isPlaying) { EditorApplication.update -= tick; return; }
            if (EditorApplication.timeSinceStartup - started > 30) { EditorApplication.update -= tick; return; }
            UnitIdentity target = null;
            foreach (UnitIdentity u in UnitIdentity.Active) if (u != null && u.name.Contains(part)) target = u;
            if (target == null || Camera.main == null) return;
            EditorApplication.update -= tick;
            var go = new GameObject("AuraCloseupCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.CopyFrom(Camera.main);
            cam.depth = Camera.main.depth + 10;
            cam.fieldOfView = 35f;
            Vector3 p = target.transform.position;
            cam.transform.position = p + new Vector3(0f, up, back);   // 유닛 키 ≈30: 앞쪽 위에서 비스듬히(원작 사진 각도)
            cam.transform.LookAt(p + Vector3.up * lookUp);
        };
        EditorApplication.update += tick;
        return "AuraCloseup 예약";
    }

    // spawn: 뒤에 생긴 유닛을 고르는 예약 선택(select:는 spawn보다 먼저 돈다) — 초상 확인용.
    static string SelectLater(string part)
    {
        EditorApplication.CallbackFunction tick = null;
        float started = (float)EditorApplication.timeSinceStartup;
        tick = () =>
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup - started > 30) { EditorApplication.update -= tick; return; }
            SelectionManager manager = Object.FindFirstObjectByType<SelectionManager>();
            if (manager == null) return;
            foreach (UnitIdentity u in UnitIdentity.Active)
                if (u != null && u.name.Contains(part) && u.TryGetComponent(out Selectable sel))
                {
                    EditorApplication.update -= tick;
                    manager.SelectOnly(sel);
                    return;
                }
        };
        EditorApplication.update += tick;
        return "예약: " + part;
    }
    static string SelectPark() => SelectLater("박민수");
    static string SelectBae() => SelectLater("배성령");
    static string SelectYoo() => SelectLater("유재헌");
    static string SelectKimTY() => SelectLater("김태영");

    // 영웅 단추 점검 — 내 유닛 중 영웅 표 대상과 단추 상태를 찍는다(gameshot call:DisplayProbe.HeroState).
    static string HeroState()
    {
        var sb = new StringBuilder();
        foreach (UnitIdentity u in UnitIdentity.Active)
            if (u != null && u.Data != null && u.Data.name.Contains("김민준"))
                sb.AppendLine($"유닛 {u.name} · 주인 {u.OwnerId}(내 번호 {LocalPlayer.LocalPlayerId}) · 데이터 이름 「{u.Data.name}」 · 영웅표 {UnitHeroTable.IsHero(u.Data)}");
        sb.AppendLine($"UnitIdentity.Active {UnitIdentity.Active.Count}개: " + string.Join(" / ", System.Linq.Enumerable.Select(UnitIdentity.Active, u => u == null ? "null" : $"{u.name}|{(u.Data != null ? u.Data.name : "데이터없음")}|주인{u.OwnerId}|영웅{UnitHeroTable.IsHero(u.Data)}")));
        GameObject col = GameObject.Find("HeroButtons");
        sb.AppendLine(col == null ? "HeroButtons 오브젝트 없음" : $"HeroButtons 자식 {col.transform.childCount}개 · 켜진 것 {col.GetComponentsInChildren<UnityEngine.UI.Button>(false).Length}개 · 위치 {((RectTransform)col.transform).anchoredPosition} 크기 {((RectTransform)col.transform).sizeDelta}");
        return sb.ToString();
    }

    // ───── 촬영용 탐침(10-03 밤) — gameshot call:로 부르면 spawn보다 먼저 도니 EditorApplication.update로 판이 준비될 때까지 기다린다.
    // 결과 한 줄은 ClaudeBridge/outbox/구현담당2_<이름>.txt 에도 남는다.
    static void WhenReady(string name, System.Func<bool> ready, System.Func<string> act)
    {
        EditorApplication.CallbackFunction tick = null;
        float started = (float)EditorApplication.timeSinceStartup;
        tick = () =>
        {
            if (!Application.isPlaying) { EditorApplication.update -= tick; return; }
            bool timeout = EditorApplication.timeSinceStartup - started > 40;
            if (!timeout && !ready()) return;
            EditorApplication.update -= tick;
            string result = timeout ? "❌ 40초 안에 준비 안 됨" : act();
            Debug.Log($"[탐침 {name}] {result}");
            string root = System.IO.Path.GetDirectoryName(Application.dataPath);
            string dir = System.IO.Path.Combine(root, "ClaudeBridge/outbox");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"구현담당2_{name}.txt"), result);
        };
        EditorApplication.update += tick;
    }

    // 위습 칸에 서로 다른 종류(Assets/Data/Wisps 전부)를 한 개씩 채운다 — 등급색·3D 아이콘 구분 사진용(gameshot call:DisplayProbe.WispKinds).
    static string WispKinds()
    {
        WhenReady("WispKinds", () => PlayerContext.Local != null && RewardDistributor.Instance != null && GameAuthority.IsServer, () =>
        {
            var rewards = new System.Collections.Generic.List<WispReward>();
            var names = new System.Collections.Generic.List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:WispData", new[] { "Assets/Data/Wisps" }))
            {
                WispData w = AssetDatabase.LoadAssetAtPath<WispData>(AssetDatabase.GUIDToAssetPath(guid));
                if (w == null || w.prefab == null) continue;
                rewards.Add(new WispReward { wisp = w, count = 1 });
                names.Add($"{w.name}({w.targetGrade})");
            }
            RewardDistributor.Instance.GrantWisps(PlayerContext.Local, rewards);
            return $"위습 {rewards.Count}종 지급: " + string.Join(", ", names);
        });
        return "WispKinds 예약";
    }

    // 아이템 칸을 6개 채우고, 7번째를 실제 지급 경로(보스·스토리 드랍 GrantItemDrop, 확률 1)로 넣어 본다 — 「칸이 가득 찼습니다」 알림이 떠야 한다.
    // 🔴 칸 비우기 없이 쓰는 촬영 전용(gameshot call:DisplayProbe.ItemsFull).
    static string ItemsFull()
    {
        WhenReady("ItemsFull", () => PlayerContext.Local != null && PlayerContext.Local.ItemInventory != null && RewardDistributor.Instance != null, () =>
        {
            PlayerContext me = PlayerContext.Local;
            var all = new System.Collections.Generic.List<ItemData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Data/Items" }))
            {
                ItemData it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (it != null && !string.IsNullOrEmpty(it.itemName)) all.Add(it);
            }
            all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var sb = new StringBuilder();
            int added = 0;
            foreach (ItemData it in all)
            {
                if (me.ItemInventory.IsFull) break;
                if (me.ItemInventory.Add(it)) { added++; sb.Append(it.itemName).Append(", "); }
            }
            sb.AppendLine($"→ {added}개 넣음 · 보유 {me.ItemInventory.Items.Count}/{ItemInventory.MaxItems} · IsFull {me.ItemInventory.IsFull}");
            ItemData seventh = null;
            foreach (ItemData it in all) if (!System.Linq.Enumerable.Contains(me.ItemInventory.Items, it)) { seventh = it; break; }
            var drops = new System.Collections.Generic.List<EnemyItemDrop> { new EnemyItemDrop { item = seventh, weight = 1f } };
            System.Reflection.MethodInfo m = typeof(RewardDistributor).GetMethod("GrantItemDrop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            m.Invoke(RewardDistributor.Instance, new object[] { me, 1f, drops });
            sb.AppendLine($"7번째({(seventh != null ? seventh.itemName : "없음")}) 드랍 경로 호출 → 보유 {me.ItemInventory.Items.Count}/{ItemInventory.MaxItems} (6이면 거절됨, 알림은 화면 사진으로)");
            // 알림은 6초뿐이라 gameshot의 늦은 사진엔 안 잡힌다 — 7번째 시도 0.5초 뒤에 직접 찍는다(UI 포함, Game 뷰가 보이는 상태여야 써진다).
            string root = System.IO.Path.GetDirectoryName(Application.dataPath);
            string shots = System.IO.Path.Combine(root, "ClaudeBridge/shots");
            System.IO.Directory.CreateDirectory(shots);
            string snap = System.IO.Path.Combine(shots, "구현담당2_ItemsFull.png");
            double due = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.CallbackFunction snapTick = null;
            snapTick = () =>
            {
                if (EditorApplication.timeSinceStartup < due) return;
                EditorApplication.update -= snapTick;
                if (Application.isPlaying) ScreenCapture.CaptureScreenshot(snap);
            };
            EditorApplication.update += snapTick;
            sb.AppendLine("0.5초 뒤 사진 → ClaudeBridge/shots/구현담당2_ItemsFull.png");
            return sb.ToString();
        });
        return "ItemsFull 예약";
    }

    // 특수지급 세 자리 이름표 촬영용 — 카메라를 가운데 자리(박은석) 앞 거리 190에 세운다(WorldLabel 페이드 260~520 안, 구현담당1 실측).
    // MoveTo만 쓰면 높이 감쇠로 엉뚱한 곳을 봐서 position + RtsCameraController.targetHeight를 직접 맞춘다. gameshot call:DisplayProbe.CamToSpecial wait:3
    static string CamToSpecial()
    {
        float born = (float)EditorApplication.timeSinceStartup;
        WhenReady("CamToSpecial", () => EditorApplication.timeSinceStartup - born > 2f && Camera.main != null, () =>
        {
            GameObject label = null;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go.name.StartsWith("특수지급_라벨_박은석")) { label = go; break; }
            if (label == null) return "❌ 특수지급_라벨_박은석 오브젝트 없음(RepairGachaRewardDisplays 먼저)";
            Camera cam = Camera.main;
            Vector3 look = label.transform.position + new Vector3(0f, 10f, 30f);
            Vector3 pos = look - cam.transform.forward * 190f;
            RtsCameraController rts = cam.GetComponent<RtsCameraController>();
            cam.transform.position = pos;
            if (rts != null)
                typeof(RtsCameraController).GetField("targetHeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(rts, pos.y);
            return $"카메라 → {pos:F1} (보는 곳 {look:F1})";
        });
        return "CamToSpecial 예약";
    }
}
