#!/bin/bash
# 에디터 없이 Assembly-CSharp만 Roslyn(csc)으로 컴파일해 에러를 본다(출력 dll은 버림). 판·에디터가 도는 중에도 안전(Assets를 안 건드린다).
# 사용: Tools/ui/csc_check.sh [프로젝트 루트=현재 폴더]   — 참조·정의는 주 저장소 Library/Bee/artifacts/*/Assembly-CSharp.rsp(유니티가 마지막으로 컴파일한 것)를 쓴다.
# 한계: Editor 폴더(Assembly-CSharp-Editor)는 검사 안 함. 새 asmdef·패키지를 추가한 직후엔 rsp가 낡았을 수 있다.
WT="${1:-$PWD}"; MAIN=/Users/sang/GitHub/GuilRandomDefense
U=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
RSP=$(ls -t $MAIN/Library/Bee/artifacts/*/Assembly-CSharp.rsp | head -1)
TMP=$(mktemp -d)
cd "$WT"
grep -v '^-out:\|^-refout:\|\.cs"$' "$RSP" | sed "s#\"Library/#\"$MAIN/Library/#g" > "$TMP/cc.rsp"
echo "-out:$TMP/cc_out.dll" >> "$TMP/cc.rsp"
grep '\.cs"$' "$RSP" | grep -v '^"Assets/Scripts/' >> "$TMP/cc.rsp"
find Assets/Scripts -name '*.cs' | sed 's#^#"#; s#$#"#' >> "$TMP/cc.rsp"
$U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig @"$TMP/cc.rsp" 2>&1 | grep -E "error" | head -40
rm -rf "$TMP"
echo "csc_check done"
