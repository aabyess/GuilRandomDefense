#!/bin/sh
# 상시 오라 원작 모양(맵 내장 18모델) → 납작 JSON. 별칭 = 폴더 이름 소문자(= SPHERE_ART_BY_ROSTER의 아트 키). 쓰고 나서 유니티 call SphereArtBuilder.BuildAll → build_table.py → refresh
B=~/GRD_motion_trial/초월_상시오라
H=$(dirname "$0")/flatten_effects.py
for d in "$B"/*/; do
  n=$(basename "$d")
  [ -f "$d/effects.json" ] || continue
  a=$(echo "$n" | tr 'A-Z' 'a-z')
  AURA=1 /usr/bin/python3 "$H" "$d" "$a" "$a"
done
