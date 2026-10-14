using UnityEngine;

/// <summary>
/// 화면의 한 점이 월드의 어디를 가리키는지 찾는다. 선택·이동·스킬 대상 지정이 전부 이걸 쓴다.
///
/// 예전엔 호출부마다 <c>Physics.Raycast(ray, out hit, 200f)</c>로 거리를 박아뒀는데,
/// 카메라가 기울어져 있어서 <b>화면 위쪽을 찍을수록 광선이 길어진다</b> — 카메라 높이가 144일 때
/// 화면 위쪽 끝은 340이 넘는다. 레인을 키워 카메라가 높아지자 화면 위쪽 절반이 통째로
/// 안 잡혔고, 유닛이 선택도 이동도 안 되는 것처럼 보였다.
///
/// 거리를 재지 않는다. 맵 밖에는 콜라이더가 없으므로 끝까지 쏴도 잡을 것이 없다.
/// </summary>
public static class WorldPick
{
    /// <summary>
    /// 커서 아래의 적. 먼저 광선으로 적 콜라이더를 찾고, 없으면 화면에서 <paramref name="tolerancePixels"/> 안에
    /// 가장 가까운 적(몸 가운데 기준)을 준다 — 레인 적은 화면에서 작아서 정확히 찍기 어렵다(2026-09-26 A 공격).
    /// 우클릭 공격처럼 이동과 헷갈리면 안 되는 곳은 0을 줘서 콜라이더에 맞을 때만 고른다.
    /// </summary>
    public static EnemyDummy TryPickEnemy(Camera cam, Vector2 screenPosition, float tolerancePixels)
    {
        if (cam == null) return null;

        if (TryHit(cam, screenPosition, out RaycastHit hit))
        {
            EnemyDummy direct = hit.collider.GetComponentInParent<EnemyDummy>();
            if (direct != null) return direct;
        }

        if (tolerancePixels <= 0f) return null;

        EnemyDummy best = null;
        float bestSqr = tolerancePixels * tolerancePixels;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null) continue;
            Vector3 center = enemy.TryGetComponent(out Collider col) ? col.bounds.center : enemy.transform.position;
            Vector3 sp = cam.WorldToScreenPoint(center);
            if (sp.z <= 0f) continue;
            float sqr = ((Vector2)sp - screenPosition).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = enemy;
        }
        return best;
    }

    /// <summary>
    /// 커서 둘레 <paramref name="pixels"/> 안에 화면으로 보이는 유닛·위습이 있으면 가장 가까운 것(발·몸 한가운데 중 가까운 쪽 기준). 없으면 null.
    /// 단일 클릭이 상점 건물에 먼저 맞았을 때 드래그 선택처럼 유닛을 우선하려고 쓴다(사장님 10-14 「드래그는 유닛이 잡히는데 단일 클릭은 건물」).
    /// </summary>
    public static Selectable NearestUnitOnScreen(Camera cam, Vector2 screenPosition, float pixels)
    {
        if (cam == null) return null;
        Selectable best = null; float bestSqr = pixels * pixels; bool bestMine = false;
        foreach (Selectable s in Selectable.All)
        {
            if (s == null || !s.gameObject.activeInHierarchy) continue;
            if (!s.TryGetComponent(out UnitIdentity _) && !s.TryGetComponent(out Wisp _)) continue;
            Vector3 foot = cam.WorldToScreenPoint(s.transform.position);
            if (foot.z <= 0f) continue;
            float d = ((Vector2)foot - screenPosition).sqrMagnitude;
            Collider c = s.GetComponentInChildren<Collider>();
            if (c != null)
            {
                Vector3 mid = cam.WorldToScreenPoint(c.bounds.center);
                if (mid.z > 0f) d = Mathf.Min(d, ((Vector2)mid - screenPosition).sqrMagnitude);
            }
            if (d > bestSqr) continue;
            bool mine = AllianceShare.CanControlLocal(s.gameObject);
            if (best != null && bestMine && !mine) continue;          // 내가 조종할 수 있는 것을 우선
            if (best != null && bestMine == mine && d >= bestSqr) continue;
            best = s; bestSqr = d; bestMine = mine;
        }
        return best;
    }

    public static bool TryHit(Camera cam, Vector2 screenPosition, out RaycastHit hit)
    {
        hit = default;
        if (cam == null) return false;

        if (!Physics.Raycast(cam.ScreenPointToRay(screenPosition), out hit, Mathf.Infinity)) return false;

        // 레인 상점 건물의 클릭 상자(건물보다 넉넉한 여유)가 앞에 선 흔함 유닛을 가렸다(사장님 10-14 제보) —
        // 첫 적중이 상점 상자인데 그 상자 안쪽에 서 있는 유닛(선택 가능·상점 아님)이 같은 광선에 맞으면 유닛을 우선한다.
        if (hit.collider.TryGetComponent(out ILaneShop _))
        {
            // 사장님 10-14 재제보 「흔함 칸 왼쪽 유닛을 눌러도 건물이 눌린다」 — 선택 가능한 유닛이 광선에 하나라도 맞으면 상점보다 유닛이 먼저다(상점은 유닛이 안 맞을 때만).
            int count = Physics.RaycastNonAlloc(cam.ScreenPointToRay(screenPosition), hits, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c.TryGetComponent(out ILaneShop _) || !c.TryGetComponent(out Selectable _)) continue;
                if (hits[i].distance >= bestDistance) continue;
                bestDistance = hits[i].distance;
                hit = hits[i];
            }
        }
        return true;
    }

    /// <summary>
    /// 첫 적중 거리에서 <paramref name="slack"/> 안쪽으로 겹쳐 있는, <b>내가 조종할 수 있는</b> Selectable(내 유닛·위습·공유 받은 유닛)이 있으면 가장 가까운 것을 준다.
    /// 칸마다 위습이 같은 자리에 겹쳐 나오는 판(10-08)에서 맨 위 남의 위습에 가려진 내 위습을 고르려는 것.
    /// </summary>
    public static bool TryHitControllable(Camera cam, Vector2 screenPosition, float firstDistance, out RaycastHit best, float slack = 40f)
    {
        best = default;
        if (cam == null) return false;
        int count = Physics.RaycastNonAlloc(cam.ScreenPointToRay(screenPosition), hits, firstDistance + slack, ~0, QueryTriggerInteraction.Collide);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (!hits[i].collider.TryGetComponent(out Selectable s) || !AllianceShare.CanControlLocal(s.gameObject)) continue;
            if (hits[i].distance >= bestDistance) continue;
            best = hits[i]; bestDistance = hits[i].distance; found = true;
        }
        return found;
    }

    // RaycastAll이 담을 자리. 이동 명령마다 새로 할당하지 않으려고 재사용한다.
    static readonly RaycastHit[] hits = new RaycastHit[32];

    /// <summary>
    /// <b>땅</b>을 찾는다 — 유닛·적은 통과시킨다.
    ///
    /// 이동 명령은 "저 바닥으로 가라"는 뜻이지 "저 유닛을 클릭했다"가 아니다. 그런데 캐릭터가
    /// 키 20이라 화면에서 차지하는 면적이 크고, 위습은 여럿이 뭉쳐 있다 — 커서와 목적지 사이에
    /// 자기 유닛이 끼면 광선이 거기서 멈춰서, 그 유닛의 몸통 좌표를 목적지로 삼으려다 실패한다.
    /// 화면상 자기 부대 너머를 못 찍는 셈이라, 뭉쳐 있을수록 조작이 막힌다.
    /// </summary>
    public static bool TryHitGround(Camera cam, Vector2 screenPosition, out RaycastHit hit)
    {
        hit = default;
        if (cam == null) return false;

        // 🔴 트리거는 땅이 아니다 — 무시한다(2026-09-24). 포탈은 isTrigger 상자라, 예전엔 광선이 그 **윗면**(y 20.54)에 먼저 맞아
        //    목적지가 공중이 됐고, UnitMover가 거기서 반경 8.3 안에 NavMesh(섬 윗면 y 8)를 못 찾아 **이동 명령을 버렸다.**
        //    위습을 포탈 원 위에 우클릭하면 안 가고 옆 땅을 찍어야 가던 원인이다(gameshot select:/rclick: 재현, outbox 2060).
        //    트리거를 건너뛰면 광선이 포탈 밑 실제 바닥에 닿아 목적지가 원 한가운데가 되고, 걸어 들어가 트리거가 발동한다.
        int count = Physics.RaycastNonAlloc(cam.ScreenPointToRay(screenPosition), hits, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        if (count == 0) return false;

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (IsCharacter(hits[i].collider)) continue;
            if (found && hits[i].distance >= hit.distance) continue;

            hit = hits[i];
            found = true;
        }

        if (found) return true;

        // 온통 유닛뿐이면 제일 가까운 것이라도 준다 — 아무것도 안 주는 것보다는 낫다.
        hit = hits[0];
        for (int i = 1; i < count; i++)
            if (hits[i].distance < hit.distance) hit = hits[i];

        return true;
    }

    static bool IsCharacter(Collider collider)
    {
        if (collider == null) return true;
        return collider.GetComponent<Selectable>() != null || collider.GetComponent<EnemyDummy>() != null;
    }
}
