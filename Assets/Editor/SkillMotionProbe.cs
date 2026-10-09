using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 스킬 시전 모션 촬영(사장님 10-09 「초월은 스킬 쓸 때 모션이 어떻게 나오나」). 유닛을 적 앞에 세우고 스킬을 CastSkillLevel로 1초마다 한 번씩(평타 섞어) 돌리며
/// Time.captureFramerate=20으로 게임 시간을 프레임에 맞춰 매 프레임 화면을 /tmp/g1n_motion/&lt;유닛&gt;/f0000.png…로 저장한다(ffmpeg가 mp4로 묶는다).
///   ClaudeBridge/g1n_motion.txt = 유닛 이름 한 줄에 하나(초월_노태현_AP …)
///   gameshot x.png 1 1280x720 click?:쉬움 mode:쉬움 wait:2 call:SkillMotionProbe.Begin wait:400 call:SkillMotionProbe.Report
/// </summary>
static class SkillMotionProbe
{
    const int Fps = 20;
    const float Seconds = 7f;
    static readonly StringBuilder log = new StringBuilder();
    static bool done;
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

    static string Begin()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        done = false; log.Clear();
        var host = Object.FindFirstObjectByType<SelectionManager>();
        host.StartCoroutine(Run(File.ReadAllLines("ClaudeBridge/g1n_motion.txt").Select(l => l.Trim()).Where(l => l.Length > 0).ToList()));
        return "시작";
    }

    static string Report() => (done ? "끝\n" : "진행 중\n") + log;

    static IEnumerator Run(List<string> names)
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var rts = Object.FindFirstObjectByType<RtsCameraController>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        float k = 1f / WorldScale.Value;
        Time.captureFramerate = Fps;
        foreach (string name in names)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
            if (data == null) { log.AppendLine("❌ 없음 " + name); continue; }
            string dir = $"/tmp/g1n_motion/{name}"; Directory.CreateDirectory(dir);
            foreach (string old in Directory.GetFiles(dir, "f*.png")) File.Delete(old);
            Vector3 c = lane.LaneCenter;   // 한 유닛씩 차례로(끝나면 치운다) — 레인 밖은 NavMesh가 없어 유닛이 안 선다
            GameObject go = spawner.Spawn(data, c, 0);
            var atk = go.GetComponent<UnitAttacker>();
            float baseRange = Mathf.Max(3f, data.attackRange * k);
            var enemies = new List<EnemyDummy>();
            var offsets = new[] { new Vector3(1, 0, 0.3f), new Vector3(0.9f, 0, -0.5f), new Vector3(1.4f, 0, 0.1f), new Vector3(0.4f, 0, 1f) };
            foreach (Vector3 o in offsets)
            {
                GameObject eg = Object.Instantiate(enemyData.prefab, c + o.normalized * baseRange * 0.7f, Quaternion.identity);
                if (eg.TryGetComponent(out WaypointMover mv)) mv.enabled = false;
                var e = eg.GetComponent<EnemyDummy>(); e.Initialize(enemyData, 1e6f); e.SetLane(-1);
                enemies.Add(e);
            }
            // 모션 정보
            var anim = go.GetComponentInChildren<Animator>();
            var ca = go.GetComponent<CharacterAnimator>() ?? go.GetComponentInChildren<CharacterAnimator>();
            string ctrl = anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "(Animator 없음)";
            string pars = anim != null ? string.Join(",", anim.parameters.Select(p => p.name)) : "-";
            string clips = anim != null && anim.runtimeAnimatorController != null ? string.Join(",", anim.runtimeAnimatorController.animationClips.Select(x => x.name).Distinct()) : "-";
            log.AppendLine($"[{name}] 컨트롤러 {ctrl} · 파라미터 [{pars}] · 클립 [{clips}] · 스킬 {data.skills.Count}개 [{string.Join(" | ", data.skills.Select(s => s.skillName.Split(' ')[0] + "/" + s.triggerType))}]");
            for (int w = 0; w < 120; w++) yield return null;   // 획득 컷인·알림이 지나가길 기다린 뒤 찍는다
            rts.FlyTo(c, 150f);
            for (int w = 0; w < 40; w++) yield return null;   // 카메라 도착
            MethodInfo cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", NP);
            int frames = Mathf.RoundToInt(Fps * Seconds), castNo = 0;
            for (int f = 0; f < frames; f++)
            {
                if (f % Fps == Fps / 2 && data.skills.Count > 0)
                {
                    SkillData s = data.skills[castNo % data.skills.Count]; castNo++;
                    try { cast.Invoke(atk, new object[] { s.levels[0], s.levels[0].WorldRange, enemies[castNo % enemies.Count], atk.AttackDamage }); }
                    catch (System.Exception ex) { log.AppendLine("   시전 예외 " + s.skillName + " " + ex.InnerException?.Message); }
                }
                ScreenCapture.CaptureScreenshot($"{dir}/f{f:D4}.png");
                yield return null;
            }
            log.AppendLine($"   촬영 {frames}장 · 시전 {castNo}회");
            foreach (var e in enemies) if (e != null) Object.Destroy(e.gameObject);
            if (go != null) Object.Destroy(go);
            yield return null;
        }
        Time.captureFramerate = 0;
        done = true;
    }

}
