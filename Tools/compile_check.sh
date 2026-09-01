#!/bin/zsh
# Unity를 열지 않고 C# 컴파일 오류를 잡는다.
#
# 왜 필요한가: 컴파일이 실패하면 Unity는 에러를 콘솔에 찍되 **마지막으로 성공한 어셈블리를
# 그대로 유지한다.** 메뉴도 옛 코드로 계속 동작하기 때문에, 겉보기에는 "고쳤는데 반영이 안 된다"로만
# 보인다. 실제로 이 함정에 두 번 빠졌다.
#
# 사용법: Tools/compile_check.sh
# 에러가 없으면 아무것도 출력하지 않고 0을 반환한다.

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

UNITY_VERSION=$(grep -m1 'm_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}')
U="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents"

if [[ ! -d "$U" ]]; then
  echo "Unity ${UNITY_VERSION}를 찾지 못했습니다: $U" >&2
  exit 2
fi

if [[ ! -d Library/ScriptAssemblies ]]; then
  echo "Library/ScriptAssemblies가 없습니다. Unity로 한 번 열어 패키지를 컴파일해야 합니다." >&2
  exit 2
fi

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

# 참조: .NET 표준 + Unity 엔진/에디터 + 패키지 어셈블리
{
  ls "$U/NetStandard/ref/"*/*.dll
  ls "$U/Managed/UnityEngine/"*.dll
  echo "$U/Managed/UnityEditor.dll"
  ls Library/ScriptAssemblies/*.dll
} | sort -u | sed 's/^/-r:/' > "$WORK/refs.rsp"

find Assets/Scripts Assets/Editor -name '*.cs' > "$WORK/sources.txt"

"$U/NetCoreRuntime/dotnet" "$U/DotNetSdkRoslyn/csc.dll" \
  -target:library -nologo -noconfig -nostdlib -langversion:9 \
  -nowarn:CS0169,CS0414,CS0649,CS0436,CS8032 \
  -out:"$WORK/check.dll" "@$WORK/refs.rsp" $(cat "$WORK/sources.txt") 2>&1 | grep -E "error" && exit 1

exit 0
