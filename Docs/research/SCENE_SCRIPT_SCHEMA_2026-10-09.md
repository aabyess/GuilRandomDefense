# 원작 연출 「대본」 json 형식 (schemaVersion 1)

위치: `~/GRD_scenes/scripts/<id>.json` — 샘플: `shanks_haki.json`(가장 단순) · `enel_eltor.json`(스테이지 머신 풀어 씀) · `dragon_storm.json`(랜덤 낙뢰 11회) · `shiki_fleet.json`(함대 + 투사체).
사진(Blender 재현): `~/GRD_scenes/render/<id>_sheet.png` (스냅샷 `<id>_<n>.png`). 생성 `Tools/w3x/scene_scripts.py`·`Tools/blender/render_scene.py`·`Tools/w3x/scene_sheet.py`(repo).

## 단위
- 거리 = **워크3 단위**(1 = 0.01 m, 우리 맵 환산은 SkillVfxTable.nativeSizes), 시간 = 초, 각도 = 도(°), 투명도 0~1.
- 높이(flyHeight)는 워크3 비행 높이(지면 기준 위로).

## 최상위
`id, title, ourUnit(우리 로스터), origUnit(원작 H코드·이름), ourSkill(붙일 우리 스킬 에셋 이름 또는 「PM이 정할 것」), source{trigger, jFunction, jLine, mechanism(스테이지 머신 요약)}, trigger{cause, anchors{caster|target 설명}}, durationSec, timeline[…], models{…}`

## timeline 항목 (t 오름차순)
| op | 필드 | 뜻 |
|---|---|---|
| `spawn` | `t, id, dummyUnit(원작 더미 유닛 코드), unitName, model(모델 폴더 이름), modelPath, baseScale(usca), at{anchor:"caster"|"target"|"ship", polar?{radius,angleDeg}, ship?}, scalePercent?(SetUnitScalePercent 100=기본), flyHeight(초기), anim?("birth"/"death"/없음=stand→첫 시퀀스), timescale?, vertexAlpha?(1=불투명), facing?(도|"random"|"caster"), owner("caster"|"neutral"), lifeSec(수명; null=모델 시퀀스 따름), deathSec(사망 연출 지속), moveTo?{anchor, speedPerSec, arc}, random?{…}, note` | 더미 유닛(=원작의 이펙트 껍데기) 생성. 실제 크기 = baseScale × scalePercent/100 |
| `set` | `t, id, scalePercent?, timescale?` | 이후 값 변경 |
| `ramp` | `t, id, prop:"flyHeight", to, ratePerSec` | 현재 높이에서 to까지 초당 rate로 직선 이동 |
| `kill` | `t, id` | 죽임 → 사망(death) 시퀀스 deathSec 동안 재생 후 제거 |
| `damage` | `t, shape:"circle", radius, around:"caster"|"target"|"spawn:<id>", amount?, stun?` | 피해 판정(연출은 아님, 우리 SkillData가 담당) |
| `camera_shake` | `t, durationSec, magnitude` | 원작 vibration(플레이어, 크기, 시간) — 단위는 원작 값 그대로 |
| `note` | `t, text` | 설명 |
- 수명: 더미 유닛은 체력 1~4에 재생 −1/s라 **lifeSec = 체력/초당 감소**(w3u에서 계산해 넣음). TimedLife가 명시되면(전함 2.25s) 그 값.
- 모든 spawn은 「위치=anchor + polar」, 높이=flyHeight. 이동 유닛은 `moveTo`(투사체).
- `random`이 있는 값은 원작이 매번 다르게 뽑는 것(범위 [lo,hi])이고 json에는 seed 고정값이 들어 있다 → 재현기는 다시 뽑아도 됨.

## models{} (모델 이름 → 재생에 필요한 것)
`folder`(~/GRD_orig_vfx_trial/<folder>/ — 없으면 null=워크3 기본 모델, 맵에 없음 → **자체 메시로 대체**, note 참고), `inMap`, `sequencesMs[{name,start,end,loop}]`(MDX 전체 시간 ms; 시퀀스 길이=end−start), `meshCount`, `jsonPath`(그 모델의 FBX json: unity 블록 uvOffsetClip0·알파 곡선·레이어 블렌드), `pre2[…]`:
- PRE2 한 묶음 필드 전부: `node, nodeFlags, parentId, pivot[x,y,z](모델 좌표), speed, variation(속도 변동 비율), latitude(방출각 °), gravity, lifespanSec, emissionRate(초당), width, length(방출 면), filterMode(blend|additive|modulate|modulate2x|alphakey), rows, cols(플립북), headOrTail, tailLength, midTime(0~1: 3구간 경계), colorsRGB01[시작,중간,끝], alpha255[시작,중간,끝], scale3[시작,중간,끝](워크3 단위 지름), squirt, textureIndex, tracks{KP2E(방출률 키)·KP2V(가시 0/1)·KP2S 속도 …: interp, keysMsAbs[[ms,[값…]]]}`.
- 키 시간은 MDX 전체 ms(시퀀스 start를 빼서 쓴다). 입자는 월드 공간(부모를 안 따라감)이 기본, 방출 위치 = pivot + (width×length 면 안 무작위).
- 재생 순서 규칙: **한 층의 알파 곡선(geoset 알파·층 알파)에서 시퀀스 첫 키보다 앞 시각은 정적 알파(보통 1.0)** — 키가 다음 시퀀스부터 시작하는 모델(roarthunder 6개 낙뢰 시퀀스)이 있다. 모델 json의 geosetAlphaKeys/layerAlphaKeys를 쓸 때 반드시 적용할 것.
- 시퀀스 선택: 더미를 만들면 `stand`(없으면 모델의 첫 시퀀스)가 돈다. `anim:"birth"`는 SetUnitAnimation("birth"). 여러 「Birth-N」 시퀀스가 있으면(roarthunder) 원작은 이름이 같은 시퀀스 중 무작위 하나(가중치 = rarity)를 고른다.

## 근사·한계
- 사진은 입자 값 추정 시뮬레이션(원작 난수·정렬과 다름), 가산 합성은 근사. 크기·타이밍은 대본 값 그대로.
- 워크3 기본 모델(번개 줄기 Lightningbolt·전함 HumanBattleship·Tranquility·돌 덩어리)은 맵에 없어 변환 불가 → 사진엔 자리표시자.

## 추가(10-09 구현담당2 요청 반영)
- `models.<모델>.playSequence{name,startMs,endMs}` = 기본 재생 시퀀스(stand, 없으면 첫 시퀀스). `pre2[].sequenceStartMs`가 같은 기준값(keysMsAbs에서 빼면 시퀀스 상대 ms). spawn에 `anim`이 있으면 그 이름이 든 시퀀스 기준(sequencesMs에서 start 확인).
- `pre2[].textureFile` = ~/GRD_orig_vfx_trial/<모델>/Textures/<파일>.png (+ `textureSourcePath` 원작 경로). **입자 텍스처 PNG가 폴더에 빠져 있던 것 1018개를 전부 복사해 채움**(Tools/w3x/fix_pre2_textures.py) — 이전에 반입한 모델은 Textures/를 다시 복사할 것.
- folder=null 모델은 `substituteNote`(대체 모양·크기 한 줄). spawn의 `substitute`(해적선 프리팹 경로·길이 380)·`originalModel`(샹크스 번개 = roarthunder로 대체) 참고.
- 대본 개수: 4(샹크스·시키·에넬·드래곤). 더 필요하면 표(Docs/research/REPRESENTATIVE_SCENES_2026-10-09.md)에서 PM이 정함.
