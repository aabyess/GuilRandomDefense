#!/bin/zsh
# 원랜디 창고 15(스킨)·16(스킬) 전체 파이프라인 재현 (2026-10-08). 저장소 루트에서:  zsh Tools/w3x/skin_warehouse_run.sh
# 0) 이름 목록: /usr/bin/python3 Tools/w3x/original_asset_inventory.py → vfx_warehouse_index.py(models.json)
# 15) 캐릭터:  skin_warehouse_build.py prep → mdx_extract → export_mdx_anim_fbx → verify_mdx_fbx → render_mdx → skin_warehouse_build.py build
# 16) 이펙트:  models.json의 fx → mdx_extract → export_mdx_anim_fbx → verify_mdx_fbx → render_cast_vfx orig → vfx_warehouse_build.py
# 17/18) icon_warehouse_build.py · sound_warehouse_build.py
set -e
MPQ=~/GRD_motion_trial/_work/ord.mpq
/usr/bin/python3 Tools/w3x/original_asset_inventory.py
/usr/bin/python3 Tools/w3x/vfx_warehouse_index.py
S=~/GRD_motion_trial/original_skin; mkdir -p $S/work $S/preview
/usr/bin/python3 Tools/w3x/skin_warehouse_build.py prep $S
tr '\n' '\0' < $S/char_names.txt | xargs -0 -n 60 /usr/bin/python3 Tools/w3x/mdx_extract.py $MPQ $S/work
rm -rf $S/fbx; tr '\n' '\0' < $S/char_names.txt | xargs -0 -n 30 blender -b --factory-startup --python Tools/blender/export_mdx_anim_fbx.py -- $S/work $S/fbx
blender -b --factory-startup --python Tools/blender/verify_mdx_fbx.py -- $S/work $S/fbx $S/verify.json
tr '\n' '\0' < $S/char_names.txt | xargs -0 -n 46 -P 4 blender -b --factory-startup --python Tools/blender/render_mdx.py -- $S/work $S/preview
/usr/bin/python3 Tools/w3x/skin_warehouse_build.py build $S
V=~/GRD_motion_trial/original_vfx; mkdir -p $V/work $V/preview
/usr/bin/python3 - <<'PY' > $V/fx_names.txt
import json,os
print('\n'.join(x['file'] for x in json.load(open(os.path.expanduser('~/GRD_motion_trial/original_vfx/models.json'))) if x['kind']=='fx'))
PY
tr '\n' '\0' < $V/fx_names.txt | xargs -0 -n 60 /usr/bin/python3 Tools/w3x/mdx_extract.py $MPQ $V/work
rm -rf $V/fbx; tr '\n' '\0' < $V/fx_names.txt | xargs -0 -n 100 blender -b --factory-startup --python Tools/blender/export_mdx_anim_fbx.py -- $V/work $V/fbx
blender -b --factory-startup --python Tools/blender/verify_mdx_fbx.py -- $V/work $V/fbx $V/verify.json
tr '\n' '\0' < $V/fx_names.txt | xargs -0 -P 3 -I{} sh -c 'blender -b --factory-startup --python Tools/blender/render_cast_vfx.py -- orig '$V'/work '$V'/preview "$1" 1.0 4 >/dev/null 2>&1' _ {}
/usr/bin/python3 Tools/w3x/vfx_warehouse_build.py
/usr/bin/python3 Tools/w3x/icon_warehouse_build.py
/usr/bin/python3 Tools/w3x/sound_warehouse_build.py
