using UnityEngine;

// 촬영 전용(구현담당2): gameshot call:HeroStatShotProbe.AddXp — 판 안의 초월·영원 유닛 전부에 경험치 1000(약 레벨 14)을 준다.
public static class HeroStatShotProbe
{
    public static string Spawn()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_구주호_AD.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ " + unit.DisplayName + " 세움";
    }

    public static string SpawnYongtae()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/불멸_김용태.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ " + unit.DisplayName + " 세움 · 스킬 " + (unit.skills != null ? unit.skills.Count : 0);
    }

    // 버프·디버프 칸 촬영용 — 판 안의 모든 유닛에 공속 오라·공격력 오라·이속 감소·마나 재생·기절을 건다.
    public static string AddBuffs()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        int n = 0;
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        {
            a.AddAuraBonus(a, SkillEffectKind.AttackSpeedBuffPercent, "probe_as", 0.3f);
            a.AddTimedAuraBonus(a, SkillEffectKind.AttackPowerBuffPercent, "probe_ap", 0.2f, 60f);
            a.AddAuraBonus(a, SkillEffectKind.AllyMoveSpeedDebuff, "probe_ms", 0.2f);
            a.AddTimedAuraBonus(a, SkillEffectKind.ManaRegenBuff, "probe_mr", 1f, 60f);
            n++;
        }
        return $"✅ {n}기에 버프 넷";
    }

    // 이름에 그 글자가 든 내 유닛을 직접 고른다(클릭 판정이 겹친 유닛에 흔들릴 때) — 이름은 SelectName 정적 필드로.
    public static string SelectYongtae()
    {
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.Contains("불멸_김용태")) { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel); return "✅ " + sel.name; }
        return "❌ 못 찾음";
    }

    // 판매 칸 회귀 확인용 — 판매되는 등급(희귀함 구주호)을 세워 고른다.
    public static string SpawnSelectRare()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_구주호.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ 희귀함 세움";
    }

    public static string SelectRare()
    {
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.Contains("희귀함_구주호")) { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel); return "✅ " + sel.name; }
        return "❌ 못 찾음";
    }

    // 조합 도우미 재료창 촬영용 — 크게 보기를 열고 탭을 고른 뒤 그 등급 첫 칸을 누른다.
    public static string HelperOpen() { RecipeHelperPanel.Show("", null); return "✅ 열림"; }

    static UnitData helperTarget;   // 지정하면 그 결과 유닛 칸만 누른다
    static string HelperClick(int tab, UnitGrade grade, int skip = 0)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var panel = RecipeHelperPanel.Instance;
        if (panel == null) return "❌ 도우미 없음";
        var t = typeof(RecipeHelperPanel);
        t.GetField("tab", flags).SetValue(panel, tab);
        t.GetMethod("LayoutColumns", flags).Invoke(panel, null);
        var columns = (System.Collections.IEnumerable)t.GetField("columns", flags).GetValue(panel);
        foreach (object column in columns)
        {
            var cells = (System.Collections.IEnumerable)column.GetType().GetField("cells").GetValue(column);
            var def = column.GetType().GetField("def").GetValue(column);
            if ((UnitGrade)def.GetType().GetField("grade").GetValue(def) != grade || (int)def.GetType().GetField("tab").GetValue(def) != tab) continue;
            foreach (object cell in cells)
            {
                if (skip-- > 0) continue;
                var cellRecipe = cell.GetType().GetField("recipe").GetValue(cell);
                if (cellRecipe == null) continue;
                if (helperTarget != null && (UnitData)cell.GetType().GetField("unit").GetValue(cell) != helperTarget) continue;
                t.GetMethod("OnCellClicked", flags).Invoke(panel, new[] { cell });
                return "✅ " + ((UnitData)cell.GetType().GetField("unit").GetValue(cell)).DisplayName;
            }
        }
        return "❌ 칸 없음";
    }

    public static string HelperClickLegend() => HelperClick(1, UnitGrade.Legendary);
    public static string HelperClickHidden() => HelperClick(1, UnitGrade.Hidden);

    // 편집 모드 명령(call): 첫 화면(NetBoot)·게임 씬을 열었다 닫는다. 촬영 뒤 반드시 OpenGame으로 되돌린다.
    public static string OpenBoot()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/NetBoot.unity");
        return "✅ NetBoot";
    }

    public static string OpenGame()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        return "✅ SampleScene";
    }

    public static string AddXp()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        int n = 0;
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        {
            a.AddHeroXp(1000); n++;
            Debug.Log($"[HeroStatShot] {a.name} Lv.{a.CharacterLevel} STR {a.CurrentStrength:F1} AGI {a.CurrentAgility:F1} INT {a.CurrentIntelligence:F1}");
        }
        return $"✅ {n}기에 경험치 1000";
    }

    // 10-08 긴 이름 액티브 칸·버프 +N·공↓ 촬영용
    public static string SpawnBae()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_배성령_AD.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ " + unit.DisplayName + " 세움";
    }

    public static string SelectBae()
    {
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.Contains("초월_배성령")) { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel); return "✅ " + sel.name; }
        return "❌ 못 찾음";
    }

    // AddBuffs 위에 공격력 감소·체력 재생·팀↑를 더해 칸을 일곱으로(여섯 칸 넘침 → 「+2」).
    public static string AddMoreBuffs()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        int n = 0;
        TeamBuffs.ActivateIntense();
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        {
            a.AddTimedAuraBonus(a, SkillEffectKind.AttackPowerBuffPercent, "probe_apd", -0.2f, 60f);
            a.AddTimedAuraBonus(a, SkillEffectKind.LifeRegenBuff, "probe_lr", 1f, 60f);
            n++;
        }
        return $"✅ {n}기에 추가 버프";
    }

    // 첫 전설 조합식의 재료를 내 유닛으로 다 세우고(보유 표시 촬영) 그 칸을 누른다. 재료창 안의 첫 재료 행이 또 조합식이면 HelperClickRow로 간다.
    public static string SpawnLegendMaterials()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var system = Object.FindFirstObjectByType<CombineSystem>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (var r in system.Recipes)
        {
            if (r == null || r.result == null || r.result.grade != UnitGrade.Legendary || r.ingredients == null) continue;
            bool ok = true;
            foreach (var ing in r.ingredients) if (ing.kind != IngredientKind.SpecificUnit || ing.unit == null) ok = false;
            if (!ok) continue;
            int n = 0;
            foreach (var ing in r.ingredients) for (int k = 0; k < Mathf.Max(1, ing.count); k++) { spawner.Spawn(ing.unit, LaneMarker.Get(0).LaneCenter, 0); n++; }
            helperTarget = r.result;
            return $"✅ {r.result.DisplayName} 재료 {n}기";
        }
        return "❌ 없음";
    }

    public static string HelperClickTarget() => HelperClick(1, UnitGrade.Legendary);

    // 10-08 판매·Shift 빼기 촬영용 — 희귀함 셋(구주호·배성령·강재규) + 초월 구주호(판매 불가)를 세워 전부 고른다.
    public static string SpawnSellGroup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (string n in new[] { "희귀함_구주호", "희귀함_배성령", "희귀함_강재규", "초월_구주호_AD" })
            spawner.Spawn(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset"), LaneMarker.Get(0).LaneCenter, 0);
        return "✅ 네 기";
    }

    public static string SelectAllMine()
    {
        var sm = Object.FindFirstObjectByType<SelectionManager>();
        sm.ClearSelection();
        var add = typeof(SelectionManager).GetMethod("AddToSelection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int n = 0;
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.StartsWith("Unit_") && sel.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == LocalPlayer.LocalPlayerId) { add.Invoke(sm, new object[] { sel }); n++; }
        return $"✅ {n}기 선택";
    }

    public static string ClickSell()
    {
        var hud = Object.FindFirstObjectByType<GameHud>();
        typeof(GameHud).GetMethod("OnUnitCommandSlotClicked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hud, new object[] { 7 });
        var sm = Object.FindFirstObjectByType<SelectionManager>();
        return $"✅ 판매 클릭 · 선택 {sm.Selected.Count}";
    }

    // 카드 한 장을 Shift 누른 채 누른 것처럼 뺀다(첫 카드).
    public static string ShiftRemoveFirst()
    {
        var sm = Object.FindFirstObjectByType<SelectionManager>();
        if (sm.Selected.Count == 0) return "❌ 선택 없음";
        string name = sm.Selected[0].name;
        sm.RemoveFromSelection(sm.Selected[0]);
        return $"✅ {name} 뺌 · 남은 {sm.Selected.Count}";
    }

    // 재료창의 첫 재료 행을 누른 것처럼(그 재료의 재료로 들어간다).
    public static string HelperClickFirstRow()
    {
        var panel = RecipeHelperPanel.Instance;
        var system = Object.FindFirstObjectByType<CombineSystem>();
        foreach (var r in system.Recipes)
            if (r != null && r.result == helperTarget)
            {
                var link = r.ingredients[0].unit;
                typeof(RecipeHelperPanel).GetMethod("OpenDetail", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, new object[] { link, true });
                return "✅ " + link.DisplayName;
            }
        return "❌";
    }

    // 촬영 전용: ClaudeBridge/nowc3.flag 파일이 있으면 판 시작 때 워크3 콘솔 테마를 끈다(옛 C 금속 바 테마 촬영).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ShotThemeFlag()
    {
        if (System.IO.File.Exists("ClaudeBridge/nowc3.flag")) UiSkin.Wc3Active = false;
    }
}
