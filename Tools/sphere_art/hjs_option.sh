#!/bin/sh
# 황준석_ADAP 겹침 결정안(사장님 답 대기) — 쓰고 나서 유니티 call SphereArtBuilder.BuildAll → build_table.py → refresh → 사진
#   a: 빅맘 구름(bigmom7) 빼기
#   b: a + 마르코 날개(mrk7) 0.6배
#   none: 원래대로
B=~/GRD_motion_trial/초월_부가이펙트
H=$(dirname "$0")/flatten_effects.py
P=/usr/bin/python3
case "$1" in
  a)    SKIP_MODELS=bigmom7.mdx $P $H $B/초월_황준석_ADAP hjs_base 초월_황준석_ADAP
        $P $H $B/_미대응/마르코_날개 hjs_marco 초월_황준석_ADAP ;;
  b)    SKIP_MODELS=bigmom7.mdx $P $H $B/초월_황준석_ADAP hjs_base 초월_황준석_ADAP
        MODEL_SCALE=mrk7.mdx:0.6 $P $H $B/_미대응/마르코_날개 hjs_marco 초월_황준석_ADAP ;;
  none) $P $H $B/초월_황준석_ADAP hjs_base 초월_황준석_ADAP
        $P $H $B/_미대응/마르코_날개 hjs_marco 초월_황준석_ADAP ;;
  *) echo "usage: hjs_option.sh a|b|none"; exit 1 ;;
esac
