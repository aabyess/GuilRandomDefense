using System;
using System.Collections.Generic;
using System.Text;

// 원작식 세이브 코드(VJSE_Save/VJSE_Load의 우리 버전). 코드 생성·검증은 이 클래스 한 곳에만 둔다 — 나중에 서버 DB로 바뀌면 이 자리만 갈아 끼운다.
//
// 원작: 열쇠 = 플레이어 닉네임(DropSharpFromString(udg_ChinghoName2) — 배틀태그 '#' 뒷부분 제거). 닉네임이 다르면 로드 실패(「로드 실패!」).
// 원작 인코더 자체는 채팅 길이 제약용이라 옮기지 않았다. 우리 코드: 「GRD1-XXXX-XXXX-…」 — Crockford Base32(0-9 A-Z에서 I·L·O·U 뺌, 대소문자·0/O·1/I/L 혼동 보정).
//   본문 = 버전 1바이트 + 닉네임 해시 2바이트 + 네 값(누적점수·클리어횟수·베스트·레벨)을 가변길이 정수로 + 검사합 3바이트(본문 + 비밀 소금).
//   검사합이 틀리면 「코드 손상」, 검사합은 맞는데 닉네임 해시가 다르면 「닉네임 불일치」 — 두 실패를 갈라 안내하려는 설계다.
// ⚠️ 보안이 아니다(원작도 아니었다): 소금은 코드를 손으로 고치기 번거롭게 할 뿐, 베타 친구들 사이 이어하기가 목적이다.
public static class SaveCode
{
    public const string Prefix = "GRD1";
    const byte Version = 1;
    const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";   // Crockford Base32
    const ulong Salt = 0x6B2F3A9C51D7E480UL;

    public enum Failure { None, Malformed, Corrupted, NicknameMismatch, UnknownVersion }

    /// <summary>원작의 열쇠 — 앞뒤 공백 제거, 배틀태그식 '#...' 꼬리 제거(DropSharpFromString). 대소문자는 구분한다(원작 Key2ParityKey가 CaseHash를 섞는다).</summary>
    public static string NormalizeKey(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return "";
        string n = nickname.Trim();
        int sharp = n.IndexOf('#');
        return sharp >= 0 ? n.Substring(0, sharp) : n;
    }

    public static string Encode(string nickname, PlayerSaveData data)
    {
        var body = new List<byte> { Version };
        ushort nick = NickHash(NormalizeKey(nickname));
        body.Add((byte)(nick >> 8));
        body.Add((byte)nick);
        WriteVarint(body, Math.Max(0, data.cumulativePlayPoint));
        WriteVarint(body, Math.Max(0, data.cumulativeClearCount));
        WriteVarint(body, Math.Max(0, data.bestRunPoint));
        WriteVarint(body, Math.Max(0, data.playerLevel));
        ulong sum = Checksum(body);
        body.Add((byte)(sum >> 16));
        body.Add((byte)(sum >> 8));
        body.Add((byte)sum);
        return Prefix + "-" + Group(ToBase32(body.ToArray()));
    }

    /// <summary>코드를 풀어 닉네임 열쇠를 확인한다. 실패면 data는 null.</summary>
    public static bool TryDecode(string nickname, string code, out PlayerSaveData data, out Failure failure, out string message)
    {
        data = null;
        failure = Failure.Malformed;
        message = "세이브 코드 형식이 아닙니다.";
        if (string.IsNullOrWhiteSpace(code)) { message = "세이브 코드를 입력하세요."; return false; }

        string s = code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
        if (!s.StartsWith(Prefix)) return false;
        s = s.Substring(Prefix.Length);

        byte[] bytes = FromBase32(s);
        if (bytes == null || bytes.Length < 1 + 2 + 4 + 3) return false;   // 버전·닉해시·최소 네 값(1바이트씩)·검사합

        int bodyLen = bytes.Length - 3;
        var body = new List<byte>(bodyLen);
        for (int i = 0; i < bodyLen; i++) body.Add(bytes[i]);
        ulong want = Checksum(body);
        ulong got = ((ulong)bytes[bodyLen] << 16) | ((ulong)bytes[bodyLen + 1] << 8) | bytes[bodyLen + 2];
        if (want != got)
        {
            failure = Failure.Corrupted;
            message = "코드가 손상되었습니다(글자 하나가 틀렸거나 잘렸습니다).";
            return false;
        }
        if (body[0] != Version)
        {
            failure = Failure.UnknownVersion;
            message = "이 버전에서 읽을 수 없는 세이브 코드입니다.";
            return false;
        }

        ushort codeNick = (ushort)((body[1] << 8) | body[2]);
        if (codeNick != NickHash(NormalizeKey(nickname)))
        {
            failure = Failure.NicknameMismatch;
            message = "닉네임이 다릅니다 — 코드를 만든 때와 같은 닉네임이어야 합니다.";
            return false;
        }

        int pos = 3;
        var d = new PlayerSaveData();
        if (!ReadVarint(body, ref pos, out d.cumulativePlayPoint) ||
            !ReadVarint(body, ref pos, out d.cumulativeClearCount) ||
            !ReadVarint(body, ref pos, out d.bestRunPoint) ||
            !ReadVarint(body, ref pos, out d.playerLevel) || pos != body.Count)
        {
            failure = Failure.Corrupted;
            message = "코드가 손상되었습니다.";
            return false;
        }

        data = d;
        failure = Failure.None;
        message = "";
        return true;
    }

    /// <summary>
    /// 불러오기 병합 규칙(PM 확정): 클리어 횟수가 큰 쪽을 통째로 채택 — 기록이 줄지 않게. 같으면 누적 점수가 큰 쪽.
    /// 돌려주는 값이 true면 코드 쪽이 채택된 것(파일을 코드 값으로 바꿔야 한다).
    /// </summary>
    public static bool CodeWins(PlayerSaveData file, PlayerSaveData code)
    {
        if (file == null) return true;
        if (code.cumulativeClearCount != file.cumulativeClearCount) return code.cumulativeClearCount > file.cumulativeClearCount;
        return code.cumulativePlayPoint > file.cumulativePlayPoint;
    }

    // ───────────── 내부 ─────────────

    static ushort NickHash(string key)
    {
        ulong h = Fnv(Encoding.UTF8.GetBytes(key), Salt ^ 0x9E3779B97F4A7C15UL);
        return (ushort)(h ^ (h >> 16) ^ (h >> 32));
    }

    static ulong Checksum(List<byte> body) => Fnv(body.ToArray(), Salt) & 0xFFFFFF;

    static ulong Fnv(byte[] bytes, ulong seed)
    {
        ulong h = 1469598103934665603UL ^ seed;
        foreach (byte b in bytes) { h ^= b; h *= 1099511628211UL; }
        h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
        return h;
    }

    static void WriteVarint(List<byte> to, int value)
    {
        uint v = (uint)value;
        while (v >= 0x80) { to.Add((byte)(v | 0x80)); v >>= 7; }
        to.Add((byte)v);
    }

    static bool ReadVarint(List<byte> from, ref int pos, out int value)
    {
        value = 0;
        int shift = 0;
        while (pos < from.Count && shift <= 28)
        {
            byte b = from[pos++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return value >= 0;
            shift += 7;
        }
        return false;
    }

    static string ToBase32(byte[] bytes)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (byte b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]); bits -= 5; }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    static byte[] FromBase32(string s)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (char raw in s)
        {
            char c = raw == 'O' ? '0' : raw == 'I' || raw == 'L' ? '1' : raw;   // Crockford 혼동 보정
            int v = Alphabet.IndexOf(c);
            if (v < 0) return null;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8) { bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; buffer &= (1 << bits) - 1; }
        }
        return bytes.ToArray();
    }

    static string Group(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i += 4)
        {
            if (i > 0) sb.Append('-');
            sb.Append(s, i, Math.Min(4, s.Length - i));
        }
        return sb.ToString();
    }
}
