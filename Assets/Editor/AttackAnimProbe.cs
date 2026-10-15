using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 공격 모션 ↔ 공속 점검(2026-10-16). gameshot call:AttackAnimProbe.Begin → (spawn 뒤 시간 경과) → call:AttackAnimProbe.Report
// 유닛별로 PlayAttack 호출 간격과 공격 상태 재시작(정규화 시간이 되감김) 횟수를 잰다.
public static class AttackAnimProbe
{
    class Track
    {
        public string name; public Animator anim; public CharacterAnimator ca;
        public List<float> attackTimes = new List<float>();
        public List<float> restartAt = new List<float>();   // 재시작 직전 정규화 시간
        public float lastNorm = -1; public bool lastAttack; public float clipLen; public string state = "";
        public int motionStarts; public string clips = "";
    }
    static readonly Dictionary<CharacterAnimator, Track> tracks = new Dictionary<CharacterAnimator, Track>();
    static bool running;

    public static string Begin()
    {
        tracks.Clear();
        if (!running) { running = true; EditorApplication.update += Tick; EditorApplication.playModeStateChanged += OnMode; }
        return "탐침 시작";
    }

    static void OnMode(PlayModeStateChange c)
    {
        if (c != PlayModeStateChange.ExitingPlayMode) return;
        EditorApplication.playModeStateChanged -= OnMode;
        Debug.Log("[공격모션]" + Report());   // gameshot 결과에 실린다([ 태그 로그)
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { running = false; EditorApplication.update -= Tick; return; }
        foreach (var ca in Object.FindObjectsByType<CharacterAnimator>(FindObjectsSortMode.None))
        {
            if (ca == null || tracks.ContainsKey(ca)) continue;
            if (!ca.TryGetComponent(out UnitIdentity id) || id.Data == null || id.GetComponent<Wisp>() != null) continue;
            var a = ca.GetComponentInChildren<Animator>();
            if (a == null || a.runtimeAnimatorController == null) continue;
            var t = new Track { name = id.name, anim = a, ca = ca };
            
            t.clips = string.Join('/', a.runtimeAnimatorController.animationClips.Select(c => c.name + ":" + c.length.ToString("F2")));
            tracks[ca] = t;
            ca.AttackPlayed += () => t.attackTimes.Add(Time.time);
        }
        foreach (var t in tracks.Values)
        {
            if (t.anim == null) continue;
            var info = t.anim.GetCurrentAnimatorStateInfo(0);
            bool attack = info.IsName("Attack") || info.IsName("Attack 2") || info.IsName("Attack 3");
            float norm = info.normalizedTime;
            if (attack && (!t.lastAttack || norm < t.lastNorm - 0.02f))
            {
                t.motionStarts++;
                if (t.lastAttack) t.restartAt.Add(t.lastNorm);
            }
            if (attack) { var ci = t.anim.GetCurrentAnimatorClipInfo(0); if (ci.Length > 0 && ci[0].clip != null) t.clipLen = ci[0].clip.length / Mathf.Max(0.01f, info.speed); }
            t.lastAttack = attack; t.lastNorm = norm;
        }
    }

    public static string Report()
    {
        var sb = new StringBuilder("\n");
        foreach (var t in tracks.Values.Where(x => x.name.Contains("장진희") || x.attackTimes.Count > 0).OrderBy(x => x.name))
        {
            float mean = t.attackTimes.Count > 1 ? (t.attackTimes.Last() - t.attackTimes.First()) / (t.attackTimes.Count - 1) : 0f;
            string restarts = t.restartAt.Count == 0 ? "-" : string.Join(",", t.restartAt.Take(6).Select(v => v.ToString("F2")));
            sb.AppendLine($"  {t.name}: 타격 {t.attackTimes.Count}회 · 평균 간격 {mean:F2}s · Attack 클립 {t.clipLen:F2}s · 모션 시작 {t.motionStarts}회 · 중간 재시작 {t.restartAt.Count}회(그때 진행도 {restarts}) · 클립 {t.clips}");
        }
        return sb.ToString();
    }
}
