using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원작 스킬 연출 재생기(10-09): CinematicScript 시간표대로 모델(더미)을 만들고 움직이고 지운다. 피해·판정은 안 건드린다(연출만).
/// 각 클라가 로컬로 재생한다(호스트는 스킬 발동 때 대본 번호만 보낸다). 연출이 끊겨도 게임에 영향 없다 — 예외는 삼킨다.
/// 단위: 워3 단위 ÷ WorldScale = 월드, 모델(FBX 미터) 1m = 100/WorldScale 월드. 모델 크기 = baseScale × scalePercent/100.
/// </summary>
public static class SkillCinematic
{
    public static bool Enabled = true;
    /// <summary>MP: 호스트가 연출을 시작할 때(표 번호·시전자·대상) — NetGameState가 친구 화면으로 넘긴다. 연출은 각 클라가 로컬 재생한다.</summary>
    public static event System.Action<int, Vector3, Vector3> Played;
    static int running;

    /// <summary>스킬을 쏠 때 부른다(SkillVfx.CastAt). 표에 있는 스킬이면 연출을 시작한다. 연출은 게임 판정과 무관 — 예외는 삼킨다.</summary>
    public static void OnCast(SkillData skill, Vector3 casterPos, Vector3 targetPos)
    {
        if (!Enabled || skill == null) return;
        try
        {
            SkillCinematicTable table = SkillCinematicTable.Instance;
            if (table == null) return;
            int index = table.Find(skill);
            if (index >= 0) PlayByIndex(index, casterPos, targetPos, notify: true);
        }
        catch (System.Exception e) { Debug.LogWarning("[연출] 시작 실패: " + e.Message); }
    }

    public static void PlayByIndex(int index, Vector3 casterPos, Vector3 targetPos, bool notify)
    {
        SkillCinematicTable table = SkillCinematicTable.Instance;
        if (!Enabled || table == null || index < 0 || index >= table.scripts.Count || table.scripts[index] == null) return;
        if (running >= 6) return;   // 한꺼번에 너무 많이 겹치지 않게
        GameObject go = Play(table.scripts[index], casterPos, targetPos);
        if (go == null) return;
        running++;
        go.AddComponent<RunningCounter>();
        if (notify) Played?.Invoke(index, casterPos, targetPos);
    }

    sealed class RunningCounter : MonoBehaviour { void OnDestroy() { running = Mathf.Max(0, running - 1); } }

    public const float MetersToWorld = 100f / WorldScale.Value;

    /// <summary>대본을 재생한다. casterPos·targetPos는 월드 좌표(대상이 없으면 시전자 앞 지점).</summary>
    public static GameObject Play(CinematicScript script, Vector3 casterPos, Vector3 targetPos)
    {
        if (!Enabled || script == null) return null;
        var go = new GameObject("Cinematic_" + script.scriptId);
        var runner = go.AddComponent<CinematicRunner>();
        runner.Begin(script, casterPos, targetPos);
        return go;
    }

    sealed class Inst
    {
        public GameObject go;
        public OriginalVfxPlayer player;
        public Pre2Driver[] drivers;
        public float baseScale, scalePercent = 100f, flyHeight, rampTo, rampRate;
        public bool ramping;
        public Vector3 anchorPos;
        public float polarRadius, polarAngle;
        public bool hasMove; public Vector3 moveTarget; public float moveSpeed;
        public float spawnedAt, lifeSec = -1f, deathSec = 0.1f;
        public bool dying; public float dieAt;
        public float yaw;
        public float substituteScale;   // >0이면 대체 모델 스케일(= 목표 길이/원래 길이)
    }

    sealed class CinematicRunner : MonoBehaviour
    {
        CinematicScript script;
        Vector3 casterPos, targetPos;
        float elapsed;
        int next;
        readonly Dictionary<string, Inst> insts = new Dictionary<string, Inst>();
        readonly List<Inst> all = new List<Inst>();
        readonly List<CinematicScript.Ev> events = new List<CinematicScript.Ev>();

        public void Begin(CinematicScript s, Vector3 caster, Vector3 target)
        {
            script = s; casterPos = caster; targetPos = target;
            events.AddRange(s.events);
            events.Sort((a, b) => a.t.CompareTo(b.t));
        }

        Vector3 AnchorPos(CinematicScript.Anchor a) => a == CinematicScript.Anchor.Target ? targetPos : casterPos;

        void Update()
        {
            elapsed += Time.deltaTime;
            try
            {
                while (next < events.Count && events[next].t <= elapsed) Fire(events[next++]);
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    Inst n = all[i];
                    if (n.go == null) { all.RemoveAt(i); continue; }
                    Tick(n);
                    if (n.dying && elapsed >= n.dieAt) { Destroy(n.go); all.RemoveAt(i); }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[연출] 예외 — 이 연출을 끝낸다: " + e.Message); Destroy(gameObject); return; }
            if (next >= events.Count && all.Count == 0 && elapsed > script.duration) Destroy(gameObject);
        }

        void Fire(CinematicScript.Ev e)
        {
            switch (e.op)
            {
                case CinematicScript.Op.Spawn: Spawn(e); break;
                case CinematicScript.Op.Set:
                    if (insts.TryGetValue(e.id, out Inst si))
                    {
                        if (e.hasScaleSet) si.scalePercent = e.scalePercent;
                        if (e.hasTimescaleSet && si.player != null) si.player.Speed = e.timescale;
                    }
                    break;
                case CinematicScript.Op.Ramp:
                    if (insts.TryGetValue(e.id, out Inst ri)) { ri.ramping = true; ri.rampTo = e.rampTo; ri.rampRate = Mathf.Max(0.01f, e.rampRate); }
                    break;
                case CinematicScript.Op.Kill:
                    if (insts.TryGetValue(e.id, out Inst ki)) Kill(ki);
                    break;
                case CinematicScript.Op.CameraShake:
                    CinematicCameraShake.Shake(casterPos, e.shakeMagnitude, e.shakeDuration);
                    break;
            }
        }

        void Spawn(CinematicScript.Ev e)
        {
            CinematicScript.ModelRef m = script.Model(e.model);
            if (m == null || m.prefab == null) return;   // 대체 모델은 대본 변환 때 prefab으로 채워진다(없으면 건너뜀)
            var n = new Inst
            {
                go = Instantiate(m.prefab, transform),
                baseScale = e.baseScale, scalePercent = e.scalePercent, flyHeight = e.flyHeight,
                anchorPos = AnchorPos(e.anchor), polarRadius = e.hasPolar ? e.polarRadius : 0f, polarAngle = e.polarAngleDeg,
                spawnedAt = elapsed, lifeSec = e.lifeSec, deathSec = e.deathSec,
                yaw = e.facingRandom ? Random.Range(0f, 360f) : 0f,
            };
            if (e.hasMove) { n.hasMove = true; n.moveTarget = AnchorPos(e.moveAnchor); n.moveSpeed = e.moveSpeed; }
            if (m.substituteLengthWc3 > 0f && m.substituteNativeLength > 0.001f) n.substituteScale = m.substituteLengthWc3 / WorldScale.Value / m.substituteNativeLength;
            n.player = n.go.GetComponent<OriginalVfxPlayer>();
            if (n.player != null) n.player.AlphaMultiplier = e.vertexAlpha;
            n.drivers = n.go.GetComponentsInChildren<Pre2Driver>(true);
            Tick(n);
            n.go.SetActive(true);
            if (n.player != null)
            {
                string kind = string.IsNullOrEmpty(e.anim) ? "stand" : e.anim;
                bool loop = kind == "stand" ? true : false;
                n.player.Play(kind, e.timescale <= 0f ? 1f : e.timescale, loop);
            }
            float seqStartMs = n.player != null ? n.player.CurrentStartSec * 1000f : 0f;
            foreach (Pre2Driver d in n.drivers) d.Begin(seqStartMs);
            if (!string.IsNullOrEmpty(e.id)) insts[e.id] = n;
            all.Add(n);
        }

        void Kill(Inst n)
        {
            if (n.dying) return;
            n.dying = true;
            foreach (Pre2Driver d in n.drivers) d.Halt();
            if (n.player != null && n.player.HasClip("death")) n.player.Play("death", 1f, false);
            n.dieAt = elapsed + Mathf.Max(0.1f, n.deathSec) + 1.2f;   // 남은 입자가 사라질 여유
        }

        void Tick(Inst n)
        {
            float dt = Time.deltaTime;
            if (n.ramping) { n.flyHeight = Mathf.MoveTowards(n.flyHeight, n.rampTo, n.rampRate * dt); if (Mathf.Approximately(n.flyHeight, n.rampTo)) n.ramping = false; }
            if (n.hasMove)
            {
                Vector3 to = n.moveTarget - n.anchorPos; to.y = 0f;
                float step = n.moveSpeed / WorldScale.Value * dt;
                if (to.magnitude <= step) { n.anchorPos = new Vector3(n.moveTarget.x, n.anchorPos.y, n.moveTarget.z); n.hasMove = false; }
                else n.anchorPos += to.normalized * step;
            }
            Vector3 polar = n.polarRadius > 0f ? Quaternion.Euler(0f, n.polarAngle, 0f) * Vector3.forward * (n.polarRadius / WorldScale.Value) : Vector3.zero;
            n.go.transform.SetPositionAndRotation(n.anchorPos + polar + Vector3.up * (n.flyHeight / WorldScale.Value), Quaternion.Euler(0f, n.yaw, 0f));
            n.go.transform.localScale = Vector3.one * (n.substituteScale > 0f ? n.substituteScale * n.scalePercent / 100f : n.baseScale * n.scalePercent / 100f * MetersToWorld);
            if (!n.dying && n.lifeSec > 0f && elapsed - n.spawnedAt >= n.lifeSec) Kill(n);
            else if (!n.dying && n.lifeSec < 0f && n.player != null && n.player.Finished) Kill(n);
        }
    }
}

/// <summary>연출 카메라 흔들림 — 약하게, 내 화면 안에서 터진 것만(PM 10-09). 카메라 컨트롤러가 위치를 매 프레임 갱신하므로 지난 프레임 오프셋을 먼저 되돌린다.</summary>
[DefaultExecutionOrder(1000)]
public class CinematicCameraShake : MonoBehaviour
{
    static CinematicCameraShake instance;
    Vector3 lastOffset;
    float amplitude, until, total;

    public static void Shake(Vector3 worldPos, float magnitude, float duration)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(worldPos);
        if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return;   // 내 시야 밖 연출은 화면을 안 흔든다
        if (instance == null) instance = cam.gameObject.AddComponent<CinematicCameraShake>();
        instance.amplitude = Mathf.Clamp(magnitude * 0.05f, 0.5f, 3f);
        instance.total = Mathf.Clamp(duration, 0.1f, 1.5f);
        instance.until = Time.unscaledTime + instance.total;
    }

    void LateUpdate()
    {
        transform.position -= lastOffset; lastOffset = Vector3.zero;
        float left = until - Time.unscaledTime;
        if (left <= 0f) return;
        lastOffset = Random.insideUnitSphere * (amplitude * (left / total));
        transform.position += lastOffset;
    }
}
