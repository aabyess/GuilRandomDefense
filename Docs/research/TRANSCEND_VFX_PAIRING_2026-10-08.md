# 초월 스킬 ↔ 원작 이펙트 대응표 (2026-10-08, blender)

전체 표(근거 줄·필드 포함)는 `pairing.csv`(= `Docs/research/TRANSCEND_VFX_PAIRING_2026-10-08.csv`). 생성: `Tools/w3x/transcend_vfx_pairing.py`. 변환: `Tools/blender/export_mdx_anim_fbx.py`. 산출 폴더 `~/GRD_motion_trial/transcend_vfx/`(fbx/<모델>/ FBX·Textures·JSON, preview_sheets/, preview_sheet_N.png).

- 우리 초월 스킬 133개(25유닛) 중 짝 있는 것 13개, 나머지 120개는 사장님 신규 사양이라 원작 대응 없음(패시브·오라 포함).
- 짝 있는 원작 이펙트 모델 37종: {'변환됨': 31, '변환불가': 4, '입자 전용(메시 없음)': 2}.

- 🔴 원작(블리자드·중국 모델러) 저작물 — 산출물은 Assets에 안 넣었다. 반입 여부는 PM·사장님 판단(기존 규칙: 원작 텍스처 금지).

- FBX에 못 실리는 것(파티클 PRE2·리본·지오셋/층 알파 곡선·UV 이동)은 `<모델>.json`에 값으로.

## 짝 목록

| 유닛 | 우리 스킬 | 근거 | 원작 트리거/능력 | 모델 | 변환 |
|---|---|---|---|---|---|
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `Stormfall.mdx` | 변환됨 파티클 8줄기(JSON만) |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `Abilities\\Weapons\\Bolt\\BoltImpact.mdl` | 변환불가 워크3 기본 모델(맵에 없음) — 우리 쪽 Kenney/자체 메시로 근사 |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `BY_Wood_Effect_Texture_Density_Integration_5.mdl` | 변환됨  |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `Mdx_Effect_Laser_Blue.mdl` | 변환됨  |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `Mdx_Effect_Railgun.mdl` | 변환됨  |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `LB_Nami.mdl` | 변환됨  |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 이름 | 트리거 Nami_Skill_2(깊이 1) · 공격 Nami_Attack( | `Abilities\Weapons\SpiritOfVengeanceMissile\SpiritOfVengeanceMissile.mdl` | 변환불가 워크3 기본 모델(맵에 없음) — 우리 쪽 Kenney/자체 메시로 근사 |
| 초월_강주혁_AP | 나미 — 5절대쿨 | 이름 | 트리거 Nami_Skill_4(깊이 1) · 공격 Nami_Attack( | `tornado33.mdl` | 변환됨 파티클 12줄기(JSON만) |
| 초월_강주혁_AP | 나미 — 5절대쿨 | 이름 | 트리거 Nami_Skill_4(깊이 1) · 공격 Nami_Attack( | `namifire3.mdl` | 변환됨 파티클 16줄기(JSON만) |
| 초월_강주혁_AP | 나미 — 5절대쿨 | 이름 | 트리거 Nami_Skill_4(깊이 1) · 공격 Nami_Attack( | `Abilities\Spells\Other\Monsoon\MonsoonBoltTarget.mdl` | 변환불가 워크3 기본 모델(맵에 없음) — 우리 쪽 Kenney/자체 메시로 근사 |
| 초월_강주혁_AP | 염왕 — 1/15 | 이름 | 트리거 Zoro_enfor_3dragon(깊이 1) · 공격 Zoro_A | `swordeff1.mdl` | 변환됨  |
| 초월_강주혁_AP | 염왕 — 1/15 | 이름 | 트리거 Zoro_enfor_3dragon(깊이 1) · 공격 Zoro_A | `rb7.mdl` | 변환됨  |
| 초월_강주혁_AP | 염왕 — 1/15 | 이름 | 트리거 Zoro_enfor_3dragon(깊이 1) · 공격 Zoro_A | `animeslashfinal.mdl` | 변환됨  |
| 초월_강주혁_AP | A0B0 2범위 나미초월 블리자드3 | 이름 | 트리거 Nami_Skill_4(깊이 1) · 공격 Nami_Attack( | `tornado33.mdl` | 변환됨 파티클 12줄기(JSON만) |
| 초월_강주혁_AP | A0B0 2범위 나미초월 블리자드3 | 이름 | 트리거 Nami_Skill_4(깊이 1) · 공격 Nami_Attack( | `namifire3.mdl` | 변환됨 파티클 16줄기(JSON만) |
| 초월_강주혁_AP | A0B0 2범위 나미초월 블리자드3 | 이름 | 트리거 Nami_Skill_4(깊이 1) · 공격 Nami_Attack( | `Abilities\Spells\Other\Monsoon\MonsoonBoltTarget.mdl` | 변환불가 워크3 기본 모델(맵에 없음) — 우리 쪽 Kenney/자체 메시로 근사 |
| 초월_강주혁_AP | 나미 — Nami_Skill_1 헤비레인 1/24(400범위 300000 | 이름 | 트리거 Nami_Skill_1(깊이 1) · 공격 Nami_Attack( | `cumulonimbus.mdl` | 변환됨 파티클 8줄기(JSON만) |
| 초월_강주혁_AP | 나미 — Nami_Skill_1 헤비레인 1/24(400범위 300000 | 이름 | 트리거 Nami_Skill_1(깊이 1) · 공격 Nami_Attack( | `Thunder_nami4.mdl` | 변환됨 파티클 45줄기(JSON만) |
| 초월_강주혁_AP | 나미 — Nami_Skill_1 헤비레인 1/24(400범위 300000 | 이름 | 트리거 Nami_Skill_1(깊이 1) · 공격 Nami_Attack( | `Thunder_nami7.mdl` | 변환됨 파티클 8줄기(JSON만) |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `BY_Wood_Eff_Ord_YeYe_Wid_KuoSan_1.mdx` | 변환됨  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `animeslashfinal.mdl` | 변환됨  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `EnergyBurst3sinobu.mdl` | 변환됨  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `saitamawave3.mdl` | 변환됨  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `az3.mdl` | 변환됨 파티클 4줄기(JSON만) |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `az_firering1a.mdl` | 변환됨 파티클 2줄기(JSON만) |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000+ STR×150 | 이름 | 트리거 Zoro_Samchun2(깊이 1) · 공격 Zoro_Attack | `BlSkill04A.mdl` | 변환됨  |
| 초월_강주혁_AP | A14E 삼도류 검술-조초 — 적 방어-50 오라 | 능력코드 | 능력 A14E B삼도류 검술-조초 | `Ora_zoro.mdx` | 입자 전용(메시 없음) 파티클 2줄기(JSON만) |
| 초월_양재모_AD | 상호파의최강자 — 현재체력(평타1/8, 대상 현재 체력 20%) | 문턱 일치(rand 8) | 트리거 Law_Skill_2_reinforce(깊이 1) · 공격 Law | `AZ_Zeusking_Impact-black.mdl` | 변환됨 파티클 3줄기(JSON만) |
| 초월_양재모_AD | 상호파의최강자 — 현재체력(평타1/8, 대상 현재 체력 20%) | 문턱 일치(rand 8) | 트리거 Law_Skill_2_reinforce(깊이 1) · 공격 Law | `EnergyBurst2-law.mdl` | 변환됨 파티클 2줄기(JSON만) |
| 초월_양재모_AD | 상호파의최강자 — 현재체력(평타1/8, 대상 현재 체력 20%) | 문턱 일치(rand 8) | 트리거 Law_Skill_2_reinforce(깊이 1) · 공격 Law | `injection.mdl` | 변환됨  |
| 초월_양재모_AD | 상호파의최강자 — 현재체력(평타1/8, 대상 현재 체력 20%) | 문턱 일치(rand 8) | 트리거 Law_Skill_2_reinforce(깊이 1) · 공격 Law | `AZ_Zeusking_Impact-blue.mdl` | 변환됨 파티클 3줄기(JSON만) |
| 초월_임채민_AP | 지상낙원 — 발동: 마젠 4(범위증폭10%) 3초 | 문턱 일치(rand 10) | 트리거 Tichi_skill_4_tr(깊이 1) · 공격 Tichi_At | `ThunderclapCasterBlack.mdx` | 변환됨 파티클 1줄기(JSON만) |
| 초월_임채민_AP | 지상낙원 — 발동: 마젠 4(범위증폭10%) 3초 | 문턱 일치(rand 10) | 트리거 Tichi_skill_4_tr(깊이 1) · 공격 Tichi_At | `NewGroundEX.mdl` | 입자 전용(메시 없음) 파티클 22줄기(JSON만) |
| 초월_임채민_AP | 지상낙원 — 발동: 마젠 4(범위증폭10%) 3초 | 문턱 일치(rand 10) | 트리거 Tichi_skill_4_tr(깊이 1) · 공격 Tichi_At | `ThunderClapCaster22.mdl` | 변환됨 파티클 1줄기(JSON만) |
| 초월_임채민_AP | 지상낙원 — 발동: 마젠 4(범위증폭10%) 3초 | 문턱 일치(rand 10) | 트리거 Tichi_skill_4_tr(깊이 1) · 공격 Tichi_At | `BY_Wood_Sand_YueKongJi_nocrack_black.mdl` | 변환됨 파티클 13줄기(JSON만) |
| 초월_임채민_AP | 지상낙원 — 발동: 마젠 4(범위증폭10%) 3초 | 문턱 일치(rand 10) | 트리거 Tichi_skill_4_tr(깊이 1) · 공격 Tichi_At | `by_kid4d.mdl` | 변환됨 파티클 4줄기(JSON만) |
| 초월_조성진_AD | A0IE 9도움소 독약1 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `Explorer_BOOM.mdl` | 변환됨 파티클 25줄기(JSON만) |
| 초월_조성진_AD | A0IE 9도움소 독약1 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `!shot09.mdl` | 변환됨 파티클 2줄기(JSON만) |
| 초월_조성진_AD | A0IE 9도움소 독약1 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `1gun55_dp.mdl` | 변환됨 파티클 19줄기(JSON만); 리본 1(미변환) |
| 초월_조성진_AD | A0IE 9도움소 독약1 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `Explorer_BOOM.mdl` | 변환됨 파티클 25줄기(JSON만) |
| 초월_조성진_AD | G.O.D — MANA게이지140.00 | 이름 | 트리거 Usop_Skill_Mana(깊이 1) · 공격 Usop_Atta | `units\human\phoenix\phoenix.mdl` | 변환불가 워크3 기본 모델(맵에 없음) — 우리 쪽 Kenney/자체 메시로 근사 |
| 초월_조성진_AD | G.O.D — MANA게이지140.00 | 이름 | 트리거 Usop_Skill_Mana(깊이 1) · 공격 Usop_Atta | `SuperBigExplosion.mdl` | 변환됨 파티클 7줄기(JSON만) |
| 초월_조성진_AD | G.O.D — 1/5 | 이름 | 트리거 Usop_Skill_2(깊이 1) · 공격 Usop_Attack( | `!shot09.mdl` | 변환됨 파티클 2줄기(JSON만) |
| 초월_조성진_AD | G.O.D — 1/5 | 이름 | 트리거 Usop_Skill_2(깊이 1) · 공격 Usop_Attack( | `1gun55_dp.mdl` | 변환됨 파티클 19줄기(JSON만); 리본 1(미변환) |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상(450000 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `Explorer_BOOM.mdl` | 변환됨 파티클 25줄기(JSON만) |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상(450000 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `!shot09.mdl` | 변환됨 파티클 2줄기(JSON만) |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상(450000 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `1gun55_dp.mdl` | 변환됨 파티클 19줄기(JSON만); 리본 1(미변환) |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상(450000 | 이름 | 트리거 Usop_Attack(깊이 0) · 공격 Usop_Attack(H | `Explorer_BOOM.mdl` | 변환됨 파티클 25줄기(JSON만) |
