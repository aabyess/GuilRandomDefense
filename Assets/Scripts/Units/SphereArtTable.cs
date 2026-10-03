using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원작 상시 오라(Sphere 능력의 대상 아트 — 유닛에 늘 붙어 있다)의 표와 모양 공장.
///
/// 표(Resources/Effects/SphereArtTable.txt, Tools/sphere_art/build_table.py가 Docs/research/SPHERE_ART_BY_ROSTER.csv에서 만든다):
///   로스터 이름 ⇥ 아트 키 ⇥ 붙는 곳(origin·chest·weapon·hand,left·hand,right·foot,right·sprite) 한 줄에 하나.
/// 등급 규칙(초월 → HandsAura2, 히든 → BlightwalkerAura)은 표가 아니라 <see cref="GradeArts"/>가 준다 — 표에는 등급 규칙에 안 걸리는
/// 캐릭터 오라(레일리·샹크스·흰수염 해적단 …)와, 접두어가 초월·히든이 아닌데 구체 오라를 가진 둘(영원·제한 하나씩)만 들어 있다.
///
/// 모양: Resources/Effects/Sphere/&lt;아트 키&gt;.prefab(Blender가 원작 MDX에서 뽑은 모델)이 있으면 그걸 쓰고, 없으면 임시 모양(코드로 그린 판)을 쓴다.
/// 아트 키 = 원작 파일 이름에서 폴더·확장자를 떼고 소문자로(예: HandsAura2.mdx → handsaura2).
/// </summary>
public static class SphereArtTable
{
    /// <summary>보이는 때 — 항상 / 공격 모션 중(Animator 상태 Attack) / 스킬 중(UnitSphereArt.PulseSkill이 켠 동안).</summary>
    /// <summary>보이는 때 — 비트 마스크(대기·이동·공격 중 하나 + 스킬 신호). 표 열: 「항상」 또는 「이동|공격|스킬」처럼 | 로 이은 것.</summary>
    [System.Flags] public enum When { Idle = 1, Move = 2, Attack = 4, Skill = 8, Always = 15 }

    public struct Art
    {
        public string key;      // 아트 키(소문자, 확장자 없음) — Resources/Effects/Sphere/<키>.prefab, 없으면 임시 모양
        public string attach;   // origin / chest / weapon / hand,left / hand,right / foot,right / sprite  또는 뼈: HumanBodyBones 이름(RightHand) · 뼈 이름 조각(Bip001 R Hand)
        public Vector3 pos;     // 붙는 곳 기준 로컬 위치(뼈면 그 뼈 로컬, origin이면 땅 기준 세계 단위)
        public Vector3 euler;   // 로컬 회전(도) — origin은 무시(늘 세계 정렬)
        public float scale;     // 크기 배수(0이면 1)
        public When when;   // 🔴 struct라 기본값 0 — new Art로 만들 땐 when = When.Always를 꼭 적을 것(옛 enum은 0 = Always였다)
    }

    const string TablePath = "Effects/SphereArtTable";
    static Dictionary<string, List<Art>> byRoster;

    static readonly Art[] TranscendentArts = { new Art { key = "handsaura2", attach = "origin", scale = 1.5f, when = When.Always } };
    static readonly Art[] HiddenArts = { new Art { key = "blightwalkeraura", attach = "origin", scale = 1f, when = When.Always } };

    /// <summary>등급만으로 붙는 오라(초월 = HandsAura2 발밑, 히든 = BlightwalkerAura 발밑). 없으면 빈 배열.</summary>
    public static Art[] GradeArts(UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Transcendent: return TranscendentArts;
            case UnitGrade.Hidden: return HiddenArts;
            default: return System.Array.Empty<Art>();
        }
    }

    /// <summary>이 로스터가 가진 오라 전부(등급 규칙 + 표). 같은 (아트, 자리)는 한 번만.</summary>
    public static List<Art> For(UnitData data)
    {
        var result = new List<Art>();
        if (data == null) return result;
        result.AddRange(GradeArts(data.grade));
        Load();
        if (byRoster.TryGetValue(Key(data.name), out List<Art> rows))
            foreach (Art a in rows)
                if (!result.Exists(r => r.key == a.key && r.attach == a.attach && r.pos == a.pos && r.when == a.when)) result.Add(a);
        return result;
    }

    // 열: 로스터 ⇥ 키 ⇥ 붙는 곳 ⇥ [위치 x,y,z] ⇥ [회전 x,y,z] ⇥ [크기] ⇥ [항상|공격|스킬] — 뒤쪽 열은 비워도 된다.
    static Art ParseRow(string[] f)
    {
        var a = new Art { key = f[1].Trim(), attach = f[2].Trim(), scale = 1f, when = When.Always };
        if (f.Length > 3) a.pos = ParseVec(f[3]);
        if (f.Length > 4) a.euler = ParseVec(f[4]);
        if (f.Length > 5 && float.TryParse(f[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sc) && sc > 0f) a.scale = sc;
        if (f.Length > 6) a.when = ParseWhen(f[6]);
        return a;
    }

    static When ParseWhen(string s)
    {
        When w = 0;
        foreach (string t in s.Split('|', ','))
            switch (t.Trim())
            {
                case "대기": w |= When.Idle; break;
                case "이동": w |= When.Move; break;
                case "공격": w |= When.Attack; break;
                case "스킬": w |= When.Skill; break;
                case "항상": w |= When.Always; break;
            }
        return w == 0 ? When.Always : w;
    }

    static Vector3 ParseVec(string s)
    {
        string[] p = s.Split(',');
        float F(int i) => i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        return new Vector3(F(0), F(1), F(2));
    }

    static string Key(string rosterName) => rosterName.Normalize(System.Text.NormalizationForm.FormC);

    static void Load()
    {
        if (byRoster != null) return;
        byRoster = new Dictionary<string, List<Art>>();
        TextAsset text = Resources.Load<TextAsset>(TablePath);
        if (text == null) { Debug.LogWarning($"[오라] Resources/{TablePath} 를 못 찾았습니다 — 등급 오라만 붙습니다."); return; }
        foreach (string raw in text.text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            string roster = Key(f[0]);
            if (!byRoster.TryGetValue(roster, out List<Art> list)) byRoster[roster] = list = new List<Art>();
            list.Add(ParseRow(f));
        }
    }

    // ── 모양 ──────────────────────────────────────────────────────────────
    // 원작 단위(맵 단위) 크기 — Blender 설명표(원작_오라날개/설명표.md): HandsAura2 169, BlightwalkerAura 199. 그 밖의 발밑 오라는 알 수 없어 150(임시).
    // 세계 단위로는 ÷ WorldScale.Value(4.167).
    public static float OriginDiameterMapUnits(string key)
    {
        switch (key)
        {
            case "handsaura2": return 169f;
            case "blightwalkeraura": return 199f;
            default: return 150f;
        }
    }

    /// <summary>Blender가 뽑은 진짜 모델 프리팹이 있나(없으면 임시 모양).</summary>
    public static bool HasRealModel(string key)
    {
        if (!prefabs.TryGetValue(key, out GameObject prefab)) { prefab = Resources.Load<GameObject>("Effects/Sphere/" + key); prefabs[key] = prefab; }
        return prefab != null;
    }

    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static Mesh flatQuad;

    /// <summary>그 아트의 모양 한 벌(지름 1짜리 — 호출한 쪽이 크기를 준다). 발밑이면 땅에 눕는다.</summary>
    public static GameObject Create(string key, bool flat)
    {
        if (!prefabs.TryGetValue(key, out GameObject prefab))
        {
            prefab = Resources.Load<GameObject>("Effects/Sphere/" + key);
            prefabs[key] = prefab;
        }
        if (prefab != null)
        {
            GameObject real = Object.Instantiate(prefab);
            real.name = "오라_" + key;
            return real;
        }
        return CreatePlaceholder(key, flat);
    }

    static GameObject CreatePlaceholder(string key, bool flat)
    {
        GameObject go = new GameObject("오라_" + key + "(임시)");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = Quad();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MaterialFor(key);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (flat) go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 쿼드 앞면(+Z 반대)이 위를 보게 눕힌다
        else go.AddComponent<SphereArtBillboard>();
        return go;
    }

    static Mesh Quad()
    {
        if (flatQuad != null) return flatQuad;
        flatQuad = new Mesh { name = "SphereArtQuad" };
        flatQuad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
        flatQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        flatQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        flatQuad.RecalculateBounds();
        return flatQuad;
    }

    static Material MaterialFor(string key)
    {
        if (materials.TryGetValue(key, out Material m)) return m;
        // Sprites/Default는 빌드에 늘 들어 있다(투명 합성, 양면). 한 아트당 한 장을 모두가 나눈다.
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        m = new Material(shader) { name = "SphereArt_" + key, mainTexture = Texture(key) };
        m.renderQueue = 3000;
        materials[key] = m;
        return m;
    }

    // ── 임시 그림(원작 모양을 흉내 낸 것 — Blender 모델이 오면 Resources/Effects/Sphere/ 프리팹이 이 자리를 대신한다) ──
    const int Size = 256;

    static Texture2D Texture(string key)
    {
        var px = new Color[Size * Size];
        switch (key)
        {
            case "handsaura2": DrawHandsAura(px); break;
            case "blightwalkeraura": DrawBlightwalker(px); break;
            default: DrawGlow(px, TintFor(key)); break;
        }
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "SphereArt_" + key, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    static Color TintFor(string key)
    {
        int h = 17; foreach (char c in key) h = h * 31 + c;
        Color c0 = Color.HSVToRGB(((h & 0xFFFF) % 360) / 360f, 0.55f, 1f);
        return c0;
    }

    static void Blend(Color[] px, int x, int y, Color c, float a)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size || a <= 0f) return;
        Color o = px[y * Size + x];
        float na = Mathf.Clamp01(o.a + a * (1f - o.a));
        float w = na > 0f ? a / na : 0f;
        px[y * Size + x] = new Color(Mathf.Lerp(o.r, c.r, w), Mathf.Lerp(o.g, c.g, w), Mathf.Lerp(o.b, c.b, w), na);
    }

    static void DrawGlow(Color[] px, Color tint)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(Size / 2f, Size / 2f)) / (Size / 2f);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.7f) / 0.12f, 2f)) * 0.8f + Mathf.Exp(-d * d * 6f) * 0.5f;
                if (d < 1f) Blend(px, x, y, tint, ring * (1f - d * d));
            }
    }

    // 발밑에 깔린 전기 호 여섯 갈래(빨간 지그재그) + 번개 사이 보라·노랑 둥근 빛 덩어리 + 가운데 파란 빛(설명표 ①).
    static void DrawHandsAura(Color[] px)
    {
        var rng = new System.Random(2);
        float half = Size / 2f;
        DrawBlob(px, half, half, 0.20f * half, new Color(0.35f, 0.45f, 1f), 0.55f);
        for (int arm = 0; arm < 6; arm++)
        {
            float baseAngle = arm * Mathf.PI / 3f;
            Vector2 prev = Vector2.zero; bool first = true;
            const int Segs = 9;
            for (int s = 0; s <= Segs; s++)
            {
                float r = Mathf.Lerp(0.12f, 0.46f, s / (float)Segs);
                float ang = baseAngle + ((float)rng.NextDouble() - 0.5f) * 0.32f * (s == 0 || s == Segs ? 0.2f : 1f);
                Vector2 p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * Size;
                if (!first) DrawLine(px, half + prev.x, half + prev.y, half + p.x, half + p.y, 2.2f, new Color(1f, 0.18f, 0.12f), 0.95f);
                prev = p; first = false;
            }
            float ba = baseAngle + Mathf.PI / 6f;   // 갈래 사이 빛 덩어리
            Vector2 bp = new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * 0.30f * Size;
            DrawBlob(px, half + bp.x, half + bp.y, 0.075f * Size, arm % 2 == 0 ? new Color(0.7f, 0.3f, 1f) : new Color(1f, 0.9f, 0.3f), 0.8f);
        }
    }

    // 바닥에 납작한 꽃 모양 고리 — 타원 꽃잎 8장이 겹친 판, 테두리에 작은 룬 점(설명표 ①).
    static void DrawBlightwalker(Color[] px)
    {
        float half = Size / 2f;
        Color edge = new Color(0.45f, 1f, 0.5f), fill = new Color(0.2f, 0.7f, 0.35f);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 v = new Vector2(x - half, y - half) / half;
                float r = v.magnitude, th = Mathf.Atan2(v.y, v.x);
                float outline = 0.62f + 0.34f * Mathf.Abs(Mathf.Cos(4f * th));   // 꽃잎 8장
                float inner = outline - 0.10f;
                if (r < outline) Blend(px, x, y, fill, 0.22f);
                float e = Mathf.Exp(-Mathf.Pow((r - outline) / 0.025f, 2f));
                if (e > 0.02f) Blend(px, x, y, edge, e * 0.9f);
                float rune = Mathf.Exp(-Mathf.Pow((r - inner) / 0.03f, 2f)) * (Mathf.Cos(16f * th) > 0.55f ? 1f : 0f);
                if (rune > 0.02f) Blend(px, x, y, new Color(0.8f, 1f, 0.6f), rune * 0.8f);
            }
    }

    static void DrawBlob(Color[] px, float cx, float cy, float rad, Color c, float a)
    {
        int r = Mathf.CeilToInt(rad * 1.8f);
        for (int y = (int)cy - r; y <= (int)cy + r; y++)
            for (int x = (int)cx - r; x <= (int)cx + r; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / rad;
                Blend(px, x, y, c, Mathf.Exp(-d * d * 1.6f) * a);
            }
    }

    static void DrawLine(Color[] px, float x0, float y0, float x1, float y1, float width, Color c, float a)
    {
        int minX = (int)Mathf.Min(x0, x1) - 6, maxX = (int)Mathf.Max(x0, x1) + 6, minY = (int)Mathf.Min(y0, y1) - 6, maxY = (int)Mathf.Max(y0, y1) + 6;
        Vector2 p0 = new Vector2(x0, y0), d = new Vector2(x1, y1) - p0;
        float len2 = Mathf.Max(d.sqrMagnitude, 1e-4f);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, y) - p0, d) / len2);
                float dist = Vector2.Distance(new Vector2(x, y), p0 + d * t);
                float core = Mathf.Exp(-Mathf.Pow(dist / width, 2f));
                Blend(px, x, y, c, core * a);
            }
    }
}

/// <summary>화면을 보는 판(손·가슴 임시 오라) — 카메라 쪽으로 돌린다.</summary>
public class SphereArtBillboard : MonoBehaviour
{
    Camera cam;
    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
