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

    // 10-08 게이지 막대 촬영용 — 노태현(체력만)·강재규(둘 다)·손오공(마나만)·흔함 강주혁(게이지 없음)을 세우고 이름 조각으로 고른다.
    static readonly string[] GaugeUnits = { "초월_노태현_AP", "초월_강재규_AP", "랜덤_손오공", "흔함_강주혁" };
    public static string SpawnGaugeUnits()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (string n in GaugeUnits)
            spawner.Spawn(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset"), LaneMarker.Get(0).LaneCenter, 0);
        return "✅ 네 기";
    }
    static string SelectByPart(string part)
    {
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.Contains(part)) { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel); return "✅ " + sel.name; }
        return "❌ 못 찾음 " + part;
    }
    public static string SelectNotae() => SelectByPart("초월_노태현");
    public static string SelectKang() => SelectByPart("초월_강재규");
    public static string SelectSonogong() => SelectByPart("랜덤_손오공");
    public static string SelectPlain() => SelectByPart("흔함_강주혁");

    // 발동 직후 모습: 체력 게이지 1·마나 게이지 0으로 내린다(값은 HUD 표시 확인용 — 발동 로직은 구현담당1 실측).
    public static string FireGauges()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        int n = 0;
        foreach (UnitAttacker a in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None))
        {
            var t = typeof(UnitAttacker);
            t.GetField("lifeGaugeInitialized", flags)?.SetValue(a, true); t.GetField("lifeGaugeCounter", flags)?.SetValue(a, 1);
            t.GetField("manaGaugeInitialized", flags)?.SetValue(a, true); t.GetField("manaGaugeCounter", flags)?.SetValue(a, 0);
            n++;
        }
        return $"✅ {n}기 체력1·마나0";
    }

    // 스토리 건물 적을 살펴보기 대상으로 건다(정보창 이름 「NN. 이름」 촬영용).
    public static string InspectStory()
    {
        var sm = Object.FindFirstObjectByType<StoryManager>();
        if (sm != null && sm.Running == null)
            typeof(StoryManager).GetMethod("Spawn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(sm, new object[] { sm.StoryAt(0) });
        foreach (EnemyDummy e in Object.FindObjectsByType<EnemyDummy>(FindObjectsSortMode.None))
            if (e.name.Contains("Story")) { InspectTarget.Set(e.gameObject); return "✅ " + e.DisplayName; }
        return "❌ 스토리 적 없음";
    }

    // 스킨 재임포트(Humanoid 아바타가 서는지) — 안흔함_이호준·특별함_조도연.
    public static string ReimportSkins()
    {
        foreach (string p in new[] { "Assets/Art/Units/안흔함_이호준/안흔함_이호준.fbx", "Assets/Art/Units/특별함_조도연/특별함_조도연.fbx" })
            UnityEditor.AssetDatabase.ImportAsset(p, UnityEditor.ImportAssetOptions.ForceUpdate);
        return "✅ 재임포트";
    }

    public static string SpawnSkinUnits()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (string n in new[] { "특별함_조도연", "안흔함_이호준" })
            spawner.Spawn(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset"), LaneMarker.Get(0).LaneCenter, 0);
        return "✅ 두 기";
    }
    public static string SelectJodoyeon() => SelectByPart("특별함_조도연");
    public static string SelectYoonho() => SelectByPart("안흔함_이호준");
    public static string AttackPoseAll()
    {
        int n = 0;
        foreach (CharacterAnimator a in Object.FindObjectsByType<CharacterAnimator>(FindObjectsSortMode.None)) { a.PlayAttack(); n++; }
        return $"✅ {n}기 공격 동작";
    }

    public static string SpawnJodoOnly()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        spawner.Spawn(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/특별함_조도연.asset"), LaneMarker.Get(0).LaneCenter, 0);
        return "✅";
    }

    public static string SpawnSkinUnitsApart()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var c = LaneMarker.Get(0).LaneCenter;
        spawner.Spawn(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/특별함_조도연.asset"), c + new Vector3(-120f, 0f, 0f), 0);
        spawner.Spawn(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/안흔함_이호준.asset"), c + new Vector3(120f, 0f, 0f), 0);
        return "✅ 떨어뜨려 두 기";
    }

    // F10 메뉴·볼륨 슬라이더 촬영/검증용
    public static string OpenMenu() { Object.FindFirstObjectByType<GameHud>().OpenGameMenu(); return "✅ 메뉴 열림"; }
    public static string SfxHalfAndPlay()
    {
        AudioPrefs.SetSfx(0.5f);
        GameSound.Play(GameSoundId.RoundStart);
        var v = new System.Text.StringBuilder();
        foreach (AudioSource a in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (a.isPlaying && a.gameObject.name == "[GameSound]") v.Append($"{a.clip.name}={a.volume:0.000} ");
        return "✅ 효과음 50% · 재생 중 " + v + "(기대 0.7×0.5(−6dB)×0.5(슬라이더)=0.175)";
    }
    public static string SfxFullAndPlay()
    {
        AudioPrefs.SetSfx(1f);
        return $"✅ 효과음 {AudioPrefs.SfxVolume:0.00} 음악 {AudioPrefs.MusicVolume:0.00}";
    }
    public static string WheelBegin()
    {
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        wheelBlendStart = (float)typeof(RtsCameraController).GetField("targetBlend", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cam);
        wheelFrames = 40;
        UnityEditor.EditorApplication.update += FeedWheel;
        return $"✅ 휠 시작 targetBlend={wheelBlendStart:0.000} 서랍열림={RecipeSearchDrawer.IsOpen} 도우미열림={RecipeHelperPanel.IsOpen}";
    }
    static float wheelBlendStart; static int wheelFrames; static bool wheelOnMap;
    public static string WheelOnMapBegin() { wheelOnMap = true; return WheelBegin(); }
    static void FeedWheel()
    {
        if (wheelFrames-- <= 0) { UnityEditor.EditorApplication.update -= FeedWheel; return; }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        var ms = new UnityEngine.InputSystem.LowLevel.MouseState { position = wheelOnMap ? new Vector2(960f, 500f) : mouse.position.ReadValue(), scroll = new Vector2(0f, 120f) };
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, ms);
    }
    public static string WheelEnd()
    {
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        float now = (float)typeof(RtsCameraController).GetField("targetBlend", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cam);
        return $"✅ 휠 끝 targetBlend {wheelBlendStart:0.000} → {now:0.000} (서랍 {RecipeSearchDrawer.IsOpen}) 높이 {cam.transform.position.y:0.0}";
    }
    public static string OpenDrawer()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        typeof(RecipeSearchDrawer).GetMethod("SetOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(d, new object[] { true });
        return "✅ 서랍 열림";
    }
    public static string CloseDrawer()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        typeof(RecipeSearchDrawer).GetMethod("SetOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(d, new object[] { false });
        return "✅ 서랍 닫힘";
    }
    public static string SetQueryJi()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        var f = typeof(RecipeSearchDrawer).GetField("input", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        ((TMPro.TMP_InputField)f.GetValue(d)).text = "최상호";
        return "✅ 검색어 최상호";
    }
    public static string QueryNow()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        var f = typeof(RecipeSearchDrawer).GetField("input", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return "✅ 입력칸 「" + ((TMPro.TMP_InputField)f.GetValue(d)).text + "」";
    }

    // 검색 서랍에서 희귀함 결과 줄의 첫 재료(특별함) 그림을 누른 것처럼 — 그 재료 유닛의 조합식으로 가는지(selectedRecipe) 확인
    public static string ClickFirstMaterial()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        var t = typeof(RecipeSearchDrawer); var fl = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var rows = (System.Collections.IList)t.GetField("rows", fl).GetValue(d);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; var rt = row.GetType();
            var root = (GameObject)rt.GetField("root").GetValue(row);
            var recipe = (CombineRecipe)rt.GetField("recipe").GetValue(row);
            if (!root.activeSelf || recipe == null || recipe.result.grade != UnitGrade.Rare) continue;
            t.GetMethod("OnCellClicked", fl).Invoke(d, new object[] { i, 0 });
            var sel = (CombineRecipe)t.GetField("selectedRecipe", fl).GetValue(d);
            return $"✅ 줄 「{recipe.result.grade.KoreanName()} {recipe.result.DisplayName}」 첫 재료 「{recipe.ingredients[0].unit.grade.KoreanName()} {recipe.ingredients[0].unit.DisplayName}」 클릭 → 선택된 식 「{(sel != null ? sel.result.grade.KoreanName() + " " + sel.result.DisplayName : "없음")}」";
        }
        return "❌ 희귀함 줄 없음";
    }

    public static string CutinChoi() { CutinOverlay.Enabled = true; return CutinOverlay.Play("초월_최상호_AD") ? "✅ 컷인 시작" : "❌ 산출물 없음"; }
    public static string CutinSeo() { CutinOverlay.Enabled = true; return CutinOverlay.Play("영원_서민성") ? "✅ 컷인 시작" : "❌ 산출물 없음"; }

    public static string CutinDebug()
    {
        var a = Resources.Load<TextAsset>("Cutin/초월_최상호_AD/layout");
        var b = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Cutin/초월_최상호_AD/layout.json");
        var c = Resources.Load<Texture2D>("Cutin/초월_최상호_AD/char");
        string nfd = "Cutin/초월_최상호_AD/layout".Normalize(System.Text.NormalizationForm.FormD);
        var d = Resources.Load<TextAsset>(nfd);
        var e = Resources.Load<TextAsset>("Cutin/_common/초월/layout");
        var g = Resources.LoadAll<TextAsset>("Cutin/초월_최상호_AD");
        return $"Resources.layout={(a != null)} AssetDB.layout={(b != null)} Resources.char={(c != null)} NFD={(d != null)} common={(e != null)} LoadAll={(g != null ? g.Length : -1)}";
    }

    // 실제 획득 경로(UnitSpawner.Spawn → RegisterTo → OnAcquired)로 초월 1기를 얻는다 — 컷인이 저절로 떠야 한다.
    public static string AcquireTranscend()
    {
        CutinOverlay.DebugSpeed = 0.2f;
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_최상호_AD.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ 초월 최상호 획득(배속 0.2)";
    }

    // 미니보스 7종 prefab이 로컬에서 NULL이 아닌지
    public static string MinibossCheck()
    {
        int n = 0, bad = 0; var names = new System.Text.StringBuilder();
        foreach (string g in UnityEditor.AssetDatabase.FindAssets("t:PirateQuestData"))
        {
            var q = UnityEditor.AssetDatabase.LoadAssetAtPath<PirateQuestData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (q == null || q.miniboss == null) continue;
            n++; if (q.miniboss.prefab == null) { bad++; names.Append(q.miniboss.name + " "); }
        }
        return $"✅ 미니보스 {n}종 · prefab NULL {bad} {names}";
    }

    // 10-08 스킬 아이콘 연결 촬영용 — 제한_김강민
    public static string SpawnKimKm()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/제한_김강민.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
        return "✅ " + unit.DisplayName + " 세움";
    }
    public static string SelectKimKm() => SelectByPart("제한_김강민");
}
