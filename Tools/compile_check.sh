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
  # Photon Fusion(멀티, Assets/Scripts/Net): 미리 빌드된 DLL이라 ScriptAssemblies에 없다 — 빠지면 Net 파일에서 CS0246이 수백 줄.
  ls Assets/Photon/Fusion/Assemblies/*.dll 2>/dev/null || true
  ls Assets/Photon/PhotonLibs/netstandard2.0/release/*.dll 2>/dev/null || true
} | sort -u | sed 's/^/-r:/' > "$WORK/refs.rsp"

find Assets/Scripts Assets/Editor -name '*.cs' > "$WORK/sources.txt"

# UNITY_EDITOR: 에디터에서 Unity가 붙이는 기호. 없으면 `#if UNITY_EDITOR` 안의 에디터 전용 코드(NetLauncher.EditorSetup 등)를
# 부르는 Assets/Editor 쪽이 없는 멤버로 잡힌다.

"$U/NetCoreRuntime/dotnet" "$U/DotNetSdkRoslyn/csc.dll" \
  -target:library -nologo -noconfig -nostdlib -langversion:9 -define:UNITY_EDITOR \
  -nowarn:CS0169,CS0414,CS0649,CS0436,CS8032 \
  -out:"$WORK/check.dll" "@$WORK/refs.rsp" $(cat "$WORK/sources.txt") 2>&1 | grep -E "error" && exit 1

exit 0
