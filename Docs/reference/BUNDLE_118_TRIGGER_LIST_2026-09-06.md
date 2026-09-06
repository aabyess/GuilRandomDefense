# 118개 번들 SkillData의 원작 트리거명 목록 — 리서치담당 기계추적용 (2026-09-06)

**요청 배경**: PM 지시 — 02번(스킬 밀도) ①②확인 중 발견한 "다중효과, 전부
`chance:1`(항상 같이 발동)" 118개 파일에 대해 리서치담당이 JASS 원문으로
배타분기(ⓑ)/진짜 별개(ⓐ) 기계추적을 건다. 이 문서는 그 대상 **트리거명 목록**이다.

이미 확정되어 수정 완료한 2건(`Nami_Skill_2`, `Tasigi_03`)은 목록에서 **제외**했다 —
`RRD_SEQUENCE_DUPLICATE_AUDIT.md`가 이미 판정했고, `APPROXIMATION_LEDGER.md` §10에
수정 내역이 있다.

## ⚠️ 넘기기 전에 — 발견한 유형 구분 (섞으면 안 된다)

이 118개를 판별하다가 **최소 3종의 서로 다른 "다중효과" 원인**을 발견했다. 트리거명
목록은 아래 ①유형만 포함한다 — ②③은 **다른 파일 포맷**이라 이 요청 범위 밖이다.
나중에 그쪽을 볼 때 아래 구분을 그대로 물려줘야 헷갈리지 않는다.

1. **RRD순번 형제** (이 문서의 118개 전부 이 유형) — 설명이
   `게이트별 배정...능력 N개를 이 SkillData 하나로 묶었다` + `· 트리거명#순번: 계산식`
   bullet 형태. 하나의 원작 트리거 아래 RRD순번이 여러 개 — **오늘 이미 확인된 두
   사례(Nami_Skill_2=배타분기, Tasigi_03=코드중복)처럼 사례마다 원인이 다를 수 있다.**
   기계추적 대상.

2. **더미채널 다목적지** (이 문서에 없음, 별도 90개 파일 그룹 중 일부) — 설명이
   `Trig_X → eYYY(더미) → AZZZZ: 값` 형태. 한 트리거가 서로 다른 AXXXX 더미로 여러
   경로를 갖는다. 배타인지 별개인지 이번 조사에서도 미판정 — **이 목록에 안 실었다.**

3. **버프게이트 조건부 2차효과** (이 문서에 없음, 예: `SkillData_원작006_H096.asset`) —
   RRD순번이 아니라 애초에 서로 다른 발동조건(예: 대상이 특정 버프 보유 시만)을 가진
   **원래부터 별개인 효과**다. **이건 절대 "1슬롯으로 접으면" 안 된다** — 접으면
   진짜 콘텐츠가 사라진다. 이 문서의 118개와 섞어서 기계추적하면 안 된다.

## 대상 파일 수 / 고유 트리거명 수

```
파일 118개 → 고유 (유닛,트리거명) 126개  (한 파일에 트리거가 여러 개인 경우 있음 —
                                          예: 카마도 탄지로 ff8bed43.asset 1파일에
                                          Ryougi_Skill_3/_double/_triple 3개 트리거)
```

## 목록 (파일 / 유닛ID / 유닛이름 / 트리거명 / 원본 bullet 수)

이 표는 전체 118행을 그대로 담기엔 길어 TSV로 별첨한다(같은 디렉터리에 없으면
요청 시 재생성 가능 — 생성 스크립트는 아래 "재생성 방법" 참고). 여기엔 **고유
트리거명 126개**만 알파벳순으로 나열한다 — 리서치담당이 `Trig_<트리거명>_Actions`로
바로 찾을 수 있게 트리거명 원문 그대로다.

### 고유 트리거명 126개

```
Ain_Skill_1  Ain_Skill_2  Akainu_02  Akainu_02_hidden
BronyaMotar_E  BronyaMotar_R  BronyaMotar_laser  Bronya_Skill_1
Brook_Skill_1  Bugi_SommonDamage2  Byakuya_R  Cavendish_skill_Mana
DP_AttackDamage  DP_AttackDamagegaksung  DP_Skill_2  DP_Skill_3
Ed_Skill_1  Ed_Skill_1_Item  En_nomal  Enel_skill_2
Franky_Skill_Mana1  Franky_misiile_re  Gaban_Skill_1  Gaban_Skill_mana
Garp_AttackDamage  Higma_Q  Higma_W  Huji01
Huji_03  Jimbe  Jimbe_Mu  Kata_03
King_skill_2  King_skill_2_tr  Kizaru_01_shoot  LaillySkill1
Law_skill_5  Legend13_2  Legend17_Marcodamage  Legend19_0
Legend19_1  Legend26_tichi_crows  Legend31_toki  Legend32_neko1
Legend34Moria_Skill  Legend35ulti_trg  Legend51  Legend_han_1
Luchi_Skill_3_6kinggun  Marco_S1  Marco_S2  Minato_E
Minato_Mana  Minato_Q2  Nami_Skill_2  Nika_AttackDamage
Odeng_Skill  Odeng_Skill_mana  Rebeca_Skill_1  Rebeca_Skill_3
Red_skill_1  Robine_skill_2  Robine_skill_3  Rokugu_skill_1
Ruffy_AttackDamage  Ruffy_Mana  Ryougi_Shiki_2  Ryougi_Shiki_R2
Ryougi_Skill_3  Ryougi_Skill_3_double  Ryougi_Skill_3_triple  Sabo_Mana
Sandi_skill_1  Sandi_skill_Mana2  Sengoku_Skill_Item  Shiki_Lion
Shiki_SKill_item  Shiki_champa2  Sinobu_Skill_4  Sinobu_Skill_hp2
Snake_2  Snake_3_Kingkobra  Tasigi_02  Tasigi_03
Tatsumaki_T_Explosion_by_AZ_Attack  Tich1_danil  Tichi_skill_1  Tichi_skill_2_tr
Transpom_AceMana  Transpom_Ace_bulRemake  Transpom_dp  Usop_Skill_2
Usop_Skill_Mana  Uta_ArmyS  Uta_skill_3_mana  Yamato_Attack_Damage2
Yukari_R2  Z_skill_3  Zoro_Samchun2  Zoro_enfor_3dragon
Zoro_raven  Zoro_samchun1  ace_skill_3  ace_skill_5
arrow_h2  byakuya_Chan  byakuya_Q  carrot_skill_2
cro_skill_2  deken_Mana  deken_skill_1  hancock_skill_10
hancock_skill_9  katakuri_Skill_2  kikoyou_hp  kikoyou_mana
kikoyou_skill_1  kikoyou_skill_2  koalla_skill_1  koalla_skill_Mana
roger_Mana  roger_Skill_2  tahsigi  tots_R
vivi_Skill_4_Mana  vivi_skill_2
```

### 파일별 상세 (118행)

| 파일 | 유닛ID | 유닛이름 | 트리거명 | bullet수 |
|---|---|---|---|---:|
| SkillData_게이트_랜덤_모몬가_ff8bed43.asset | h09K | 옌 - 랜덤전용 | En_nomal | 1 |
| SkillData_게이트_랜덤_미도리야_이즈쿠_079f80a5.asset | h0AC | 마운틴.D.히그마 - 랜덤전용[제한됨] | Higma_W | 1 |
| SkillData_게이트_랜덤_미도리야_이즈쿠_2c7de21c.asset | h0AC | 마운틴.D.히그마 - 랜덤전용[제한됨] | Higma_Q | 2 |
| SkillData_게이트_랜덤_손오공_948ad1b7.asset | h0A6 | 야쿠모 유카리 - 랜덤전용[제한됨] | Yukari_R2 | 1 |
| SkillData_게이트_랜덤_이민형_4266b6e3.asset | h0BC | 타츠마키 - 랜덤전용[제한됨] | Tatsumaki_T_Explosion_by_AZ_Attack | 1 |
| SkillData_게이트_랜덤_이즈미_신이치_351a0f00.asset | h0BF | 키쿄우 - 랜덤전용[제한됨] | kikoyou_hp | 2 |
| SkillData_게이트_랜덤_이즈미_신이치_41677101.asset | h0BF | 키쿄우 - 랜덤전용[제한됨] | kikoyou_skill_1 | 4 |
| SkillData_게이트_랜덤_이즈미_신이치_61743e88.asset | h0BF | 키쿄우 - 랜덤전용[제한됨] | kikoyou_mana | 2 |
| SkillData_게이트_랜덤_이즈미_신이치_d3498c23.asset | h0BF | 키쿄우 - 랜덤전용[제한됨] | kikoyou_skill_2 | 1 |
| SkillData_게이트_랜덤_이타도리_유지_18aa2343.asset | h09W | 나미카제 미나토 - 랜덤전용[제한됨] | Minato_Q2 | 1 |
| SkillData_게이트_랜덤_이타도리_유지_84bf5e76.asset | h09W | 나미카제 미나토 - 랜덤전용[제한됨] | Minato_E | 2 |
| SkillData_게이트_랜덤_이타도리_유지_a8343962.asset | h09W | 나미카제 미나토 - 랜덤전용[제한됨] | Minato_Mana | 1 |
| SkillData_게이트_랜덤_주호페이크_3a13fd3d.asset | h09L | 이치의 율자 - 랜덤전용 | Bronya_Skill_1 | 2 |
| SkillData_게이트_랜덤_카마도_탄지로_3c5f8bd9.asset | h0A2 | 료우기 시키 - 랜덤전용[제한됨] | Ryougi_Shiki_R2 | 3 |
| SkillData_게이트_랜덤_카마도_탄지로_d370bb23.asset | h0A2 | 료우기 시키 - 랜덤전용[제한됨] | Ryougi_Shiki_2 | 5 |
| SkillData_게이트_랜덤_카마도_탄지로_ff8bed43.asset | h0A2 | 료우기 시키 - 랜덤전용[제한됨] | Ryougi_Skill_3, Ryougi_Skill_3_double, Ryougi_Skill_3_triple | 12 |
| SkillData_게이트_랜덤_한마_바키_355f5d39.asset | h08I | 쿠치키 뱌쿠야 - 랜덤전용[제한됨] | Byakuya_R, byakuya_Chan | 3 |
| SkillData_게이트_랜덤_한마_바키_9ffa9c45.asset | h08I | 쿠치키 뱌쿠야 - 랜덤전용[제한됨] | byakuya_Q | 1 |
| SkillData_게이트_랜덤_호시노_아이_33bd8aa4.asset | h09N | 부릉냐 - 랜덤전용[제한됨] | BronyaMotar_E | 1 |
| SkillData_게이트_랜덤_호시노_아이_3a13fd3d.asset | h09N | 부릉냐 - 랜덤전용[제한됨] | BronyaMotar_laser | 1 |
| SkillData_게이트_랜덤_호시노_아이_55b5cff0.asset | h09N | 부릉냐 - 랜덤전용[제한됨] | BronyaMotar_R | 3 |
| SkillData_게이트_변화됨_강재규_5c20b80f.asset | h07J | 캐럿 스론 달의 사자 - 변화된 | carrot_skill_2 | 1 |
| SkillData_게이트_변화됨_김건_355f5d39.asset | h077 | 간부 파견 해군 - 변화된 | Bugi_SommonDamage2 | 1 |
| SkillData_게이트_변화됨_박은석_8eab1c6f.asset | h05V | 포트거스.D.에이스 스페이드 해적단 선장 - 변 | Transpom_AceMana | 1 |
| SkillData_게이트_변화됨_박은석_9ffa9c45.asset | h05V | 포트거스.D.에이스 스페이드 해적단 선장 - 변 | Transpom_Ace_bulRemake | 1 |
| SkillData_게이트_변화됨_최상호_9ffa9c45.asset | h05S | 돈키호테 도플라밍고 하늘귀신 - 변화된 | Transpom_dp | 2 |
| SkillData_게이트_불멸_고도현_caa364ec.asset | h04A | 에드워드 뉴게이트 한시대를 주름잡던 대해적 -  | Ed_Skill_1, Ed_Skill_1_Item | 4 |
| SkillData_게이트_불멸_김용태_55b5cff0.asset | h04J | 골.D.로져 해적왕 - 불멸의 | roger_Mana | 1 |
| SkillData_게이트_불멸_김용태_57c27e37.asset | h04J | 골.D.로져 해적왕 - 불멸의 | roger_Skill_2 | 1 |
| SkillData_게이트_불멸_신지우_84bf5e76.asset | h04F | 스코퍼 가반 로져 해적단의 검호 - 불멸의 | Gaban_Skill_1 | 2 |
| SkillData_게이트_불멸_신지우_d33a6a78.asset | h04F | 스코퍼 가반 로져 해적단의 검호 - 불멸의 | Gaban_Skill_mana | 3 |
| SkillData_게이트_영원_김영원_5a6f48de.asset | h05C | |CFFFFFA78'아마존릴리의 여제'  보아  | hancock_skill_9 | 1 |
| SkillData_게이트_영원_김영원_86ef2902.asset | h05C | |CFFFFFA78'아마존릴리의 여제'  보아  | arrow_h2 | 1 |
| SkillData_게이트_영원_김영원_b3948c74.asset | h05C | |CFFFFFA78'아마존릴리의 여제'  보아  | hancock_skill_10 | 1 |
| SkillData_게이트_영원_김정래_a22567e4.asset | h067 | |CFFFFFA78세계의 가희 우타 - |CFF | Uta_skill_3_mana | 1 |
| SkillData_게이트_영원_김정래_ff8bed43.asset | h067 | |CFFFFFA78세계의 가희 우타 - |CFF | Uta_ArmyS | 1 |
| SkillData_게이트_영원_문필환_355f5d39.asset | h088 | |CFFFFFA78토트 무지카 봉인된 악보의 괴 | tots_R | 1 |
| SkillData_게이트_영원_서민성_948ad1b7.asset | h08R | |CFFFFFA78참된 호걸 코즈키 오뎅 - | | Odeng_Skill | 1 |
| SkillData_게이트_영원_서민성_b5c9b49a.asset | h08R | |CFFFFFA78참된 호걸 코즈키 오뎅 - | | Odeng_Skill_mana | 1 |
| SkillData_게이트_영원_윤현모_aa9cf1bd.asset | h059 | |CFFFFFA78'화권의' 포트거스.D.에이스 | ace_skill_5 | 1 |
| SkillData_게이트_영원_윤현모_d319dd11.asset | h059 | |CFFFFFA78'화권의' 포트거스.D.에이스 | ace_skill_3 | 1 |
| SkillData_게이트_영원_이지원_3ba504d5.asset | H0BK | |CFFFFFA78태양의신-니카 | Nika_AttackDamage | 4 |
| SkillData_게이트_영원_최상호_a8343962.asset | h05B | |CFFFFFA78해적왕자-롬멜의 카마이타치 카 | Cavendish_skill_Mana | 3 |
| SkillData_게이트_전설적인_이현주_0e5cf375.asset | h032 | 보아 핸콕 쿠사 해적단 선장 - 전설적인 | Legend_han_1 | 3 |
| SkillData_게이트_전설적인_임건웅_87ed0ff5.asset | h033 | 빈스모크 레이쥬 포이즌 핑크 - 전설적인 | Legend19_1 | 3 |
| SkillData_게이트_전설적인_임건웅_9ffa9c45.asset | h033 | 빈스모크 레이쥬 포이즌 핑크 - 전설적인 | Legend19_0 | 3 |
| SkillData_게이트_전설적인_임장혁_3a13fd3d.asset | h087 | 아마츠키 토키 시간 여행자 - 전설적인 | Legend31_toki | 3 |
| SkillData_게이트_전설적인_임채민_355f5d39.asset | h02T | 마르코 환수종'불사조' - 전설적인 | Legend17_Marcodamage | 1 |
| SkillData_게이트_전설적인_임채현_03847fe6.asset | h03S | 울티 토비롯포 - 전설적인 | Legend35ulti_trg | 1 |
| SkillData_게이트_전설적인_정윤식_355f5d39.asset | h02N | 겟코 모리아 쉐도우 아스가르드 - 전설적인 | Legend34Moria_Skill | 2 |
| SkillData_게이트_전설적인_정준영_3a13fd3d.asset | h02S | 롤로노아 조로 죽음의경지'사자의노래' - 전설적 | Legend13_2 | 4 |
| SkillData_게이트_전설적인_진연서_483e0464.asset | h09Z | 네코마무시 밤의 왕 - 전설적인 | Legend32_neko1 | 2 |
| SkillData_게이트_전설적인_최상호_355f5d39.asset | h03B | 에드워드 뉴게이트 '사황' 흰수염 해적단의 아버 | Legend51 | 2 |
| SkillData_게이트_전설적인_홍인창_fbb3c4ff.asset | h02U | 마샬.D.티치 검은수염 해적단 선장 "검은수염" | Legend26_tichi_crows | 3 |
| SkillData_게이트_제한_강보명_57c27e37.asset | h05G | 패트릭 레드필드 붉은백작 - 제한됨 | Red_skill_1 | 2 |
| SkillData_게이트_제한_김강민_948ad1b7.asset | h05F | 크로커다일 사막의 악어 - 제한됨 | cro_skill_2 | 1 |
| SkillData_게이트_제한_김민규_136d09f9.asset | h07I | 샬롯 카타쿠리 장성 밀가루 대신 - 제한됨 | Kata_03 | 1 |
| SkillData_게이트_제한_김민규_3a13fd3d.asset | h07I | 샬롯 카타쿠리 장성 밀가루 대신 - 제한됨 | katakuri_Skill_2 | 1 |
| SkillData_게이트_제한_박성호_3a13fd3d.asset | h08O | 마르코 흰수염 유산의 수호자 - 제한됨 | Marco_S2 | 3 |
| SkillData_게이트_제한_박성호_874ea782.asset | h08O | 마르코 흰수염 유산의 수호자 - 제한됨 | Marco_S1 | 2 |
| SkillData_게이트_제한_이유범_351a0f00.asset | h084 | 시노부 뇌쇄 쿠노이치 - 제한됨 | Sinobu_Skill_hp2 | 3 |
| SkillData_게이트_제한_이유범_43ad0210.asset | h084 | 시노부 뇌쇄 쿠노이치 - 제한됨 | Sinobu_Skill_4 | 1 |
| SkillData_게이트_제한_이충민_18aa2343.asset | h040 | 아인 네오 해군 장교 - 제한됨 | Ain_Skill_1 | 5 |
| SkillData_게이트_제한_이충민_8f2b5872.asset | h040 | 아인 네오 해군 장교 - 제한됨 | Ain_Skill_2 | 7 |
| SkillData_게이트_제한_임준성_355f5d39.asset | h0AI | 킹 삼재해:화재 - 제한됨 | King_skill_2, King_skill_2_tr | 4 |
| SkillData_게이트_제한_전법규_9ffa9c45.asset | h05E | 에넬 G.O.D - 제한됨 | Enel_skill_2 | 1 |
| SkillData_게이트_제한_최영민_30f704bf.asset | h05I | 레베카 콜로세움 전설의  후예 - 제한됨 | Rebeca_Skill_3 | 1 |
| SkillData_게이트_제한_최영민_33bd8aa4.asset | h05I | 레베카 콜로세움 전설의  후예 - 제한됨 | Rebeca_Skill_1 | 2 |
| SkillData_게이트_초월_강재규_AP_85d0117d.asset | H098 | 오하라의 마지막 생존자 | Robine_skill_2 | 1 |
| SkillData_게이트_초월_강재규_AP_fcba0856.asset | H098 | 오하라의 마지막 생존자 | Robine_skill_3 | 1 |
| SkillData_게이트_초월_강주혁_AP_5c20b80f.asset | H08V | 웨더리아의 항해사 | Nami_Skill_2 | 3 |
| SkillData_게이트_초월_구주호_AD_355f5d39.asset | H0BE | 오니히메 인수화 | Yamato_Attack_Damage2 | 3 |
| SkillData_게이트_초월_김경현_AP_4266b6e3.asset | H0B2 | 다섯번째 황제 | Snake_3_Kingkobra | 1 |
| SkillData_게이트_초월_김경현_AP_b7ae9b41.asset | H0B2 | 다섯번째 황제 | Snake_2 | 1 |
| SkillData_게이트_초월_김민준_AP_2a646778.asset | H05N | 해군의 홍일점 | Tasigi_03 | 4 |
| SkillData_게이트_초월_김민준_AP_355f5d39.asset | H05N | 해군의 홍일점 | Tasigi_02, tahsigi | 3 |
| SkillData_게이트_초월_두유찬_AD_8eab1c6f.asset | H092 | 혁명군 참모총장 | Sabo_Mana | 2 |
| SkillData_게이트_초월_박기찬_AD_a8343962.asset | H08Y | 무적의 아이언 파이러츠 | Franky_Skill_Mana1 | 1 |
| SkillData_게이트_초월_박기찬_AD_b3948c74.asset | H08Y | 무적의 아이언 파이러츠 | Franky_misiile_re | 1 |
| SkillData_게이트_초월_박민수_AD_b5c9b49a.asset | H09F | 수라의 검사 | Zoro_Samchun2, Zoro_samchun1 | 2 |
| SkillData_게이트_초월_박민수_AD_b7ae9b41.asset | H09F | 수라의 검사 | Zoro_raven | 2 |
| SkillData_게이트_초월_배성령_AD_18aa2343.asset | H09G | 정열의 요리사 | Sandi_skill_1 | 2 |
| SkillData_게이트_초월_배성령_AD_3c5f8bd9.asset | H09G | 정열의 요리사 | Sandi_skill_Mana2 | 3 |
| SkillData_게이트_초월_신문철_AP_355f5d39.asset | H099 | 명왕 레일리의 제자 | Ruffy_AttackDamage | 1 |
| SkillData_게이트_초월_신문철_AP_55ecbfb2.asset | H099 | 명왕 레일리의 제자 | Ruffy_Mana | 2 |
| SkillData_게이트_초월_양재모_AD_29846395.asset | H08X | 해군 대장 '성난 보라호랑이' | Huji01, Huji_03 | 4 |
| SkillData_게이트_초월_임장혁_AD_5e483927.asset | H08W | CP.Zero | Luchi_Skill_3_6kinggun | 3 |
| SkillData_게이트_초월_임채민_AP_42844439.asset | H090 | 지진과 어둠의 검은수염 | Tichi_skill_2_tr | 2 |
| SkillData_게이트_초월_임채민_AP_da9fb163.asset | H090 | 지진과 어둠의 검은수염 | Tichi_skill_1 | 3 |
| SkillData_게이트_초월_조성진_AD_29846395.asset | H09B | G.O.D | Usop_Skill_Mana | 1 |
| SkillData_게이트_초월_조성진_AD_33bd8aa4.asset | H09B | G.O.D | Usop_Skill_2 | 3 |
| SkillData_게이트_초월_최상호_AD_3c5f8bd9.asset | H09A | 어인협객 | Jimbe_Mu | 1 |
| SkillData_게이트_초월_최상호_AD_b8d2fd85.asset | H09A | 어인협객 | Jimbe | 2 |
| SkillData_게이트_초월_최상호_AP_18aa2343.asset | H09D | "어둠의 조커"드레스로자의 악몽 | DP_Skill_3 | 1 |
| SkillData_게이트_초월_최상호_AP_355f5d39.asset | H09D | "어둠의 조커"드레스로자의 악몽 | DP_AttackDamagegaksung | 1 |
| SkillData_게이트_초월_최상호_AP_5c20b80f.asset | H09D | "어둠의 조커"드레스로자의 악몽 | DP_Skill_2 | 2 |
| SkillData_게이트_희귀함_황준석_d1ddeea9.asset | h021 | 마샬.D.티치 흰수염해적단의 배신자 - 희귀함 | Tich1_danil | 2 |
| SkillData_게이트_히든_최윤서_3c5f8bd9.asset | h03R | [히든조합]반 더 데켄어인섬 절반면적의 노아를  | deken_Mana | 1 |
| SkillData_게이트_히든_최윤서_b8d2fd85.asset | h03R | [히든조합]반 더 데켄어인섬 절반면적의 노아를  | deken_skill_1 | 1 |
| SkillData_게이트_히든_호치킨_21297197.asset | h03Z | [히든조합]아카이누흰수염을죽인 붉은개 | Akainu_02_hidden | 3 |
| SkillData_게이트_히든_황정기_2251ce1a.asset | h03V | [히든조합]코알라혁명군-어인공수도대리사범 | koalla_skill_1 | 2 |
| SkillData_게이트_히든_황정기_2a646778.asset | h03V | [히든조합]코알라혁명군-어인공수도대리사범 | koalla_skill_Mana | 3 |
| SkillData_회수_불멸_고도현_8eab1c6f.asset | h04B | 금사자 시키 천신 - 불멸의 | Shiki_Lion | 1 |
| SkillData_회수_불멸_고도현_a23cc245.asset | h04B | 금사자 시키 천신 - 불멸의 | Shiki_SKill_item | 1 |
| SkillData_회수_불멸_고도현_a80b907f.asset | h04B | 금사자 시키 천신 - 불멸의 | Shiki_champa2 | 1 |
| SkillData_회수_불멸_박은석_b8d2fd85.asset | h04G | 제트 해군 스승 "Z" - 불멸의 | Z_skill_3 | 1 |
| SkillData_회수_불멸_이승우_355f5d39.asset | h04C | 몽키.D.거프 해군영웅 주먹의 거프 - 불멸의 | Garp_AttackDamage | 2 |
| SkillData_회수_불멸_이이삭_f50ad66e.asset | h04E | 센고쿠 해군원수 "부처님" - 불멸의 | Sengoku_Skill_Item | 1 |
| SkillData_회수_불멸_정윤식_355f5d39.asset | h049 | 실버즈 레일리 명왕 - 불멸의 | LaillySkill1 | 1 |
| SkillData_회수_영원_문필환_55b5cff0.asset | h057 | |CFFFFFA78알라바스타의 왕녀-ver파이러 | vivi_Skill_4_Mana | 2 |
| SkillData_회수_영원_문필환_9ffa9c45.asset | h057 | |CFFFFFA78알라바스타의 왕녀-ver파이러 | vivi_skill_2 | 1 |
| SkillData_회수_초월_강주혁_AP_b7ae9b41.asset | H0BT | 염왕 | Zoro_enfor_3dragon | 1 |
| SkillData_회수_초월_구주호_AD_3a13fd3d.asset | H0BL | '신'해군대장-초록소 | Rokugu_skill_1 | 1 |
| SkillData_회수_초월_구주호_AD_c453ba09.asset | H0B5 | 해군대장-보르살리노 | Kizaru_01_shoot | 1 |
| SkillData_회수_초월_김만경_AD_0ac0451e.asset | H095 | 신 해군원수  | Akainu_02 | 1 |
| SkillData_회수_초월_노태현_AP_3a13fd3d.asset | H09I | 소울 킹 | Brook_Skill_1 | 2 |
| SkillData_회수_초월_양재모_AD_f54a123f.asset | H096 | 로키포트 사건의 주모자 | Law_skill_5 | 1 |
| SkillData_회수_초월_최상호_AP_355f5d39.asset | H09E | "어둠의 조커"드레스로자의 악몽 | DP_AttackDamage | 1 |

## 재생성 방법

```bash
grep -l -E '게이트별 배정.*능력 [0-9]* *개를 이 SkillData 하나로 묶었다' Assets/Data/UnitSkills/*.asset
# 각 파일의 description에서 '· <트리거명>#<순번>: ' bullet을 파싱해 트리거명 중복 제거
```
