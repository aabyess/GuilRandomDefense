#!/bin/bash
# 영상형 파칭코 테마 영상 변환(2026-10-16): 원본(AV1 1080p·Opus)은 Unity VideoPlayer가 못 읽는다 → H.264 720p + AAC.
#   사용: Tools/pachinko_video_transcode.sh fire|slime [원본경로]
#   fire  : ~/GRD_pachinko_art/fireforce_ref/src2/fireforce_play.mp4 (https://youtu.be/dyEh7wz1Xrk) → 오른쪽 위 워터마크 delogo → StreamingAssets/fireforce_play.mp4
#   slime : ~/GRD_pachinko_art/tensura_ref/src/tensura_play.mp4      (https://youtu.be/jCWZuFgaPFI) → StreamingAssets/tensura_play.mp4
# 공개 소개 레포에는 넣지 않는다(저작권). 값은 PachinkoSettings.VideoThemes.
set -e
KEY="${1:?fire|slime}"
case "$KEY" in
  fire)  SRC_DEF="$HOME/GRD_pachinko_art/fireforce_ref/src2/fireforce_play.mp4"; OUT=fireforce_play.mp4; VF="scale=1280:720,delogo=x=1150:y=8:w=122:h=78";;
  slime) SRC_DEF="$HOME/GRD_pachinko_art/tensura_ref/src/tensura_play.mp4";      OUT=tensura_play.mp4;   VF="scale=1280:720";;
  *) echo "모르는 테마: $KEY"; exit 1;;
esac
SRC="${2:-$SRC_DEF}"
DST="$(cd "$(dirname "$0")/.." && pwd)/Assets/StreamingAssets/$OUT"
ffmpeg -y -loglevel error -i "$SRC" -vf "$VF" \
  -c:v libx264 -preset slow -crf 32 -pix_fmt yuv420p -profile:v main -r 30000/1001 \
  -c:a aac -b:a 128k -ar 44100 -movflags +faststart "$DST"
ls -la "$DST"
