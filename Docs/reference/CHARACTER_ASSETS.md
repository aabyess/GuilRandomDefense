# 캐릭터 스킨 조사 — 애니풍 (2026-09-01, 리서치담당)

> 사용자 원문: "유니티는 캐릭터 스킨 어디서 무료로 찾을 수 있는지 조사좀 해달라고해 주로 애니캐릭터 스킨을 넣을거임 괴물이나"
> `Docs/reference/ART_ASSETS.md`(워크3풍 hand-painted 판타지, Quaternius CC0 추천)를 **대체하지 않는다.** 캐릭터 부분만 다시 본 것 — 아래 "ART_ASSETS.md와의 관계" 참고.
> 환경: Unity 6(6000.0.82f1), URP, macOS Apple Silicon, 1인 개발.
> 표기: **[확인]** = 페이지 직접 열람·URL 있음 / **[추정]** = 근거는 있으나 직접 대조 못함 / **[미확인]** = 못 찾음
> 3개 fork(아군 캐릭터 / 몬스터 / VRM+툰셰이더) 조사를 종합. **이후 사용자가 IP 제약을 해제**(비공개 repo·비상업·지인 대상 개인 플레이)함에 따라 2개 fork(MMD / VRM·Booth·Sketchfab IP)로 IP 캐릭터 경로를 추가 조사해 "## 4. IP 캐릭터 경로" 섹션에 통합함.

## ⚠️ 저작권 — 제약이 풀렸다 (2026-09-01, 사용자 확인)

> "나는 어짜피 개인적으로 친구들이랑 할거라 ip문제는 상관없지않아? 이거 상업적으로 할것도아니고 깃에 올라간것도 private인데" — 사용자 확인

**비공개 저장소·비상업·지인 대상 개인 플레이**라는 전제로, 원피스·원펀맨·주술회전 등 실제 IP 캐릭터 모델도 후보에 포함한다. 아래 표에는 `IP`/`오리지널` 열을 넣었다 — **배제하기 위해서가 아니라, 나중에 공개 전환을 고려할 때 뭘 갈아끼워야 하는지 표시하기 위함**이다. 원작 IP 자체의 저작권은 개별 모델 제작자의 "재배포 허가"와는 별개 문제이지만, 비공개·비상업 개인 사용이라는 전제하에서는 실무적 리스크가 낮다고 판단된다 — 다만 이건 법률 자문이 아니다 [추정].

기준도 바뀌었다: "상업적으로 쓸 수 있는가"보다 **"받아서 Unity에 넣을 수 있는가"**(포맷·리그·애니메이션 유무, 다운로드 접근성)가 우선이다.

또한 조사 중 `unityassetcollection.com` 같은 사이트가 유료 에셋을 무단으로 무료 재배포하고 있는 걸 발견했다 [확인]. 사이트 자체가 "학습/테스트 목적만, 상업적 사용 금지"라고 명시하면서도 원 제작자 허가 근거가 없다 — **이런 사이트에서 받은 에셋은 이 정책 변경과 무관하게 쓰면 안 된다** (이건 우리 쪽 라이선스 문제가 아니라 애초에 배포 자체가 불법이라는 별개 사안).

## 결론 먼저

**환경은 기존 `ART_ASSETS.md`(Quaternius CC0)를 그대로 유지.** 이번 조사는 캐릭터에만 영향을 준다.

**몬스터(적 75라운드+보스 9종)**: 기존 추천과 **자연스럽게 이어진다.** Quaternius **Ultimate Monsters Pack**(50종, CC0, 애니메이션 포함)이 같은 생태계라 스타일이 통일된다. 여기에 무료 URP 툰 셰이더를 얹으면 "애니풍" 방향에 맞출 수 있다.

**아군 유닛 234종**: 여기가 기존 문서와 갈리는 지점이다. **진짜 anime 스타일의 CC0 무료 모듈러 캐릭터 세트는 못 찾았다.** 1차안으로 추천하는 건:

1. **기본 전략 유지**: `ART_ASSETS.md`가 추천한 Quaternius 베이스+파츠+애니메이션 조합(CC0, 이미 검증됨)을 그대로 쓰고, 그 위에 **무료 URP 툰 셰이더**(Anime Cel Shader URP 또는 OToon)를 얹어서 "애니 느낌"을 낸다. 모델링을 바꾸지 않고 셰이딩만 바꾸는 거라 234종 전체에 비용 없이 일괄 적용 가능.
2. **소수 예외**: 보스나 스토리 관련 "간판 캐릭터"처럼 진짜 anime 얼굴/비율이 필요한 소수에는 유료 모듈러 애니 팩($15~40, 아래 표)이나 VRoid Studio 수작업을 선택적으로 쓴다. 234종 전체를 이걸로 채우는 건 시간·비용상 비현실적이다.

즉 **"무료 조합 + 셰이더로 스타일 전환" 이 주력, 유료/VRoid는 보조.** 아래에 근거를 정리한다.

**(추가) IP 캐릭터 경로 — 사용자가 IP 제약을 해제한 뒤 추가 조사**: 원피스·주술회전 등 실제 IP 캐릭터 모델(MMD, Sketchfab, VRoid Hub, Booth)이 실제로 존재하고 Unity로 가져올 수 있다는 것까지 확인했다. 하지만 **확보되는 건 루피·조로·고죠 사토루 같은 주인공급 유명 캐릭터뿐**이라 234종 전체를 커버하지 못한다 — 위 1·2번 전략(무료 조합+툰셰이더가 주력, 소수 예외는 유료/VRoid)의 "소수 예외" 자리에 **IP 모델도 후보로 추가**되는 것이지, 234종 문제 자체를 해결하진 못한다. 상세는 "## 4. IP 캐릭터 경로" 참고.

---

## 1. 아군 유닛 234종 — 애니풍 휴머노이드

### CC0 무료 모듈러 세트는 못 찾았다

Quaternius(판타지)에 대응하는 "무료 CC0 애니풍 베이스+파츠+애니메이션 세트"는 **[미확인]** — 검색된 모듈러 애니 캐릭터 팩은 전부 유료였다. Synty Studios도 anime 전용 라인은 없다("SIMPLE People"은 카툰이지 애니풍 아님) [확인].

### 유료 후보 (검증 필요)

| 에셋명 | 가격 | 라이선스 | URP | 애니메이션 | 모듈러 | URL | 태그 |
|---|---|---|---|---|---|---|---|
| BoZo: Modular/Stylized Anime Characters | $40 | Standard EULA | 지원(Built-in/URP, HDRP 미지원) | 미확인 | 파츠 다수(정확한 개수 스니펫마다 다름) | https://assetstore.unity.com/packages/3d/characters/humanoids/humans/bozo-modular-anime-characters-base-pack-323550 | [확인, 애니메이션은 [미확인] |
| Modular Stylized Character 1 | $40 | Standard EULA | 지원(Built-in/URP/HDRP) | 미확인 | 헤어14/의상다수(추정) | https://assetstore.unity.com/packages/3d/characters/humanoids/humans/modular-stylized-character-1-255279 | [확인, 애니메이션은 [미확인] |
| Customizable Anime Character 3D | $15 | Standard EULA | 지원(Built-in/URP/HDRP) | 미확인 | 모듈러(상세 미확인) | https://assetstore.unity.com/packages/3d/characters/customizable-anime-character-3d-239978 | [확인, 애니메이션은 [미확인] |

**세 후보 다 페이지에서 애니메이션(대기/이동/공격/사망) 포함 여부를 확인하지 못했다** [미확인] — 실제 검토 시 Package Content를 직접 열어 재확인 필요. 없으면 humanoid 리그인지도 확인해서 Mixamo 리타게팅 가능성을 봐야 한다.

### VRM/VRoid 경로 — "가능하지만 234종 1차 수단으로는 부담"

- **VRoid Studio는 무료**이고 [확인], 만든 모델은 상업적 이용이 기본 허용된다(이미지/영상 수익화, 게임 사용, 모델 데이터 판매까지) [확인: https://vroid.com/en/studio/guidelines ]. 단 "VRoid 메시·텍스처를 변형/재조합하는 애플리케이션"은 별도 라이선스가 필요하다는 조항이 있음 — **아트 에셋 소스로만 쓰면 문제없고, 게임 내에서 파츠를 실시간 재조합하는 커스터마이저를 만들려는 경우에만 해당**.
- **VRoid Hub의 기존 아바타**는 모델마다 라이선스가 제각각(CC0~CC BY) [확인] — 234종 규모로 쓰려면 하나하나 확인해야 해서 비효율적. VRoid Studio로 직접 만드는 게 라이선스 확인 부담이 적다.
- **UniVRM + Unity 6 URP**: 작동은 하지만 "바로 되는" 수준은 아니다. MToon10 아웃라인 렌더 패스가 Unity 6의 RenderGraph와 충돌하는 알려진 이슈가 있고, 해결책은 URP 설정에서 **Compatibility Mode**를 켜는 것 [확인: https://github.com/vrm-c/UniVRM/issues/2713 ]. 서드파티 URP MToon 포트도 여럿 있음(라이선스는 저장소별 재확인 필요).
- **234종을 VRoid로 빠르게 대량 생성하는 공식 기능은 확인하지 못했다** [미확인] — 프리셋을 사람이 수동으로 조합하는 구조로 보인다. 캐릭터 1개당 정확한 소요 시간 자료도 없음. → **VRoid는 소수 간판 캐릭터용으로 강하고, 234종 전체의 1차 수단으로는 시간 비용이 부담될 가능성이 크다** (정량 근거 없는 fork의 판단).
- 애니메이션은 걱정 없음: VRM은 humanoid 리그라 **Mixamo 리타게팅이 확실히 된다** [확인: FBX 내보내기 → Mixamo 업로드 → "FBX for Unity" 다운로드 → Unity Humanoid 리그 설정].

---

## 2. 몬스터(적 75라운드 + 보스 9종)

### 잡몹용 — Quaternius Ultimate Monsters Pack이 최유력

| 에셋명 | 종수 | 라이선스 | 애니메이션 | 가격 | 스타일 | URL | 태그 |
|---|---|---|---|---|---|---|---|
| **Quaternius Ultimate Monsters Pack** | **50종** | **CC0** | 풀 애니메이션(공격/사망/이동) | 무료 | 로우폴리 stylized | https://quaternius.com/packs/ultimatemonsters.html | [확인] |
| Quaternius Cute Animated Monsters Pack | 21종 | CC0 | 개별 애니메이션 | 무료 | 로우폴리 cute | https://quaternius.com/packs/cutemonsters.html | [추정] |
| Monsters Pack – Stylized (by Ake) | 미상 | Standard EULA | O | $5 | Cute/Cartoon | https://assetstore.unity.com/packages/3d/characters/creatures/monsters-pack-stylized-asset-pack-by-ake-309971 | [확인] |

`ART_ASSETS.md`가 이미 추천한 Quaternius 생태계와 **같은 출처**라 유닛(만약 Quaternius로 갈 경우)과 스타일이 통일된다.

### 보스 9종용 — 단품은 빈약, Quaternius 승격이 현실적

| 에셋명 | 라이선스 | 가격 | URL | 태그 |
|---|---|---|---|---|
| Monster Boss (x.A-Studio.x) | Standard EULA | $45 | https://assetstore.unity.com/packages/3d/characters/creatures/monster-boss-295453 | [확인] |

9종을 각각 $45짜리로 채우는 건 예산상 비현실적. **Quaternius Ultimate Monsters Pack 중 덩치 크고 개성 있는 종을 크기·색으로 승격시켜 보스로 쓰는 게 현실적**이다 [추정, 판단].

### 진짜 "애니메이션풍" cel-shaded 몬스터는 못 찾음

이번 조사에서 눈이 크고 윤곽선이 뚜렷한 진짜 애니풍 몬스터 무료 팩은 **[미확인]**. 대안: 로우폴리 stylized(Quaternius) + 툰 셰이더 조합으로 애니 느낌에 근접시키는 것 (아래 3번 참고).

---

## 3. URP 툰 셰이딩 — 문제 없음, 무료 해법 다수

URP에는 툰 셰이더가 기본으로 없지만 해법이 여럿이다:

1. **무료 에셋**: "Anime Cel Shader URP"(Neko Legends, 무료, 아웃라인 포함) [확인: https://assetstore.unity.com/packages/vfx/shaders/anime-cel-shader-urp-259864 ], "OToon"(무료 uber 셰이더) [확인]
2. **Shader Graph로 직접 제작**: cel-shading(계단식 명암)+아웃라인 튜토리얼이 다수 존재, 잘 닦인 길로 보임 [확인, 예시: https://www.youtube.com/watch?v=76zt0DD8CLg ]
3. **MToon URP 포트**: VRM 캐릭터를 쓸 경우 MToon(VRM 표준 셰이더)의 URP 포트를 그대로 쓰는 게 자연스럽다 (2번 섹션과 연결)

→ **어떤 모델을 쓰든 무료로 애니/툰 느낌을 낼 수 있다.** 이게 "몬스터·유닛 모델은 기존 Quaternius(CC0)를 유지하고 셰이더만 바꾸자"는 결론의 핵심 근거다.

---

## 4. IP 캐릭터 경로 (제약 해제 후 추가 조사)

> 사용자가 IP 제약을 해제한 뒤(비공개·비상업·지인 대상 개인 플레이) MMD 생태계 + VRM/Booth/VRoid Hub/Sketchfab을 추가로 조사했다. 아래 표의 `IP` 항목은 원작 IP 캐릭터를 본뜬 팬메이드/상업 모델이다 — **배제용 표시가 아니라, 나중에 공개 전환 시 교체 대상을 표시하기 위함**이다.

### 결론: 존재는 하지만 234종을 못 채운다

IP 캐릭터 모델은 실제로 여러 경로에 풍부하게 존재하고 Unity로 가져올 수도 있다. 다만 **확보되는 건 전부 주인공/인기 캐릭터 위주**(루피·조로·나미·상디·프랑키·니코로빈·고죠 사토루 등)다. 우리 유닛 234종 중 다수는 조연이거나 여러 캐릭터를 조합한 커스텀 이름(예: "무면허 라이더")인데, 이런 이름에 정확히 대응하는 팬메이드 모델은 존재할 가능성이 낮다 [추정 — 대표 검색어만 확인, 234종 전수조사는 이번 범위 밖]. **→ 234종의 1차 해법이 아니라, 소수 간판 캐릭터(주인공급 유닛, 보스)에 한해 쓸 수 있는 보조 경로다.**

### IP별 존재 여부

| IP | 존재 여부 | 근거 |
|---|---|---|
| 원피스(루피/조로/나미/상디/프랑키/니코로빈 등) | **풍부함** [확인] | MMD(DeviantArt), Sketchfab(무료+유료), VRoid Hub "One Piece" 태그 페이지 전부에서 확인 |
| 주술회전(고죠 사토루) | **일부 존재** [확인] | Booth VRChat 아바타, VRoid Hub에서 확인. 다만 원피스만큼 풍부하진 않음 |
| 원펀맨(사이타마 등) | **[미확인]** | 이번 검색 범위에서 못 찾음 — 없다는 뜻은 아니고 시도가 제한적 |

전반적 경향: MMD·팬아트 커뮤니티는 인기·장수 프랜차이즈일수록 모델이 많다 — 원피스처럼 오래된 인기작은 확보가 쉽고, 상대적으로 덜 알려진 작품/캐릭터는 케이스별로 다르다.

### 경로별 비교

| 경로 | 대표 사례 | 포맷 | Unity 임포트 난이도 | 애니메이션 | 라이선스 | IP/오리지널 |
|---|---|---|---|---|---|---|
| **MMD (DeviantArt/bowlroll)** | 루피, 조로 등 원피스 모델 다수 [확인] | .pmx/.pmd | 중간 — Blender+mmd_tools로 FBX 변환 필요(권장 경로, 아래 설명) | 없음, Mixamo 리타게팅 필요(리그 자동매핑 여부 [미확인]) | 모델마다 제각각, Read Me 확인 필수. 흔히 재배포 금지+크레딧 표기 | IP |
| **Sketchfab 무료 다운로드** | Nami, Luffy 등 (Cheap3D, kishi 등 제작) [확인] | FBX/glTF | **쉬움** — 바로 임포트 | 대부분 없음(T포즈) | 모델마다 다름(CC-Attribution 다수), 재확인 필요 [미확인] | IP |
| **Sketchfab 유료(Store)** | Nico Robin, Franky (GremorySaiyan) [확인] | FBX | 쉬움 | 미확인 | "Royalty Free" 구매 라이선스로 추정 [추정] | IP |
| **VRoid Hub** | "One Piece" 태그: 루피/조로/나미, 주술회전 토게 인우마키 등 [확인] | VRM | 중간 — UniVRM + Unity 6 Compatibility Mode 필요(기존 조사) | 없음, Mixamo 리타게팅 가능(humanoid 리그, 기존 조사에서 확인) | **모델마다 개별 지정** — 페이지 403으로 상세 확인 못함, 다운로드 전 필수 재확인 [미확인] | IP |
| **Booth (.unitypackage)** | Satomi Gojo 등 VRChat 아바타 [확인] | Unity 패키지 | **가장 쉬움** — 드래그앤드롭 | VRChat용 표정/제스처는 있으나 게임용 공격/사망 모션은 별도 필요할 가능성 [추정] | **개인 사용만, 재배포/재판매/이식 전부 금지**가 흔함. $200대로 비쌈 [확인] | IP |
| CGTrader/TurboSquid | "Luffy Gear 4"(Animated+Rigged) 등 | FBX/OBJ/STL | 쉬움 | 일부 있음("Animated" 태그) | Royalty Free 흔함, 개별 재확인 필요 [추정] | IP |

### Unity 임포트 — MMD는 Blender 경유가 현실적

MMD 모델을 Unity로 가져오는 방법은 두 가지다:
- **MMD4Mecanim**(무료 플러그인): 최신 업데이트가 2020년에 멈춘 것으로 보여 **Unity 6 호환이 불확실** [추정]
- **Blender + mmd_tools 애드온**: 무료·오픈소스, 활발히 유지보수됨. PMX 임포트 → FBX 내보내기 → Unity. 단점은 MMD 원본 셰이더가 안 넘어온다는 것인데, **어차피 URP 툰 셰이더를 새로 씌울 계획이라 이 단점이 문제가 안 된다** [확인/판단]

→ **Blender 경유가 더 현실적**이라고 판단했다. macOS Apple Silicon에서 Blender는 네이티브로 잘 동작한다.

### 라이선스 — "재배포 금지"와 "개인 플레이"의 경계

MMD·Sketchfab·VRoid Hub 모델 대부분이 "재배포 금지"를 명시한다. 이건 **제3자에게 다시 뿌리는 것**을 금지하는 것이지, 우리가 받아서 우리 게임에 넣어 지인들과 플레이하는 것과는 성격이 다르다고 판단되나 — **이건 법률 자문이 아니고 회색지대로 남겨둔다** [추정]. Booth .unitypackage처럼 "재배포/이식 전부 금지"가 명시적으로 강한 경우는, **git(비공개 repo라도)에 원본 에셋을 커밋하지 않고 로컬에만 두는 방식이 더 안전**해 보인다 [판단].

### 확인 못한 항목 (IP 경로)

- 원펀맨 등 일부 IP의 모델 존재 여부 (검색 시도 제한적)
- MMD 리그가 Unity Humanoid로 자동 매핑되는지 (본 구조가 다르면 수동 작업 필요할 수 있음)
- VRoid Hub·Sketchfab 무료 모델들의 정확한 라이선스 문구 (페이지 접근 실패로 다수 미확인 — 다운로드 전 개별 재확인 필수)
- 우리 게임의 개별 유닛명(다수 커스텀/조연 조합) 각각에 대응하는 모델이 실제로 있는지 (전수조사 안 함)

---

## `ART_ASSETS.md`와의 관계 — 무엇이 바뀌고 무엇이 유지되는가

| 영역 | ART_ASSETS.md 기존 추천 | 이번 조사 후 |
|---|---|---|
| 맵/환경(건물·소품) | Quaternius CC0 (변경 없음) | **유지** |
| 지형 텍스처·물 셰이더 | 자체 제작(PIL+Shader Graph) | **유지** |
| 몬스터(적+보스) | Quaternius 생태계 일부로 암묵 포함 | **유지, 명시적으로 Ultimate Monsters Pack 지정** + 애니 방향으로 툰 셰이더 추가 |
| 유닛 234종 | Quaternius 베이스+파츠+애니(판타지 스타일) | **모델은 유지 가능, 스타일 방향이 "애니풍"으로 바뀌어서 셰이더를 툰/셀셰이딩으로 교체해야 함.** 진짜 anime 룩이 필요하면 소수 캐릭터에 한해 유료 애니 팩·VRoid·**IP 모델(원피스 등, 제약 해제됨)**을 추가로 검토 |
| 주인공급 소수 유닛(루피 등 이름이 곧 IP 원본인 경우) | (해당 없음 — 이전엔 배제) | **IP 모델을 그대로 쓸 수 있다** (MMD/Sketchfab/VRoid Hub 등, "## 4. IP 캐릭터 경로" 참고). 다만 234종 전체 해법은 아님 |

즉 **큰 파이프라인(Quaternius CC0 + 모듈러 조합)은 안 바뀌고, 렌더링 스타일(셰이더)이 hand-painted 판타지 룩에서 셀셰이딩 애니 룩으로 바뀐다**는 게 두 문서를 합친 최종 그림이다.

---

## 확인 못한 항목 (`[미확인]`)

- 유료 애니 캐릭터 팩 3종의 애니메이션(대기/이동/공격/사망) 포함 여부 — 구매 전 재확인 필요
- VRoid Studio macOS Apple Silicon 네이티브 지원 여부 (공식 페이지 403으로 접근 불가)
- VRoid 캐릭터 1개 제작 소요 시간의 정량 수치, 대량 자동 생성 도구 존재 여부
- 서드파티 URP MToon 포트들의 정확한 라이선스·유지보수 상태
- Quaternius 몬스터 팩들의 URP 호환 명시 여부 (FBX 원본이라 될 것으로 추정만)
- (IP 경로 추가조사분) 원펀맨 등 일부 IP의 모델 존재 여부, MMD 리그의 Unity Humanoid 자동매핑 여부, VRoid Hub/Sketchfab 무료 모델의 정확한 라이선스 문구, 개별 유닛명 234종에 대응하는 IP 모델 존재 여부 — 상세는 "## 4. IP 캐릭터 경로" 하단 참고

---

## 받을 목록 (2026-09-01 추가 — 직접 링크)

> 사용자가 CC-BY(저작자 표시)까지 허용하기로 확정 — 게임 시작 전 크레딧 화면에 저작자를 표기한다. **못 쓰는 건 (1) 불법 재배포 사이트, (2) 유료를 안 사고 가져오는 것뿐.**
> 표기: **[확인]** = 해당 페이지를 WebFetch로 직접 열어 검증함 / **[검색]** = WebSearch 결과 스니펫 기준(페이지 직접 열람은 안 함, 링크 자체는 실재하는 검색 결과) / **[미확인]** = 못 찾음
> **링크는 전부 검색·열람으로 실재를 확인한 것만 실었다. 지어낸 링크는 없다.**

### 1. 잡몹·보스 (최우선)

| 항목 | 링크 | 라이선스 | 포맷 | 애니메이션 | 용도 | 태그 |
|---|---|---|---|---|---|---|
| Quaternius Ultimate Monsters Pack | https://quaternius.com/packs/ultimatemonsters.html | CC0 (저작자 표시 불필요) | FBX, OBJ, Blend, glTF | **있음** — "50 fully animated monsters" | 잡몹 50종 | [확인] |
| Quaternius LowPoly Animated Monsters | https://quaternius.itch.io/lowpoly-animated-monsters | CC0 | OBJ, FBX, Blend | **있음** — "punch, attack, jump, flying, walk" | 잡몹 보조 세트 | [확인] |
| Monster Boss (x.A-Studio.x) | https://assetstore.unity.com/packages/3d/characters/creatures/monster-boss-295453 | Standard Unity EULA | 미확인(Unity 패키지) | 있음("Animated" 태그) | 보스 단품 1종, $45 | [확인 — 기존 조사 재확인, 이번엔 재열람 안 함] |

**보스 9종 전체는 위 Ultimate Monsters Pack 안에서 덩치 크거나 개성 있는 종을 크기/색으로 승격시키는 걸 권장** — 팩 내 개별 몬스터 목록/썸네일은 페이지에서 직접 확인 가능(다운로드 페이지 자체에 미리보기가 있음).

### 2. 툰 셰이더

| 항목 | 링크 | 라이선스 | Unity/URP 호환 | 용도 | 태그 |
|---|---|---|---|---|---|
| Anime Cel Shader URP (Neko Legends) | https://assetstore.unity.com/packages/vfx/shaders/anime-cel-shader-urp-259864 | Standard Unity EULA, **무료** | URP 호환 명시, 2025-09-24 최종 업데이트, 원본 Unity 6000.2.5 | 아군/몬스터 공용 셀셰이딩 | [확인] — **1순위 추천** |
| OToon - URP Toon Shading | https://assetstore.unity.com/packages/package/id/216102 | Standard EULA, 원가 $39.99 | ⚠️ **Deprecated — 페이지 직접 열람으로 확인함.** "no longer available for purchase, no longer supported by the publisher" | — | [확인] — **정정: 이전 조사에서 "무료 툰 셰이더"로 잘못 기록됐다. 실제로는 지금 신규 획득이 불가능한 죽은 상품이다. 쓰지 말 것.** |
| Toon Shader Free (백업 후보) | https://assetstore.unity.com/packages/vfx/shaders/toon-shader-free-21288 | Standard EULA, 무료 | 정상 존재하나 원본이 Unity 4.5.3(2019년) 기준 — URP 명시 없음, Unity 6 호환 미확인 | 백업, URP 동작은 실제 임포트 후 확인 필요 | [확인, 존재만 — 호환은 미확인] |
| URP Toon Shader (Delt06, GitHub) | https://github.com/Delt06/urp-toon-shader | **MIT 라이선스** (완전 자유, 저작자 표시도 법적 의무 아님) | Unity 2021.3.0f1 LTS + URP 12.1.6 기준 개발. **유지보수는 중단 상태**(개발자가 후속 프로젝트 "Toon RP"로 이동) — Unity 6 공식 검증은 안 됨 | 대안/백업용 | [확인] |

→ **1순위 추천: Anime Cel Shader URP.** 무료+URP 명시+최근 업데이트(2025-09)로 조건을 다 만족하는 유일한 후보. **OToon은 목록에서 빼는 게 아니라 "확인 결과 죽은 상품"이라고 명시한다** — 링크 자체는 실재하고 직접 열람으로 deprecated 상태를 검증했다.

### 3. 아군 유닛 베이스 (Quaternius 3종 세트)

| 항목 | 링크 | 라이선스 | 포맷 | 구성 | 태그 |
|---|---|---|---|---|---|
| Universal Base Characters | https://quaternius.itch.io/universal-base-characters (다운로드 버튼: `/purchase` 경로로 연결) | CC0 | FBX/OBJ/Blend(표준), 소스판(600MB)엔 Blender/UE/Unity/Godot 프로젝트 별도 포함 | 베이스 6종(남/녀 × Superhero/Regular/Teen 체형), 헤어스타일 20종, 피부·눈 색 커스터마이즈 | [확인] |
| Modular Character Outfits – Fantasy | https://quaternius.com/packs/modularcharacteroutfitsfantasy.html | CC0 | FBX, OBJ, Blend, glTF | 의상 12종/파츠 62개, 의상마다 색 텍스처 3종. **Universal Base Character 헤드와 호환 명시**, Humanoid 리그라 다른 베이스에도 재타게팅 가능 | [확인] |
| Ultimate Animated Character Pack | https://quaternius.com/packs/ultimatedanimatedcharacter.html | CC0 | FBX, OBJ, Blend | 캐릭터 52종 + "many animations"(구체 목록은 페이지에 없음 — 다운로드해서 확인 필요) | [확인] |

3종 다 CC0라 저작자 표시 자체가 불필요하다(표시해도 무방하지만 의무 아님). **Universal Base Characters + Modular Outfits는 호환성이 페이지에 명시**돼 있어 조합 리스크가 낮다. Ultimate Animated Pack의 애니메이션이 이 베이스와 같은 리그를 쓰는지는 페이지에 명시가 없어 **다운로드 후 직접 확인이 필요하다** [미확인].

### 4. IP 캐릭터 (사장님이 직접 고르시도록 후보 다수)

> 전부 팬메이드/2차창작 모델이다. 라이선스가 명시 안 된 것도 많다 — 그런 경우 "저작자 표시 필수로 간주하고 크레딧에 넣는" 게 안전하다 [판단]. **T포즈만 있고 애니메이션이 없는 경우가 대부분** — Mixamo 리타게팅 전제로 봐야 한다.

| 유닛명 | 원작 | 후보 링크 | 라이선스 | 포맷 | 태그 |
|---|---|---|---|---|---|
| 무면허 라이더 | 원펀맨 — **멈맨 라이더(Mumen Rider)의 한국어 번역명**, 캐릭터명 자체가 "무면허(라이선스 없는) 라이더" | 게임용 리그드 모델을 못 찾음 — 검색된 건 전부 **3D프린트용 피규어(STL)**: https://cults3d.com/en/3d-model/art/mumen-rider-ciclista-sin-licencia-one-punch-man , CGTrader 유료 프린트 모델 다수 | 프린트 모델마다 다름 | STL(게임용 아님) | **[검색, 게임 임포트 부적합]** — 리그·애니메이션 없는 조각상용 모델뿐. 이 캐릭터는 IP 경로보다 오리지널/Quaternius 베이스+커스텀이 현실적 |
| 브로리 | 드래곤볼 슈퍼 (극장판) | https://sketchfab.com/3d-models/broly-super-saiyan-9c482f44944348acb9e048da00f7db27 (CC-BY로 검색됨) / https://sketchfab.com/3d-models/broly-dragon-ball-z-sagas-bced4ed9e8c44c07aa6575ad6e668a49 (CC-BY, GameCube 게임 추출 모델) | **CC-BY** (검색 결과에 명시) — 저작자명은 각 페이지에서 재확인 필요 | Sketchfab 표준(다운로드 시 FBX/glTF 선택 가능) | [검색] — 직접 열람으로 저작자명 재확인 필요 |
| 고죠 사토루 | 주술회전 | https://sketchfab.com/3d-models/satoru-gojo-1cf90882c2e64074ab62d766ad77d6c4 | **CC-BY 4.0** (직접 열람 확인) — **저작자: Godfrey (SteamySenpai / godfreywilliams)** | .blend/.fbx/.obj/.mtl | **[확인]** — 리그 포함(T포즈, Blender pose mode로 조정 가능). "재업로드·재배포 금지" 별도 명시 있음(우리끼리 게임에 넣는 것과는 무관하다고 판단되나 회색지대) |
| 나나미 치아키 | **단간론파** (사장님 확인 2026-09-01 — 주술회전 나나미 켄토가 아니다) | https://sketchfab.com/3d-models/nanami-jujutsu-kaisen-rigged-sculptrun-da1c5b019e9c420b880de1cfd26b757f (제목에 "Rigged" 명시) | 미확인(페이지 직접 열람 안 함) | 미확인 | [검색] — 리그 있다는 것만 제목으로 확인, 라이선스는 재확인 필요 |
| 모리야 스와코 | 동방 프로젝트 | https://sketchfab.com/3d-models/suwako-moriya-9182554555534b06a3b257d754744511 | 미확인 | 미확인 | [검색] — 동방은 원래 2D 게임이라 3D 모델은 전부 2차창작. 상대적으로 후보가 적음(모자 소품, 피규어 등 부분 모델이 더 많이 나옴) |
| 한마 유지로 | 그래플러 바키 | https://sketchfab.com/3d-models/yujiro-hanma-v10-6739950ee3e94a42ad1252bc4f5a049b | **CC-BY로 추정, 단 "비상업적 용도만" 명시** — 크레딧: **Omega Slender**, "격투의 북두의 권 레전드 리바이브 게임 모델을 수정한 것"이라는 서술 | 미확인 | [검색] — 정적 모델로 보임(리그/애니메이션 여부 미확인) |
| 호시노 루비 | 최애의 아이 | https://hub.vroid.com/en/characters/4094744293042894568/models/1052211098702716463 (VRoid Hub) / https://sketchfab.com/3d-models/oshi-no-ko-ruby-hoshino-3d-model-fv-dl-9d9c913ad2bd4cf692aa064167865494 (Sketchfab, "XPS/PMX/FBX/Blender/VRChat 포맷 다양하게 제공"이라는 서술) | 미확인(개별 재확인 필요) | VRM(Hub) 또는 FBX 등 다수(Sketchfab) | [검색] — 최근작이지만 팬층이 두터워 후보가 예상보다 많음. VRM 경로는 기존 조사(UniVRM+Compatibility Mode)와 연결됨 |
| 김건부 | **애니메이션 캐릭터가 아니다** — League of Legends 프로게이머(정글러 "캐니언", 본명 김건부)의 실명 | 없음 | — | — | **⚠️ [확인] 3D 모델 소스 자체가 존재하지 않는다** — 이건 실존 인물이라 애니풍 3D 모델을 찾는 게 원천적으로 잘못된 방향이다. **PM/사용자에게 확인 필요**: 이 유닛명이 의도한 게 맞는지, 아니면 다른 캐릭터를 의도했는데 이름이 잘못 들어간 건지 |
| 올마이트 | 마이 히어로 아카데미아 | https://sketchfab.com/3d-models/all-might-the-number-one-hero-ab3d819d719745a0a0bde8b9de05daa0 / https://sketchfab.com/3d-models/all-might-hero-bb66cb7daf0f4d4dbc2442dae1e43289 | 미확인 | Sketchfab 표준 | [검색] — 후보 다수, MMD판(DeviantArt, SAB64 제작)도 있음: https://www.deviantart.com/sab64/art/MMD-Model-All-Might-Download-787247965 |

### 받을 목록 관련 주의

1. **위 `[검색]` 태그 항목은 링크 자체는 실재하지만, 라이선스·정확한 저작자명·포맷 상세는 다운로드 직전에 해당 페이지를 직접 열어 재확인해야 한다.** 특히 CC-BY 표기를 크레딧 화면에 넣으려면 정확한 저작자명이 필요하다.
2. **김건부는 3D 모델을 찾을 수 있는 대상이 아니다** — 실존 인물이다. 사용자 확인 필요.
3. **무면허 라이더는 게임용 리그드 모델이 없다** — 3D프린트 피규어뿐이다. Quaternius 베이스로 자체 제작하는 게 현실적이다.
4. Sketchfab에서 "Download Free"라고 표시돼도 실제로는 모델별로 라이선스가 CC0부터 CC-BY, 심지어 비상업 한정까지 갈리므로, **다운로드 페이지의 라이선스 섹션을 반드시 확인**해야 한다.
5. (기존 조사 재확인) 진짜 cel-shaded 애니풍 무료 몬스터 팩은 여전히 못 찾았다 — 로우폴리(Quaternius)+툰셰이더 조합이 현재로선 유일한 대안이다.
6. Unity Asset Store 표준 EULA 원문 전체 대조는 이번에도 안 했다 — 재판매 금지 외 세부 조항은 구매 전 재확인 권장(`ART_ASSETS.md`와 동일한 주의사항).

---

## 5. 2차 조사 (2026-09-02) — 리깅 팩 확장 / 툰 셰이더 실동작 검증 / VRM 실전 문제

> 나루토 1종을 Sketchfab → Mixamo(대기·이동·공격 3클립, Humanoid 공유) → Unity로 넣는 데 성공한 뒤의 후속 조사. 3개 fork(리깅 팩 / 툰 셰이더 실동작 / VRM 실전 문제)를 종합.

### 5-1. 리깅된 캐릭터 모델 팩 — Quaternius가 기존 파악보다 훨씬 풍부하다

기존에 확인한 Quaternius 3종(Universal Base Characters 6종 / Modular Outfits-Fantasy / Ultimate Animated Character Pack 52종) 외에 **5개 팩을 추가로 발견**했다 — 전부 CC0, quaternius.com:

| 팩 | 개수 | Humanoid 리그 | 링크 | 태그 |
|---|---|---|---|---|
| Ultimate Modular Men Pack | 11종×4교체파츠, 애니 24개 | 명시 없음 | https://quaternius.com/packs/ultimatemodularcharacters.html | [확인] |
| Ultimate Modular Women Pack | 10종×4교체파츠, 애니 24개 | **있음(명시)** | https://quaternius.com/packs/ultimatemodularwomen.html | [확인] |
| RPG Character Pack | 6종 | 명시 없음 | https://quaternius.com/packs/rpgcharacters.html | [확인] |
| Animated Men Pack | 4종 | 명시 없음 | https://quaternius.com/packs/animatedmen.html | [확인] |
| Animated Women Pack | 4종 | 명시 없음 | https://quaternius.com/packs/animatedwomen.html | [확인] |

**순수 모델 수 합산(중복 제외) 약 93종+** — 여기에 Modular Men/Women의 파츠 교체 조합까지 고려하면 **234종에 상당히 근접**한다 [추정, 파츠 교차 호환은 직접 확인 필요]. Quaternius가 별도로 "Universal Animation Library"(120+ 애니메이션, 공용 Humanoid 리그, Unity 호환)를 제공한다는 것도 확인했다 [확인, 검색 요약 — 페이지 직접 열람은 안 함: https://quaternius.itch.io/universal-animation-library] — PM이 이미 구축한 Mixamo 클립 공유 구조와 같은 원리라 호환 가능성이 높다 [추정].

**리스크**: 새로 찾은 5팩 중 Ultimate Modular Women Pack만 Humanoid 리그가 페이지에 명시돼 있다. 나머지 4팩은 리그 종류가 안 적혀 있어 **다운로드 후 직접 확인이 필요**하다.

Mixamo 기본 캐릭터(X/Y Bot, 정확한 총 개수 미확인), Kenney(Character Assets 번들이 페이지에 "currently unavailable" — 다운로드 가능 여부 불확실), Unity Asset Store 무료 팩(Creative Characters FREE, Character Pack Free Sample — Humanoid+URP 확인되나 종수가 4~11개뿐)은 전부 종수가 너무 적어 **보조 수단일 뿐**이다.

### 5-2. 애니풍 툰 셰이더 — Unity 6 실동작 근거 재검증

| 후보 | 가격 | Unity 6 실동작 근거 | 아웃라인 | 태그 |
|---|---|---|---|---|
| **Anime Cel Shader URP** (Neko Legends) | 무료 | **강함** — 페이지 호환성 표에 "Unity 6000.2.5f1 / URP Compatible" 명시, 최신 릴리스 2025-09(v3.1.3) | 자체 지원, 색상·두께·거리감쇠까지 커스터마이즈 | [확인] — **1순위 유지** |
| Sketchy Toon Shader URP (Funflower) | $4.99 | **강함** — "Unity 6000.0.32f1 / URP Compatible" 명시, 업데이트가 3개 중 가장 최근(2025-12) | 손그림풍 아웃라인 지원 | [확인] — 2순위 백업 |
| Toon Shaders Pro (danielilett) | 유료(가격 미확인) | 약함 — "Unity 2022.3 이상"이라고만, Unity 6 특정 버전 명시 없음 | 가장 유연 — 4가지 아웃라인 알고리즘(인버티드 헐 포함) | [확인] — 근거 약해 후순위 |

**⚠️ 새로 발견한 함정**: Anime Cel Shader URP v3는 **Unity 6에서 Project Settings > Graphics > URP의 "Compatibility Mode"(Render Graph 비활성화)가 필요할 수 있다**는 서술이 검색에서 일관되게 나온다 [추정 — 페이지 원문 직접 재확인은 못함]. 이건 VRM/UniVRM(아래 5-3)에서도 나온 것과 **같은 종류의 Unity 6 RenderGraph 함정**이다 — 셰이더를 적용하기 전에 이 설정부터 확인해야 한다.

**234종 일괄 적용**은 구조적으로 문제없다 — 셰이더 1개 + 머티리얼 인스턴스 다수(색만 다르게)로 쓰는 게 URP 표준 워크플로다 [추정, URP 일반 원리 기반 — 특정 셰이더 문서로 100% 확인한 건 아님].

`unityassetcollection.com`이 "Anime Cel Shader URP 무료 다운로드"로 이번에도 검색에 걸렸다 — **여전히 불법 재배포 사이트다, 쓰지 말 것** (Anime Cel Shader URP 자체가 이미 무료라 이 사이트를 거칠 이유도 없다).

### 5-3. VRoid/VRM 경로 — "임포트는 더 쉽지만 셰이더 계통이 갈린다"

- MToon10 아웃라인 버그(#2527)는 **이미 고쳐졌다**(PR #2584) [확인]. 하지만 **"Unity 6 이후 URP에 맞춰 MToon을 근본적으로 재설계"하는 이슈(#2713)는 2025-08-21에 열려 지금도 진행 중**이다 [확인] — 즉 개별 버그는 고쳐져도 구조적 불안정은 남아있다.
- **새로 발견한 구체 사례**: UniVRM 0.131.0 + Unity 6.4에서 "머티리얼/텍스처 추출" 버튼이 에셋 임포트 제약과 충돌해 깨진 사례가 실제로 있었다(#2783, 이후 패치됨) [확인]. **Unity 6.x 마이너 버전이 올라갈 때마다 UniVRM이 한 박자 늦게 따라잡는 패턴**이 확인된다. 우리 프로젝트는 6000.0.82f1이라 이 특정 버전엔 해당 안 되지만, 패턴 자체가 리스크다.
- 임포트 절차 자체는 VRM이 **오히려 더 쉽다** — .vrm을 드래그하면 UniVRM이 자동으로 Humanoid 리그로 변환해준다(FBX는 수동 설정 필요).
- **결정적 문제**: VRM은 MToon이라는 **별도 셰이더 계통**이 자동으로 붙는다. 우리는 이미 Anime Cel Shader URP로 234종을 통일하기로 방향을 잡았는데, MToon을 URP에서 안정적으로 쓰려면 프로젝트 전역 Compatibility Mode를 켜거나(다른 렌더링에 영향), MToon을 걷어내고 우리 셰이더로 재적용해야 한다(사실상 FBX 경로와 같은 작업량).

**판단**: 나루토 경로(FBX+Mixamo+커스텀 툰셰이더) 대비 **VRM 경로는 지금은 권장하지 않는다.** FBX/glTF로도 구할 수 있는 캐릭터는 그쪽을 우선하고, **VRM으로만 존재하는 캐릭터에 한해서만** 예외적으로 쓰되 MToon은 걷어내고 Anime Cel Shader URP로 재적용하는 걸 권장한다.

### 리서치담당의 추천 (PM 요청)

**234종을 채우는 가장 현실적인 경로: Quaternius CC0 팩 8종(93종+, 파츠 조합 포함시 234종에 근접) + Anime Cel Shader URP(무료) + Mixamo 3클립 공유(이미 검증됨).** 리그가 불명확한 4팩(Modular Men/RPG/Animated Men/Animated Women)만 받아서 Humanoid 여부를 먼저 확인하고, 안 되는 것만 나루토처럼 개별 Mixamo 리타게팅으로 보완하면 된다. VRM/VRoid는 주력이 아니라 "그 캐릭터가 VRM으로만 존재할 때"의 예외 경로로 남겨둔다.
