#!/bin/zsh
# 전체 렌더+후처리 (정본 스크립트: Tools/blender/gen_pachinko_fx.py)
S=/Users/sang/GitHub/GuilRandomDefense/Tools/blender/gen_pachinko_fx.py
LIST=(starburst shockwave coins speedlines fire slime rainbow text_chance text_geki text_kakuhen text_push); [ $# -gt 0 ] && LIST=($@)
for e in $LIST; do
  rm -rf ~/grd/GRD_pachinko_fx/$e
  /opt/homebrew/bin/blender -b --factory-startup --python $S -- render $e >/dev/null 2>&1
  /usr/bin/python3 -W ignore $S post $e >/dev/null 2>&1
  echo "done $e"
done
