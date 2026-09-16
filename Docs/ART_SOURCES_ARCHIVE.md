# 스킨 원본 보관 기록 (2026-09-14)

사장님 결정(09-14): 게임에 들어간 스킨의 다운로드 원본은 **정보만 저장소에 적고 지운다**.
게임 스킨은 저장소의 FBX·텍스처·git 첫 임포트 커밋만으로 다시 지을 수 있다(`Tools/blender/fix_unit_fbx.py`가 git rev에서 원본을 꺼냄).
🔴 예외 — 삭제 제외·보존(스크립트가 ~/Downloads에서 직접 읽음): `zombi.glb`·`nanachi.glb`·`luffy.glb`·`denji_and_pochita.glb`
(`Tools/blender/fix_unit_fbx.py` DL 경로, luffy는 `Tools/fix_glb_rig_for_humanoid.py`도). 이 넷을 지우려면 먼저 저장소로 옮기고 경로를 바꿀 것.
🔴 예외 — 스킨 미완성이라 보존: 안흔함_강주혁(알라) `hxh__koala_chimera_ant.glb`·`강주혁_mixamo업로드.obj` (모델 FBX 없음, Mixamo 리깅 대기).
휴지통(~/.Trash)에 있던 항목은 기록만 했고 휴지통 비우기는 사장님 몫.

원본을 다시 받아야 하면 아래 파일명·zip 이름으로 Sketchfab 등에서 찾고 sha256 앞 16자리로 같은 파일인지 확인한다.

## 안흔함_강재규

- ~/Downloads/jaguar.zip · 7.2MB · sha256 a287851024ec7a68
    source/jaguar.fbx (4.2MB)
    textures/aiStandardSurface5_baseColor.jpeg (0.0MB)
    textures/aiStandardSurface1_baseColor.jpeg (2.9MB)
- ~/Downloads/jaguar/ (풀어 둔 폴더 · 7.2MB · 파일 3개)
    textures/aiStandardSurface5_baseColor.jpeg (0.0MB)
    textures/aiStandardSurface1_baseColor.jpeg (2.9MB)
    source/jaguar.fbx (4.2MB)

## 안흔함_강주혁

- ~/Downloads/hxh__koala_chimera_ant.glb · 25.6MB · sha256 d1d338abd1d92cc6
    glb: 메시 4 · 노드 8 · 스킨 0 · 애니 0 [] · 이미지 3
    재질 tripo_material_6a2fe4d0-e7a3-4194-a14c-a24004d42c3c→image0/n:image2
- ~/Downloads/강주혁_mixamo업로드.obj · 45.3MB · sha256 3c3fe7d4882b7221

## 안흔함_김경현

- ~/.Trash/sanji-one-piece.zip · 7.4MB · sha256 6dfe7d016f9a8ba7
    source/sanji kuroasi.fbx (5.7MB)
    textures/1653.png (0.6MB)
    textures/1717.png (0.1MB)
    textures/1664.png (0.4MB)
    textures/1707.png (0.0MB)
    textures/1696.png (0.1MB)
    textures/1735.png (0.0MB)
    textures/1685.png (0.0MB)
    textures/1674.png (0.1MB)
    textures/1642.png (0.0MB)
    textures/1631.png (0.4MB)
- ~/.Trash/안흔함_김경현/ (풀어 둔 폴더 · 7.4MB · 파일 11개)
    textures/1631.png (0.4MB)
    textures/1735.png (0.0MB)
    textures/1696.png (0.1MB)
    textures/1642.png (0.0MB)
    textures/1685.png (0.0MB)
    textures/1653.png (0.6MB)
    textures/1674.png (0.1MB)
    textures/1717.png (0.1MB)
    textures/1707.png (0.0MB)
    textures/1664.png (0.4MB)
    source/sanji kuroasi.fbx (5.7MB)

## 안흔함_김수빈

- ~/Downloads/nanachi.glb · 38.3MB · sha256 18c95c1097a4f059
    glb: 메시 16 · 노드 509 · 스킨 1 · 애니 2 [Action, Action.001] · 이미지 5
    재질 material_0→image0
    재질 Baked_All→image2/n:image4
- ~/Downloads/nanachi__made_in_abyss.glb · 12.8MB · sha256 743ff51a8835732a
    glb: 메시 26 · 노드 120 · 스킨 1 · 애니 1 [Animation] · 이미지 6
    재질 helmet→image0
    재질 backbone→-
    재질 body→image1
    재질 Material→-
    재질 material→image2
    재질 hair_back→image3
    재질 pants→image4
    재질 hair→image5

## 안흔함_김용태

- ~/.Trash/yujiro-hanma-v10.zip · 11.9MB · sha256 4b5ec7b039bafdc2
    source/Yujiro Hanma v1_0 by Omega Slender.zip (8.3MB)
    textures/Hair.png (0.5MB)
    textures/Body.png (3.0MB)
- ~/.Trash/yujiro-hanma-v10/ (풀어 둔 폴더 · 20.6MB · 파일 9개)
    textures/Hair.png (0.5MB)
    textures/Body.png (3.0MB)
    source/Yujiro Hanma v1_0 by Omega Slender.zip (8.3MB)
    source/Yujiro Hanma v1.0 by Omega Slender/Read Me!.txt (0.0MB)
    source/Yujiro Hanma v1.0 by Omega Slender/Yujiro Hanma.fbx (4.8MB)
    source/Yujiro Hanma v1.0 by Omega Slender/Yujiro Hanma.fbm/Hair.png (0.8MB)
    source/Yujiro Hanma v1.0 by Omega Slender/Yujiro Hanma.fbm/Body.png (3.0MB)
    source/Yujiro Hanma v1.0 by Omega Slender/Yujiro Hanma.fbm/Body_Normal.png (0.1MB)
    source/Yujiro Hanma v1.0 by Omega Slender/Yujiro Hanma.fbm/Hair_Normal.png (0.0MB)

## 안흔함_문필환

- ~/Downloads/naruto-shippuuden-sasuke.zip · 0.3MB · sha256 8fda115fa8f0e2d5
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Sasuke.fbx (0.3MB)
    textures/ssk_tex01.png (0.0MB)
    textures/ssk_tex02.png (0.0MB)
    textures/ssk_eye.png (0.0MB)
- ~/Downloads/naruto-shippuuden-sasuke/ (풀어 둔 폴더 · 0.3MB · 파일 4개)
    textures/ssk_eye.png (0.0MB)
    textures/ssk_tex02.png (0.0MB)
    textures/ssk_tex01.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Sasuke.fbx (0.3MB)
- ~/Downloads/naruto-shippuuden-sasuke 2/ (풀어 둔 폴더 · 0.3MB · 파일 4개)
    textures/ssk_eye.png (0.0MB)
    textures/ssk_tex02.png (0.0MB)
    textures/ssk_tex01.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Sasuke.fbx (0.3MB)

## 안흔함_박민수

- ~/Downloads/denji_and_pochita.glb · 8.5MB · sha256 f615a629404f028c
    glb: 메시 11 · 노드 390 · 스킨 2 · 애니 0 [] · 이미지 7
    재질 Outline→image0
    재질 Denji_face→image1
    재질 Denji_Body→image2
    재질 Denji_Clothes→image3
    재질 Denji_Hair→image4
    재질 Denji_shoes→image5
    재질 Pochita_Texture→image6

## 안흔함_박준희

- ~/.Trash/siren_head (1).glb · 43.2MB · sha256 7b5ec607f3ef754d
    glb: 메시 9 · 노드 73 · 스킨 1 · 애니 1 [Animation] · 이미지 8
    재질 Body→image0/n:image2
    재질 Siren→image3/n:image5
    재질 Wires→image6/n:image7
- ~/.Trash/siren_head.glb · 64.4MB · sha256 1bb3681ff7d0e3e2
    glb: 메시 9 · 노드 73 · 스킨 1 · 애니 1 [Animation] · 이미지 8
    재질 Body→image0/n:image2
    재질 Siren→image3/n:image5
    재질 Wires→image6/n:image7
- ~/.Trash/안흔함_박준희/ (풀어 둔 폴더 · 6.8MB · 파일 5개)
    textures/106_emissive.jpg (0.0MB)
    textures/106_diffuse.jpg (0.1MB)
    textures/106_normals.png (5.4MB)
    textures/mat_106_MaskMap.png (0.1MB)
    source/NPC_BM.fbx (1.2MB)
- ~/.Trash/안흔함_박준희_리깅용.obj · 1.1MB · sha256 43babfe057ccb8a2

## 안흔함_신문철

- ~/Downloads/Capoeira.fbx · 0.8MB · sha256 a50800f93297fb79
- ~/.Trash/naruto-shippuden-naruto.zip · 0.9MB · sha256 6b6eaeaf98c7c91e
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto.zip (0.8MB)
    textures/nrt_tex02.png (0.0MB)
    textures/nrt_tex01.png (0.0MB)
    textures/nrt_eye.png (0.0MB)
- ~/.Trash/naruto-shippuden-naruto/ (풀어 둔 폴더 · 4.8MB · 파일 58개)
    textures/nrt_eye.png (0.0MB)
    textures/nrt_tex01.png (0.0MB)
    textures/nrt_tex02.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto.zip (0.8MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/readme.txt (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/Naruto.obj (0.2MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/Naruto_9T.obj (0.3MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/Naruto_9T.mtl (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/Naruto.smd (1.1MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/Naruto_9T.smd (1.9MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/Naruto.mtl (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/hige.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/Thumbs.db (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/nrt_tex20.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/nrt_eye.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/rim.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/indirect_00.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/toon00.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/9bi_aura_01.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/9bi_aura_02.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0100.brres/nrt_tex10.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/hige.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/Thumbs.db (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/nrt_eye.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/rim.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/indirect_00.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/toon00.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/9bi_aura_01.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/nrt_tex01.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/9bi_aura_02.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0000.brres/nrt_tex02.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/Thumbs.db (0.1MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original03.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original02.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original00.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original01.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original05.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original11.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original10.png (0.0MB)
    source/Wii - Naruto Shippuden Clash of Ninja Revolution 3 - Naruto/Naruto_3/0001.brres/original04.png (0.0MB)
    … 외 18개
- ~/.Trash/naruto_shippuden-_naruto.glb · 0.2MB · sha256 515d27cff2e910d0
    glb: 메시 4 · 노드 6 · 스킨 0 · 애니 0 [] · 이미지 3
    재질 nrt_eye.png.001_nrt_eye.png→image0
    재질 nrt_tex01.png_nrt_tex01.png→image1
    재질 nrt_tex02.png_nrt_tex02.png→image2
    재질 nrta_tex2.png_nrt_tex02.png→image2

## 안흔함_엄태웅

- ~/Downloads/baki-hanma-v10.zip · 9.9MB · sha256 b804a94172acc890
    source/Baki v1_0 by Omega Slender.zip (6.1MB)
    textures/Hair.png (0.4MB)
    textures/Base.png (3.3MB)
- ~/Downloads/baki-hanma-v10/ (풀어 둔 폴더 · 9.9MB · 파일 3개)
    textures/Hair.png (0.4MB)
    textures/Base.png (3.3MB)
    source/Baki v1_0 by Omega Slender.zip (6.1MB)

## 안흔함_이재윤

- ~/Downloads/monkey.zip · 17.4MB · sha256 2bb3f3981a4aac78
    source/monkey.zip.zip (10.4MB)
    textures/head_u1_v1.png (0.5MB)
    textures/NormalMap3.png (1.8MB)
    textures/NormalMap2.png (2.3MB)
    textures/NormalMap.png (2.2MB)
    textures/eyes_u2_v1.png (0.0MB)
    textures/body_u1_v1.png (0.2MB)
    textures/body_u2_v1.png (0.0MB)
- ~/Downloads/monkey/ (풀어 둔 폴더 · 28.5MB · 파일 16개)
    textures/head_u1_v1.png (0.5MB)
    textures/body_u2_v1.png (0.0MB)
    textures/NormalMap2.png (2.3MB)
    textures/NormalMap.png (2.2MB)
    textures/NormalMap3.png (1.8MB)
    textures/eyes_u2_v1.png (0.0MB)
    textures/body_u1_v1.png (0.2MB)
    source/monkey.zip.zip (10.4MB)
    source/monkey.zip/head_u1_v1.png (0.5MB)
    source/monkey.zip/body_u2_v1.png (0.0MB)
    source/monkey.zip/NormalMap2.png (3.6MB)
    source/monkey.zip/NormalMap.png (2.7MB)
    source/monkey.zip/NormalMap3.png (2.8MB)
    source/monkey.zip/eyes_u2_v1.png (0.0MB)
    source/monkey.zip/body_u1_v1.png (0.2MB)
    source/monkey.zip/monkey.FBX (1.2MB)

## 안흔함_이호준

- ~/Downloads/zombi.glb · 4.8MB · sha256 07ce7c4e6c36efd8
    glb: 메시 1 · 노드 78 · 스킨 1 · 애니 2 [Armature.001|mixamo.com|Layer0, ����������������Action] · 이미지 3
    재질 material→image0/n:image2

## 안흔함_황정기

- ~/.Trash/export_6be8ddd4-566b-4ea1-8b1c-299009527c7c.fbx · 0.6MB · sha256 a30311ab311ff72c
- ~/.Trash/scp-173.zip · 1.6MB · sha256 9f7bf6bed3d3efaf
    source/173.zip (0.9MB)
    textures/173_Norm.jpeg (0.4MB)
    textures/173texture.jpeg (0.3MB)
- ~/.Trash/안흔함_황정기/ (풀어 둔 폴더 · 1.6MB · 파일 3개)
    textures/173texture.jpeg (0.3MB)
    textures/173_Norm.jpeg (0.4MB)
    source/173.zip (0.9MB)

## 특별함_노태현

- ~/Downloads/one-piece-bounty-rush-lucci-leopard-form.zip · 2.8MB · sha256 c86e9490ddab65d5
    source/pl_lucci_orig02.rar (1.5MB)
      └ pl_lucci_orig02/abnormal_pattern.png (0.0MB)
      └ pl_lucci_orig02/matcap.png (0.0MB)
      └ pl_lucci_orig02/pl_lucci_orig02.fbx (0.9MB)
      └ pl_lucci_orig02/pl_lucci_orig02_diff.png (1.2MB)
      └ pl_lucci_orig02/ramp.png (0.0MB)
      └ pl_lucci_orig02/stg_common_bayer_dither.png (0.0MB)
    textures/pl_lucci_orig02_diff.png (1.2MB)

## 특별함_박민석

- ~/Downloads/one-piece-bounty-rush-bullet.zip · 3.7MB · sha256 f0dc4b4dd4c11208
    source/pl_bullet_stam01.rar (2.3MB)
      └ pl_bullet_stam01/abnormal_pattern.png (0.0MB)
      └ pl_bullet_stam01/matcap_magenta.png (0.0MB)
      └ pl_bullet_stam01/pl_bullet_stam01.fbx (4.8MB)
      └ pl_bullet_stam01/pl_bullet_stam01_diff.png (1.4MB)
      └ pl_bullet_stam01/ramp.png (0.0MB)
      └ pl_bullet_stam01/stg_common_bayer_dither.png (0.0MB)
    textures/pl_bullet_stam01_diff.png (1.4MB)

## 특별함_양재모

- ~/Downloads/one-piece-bounty-rush-law-wano.zip · 3.9MB · sha256 e0723d38346fedae
    source/pl_law_wano01.rar (2.6MB)
      └ pl_law_wano01/abnormal_pattern.png (0.0MB)
      └ pl_law_wano01/matcap.png (0.0MB)
      └ pl_law_wano01/pl_law_wano01.fbx (8.8MB)
      └ pl_law_wano01/pl_law_wano01_diff.png (1.3MB)
      └ pl_law_wano01/ramp.png (0.0MB)
      └ pl_law_wano01/stg_common_bayer_dither.png (0.0MB)
    textures/pl_law_wano01_diff.png (1.3MB)
- ~/Downloads/one-piece-bounty-rush-law-wano/ (풀어 둔 폴더 · 3.9MB · 파일 2개)
    textures/pl_law_wano01_diff.png (1.3MB)
    source/pl_law_wano01.rar (2.6MB)

## 특별함_임장혁

- ~/Downloads/one-piece-bounty-rush-smoker-new-world.zip · 3.6MB · sha256 8504e84c17c4d077
    source/Smoker New World.rar (2.4MB)
      └ Smoker New World/pl_smoker_2yaf01_diff.png (1.3MB)
      └ Smoker New World/Smoker New World by Annettlw.fbx (7.0MB)
    textures/pl_smoker_2yaf01_diff.png (1.3MB)

## 특별함_최상호

- ~/Downloads/luffy.glb · 4.0MB · sha256 c8690976ae00157c
    glb: 메시 20 · 노드 489 · 스킨 1 · 애니 1 [blink] · 이미지 9
    재질 Pupil→-
    재질 shock→-
    재질 skin→image0
    재질 tongue→-
    재질 material→-
    재질 material_5→-
    재질 Sandals→image1
    재질 Sandals.001→-
    재질 Shorts→image2
    재질 Lower_shorts→image3
    재질 Teeth→-
    재질 Gum.001→-
    재질 24_-Straw_Hat.Hat_Hair_0.2_0_0→image4
    재질 hair→-
    재질 Straw_hat→image5
    재질 Ribbon→image6
    재질 Shirt→image6
    재질 button→image7
    재질 Eyebrows_and_scratch→image8

## 흔함_강재규

- ~/.Trash/tony-tony-chopper.zip · 4.3MB · sha256 63c48f5366bee110
    source/Chopper.zip (3.1MB)
    textures/HatBag_Roughness.jpeg (0.1MB)
    textures/Body_Metallic.jpeg (0.0MB)
    textures/Body_Mixed_AO.jpeg (0.1MB)
    textures/Body_Roughness.jpeg (0.1MB)
    textures/AntlesCloth_Base_Color.jpeg (0.2MB)
    textures/HatBag_Metallic.jpeg (0.0MB)
    textures/HatBag_Mixed_AO.jpeg (0.1MB)
    textures/HatBag_Base_Color.jpeg (0.3MB)
    textures/AntlesCloth_Roughness.jpeg (0.1MB)
    textures/AntlesCloth_Mixed_AO.jpeg (0.1MB)
    textures/AntlesCloth_Metallic.jpeg (0.0MB)
    textures/Body_Base_Color.jpeg (0.1MB)
- ~/.Trash/Chopper/ (풀어 둔 폴더 · 3.1MB · 파일 19개)
    AntlesCloth_Mixed_AO.jpg (0.1MB)
    HatBag_Base_Color.jpg (0.3MB)
    HatBag_Roughness.jpg (0.1MB)
    AntlesCloth_Metallic.jpg (0.0MB)
    HatBag_Height.jpg (0.0MB)
    HatBag_Metallic.jpg (0.0MB)
    HatBag_Mixed_AO.jpg (0.1MB)
    Body_Normal_DirectX.jpg (0.1MB)
    Body_Height.jpg (0.0MB)
    AntlesCloth_Height.jpg (0.1MB)
    Body_Roughness.jpg (0.1MB)
    AntlesCloth_Normal_DirectX.jpg (0.2MB)
    HatBag_Normal_DirectX.jpg (0.1MB)
    Chopper.FBX (1.2MB)
    Body_Mixed_AO.jpg (0.1MB)
    Body_Base_Color.jpg (0.1MB)
    AntlesCloth_Roughness.jpg (0.1MB)
    Body_Metallic.jpg (0.0MB)
    AntlesCloth_Base_Color.jpg (0.2MB)
- ~/.Trash/흔함_강재규/ (풀어 둔 폴더 · 4.3MB · 파일 13개)
    textures/AntlesCloth_Mixed_AO.jpeg (0.1MB)
    textures/Body_Metallic.jpeg (0.0MB)
    textures/AntlesCloth_Roughness.jpeg (0.1MB)
    textures/Body_Mixed_AO.jpeg (0.1MB)
    textures/AntlesCloth_Metallic.jpeg (0.0MB)
    textures/Body_Roughness.jpeg (0.1MB)
    textures/Body_Base_Color.jpeg (0.1MB)
    textures/HatBag_Base_Color.jpeg (0.3MB)
    textures/HatBag_Roughness.jpeg (0.1MB)
    textures/HatBag_Metallic.jpeg (0.0MB)
    textures/HatBag_Mixed_AO.jpeg (0.1MB)
    textures/AntlesCloth_Base_Color.jpeg (0.2MB)
    source/Chopper.zip (3.1MB)
- ~/.Trash/흔함_강재규.fbx · 1.6MB · sha256 e00461f49523dac9

## 흔함_강주혁

- ~/.Trash/one-piece-bounty-rush-ceasar.zip · 2.3MB · sha256 ee4fc9a863b5144a
    source/pl_caesar.7z (1.3MB)
    textures/pl_caesar_orig01_diff.png (1.0MB)
- ~/.Trash/one-piece-bounty-rush-ceasar/ (풀어 둔 폴더 · 2.3MB · 파일 2개)
    textures/pl_caesar_orig01_diff.png (1.0MB)
    source/pl_caesar.7z (1.3MB)
- ~/.Trash/흔함_강주혁/ (풀어 둔 폴더 · 2.3MB · 파일 2개)
    textures/pl_caesar_orig01_diff.png (1.0MB)
    source/pl_caesar.7z (1.3MB)

## 흔함_노태현

- ~/.Trash/one-piece-bounty-rush-lucci.zip · 3.4MB · sha256 de386d664535c013
    source/pl_lucci_orig01.rar (2.3MB)
      └ pl_lucci_orig01/abnormal_pattern.png (0.0MB)
      └ pl_lucci_orig01/matcap.png (0.0MB)
      └ pl_lucci_orig01/pl_lucci_orig01.fbx (8.6MB)
      └ pl_lucci_orig01/pl_lucci_orig01_diff.png (1.1MB)
      └ pl_lucci_orig01/ramp.png (0.0MB)
      └ pl_lucci_orig01/stg_common_bayer_dither.png (0.0MB)
    textures/pl_lucci_orig01_diff.png (1.1MB)
- ~/.Trash/흔함_노태현/ (풀어 둔 폴더 · 3.4MB · 파일 2개)
    textures/pl_lucci_orig01_diff.png (1.1MB)
    source/pl_lucci_orig01.rar (2.3MB)

## 흔함_문필환

- ~/.Trash/one-piece-fighting-path-koby.zip · 4.5MB · sha256 cbee441500d95876
    source/koby.rar (4.2MB)
      └ koby/12017 (merge).fbx (27.8MB)
      └ koby/12017_L.png (0.3MB)
    textures/12017_L.png (0.3MB)
- ~/.Trash/흔함_문필환/ (풀어 둔 폴더 · 4.5MB · 파일 2개)
    textures/12017_L.png (0.3MB)
    source/koby.rar (4.2MB)

## 흔함_박민석

- ~/.Trash/흔함_박민석/ (풀어 둔 폴더 · 22.0MB · 파일 3개)
    textures/32fc51d0ec804d9c9563a4cd6f1bdaa4_RGB_kuroh.png (3.6MB)
    textures/e4a8f708c2ab43a1a1e95bfa31a5ff37_RGB_kuroh.png (11.6MB)
    source/OnePiece - BlackBeard.fbx (6.9MB)

## 흔함_박민수

- ~/.Trash/one-piece-bounty-rush-blueno-cp9.zip · 2.9MB · sha256 c5562443da852a44
    source/Blueno CP9.rar (2.0MB)
      └ Blueno CP9/Blueno CP9 by Annettlw.fbx (6.8MB)
      └ Blueno CP9/pl_blueno_orig01_diff.png (0.9MB)
    textures/pl_blueno_orig01_diff.png (0.9MB)
- ~/.Trash/흔함_박민수/ (풀어 둔 폴더 · 2.9MB · 파일 2개)
    textures/pl_blueno_orig01_diff.png (0.9MB)
    source/Blueno CP9.rar (2.0MB)

## 흔함_양재모

- ~/.Trash/one-piece-bounty-rush-jabra-cp0.zip · 3.0MB · sha256 56f690781384f776
    source/Jabra CP9.rar (2.3MB)
      └ Jabra CP9/Jabra CP9 by Annettlw.fbx (8.4MB)
      └ Jabra CP9/pl_jabra_jinj01_diff.png (0.7MB)
    textures/pl_jabra_jinj01_diff.png (0.7MB)
- ~/.Trash/흔함_양재모/ (풀어 둔 폴더 · 3.0MB · 파일 2개)
    textures/pl_jabra_jinj01_diff.png (0.7MB)
    source/Jabra CP9.rar (2.3MB)

## 흔함_임장혁

- ~/.Trash/one-piece-bounty-rush-general-franky.zip · 2.5MB · sha256 c6129ce00278954f
    source/general franky.rar (1.5MB)
      └ general franky/pl_franky_punk01 (merge).fbx (3.1MB)
      └ general franky/pl_franky_punk01_diff.png (0.9MB)
      └ general franky/stg_gasha_tex_bayer_dither.png (0.0MB)
    textures/pl_franky_punk01_diff.png (0.9MB)
- ~/.Trash/흔함_임장혁/ (풀어 둔 폴더 · 3.8MB · 파일 2개)
    textures/pl_magellan_orig01_diff.png (1.4MB)
    source/pl_magellan_orig01.rar (2.5MB)
- ~/.Trash/흔함_임장혁.zip · 3.8MB · sha256 73cf14a925f902bd
    source/pl_magellan_orig01.rar (2.5MB)
      └ pl_magellan_orig01/abnormal_pattern.png (0.0MB)
      └ pl_magellan_orig01/matcap.png (0.0MB)
      └ pl_magellan_orig01/pl_magellan_orig01.fbx (7.3MB)
      └ pl_magellan_orig01/pl_magellan_orig01_diff.png (1.4MB)
      └ pl_magellan_orig01/ramp.png (0.0MB)
      └ pl_magellan_orig01/stg_common_bayer_dither.png (0.0MB)
    textures/pl_magellan_orig01_diff.png (1.4MB)

## 안흔함_상붕카(Characters)

- ~/Downloads/상붕카.glb · 3.4MB · sha256 a6358d12b679612f
    glb: 메시 32 · 노드 65 · 스킨 0 · 애니 0 [] · 이미지 0
    재질 Material.012→-
    재질 Material.003→-
    재질 Material.013→-
    재질 Material.005→-
    재질 Material.002→-
    재질 Material.010→-
    재질 Material.004→-
    재질 Material.001→-


# 스킨 원본 보관 기록 — 2026-09-15 추가분

09-14 기록과 같은 규칙이다(원본을 지우면 아래 파일명·sha256으로 다시 받아 대조한다).
유닛 폴더의 SOURCE.txt에 더 자세한 내역(리그·메시·재질)이 있다.

## 특별함_노건완 — 노가리(잉어)
- 출처: ~/Downloads/carp_fish.glb (Sketchfab, 잉어) · 4,105,436 bytes · sha256 9aa465afb18456a5b211d91ead0af9fbc3c45960102869ce6ae41234853645bd

## 특별함_최동준 — 구일톱블랙홀(걷는 남성 스캔)
- 원본(보존 — 지우지 말 것): ~/Downloads/free_download_athletic_african_man_walking_223.glb · 5011880 bytes · sha256 c4fc1f47d58987fad56439a9e7f1b7830bde3317241b749045554f780a46e917

## 특별함_강주혁 — 코알라 자리 → 아이언맨
- 출처: ~/Downloads/ironman.zip (7,831,238 bytes · sha256 c3f0f6523bb11c75e23f681a829f68d2b604dd36658d2d0e34138dcb1454abb5) 속 source/IronMan.fbx(7,831,104 bytes) — 아이언맨 Mk.III
- 🗑 이 유닛의 이전 모델(HxH 키메라 앤트 코알라, ~/Downloads/hxh__koala_chimera_ant.glb)은 2026-09-15 폐기 — AI 생성 메시가 닿은 곳마다 붙어 있어 T자·Idle에서 찢겨

## 특별함_왕승환 — 왕싱싱(강한 기사)
- 출처: ~/Downloads/strong_knight.glb (9,383,748 bytes · sha256 08663be7aab6fca5b4bebfef6f51bbdb9b1219f7746b1f60d6df878a22cefddf) — Sketchfab 「strong knight」(검은 갑옷 기사)

## 특별함_정승준 — 저승사자(류마)
- 출처: ~/Downloads/one-piece-bounty-rush-ryuma.zip (3,822,053 bytes · sha256 ca01e8c2d42201fb081ddd37a5fbbc067c4833515197a04f8ab7d12e976544b6) — 원피스 바운티러시 류마
- zip 속 source/pl_ryuma_orig01.rar (2,538,885 bytes · sha256 0ed111c7cd2a4574…, RAR5 — bsdtar로 풀림) 속 pl_ryuma_orig01/pl_ryuma_orig01.fbx (8,523,280 bytes · sha256 189e518d56ab080d…)
- + textures/pl_ryuma_orig01_diff.png(zip, sha256 69fba358…)·rar 안 pl_ryuma_orig01_diff.png(sha256 d553c3ff…) — 바이트는 다르지만 픽셀 비교 결과 RGB·알파 전부 동일(1024 RGBA). FBX가 부르는 rar 판 기준.

## 특별함_이지원 — 음지소녀(요크, 릴리스 교체)
- 출처: ~/Downloads/one-piece-bounty-rush-york.zip (3,585,410 bytes · sha256 75dfdde2dee1c89f3950aee95a6c2bfa2949801a17ab4620a59fb43af32c9eb2) — 원피스 바운티러시 요크
- zip 속 source/york.rar (2,650,624 bytes · sha256 297e41933c616698…) 속 「york/pl_york_orig01 (merge).fbx」(7,424,640 bytes · sha256 e06d44c76a556d19…)
- + textures/pl_york_orig01_diff.png(zip, sha256 fa713e5f…)·rar 안 york/pl_york_orig01_diff.png(sha256 7b2991fc…) — 바이트는 다르지만 픽셀 비교 결과 RGB·알파 전부 동일(1024 RGBA). FBX가 부르는 rar 판 기준.

## 특별함_김태영 — 일베조무사(찰로스)
- 출처: ~/Downloads/one-piece-bounty-rush-charlos.zip (3,577,424 bytes · sha256 318224ca4363c8dae72b65391138482c468b148701ed39fb98a451a8a73ef583) — 원피스 바운티러시 찰로스 성
- zip 속 source/charlos.rar (2,678,807 bytes · sha256 949f9e0aed99ff11…) 속 「charlos/pl_charlos_orig01 (merge).fbx」(9,753,760 bytes · sha256 0ea350012506a0ce…)
- + textures/pl_charlos_orig01_diff.png(zip, sha256 655750a2…)·rar 안 charlos/pl_charlos_orig01_diff.png(sha256 6fdc656b…) — 바이트는 다르지만 픽셀 비교 결과 RGB·알파 전부 동일(1024 RGBA). FBX가 부르는 rar 판 기준.

## 특별함_송형성 — 틱장애(아디오)
- 출처: ~/Downloads/one-piece-bounty-rush-adio.zip (4,516,684 bytes · sha256 313c259c03e4de3a901cfa705db5bd96c0982897c46f9e1723e895178fb110db) — 원피스 바운티러시 아디오
- zip 속 source/pl_adio_orig01.rar (3,432,107 bytes · sha256 b8550f86f4fe3543…) 속 pl_adio_orig01/pl_adio_orig01.fbx (9,490,080 bytes · sha256 39f3c64ce6d7a7ed…)
- + textures/pl_adio_orig01_diff.png(zip, sha256 93dde3cd…)·rar 안 pl_adio_orig01_diff.png(1,335,384 bytes · sha256 1924ee9b…) — 바이트는 다르지만 픽셀 비교 결과 RGB·알파 전부 동일(1024 RGBA).

## 특별함_최준우 — 원조렝가(베르고 피규어)
- 출처: ~/Downloads/onepiece_figure_vergo.glb(원피스 피규어 베르고, Sketchfab, 원작 배포처 미상 — 사장님 다운로드분)
- 원본 파일: onepiece_figure_vergo.glb · 3,704,312 bytes(3.53MB) · sha256 35fb4c4e20f7ea6cdb1754c095b3b1053563042a8602dc27ccdb6c324ba52e1a

## 특별함_김정래 — 프로그래머(노트북)
- ~/Downloads/laptop.glb (457,076B)
- sha256: 026f04e3fad173f88066fdd67cf56ac8d9dda6951da00aa629243ac0254269a0



## 2026-09-16 — 원본 보관 위치 변경 (사장님 지시)

다운로드 원본을 **`~/Desktop/구랜디스킨모음/<등급폴더>/<유닛이름>.<확장자>`**로 옮기고 이름을 유닛 이름으로 바꿨다.
등급 폴더(01_흔함·02_안흔함·03_특별함 …)는 사장님이 만들어 둔 구조이고 그 폴더의 README.md가 규칙을 적어 둔다.

| 규칙 | 예 |
|---|---|
| 모델 한 개 | `03_특별함/특별함_왕승환.glb` |
| zip과 푼 폴더 둘 다 | `03_특별함/특별함_박기찬.zip` + `03_특별함/특별함_박기찬/` |
| Mixamo 업로드본·참고 사진 | `03_특별함/특별함_최동준_mixamo업로드.fbx` · `…_원본자세_정면.png` |
| 교체돼 안 쓰는 원본 | `99_폐기_교체된원본/특별함_이지원_이전_릴리스.zip` · `…/특별함_강주혁_이전_코알라.glb` |

스크립트(`fix_unit_fbx.py`·`gen_skin_rig.py`·`gen_scan_rig.py`·`gen_animal_skin.py`·`gen_prop_unit.py`·`Tools/fix_glb_rig_for_humanoid.py`)의
source·archive 경로도 이 위치로 바꿨다(`SKINS` 상수). 이미 지워져 없는 옛 항목은 `DL`(~/Downloads)에 남겨 뒀고 `rev=`(git 커밋)로 되살린다.
앞으로 새 스킨 원본도 ~/Downloads가 아니라 이 모음집에 같은 규칙으로 둔다.
