#!/bin/bash
# 에디터 없이 Assembly-CSharp-Editor(Assets/Editor)를 Roslyn(csc)으로 컴파일해 에러를 본다(출력 dll은 버림). csc_check.sh의 Editor 판.
# 사용: Tools/ui/csc_check_editor.sh [프로젝트 루트=현재 폴더]
# 참조(-r:)는 주 저장소 Library/ScriptAssemblies의 Assembly-CSharp.dll(유니티가 마지막으로 컴파일한 것)을 쓴다 —
# 그래서 worktree에서 Assets/Scripts를 바꾼 경우엔 이 검사가 낡은 Assembly-CSharp을 본다(Editor만 고친 경우에 쓸 것).
WT="${1:-$PWD}"; MAIN=/Users/sang/GitHub/GuilRandomDefense
U=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
RSP=$(ls -t $MAIN/Library/Bee/artifacts/*/Assembly-CSharp-Editor.rsp | head -1)
TMP=$(mktemp -d)
cd "$WT"
grep -v '^-out:\|^-refout:\|\.cs"$' "$RSP" | sed "s#\"Library/#\"$MAIN/Library/#g" > "$TMP/cc.rsp"
echo "-out:$TMP/cc_out.dll" >> "$TMP/cc.rsp"
grep '\.cs"$' "$RSP" | grep -v '^"Assets/Editor/' >> "$TMP/cc.rsp"
find Assets/Editor -name '*.cs' | sed 's#^#"#; s#$#"#' >> "$TMP/cc.rsp"
$U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig @"$TMP/cc.rsp" 2>&1 | grep -E "error" | head -40
rm -rf "$TMP"
echo "csc_check_editor done"
