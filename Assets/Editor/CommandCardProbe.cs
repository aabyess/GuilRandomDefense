using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 명령 카드 재배치 점검(10-06 사장님 「기본 명령은 상단·스킬은 오른쪽 하단부터」) — gameshot call:로 판 안에서 부른다.
//   call:CommandCardProbe.Baji | Minsoo | Notae | Common | Shop   고른 유닛(상점)을 세우고 고른다
//   call:CommandCardProbe.Check     12칸 표: 글자·단축키·눌림·진짜 클릭이 그 칸에 닿는가(레이캐스트) + 홀드·모으기 클릭이 명령으로 먹었나
//   call:CommandCardProbe.ClickFlex  4~11의 글자 있는 칸을 진짜 클릭 경로로 하나씩 누르고 알림·변화를 적는다(판매·공격은 건드리지 않는다)
static class CommandCardProbe
{
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string SpawnAndSelect(string asset)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var selection = Object.FindFirstObjectByType<SelectionManager>();
        UnitData d = Roster(asset);
        if (spawner == null || selection == null || d == null) return $"❌ 준비 안 됨 spawner {spawner != null} selection {selection != null} data {d != null}";
        LaneMarker lane = LaneMarker.Get(0);
        GameObject go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        if (go == null || !go.TryGetComponent(out Selectable sel)) return "❌ 소환 실패";
        selection.SelectOnly(sel);
        return $"선택: {d.unitName}({asset}) 스킬 {(d.skills != null ? d.skills.Count : 0)}개";
    }

    static string Baji() => SpawnAndSelect("초월_최상호_AP");
    static string Minsoo() => SpawnAndSelect(UnitAttacker.TalentUnitAsset);
    static string Notae() => SpawnAndSelect("초월_노태현_AP");

    // 조합 결과가 있는 흔함 유닛 — 첫 재료로 쓰이는 식이 있는 흔함 로스터 하나.
    static string Common()
    {
        var system = Object.FindFirstObjectByType<CombineSystem>();
        if (system == null) return "❌ CombineSystem 없음";
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData 흔함_", new[] { "Assets/Data/Units/Roster" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("흔함_")) continue;
            UnitData d = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            int n = system.GetRecipesStartingWith(d).Count(r => r != null && r.result != null);
            if (n >= 2) return SpawnAndSelect(name) + $" · 조합 결과 {n}개";
        }
        return "❌ 조합 결과 2개 이상인 흔함 유닛 없음";
    }

    static string Shop()
    {
        var selection = Object.FindFirstObjectByType<SelectionManager>();
        Selectable s = Selectable.All.FirstOrDefault(x => x.name.Contains("Lane1_도박소"));
        if (selection == null || s == null) return "❌ 도박소 없음";
        selection.SelectOnly(s);
        return $"선택 {s.name}";
    }

    static string Label(Button b) =>
        (string.Concat(b.GetComponentsInChildren<Text>().Select(t => t.text)) + string.Concat(b.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text))).Replace("\n", " ");

    static List<Button> Slots() => Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .Where(b => b.name.StartsWith("UnitCommandSlot") && b.IsActive())
        .OrderBy(b => int.TryParse(b.name.Substring("UnitCommandSlot".Length), out int i) ? i : 99).ToList();

    static int Index(Button b) => int.Parse(b.name.Substring("UnitCommandSlot".Length));

    static Vector2 Center(Button b) =>
        RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));

    static void Click(Button b) =>
        ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current) { position = Center(b), button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

    static string Check()
    {
        if (EventSystem.current == null) return "❌ EventSystem 없음";
        var sb = new StringBuilder();
        int shown = 0, blocked = 0;
        foreach (Button b in Slots())
        {
            string label = Label(b);
            Image img = b.GetComponent<Image>();
            bool empty = string.IsNullOrEmpty(label) && (img == null || img.color.a < 0.05f);
            if (empty) { sb.AppendLine($"   {b.name,-18} (빈 칸)"); continue; }
            shown++;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Center(b) }, hits);
            GameObject top = hits.Count > 0 ? hits[0].gameObject : null;
            bool ok = top != null && (top == b.gameObject || top.transform.IsChildOf(b.transform));
            if (!ok) blocked++;
            sb.AppendLine($"   {(ok ? "✅" : "❌")} {b.name,-18}「{label}」 눌림 {b.interactable} 알파 {(img != null ? img.color.a : -1):F2}{(ok ? "" : " ← 가로챔: " + (top != null ? top.name : "없음"))}");
        }
        sb.AppendLine($"   글자 있는 칸 {shown}개 중 가로채인 것 {blocked}");
        return sb.ToString();
    }

    static string ClickFlex()
    {
        if (EventSystem.current == null) return "❌ EventSystem 없음";
        var sb = new StringBuilder();
        var notes = new List<string>();
        System.Action<int, string, float> hook = (pid, msg, dd) => notes.Add(msg);
        PlayerNotification.Shown += hook;
        try
        {
            PlayerContext me = PlayerContext.Local;
            foreach (Button b in Slots())
            {
                int i = Index(b);
                if (i < 4) continue;   // 1줄(홀드·공격·모으기·판매)은 건드리지 않는다
                string label = Label(b);
                if (string.IsNullOrEmpty(label) || !b.interactable) continue;
                notes.Clear();
                int unitsBefore = me != null ? me.UnitInventory.Units.Count : -1;
                Click(b);
                int unitsAfter = me != null ? me.UnitInventory.Units.Count : -1;
                sb.AppendLine($"   칸{i}「{label}」 클릭 → 알림 {(notes.Count == 0 ? "(없음)" : string.Join(" / ", notes.Select(n => n.Length > 50 ? n.Substring(0, 50) + "…" : n)))} · 유닛 수 {unitsBefore}→{unitsAfter}");
            }
        }
        finally { PlayerNotification.Shown -= hook; }
        return sb.Length == 0 ? "   (누를 칸 없음)" : sb.ToString();
    }
}
