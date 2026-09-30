using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 조합판 등급 머리글(사장님 2026-09-30) — 각 등급이 시작하는 열 맨 위 바닥에 「안흔함 · 특별함 · 희귀함 · 전설적인 · 제한됨 · 히든」을
/// 등급색 글씨로 적는다. 흔함 열(1열)은 뺀다(사장님 「1열 흔함 이건 스킵」).
///
/// 씬에는 없다 — 판이 열리면 스스로 선다(ShopNameplateLayer와 같은 설치 방식). 맵 생성기를 다시 돌리지 않아도 되게,
/// 생성기가 이미 깔아 둔 등급별 바닥판(<c>조합표_{등급}</c>, MapGenerator.BuildCombineColumns)을 이름으로 찾아 자리를 **잰다**:
/// 가로는 그 등급 바닥판 전부(특별함·희귀함·전설적인은 두 열)의 한가운데, 세로는 바닥판 위쪽 끝 바로 위.
/// 열 배정이 바뀌어도(09-24 하루에 네 번 바뀌었다) 바닥판을 따라간다. 한 열 안에서 시작하는 등급(전설 밑 제한됨)은
/// 등급 구분벽과 바닥판 사이 틈에 적힌다.
/// 글씨는 어두운 판을 댄 세계 공간 TextMeshPro 팻말이다 — 화면에 붙는 이름표와 달리 줌·이동을 따라 조합판에 서 있는 것으로 보인다.
/// </summary>
public class CombineBoardHeaders : MonoBehaviour
{
    // 머리글을 다는 등급. 흔함은 조합식이 없는 전시 열이라 뺀다.
    static readonly UnitGrade[] Grades =
    {
        UnitGrade.Uncommon, UnitGrade.Special, UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.Limited, UnitGrade.Hidden,
    };

    const string StripPrefix = "조합표_";
    const string AnchorStrip = "조합표_흔함";   // 바닥판들의 부모를 찾는 손잡이
    const float TextHeightInRows = 0.75f;       // 글자 높이 = 흔함 열 폭(결과 칸 하나 + 여백)의 몇 배
    const float MarginInRows = 0.10f;           // 바닥판 위쪽 끝에서 글씨 아래까지 틈
    const float Lift = 0.35f;                   // 바닥판 윗면에서 띄우는 높이(깊이 겹침 방지)
    const float Tilt = 40f;                     // 수직에서 뒤로 눕힌 각도 — 기본 카메라가 비스듬히 내려다보므로 그 시선에 맞선다
    const float PlateHeight = 1.5f;             // 판 높이 = 글자 높이의 몇 배

    static Sprite whitePixel;

    static Sprite WhitePixel()
    {
        if (whitePixel != null) return whitePixel;
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "머리글_판" };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whitePixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whitePixel;
    }

    // 🔴 배포판은 첫 씬이 NetBoot — AfterSceneLoad만으론 게임 씬에서 안 돈다(IslandShores와 같은 이유, 0.3.0 빌드에서 잡음).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Install();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        GameObject anchor = GameObject.Find(AnchorStrip);
        if (anchor == null || anchor.transform.parent == null) return;   // 조합판이 없는 씬(메뉴 등)

        Transform board = anchor.transform.parent;
        if (board.Find("조합표_머리글") != null) return;

        // 한 줄 깊이 = 흔함 바닥판 깊이 ÷ 그 위에 선 인형 수가 정확하지만, 인형은 실행 때 세워져 아직 없을 수 있다.
        // 바닥판 폭(결과 칸 하나 + 여백)이 줄 깊이와 같은 자에서 나오므로 그 폭을 자로 쓴다.
        float row = Mathf.Max(1f, anchor.transform.lossyScale.x);

        GameObject root = new GameObject("조합표_머리글");
        root.transform.SetParent(board, false);
        root.AddComponent<CombineBoardHeaders>();

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF");
        int made = 0;
        foreach (UnitGrade grade in Grades)
        {
            string stripName = StripPrefix + grade.KoreanName();
            float left = float.MaxValue, right = float.MinValue, top = float.MinValue, y = 0f;
            foreach (Transform child in board)
            {
                if (child.name != stripName) continue;
                Vector3 p = child.position, s = child.lossyScale;
                left = Mathf.Min(left, p.x - s.x * 0.5f);
                right = Mathf.Max(right, p.x + s.x * 0.5f);
                top = Mathf.Max(top, p.z + s.z * 0.5f);
                y = p.y + s.y * 0.5f;
            }
            if (right < left) continue;   // 그 등급 바닥판이 없다(배정에서 빠짐)

            float height = row * TextHeightInRows;
            // 팻말처럼 비스듬히 세운다 — 바닥에 눕히면 기본 카메라 각도에서 글자가 납작해져 안 읽힌다(09-30 첫 시도 실측).
            Quaternion tilt = Quaternion.Euler(Tilt, 0f, 0f);
            Vector3 foot = new Vector3((left + right) * 0.5f, y + Lift, top + row * MarginInRows);
            Vector3 center = foot + tilt * Vector3.up * (height * PlateHeight * 0.5f);

            GameObject go = new GameObject("머리글_" + grade.KoreanName());
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(center, tilt);

            TextMeshPro text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.text = grade.KoreanName();
            text.color = Color.Lerp(grade.Color(), Color.white, 0.25f);   // 어두운 판 위에서 등급색이 또렷하게
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontSize = height * 10f;   // 세계 공간 TMP: 글자 크기 10 ≈ 높이 1
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(height * 8f, height * PlateHeight);
            text.sortingOrder = 1;
            text.ForceMeshUpdate();

            // 글자 뒤 어두운 판 — 잔디·바다 위에서도 읽히게. Sprites/Default는 빌드에 늘 들어 있다(Shader.Find로 URP 셰이더를 찾으면 빌드에서 빠질 수 있다).
            GameObject plate = new GameObject("판");
            plate.transform.SetParent(go.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.05f);   // 글자 바로 뒤
            float plateWidth = Mathf.Max(text.preferredWidth, height) + height * 0.7f;
            plate.transform.localScale = new Vector3(plateWidth, height * PlateHeight, 1f);
            SpriteRenderer back = plate.AddComponent<SpriteRenderer>();
            back.sprite = WhitePixel();
            back.color = new Color(0.05f, 0.05f, 0.07f, 0.78f);
            back.sortingOrder = 0;
            made++;
        }
        Debug.Log($"[조합판] 등급 머리글 {made}개(줄 깊이 자 {row:0.0})");
    }
}
