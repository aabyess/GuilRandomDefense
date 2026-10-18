using UnityEngine;

/// <summary>
/// S2 2.323 「오로성 저주」(j:8737~8960 BeQ·BeR·BeS, 시작 j:58970~58980, 점수판 j:48976~48991).
/// 난이도 신·악몽에서 판 시작에 오로성 셋(워큐리·새턴·나스쥬로) 중 하나를 뽑아 맵 정중앙에 검은 실루엣으로 세우고
/// 「오로성은 신세계 이후에 등장합니다」를 띄운다. 신세계에 들어가면 색이 차오르며 효과가 걸린다(수치는 j 그대로, 악몽은 괄호):
///   워큐리: 적 방어 +10(15) · 적 마저항 +10%(15%) · 보스 체력 +1000만(1500만)
///   새턴:   아군 공격력 −20%(30%) · 아군 폭발형 피해 −7%(10%) · 적 재생 +22.5만(30만)
///   나스쥬로: 적 이동속도 +10%(15%) · 아군 공속 −10%(15%) · 라인몹 체력 +1000만(1500만)
///   공통 난이도 추가: 신은 모든 적 체력 +500만·재생 +25만, 악몽은 +1000만·+50만.
/// 해제: 누가 「니카(기어 피프스)」를 얻으면 에메트가 나와 오로성을 퇴치한다(걸려 있었으면 효과가 풀린다). 우리에 니카 유닛이 아직 없어
///   UnitSpawner가 부르는 OnUnitSpawned가 이름에 「니카」가 든 유닛을 알아본다(유닛이 생기면 그대로 작동).
/// 실루엣은 모든 PC가 자기 화면에 세운다 — 호스트가 NetGameState(S2Saint·S2OroPhase·S2OroNightmare)로 알리면 클라가 ClientSync로 같은 오로성을 세우고, 색 차오름·에메트 퇴치 사라짐도 따라간다(효과 계산·공지는 호스트 몫, 공지는 알림 경로로 전원에게). 원작 모델(Saint_*.mdl)은 Resources/S2/Saint_<이름> 프리팹이 있으면 쓰고 없으면 캡슐.
/// </summary>
public static class OroSeong
{
    public enum Saint { None = 0, Warcury = 1, Saturn = 2, Nasjuro = 3 }

    public static Saint Chosen { get; private set; }
    public static bool Active { get; private set; }
    public static bool Dismissed { get; private set; }
    static bool nightmare;
    static OroSeongSilhouette silhouette;
    static bool fillStarted, clientRetired;

    public static string Name(Saint s) => s == Saint.Warcury ? "워큐리" : s == Saint.Saturn ? "새턴" : s == Saint.Nasjuro ? "나스쥬로" : "";

    public static void ResetMatch()
    {
        Chosen = Saint.None; Active = false; Dismissed = false; nightmare = false; fillStarted = false; clientRetired = false;
        if (silhouette != null) Object.Destroy(silhouette.gameObject);
        silhouette = null;
    }

    // ---- 효과 읽기(아무것도 안 걸렸으면 전부 중립값) ----
    static bool Is(Saint s) => Active && Chosen == s;
    static float Pick(float god, float night) => nightmare ? night : god;

    public static float EnemyArmorBonus => Is(Saint.Warcury) ? Pick(10f, 15f) : 0f;
    /// <summary>적이 받는 마법 피해에 곱한다(마저항 +10% → 0.9).</summary>
    public static float EnemyMagicTakenFactor => Is(Saint.Warcury) ? 1f - Pick(0.10f, 0.15f) : 1f;
    public static float EnemyMoveSpeedFactor => Is(Saint.Nasjuro) ? 1f + Pick(0.10f, 0.15f) : 1f;
    public static float AllyAttackPowerPercent => Is(Saint.Saturn) ? -Pick(0.20f, 0.30f) : 0f;
    public static float AllyAttackSpeedFactor => Is(Saint.Nasjuro) ? 1f - Pick(0.10f, 0.15f) : 1f;
    public static float AllyExplosiveFactor => Is(Saint.Saturn) ? 1f - Pick(0.07f, 0.10f) : 1f;

    /// <summary>이 적에게 더해질 최대 체력(모든 적 난이도분 + 보스/라인몹 분).</summary>
    public static float EnemyHpBonus(bool isBoss, bool laneMob)
    {
        if (!Active) return 0f;
        float hp = Pick(5_000_000f, 10_000_000f);
        if (Chosen == Saint.Warcury && isBoss) hp += Pick(10_000_000f, 15_000_000f);
        if (Chosen == Saint.Nasjuro && laneMob) hp += Pick(10_000_000f, 15_000_000f);
        return hp;
    }

    public static float EnemyRegenBonus
    {
        get
        {
            if (!Active) return 0f;
            float regen = Pick(250_000f, 500_000f);
            if (Chosen == Saint.Saturn) regen += Pick(225_000f, 300_000f);
            return regen;
        }
    }

    // ---- 흐름 ----

    /// <summary>판 시작(호스트·싱글): 신·악몽이면 하나 뽑아 실루엣을 세운다.</summary>
    public static void OnMatchStart()
    {
        if (!GameAuthority.IsServer || DifficultyManager.Instance == null) return;
        DifficultyMode mode = DifficultyManager.Instance.Current;
        if (mode != DifficultyMode.God && mode != DifficultyMode.Nightmare) return;
        nightmare = mode == DifficultyMode.Nightmare;
        Chosen = (Saint)Random.Range(1, 4);
        silhouette = OroSeongSilhouette.Create(Chosen);
        Debug.Log($"[오로성] 이번 판: {Name(Chosen)} ({mode})");
    }

    // ---- 멀티 복제(NetGameState) ----
    /// <summary>호스트가 싣는 값: 0 없음 · 1 실루엣 대기 · 2 색 차오름(신세계 진입) · 3 퇴치됨.</summary>
    public static byte HostPhase => Chosen == Saint.None ? (byte)0 : Dismissed ? (byte)3 : fillStarted ? (byte)2 : (byte)1;
    public static bool HostNightmare => nightmare;

    /// <summary>클라(호스트 아님): 복제된 값으로 같은 실루엣을 세우고 색 차오름·퇴치를 따라간다. 효과(Active)는 호스트만 건다.</summary>
    public static void ClientSync(byte saint, byte phase, bool isNightmare)
    {
        if (saint == 0 || clientRetired) return;
        Chosen = (Saint)saint; nightmare = isNightmare; Dismissed = phase == 3;
        if (silhouette == null) silhouette = OroSeongSilhouette.Create(Chosen);
        if (phase == 2 && !fillStarted) { fillStarted = true; silhouette.StartFill(true); }   // true = 효과는 안 건다(호스트 몫)
        if (phase == 3) { silhouette.Vanish(); silhouette = null; clientRetired = true; }
    }

    /// <summary>탐침 전용(Assets/Editor/S2Probe): 난이도 선택 없이 이 오로성·난이도로 판을 세팅한다.</summary>
    public static void ForceForTest(Saint saint, bool isNightmare)
    {
        ResetMatch();
        nightmare = isNightmare;
        Chosen = saint;
        silhouette = OroSeongSilhouette.Create(saint);
    }

    /// <summary>RoundManager: 신세계 진입(R60 → 61). 니카가 이미 있으면 에메트가 막고, 아니면 효과를 건다.</summary>
    public static void OnNewWorldEnter()
    {
        if (!GameAuthority.IsServer || Chosen == Saint.None || Active || silhouette == null) return;
        fillStarted = true;
        silhouette.StartFill(Dismissed);
    }

    /// <summary>실루엣이 다 차오른 순간(OroSeongSilhouette가 부름): 대사·효과 적용.</summary>
    public static void ApplyNow()
    {
        if (Chosen == Saint.None || Active || Dismissed) return;
        Active = true;
        string line = Chosen == Saint.Warcury ? "악연으로 얽힌 자들이여, 그 녀석이 누구인지 알고 있는건가?"
            : Chosen == Saint.Saturn ? "우리 세계정부에 거역한 것을 후회하며 죽어주길 바란다."
            : "언젠가 그 힘을 자유자재로 쓰게 될 것이다.";
        string eff = Chosen == Saint.Warcury
            ? $"모든 적 유닛들의 방어력이 {Pick(10, 15):0} 증가합니다.\n모든 적 유닛들의 마법방어력이 {Pick(10, 15):0}% 증가합니다.\n보스 몬스터의 체력이 {Pick(1000, 1500):0}만 증가합니다."
            : Chosen == Saint.Saturn
            ? $"모든 아군 유닛들의 공격력이 {Pick(20, 30):0}% 감소합니다.\n모든 아군 유닛들의 폭발형 데미지가 {Pick(7, 10):0}% 감소합니다.\n모든 적 유닛들의 체력재생이 {Pick(22.5f, 30f):0.#}만 증가합니다."
            : $"모든 적 유닛들의 이동속도가 {Pick(10, 15):0}% 증가합니다.\n모든 아군 유닛들의 공격속도가 {Pick(10, 15):0}% 감소합니다.\n라인 몬스터의 체력이 {Pick(1000, 1500):0}만 증가합니다.";
        Announce($"<color=#C15AF4>{Name(Chosen)} :</color> <color=#DC143C>{line}</color>");
        Announce($"<color=#F15F5F>[시스템]</color> <color=#C15AF4>{Name(Chosen)} 성</color>의 효과가 적용됩니다.\n{eff}");
        Announce($"<color=#F15F5F>[시스템]</color> {(nightmare ? "<color=#CC3337>악몽</color>" : "<color=#FFD700>신</color>")} 추가 효과가 적용됩니다. 모든 적 유닛들의 체력이 {Pick(500, 1000):0}만, 체력재생이 {Pick(25, 50):0}만 증가합니다.");
        RefreshEnemies();
    }

    /// <summary>에메트: 니카를 얻었다. 아직 안 걸렸으면 막고, 걸렸으면 푼다.</summary>
    public static void Dismiss()
    {
        if (Dismissed || Chosen == Saint.None) return;
        Dismissed = true;
        bool wasActive = Active;
        Active = false;
        Announce("<color=#FFFA78>에메트 :</color> <color=#C15AF4>너를... 죽게 놔두지 않아!</color>");
        Announce("<color=#F15F5F>[시스템]</color> <color=#FFFA78>에메트</color>가 조이보이의 최대 패기를 방출하여 오로성을 퇴치합니다.\n<color=#DC143C>◈</color> 오로성 효과가 적용되지 않습니다.");
        if (silhouette != null) { silhouette.Vanish(); silhouette = null; }
        if (wasActive) RefreshEnemies();
    }

    /// <summary>UnitSpawner.Spawn이 부른다(호스트).</summary>
    public static void OnUnitSpawned(UnitData data)
    {
        if (data == null || Chosen == Saint.None || Dismissed || string.IsNullOrEmpty(data.unitName)) return;
        if (data.unitName.Contains("니카")) Dismiss();
    }

    static void RefreshEnemies()
    {
        foreach (EnemyDummy enemy in EnemyDummy.Active)
            if (enemy != null && !enemy.IsReplica) enemy.RefreshOroSeong();
    }

    static void Announce(string message)
    {
        foreach (PlayerContext c in PlayerContext.Occupied) S2Notify.Show(c.PlayerId, message, 10f);
    }

    /// <summary>점수판 한 줄(원작 「◎ 오로성 : 이름」). 없으면 null.</summary>
    public static string ScoreboardLine => Chosen == Saint.None ? null : $"◎ 오로성 : {Name(Chosen)}{(Dismissed ? " (퇴치)" : "")}";
}

/// <summary>맵 정중앙 검은 실루엣 + 머리 위 글자. 신세계 때 색이 차오른다(0.02초마다 1%·10단계 = 원작은 0.1초 10단계, 여기선 1초).</summary>
public class OroSeongSilhouette : MonoBehaviour
{
    TextMesh label;
    S2Silhouette sil;
    bool filling, vanishing, applyAtEnd;
    float t;

    public static OroSeongSilhouette Create(OroSeong.Saint saint)
    {
        Vector3 center = Vector3.zero; int n = 0;
        foreach (PlayerContext c in PlayerContext.All)
        {
            LaneMarker lane = c != null ? LaneMarker.Get(c.PlayerId) : null;
            if (lane != null) { center += lane.LaneCenter; n++; }
        }
        if (n > 0) center /= n;

        GameObject prefab = Resources.Load<GameObject>("S2/Saint_" + saint);
        GameObject go = prefab != null ? Object.Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "오로성 실루엣 " + OroSeong.Name(saint);
        go.transform.position = center;
        if (prefab == null) go.transform.localScale = new Vector3(30f, 60f, 30f);
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
        foreach (Animator a in go.GetComponentsInChildren<Animator>(true)) a.enabled = true;

        var s = go.AddComponent<OroSeongSilhouette>();
        s.sil = new S2Silhouette(go.GetComponentsInChildren<Renderer>(true));
        s.Paint(0f);

        var textGo = new GameObject("오로성 글자");
        textGo.transform.SetParent(go.transform, false);
        textGo.transform.localPosition = new Vector3(0f, prefab == null ? 1.6f : 90f, 0f);
        textGo.transform.localScale = prefab == null ? new Vector3(1f / 30f, 1f / 60f, 1f / 30f) : Vector3.one;
        s.label = textGo.AddComponent<TextMesh>();
        s.label.text = "오로성은 신세계 이후에 등장합니다.";
        s.label.anchor = TextAnchor.MiddleCenter;
        s.label.characterSize = 4f;
        s.label.fontSize = 48;
        s.label.color = new Color32(139, 189, 255, 255);
        return s;
    }

    void Paint(float v) => sil.Paint(v);

    public void StartFill(bool dismissedAlready)
    {
        if (label != null) label.text = "";
        applyAtEnd = !dismissedAlready;
        filling = true;
        t = 0f;
    }

    public void Vanish() { vanishing = true; t = 0f; if (label != null) label.text = ""; }

    void LateUpdate()
    {
        if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
    }

    void Update()
    {
        if (filling)
        {
            t += Time.deltaTime;
            Paint(Mathf.Clamp01(t / 1f));
            if (t >= 1f)
            {
                filling = false;
                sil.Restore();   // 원래 색으로(원작: 색이 다 차오른다)
                if (applyAtEnd) OroSeong.ApplyNow();
            }
        }
        else if (vanishing)
        {
            t += Time.deltaTime;
            if (t >= 4f) Destroy(gameObject);
        }
    }
}
