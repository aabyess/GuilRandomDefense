# 아트 에셋 조사 (2026-09-01, 리서치담당)

> 목표 스타일: **워크래프트3 느낌** — 밝고 채도 높은 색, hand-painted 텍스처, 로우폴리+텍스처로 디테일.
> 검색 방향: stylized / hand-painted / low poly fantasy / RTS. realistic·photoscan 계열 제외.
> 환경: Unity 6(6000.0.82f1) URP, macOS Apple Silicon, 1인 개발.
> 표기: **[확인]** = 페이지/문서 직접 열람 · URL 있음 / **[추정]** = 근거는 있으나 직접 대조 못함 / **[미확인]** = 못 찾음
> 이 문서는 3개 fork(마켓플레이스 / 물+자체제작 / 유닛 전략) 조사를 리서치담당이 종합한 것.

## 결론 먼저

**무료 조합 (Quaternius CC0 생태계)을 추천한다.**

- **환경**: Quaternius *Medieval Village MegaKit* + *Fantasy Props MegaKit* (건물·소품 500개 이상, CC0)
- **유닛 234종**: Quaternius *Universal Base Characters*(베이스 6종) + *Modular Character Outfits – Fantasy*(파츠) + *Ultimate Animated Character Pack*(공용 애니메이션) 조합 + 등급별 `MaterialPropertyBlock` 색 틴트
- **지형 텍스처·물 셰이더**: 이미 검증된 **자체 제작(PIL 절차적 생성 + 단순 Shader Graph)** 유지 — 이 부분은 재조사 결과도 바꿀 이유를 못 찾음

이걸 "무료 조합"으로 고른 이유는 아래 결론 섹션에.

---

## 1. 어디서 구하나 (마켓플레이스별)

### Unity Asset Store

| 에셋명 | 종류 | URP | 라이선스 | 가격 | 태그 |
|---|---|---|---|---|---|
| [RTS Pack Constructor (Human vs Orc)](https://assetstore.unity.com/packages/3d/environments/fantasy/rts-pack-cunstructor-low-poly-buildings-characters-props-human-v-301611) | 환경+캐릭터+소품 | 지원 | Standard EULA | $5.99 | [확인] |
| [RTS Pack - Stylized Buildings, Props, Ships](https://assetstore.unity.com/packages/3d/environments/fantasy/rts-pack-stylized-buildings-props-ships-set-237270) | 환경 | 지원 (Built-in/URP/HDRP) | Standard EULA | $20 | [확인] |
| [Stylized Fantasy Enemy NPC Bundle](https://assetstore.unity.com/packages/3d/characters/stylized-fantasy-enemy-npc-bundle-184700) | 몬스터 캐릭터 | 지원 | Standard EULA | $149.99 | [확인] |
| [KayKit Adventurers (Unity판)](https://assetstore.unity.com/packages/3d/characters/humanoids/humans/kaykit-adventurers-character-pack-for-unity-290679) | 캐릭터 4종 | 지원 | CC0(원본) + Standard EULA(스토어판) | $11.99 | [확인] |

**주의**: Unity Asset Store 표준 EULA는 에셋 자체 재판매만 금지하고, 완성 게임에 포함해 배포·수익화하는 건 일반적으로 허용된다. 단 이번 조사에서 EULA 원문 전체를 대조하지는 않았음 — 실제 구매 전 확인 권장 [추정].

### Kenney (kenney.nl)

[Fantasy Town Kit](https://kenney.nl/assets/fantasy-town-kit) — 건물/타일 약 160개, **CC0**, 무료(기부 선택) [확인]. Kenney 전체가 CC0라 라이선스 리스크는 최저지만, "플랫 채색 로우폴리"에 가까워 워크3의 hand-painted 느낌과는 결이 다소 다름.

### Fab (구 Quixel/Unreal 마켓플레이스)

Unity 프로젝트도 지원한다고 공식 발표됐고 "Fab Standard License"가 엔진 무관하게 적용된다는 것까지는 확인했지만 [추정], 페이지 접근이 403으로 막혀 **실제 Unity 임포트 방식은 미확인**. Quixel Megascans 계열은 사실적 스캔 소재 위주라 애초에 우리 방향(hand-painted)과 안 맞을 가능성이 높음.

### itch.io — Quaternius 생태계 (핵심 후보)

| 에셋명 | 종류 | 라이선스 | 가격 |
|---|---|---|---|
| [Universal Base Characters](https://quaternius.itch.io/universal-base-characters) | 캐릭터 베이스 6종(남/녀×3체형) | CC0 | 무료 |
| [Modular Character Outfits – Fantasy](https://quaternius.com/packs/modularcharacteroutfitsfantasy.html) | 캐릭터 파츠(의상) | CC0 | 무료 |
| [Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter.html) | 공용 애니메이션 50+ | CC0 | 무료 |
| [Medieval Village MegaKit](https://quaternius.itch.io/medieval-village-megakit) | 건물 300+ | CC0 | 무료 |
| [Fantasy Props MegaKit](https://quaternius.itch.io/fantasy-props-megakit) | 소품 200+ | CC0 | 무료 |
| [KayKit Adventurers (원본)](https://kaylousberg.itch.io/kaykit-adventurers) | 캐릭터 4종+애니 | CC0 | 무료(자율결제) |

[확인] 전부 CC0 — 저작자 표시 불필요, 상업적 사용·재배포 전부 허용. 이미 `PROJECT_BRIEF.md`에 후보로 있던 "Ultimate Fantasy RTS"(애니메이션 없음이라 보류됐던 것)의 약점을, 같은 Quaternius 생태계의 다른 팩(베이스+파츠+애니 조합)이 메워준다.

### Sketchfab

개별 모델 단위로 CC-Attribution이 대부분이라(저작자 표시 필요, 조건이 모델마다 다름) 234종 규모에는 부적합. "특정 보스 1종만 눈에 띄게" 같은 소량 용도로만 적합 [추정].

### Poly Haven

이번 조사에서 별도 확인 못함 [미확인]. HDRI·사실적 PBR 소재가 강점이라 방향이 다를 가능성이 높아 우선순위를 낮게 판단하고 스킵함.

---

## 2. 물 셰이더는 따로

**Unity 6 내장 Water System은 URP에서 못 쓴다** — 공식 Water System은 **HDRP 전용**이다 [확인].
출처: https://digitalproduction.com/2025/04/28/unity-6-1-surfing-waves-and-shading-smarter/ , https://discussions.unity.com/t/water-system-for-hdrp-not-urp/918838

URP용 대안으로 Unity 공식 오픈소스 데모 [Boat Attack Water](https://github.com/Unity-Technologies/BoatAttack)가 있으나 "공식 지원 없음" 경고가 있고 Unity 6 호환 여부·정확한 라이선스가 [미확인]. 유료 stylized water 셰이더 에셋도 다수 존재하나 대부분 반사·굴절·Gerstner wave 등 3인칭/저각 카메라를 전제로 설계됨.

**톱다운 시점에는 이런 고급 기능이 거의 안 보인다** — 반사·굴절은 카메라가 수면과 낮은 각도를 이룰 때 두드러지는데 탑다운은 그 반대다 [추정, 렌더링 원리 기반]. 워크3 자체도 물을 텍스처 스크롤+약한 웨이브 노멀 정도로 단순 처리했다 [추정, 직접 문서 대조는 못함].

→ **결론: 유료 물 에셋은 불필요.** 이미 있는 사인파 텍스처(PIL) + URP Shader Graph의 단순 UV스크롤+노멀 셰이더 조합이면 톱다운 시점엔 충분하다.

---

## 3. 자체 제작 선택지

| 영역 | 판단 | 근거 |
|---|---|---|
| 지형 텍스처(잔디/물/바위/흙) | **이미 검증됨, 계속 자체 제작** | `Tools/generate_map_textures.py`가 실전에서 "쓸 만하다"는 평가를 이미 받음 |
| 물 셰이더 | **자체 제작 권장** | 위 2번 결론 — 단순 셰이더로 충분, 며칠 내 구현 가능 [추정] |
| 정적 환경 오브젝트(건물/바위/소품) | 무료 팩이 유리 | ProBuilder로 직접 만들 수는 있지만[확인: Unity 공식 패키지, 로우폴리에 적합] 섬 19개를 채우려면 며칠~1~2주 규모로 추정되는 반면, Quaternius MegaKit 받으면 며칠 내 배치 가능 [추정] |
| **유닛 234종 캐릭터 모델** | **자체 제작 부적합** | ProBuilder로 캐릭터를 처음부터 만드는 워크플로는 비주류로 보이고(검색에서 사례를 못 찾음, Blender 없이는 사실상 불가) [추정], 234종을 손으로 만드는 건 1인 개발 시간 예산상 비현실적 |

**셰이더만으로 되는 부분**: Toon/Cel-shading은 URP Shader Graph로 표준적으로 구현 가능하고 [확인: https://www.wayline.io/blog/unity-toon-shader-tutorial-2023 , 오픈소스 예시 https://github.com/Delt06/urp-toon-shader ] 로우폴리 모델에 씌우면 "손그림 느낌"에 상당히 가까워진다. 다만 이건 **라이팅 처리**만 해결하고, hand-painted 특유의 "칠해진 디테일"(이끼 자국 등)은 여전히 텍스처가 있어야 한다 [추정].

**시간 비교**: 정확한 벤치마크 자료는 못 찾음 [미확인]. 정성적으로는, 지형/물처럼 이미 자체 제작이 검증된 영역은 계속 자체 제작이 유리하고, 정적 오브젝트·특히 유닛 모델링은 1인 개발 시간 대비 무료 에셋 조합이 압도적으로 유리해 보인다.

---

## 4. 유닛 234종을 어떻게 할 것인가

| 접근법 | 개발 시간 | 234종 확장성 | 워크3 적합도 | 시각적 다양성 |
|---|---|---|---|---|
| 1. 단일 메시 + 색/크기 변주 | 낮음 | 매우 좋음 | 좋음 | 낮음~중간 (실루엣 동일) |
| 2. 2D 스프라이트 빌보드 | 중간 (234장 조달 필요) | 좋음 | 애매함 (3D 로우폴리 정체성과 어긋남) | 높음 |
| **3. 모듈러 파츠 조합** | 중간 (초기 파이프라인 구축) | 매우 좋음 | 좋음 | 높음 (파츠 조합으로 실루엣도 다름) |
| 4. 기타(AI 텍스처 등) | 불명 | 불명 | 불명 | 불명 (사례 못 찾음) |

- **방법 1**: `MaterialPropertyBlock`으로 동일 메시에 색만 다르게 입히는 건 표준 기법이고 GPU 인스턴싱과도 궁합이 좋다 [확인: https://docs.unity3d.com/6000.5/Documentation/ScriptReference/MaterialPropertyBlock.html]. 다만 지금 상태(색 큐브)에서 "색 다른 사람 모델"로 바뀌는 정도라 체감 개선이 제한적일 수 있음.
- **방법 2**: 빌보드는 버텍스 셰이더로 대량 렌더링이 가능하고 [확인: https://gamedevbeginner.com/billboards-in-unity-and-how-to-make-your-own/], 실제로 3D RTS에서 보병만 스프라이트로 처리한 사례("WarGames")가 있다 [확인: https://retrostylegames.com/blog/3d-games-with-2d-sprites/]. 다만 234장의 개별 일러스트 조달이 3D 모델링보다 반드시 빠르다는 보장이 없고, 워크3의 3D 로우폴리 정체성과 어긋날 위험이 있음.
- **방법 3**: Synty `POLYGON | Modular Fantasy Hero Characters`가 720개 모듈 파츠 + 커스텀 색 변경 셰이더를 제공하는 등[확인: https://syntystore.com/products/polygon-modular-fantasy-hero-characters] 유료 시장에 성숙한 선례가 있다(가격대 $9.99~$349.99, 구독 월 $30~). 그런데 **같은 구조(베이스+파츠+공용 애니메이션)를 Quaternius가 전부 CC0 무료로 제공**한다(위 1번 섹션) — 유료 Synty를 살 필요 없이 같은 전략을 무료로 구현할 수 있다는 뜻.
- **반례 참고**: Arknights(상업 가챠 타워디펜스)는 오퍼레이터마다 전부 별도 아트를 쓴다 [확인, 간접: https://naavik.co/deep-dives/arknights-tower-defense-redefined/] — 다만 상업 스튜디오 규모라 1인 개발 234종 전제와 다름, 반대 사례로 참고만.

**추천: 3번(모듈러 파츠 조합) + 1번(등급별 색 틴트)의 하이브리드.** 파츠 조합으로 실루엣 다양성을 확보하고, 그 위에 등급별 색 틴트를 얹어 "같은 파츠 조합이라도 등급이 다르면 색이 다르다"는 식으로 234종을 감당한다. 실행 수단은 유료 Synty가 아니라 **무료 Quaternius 조합**으로 충분하다.

---

## 종합 결론

**무료 조합 (에셋 구매 아님, 순수 자체 제작도 아님)을 추천한다.**

이유:
1. **환경과 유닛 양쪽 다 Quaternius 생태계 하나로 커버된다** — 마켓플레이스 fork가 찾은 "구매 후보"(KayKit $11.99, Synty $9.99~$349.99)는 완성도는 높지만, 완전히 동일한 전략(베이스+파츠+애니메이션 조합)을 **CC0 무료로 대체 가능**하다는 게 이번 조사의 핵심 교차 확인 지점이다. 굳이 돈을 쓸 이유가 약하다.
2. **자체 제작은 이미 검증된 영역(지형 텍스처, 물 셰이더)에서는 계속 유지**한다 — 이건 재조사로도 바뀔 이유가 없었다. 하지만 유닛 234종의 캐릭터 모델링은 자체 제작이 명백히 부적합(ProBuilder로 안 됨, Blender+시간이 필요)하다.
3. **라이선스 리스크가 가장 낮다** — CC0는 저작자 표시도 필요 없고 상업적 재배포까지 다 허용되어, 나중에 배포·수익화할 가능성까지 고려해도 안전하다. Asset Store EULA는 대체로 허용적이지만 원문 전체 대조를 안 해서 확신도가 낮고, Synty는 라이선스 원문을 반드시 구매 전 확인해야 하는 채로 남아있다.

다만 **한 가지 예외**: 워크3풍 hand-painted "느낌"을 더 강하게 원하면(Quaternius는 플랫 채색에 가까움) KayKit Adventurers($11.99) 정도의 저가 유료 에셋을 소수 간판 캐릭터(보스 등)에만 섞는 절충도 있다. 이건 전면 구매가 아니라 "무료 조합 기반 + 일부 유료 포인트 강화"로, 위 결론과 상충하지 않는다.

## 확인 못한 항목 (`[미확인]`)

- Fab의 실제 Unity 임포트 방식 (페이지 403으로 접근 실패)
- Boat Attack Water의 정확한 라이선스, Unity 6(6000.0.82f1) 호환 여부
- Synty 라이선스 원문 (상업적 재배포 허용 범위 — 구매 검토 시 반드시 재확인 필요)
- Poly Haven 워크3풍 소재 존재 여부 (조사 안 함)
- 자체 제작 vs 구매의 정량적 시간 비교 데이터 (벤치마크 자료 없음, 정성적 추정만)
- ProBuilder로 캐릭터를 처음부터 만든 실제 사례 유무 (못 찾음 — "안 된다"가 아니라 "비주류"라는 추정)
