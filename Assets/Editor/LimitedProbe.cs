using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>제한됨 실측(10-06 구현담당3) — gameshot: call:LimitedProbe.Recipes call:LimitedProbe.Units wait:4 call:LimitedProbe.Aura (신 모드).</summary>
public static class LimitedProbe
{
    static UnitData U(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static int Mine() => UnitIdentity.Active.Count(u => u != null && u.OwnerId == 0 && !u.IsSummon);
    static void Clear() { foreach (var u in UnitIdentity.Active.Where(x => x != null && x.OwnerId == 0 && !x.IsSummon).ToList()) u.Consume(); }
    static readonly MethodInfo CastLevel = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);

    static EnemyDummy Make(Vector3 at, float lost = 0f, string asset = "Enemy_R45_이현빈")
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{asset}.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(0);
        typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, d.MaxHp * (1f - lost));
        return d;
    }

    static string Try(string label, string recipeName, params string[] units)
    {
        Clear();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{recipeName}.asset");
        PlayerContext ctx = PlayerContext.Local;
        ctx.GoldWallet.Add(50000); ctx.ResourceWallet.Add(ResourceType.Wood, 20); ctx.ResourceWallet.Add(ResourceType.Token, 5); ctx.ResourceWallet.Add(ResourceType.LuckyToken, 5);
        foreach (string n in units) spawner.Spawn(U(n), LaneMarker.Get(0).TakeSpawnPosition(U(n)), 0);
        int before = Mine();
        bool ok = Object.FindFirstObjectByType<CombineSystem>().TryCombine(recipe);
        string left = string.Join(",", UnitIdentity.Active.Where(x => x != null && x.OwnerId == 0 && !x.IsSummon && x.Data != null).Select(x => x.Data.name));
        return $"   {label}: TryCombine {ok} · 내 유닛 {before} → {Mine()} · 남은 [{left}]";
    }

    public static string Recipes()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Try("김강민(특별 박기찬)", "제한_김강민", "전설적인_박성호", "히든_서승혁", "희귀함_황준석", "특별함_박기찬", "특별함_조도연"));
        sb.AppendLine(Try("이유범(특별 박기찬)", "제한_이유범", "전설적인_이시원", "전설적인_양문호", "희귀함_노수신", "특별함_박기찬"));
        sb.AppendLine(Try("김민규(특별 강주혁·김용태)", "제한_김민규", "전설적인_김민규", "히든_전유라", "희귀함_김청운", "특별함_강주혁", "특별함_김용태"));
        Clear();
        return sb.ToString();
    }

    public static string Units()
    {
        var sb = new StringBuilder();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        Vector3 c = LaneMarker.Get(0).LaneCenter + Vector3.back * 80f;
        // 김민규
        UnitAttacker min = spawner.Spawn(U("제한_김민규"), LaneMarker.Get(0).TakeSpawnPosition(U("제한_김민규")), 0).GetComponent<UnitAttacker>();
        UnitData md = U("제한_김민규");
        sb.AppendLine($"   김민규 스킬 {md.skills.Count}: {string.Join(" / ", md.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 체력 게이지 {md.lifeGaugeMax}");
        EnemyDummy victim = Make(min.transform.position + Vector3.forward * 30f);
        SkillData del = md.skills.First(s => s.skillName.Contains("유닛삭제"));
        CastLevel.Invoke(min, new object[] { del.levels[0], del.levels[0].WorldRange, null, 0f });
        sb.AppendLine($"     유닛삭제 시전: 일반 적 {(victim == null || victim.IsDead || !victim.gameObject.activeInHierarchy ? "사라짐" : "남음")}");
        EnemyDummy toRecruit = Make(min.transform.position + Vector3.back * 30f);
        SkillData rec = md.skills.First(s => s.skillName.Contains("유닛회유"));
        CastLevel.Invoke(min, new object[] { rec.levels[0], rec.levels[0].WorldRange, null, 0f });
        var recruits = (System.Collections.IList)typeof(UnitAttacker).GetField("recruits", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(min);
        sb.AppendLine($"     유닛회유 시전: 회유 유닛 {recruits.Count}기(기대 1)");

        // 강보명
        UnitData bd = U("제한_강보명");
        UnitAttacker bo = spawner.Spawn(bd, LaneMarker.Get(0).TakeSpawnPosition(bd), 0).GetComponent<UnitAttacker>();
        sb.AppendLine($"   강보명 스킬 {bd.skills.Count}: {string.Join(" / ", bd.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 스플래시 {bd.attackSplashRadius} · 멀티샷 +{bd.attackExtraTargets}(반경 {bd.attackExtraTargetRadius:F0})");
        EnemyDummy t = Make(c), inside = Make(c + Vector3.right * 15f), outside = Make(c + Vector3.forward * 200f);
        SkillData bump = bd.skills.First(s => s.skillName.StartsWith("범퍼"));
        float r = bump.levels[0].WorldRange; float t0 = t.Hp, i0 = inside.Hp, o0 = outside.Hp;
        CastLevel.Invoke(bo, new object[] { bump.levels[0], r, t, 0f });
        sb.AppendLine($"     범퍼(현재체력 2%): 표적 {(1 - t.Hp / t0) * 100f:F2}% · 반경 안 {(1 - inside.Hp / i0) * 100f:F2}% · 밖 {(1 - outside.Hp / o0) * 100f:F2}%");
        var factor = typeof(UnitAttacker).GetMethod("DamagePassiveFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        EnemyDummy boss = Make(c + Vector3.left * 15f, 0f, "Enemy_R60_정윤식"), zerk = Make(c + Vector3.left * 30f);
        zerk.AddBuff("B06B", 30f);
        sb.AppendLine($"     피해 계수: 일반 {(float)factor.Invoke(bo, new object[] { t }):F2} · 보스 {(float)factor.Invoke(bo, new object[] { boss }):F2}(기대 1.30) · 광폭화 몹 {(float)factor.Invoke(bo, new object[] { zerk }):F2}(기대 1.50)");

        // 이충민
        UnitData cd = U("제한_이충민");
        UnitAttacker ch = spawner.Spawn(cd, LaneMarker.Get(0).TakeSpawnPosition(cd), 0).GetComponent<UnitAttacker>();
        SkillData inv = cd.skills.First(s => s.skillName.StartsWith("발명품제작"));
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < 500; i++) { CastLevel.Invoke(ch, new object[] { inv.levels[0], 0f, null, 0f }); string k = ch.LastInventionResult ?? "?"; counts[k] = counts.TryGetValue(k, out int n) ? n + 1 : 1; }
        sb.AppendLine($"   이충민 스킬 {cd.skills.Count}: {string.Join(" / ", cd.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 마나 {cd.manaMax} · 마젠 {cd.manaAuraRegenPerSecond} · 체젠 {cd.lifeAuraRegenPerSecond}");
        sb.AppendLine($"     발명품제작 500회: {string.Join(" · ", counts.Select(kv => kv.Key + " " + kv.Value))} (기대 각 100)");
        foreach (var u in UnitIdentity.Active.Where(x => x != null && x.OwnerId == 0 && x.IsSummon).ToList()) Object.Destroy(u.gameObject);

        // 전법규
        UnitData fd = U("제한_전법규");
        UnitAttacker fa = spawner.Spawn(fd, LaneMarker.Get(0).TakeSpawnPosition(fd), 0).GetComponent<UnitAttacker>();
        SkillData mana = fd.skills.First(s => s.skillName.StartsWith("마나스킬"));
        EnemyDummy ft = Make(fa.transform.position + Vector3.forward * 40f, 0.5f), fa2 = Make(fa.transform.position + Vector3.forward * 60f, 0.5f), far = Make(fa.transform.position + Vector3.forward * 400f, 0.5f);
        float f0 = ft.Hp, lost = ft.MaxHp - ft.Hp;
        CastLevel.Invoke(fa, new object[] { mana.levels[0], mana.levels[0].WorldRange, ft, 0f });
        sb.AppendLine($"   전법규 스킬 {fd.skills.Count}: {string.Join(" / ", fd.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 마나 {fd.manaMax}");
        sb.AppendLine($"     마나 스킬: 표적 스턴 {ft.IsStunned}·근처 {fa2.IsStunned}·먼 적 {far.IsStunned}(기대 True/True/False) · 표적 잃은 체력의 {(f0 - ft.Hp) / lost * 100f:F2}%(기대 6%×상성)");
        SkillData tp = fd.skills.First(s => s.skillName.StartsWith("순간이동"));
        Vector3 s0 = fa.transform.position;
        bool ok = fa.TryCastActiveAtPoint(tp, LaneMarker.Get(0).LaneCenter, out string why);
        sb.AppendLine($"     순간이동 {(ok ? "성공" : why)} · {Vector3.Distance(s0, fa.transform.position):F0} 이동");
        return sb.ToString();
    }

    public static string Aura()
    {
        var sb = new StringBuilder();
        var chung = UnitIdentity.Active.First(u => u != null && u.Data != null && u.Data.name == "제한_이충민").GetComponent<UnitAttacker>();
        var other = UnitIdentity.Active.First(u => u != null && u.Data != null && u.Data.name == "제한_강보명").GetComponent<UnitAttacker>();
        var total = typeof(UnitAttacker).GetMethod("AuraBonusTotal", BindingFlags.NonPublic | BindingFlags.Instance);
        float a = (float)total.Invoke(other, new object[] { SkillEffectKind.SkillTriggerChanceBonus, false }), b = (float)total.Invoke(chung, new object[] { SkillEffectKind.SkillTriggerChanceBonus, false });
        sb.AppendLine($"   확률조작 오라: 강보명(아군) +{a:F2} · 이충민(자기) +{b:F2} (기대 +0.25)");
        var snl = UnitIdentity.Active.First(u => u != null && u.Data != null && u.Data.name == "제한_전법규");
        EnemyDummy slowTarget = Make(snl.transform.position + Vector3.forward * 50f);
        sb.AppendLine("   (이감 오라는 0.25초 틱 뒤 별도 확인)");
        return sb.ToString();
    }
}
