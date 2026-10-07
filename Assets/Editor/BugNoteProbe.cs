using System.Reflection;
using UnityEngine;

/// <summary>버그 기록지 촬영(10-07) — gameshot call:BugNoteProbe.Open (창 열고 글·머리줄 넣음) / Info</summary>
static class BugNoteProbe
{
    static BugNotepad Pad => Object.FindFirstObjectByType<BugNotepad>();
    static object Call(string name, params object[] args) => typeof(BugNotepad).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Pad, args);

    static string Open()
    {
        Call("OpenWindow");
        var input = (TMPro.TMP_InputField)typeof(BugNotepad).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Pad);
        input.text = "";
        Call("InsertHeader");
        input.text += "R3에서 이재윤 비행이 멈추지 않음. 정지(S)를 눌러도 계속 간다.\n한글 입력 확인: 가나다라마바사 ABC 123\n";
        Call("InsertHeader");
        input.text += "도박소 Q칸이 안 눌림";
        return $"창 열림 · 글 {input.text.Length}자";
    }

    static string Info()
    {
        var path = (string)typeof(BugNotepad).GetField("savePath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Pad);
        var dirty = (bool)typeof(BugNotepad).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Pad);
        return $"저장 위치 {path} · 아직 안 저장 {dirty} · 파일 있음 {System.IO.File.Exists(path)} {(System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Length + "자" : "")}";
    }
}
