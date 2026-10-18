using UnityEngine;

/// <summary>
/// 시작 흐름(인트로·첫 화면·대기실·설정 창·문 전환)의 테마 스위치. 아래 <see cref="Current"/> 한 줄로 고른다.
/// 옛 테마는 지우지 않는다(사장님 10-06 「되돌아올 수 있게」) — Tavern = Resources/UI/{Lobby,Door,Settings}, Castle = Resources/UI/{LobbyCastle,DoorCastle,SettingsCastle}.
/// Castle 그림이 아직 없으면(개발 중) 같은 이름의 Tavern 그림으로 물러난다. Tavern 값은 스위치가 생기기 전의 코드 값 그대로다.
/// </summary>
public static class MenuTheme
{
    public enum Kind { Tavern, Castle }

    public const Kind Current = Kind.Castle;

    static bool Castle => Current == Kind.Castle;
    static T Pick<T>(T tavern, T castle) => Castle ? castle : tavern;

    /// <summary>문 전환 방식(DoorTransition 머리 주석). Classic = 선술집 쌍여닫이(종전 그대로).</summary>
    public enum DoorKind { Classic, Doors, Portcullis }

    /// <summary>Castle일 때 쓸 문 방식 — 한 줄로 고른다: Doors(쇠문 쌍여닫이 다듬은 판) / Portcullis(성문 쇠창살).</summary>
    public const DoorKind CastleDoorStyle = DoorKind.Doors;

    /// <summary>촬영 전용 덮어쓰기(에디터 DoorPreview). 음수면 안 쓴다.</summary>
    public static int DoorStyleOverride = -1;
    public static DoorKind DoorStyle => DoorStyleOverride >= 0 ? (DoorKind)DoorStyleOverride : (Castle ? CastleDoorStyle : DoorKind.Classic);

    public static readonly string LobbyDir = Pick("UI/Lobby/", "UI/LobbyCastle/");
    public static readonly string DoorDir = Pick("UI/Door/", "UI/DoorCastle/");
    public static readonly string SettingsDir = Pick("UI/Settings/", "UI/SettingsCastle/");

    const string TavernLobby = "UI/Lobby/", TavernDoor = "UI/Door/", TavernSettings = "UI/Settings/";

    public static Sprite LobbySprite(string name) => LoadSprite(LobbyDir, TavernLobby, name);
    public static Sprite SettingsSprite(string name) => LoadSprite(SettingsDir, TavernSettings, name);
    public static Texture2D DoorTexture(string name) => LoadTexture(DoorDir, TavernDoor, name);

    /// <summary>첫 화면 배경 사진. Castle 그림이 있으면 그것(영상은 안 쓴다), 없으면 Tavern 사진(+영상). usedCastle로 어느 쪽인지 알려 준다.</summary>
    public static Texture2D LobbyBackground(string name, out bool usedCastle)
    {
        usedCastle = false;
        if (Castle)
        {
            Texture2D t = Resources.Load<Texture2D>(LobbyDir + name);
            if (t != null) { usedCastle = true; return t; }
        }
        return Resources.Load<Texture2D>(TavernLobby + name);
    }

    static Sprite LoadSprite(string dir, string fallbackDir, string name)
    {
        Sprite s = Resources.Load<Sprite>(dir + name);
        return s != null || dir == fallbackDir ? s : Resources.Load<Sprite>(fallbackDir + name);
    }

    static Texture2D LoadTexture(string dir, string fallbackDir, string name)
    {
        Texture2D t = Resources.Load<Texture2D>(dir + name);
        return t != null || dir == fallbackDir ? t : Resources.Load<Texture2D>(fallbackDir + name);
    }

    /// <summary>판 들어가기 전 로딩 화면 그림(Castle은 LoadingScreenCastle.jpg, 없으면 옛 사진).</summary>
    public static Texture2D LoadingTexture()
    {
        if (Castle)
        {
            Texture2D t = Resources.Load<Texture2D>("UI/LoadingScreenCastle");
            if (t != null) return t;
        }
        return Resources.Load<Texture2D>("UI/LoadingScreen");
    }

    // ───────────── 색 ─────────────
    // Tavern = 갈색 가죽·금빛. Castle = 푸른기 도는 회색 돌·쇠, 글자는 은/회색 + 어두운 쇠 테두리.
    public static readonly Color Card = Pick(new Color(0.14f, 0.10f, 0.07f, 0.97f), new Color(0.12f, 0.13f, 0.15f, 0.97f));       // 그림이 없을 때 물러나는 색
    public static readonly Color Row = Pick(new Color(0.10f, 0.07f, 0.05f, 0.88f), new Color(0.08f, 0.09f, 0.11f, 0.88f));
    public static readonly Color RowMe = Pick(new Color(0.34f, 0.22f, 0.08f, 0.95f), new Color(0.24f, 0.28f, 0.34f, 0.95f));
    public static readonly Color Highlight = Pick(new Color(0.42f, 0.30f, 0.12f, 0.95f), new Color(0.30f, 0.35f, 0.43f, 0.95f));
    public static readonly Color ButtonAccent = Pick(Color.white, new Color(1f, 1f, 1f, 0.99f));   // Tavern은 으뜸 표식이 없다(ButtonNormal과 같음), Castle은 글자색으로 구분
    public static readonly Color ButtonDanger = Pick(new Color(1f, 0.70f, 0.66f, 1f), new Color(1f, 0.70f, 0.66f, 1f));
    public static readonly Color Selected = Pick(new Color(1f, 0.84f, 0.25f, 1f), new Color(0.78f, 0.88f, 1f, 1f));                   // 방장 표시·캐럿
    public static readonly Color Gold = Pick(new Color(0.79f, 0.64f, 0.29f, 1f), new Color(0.62f, 0.67f, 0.74f, 1f));                // 슬라이더 채움 등 포인트색
    public static readonly Color ButtonText = Pick(new Color(0.96f, 0.86f, 0.58f, 1f), new Color(0.86f, 0.89f, 0.93f, 1f));
    public static readonly Color ButtonTextAccent = Pick(new Color(1f, 0.92f, 0.62f, 1f), new Color(0.97f, 0.98f, 1f, 1f));
    public static readonly Color TextMain = Pick(new Color(0.96f, 0.93f, 0.84f, 1f), new Color(0.93f, 0.94f, 0.96f, 1f));
    public static readonly Color TextDim = Pick(new Color(0.80f, 0.72f, 0.54f, 1f), new Color(0.68f, 0.72f, 0.78f, 1f));
    public static readonly Color ReadyGreen = new Color(0.52f, 0.88f, 0.50f, 1f);
    public static readonly Color ButtonFallback = Pick(new Color(0.20f, 0.18f, 0.16f, 1f), new Color(0.20f, 0.21f, 0.23f, 1f));
    public static readonly Color SettingsCard = Pick(new Color(0.16f, 0.11f, 0.07f, 0.98f), new Color(0.13f, 0.14f, 0.16f, 0.98f));
    public static readonly Color NameplateFallback = Pick(new Color(0.3f, 0.2f, 0.1f, 1f), new Color(0.26f, 0.28f, 0.32f, 1f));
    public static readonly Color TrackFallback = Pick(new Color(0.08f, 0.06f, 0.04f, 0.95f), new Color(0.06f, 0.07f, 0.09f, 0.95f));
    public static readonly Color InputBackground = Pick(new Color(0.05f, 0.035f, 0.025f, 0.96f), new Color(0.05f, 0.055f, 0.07f, 0.96f));
    public static readonly Color InputBorder = Pick(new Color(0.62f, 0.48f, 0.20f, 1f), new Color(0.46f, 0.50f, 0.57f, 1f));
    public static readonly Color InputSelection = Pick(new Color(0.79f, 0.64f, 0.29f, 0.35f), new Color(0.62f, 0.72f, 0.90f, 0.35f));

    // 글자 테두리(어두운 쇠 / 어두운 갈색)
    public static readonly Color32 SubtitleOutline = Pick(new Color32(40, 22, 6, 255), new Color32(14, 16, 20, 255));
    public static readonly Color32 HeadOutline = Pick(new Color32(30, 16, 4, 255), new Color32(10, 12, 16, 255));
    public static readonly Color32 TitleOutline = Pick(new Color32(48, 26, 6, 255), new Color32(16, 18, 22, 255));

    // 제목 「G.R.D」 위·아래 그라데이션 / 부제 그라데이션
    public static readonly Color TitleTop = Pick(new Color(1f, 0.93f, 0.62f, 1f), new Color(0.97f, 0.98f, 1f, 1f));
    public static readonly Color TitleBottom = Pick(new Color(0.90f, 0.62f, 0.18f, 1f), new Color(0.58f, 0.63f, 0.71f, 1f));
    public static readonly Color SubTop = Pick(new Color(0.94f, 0.88f, 0.70f, 1f), new Color(0.90f, 0.93f, 0.97f, 1f));
    public static readonly Color SubBottom = Pick(new Color(0.78f, 0.67f, 0.43f, 1f), new Color(0.60f, 0.65f, 0.73f, 1f));

    // 로딩 화면 진행 막대(Tavern = 금색, Castle = 차가운 은청색)
    public static readonly Color LoadingBar = Pick(new Color(0.95f, 0.78f, 0.25f), new Color(0.66f, 0.76f, 0.92f));

    // 문 틈 사이로 비치는 빛(Tavern = 따뜻한 주황, Castle = 차가운 횃불/달빛)
    public static readonly Color DoorGapLight = Pick(new Color(1f, 0.85f, 0.6f, 1f), new Color(0.72f, 0.82f, 1f, 1f));
}
