using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 광폭화 유닛 실측(2026-10-07 구현담당1, 빌드 시험용 -mpTestBerserk 초) — 호스트/솔로 창에서 R1부터 확률 100%로 광폭화를 내보내(R61 규칙은 에디터 빌드 밖이라 최소 라운드를 0으로 덮는다)
/// 왕복·방어 오라·회복 오라·이름·처치 뒤 해제를 로그([BZK])로 본다.
/// </summary>
public class NetBerserkTest : MonoBehaviour
{
    public float startAt = 10f;

    void Awake()
    {
        WaveSpawner.BerserkChanceOverride = 1f;
        WaveSpawner.BerserkMinRoundOverride = 0;
    }

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(startAt);
        if (!GameAuthority.IsServer) { yield return ClientRoutine(); yield break; }
        BerserkMob mob = null;
        for (int w = 0; w < 120 && mob == null; w++)
        {
            mob = BerserkMob.Active.FirstOrDefault(m => m != null);
            if (mob == null) yield return new WaitForSecondsRealtime(1f);
        }
        if (mob == null) { Debug.Log("[BZK] ❌ 광폭화 유닛이 안 나옴"); yield break; }
        EnemyDummy me = mob.GetComponent<EnemyDummy>();
        WaypointMover mv = mob.GetComponent<WaypointMover>();
        foreach (Vector3 pt in new[] { mv.ShuttleA, mv.ShuttleB })
        {
            bool on = UnityEngine.AI.NavMesh.SamplePosition(pt, out UnityEngine.AI.NavMeshHit nh, 3f, UnityEngine.AI.NavMesh.AllAreas);
            bool onRoad = UnityEngine.AI.NavMesh.SamplePosition(pt + new Vector3(45f, 0f, 0f), out UnityEngine.AI.NavMeshHit nr, 3f, UnityEngine.AI.NavMesh.AllAreas);
            string onText = on ? "있음(거리 " + Vector3.Distance(pt, nh.position).ToString("F1") + ", 영역 " + nh.mask + ")" : "없음";
            Debug.Log("[BZK] NavMesh 점검 " + pt.ToString("F0") + ": 3 안 NavMesh " + onText + " · 길 중심(+45) " + (onRoad ? "있음" : "없음"));
        }
        Debug.Log($"[BZK] 광폭화 등장: 「{me.DisplayName}」 크기 {mob.transform.localScale.x:F2} · 왕복 {mv.Shuttling} · 변 {mv.ShuttleA:F0} ↔ {mv.ShuttleB:F0} · 방어 반경 {mob.DefenseRadius:F0} · 회복 반경 {mob.RegenRadius:F0} · 이속 {me.MoveSpeed:F1}");

        RtsCameraController cam = FindFirstObjectByType<RtsCameraController>();
        // 1) 왕복 — 20초 동안 위치를 찍는다(x는 변의 x 근처, z는 두 점 사이를 오가야 한다)
        float zMin = float.MaxValue, zMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue; int flips = 0; float lastDz = 0f;
        Vector3 prev = mob.transform.position;
        for (int k = 0; k < 80; k++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (mob == null) break;
            if (cam != null) cam.MoveTo(mob.transform.position);
            Vector3 p = mob.transform.position;
            zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z); xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
            float dz = p.z - prev.z; if (dz * lastDz < 0f && Mathf.Abs(dz) > 0.01f) flips++; if (Mathf.Abs(dz) > 0.01f) lastDz = dz;
            prev = p;
        }
        Debug.Log($"[BZK] 왕복 20초: x {xMin:F0}~{xMax:F0} · z {zMin:F0}~{zMax:F0}(변 z {Mathf.Min(mv.ShuttleA.z, mv.ShuttleB.z):F0}~{Mathf.Max(mv.ShuttleA.z, mv.ShuttleB.z):F0}) · 방향 전환 {flips}회");

        // 2) 오라 — 범위 안 일반 적의 방어·회복을 본다
        EnemyDummy other = EnemyDummy.Active.FirstOrDefault(e => e != null && e != me && !e.IsDead && !e.HasBuff(BerserkMob.BuffId) && Vector3.Distance(e.transform.position, mob.transform.position) < mob.DefenseRadius * 0.8f);
        if (other == null) { Debug.Log("[BZK] 범위 안 일반 적 없음 — 오라 점검 건너뜀"); yield break; }
        float armorWith = other.EffectiveArmor, maxHp = other.MaxHp;
        other.TakeDamage(maxHp * 0.5f, DamageType.AD, AttackType.Unassigned, -1, armorIgnoreRatio: 1f, isAbilityDamage: false);
        float inRange = 0f, gained = 0f, lastHp = other.Hp;
        for (int k = 0; k < 16 && other != null; k++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (other == null || mob == null) break;
            bool near = Vector3.Distance(other.transform.position, mob.transform.position) < mob.RegenRadius * 0.95f;
            if (near && other.Hp < other.MaxHp * 0.99f) { inRange += 0.25f; gained += other.Hp - lastHp; }
            lastHp = other.Hp;
        }
        Debug.Log($"[BZK] 오라 안: 방어 {armorWith:F1} · 범위 안 {inRange:F2}초 동안 회복 {gained:F1} = 최대체력 {maxHp:F0}의 {(inRange > 0f ? gained / inRange / maxHp * 100f : 0f):F2}%/초 ({BerserkMob.Active.Count(m => m != null)}기 → 1.00×기수 기대)");

        // 3) 처치 → 오라 해제
        int alive = BerserkMob.Active.Count(m => m != null);
        foreach (BerserkMob m in BerserkMob.Active.Where(m => m != null).ToList()) m.GetComponent<EnemyDummy>().TakeDamage(1e12f, DamageType.AD, AttackType.Unassigned, -1, armorIgnoreRatio: 1f, isAbilityDamage: false);
        Debug.Log($"[BZK] 광폭화 {alive}기 전부 처치 (동시에 {alive}기였다 → 회복 오라 {alive}개 겹침, 방어 오라는 같은 버프라 최댓값 하나)");
        yield return new WaitForSecondsRealtime(1.5f);
        if (other != null)
        {
            float a2 = other.EffectiveArmor; float h2 = other.Hp;
            if (other.Hp < other.MaxHp * 0.95f) { yield return new WaitForSecondsRealtime(3f); }
            Debug.Log($"[BZK] 처치 뒤: 광폭화 {(mob == null ? "사라짐" : "남음")} · 일반 적 방어 {armorWith:F1} → {a2:F1}(−5 기대) · 3초 회복 {other.Hp - h2:F0}(0 기대) · 아직 활성 광폭화 {BerserkMob.Active.Count(m => m != null)}기");
        }
        else Debug.Log("[BZK] 처치 뒤 일반 적이 이미 사라짐 — 해제 점검 불가");
    }

    // 클라: 거울 중 Berserk 표시가 선 적을 찾아 이름·겉모습 부품을 로그로, 카메라는 그쪽으로.
    IEnumerator ClientRoutine()
    {
        RtsCameraController cam = FindFirstObjectByType<RtsCameraController>();
        for (int k = 0; k < 160; k++)
        {
            NetEntity found = FindObjectsByType<NetEntity>(FindObjectsSortMode.None).FirstOrDefault(e => e != null && e.Object != null && e.Object.IsValid && e.EntityKind == NetEntityKind.Enemy && e.Berserk && e.Visual != null);
            if (found != null)
            {
                if (cam != null) cam.MoveTo(found.Visual.transform.position);
                if (k % 8 == 0)
                {
                    found.Visual.TryGetComponent(out EnemyDummy ed);
                    Debug.Log($"[BZK] 클라: 광폭화 거울 「{(ed != null ? ed.DisplayName : "?")}」 · 겉모습 부품 BerserkLook {found.Visual.TryGetComponent(out BerserkLook _)} · 크기 {found.Visual.transform.lossyScale.x:F2} · 위치 {found.Visual.transform.position:F0}");
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
