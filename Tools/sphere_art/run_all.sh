#!/bin/sh
# 초월 부가 이펙트 재료 → 납작 JSON 전부 다시(알파벳 = Resources 키 접두, 로스터 = Roster 에셋 이름). 쓰고 나서 유니티에서 call SphereArtBuilder.BuildAll → python3 Tools/sphere_art/build_table.py → refresh
B=~/GRD_motion_trial/초월_부가이펙트
H=$(dirname "$0")/flatten_effects.py
python3 $H $B/초월_구주호_AD juho_ad 초월_구주호_AD
python3 $H $B/초월_김만경_AD kim_ad 초월_김만경_AD
python3 $H $B/초월_최상호_AD sang_ad 초월_최상호_AD
python3 $H $B/초월_박기찬_AD gi_ad 초월_박기찬_AD
python3 $H $B/초월_황준석_ADAP hjs_base 초월_황준석_ADAP
python3 $H $B/_미대응/마르코_날개 hjs_marco 초월_황준석_ADAP
python3 $H $B/_미대응/쿠잔_얼음칼날 hjs_kuzan 초월_황준석_ADAP
python3 $H $B/_미대응/검은_초승달 sang_crescent 초월_최상호_AD
