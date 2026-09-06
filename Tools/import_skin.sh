#!/bin/bash
# 다운로드한 스킨 폴더를 프로젝트로 옮긴다.
#
#   Tools/import_skin.sh ~/Downloads/흔함_노태현
#   Tools/import_skin.sh ~/Downloads/*            (여러 개 한꺼번에)
#
# 받는 쪽 폴더 이름이 곧 유닛 이름이다 — Assets/Data/Units/Roster/<이름>.asset 과 맞춘다.
# 압축(rar/zip/7z/tar.gz) 안에 들어 있어도 알아서 푼다. fbx 하나만 있어도 된다.
set -u
PROJ="$(cd "$(dirname "$0")/.." && pwd)"
ROSTER="$PROJ/Assets/Data/Units/Roster"
DEST_ROOT="$PROJ/Assets/Art/Units"

for SRC in "$@"; do
  [ -d "$SRC" ] || { echo "🔴 폴더가 아닙니다: $SRC"; continue; }
  NAME="$(basename "$SRC")"

  # 로스터에 그 이름의 유닛이 있는지 먼저 본다 — 없으면 이름이 틀린 것이다.
  if [ ! -f "$ROSTER/$NAME.asset" ]; then
    echo "🔴 $NAME — 로스터에 그 이름의 유닛이 없습니다. 폴더명을 유닛 이름과 맞추세요."
    continue
  fi

  WORK="$(mktemp -d)"
  # 압축이 있으면 푼다(rar/zip). 없으면 원본을 그대로 쓴다.
  FOUND_ARCHIVE=0
  while IFS= read -r a; do
    FOUND_ARCHIVE=1
    unar -q -f -o "$WORK" "$a" >/dev/null 2>&1
  done < <(find "$SRC" -type f \( -iname '*.rar' -o -iname '*.zip' -o -iname '*.7z' -o -iname '*.tar.gz' -o -iname '*.tgz' \))
  [ "$FOUND_ARCHIVE" = "0" ] && cp -R "$SRC/." "$WORK/"
  # 압축 안에 없던 텍스처(미리보기용 등)도 같이 챙긴다.
  find "$SRC" -type f -iname '*.png' -exec cp {} "$WORK/" \; 2>/dev/null

  FBX="$(find "$WORK" -type f -iname '*.fbx' | head -1)"
  if [ -z "$FBX" ]; then echo "🔴 $NAME — fbx를 못 찾았습니다"; rm -rf "$WORK"; continue; fi

  DEST="$DEST_ROOT/$NAME"
  mkdir -p "$DEST/Textures"
  cp "$FBX" "$DEST/$NAME.fbx"
  find "$WORK" -type f \( -iname '*.png' -o -iname '*.jpg' -o -iname '*.tga' \) -exec cp {} "$DEST/Textures/" \; 2>/dev/null

  BONES=$(strings -n 4 "$DEST/$NAME.fbx" | grep -cE 'LimbNode|Deformer|Cluster')
  ANIMS=$(strings -n 4 "$DEST/$NAME.fbx" | grep -cE 'AnimationStack|AnimationCurve')
  {
    echo "원본 파일명: $(basename "$FBX")"
    echo "받은 폴더:   $SRC"
    echo "옮긴 날:     $(date '+%Y-%m-%d')"
    echo "리깅:        본·스킨 데이터 $BONES 건"
    echo "애니메이션:  커브 $ANIMS 건"
    echo
    echo "⚠️ 출처 라이선스를 확인하고, 상용 게임 추출 에셋이면 배포 전에 교체해야 합니다."
  } > "$DEST/SOURCE.txt"

  echo "✅ $NAME  (본 $BONES · 애니 $ANIMS)"
  rm -rf "$WORK"
done
