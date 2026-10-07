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
# 후보 시트(유닛마다 한 줄: c1~c3 공격 프레임 · p_* 프리셋 · ★ = 지금 고른 것) + 41기 연속 영상
/usr/bin/python3 - <<'PY'
import glob, json, os, subprocess
from PIL import Image, ImageDraw, ImageFont
R = os.path.expanduser("~/GRD_cutin/render"); D = os.path.expanduser("~/GRD_cutin/C_all")
F = ImageFont.truetype(os.path.expanduser("~/GRD_cutin/fonts/NanumGothic-ExtraBold.ttf"), 18)
infos = [json.load(open(f)) for f in sorted(glob.glob(os.path.join(R, "C_*_cands.json")))]
S, cols = 180, 8
sheet = Image.new("RGB", (S * cols + 220, (S + 26) * len(infos)), (40, 42, 50)); d = ImageDraw.Draw(sheet)
for r, inf in enumerate(infos):
    y = r * (S + 26)
    d.text((8, y + 8), inf["unit"], font=F, fill=(255, 255, 255)); d.text((8, y + 34), inf.get("source", ""), font=F, fill=(180, 200, 255))
    for c, cd in enumerate(inf["candidates"][:cols]):
        im = Image.open(os.path.join(R, cd["file"])).convert("RGBA").resize((S, S))
        bg = Image.new("RGBA", (S, S), (70, 74, 86, 255) if cd["id"] != inf["chosen"] else (120, 100, 40, 255)); bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (220 + c * S, y))
        d.text((224 + c * S, y + S + 2), ("★ " if cd["id"] == inf["chosen"] else "") + cd["id"], font=F, fill=(255, 220, 120) if cd["id"] == inf["chosen"] else (220, 220, 220))
sheet.save(os.path.join(D, "_candidates_sheet.png"))
vids = sorted(glob.glob(os.path.join(D, "C_*.mp4")))
with open(os.path.join(D, "_concat.txt"), "w") as fp:
    fp.write("".join(f"file '{v}'\n" for v in vids))
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", os.path.join(D, "_concat.txt"), "-c", "copy", os.path.join(D, "컷인_C안_41기_연속.mp4")], check=True)
print("후보 시트", len(infos), "연속 영상", len(vids))
PY
