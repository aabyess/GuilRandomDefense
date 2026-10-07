#!/bin/zsh
# 컷인 C안 41기 일괄(초월·불멸·영원 로스터 전부). 사장님 확정 신호 뒤에 돌린다.
#   Tools/blender/run_cutin_c_all.sh            → 캐릭터 렌더(Blender, 허벅지까지·차분한 정면) + 합성·영상
#   Tools/blender/run_cutin_c_all.sh 유닛 …     → 그 유닛만(예: 초월_노태현_AP 불멸_신지우)
#   C_POSE=hips Tools/blender/run_cutin_c_all.sh → 포즈 바꾸기(calm·hips·crossed·point·fist)
# 산출: ~/GRD_cutin/render/C_<유닛>_thigh.png(+.txt 틀 치수) → ~/GRD_cutin/C_all/C_<유닛>.mp4 · _still.png · _timing.txt · _contact_sheet.png
# Assets엔 안 쓴다(읽기만: 로스터 에셋 이름·Assets/Art/Units/<유닛>/<유닛>.fbx).
cd "$(dirname "$0")/../.."
OUT=~/GRD_cutin/render
mkdir -p $OUT
units=("$@")
if [ ${#units} -eq 0 ]; then
  units=(${(f)"$(ls Assets/Data/Units/Roster | grep -E '^(초월|불멸|영원)_.*\.asset$' | sed 's/\.asset$//')"})
fi
for u in $units; do
  fb=Assets/Art/Units/$u/$u.fbx
  if [ ! -f $fb ]; then echo "FBX 없음: $u"; continue; fi
  ONLY=C blender -b --factory-startup --python Tools/blender/gen_cutin_char.py -- $fb $OUT $u 2>&1 | grep -E "^head|Traceback|Error" | sed "s/^/$u: /"
done
/usr/bin/python3 Tools/blender/gen_cutin_comp.py C all $@
