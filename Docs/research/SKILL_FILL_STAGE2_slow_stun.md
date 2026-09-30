# 스킬 채우기 2단계: 이감·강타 스턴 전수 (원작 j/w3a 직접 디코드)

2026-09-30 · 구현담당1 · **읽기 전용 단계**: Assets는 건드리지 않았다. PM 검증 뒤 반영한다.
지시서는 워크플로 `skill-fill-stage2`(wf_88215736-b55)의 RULES·PROPOSAL·EXTRACTORS 중 `slow`·`stun` 두 과제다. 사람 손으로 다시 돌렸다.
근거는 `Tools/w3x/원본/war3map_new.w3a`(`w3a.parse`), `war3map_new.w3u`(주인 유닛 `uabi`/`uabh`), `war3map_new.j`(줄 번호는 `grep -n`과 같은 기준, `\n`으로 나눔), 그리고 맵 MPQ에서 꺼낸 `war3mapMisc.txt`다. Docs는 근거로 쓰지 않았다.

---

## 0. 요약

| 묶음 | 원작 능력 수 | 우리 에셋에 대응 있음 | confirmed | likely | unknown | 우리 에셋 없음 |
|---|---|---|---|---|---|---|
| **강타(ACbh) 스턴** §3 | 104 (ACbh 469 중 스턴 있음) | 74 | 58 | 9 | 7 | 30 |
| **더미·트리거 스턴** §4 | 115 | 82 | 23 | 0 | 59 | 33 |
| **이감 전체** §5 | 103 | 58 | 16 | 28 | 14 | 45 |
| └ 패시브 이감 오라(AOae 음수·Aasl) §5-1 | 66 | | | | | |
| └ 평타 부착(Aspo·Afra) §5-2 | 7 | | | | | |
| └ 더미·트리거(AHtc·ACt2·AOeq·Apg2·Aslo) §5-3 | 30 | | | | | |

- **이감 N = 103**(반영 후보 58), **스턴 M = 219**(강타 104 + 더미 115, 반영 후보 156).
- 확신도는 두 축 중 낮은 쪽을 적었다.
  - **값:** 필드가 명시돼 있으면 confirmed, 기반 기본값에 기대면 unknown.
  - **매핑:** 에셋 이름이 원작 능력 ID로 시작하거나 설명에 더미 ID를 명시하면 confirmed. 원작 유닛 `(hXXX)의`가 명시돼 있고 새 오라 에셋이 필요하면 likely. 트리거를 등록한 원작 유닛 경유 후보뿐이면 unknown.
- 「우리 에셋 없음」은 원작 주인 유닛이 우리 로스터로 옮겨지지 않은 경우다(순위 배정에서 빠진 원작 유닛). 반영할 곳이 없다.

### 핵심 발견 다섯
1. **원작 이감의 주력은 천둥강타가 아니라 「지구력 오라(AOae)를 음수로 적에게 건 패시브 오라」다.** 58종이다(빙빙열매 −70/−90%, 흉포한 광기 −70%, 씽크홀 −99% 등). 이 경로는 **우리 코드가 아직 못 받는다**(§2-1).
2. **뿌리묶기(Aenr) 14종은 적 스턴이 아니다.** j의 `"entanglingroots"` 명령 8곳이 전부 `unitA`, 즉 시전한 **우리 유닛 자신**을 묶는다(연출 중 자기 정지). 예: `war3map_new.j:14802`의 `s__TrigVariables_Setunit(GlobalTV,0,s__TrigVariables_Attacker…)`→`IssueTargetOrder(…,"entanglingroots",…get_unitA…)`. 제안에서 뺐다.
3. **A0QY 「레베카제한 기본둔화」는 이름과 달리 둔화가 없다.** `Htc3=0`·`adur=0`이다. 같은 꼴(Htc3=0 또는 adur=0이라 이감 없음)의 AHtc가 18종 더 있다(§6).
4. **맵이 냉기 상수를 바꿨다:** `FrostMoveSpeedDecrease=0.15`, `FrostAttackSpeedDecrease=0.01`, `MinUnitSpeed=70`(war3mapMisc.txt). 따라서 Afra(쿠잔 아이스 사브르 등)는 −15%다. Htc3≥1(도플 2.5·로저 Ctc3 2.0)은 「최저 이속 70까지」라는 뜻이다.
5. **(범위 밖 버그) 「초월_황준석_ADAP」 17개와 「불멸_정준영」 10개 분할 파일의 피해 값이 서로 섞여 있다.** triggerChance는 맞다. §7-1.

---

## 1. 필드 의미 (이 문서가 쓴 것)

| 기반 | 필드 | 뜻 | 우리 쪽 |
|---|---|---|---|
| ACbh 강타 | Hbh1 확률(%) · Hbh2 공격력 배수 · Hbh3 추가 피해 · `adur` 일반 스턴 · `ahdu` 영웅 스턴 | 평타 적중 시 확률 발동 | triggerChance=Hbh1/100, Damage(basis CasterAttackPower: multiplier=Hbh2, bonus=Hbh3) + **Stun(adur)** |
| AOws 전쟁발구르기 | Wrs1 피해 · `aare` 반경 · `adur`/`ahdu` | 더미가 `stomp` | Stun Enemies |
| AHtb 폭풍망치 · ANfb/Awfb 화염탄 | Htb1 피해 · `adur`/`ahdu` | 더미가 `thunderbolt`/`firebolt` → 단일 | Stun SingleTarget |
| ANcs 집속 로켓 | Ncs* · `adur` | 더미가 `clusterrockets` | Stun Enemies |
| AHtc 천둥강타 / ACt2 | Htc1/Ctc1 피해 · **Htc3/Ctc3 이속 감소 비율** · Htc4/Ctc4 공속 감소 · `adur` 지속 | 더미가 `thunderclap`/`creepthunderclap` | Slow Enemies, multiplier=1−Htc3 |
| AOae 지구력 오라 | **Oae1 이속(음수=감소)** · Oae2 공속 · `atar` enemies · `aare` | 패시브 오라 | Aura + Slow, multiplier=1+Oae1 |
| Aasl 토네이도 감속 오라 | Slo1(음수) · `aare` | 패시브 오라 | 같음 |
| Aspo 느린 독 | Spo1 초당 피해 · **Spo2 이속 감소** · Spo3 공속 · `adur` | 평타 부착 | Slow SingleTarget |
| Afra 냉기 공격 | (값은 맵 상수) · `adur` | 평타 부착 | Slow 0.85 |
| AOeq 지진 | Oeq3 이속 감소 · `adur` | 더미가 `earthquake` | Slow Enemies |
| Apg2 퍼지 | Prg4/Prg5 회복 주기 · `adur` | 더미가 `purge` | 감쇠형이라 근사 |

빈 필드는 기반 능력의 기본값이다(RULES 5). 그런데 **기본값 원본(AbilityData.slk)은 이 맵 MPQ에 없어서**(`mpqread`로 `Units\AbilityData.slk`를 읽으면 None) 값을 확인할 수 없다. 그런 칸은 모두 「기반 기본값(미확인)」·unknown으로 적었다. 기억에 기대 숫자를 채우지 않았다.

---

## 2. 반영 전에 필요한 코드 (PM 판단)

1. **오라 경로가 Slow를 모른다.** `UnitAttacker.UpdateAuraTick`이 들어온 적에게 `ApplyPersistentAuraEffectsToEnemy`, 나간 적에게 `RemovePersistentAuraEffectsFromEnemy`를 부르는데(`Assets/Scripts/Units/UnitAttacker.cs:981`·`:999`), 두 함수 모두 ArmorBonus·HealOverTime·ApplyBuff만 처리한다. 게다가 `ApplyEffect`의 `case SkillEffectKind.Slow`는 `duration > 0f`일 때만 동작한다(`:1538`). 결국 §5-1의 58+8종은 **데이터만 넣으면 아무 일도 안 일어난다.** 필요한 것: 두 함수에 `case Slow: target.AddSlow(effect.multiplier)` / `target.RemoveSlow(effect.multiplier)` 추가. AddSlow/RemoveSlow는 값으로 짝을 맞추니 그대로 쓰면 된다.
2. **최저 이속:** 원작 `MinUnitSpeed=70`. Htc3·Ctc3가 1 이상인 행(A0AM·A0PI 2.5, A0OY 2.0)은 「하한까지」라는 뜻이다. 우리 Slow는 `multiplier < 1f`여야 동작하고 하한은 0.01이다. 제안은 multiplier = 70 ÷ 원작 적 기본 이속. 적마다 다르므로 PM이 대표값을 고르거나 EnemyDummy에 「최저 이속」 개념을 두어야 한다.
3. **효과별 범위가 없다:** SkillEffect에 range가 없어서, 스턴 반경(aare)이 우리 레벨 range와 다르면 한쪽에 맞춰야 한다. §4 표에 ⚠️로 표시했다(28건).
4. **공속 감소:** 원작 Htc4·Ctc4·Oae2·Spo3는 값이 적힌 곳이 전부 0이었다. 빈칸은 주인 없는 A04R·A04S의 Spo3뿐이다. Afra는 맵 상수 −1%라 무시할 만하다. 지금으로선 공속 둔화 kind가 필요 없다.

---

## 3. 강타(ACbh) 스턴 전수 — 469종 중 스턴 있음 104종

판정: `adur`(일반 유닛 스턴 초)가 0이 아니거나 빈칸(=기반 기본값)인 ACbh. 우리 적은 일반 유닛이므로 `adur`를 쓴다(`ahdu`는 영웅용, 참고로만 적음).
제안 공통형: **해당 에셋의 모든 레벨 effects에 `{kind: Stun(1), target: SingleTarget(3), duration: adur, chance: 1}` 추가.** 발동 확률은 이미 레벨의 triggerChance(=Hbh1/100)가 맡는다.

| 원작 ID | 이름 | 원작 주인 | Hbh1 확률% | Hbh2 배수 | Hbh3 추가 | adur | ahdu | 우리 에셋 (현재) | 제안 duration | 확신도 | 메모 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A03M | !시구레 | h00E 타시기 - 안흔함 | 20 | 빈칸 | 200 | 0.7 | 0.35 | `원작능력_안흔함_이재윤` OnHitChance[Damage] | 0.7초 | confirmed |  |
| A03N | !명왕의 검술 | h05X [히든]실버즈 레일리 세계 | 10 | 빈칸 | 50000 | 0.5 | 0.25 | `원작능력_희귀함_박기찬` OnHitChance[Damage]<br>`원작능력_희귀함_최상호_윤식파의두뇌` OnHitChance[Damage×4] | 0.5초 | confirmed |  |
| A03W | !패기의 검술 | h04U 샹크스 마린포드 정상해전 , H08Z 마린포드 정상해전 종결자 | 12 | 10 | 500000 | 1.5 | 0.75 | `원작능력_초월_황준석_ADAP_A03W` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A03X | !패기의 검술 | h035 샹크스 사황 '붉은머리'  | 20 | 10 | 250000 | 0.75 | 0.38 | `원작능력_전설적인_임채민` OnHitChance[Damage×6] | 0.75초 | confirmed |  |
| A03Y | !패기의 검술 | h029 샹크스 붉은머리 해적단 선 | 20 | 2 | 0 | 0.5 | 0.25 | `원작능력_희귀함_이승우` OnHitChance[Damage] | 0.5초 | confirmed |  |
| A03Z | !패기의 대포 | h03U [히든조합]레드포스 호사황 | 30 | 3 | 0 | 빈칸 | 빈칸 | 없음(우리 로스터에 대응 에셋 없음) | 기반 기본값(미확인) | —(에셋 없음) | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A040 | !패기의 탄환 | h01S 벤 베크만 붉은머리 해적단 | 35 | 빈칸 | 2500 | 0.75 | 0.38 | `원작능력_희귀함_임채민` OnHitChance[Damage×5] | 0.75초 | confirmed |  |
| A044 | !패기의 주먹 | h04C 몽키.D.거프 해군영웅 주 | 20 | 5 | 250000 | 3 | 1.5 | `원작능력_불멸_이승우_A044` OnHitChance[Damage] | 3초 | confirmed |  |
| A048 | !육식 | h02R 로브 루치 생명귀환 지회무 | 빈칸 | 빈칸 | 250000 | 0.6 | 0.3 | `원작능력_전설적인_임채민_A048` OnHitChance[Damage]<br>`원작능력_전설적인_홍인창` OnHitChance[Damage×10] | 0.6초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A049 | !참륙용인 | h026 시류 전 임펠다운 간수장  | 20 | 빈칸 | 25000 | 0.9 | 0.45 | `원작능력_희귀함_최현우` OnHitChance[Damage×5] | 0.9초 | confirmed |  |
| A04A | !참륙용인 | h03J [히든조합]시류비의 시류 | 20 | 4 | 250000 | 0.9 | 0.45 | 없음(우리 로스터에 대응 에셋 없음) | 0.9초 | —(에셋 없음) |  |
| A04B | !잠의 노래 프람 | h01N 브룩 해골 음악가 - 희귀 | 14 | 빈칸 | 4500 | 0.4 | 0.2 | `원작능력_희귀함_노수신` OnHitChance[Damage]<br>`원작능력_희귀함_이태훈` OnHitChance[Damage] | 0.4초 | confirmed |  |
| A04C | !잠의 노래 프람 | h00D 브룩 - 안흔함 | 10 | 빈칸 | 0 | 0.2 | 0.1 | `원작능력_안흔함_문필환` OnHitChance[Damage] | 0.2초 | confirmed |  |
| A04D | !소울 솔리드 | h04T 브룩 소울 킹 - 초월함, H09I 소울 킹 | 17.5 | 빈칸 | 0 | 1.75 | 0.88 | `원작능력_초월_황준석_ADAP_A04D` OnHitChance[Damage] | 1.75초 | confirmed |  |
| A050 | !고무고무 열매 | 없음 | 20 | 빈칸 | 20 | 0.25 | 0.12 | 없음(우리 로스터에 대응 에셋 없음) | 0.25초 | —(에셋 없음) |  |
| A051 | !고무고무 열매 | h04R 몽키.D.루피 명왕 레일리, H099 명왕 레일리의 제자 | 18 | 5 | 0 | 빈칸 | 1 | `원작능력_초월_황준석_ADAP_A051` OnHitChance[Damage] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A052 | !고무고무 열매 | h02X 몽키.D.루피 '기어 2' | 빈칸 | 4.25 | 75000 | 0.7 | 0.35 | `원작능력_전설적인_임채현` OnHitChance[Damage×7] | 0.7초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A053 | !고무고무 열매 | h00U 몽키.D.루피 기어 세컨드 | 20 | 빈칸 | 30 | 0.25 | 0.12 | 없음(우리 로스터에 대응 에셋 없음) | 0.25초 | —(에셋 없음) |  |
| A05E | !마기탄 | h015 버기 특제 마기탄 - 특별 | 30 | 빈칸 | 800 | 1 | 0.5 | `원작능력_특별함_임채준` OnHitChance[Damage]<br>`원작능력_특별함_최준우` OnHitChance[Damage] | 1초 | confirmed |  |
| A05K | !와노쿠니의 검술 | h046 [히든조합]킨에몬여우불의  | 20 | 빈칸 | 100000 | 빈칸 | 1 | 없음(우리 로스터에 대응 에셋 없음) | 기반 기본값(미확인) | —(에셋 없음) | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A05L | !검투사 | h03T [히든조합]레베카콜로세움의 | 20 | 빈칸 | 80000 | 1.25 | 0.62 | 없음(우리 로스터에 대응 에셋 없음) | 1.25초 | —(에셋 없음) |  |
| A05M | !뇌영 | h05E 에넬 G.O.D - 제한됨 | 50 | 빈칸 | 500000 | 빈칸 | 1 | `원작능력_제한_전법규` OnHitChance[Damage×14] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A05P | !대구경 탄환2 | h04G 제트 해군 스승 "Z" - | 100 | 빈칸 | 50000 | 2.25 | 2.25 | `원작능력_불멸_박은석` OnHitChance[Damage] | 2.25초 | confirmed |  |
| A05R | !매료매료 열매 | h01M 보아 핸콕 칠무해 - 희귀 | 빈칸 | 빈칸 | 10000 | 0.6 | 0.3 | `원작능력_희귀함_황준석` OnHitChance[Damage×7] | 0.6초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A05S | !매료매료 열매 | h032 보아 핸콕 쿠사 해적단 선 | 빈칸 | 빈칸 | 150000 | 0.8 | 0.4 | `원작능력_전설적인_진연서` OnHitCount[Damage×6] | 0.8초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A065 | !금쇄봉 | H0BD 오니히메, H0BE 오니히메 인수화 | 30 | 빈칸 | 50000 | 0.45 | 0.45 | 없음(우리 로스터에 대응 에셋 없음) | 0.45초 | —(에셋 없음) |  |
| A06N | !어인 | h03R [히든조합]반 더 데켄어인 | 20 | 빈칸 | 150000 | 0.4 | 0.2 | 없음(우리 로스터에 대응 에셋 없음) | 0.4초 | —(에셋 없음) |  |
| A06O | !어인 | h011 아론 어인해적단 선장 - , h018 징베 칠무해 - 특별함 | 20 | 빈칸 | 500 | 0.25 | 0.15 | `원작능력_특별함_이지원` OnHitChance[Damage]<br>`원작능력_특별함_이현빈` OnHitChance[Damage]<br>`원작능력_특별함_조세민` OnHitChance[Damage×2]<br>`원작능력_특별함_주영호` OnHitChance[Damage×2] | 0.25초 | confirmed |  |
| A06P | !어인 | h00F 하찌 - 안흔함 | 20 | 빈칸 | 150 | 0.1 | 0.05 | `원작능력_안흔함_박민수` OnHitChance[Damage] | 0.1초 | confirmed |  |
| A06Q | !어인 | h01T 반 더 데켄 플라잉더치맨호 | 20 | 빈칸 | 2000 | 0.3 | 0.15 | `원작능력_희귀함_김청운` OnHitChance[Damage]<br>`원작능력_희귀함_이은엽` OnHitChance[Damage] | 0.3초 | confirmed |  |
| A08H | !샨디아의 기운 | h03F 카르가라 샨디아의 대전사  | 25 | 10 | 0 | 0.4 | 0.4 | `원작능력_전설적인_박민석` OnHitChance[Damage]<br>`원작능력_전설적인_이승우` OnHitChance[Damage×3] | 0.4초 | confirmed |  |
| A0AG | 10톤 해머 | h04W 우솝 10톤해머 G.O.D, H09C G.O.D | 20 | 7 | 0 | 1 | 1 | `원작능력_초월_황준석_ADAP_A0AG` OnHitChance[Damage] | 1초 | confirmed |  |
| A0CS | !빛빛열매 | h04L 보르살리노 해군대장 키자루, H0B5 해군대장-보르살리노 | 빈칸 | 8.88 | 0 | 3 | 1.5 | `원작능력_초월_황준석_ADAP_A0CS` OnHitChance[Damage] | 3초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A0DJ | !살육 | h01B 킬러 초신성 - 특별함 | 10 | 2 | 2500 | 0.5 | 0.25 | `원작능력_특별함_정승준` OnHitChance[Damage]<br>`원작능력_특별함_황정기` OnHitChance[Damage] | 0.5초 | confirmed |  |
| A0DK | !살육 | h045 [히든조합]킬러초신성 -  | 빈칸 | 7 | 0 | 1.35 | 0.67 | 없음(우리 로스터에 대응 에셋 없음) | 1.35초 | —(에셋 없음) | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A0DP | !무장색의 패기-가반 | h04F 스코퍼 가반 로져 해적단의 | 45 | 빈칸 | 350000 | 3 | 1.5 | `원작능력_불멸_신지우` OnHitChance[Damage×11] | 3초 | confirmed |  |
| A0DS | !거인의 힘- 오즈희귀 | h020 마인 오즈 대륙파괴인 -  | 20 | 2 | 0 | 2.25 | 1.12 | `원작능력_희귀함_김만경` OnHitChance[Damage]<br>`원작능력_희귀함_이용민` OnHitChance[Damage] | 2.25초 | confirmed |  |
| A0EA | !듀랜달 | h05B 해적왕자-롬멜의 카마이타치 | 20 | 10 | 400000 | 3 | 1.5 | `원작능력_영원_윤현모` OnHitChance[Damage×10]<br>`원작능력_영원_최상호_A0EA` OnHitChance[Damage] | 3초 | confirmed |  |
| A0EM | !명왕의 검술 | h049 실버즈 레일리 명왕 - 불 | 빈칸 | 빈칸 | 92500 | 0.6 | 0.6 | `원작능력_불멸_고도현` OnHitChance[Damage×6]<br>`원작능력_불멸_정윤식_A0EM` OnHitChance[Damage] | 0.6초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A0EN | !명왕의 검술 | h03A 실버즈 레일리 로저해적단  | 12 | 빈칸 | 80000 | 0.6 | 0.3 | `원작능력_전설적인_이재윤` OnHitChance[Damage×2]<br>`원작능력_전설적인_진연서_A0EN` OnHitChance[Damage] | 0.6초 | confirmed |  |
| A0EO | !순속 | h049 실버즈 레일리 명왕 - 불 | 12 | 빈칸 | 0 | 0.7 | 0.35 | `원작능력_불멸_정윤식_A0EO` OnHitChance[Damage] | 0.7초 | confirmed |  |
| A0FI | !카무사리 | h04J 골.D.로져 해적왕 - 불 | 12.5 | 빈칸 | 500000 | 0.75 | 0.75 | `원작능력_불멸_김용태_A0FI` OnHitChance[Damage] | 0.75초 | confirmed |  |
| A0FJ | !공작슬래셔-희귀 | h01O 비비 MIss.웬즈데이 - | 10 | 빈칸 | 20000 | 0.5 | 0.5 | `원작능력_희귀함_노태현` OnHitChance[Damage]<br>`원작능력_희귀함_장하민` OnHitChance[Damage×4] | 0.5초 | confirmed |  |
| A0FU | !주먹의거프-거불 | h04C 몽키.D.거프 해군영웅 주 | 0 | 빈칸 | 200000 | 1.2 | 1.2 | 없음(우리 로스터에 대응 에셋 없음) | 1.2초 | —(에셋 없음) | Hbh1=0 → 발동 안 함 |
| A0G3 | !최강의 참격 | h058 세계 최강의 대검호 쥬라클 | 100 | 빈칸 | 0 | 1.2 | 1.2 | `원작능력_영원_김영원` OnHitChance[효과0] | 1.2초 | confirmed |  |
| A0G4 | !화염성 | h04W 우솝 10톤해머 G.O.D, h04X 우솝 G.O.D - 초월함, H09B G.O.D | 100 | 빈칸 | 0 | 1 | 1 | `원작능력_초월_황준석_ADAP_A0G4` OnHitChance[Damage×2] | 1초 | confirmed |  |
| A0GD | !숲숲 열매 | h07E 코비 "거프의 제자" 해군, h08Q 아라마키 해군대장-초록 - | 20 | 빈칸 | 0 | 1.75 | 1.75 | `원작능력_변화됨_최상호` OnHitChance[Damage×2] | 1.75초 | confirmed |  |
| A0GH | !무장색의패기-제파 | h03I 제파 전 해군 대장 '흑완 | 30 | 빈칸 | 150000 | 0.75 | 0.38 | `원작능력_전설적인_임건웅` OnHitChance[Damage×6] | 0.75초 | confirmed |  |
| A0GK | !단신 최강 | h05G 패트릭 레드필드 붉은백작  | 100 | 빈칸 | 850000 | 1.5 | 1.5 | `원작능력_제한_강보명_A0GK` OnHitChance[Damage]<br>`원작능력_제한_최영민` OnHitChance[Damage×11] | 1.5초 | confirmed | `원작능력_제한_최영민` triggerChance 0.75 ≠ Hbh1 100% |
| A0GL | !신의 손 | h06Y 신의손 - 카미조 토우마  | 100 | 3 | 0 | 3 | 3 | `원작능력_랜덤_야사카_카나코` OnHitChance[Damage]<br>`원작능력_랜덤_한마_바키` OnHitChance[Damage×6+AegrStack] | 3초 | confirmed |  |
| A0GM | !끓어오르는피 | h05G 패트릭 레드필드 붉은백작  | 20 | 빈칸 | 252500 | 2.5 | 1.25 | `원작능력_제한_강보명_A0GM` OnHitChance[Damage]<br>`원작능력_제한_이유범` OnHitChance[Damage] | 2.5초 | confirmed |  |
| A0H6 | !슈타인베르거 | h06X 센토 이스즈 - 랜덤전용 | 4.25/4.25 | 10 | 0 | 5/5 | 5/5 | `원작능력_랜덤_이즈미_신이치` OnHitChance[Damage×11] | 5/5초 | confirmed | 레벨 2개 — 레벨별 값 그대로 · j 참조 줄 20775 |
| A0HK | !저격왕-우솝초 | h04X 우솝 G.O.D - 초월함, H09B G.O.D | 빈칸 | 3.5 | 0 | 5 | 5 | `원작능력_초월_황준석_ADAP_A0HK` OnHitChance[Damage] | 5초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A0HV | !흑도-일섬 | h058 세계 최강의 대검호 쥬라클 | 14 | 3.5 | 0 | 1.5 | 1.5 | `원작능력_영원_최상호_A0HV` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A0II | !혁명군대리사범 | h03V [히든조합]코알라혁명군-어 | 40 | 빈칸 | 0 | 0.5 | 0.25 | 없음(우리 로스터에 대응 에셋 없음) | 0.5초 | —(에셋 없음) |  |
| A0IL | !조선공의 망치 | h05O 아이스버그 워터세븐의 조선 | 50 | 빈칸 | 500000 | 빈칸 | 1 | `원작능력_특수함_황길라` OnHitChance[Damage] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A0IS | !화이트 블로우 | h02V 스모커 집념의 해군 중장  | 18 | 빈칸 | 250000 | 0.85 | 0.85 | `원작능력_전설적인_임장혁` OnHitChance[Damage×7]<br>`원작능력_전설적인_정준영_A0IS` OnHitChance[Damage] | 0.85초 | confirmed |  |
| A0IT | !강철 풍선 | h04Q 빅 맘 강철 풍선 - 불멸, h04Y 광분한 빅 맘 강철 풍선 , h09J 폭주한 빅 맘 사황 '강철 | 20 | 빈칸 | 0 | 0.75 | 0.38 | `원작능력_불멸_정준영_A0IT` OnHitChance[Damage×2]<br>`원작능력_초월_황준석_ADAP_A0IT` OnHitChance[Damage] | 0.75초 | confirmed |  |
| A0IV | !트루에노 바스타드 | h05I 레베카 콜로세움 전설의   | 33 | 빈칸 | 0 | 3 | 1.5 | 없음(우리 로스터에 대응 에셋 없음) | 3초 | —(에셋 없음) |  |
| A0J7 | !쿠사의 패기 | h05C '아마존릴리의 여제'  보 | 빈칸 | 8 | 500000 | 3 | 1.5 | `원작능력_영원_조세민` OnHitChance[효과0]<br>`원작능력_영원_최상호` OnHitChance[Damage×14] | 3초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A0JJ | 예검 시구레 | 없음 | 17/19.5/22/25 | 빈칸 | 200000/300000/400000/500000 | 2/2/2 | 2/2/2 | 없음(우리 로스터에 대응 에셋 없음) | 2/2/2초 | —(에셋 없음) | 레벨 4개 — 레벨별 값 그대로 · j 참조 줄 10231,10232 |
| A0JW | !배리어 피스트 | h01W 바르토로메오 밀짚모자 해적, h02Z 바르토로메오 신예 초신성- | 17 | 빈칸 | 6000 | 0.5 | 0.25 | `원작능력_전설적인_박민수` OnHitChance[Damage]<br>`원작능력_전설적인_이일중` OnHitChance[Damage×7]<br>`원작능력_희귀함_장태영` OnHitChance[Damage×4] | 0.5초 | confirmed |  |
| A0K6 | !자석 팔 | h02D 유스타스 키드 캡틴-키드  | 40 | 빈칸 | 6000 | 0.5 | 0.25 | `원작능력_희귀함_두유찬` OnHitChance[Damage]<br>`원작능력_희귀함_정내연` OnHitChance[Damage] | 0.5초 | confirmed |  |
| A0KA | !레드호크 | h04R 몽키.D.루피 명왕 레일리, H099 명왕 레일리의 제자 | 10.62 | 빈칸 | 1062500 | 빈칸 | 1 | `원작능력_초월_황준석_ADAP_A0KA` OnHitChance[Damage] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A0KE | !숙련된 패기 | h04R 몽키.D.루피 명왕 레일리, H099 명왕 레일리의 제자 | 6.25 | 빈칸 | 625000 | 빈칸 | 1 | `원작능력_초월_황준석_ADAP_A0KE` OnHitChance[Damage] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A0KW | !액션펭귄 | h06Z 전투펭귄- 엘리자베스 랜덤 | 14 | 14.9 | 0 | 3 | 1.5 | `원작능력_랜덤_가사이_유노` OnHitChance[Damage]<br>`원작능력_랜덤_호시노_아이` OnHitChance[Damage×13] | 3초 | confirmed |  |
| A0L4 | !코비 체 | h03E 코비 세계의 운명을 바꾼  | 20 | 빈칸 | 200000 | 0.3 | 0.15 | `원작능력_전설적인_이현주` OnHitChance[Damage×2] | 0.3초 | confirmed |  |
| A0L9 | !파라다이스  로스트 | 없음 | 4.25 | 50 | 0 | 5 | 5 | 없음(우리 로스터에 대응 에셋 없음) | 5초 | —(에셋 없음) | j 참조 줄 20775 |
| A0LS | !천신 | h04B 금사자 시키 천신 - 불멸 | 18 | 빈칸 | 700000 | 3 | 3 | `원작능력_불멸_김용태` OnHitChance[Damage×4] | 3초 | confirmed |  |
| A0M8 | !해적파견조직총수 | h05A 천냥광대 버기 - 영원한 | 7.77 | 7.77 | 3333333 | 7.77 | 7.77 | `원작능력_영원_이지원` OnHitChance[Damage×4] | 7.77초 | confirmed |  |
| A0MH | !베르고-무장색의 패기 | h03W [히든조합]귀죽의 베르고해 | 20 | 빈칸 | 0 | 0.6 | 0.6 | 없음(우리 로스터에 대응 에셋 없음) | 0.6초 | —(에셋 없음) |  |
| A0O7 | !그림자 일격-일도양단 | h02Y 몽키.D.루피 나이트메어  | 18 | 11 | 0 | 3 | 3 | `원작능력_전설적인_엄태웅` OnHitChance[Damage×2+AegrStack] | 3초 | confirmed |  |
| A0P8 | !어인-피셔 | h047 [히든조합]피셔 타이거마리 | 22 | 빈칸 | 0 | 0.7 | 0.35 | 없음(우리 로스터에 대응 에셋 없음) | 0.7초 | —(에셋 없음) |  |
| A0PQ | !강철을 벤 사나이 | h02S 롤로노아 조로 죽음의경지' | 30 | 빈칸 | 500000 | 1 | 0.5 | `원작능력_전설적인_정준영_A0PQ` OnHitChance[Damage]<br>`원작능력_전설적인_최상호` OnHitChance[Damage×10] | 1초 | confirmed |  |
| A0PZ | !이강력참1 | H09F 수라의 검사 | 25 | 10 | 200000 | 3 | 1.5 | 없음(우리 로스터에 대응 에셋 없음) | 3초 | —(에셋 없음) |  |
| A0QL | !어인족 수장1 | h03G 징베 전 칠무해 어인족 수 | 25 | 빈칸 | 600000 | 0.6 | 0.3 | `원작능력_전설적인_정준영` OnHitChance[Damage×9] | 0.6초 | confirmed |  |
| A0QO | !떡떡열매 | h07I 샬롯 카타쿠리 장성 밀가루 | 20 | 빈칸 | 150000 | 1.2 | 0.6 | `원작능력_제한_김민규_A0QO` OnHitChance[Damage]<br>`원작능력_제한_박성호` OnHitChance[Damage×2] | 1.2초 | confirmed |  |
| A0QQ | !견문색의 패기-카타쿠리 | h07I 샬롯 카타쿠리 장성 밀가루 | 100 | 6.25 | 0 | 0.1 | 0.1 | `원작능력_제한_김민규` OnHitChance[Damage×2] | 0.1초 | confirmed |  |
| A0QV | !배수의 검무 | h05I 레베카 콜로세움 전설의   | 16/17 | 2.5/3 | 0 | 빈칸 | 1 | `원작능력_제한_최영민_A0QV` OnHitChance[Damage] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A0R5 | !육식 | H08W CP.Zero | 25 | 빈칸 | 500000 | 빈칸 | 1 | 없음(우리 로스터에 대응 에셋 없음) | 기반 기본값(미확인) | —(에셋 없음) | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A0RQ | !폭발탄 사격 | h040 아인 네오 해군 장교 -  | 25 | 빈칸 | 0 | 1.75 | 0.88 | 없음(우리 로스터에 대응 에셋 없음) | 1.75초 | —(에셋 없음) |  |
| A0S7 | !전격-캐럿 | h07J 캐럿 스론 달의 사자 -  | 50 | 빈칸 | 350000 | 0.45 | 0.22 | `원작능력_변화됨_박은석` OnHitChance[Damage×9] | 0.45초 | confirmed |  |
| A0S9 | !전격-캐럿 | h07L [히든조합]캐럿밍크족 토끼 | 20 | 빈칸 | 300000 | 0.6 | 0.3 | 없음(우리 로스터에 대응 에셋 없음) | 0.6초 | —(에셋 없음) |  |
| A0SL | !마술카드-조커 | H094 운명을 점치는 마술사 | 25 | 빈칸 | 0 | 1 | 0.5 | 없음(우리 로스터에 대응 에셋 없음) | 1초 | —(에셋 없음) |  |
| A0SN | !백수의 왕 | h07M 카이도 사황'백수의 왕' , h0AD 카이도 사황'백수의 왕'  | 100 | 빈칸 | 0 | 0.37 | 0.37 | `원작능력_불멸_정준영_A0SN` OnHitChance[Damage] | 0.37초 | confirmed |  |
| A0TW | !그림자 흡수-루피 나메 | h02Y 몽키.D.루피 나이트메어  | 빈칸 | 12.5 | 0 | 6 | 6 | `원작능력_전설적인_이유선` OnHitChance[Damage×3] | 6초 | likely | Hbh1 빈칸 → 확률은 ACbh 기본값(미확인). 우리 triggerChance가 이 값을 지어낸 것일 수 있다 |
| A0X0 | !어인공수도-징베 | h04K 징베 어인협객 - 초월함, H09A 어인협객 | 40 | 빈칸 | 0 | 1.5 | 0.75 | `원작능력_초월_황준석_ADAP_A0X0` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A0X4 | !고무고무 열매 | H0B2 다섯번째 황제, H0B3 다섯번째 황제 | 20 | 3 | 0 | 0.5 | 0.25 | 없음(우리 로스터에 대응 에셋 없음) | 0.5초 | —(에셋 없음) |  |
| A0XF | !스탠드-스타플라티나 | h09R 쿠죠 죠타로 - 랜덤전용[ | 25 | 빈칸 | 0 | 3 | 1.5 | 없음(우리 로스터에 대응 에셋 없음) | 3초 | —(에셋 없음) |  |
| A0XN | !선법 | h09M 후카-운묵단심 - 랜덤전용 | 25 | 빈칸 | 0 | 1.5 | 1.5 | 없음(우리 로스터에 대응 에셋 없음) | 1.5초 | —(에셋 없음) |  |
| A0Y0 | !패기-류오(오뎅) | h08R 참된 호걸 코즈키 오뎅 - | 35 | 빈칸 | 0 | 0.45 | 0.45 | 없음(우리 로스터에 대응 에셋 없음) | 0.45초 | —(에셋 없음) |  |
| A0YJ | !은폐나이프-료우기 | h0A2 료우기 시키 - 랜덤전용[ | 0 | 빈칸 | 0 | 3 | 1.5 | 없음(우리 로스터에 대응 에셋 없음) | 3초 | —(에셋 없음) | Hbh1=0 → 발동 안 함 |
| A0YY | !산적왕의 검기 | h0AE 마운틴.D.히그마 리얼리스 | 50 | 빈칸 | 20000 | 빈칸 | 1 | `원작능력_불멸_정준영_A0YY` OnHitChance[Damage] | 기반 기본값(미확인) | unknown | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A101 | !자석 팔 | h0AG 키드 유스타스'캡틴' - , H0B4 유스타스'캡틴' | 35 | 빈칸 | 360000 | 1.5 | 0.75 | `원작능력_초월_황준석_ADAP_A101` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A11Y | !디아블 잠브-검은다리-상초 | H09G 정열의 요리사, H09H 뉴카마섬의 생존자 | 20/22.5 | 빈칸 | 0 | 0.25/0.25 | 0.12/0.12 | 없음(우리 로스터에 대응 에셋 없음) | 0.25/0.25초 | —(에셋 없음) | 레벨 2개 — 레벨별 값 그대로 · j 참조 줄 15110 |
| A13O | !부유물-돌 | h0BI 부유물1불멸의 | 17 | 빈칸 | 120000 | 1.5 | 0.75 | `원작능력_불멸_정준영_A13O` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A13P | !부유물-돌31 | h0BH 부유물2불멸의 | 20 | 빈칸 | 30000 | 1.5 | 0.75 | `원작능력_불멸_정준영_A13P` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A13Q | !부유물-돌1 | h0BI 부유물1불멸의 | 25 | 빈칸 | 70000 | 1.5 | 0.75 | `원작능력_불멸_정준영_A13Q` OnHitChance[Damage] | 1.5초 | confirmed |  |
| A13R | !부유물-돌2 | h0BH 부유물2불멸의 | 35 | 빈칸 | 30000 | 1.5 | 0.75 | `더미채널_불멸_고도현_79행_10000` OnHitChance[Damage×2]<br>`원작능력_불멸_정준영_A13R` OnHitChance[효과0] | 1.5초 | confirmed | `더미채널_불멸_고도현_79행_10000` triggerChance 1.0 ≠ Hbh1 35% |
| A149 | !명왕의 검술 | h05J 실버즈 레일리 명왕 - 불 | 10 | 빈칸 | 102500 | 0.6 | 0.6 | `더미채널_불멸_정윤식_79행_10000` OnHitChance[Damage] | 0.6초 | confirmed | `더미채널_불멸_정윤식_79행_10000` triggerChance 1.0 ≠ Hbh1 10% |
| A15B | !괴승 | h07Q [히든조합]우루지 초신성- | 20 | 빈칸 | 100000 | 1 | 1 | 없음(우리 로스터에 대응 에셋 없음) | 1초 | —(에셋 없음) |  |
| A15P | !고무고무 열매 | H0BK 태양의신-니카 | 20 | 5 | 0 | 빈칸 | 1 | 없음(우리 로스터에 대응 에셋 없음) | 기반 기본값(미확인) | —(에셋 없음) | adur 빈칸 → ACbh 기본값. 원본 AbilityData.slk가 맵에 없어 확인 불가 |
| A15Z | !엔마 | H0BT 염왕 | 25 | 11 | 200000 | 3 | 1.5 | 없음(우리 로스터에 대응 에셋 없음) | 3초 | —(에셋 없음) |  |
| A16L | !화이트아웃 | h02V 스모커 집념의 해군 중장  | 25 | 빈칸 | 0 | 1.5 | 1.5 | `원작능력_전설적인_양재모` OnHitChance[Damage×5] | 1.5초 | confirmed |  |

**스턴 0(확인함, 항목 아님)** — `adur`=0으로 명시된 ACbh 365종: A003, A02U, A031, A033, A034, A035, A038, A03A, A03F, A041, A042, A043, A045, A046, A047, A04U, A04V, A04W, A04X, A054, A059, A05A, A05B, A05C, A05D, A05J, A05N, A05O, A05T, A05X, A05Y, A060, A061, A066, A067, A068, A069, A06A, A06B, A06C, A06D, A06E, A06F, A06G, A06H, A06I, A06J, A06K, A06L, A06M, A06R, A06S, A06T, A06U, A06X, A06Y, A06Z, A074, A075, A076, A09E, A09O, A09P, A0AK, A0B7, A0CK, A0CT, A0CV, A0CW, A0CX, A0CY, A0CZ, A0D0, A0D1, A0D2, A0D6, A0D7, A0D8, A0DE, A0DF, A0DN, A0DO, A0DV, A0DW, A0E2, A0E3, A0E7, A0E8, A0ED, A0EE, A0EG, A0EK, A0EP, A0EQ, A0ET, A0EU, A0EV, A0EW, A0EY, A0EZ, A0F0, A0F1, A0F2, A0F3, A0F5, A0F6, A0F7, A0F9, A0FE, A0FH, A0FK, A0FM, A0FN, A0FR, A0FS, A0FT, A0FY, A0FZ, A0G0, A0G1, A0G2, A0G5, A0G6, A0G9, A0GE, A0GF, A0GG, A0GR, A0GS, A0GT, A0GU, A0GV, A0GW, A0GX, A0GY, A0H5, A0HA, A0HB, A0HC, A0HD, A0HE, A0HF, A0HG, A0HH, A0HI, A0HJ, A0HM, A0HN, A0HO, A0HQ, A0HS, A0HT, A0HU, A0HW, A0HX, A0HY, A0HZ, A0I0, A0I3, A0I6, A0I7, A0IF, A0IH, A0IJ, A0IK, A0IN, A0IU, A0IY, A0IZ, A0J8, A0J9, A0JB, A0JG, A0JH, A0JK, A0JL, A0JM, A0JO, A0JT, A0JU, A0JX, A0K0, A0K1, A0K8, A0K9, A0KC, A0KD, A0KF, A0KN, A0KO, A0KP, A0KQ, A0KR, A0KT, A0KU, A0KV, A0KY, A0L1, A0L2, A0L3, A0L7, A0LA, A0LR, A0LT, A0LU, A0LZ, A0M1, A0M2, A0MA, A0MB, A0MC, A0MG, A0MK, A0MN, A0MO, A0MQ, A0MR, A0MS, A0MT, A0MU, A0MV, A0MW, A0MX, A0MY, A0MZ, A0N0, A0N1, A0N2, A0N3, A0NJ, A0NK, A0NL, A0NX, A0NY, A0NZ, A0ON, A0OO, A0OT, A0OU, A0P0, A0P1, A0P5, A0P6, A0PO, A0PP, A0Q4, A0QG, A0QI, A0QP, A0QW, A0R2, A0R3, A0R4, A0R9, A0RD, A0RI, A0RJ, A0RL, A0RM, A0S8, A0SA, A0SC, A0SD, A0SI, A0SK, A0SM, A0SQ, A0SY, A0T0, A0T1, A0T4, A0T5, A0TL, A0TS, A0U3, A0U9, A0UN, A0UU, A0UZ, A0V2, A0VD, A0VY, A0VZ, A0W0, A0WA, A0WJ, A0WR, A0WX, A0X9, A0XA, A0XB, A0XD, A0XE, A0XG, A0XI, A0XJ, A0XL, A0XM, A0XO, A0XQ, A0XV, A0XW, A0XY, A0XZ, A0YB, A0YF, A0YG, A0YH, A0YQ, A0YZ, A0Z0, A0Z1, A0Z8, A0Z9, A0ZA, A0ZB, A0ZC, A0ZG, A0ZV, A103, A10E, A10F, A10G, A10K, A10N, A10O, A10S, A10Y, A112, A114, A117, A11M, A12G, A12H, A12L, A12M, A12W, A12Z, A13W, A147, A14C, A14M, A14N, A14R, A154, A155, A156, A157, A158, A159, A15J, A15K, A15L, A15M, A15N, A15S, A15T, A15W, A15X, A15Y, A16P, A175, A17I, A17K, A17O

## 4. 더미·트리거 스턴 전수 (강타 아님) — 115종

원작은 트리거가 더미 유닛(e…)을 만들고 `stomp`(AOws 전쟁발구르기)·`thunderbolt`(AHtb 폭풍망치)·`firebolt`(ANfb)·`clusterrockets`(ANcs)·`entanglingroots`(Aenr 뿌리묶기)를 시킨다. 우리 에셋은 그 트리거의 **피해만** 옮기고 스턴은 버렸다. 「원작 발동」 칸은 더미를 만드는 j 줄·트리거 이름·트리거를 HashAttack에 등록한 원작 유닛·직전 if 조건이다. 발동 명령이 같은 트리거 안에 실제로 있는 것만 올렸다(명령 없는 더미는 §6).
제안 공통형: 트리거 게이트에 대응하는 에셋 레벨 effects에 `{kind: Stun(1), target: 범위형이면 Enemies(2)·단일형이면 SingleTarget(3), duration: adur, chance: 1}` 추가. 범위(aare)는 우리 레벨 range와 다를 수 있다(SkillEffect에 개별 range 없음 — 다르면 메모).

| 원작 ID | 기반 | 이름 | adur | ahdu | aare | 원작 발동 | 우리 에셋 (현재) | 제안 | 확신도 |
|---|---|---|---|---|---|---|---|---|---|
| A05Q | ANfb | C파이어볼트-거불 유성1 | 3 | 3 | 빈칸 | e0B6 #불멸 주먹 유성군 더미1 ← L19101 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func004Func006C()) {}`; L19105 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func005Func004C()) {}` | [후보·원작유닛 경유] `원작023_h04C` OnHitCount[Damage ／ Damage×2] | Stun SingleTarget, duration 3초 | unknown |
| A06W | AOws | 2범위 시라초 스턴 | 2.15 | 1.18 | 600 | e04V @초월 시라호시 해역조정  ← L16232 `Sirahoshi_Attack` [H08U] 게이트 `GetUnitAbilityLevelSwapped('A0EQ',GetAttacker())`; L16236 `Sirahoshi_Attack` [H08U] 게이트 `GetUnitAbilityLevelSwapped('A0EQ',GetAttacker())` | 없음 | Stun Enemies, duration 2.15초 | —(에셋 없음) |
| A07C | AOws | 2범위 죠즈 스턴더미 | 0.9 | 0.14 | 500 | e090 U희귀함 죠즈 더미1 ← L14380 `Unique34` [h02E] 게이트 `if s__TrigVariables_Stage[GlobalTV]==8` | [명시] `더미채널_희귀함_황준석_1` OnHitChance[Damage]<br>`원작능력_희귀함_황준석` OnHitChance[Damage×7] | Stun Enemies, duration 0.9초 | confirmed |
| A07D | AOws | 2범위 드래곤전설 스턴 | 2.75 | 0.41 | 500 | e007 !전설 드래곤 더미1 ← L14722 `Legend3` [h02W] 게이트 `GetRandomInt(1,10)==6` | 없음 | Stun Enemies, duration 2.75초 | —(에셋 없음) |
| A07E | AOws | 2범위 시키전설 스턴 | 2.75 | 0.41 | 525 | e008 !전설 시키 더미 ← L14722 `Legend4` [h039,h07F] 게이트 `GetRandomInt(1,10)==5` | [명시] `더미채널_전설적인_김건_1` OnHitChance[Damage]<br>`원작능력_전설적인_신문철` OnHitChance[Damage×2] | Stun Enemies, duration 2.75초 | confirmed |
| A07K | AOws | 2범위 쿠마전설 스턴 | 0.6 | 0.09 | 500 | e00E !전설 쿠마 스턴 더미2 ← L14747 `Legend6` [h030] 게이트 `(Trig_Legend6_Func002C()) {GetRandomInt(1,10)==3`; L15158 `kuma_warp` [?] 게이트 ``<br>e0KD !전설 쿠마 스턴 더미 ← L14747 `Legend6` [h030] 게이트 `(Trig_Legend6_Func002C()) {GetRandomInt(1,10)==3`<br>e0KE !전설 쿠마 스턴 더미1 ← L14749 `Legend6` [h030] 게이트 `if(Trig_Legend6_Func003C()) {UNIT_STATE_MANA,Get` | [명시] `더미채널_전설적인_최상호_1` OnHitChance[Damage×2]<br>`더미채널_전설적인_최상호_2` OnHitCount[Damage]<br>`원작능력_전설적인_최상호` OnHitChance[Damage×10] | Stun Enemies, duration 0.6초 · ⚠️ aare 500 ≠ 레벨 range 0/450.0 | confirmed |
| A07L | AOws | 2범위 레일리전설 스턴 | 빈칸 | 0.45 | 500 | e00F !전설 쿠마 마나스킬 더미 ← L14751 `Legend6` [h030] 게이트 `if(Trig_Legend6_Func003C()) {UNIT_STATE_MANA,Get`<br>e00J !전설 레일리 마나스킬 더 ← L14756 `Legend7` [h03A] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=`; L18846 `LaillySkill3` [h056] 게이트 `(Trig_LaillySkill3_Func001C()) {}` | [명시] `더미채널_전설적인_박병규_1` OnHitCount[효과0]<br>`더미채널_전설적인_최상호_2` OnHitCount[Damage]<br>`원작능력_전설적인_진연서` OnHitCount[Damage×6]<br>`원작능력_전설적인_진연서_A0EN` OnHitChance[Damage]<br>`원작능력_전설적인_최상호` OnHitChance[Damage×10] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 500 ≠ 레벨 range 0/450.0 | unknown |
| A07P | AOws | 2범위 봉쿠레 히든 스턴 | 1.55 | 0.22 | 500 | e00N H히든 봉쿠레 더미 ← L14404 `Hidden2` [h03O] 게이트 `GetRandomInt(1,11)==3` | 없음 | Stun Enemies, duration 1.55초 | —(에셋 없음) |
| A07S | AHtb | C전설거프 맨손투포환 | 2.5 | 1.25 | 빈칸 | e00T !전설 거프 맨손투포환폭파 ← L14288 `Unique30` [h01Q] 게이트 `GetRandomInt(1,2)==1`<br>e00U !전설 거프 맨손투포환폭파 ← L14289 `Unique30` [h01Q] 게이트 `GetRandomInt(1,2)==1` | [후보·원작유닛 경유] `원작능력_희귀함_최상호_오타쿠의길` OnHitChance[Damage×4] | Stun SingleTarget, duration 2.5초 | unknown |
| A07T | AOws | 2범위 샹크스전설 스턴 | 2 | 0.3 | 600 | e00V !전설 샹크스 스턴 ← L14769 `Legend10` [h035] 게이트 `GetRandomInt(1,10)==3` | [명시] `더미채널_전설적인_임채민_1` OnHitChance[효과0]<br>`원작능력_전설적인_백기현` OnHitChance[Damage×2] | Stun Enemies, duration 2초 | confirmed |
| A07U | AOws | 2범위 후지전설 스턴 | 2.4 | 0.24 | 450 | e00B !전설 후지토라 더미2 ← L14796 `Legend11` [h031] 게이트 `GetRandomInt(1,7)==6`; L16540 `Huji01` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2`<br>e00Z !전설 후지토라 더미 ← L14796 `Legend11` [h031] 게이트 `GetRandomInt(1,7)==6` | [명시] `더미채널_전설적인_임채현_1` OnHitChance[Damage]<br>`원작능력_전설적인_임채현` OnHitChance[Damage×7] | Stun Enemies, duration 2.4초 · ⚠️ aare 450 ≠ 레벨 range 0/345.0 | confirmed |
| A07V | AOws | 2범위 라분 스턴 | 2.25 | 0.33 | 600 | e010 !전설 라분 박치기2 ← L14802 `Legend12` [h02Q] 게이트 `GetUnitUserData(s__TrigVariables__get_unitA(Glob`; L14805 `Legend12` [h02Q] 게이트 `GetRandomInt(1,100)<26` | 없음 | Stun Enemies, duration 2.25초 | —(에셋 없음) |
| A07Z | AOws | 2범위 이완코브히든 스턴 | 1.8 | 0.27 | 500 | e01B H히든 이완코브더미 ← L14405 `Hidden3` [h03Y] 게이트 `GetRandomInt(1,9)==3`; L14407 `Hidden3buggi` [h076] 게이트 `GetRandomInt(1,9)==3` | 없음 | Stun Enemies, duration 1.8초 | —(에셋 없음) |
| A081 | AOws | 2범위 레일리불멸 스턴 | 빈칸 | 0.45 | 500 | e01H #불멸 레일리 마나스킬 ← L18843 `LaillySkill3` [h056] 게이트 `(Trig_LaillySkill3_Func001C()) {}`; L18853 `LaillySkill3` [h056] 게이트 `GetUnitPointValue(udg_Hero_leily[3])<200` | [후보·원작유닛 경유] `원작026_h049` OnHitCount[ArmorBreak ／ ArmorBreak] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A086 | AOws | 2범위 피셔타이거 스턴더미 | 2 | 0.3 | 515 | e00O H히든 피셔타이거 더미1 ← L14412 `Hidden4` [h047] 게이트 `endif if GetRandomInt(1,10)==4` | 없음 | Stun Enemies, duration 2초 | —(에셋 없음) |
| A089 | AHtb | C히든 류마 오늬베기 | 0.4 | 0.2 | 빈칸 | e01P H히든 류마오늬베기 ← L14449 `Hidden8` [h044] 게이트 `GetRandomInt(1,7)==4` | 없음 | Stun SingleTarget, duration 0.4초 | —(에셋 없음) |
| A08A | AOws | 2범위 아오히든 | 1.35 | 0.22 | 415 | e01Q H히든 아오키지 더미 ← L14445 `Hidden6` [h041] 게이트 `GetRandomInt(1,10)==4` | 없음 | Stun Enemies, duration 1.35초 | —(에셋 없음) |
| A08D | AOws | 2범위 샹크스초월 패기 스턴 | 1.8 | 1.05 | 800 | e01T @초월 샹크스 패기더미1 ← L15921 `Shanks_Attack` [H08Z] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=`; L15997 `Shanks_skill_5` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1`<br>e0G2 @초월 샹크스 패기더미3 ← L14472 `Hidden12` [h03U] 게이트 `GetRandomInt(1,7)==3`; L15924 `Shanks_Attack` [H08Z] 게이트 `if GetRandomInt(1,10)==5` | [명시] `원작019_H08Z` OnHitCount[Damage+Stun] | Stun Enemies, duration 1.8초 · ⚠️ aare 800 ≠ 레벨 range 1100 | confirmed |
| A08E | AOws | 2범위 샹크스초월 스턴 | 2 | 1.1 | 800 | e01S @초월 샹크스 스턴1 ← L15924 `Shanks_Attack` [H08Z] 게이트 `if GetRandomInt(1,10)==3`<br>e0G1 @초월 샹크스 스턴 이펙 ← L15924 `Shanks_Attack` [H08Z] 게이트 `if GetRandomInt(1,10)==3`; L15924 `Shanks_Attack` [H08Z] 게이트 `if GetRandomInt(1,10)==3` | 없음 | Stun Enemies, duration 2초 | —(에셋 없음) |
| A08N | AOws | 2범위 시키불멸 마나스킬 | 빈칸 | 0.45 | 700 | e02X #불멸 시키 사자떨어트리기 ← L18927 `Shiki_Lion` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0` | [후보·원작유닛 경유] `회수_불멸_고도현_8eab1c6f` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A08O | AOws | 2범위 시키불멸 스턴 | 빈칸 | 0.45 | 600 | e026 #불멸 시키 더미 ← L18918 `Shiki_Attack` [h04B,h07R,h0BH,h0BI,h0BJ] 게이트 `if GetRandomInt(1,10)==3` | [명시] `더미채널_불멸_고도현_1` OnHitChance[Damage]<br>`원작능력_불멸_고도현` OnHitChance[Damage×6] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 600 ≠ 레벨 range 0/450.0 | unknown |
| A08U | AOws | 2범위 거프불멸주먹스턴 | 2.5 | 0.41 | 500 | e01G #불멸 거프주먹더미 ← L19081 `Garp_AttackDamage` [?] 게이트 `if GetRandomInt(1,10)==6`<br>e0B6 #불멸 주먹 유성군 더미1 ← L19101 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func004Func006C()) {}`; L19105 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func005Func004C()) {}` | [후보·원작유닛 경유] `원작023_h04C` OnHitCount[Damage ／ Damage×2]<br>`회수_불멸_이승우_355f5d39` OnHitChance[Damage×2] | Stun Enemies, duration 2.5초 · ⚠️ aare 500 ≠ 레벨 range 360.0/650 | unknown |
| A08V | AOws | 2범위 드래곤불멸스턴더미 | 빈칸 | 0.45 | 525 | e02L #불멸 드래곤스턴2 ← L19020 `Dragon_Attack` [h04D,h08H] 게이트 `endif if GetRandomInt(1,10)==6` | [명시] `더미채널_불멸_정준영_1` OnHitChance[효과0]<br>`원작024_h04D` OnHitChance[Damage×2 ／ Damage×3]<br>`원작능력_불멸_김용태` OnHitChance[Damage×4]<br>`원작능력_불멸_김용태_A08V` OnHitChance[Damage] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 525 ≠ 레벨 range 0/475/500.0 | unknown |
| A096 | AOws | 2범위 바르토전설 스턴 | 2.75 | 0.41 | 550 | e020 !전설 바르토로메오 가드1 ← L14843 `Legend15` [h02Z] 게이트 `GetRandomInt(1,10)==3` | [후보·원작유닛 경유] `원작능력_전설적인_박민수` OnHitChance[Damage] | Stun Enemies, duration 2.75초 | unknown |
| A098 | AOws | 2범위 흰불스턴더미1 | 빈칸 | 1.65 | 625 | e03C #불멸 흰수염지진펀치 더미 ← L14740 `Legend51` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3`; L19172 `Ed_Skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3` | [후보·원작유닛 경유] `게이트_불멸_고도현_caa364ec` OnHitCount[Damage×4]<br>`게이트_전설적인_최상호_355f5d39` OnHitChance[Damage×2] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A09B | AHtb | C모몬가 거합베기 | 1.5 | 1 | 빈칸 | e03N U희귀함 모몬가 더미 ← L14389 `Unique35` [h01Y] 게이트 `endif if s__TrigVariables_Stage[GlobalTV]==1` | 없음 | Stun SingleTarget, duration 1.5초 | —(에셋 없음) |
| A09I | AOws | 2범위 로빈초스턴더미 | 2.85 | 0.42 | 525 | e04R @초월 로빈 마나 ← L16214 `Robine_skill_2` [?] 게이트 `` | [후보·원작유닛 경유] `게이트_초월_강재규_AP_85d0117d` OnHitCount[Damage] | Stun Enemies, duration 2.85초 | unknown |
| A09Q | ANcs | 0장풍 기간트개틀링1 | 1 | 1 | 405 | e04Q @초월 루피 개틀링 더미 ← L15893 `Ruffy_Attack` [H099] 게이트 `UnitHasBuffBJ(GetAttacker(),'B06Y')==false` | [명시] `더미채널_초월_신문철_AP_79행_10000` OnHitChance[Damage×2]<br>`원작013_H099` OnHitChance[ApplyBuff+AttackPowerBuffFlat] | Stun Enemies, duration 1초 | confirmed |
| A09R | AOws | 2범위 루피초월 마나스턴더미1 | 2.5 | 0.37 | 600 | e04W @초월 루피 레드호크 더미 ← L15914 `Ruffy_Mana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L15916 `Ruffy_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==4`<br>e04Z @초월 루피 레드호크 더미 ← L15917 `Ruffy_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==4`<br>e0PI @초월 루피 레드호크 더미 ← L15915 `Ruffy_Mana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L15915 `Ruffy_Mana` [?] 게이트 `if(Trig_Ruffy_Mana_Func016C()) {}` | [후보·원작유닛 경유] `게이트_초월_신문철_AP_55ecbfb2` OnHitCount[Damage×2] | Stun Enemies, duration 2.5초 · ⚠️ aare 600 ≠ 레벨 range 625.0 | unknown |
| A09S | AOws | 2범위 호크 개틀링 더미 | 1.5 | 0.22 | 500 | e050 @초월 루피 호크 개틀링  ← L15900 `Ruffy_Attack` [H099] 게이트 `GetRandomInt(1,8)==5`<br>e051 @초월 루피 호크 개틀링  ← L15899 `Ruffy_Attack` [H099] 게이트 `endif endif if UnitHasBuffBJ(GetAttacker(),'B00S` | [명시] `원작013_H099_A09S` OnHitChance[Damage+Stun] | Stun Enemies, duration 1.5초 | confirmed |
| A09Z | AHtb | C 상디 특별함 단일 데미지 | 0.01 | 0.01 | 빈칸 | e05D S특별함 상디 더미 ← L14121 `Speical6` [h013] 게이트 `GetRandomInt(1,5)==3` | [명시] `더미채널_특별함_조성진_1` OnHitChance[Damage]<br>`원작능력_특별함_조성진` OnHitChance[Damage] | **반영 불필요** — adur 0.01초는 피해 전달용 더미(스턴 사실상 없음) | confirmed |
| A0A8 | AOws | 2범위 제트불멸 마나스킬 | 빈칸 | 3 | 500 | e068 #불멸 Z 마나스킬 더미  ← L19125 `Z_Skill_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0AA | AOws | 2범위 써니호 스턴 | 빈칸 | 2.25 | 600 | e06G H히든 써니호 어흥포 더미 ← L14461 `Hidden11` [h03L] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=`; L14465 `Hidden11` [h03L] 게이트 `GetUnitPropWindowBJ(GetTriggerUnit())==65.00` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0AO | AOws | 2범위 브룩마나스킬 | 빈칸 | 0.45 | 600 | e06V @초월 브룩 마나스킬 ← L17071 `Brook_Skill_Mana` [?] 게이트 `` | [후보·원작유닛 경유] `원작018_H09I` OnHitCount[효과0] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A0AW | AOws | 2범위 나생문 스턴더미 | 2.5 | 0.37 | 500 | e089 @초월 이도류 나생문 더미 ← L16897 `Zoro_tiger` [?] 게이트 `if(Trig_Zoro_tiger_Func004C()) {}`; L16897 `Zoro_tiger` [?] 게이트 `if(Trig_Zoro_tiger_Func004C()) {}`<br>e0K0 @초월 이도류 나생문 더미 ← L16958 `Zoro_Samchun2` [h07G] 게이트 `(Trig_Zoro_Samchun2_Func003Func007C()) {}` | [후보·원작유닛 경유] `게이트_초월_박민수_AD_b5c9b49a` OnHitCount[Damage×2] | Stun Enemies, duration 2.5초 · ⚠️ aare 500 ≠ 레벨 range 525.0 | unknown |
| A0B3 | AOws | 2범위 캐번디시 스턴 | 2.35 | 0.31 | 500 | e08G E영원 캐번디시 1스킬 더 ← L19709 `Cavendish_Attack` [h05B] 게이트 `if GetRandomInt(1,10)==6` | [명시] `더미채널_영원_최상호_1` OnHitChance[Damage]<br>`원작능력_영원_최상호` OnHitChance[Damage×14]<br>`원작능력_영원_최상호_A0EA` OnHitChance[Damage]<br>`원작능력_영원_최상호_A0HV` OnHitChance[Damage] | Stun Enemies, duration 2.35초 · ⚠️ aare 500 ≠ 레벨 range 0/600.0 | confirmed |
| A0BD | AOws | 2범위 시라호시 다이브 | 빈칸 | 3 | 700 | e0BC @초월 넵튠 다이브 2 ← L16260 `Sirahoshi_skill_Mana` [?] 게이트 `if(Trig_Sirahoshi_skill_Mana_Func003C()) {}`<br>e0BD @초월 넵튠 다이브 3 ← L16260 `Sirahoshi_skill_Mana` [?] 게이트 `if(Trig_Sirahoshi_skill_Mana_Func003C()) {}` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0BF | AHtb | C 반더데켄 느와르1 | 2 | 1 | 빈칸 | e093 H히든 반더데켄 더미1 ← L14489 `deken_Mana` [?] 게이트 `(Trig_deken_Mana_Func003C()) {}` | [후보·원작유닛 경유] `게이트_히든_최윤서_3c5f8bd9` OnHitCount[Damage] | Stun SingleTarget, duration 2초 | unknown |
| A0BG | AOws | 2범위 반더데켄 느와르 스턴더 | 빈칸 | 3 | 800 | e036 H히든 반더데켄 폭발 ← L14491 `deken_Mana` [?] 게이트 `(Trig_deken_Mana_Func004Func002C()) {}` | [후보·원작유닛 경유] `게이트_히든_최윤서_3c5f8bd9` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A0BH | AOws | 2범위 페로나 스턴 | 빈칸 | 0.45 | 500 | e098 H히든 페로나 마나 더미3 ← L14558 `perona_Mana` [?] 게이트 `if(Trig_perona_Mana_Func004C()) {}`; L19098 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func004Func006C()) {}`<br>e0PV H히든 페로나 마나 더미2 ← L14558 `perona_Mana` [?] 게이트 `if(Trig_perona_Mana_Func004C()) {}` | [후보·원작유닛 경유] `원작023_h04C` OnHitCount[Damage ／ Damage×2] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 500 ≠ 레벨 range 650 | unknown |
| A0BS | AOws | 2범위 아오초 강스턴 | 2.5 | 1.35 | 675 | e09J @초월 아오키지 아이스에이 ← L12831 `Bustercall_Etc3` [?] 게이트 ``; L12831 `Bustercall_Etc3` [?] 게이트 ``<br>e09L @초월 아오키지 아이스에이 ← L12831 `Bustercall_Etc3` [?] 게이트 ``; L16834 `Aokigi_Attack` [H097,H0B1] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())>`<br>e0QX @초월 아오키지 아이스에이 ← L12831 `Bustercall_Etc3` [?] 게이트 ``; L16834 `Aokigi_Attack` [H097,H0B1] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())>` | 없음 | Stun Enemies, duration 2.5초 | —(에셋 없음) |
| A0BT | AOws | 2범위 아오초 약스턴 | 2.3 | 1.25 | 550 | e09K @초월 아오키지 아이스에이 ← L16862 `Aokigi_skill_3` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e09M @초월 아오키지 아이스에이 ← L16865 `Aokigi_skill_3` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==1`; L16866 `Aokigi_skill_3` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==2` | 없음 | Stun Enemies, duration 2.3초 | —(에셋 없음) |
| A0BW | AOws | 2범위 키자루초월 빛의 기둥 | 2.75 | 1.5 | 500 | e09S @초월 키자루 빛의 기둥1 ← L16795 `Kizaru_Attack` [H0B5] 게이트 `if GetRandomInt(1,12)==10`<br>e0HD @초월 키자루 빛의 기둥 ← L16795 `Kizaru_Attack` [H0B5] 게이트 `if GetRandomInt(1,12)==10` | [후보·원작유닛 경유] `회수_초월_구주호_AD_c453ba09` OnHitChance[Damage] | Stun Enemies, duration 2.75초 · ⚠️ aare 500 ≠ 레벨 range 385.0 | unknown |
| A0C1 | AOws | 2범위 화염용왕  | 0.85 | 0.85 | 600 | e08O @초월 사보 화염용왕4 ← L16192 `Sabo_Skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3` | [후보·원작유닛 경유] `절대쿨_초월_두유찬_AD_1` OnHitCount[Damage] | Stun Enemies, duration 0.85초 · ⚠️ aare 600 ≠ 레벨 range 475 | unknown |
| A0C2 | AHtb | C사보초월 단일 | 1.85 | 0.93 | 빈칸 | e049 @초월 사보 작열더미1 ← L16202 `Sabo_Skill_4` [?] 게이트 `` | [후보·원작유닛 경유] `절대쿨_초월_두유찬_AD_4` OnHitCount[Damage×2] | Stun SingleTarget, duration 1.85초 | unknown |
| A0C3 | AOws | 2범위 사보초월 마나스턴더미 | 1.25 | 1.25 | 500 | e09U @초월 사보 용의 숨결더미 ← L16181 `Sabo_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2`<br>e09W @초월 사보 용의 숨결더미 ← L16180 `Sabo_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2` | [후보·원작유닛 경유] `게이트_초월_두유찬_AD_8eab1c6f` OnHitCount[Damage×2] | Stun Enemies, duration 1.25초 | unknown |
| A0CE | AOws | 2범위 기공장스턴더미 | 2.5 | 0.37 | 650 | e097 H히든 기공장더미3 ← L14590 `koalla_skill_Mana` [?] 게이트 `if(Trig_koalla_skill_Mana_Func004C()) {}`<br>e0AQ H히든 기공장더미2 ← L14589 `koalla_skill_Mana` [?] 게이트 `if(Trig_koalla_skill_Mana_Func004C()) {}`<br>e0AR H히든 기공장더미4 ← L14589 `koalla_skill_Mana` [?] 게이트 `if(Trig_koalla_skill_Mana_Func004C()) {}`; L14592 `koalla_skill_Mana` [?] 게이트 `if(Trig_koalla_skill_Mana_Func005C()) {}` | [후보·원작유닛 경유] `게이트_히든_황정기_2a646778` OnHitCount[Damage×3] | Stun Enemies, duration 2.5초 · ⚠️ aare 650 ≠ 레벨 range 625.0 | unknown |
| A0EL | AOws | 2범위 임팩트다이얼-우솝희귀 | 1.15 | 0.18 | 485 | e0AT U희귀함 우솝 더미 ← L14235 `Unique23` [h02C] 게이트 `GetRandomInt(1,10)==3` | [명시] `더미채널_희귀함_유재헌_1` OnHitChance[Damage]<br>`원작능력_희귀함_유재헌` OnHitChance[Damage] | Stun Enemies, duration 1.15초 | confirmed |
| A0K2 | AOws | 2범위 바제스 스턴 | 0.9 | 0.13 | 500 | e0BZ U희귀함 바제스 더미 ← L14243 `Unique27` [h01V] 게이트 `GetRandomInt(1,10)==3` | [명시] `더미채널_희귀함_이용민_1` OnHitChance[Damage]<br>`원작능력_희귀함_이용민` OnHitChance[Damage] | Stun Enemies, duration 0.9초 | confirmed |
| A0LF | AOws | 2범위 대옥나선환 | 2.85 | 0.42 | 600 | e0CL R랜유 대옥나선환 더미 ← L17769 `Naruto1` [h06V] 게이트 `if(Trig_Naruto1_Func002C()) {GetRandomInt(1,20)=` | [명시] `더미채널_랜덤_이즈미_신이치_1` OnHitChance[Damage×2]<br>`원작능력_랜덤_이즈미_신이치` OnHitChance[Damage×11]<br>`원작능력_랜덤_이즈미_신이치_A0LG` OnHitChance[Damage] | Stun Enemies, duration 2.85초 · ⚠️ aare 600 ≠ 레벨 range 0/375.0 | confirmed |
| A0LJ | AOws | 2범위 미닛츠 스파이크 | 빈칸 | 0.45 | 460 | e0CQ R랜유 k 더미 4 미닛츠 ← L17801 `k1` [h06U] 게이트 `(Trig_k1_Func001C()) {GetRandomInt(1,33)==5}`<br>e0CT R랜유 k 더미 3 미닛츠 ← L17799 `k1` [h06U] 게이트 `(Trig_k1_Func001C()) {GetRandomInt(1,33)==5}` | [명시] `더미채널_랜덤_카마도_탄지로_1` OnHitChance[효과0]<br>`원작능력_랜덤_호시노_아이` OnHitChance[Damage×13]<br>`원작능력_랜덤_호시노_아이_A0KT` OnHitChance[Damage] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 460 ≠ 레벨 range 0/500.0 | unknown |
| A0LV | AHtb | 4지정 멀티헤드샷 | 1 | 1 | 빈칸 | e0CW E영원 비비 더블샷 더미1 ← L19792 `vivi_Skill_Double` [?] 게이트 `if(Trig_vivi_Skill_Double_Func003C()) {}`; L19795 `vivi_Skill_Double` [?] 게이트 `if(Trig_vivi_Skill_Double_Func004C()) {}` | 없음 | Stun SingleTarget, duration 1초 | —(에셋 없음) |
| A0LW | AHtb | 4지정 멀티헤드샷 | 1 | 1 | 빈칸 | e0CY E영원 비비 더블샷 더미2 ← L19792 `vivi_Skill_Double` [?] 게이트 `if(Trig_vivi_Skill_Double_Func003C()) {}`; L19795 `vivi_Skill_Double` [?] 게이트 `if(Trig_vivi_Skill_Double_Func004C()) {}` | 없음 | Stun SingleTarget, duration 1초 | —(에셋 없음) |
| A0LX | AOws | 2범위 에넬히든 뇌영 | 2 | 0.3 | 600 | e037 H히든 방주맥심 뇌영 더미 ← L14458 `Hidden9` [h03X] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=` | 없음 | Stun Enemies, duration 2초 | —(에셋 없음) |
| A0MD | AOws | 2범위 맥시멈 스윙 | 2.5 | 0.37 | 500 | e0DH H히든 베르고 더미2 ← L14672 `Hidden19` [h03W] 게이트 `if GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker(` | 없음 | Stun Enemies, duration 2.5초 | —(에셋 없음) |
| A0NP | AOws | 2범위 염제 스턴 | 빈칸 | 2.1 | 860 | e06O E영원 에이스 염제2 ← L19556 `ace_skill_5` [?] 게이트 `if(Trig_ace_skill_5_Func004C()) {}`<br>e0AM E영원 에이스 염제1 ← L19550 `ace_skill_5` [?] 게이트 `(Trig_ace_skill_5_Func002C()) {}`<br>e0IO E영원 에이스 염제4 ← L19552 `ace_skill_5` [?] 게이트 `(Trig_ace_skill_5_Func002C()) {}` | [후보·원작유닛 경유] `게이트_영원_윤현모_aa9cf1bd` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 860 ≠ 레벨 range 850.0 | unknown |
| A0O4 | AOws | 2범위 조로초월 마나 | 빈칸 | 3 | 600 | e08A @초월 이도류 나생문 더미 ← L16955 `Zoro_Samchun2` [h07G] 게이트 `(Trig_Zoro_Samchun2_Func003Func007C()) {}`; L16958 `Zoro_Samchun2` [h07G] 게이트 `(Trig_Zoro_Samchun2_Func003Func007C()) {}`<br>e0DS @초월 조로 사자의 노래더 ← L16934 `Zoro_samchun1` [?] 게이트 `(Trig_Zoro_samchun1_Func002C()) {}`<br>e0JD @초월 조로 리메이크 4 ← L16936 `Zoro_samchun1` [?] 게이트 `(Trig_Zoro_samchun1_Func002C()) {}`; L16947 `Zoro_Samchun2` [h07G] 게이트 `(Trig_Zoro_Samchun2_Func002C()) {}` | [후보·원작유닛 경유] `게이트_초월_박민수_AD_b5c9b49a` OnHitCount[Damage×2] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 600 ≠ 레벨 range 525.0 | unknown |
| A0OW | AOws | 2범위 후지초월스턴 | 2.6 | 0.39 | 475 | e074 @초월 후지초월1 ← L16539 `Huji01` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2`<br>e076 @초월 후지초월22 ← L12842 `Bustercall_Etc4` [?] 게이트 `(Trig_Bustercall_Etc4_Func003C()) {}`; L16636 `Huji_meteor` [?] 게이트 `(Trig_Huji_meteor_Func002C()) {}`<br>e0OB @초월 후지초월2 ← L14796 `Legend11` [h031] 게이트 `GetRandomInt(1,7)==6`; L16540 `Huji01` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2` | [후보·원작유닛 경유] `게이트_초월_양재모_AD_29846395` OnHitCount[Damage×4] | Stun Enemies, duration 2.6초 · ⚠️ aare 475 ≠ 레벨 range 485.0 | unknown |
| A0OX | AOws | 2범위 후지초월 2스턴더미1 | 빈칸 | 0.45 | 475 | e07D @초월 후지초월 2스킬 4 ← L16575 `Huji02DM` [H08X] 게이트 `` | [후보·원작유닛 경유] `게이트_초월_양재모_AD_29846395` OnHitCount[Damage×4] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 475 ≠ 레벨 range 485.0 | unknown |
| A0PC | AHtb | C루피희귀 스턴 | 2 | 1 | 빈칸 | e0BR U희귀함 루피 더미 3 ← L14320 `Unique32` [h01X] 게이트 `if(Trig_Unique32_Func005C()) {}`; L14322 `Unique32` [h01X] 게이트 `if(Trig_Unique32_Func006C()) {}` | [명시] `더미채널_희귀함_구주호_1` OnHitChance[효과0]<br>`원작능력_희귀함_장태영` OnHitChance[Damage×4] | Stun SingleTarget, duration 2초 | confirmed |
| A0PL | AOws | 2범위 센불 스턴1 | 2.85 | 0.42 | 525 | e02B #불멸 센고쿠 여래신장 더 ← L18983 `Sengoku_Attack` [h04E] 게이트 `eBJ(GetAttacker(),(GetUnitStateSwap(UNIT_STATE_L` | [명시] `더미채널_불멸_이이삭_1` OnHitChance[Damage]<br>`원작021_h04E` OnHitCount[Damage×2 ／ 효과0]<br>`원작능력_불멸_이이삭` OnHitChance[Damage×3] | Stun Enemies, duration 2.85초 | confirmed |
| A0PN | AOws | 2범위 에넬제한 뇌영1 | 빈칸 | 0.45 | 800 | e07N %제한됨 에넬 뇌영 5 ← L15249 `Enel_Mana` [?] 게이트 `if(Trig_Enel_Mana_Func004C()) {}` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0PU | AOws | 2범위 금강보도 | 2.5 | 0.37 | 550 | e0EM %제한됨 크로커 금강보도2 ← L15213 `cro_skill_2` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1` | [후보·원작유닛 경유] `게이트_제한_김강민_948ad1b7` OnHitChance[Damage] | Stun Enemies, duration 2.5초 | unknown |
| A0PX | AOws | 2범위 아오희귀 | 0.95 | 0.18 | 400 | e057 U희귀함 아오키지 더미 ← L14193 `Unique11` [h02B] 게이트 `GetRandomInt(1,12)==3` | [명시] `더미채널_희귀함_윤현모_1` OnHitChance[Damage]<br>`원작능력_희귀함_윤현모` OnHitChance[Damage] | Stun Enemies, duration 0.95초 | confirmed |
| A0Q0 | AOws | 2범위 핸영 스턴 | 2.75 | 0.42 | 650 | e0AC E영원 핸콕 패기더미1 ← L19596 `hancock_skill_10` [?] 게이트 `` | [후보·원작유닛 경유] `게이트_영원_김영원_b3948c74` OnHitChance[Damage] | Stun Enemies, duration 2.75초 · ⚠️ aare 650 ≠ 레벨 range 600.0 | unknown |
| A0QH | AHtb | C조로희귀 스턴 | 2 | 1 | 빈칸 | e0BT U희귀함 조로 더미 2 ← L14359 `Unique33` [h02F] 게이트 `if(Trig_Unique33_Func008C()) {}`; L14360 `Unique33` [h02F] 게이트 `if(Trig_Unique33_Func008C()) {}` | [명시] `더미채널_희귀함_정내연_1` OnHitChance[Damage]<br>`원작능력_희귀함_정내연` OnHitChance[Damage] | Stun SingleTarget, duration 2초 | confirmed |
| A0QN | AOws | 2범위 파퓸페뮤르 스턴 | 2.5 | 2.5 | 785 | e0AH E영원 핸콕 파퓸페뮤르0 ← L19616 `hancock_skill_Mana` [?] 게이트 `if(Trig_hancock_skill_Mana_Func003C()) {}` | [후보·원작유닛 경유] `원작028_h05C` OnHitCount[Damage×3 ／ Damage×3] | Stun Enemies, duration 2.5초 · ⚠️ aare 785 ≠ 레벨 range 760 | unknown |
| A0QU | AOws | 2범위 도움소 스턴 | 빈칸 | 0.45 | 600 | e0EV 더미-도움소지진 ← L12781 `Absolb1` [?] 게이트 `if(Trig_Absolb1_Func004C()) {}` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0R8 | AOws | 2범위 이완 데스윙크 | 1.2 | 0.18 | 500 | e03O U희귀함 이완코브 더미 ← L14212 `Unique18` [h02A] 게이트 `GetRandomInt(1,16)==3` | [명시] `더미채널_희귀함_이재윤_1` OnHitChance[Damage]<br>`원작능력_희귀함_이재윤` OnHitChance[Damage] | Stun Enemies, duration 1.2초 | confirmed |
| A0S1 | AOws | 2범위 샹크스초월 특성스턴 | 2.5 | 1.4 | 1100 | e01U @초월 샹크스 패기더미4 ← L15920 `Shanks_Attack` [H08Z] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=`; L15925 `Shanks_Attack` [H08Z] 게이트 `if GetRandomInt(1,10)==5` | [명시] `원작019_H08Z` OnHitCount[Damage+Stun] | Stun Enemies, duration 2.5초 | confirmed |
| A0S3 | AOws | 2범위 파이어버드 스턴 | 빈칸 | 0.45 | 600 | e06P @초월 우솝 파이어버드2 ← L16790 `Usop_Skill_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1` | [후보·원작유닛 경유] `게이트_초월_조성진_AD_29846395` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A0TI | AOws | 2범위 카이도우마나스턴 | 3.5 | 3.5 | 900 | e0GS #불멸 카이도우 레이저 ← L19376 `Kaido_Mana` [h07O] 게이트 `if(Trig_Kaido_Mana_Func003C()) {}`; L19378 `Kaido_Mana` [h07O] 게이트 `(Trig_Kaido_Mana_Func004Func001C()) {}` | 없음 | Stun Enemies, duration 3.5초 | —(에셋 없음) |
| A0TO | AHtb | C 희귀함 베이비 단일 | 0.01 | 0.01 | 빈칸 | e0EN U희귀함 베이비 더미 ← L14263 `Unique29` [h02M] 게이트 `(Trig_Unique29_Func003Func002C()) {}`; L14269 `Unique29` [h02M] 게이트 `if(Trig_Unique29_Func004C()) {}` | [명시] `더미채널_희귀함_임채민_1` OnHitChance[Damage]<br>`원작능력_희귀함_임채민` OnHitChance[Damage×5] | **반영 불필요** — adur 0.01초는 피해 전달용 더미(스턴 사실상 없음) | confirmed |
| A0UE | AHtb | 4지정 샹전패기장판단일 | 0.62 | 0.62 | 빈칸 | e0IB !전설 핸콕 석화! ← L19640 `hancock_petrification` [?] 게이트 ``; L19646 `Legend14han_petrification1` [?] 게이트 `ication1_Actions takes nothing returns nothing i`<br>e0PD !전설 샹크스 이벤트패기장 ← L15936 `Shanks_ET_pegi1` [?] 게이트 `(Trig_Shanks_ET_pegi1_Func001C()) {}`; L15938 `Shanks_ET_pegi1` [?] 게이트 `(Trig_Shanks_ET_pegi1_Func001Func001Func001C()) ` | 없음 | Stun SingleTarget, duration 0.62초 | —(에셋 없음) |
| A0UO | AHtb | C 염계 스턴 | 2 | 2 | 빈칸 | e0E2 %제한변화 에이스 불기둥1 ← L15185 `Transpom_AceMana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L19534 `ace_skill_3` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1` | [후보·원작유닛 경유] `게이트_변화됨_박은석_8eab1c6f` OnHitCount[Damage]<br>`게이트_영원_윤현모_d319dd11` OnHitChance[Damage] | Stun SingleTarget, duration 2초 | unknown |
| A0UP | ANfb | C파이어볼트-염계 | 빈칸 | 1 | 빈칸 | e030 %제한변화 에이스 불주먹1 ← L15192 `Transpom_Ace_bulRemake` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e09X %제한변화 에이스 불주먹2 ← L15195 `Transpom_Ace_bulRemake` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e0A0 %제한변화 에이스 불기둥2 ← L15189 `Transpom_AceMana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==2`; L19536 `ace_skill_3` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==2` | [명시] `더미채널_변화됨_박은석_79행_01000` OnHitChance[Damage] | Stun SingleTarget, duration 기반 기본값(미확인) | unknown |
| A0UQ | ANfb | C파이어볼트계열-조로전설사자의 | 1 | 0.5 | 빈칸 | e013 !전설 조로 사자의 노래2 ← L14818 `Legend13_2` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e014 !전설 조로 사자의 노래0 ← L14819 `Legend13_2` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e0JG !전설 조로 리메이크6 ← L16905 `Zoro_saza1` [?] 게이트 `(Trig_Zoro_saza1_Func002C()) {}`; L16908 `Zoro_saza1` [?] 게이트 `(Trig_Zoro_saza1_Func002Func042C()) {}` | [후보·원작유닛 경유] `게이트_전설적인_정준영_3a13fd3d` OnHitChance[Damage×4] | Stun SingleTarget, duration 1초 | unknown |
| A0UX | ANfb | C파이어볼트-아카히든 | 2.25 | 2.25 | 빈칸 | e0EI H히든 아카이누 더미3 ← L14415 `Hidden5` [h03Z] 게이트 `GetRandomInt(1,7)==4`; L14441 `Akainu_02_hidden` [?] 게이트 `(Trig_Akainu_02_hidden_Func019Func001C()) {}`<br>e0K4 @초월 아카이누 용암분출 ← L16171 `Akainu_03` [?] 게이트 `` | [후보·원작유닛 경유] `게이트_히든_호치킨_21297197` OnHitChance[Damage×3] | Stun SingleTarget, duration 2.25초 | unknown |
| A0V9 | ANfb | C파이어볼트-로빈초월스턴 | 3 | 3 | 빈칸 | e04O @초월 로빈 클러치 ← L16216 `Robine_skill_3` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1` | [후보·원작유닛 경유] `게이트_초월_강재규_AP_fcba0856` OnHitCount[Damage] | Stun SingleTarget, duration 3초 | unknown |
| A0VL | ANfb | C파이어볼트-아카초월 | 3 | 3 | 빈칸 | e04I @초월 아카이누 대분화2 ← L14440 `Akainu_02_hidden` [?] 게이트 `(Trig_Akainu_02_hidden_Func019Func001C()) {}`; L16165 `Akainu_02` [?] 게이트 `(Trig_Akainu_02_Func019Func001C()) {}` | [후보·원작유닛 경유] `게이트_히든_호치킨_21297197` OnHitChance[Damage×3]<br>`회수_초월_김만경_AD_0ac0451e` OnHitCount[Damage] | Stun SingleTarget, duration 3초 | unknown |
| A0VV | ANfb | C파이어볼트계열 | 1.75 | 0.88 | 빈칸 | e0HZ !전설 시노부 더미4 ← L14982 `Legend30` [h042,h085] 게이트 `endif if GetRandomInt(1,14)==4`; L14986 `Legend30` [h042,h085] 게이트 `GetRandomInt(1,4)==3`<br>e0P8 !제한됨 시노부 마나 더미 ← L15679 `Sinobu_Skill_hp2` [h086,h09O,h09P] 게이트 `s__TrigVariables_Stage[GlobalTV]==3` | [명시] `원작능력_전설적인_이일중` OnHitChance[Damage×7]<br>`원작능력_제한_임준성` OnHitChance[Damage×10] | Stun SingleTarget, duration 1.75초 | confirmed |
| A0W5 | AOws | 2범위 레일리불멸 스턴 | 빈칸 | 0.45 | 500 | e00Q #불멸 레일리 마나스킬 1 ← L18846 `LaillySkill3` [h056] 게이트 `(Trig_LaillySkill3_Func001C()) {}`; L18852 `LaillySkill3` [h056] 게이트 `GetUnitPointValue(udg_Hero_leily[3])<200` | [후보·원작유닛 경유] `원작026_h049` OnHitCount[ArmorBreak ／ ArmorBreak] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A0WC | ANfb | C파이어볼트계열-호킨스 | 빈칸 | 빈칸 | 빈칸 | e0H3 @초월 호킨스 트리플2 ← L17136 `Hokins_Skill_3` [?] 게이트 `(Trig_Hokins_Skill_3_Func002C()) {}`; L17138 `Hokins_Skill_3` [?] 게이트 `if(Trig_Hokins_Skill_3_Func003C()) {}` | 없음 | Stun SingleTarget, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0WF | AOws | 2범위 호킨스 마나스턴 | 빈칸 | 0.45 | 500 | e0GX @초월 호킨스 스트레이트4 ← L17116 `Hokins_mana` [?] 게이트 `(Trig_Hokins_mana_Func002C()) {}` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0WW | AOws | 2범위 시라초 스턴 | 2.35 | 2.35 | 600 | e0KA @초월 시라호시 다이브 ← L16234 `Sirahoshi_Attack` [H08U] 게이트 `GetUnitAbilityLevelSwapped('A0EQ',GetAttacker())`; L16235 `Sirahoshi_Attack` [H08U] 게이트 `GetUnitAbilityLevelSwapped('A0EQ',GetAttacker())` | 없음 | Stun Enemies, duration 2.35초 | —(에셋 없음) |
| A0X8 | AOws | 2범위 징베초 스턴 | 빈칸 | 0.45 | 600 | e0KM @초월 징베 무뢰관3 ← L17268 `Jimbe_Mu` [?] 게이트 `(Trig_Jimbe_Mu_Func002Func001C()) {}`; L17269 `Jimbe_Mu` [?] 게이트 `(Trig_Jimbe_Mu_Func002Func001C()) {}` | [후보·원작유닛 경유] `게이트_초월_최상호_AD_3c5f8bd9` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A0Y8 | AOws | 2범위 미나토 스턴 | 2.75 | 0.43 | 500 | h0A0 #더미 금-미나토스턴 ← L18013 `Minato_Q2` [h09O,h0A0] 게이트 `s__TrigVariables_Stage[GlobalTV]==11`; L18051 `Minato_Mana` [h09O,h09P,h0A0] 게이트 `s__TrigVariables_Stage[GlobalTV]==2` | [후보·원작유닛 경유] `게이트_랜덤_이타도리_유지_18aa2343` OnHitChance[Damage]<br>`게이트_랜덤_이타도리_유지_a8343962` OnHitCount[Damage] | Stun Enemies, duration 2.75초 | unknown |
| A0YA | AHtb | 4지정 죠타로 단일 스턴 | 빈칸 | 5 | 빈칸 | h0A3 #더미 죠타로 단일스턴1 ← L17925 `Jotaro_W` [h09O,h09S,h0A3] 게이트 `s__TrigVariables_Stage[GlobalTV]==14`; L17946 `Jotaro_E` [h09O,h09S,h0A3] 게이트 `s__TrigVariables_Stage[GlobalTV]==2` | 없음 | Stun SingleTarget, duration 기반 기본값(미확인) | —(에셋 없음) |
| A0YD | AOws | 2범위 오뎅 스턴 | 2.25 | 2.25 | 600 | e0MC E영원 오뎅 토츠카 1 ← L20097 `Odeng_Skill_mana` [h09O] 게이트 `if(Trig_Odeng_Skill_mana_Func003C()) {}`; L20099 `Odeng_Skill_mana` [h09O] 게이트 `if(Trig_Odeng_Skill_mana_Func003C()) {}` | [후보·원작유닛 경유] `게이트_영원_서민성_b5c9b49a` OnHitCount[Damage] | Stun Enemies, duration 2.25초 | unknown |
| A0YL | AOws | 2범위 미나토 나선환 | 빈칸 | 0.45 | 600 | h0A4 #더미 금-미나토 나선환스 ← L18037 `Minato_E` [h09O,h0A4] 게이트 `s__TrigVariables_Stage[GlobalTV]==6` | [후보·원작유닛 경유] `게이트_랜덤_이타도리_유지_84bf5e76` OnHitChance[Damage×2] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A0YV | ANfb | C파이어볼트-스네이크맨 킹코브 | 빈칸 | 빈칸 | 빈칸 | e0L3 @초월 스네이크맨 블랙맘바 ← L17288 `Snake_Attack_BlackMamba` [H0B3] 게이트 `if GetUnitUserData(GetAttacker())==0`; L17293 `Snake_Attack_BlackMamba` [H0B3] 게이트 `if GetUnitUserData(GetAttacker())==0` | [후보·원작유닛 경유] `게이트_초월_김경현_AP_4266b6e3` OnHitCount[Damage×2] | Stun SingleTarget, duration 기반 기본값(미확인) | unknown |
| A0YW | ANfb | C파이어볼트-료우기스킬 스턴 | 1.25 | 1.25 | 빈칸 | h0A7 #더미 료우기 스킬스턴 ← L18108 `Ryougi_Shiki_2` [h09O,h0A7] 게이트 `GetUnitPointValue(s__TrigVariables__get_unitB(Gl`; L18149 `Ryougi_Skill_3` [h09O,h09P,h0A7] 게이트 `call s__TrigVariables_SleepForStageNext(GlobalTV` | [후보·원작유닛 경유] `게이트_랜덤_카마도_탄지로_3c5f8bd9` OnHitCount[Damage×3]<br>`게이트_랜덤_카마도_탄지로_d370bb23` OnHitChance[Damage×5]<br>`게이트_랜덤_카마도_탄지로_ff8bed43` OnHitChance[Damage×12] | Stun SingleTarget, duration 1.25초 | unknown |
| A0ZZ | AOws | 2범위 기면증 스턴 | 빈칸 | 0.45 | 500 | e0A3 E영원 기면증 더미 1 ← L19734 `Cavendish_skill_Mana` [h06H] 게이트 `(Trig_Cavendish_skill_Mana_Func002Func008C()) {}`; L19738 `Cavendish_skill_Mana` [h06H] 게이트 `(Trig_Cavendish_skill_Mana_Func002Func008C()) {}` | [후보·원작유닛 경유] `게이트_영원_최상호_a8343962` OnHitCount[Damage×3] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 500 ≠ 레벨 range 900.0 | unknown |
| A10L | ANfb | C파이어볼트-키드 펑크 | 3 | 3 | 빈칸 | e0N6 @초월 키드 더미 자석팔- ← L17517 `Kid_Skill_Life` [?] 게이트 `e_Actions takes nothing returns nothing call s__`; L17535 `Kid_Skill_Life2` [?] 게이트 `e2_Actions takes nothing returns nothing call s_` | 없음 | Stun SingleTarget, duration 3초 | —(에셋 없음) |
| A110 | AOws | 2범위 키드초월 스턴 | 2 | 빈칸 | 500 | e0N1 @초월 키드 더미4 ← L17466 `Kid_Skill_Mana` [?] 게이트 `if(Trig_Kid_Skill_Mana_Func003C()) {}` | 없음 | Stun Enemies, duration 2초 | —(에셋 없음) |
| A111 | AOws | 2범위 흰불마나스턴 | 빈칸 | 1.65 | 625 | e0NF #불멸 흰수염 마나스킬0 ← L12790 `Absolb1` [?] 게이트 `(Trig_Absolb1_Func006Func005C()) {}`; L19162 `Ed_Skill_Mana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e0QY #불멸 흰수염 아이템 ← L19179 `Ed_Skill_1_Item` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3` | [후보·원작유닛 경유] `게이트_불멸_고도현_caa364ec` OnHitCount[Damage×4] | Stun Enemies, duration 기반 기본값(미확인) | unknown |
| A11L | AOws | 2범위 후지초월 미티어스턴더미 | 빈칸 | 0.45 | 535 | e079 @초월 후지초월6 ← L12848 `Bustercall_Etc4` [?] 게이트 `if(Trig_Bustercall_Etc4_Func005C()) {}`; L16642 `Huji_meteor` [?] 게이트 `if(Trig_Huji_meteor_Func004C()) {}` | 없음 | Stun Enemies, duration 기반 기본값(미확인) | —(에셋 없음) |
| A12J | AOws | 2범위 키드초월 스턴 강화 | 2.25 | 2.25 | 525 | e0N3 @초월 키드 더미5 ← L17468 `Kid_Skill_Mana` [?] 게이트 `if(Trig_Kid_Skill_Mana_Func003C()) {}`; L17484 `Kid_Skill_Mana_Tr` [h09O] 게이트 `elseif s__TrigVariables_Stage[GlobalTV]==5` | 없음 | Stun Enemies, duration 2.25초 | —(에셋 없음) |
| A12K | AOws | 2범위 타츠마키 스턴 | 1.75 | 1.75 | 525 | e0OS R랜유 타츠마키 Q 더미 ← L18598 `Tatsumaki_Q2_Attack` [h09O,h09P] 게이트 `s__TrigVariables_Stage[GlobalTV]==2`; L18613 `Tatsumaki_R_Attack` [h09O] 게이트 `s__TrigVariables_Stage[GlobalTV]==2` | 없음 | Stun Enemies, duration 1.75초 | —(에셋 없음) |
| A12N | AOws | 2범위 야마토 마나 스턴1 | 2.35 | 2.35 | 800 | e0OL @초월 야마토 무시빙아1 ← L17594 `Yamato_Life_skill` [?] 게이트 `if(Trig_Yamato_Life_skill_Func004C()) {}`; L17595 `Yamato_Life_skill` [?] 게이트 `if(Trig_Yamato_Life_skill_Func004C()) {}`<br>e0OU @초월 야마토 무시빙아 기 ← L17594 `Yamato_Life_skill` [?] 게이트 `if(Trig_Yamato_Life_skill_Func004C()) {}`; L17605 `Yamato_Dash` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0` | 없음 | Stun Enemies, duration 2.35초 | —(에셋 없음) |
| A12O | AOws | 2범위 타츠마키 운석 | 빈칸 | 3 | 625 | e0OT R랜유 타츠마키 더미 바위 ← L18656 `Tatsumaki_T_Explosion_by_AZ_Attack` [h09O] 게이트 `s__TrigVariables_Stage[GlobalTV]==18` | [후보·원작유닛 경유] `게이트_랜덤_이민형_4266b6e3` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 625 ≠ 레벨 range 600.0 | unknown |
| A134 | ANfb | C파이어볼트-파마의화살스턴 | 3 | 3 | 빈칸 | h0A5 #더미 금-운묵단심 스턴 ← L18361 `kikoyou_hp` [h09O,h0A5] 게이트 `s__TrigVariables_Stage[GlobalTV]==18` | [후보·원작유닛 경유] `게이트_랜덤_이즈미_신이치_351a0f00` OnHitCount[Damage×2] | Stun SingleTarget, duration 3초 | unknown |
| A13F | ANfb | C파이어볼트-카이도 스턴 | 2.5 | 2.5 | 빈칸 | e0ME #불멸 카이도우 [공통]  ← L19354 `Kaido_Skill_1_8` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==2`; L19396 `Kaido_Dragon_melee_1` [?] 게이트 `GetRandomInt(1,7)==2` | [후보·원작유닛 경유] `카이도_불멸_신지우_buster` OnHitChance[Damage×2]<br>`카이도_불멸_신지우_life100` OnHitCount[Damage×4]<br>`카이도_불멸_신지우_skill18` OnHitChance[Damage×2] | Stun SingleTarget, duration 2.5초 | unknown |
| A13M | ANfb | C파이어볼트-루피 레드호크 | 2.15 | 2.15 | 빈칸 | e04W @초월 루피 레드호크 더미 ← L15914 `Ruffy_Mana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L15916 `Ruffy_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==4`<br>e0PI @초월 루피 레드호크 더미 ← L15915 `Ruffy_Mana` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L15915 `Ruffy_Mana` [?] 게이트 `if(Trig_Ruffy_Mana_Func016C()) {}` | [후보·원작유닛 경유] `게이트_초월_신문철_AP_55ecbfb2` OnHitCount[Damage×2] | Stun SingleTarget, duration 2.15초 | unknown |
| A148 | ANfb | C파이어볼트계열 | 1 | 1 | 빈칸 | e0A2 !전설 네코 더미-던지기 ← L15055 `Legend32_neko1` [h09P] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1` | [후보·원작유닛 경유] `게이트_전설적인_진연서_483e0464` OnHitCount[Damage×2] | Stun SingleTarget, duration 1초 | unknown |
| A14B | ANfb | C파이어볼트-레일리 | 1 | 1 | 빈칸 | e0PZ #불멸 레일리흡수0 ← L18901 `Kick_1` [h056,h09O] 게이트 `if s__TrigVariables__get_integerA(GlobalTV)==s__` | [후보·원작유닛 경유] `절대쿨_불멸_정윤식` OnHitChance[Damage×3] | Stun SingleTarget, duration 1초 | unknown |
| A14J | ANfb | C파이어볼트-화염용왕 | 빈칸 | 빈칸 | 빈칸 | e08N @초월 사보 화염용왕3 ← L16188 `Sabo_Skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2`; L16189 `Sabo_Skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3`<br>e08O @초월 사보 화염용왕4 ← L16192 `Sabo_Skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3` | [후보·원작유닛 경유] `절대쿨_초월_두유찬_AD_1` OnHitCount[Damage] | Stun SingleTarget, duration 기반 기본값(미확인) | unknown |
| A14K | ANfb | C파이어볼트-대염계 | 3.5 | 3.5 | 빈칸 | e0D5 E영원 산화 부지화 더미 ← L14871 `Legend18` [h02O] 게이트 `GetRandomInt(1,10)==7`; L14872 `Legend18` [h02O] 게이트 `GetRandomInt(1,10)==7`<br>e0D8 E영원 에이스 특대화권 더 ← L19522 `Ace_Attack` [h059] 게이트 `if UnitHasBuffBJ(GetAttacker(),'B017')==false`<br>e0DF E영원 에이스 화달마 더미 ← L19530 `Ace_Attack` [h059] 게이트 `if GetRandomInt(1,8)==7` | [명시] `원작능력_영원_윤현모` OnHitChance[Damage×10] | Stun SingleTarget, duration 3.5초 | confirmed |
| A14S | ANfb | C파이어볼트-불꽃칼 | 1 | 0.5 | 빈칸 | e02K @초월 빅맘 내려찍기4 ← L18762 `Bigmam_Attack` [h04Q] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3`; L18763 `Bigmam_Attack` [h04Q] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3`<br>e0Q4 @초월 빅맘 먹이3 ← L18823 `Bigmam_Eat` [h09O] 게이트 `if(Trig_Bigmam_Eat_Func005C()) {}` | [후보·원작유닛 경유] `원작능력_불멸_정준영_A0IT` OnHitChance[Damage×2]<br>`원작능력_불멸_정준영_A0SN` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0YY` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0YZ` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0Z8` OnHitChance[Damage]<br>…외 5 | Stun SingleTarget, duration 1초 | unknown |
| A15Q | AOws | 2범위 센불 포격 스턴 | 빈칸 | 0.5 | 525 | e02G #불멸 센고쿠 충격파 폭발 ← L19000 `Seongoku_Skill_4` [?] 게이트 `(Trig_Seongoku_Skill_4_Func002C()) {}`; L19007 `Seongoku_Skill_4` [?] 게이트 `if(Trig_Seongoku_Skill_4_Func004C()) {}` | [후보·원작유닛 경유] `원작021_h04E` OnHitCount[Damage×2 ／ 효과0]<br>`회수_불멸_이이삭_f50ad66e` OnHitCount[Damage] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 525 ≠ 레벨 range 0/450.0 | unknown |
| A165 | AOws | 2범위 니카 마나스턴더미 | 2.5 | 0.37 | 600 | e0QK E영원 니카 바즈랑건3 ← L19468 `Nika_Attack` [H0BK,h09O] 게이트 `if GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker(`; L19469 `Nika_Attack` [H0BK,h09O] 게이트 `if GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker(` | [후보·원작유닛 경유] `게이트_영원_이지원_3ba504d5` OnHitCount[Damage×6] | Stun Enemies, duration 2.5초 · ⚠️ aare 600 ≠ 레벨 range 500.0 | unknown |
| A16F | AOws | 2범위 니카 스턴더미 | 2 | 0.3 | 500 | e0QH E영원 니카 거대번개 45 ← L19513 `Nika_AttackDamage` [?] 게이트 `s__TrigVariables__get_integerC(GlobalTV)<181`; L19514 `Nika_AttackDamage` [?] 게이트 `s__TrigVariables__get_integerC(GlobalTV)<181`<br>e0RB E영원 니카 스턴더미 ← L19483 `Nika_Attack` [H0BK,h09O] 게이트 `GetRandomInt(1,10)==2` | [후보·원작유닛 경유] `게이트_영원_이지원_3ba504d5` OnHitCount[Damage×6] | Stun Enemies, duration 2초 | unknown |
| A16W | AOws | 2범위 우타 스턴 | 1.5 | 0.3 | 500 | e0R1 E영원 우타 오선보1 ← L20144 `Uta_Attack` [h067] 게이트 `if GetRandomInt(1,9)==2` | [명시] `더미채널_영원_김정래_1` OnHitChance[효과0]<br>`원작능력_영원_문필환` OnHitChance[Damage×3]<br>`원작능력_영원_문필환_A0LZ` OnHitChance[Damage]<br>`원작능력_영원_문필환_A16W` OnHitChance[Damage] | Stun Enemies, duration 1.5초 · ⚠️ aare 500 ≠ 레벨 range 0/450.0 | confirmed |
| A177 | ANfb | C파이어볼트-봉황인 스턴 | 1.5 | 0.75 | 빈칸 | e0RJ %제한변화 마르코 봉황인스 ← L15794 `Marco_S2` [h09O] 게이트 `s__TrigVariables_Stage[GlobalTV]==1` | [후보·원작유닛 경유] `게이트_제한_박성호_3a13fd3d` OnHitChance[Damage×3] | Stun SingleTarget, duration 1.5초 | unknown |
| A17N | AOws | 2범위 거프불멸마나강화 | 빈칸 | 3 | 600 | e0B5 #불멸 주먹 유성군 더미2 ← L19097 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func004Func006C()) {}`; L19107 `Garp_Mana2` [?] 게이트 `(Trig_Garp_Mana2_Func006Func002C()) {}` | [후보·원작유닛 경유] `원작023_h04C` OnHitCount[Damage ／ Damage×2] | Stun Enemies, duration 기반 기본값(미확인) · ⚠️ aare 600 ≠ 레벨 range 650 | unknown |

**주인이 있어도 발동 명령이 없거나 주인이 없는 스턴 능력(33종, 확인함·항목 아님)**: A070, A071, A07X, A0AI, A0AT, A0B4, A0BX, A0GJ, A0J0, A0JC, A0JR, A0K3, A0K7, A0LC, A0M9, A0OJ, A0P2, A0P3, A0P4, A0P7, A0QJ, A0SG, A0UR, A0V3, A0VU, A0YM, A10R, A11O, A135, A13A, A144, A14U, A14V

## 5. 이감(둔화) 전수 — 103종

우리 `Slow` kind(13): multiplier = **남는 속도 비율**, duration초 뒤 해제, 여럿이면 가장 강한 하나. 원작 값은 「깎는 비율」이라 `multiplier = 1 − 원작값`(AOae·Aasl은 음수로 적혀 있어 `1 + 원작값`).
원작 이속 하한 `MinUnitSpeed=70`(war3mapMisc.txt) — 원작값이 1 이상(예: Htc3=2.5)이면 「하한까지」라는 뜻이다. 우리 하한 0.01과 다르니 그런 행은 multiplier를 `70/원작 적 기본이속`으로 잡자고 제안한다(표에 「하한」으로 표시).

### 5-1. 패시브 이감 오라 (AOae 음수 · Aasl) — 66종

지구력 오라(AOae)의 Oae1을 음수로, 대상을 enemies로 바꾼 것이 원작 이감의 주력이다. Aasl은 토네이도 감속 오라. 같은 버프 ID(abuf)끼리는 원작에서도 안 겹친다 — 우리 「가장 강한 하나」 규칙과 대체로 맞는다(다른 버프끼리는 원작이 합산하므로 우리 쪽이 약하다, 메모만).

| 원작 ID | 기반 | 이름 | 원작값 | adur | aare | 원작 발동/주인 | 우리 에셋 (현재) | 제안 | 확신도 |
|---|---|---|---|---|---|---|---|---|---|
| A03P | AOae | A공작슬래셔-비변 | Oae1=-0.2 | 빈칸 | 850 | h05W 비비 알라바스타의 왕녀 - 변화된(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A03V | AOae | A룸 이속감소1 | Oae1=-0.45 | 빈칸 | 825 | e03U @초월 로우 룸1 ← L16005 `Law_Attack` [H096] 게이트 `endif if GetUnitStateSwap(UNIT_STATE_MANA,GetAtt` | [후보·원작유닛 경유] `회수_초월_양재모_AD_f54a123f` OnHitChance[Damage+7] | Aura(2) · Slow Enemies, multiplier 0.55, duration 0(오라 지속), range 825 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | unknown |
| A063 | AOae | 홀로홀로열매 | Oae1=-0.45 | 빈칸 | 915 | h048 [히든조합]페로나고스트 프린세스(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.55, duration 0(오라 지속), range 915 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A064 | AOae | 홀로홀로열매 | Oae1=-0.25 | 빈칸 | 800 | h05K 페로나 홀로홀로열매-네거티브 - (패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.75, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A09M | AOae | !아카초 - 지천을 녹이는 열기  | Oae1=-0.12 | 빈칸 | 700 | e046 @초월 아카이누 용암분출1 ← L16171 `Akainu_03` [?] 게이트 ``<br>e04F @초월 아카이누 유성화산5 ← L16153 `Akainu_01_Shoot` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==1`<br>e04J @초월 아카이누 대분화9 ← L16166 `Akainu_02` [?] 게이트 `(Trig_Akainu_02_Func019Func001C()) {}` | [후보·원작유닛 경유] `게이트_히든_호치킨_21297197` OnHitChance[Damage×3]<br>`회수_초월_김만경_AD_0ac0451e` OnHitCount[Damage] | Aura(2) · Slow Enemies, multiplier 0.88, duration 0(오라 지속), range 700 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | unknown |
| A0AR | Aasl | 로저 이동속도 감소1 | Slo1=-0.5 | 빈칸 | 825 | h04J 골.D.로져 해적왕 - 불멸의(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_불멸_김용태_55b5cff0` OnHitCount[Damage]<br>`게이트_불멸_김용태_57c27e37` OnHitChance[Damage]<br>`원작능력_불멸_김용태` OnHitChance[Damage×4] | Aura(2) · Slow Enemies, multiplier 0.5, duration 0(오라 지속), range 825 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0CG | AOae | !모래모래 열매 | Oae1=-0.4 | 빈칸 | 800 | h05F 크로커다일 사막의 악어 - 제한됨(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_제한_김강민_948ad1b7` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.6, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0CH | AOae | !모래모래 열매 | Oae1=-0.2 | 빈칸 | 800 | h02H 크로커 다일 Mr.0 Sir - (패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0CI | AOae | !모래모래 열매 | Oae1=-0.07 | 빈칸 | 600 | h01A 크로커 다일 전 칠무해 - 특별함(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.93, duration 0(오라 지속), range 600 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0D3 | AOae | A!빙빙열매 | Oae1=-0.35 | 빈칸 | 825 | h041 [히든조합]쿠잔푸른 꿩(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.65, duration 0(오라 지속), range 825 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0D4 | AOae | A!빙빙열매 | Oae1=-0.12 | 빈칸 | 825 | h02B 쿠잔 해군대장 아오키지 - 희귀함(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.88, duration 0(오라 지속), range 825 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0D5 | AOae | A!빙빙열매 | Oae1=-0.7/-0.9 | 빈칸 | 825/805 | h04V 쿠잔 전 해군대장 아오키지  - (패시브)<br>H097 전 해군대장 아오키지 (패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.3/0.1, duration 0(오라 지속), range 825/805 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0DC | AOae | A!연기연기열매 | Oae1=-0.5 | 빈칸 | 800 | h02V 스모커 집념의 해군 중장 - 전설(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_전설적인_정준영` OnHitChance[Damage×9]<br>`원작능력_전설적인_정준영_A06M` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.5, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0DD | AOae | A!연기연기열매 | Oae1=-0.07 | 빈칸 | 800 | h01F 스모커 해군 준장 - 특별함(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.93, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0DT | AOae | A흉포한 광기 | Oae1=-0.7 | 빈칸 | 888 | h04Q 빅 맘 강철 풍선 - 불멸의(패시브)<br>h04Y 광분한 빅 맘 강철 풍선 - 불멸(패시브)<br>h09J 폭주한 빅 맘 사황 '강철 풍선'(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_불멸_정준영_A0IT` OnHitChance[Damage×2]<br>`원작능력_불멸_정준영_A0SN` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0YY` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0YZ` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0Z8` OnHitChance[Damage]<br>…외 22 | Aura(2) · Slow Enemies, multiplier 0.3, duration 0(오라 지속), range 888 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0DX | AOae | A흰수염의 전언 | Oae1=-0.33 | 빈칸 | 925 | h03Q [히든조합]모비딕 호사황 흰수염의(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.67, duration 0(오라 지속), range 925 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0DY | AOae | A신념 | Oae1=-0.35 | 빈칸 | 빈칸 | h04G 제트 해군 스승 "Z" - 불멸의(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_불멸_박은석` OnHitChance[Damage]<br>`회수_불멸_박은석_b8d2fd85` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.65, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0E5 | AOae | A투기 | Oae1=-0.18 | 빈칸 | 800 | h03M [히든조합]사보 - 용조권(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.82, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0E6 | AOae | A투기 | Oae1=-0.2 | 빈칸 | 800 | h04N 사보 혁명군 참모총장 - 초월함(패시브)<br>H092 혁명군 참모총장(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_초월_두유찬_AD_8eab1c6f` OnHitCount[Damage×2]<br>`원작능력_초월_황준석_ADAP_A03W` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A04D` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A051` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AG` OnHitChance[Damage]<br>…외 13 | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0FF | AOae | A대염계1 | Oae1=-0.45 | 빈칸 | 빈칸 | h059 '화권의' 포트거스.D.에이스 -(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_영원_윤현모_aa9cf1bd` OnHitCount[Damage]<br>`게이트_영원_윤현모_d319dd11` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.55, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0FV | AOae | A비구름 이감패시브 | Oae1=-0.45 | 빈칸 | 800 | e070 @초월 나미 헤비레인22 ← L17031 `Nami_Skill_1` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`<br>e0IZ @초월 나미 웨더리아5-리 ← L17057 `Nami_Skill_4` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L17060 `Nami_Skill_4` [?] 게이트 `call s__TrigVariables_SleepForStageAdd(GlobalTV,` | [명시] `더미채널_초월_강주혁_AP_79행_10000` OnHitChance[Damage×2] | Aura(2) · Slow Enemies, multiplier 0.55, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | confirmed |
| A0FW | AOae | A중력중력열매 | Oae1=-0.55 | 빈칸 | 빈칸 | h05M 후지토라 해군 대장 '성난 보라호(패시브)<br>H08X 해군 대장 '성난 보라호랑이'(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_초월_양재모_AD_29846395` OnHitCount[Damage×4] | Aura(2) · Slow Enemies, multiplier 0.45, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0FX | AOae | A중력중력열매 | Oae1=-0.24 | 빈칸 | 850 | h031 후지토라 잇쇼우 '신'해군대장 -(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.76, duration 0(오라 지속), range 850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0GQ | AOae | A귀기-조초 | Oae1=-0.3/-0.45/-0.5 | 빈칸 | 815/815/815 | H09F 수라의 검사(패시브)<br>H0BT 염왕(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_초월_박민수_AD_b5c9b49a` OnHitCount[Damage×2]<br>`게이트_초월_박민수_AD_b7ae9b41` OnHitChance[Damage×2]<br>`회수_초월_강주혁_AP_b7ae9b41` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.7/0.55/0.5, duration 0(오라 지속), range 815/815/815 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0H1 | AOae | A씽크홀 이감 | Oae1=-0.99 | 빈칸 | 800 | e07E @초월 후지초월 3스킬 2 ← L16603 `Huji_03` [?] 게이트 `if(Trig_Huji_03_Func003C()) {}`; L16604 `Huji_03` [?] 게이트 `if(Trig_Huji_03_Func003C()) {}`<br>e0E7 @초월 후지초월 3스킬 6 ← L16619 `Huji_03` [?] 게이트 `if(Trig_Huji_03_Func006C()) {}` | [후보·원작유닛 경유] `게이트_초월_양재모_AD_29846395` OnHitCount[Damage×4] | Aura(2) · Slow Enemies, multiplier 0.01, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | unknown |
| A0HP | AOae | A매의눈 | Oae1=-0.45 | 빈칸 | 빈칸 | h058 세계 최강의 대검호 쥬라클 미호크(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_영원_최상호` OnHitChance[Damage×14]<br>`원작능력_영원_최상호_A0HV` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.55, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0I5 | AOae | A자기장 | Oae1=-0.4 | 빈칸 | 빈칸 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.6, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0K4 | AOae | A자석자석 열매 | Oae1=-0.07 | 빈칸 | 800 | h00Y 유스타스 키드 초신성 - 특별함(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.93, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0K5 | AOae | A자석자석 열매 | Oae1=-0.2 | 빈칸 | 800 | h02D 유스타스 키드 캡틴-키드 - 희귀(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_희귀함_두유찬` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0L0 | AOae | A펭귄의 패기 | Oae1=-0.35 | 빈칸 | 800 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.65, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0LL | AOae | 8업글 | Oae1=-0.1 | 빈칸 | 1850 | h05U !#연구소 효과(패시브)<br>h08G 롤로노아 조로 수라의 검사 - 초(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.9, duration 0(오라 지속), range 1850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0PW | AOae | A풍압 | Oae1=-0.3 | 빈칸 | 875 | h02T 마르코 환수종'불사조' - 전설적(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_전설적인_임채민_355f5d39` OnHitChance[Damage]<br>`원작능력_전설적인_임채민` OnHitChance[Damage×6]<br>`원작능력_전설적인_임채민_A048` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.7, duration 0(오라 지속), range 875 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0Q7 | AOae | A자장가 프람 | Oae1=-0.05 | 빈칸 | 99999 | h04T 브룩 소울 킹 - 초월함(패시브)<br>H09I 소울 킹(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_초월_황준석_ADAP_A03W` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A04D` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A051` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AG` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AK` OnHitChance[Damage]<br>…외 13 | Aura(2) · Slow Enemies, multiplier 0.95, duration 0(오라 지속), range 99999 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0QC | AOae | A이속감소 냉철함-아오키지버프 | Oae1=-0.06/-0.04/-0.08/-0.12 | 빈칸 | 9999/9999/9999/9999 | h06S 연구소(패시브)<br>e0HK 전체오라 더미(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.94/0.96/0.92/0.88, duration 0(오라 지속), range 9999/9999/9999/9999 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0RE | Aasl | 버기 이동속도 감소 | Slo1=-0.25 | 빈칸 | 800 | h05A 천냥광대 버기 - 영원한(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_영원_김정래` OnHitChance[Damage×10] | Aura(2) · Slow Enemies, multiplier 0.75, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0RF | AOae | A이속감소 계열 크리마 | Oae1=-0.05 | 빈칸 | 450 | e099 S특별함 나미 더미 ← L14101 `Speical1` [h00P] 게이트 `GetRandomInt(1,10)==3` | [명시] `원작능력_특별함_주영호` OnHitChance[Damage×2] | Aura(2) · Slow Enemies, multiplier 0.95, duration 0(오라 지속), range 450 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | confirmed |
| A0SB | AOae | A덩쿨 뿌리1 | Oae1=-0.12 | 빈칸 | 500 | h04Z 쵸파 밀짚모자 해적단 의사 - 초(패시브)<br>e0C1 H히든 로쿠규 더미0 ← L14701 `Hidden23_rokugu` [h03N] 게이트 `elseif GetRandomInt(1,14)==5`; L14701 `Hidden23_rokugu` [h03N] 게이트 `elseif GetRandomInt(1,14)==5`<br>e0C3 H히든 로쿠규 더미1 ← L14701 `Hidden23_rokugu` [h03N] 게이트 `elseif GetRandomInt(1,14)==5`; L14701 `Hidden23_rokugu` [h03N] 게이트 `elseif GetRandomInt(1,14)==5` | [후보·원작유닛 경유] `원작능력_제한_최영민` OnHitChance[Damage×11]<br>`원작능력_제한_최영민_A0QV` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A03W` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A04D` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A051` OnHitChance[Damage]<br>…외 15 | Aura(2) · Slow Enemies, multiplier 0.88, duration 0(오라 지속), range 500 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | unknown |
| A0SR | AOae | A오바드 꾸 드로와 | Oae1=-0.15 | 빈칸 | 875 | h04T 브룩 소울 킹 - 초월함(패시브)<br>H09I 소울 킹(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_초월_황준석_ADAP_A03W` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A04D` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A051` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AG` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AK` OnHitChance[Damage]<br>…외 13 | Aura(2) · Slow Enemies, multiplier 0.85, duration 0(오라 지속), range 875 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0ST | Aasl | 드래곤전설 이동속도감소 | Slo1=-0.1 | 빈칸 | 800 | h02W 몽키.D.드래곤 세계최악의 범죄자(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.9, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0SW | AOae | A독기 | Oae1=-0.35 | 빈칸 | 850 | h033 빈스모크 레이쥬 포이즌 핑크 - (패시브) | [원작 유닛 명시 — 새 에셋] `게이트_전설적인_임건웅_87ed0ff5` OnHitChance[Damage×3]<br>`게이트_전설적인_임건웅_9ffa9c45` OnHitChance[Damage×3]<br>`원작능력_전설적인_임건웅` OnHitChance[Damage×6] | Aura(2) · Slow Enemies, multiplier 0.65, duration 0(오라 지속), range 850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0T6 | AOae | A!빙빙열매-서리걸음 | Oae1=-0.8/-0.9 | 빈칸 | 805/805 | H0B1 전 해군대장 아오키지 (패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.2/0.1, duration 0(오라 지속), range 805/805 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0T8 | AOae | A이속감소 영웅 | Oae1=-0.2 | 빈칸 | 800 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0UJ | AOae | A암행 | Oae1=-0.36 | 빈칸 | 850 | h084 시노부 뇌쇄 쿠노이치 - 제한됨(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_제한_이유범_351a0f00` OnHitCount[Damage×3]<br>`게이트_제한_이유범_43ad0210` OnHitCount[Damage] | Aura(2) · Slow Enemies, multiplier 0.64, duration 0(오라 지속), range 850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A0VB | AOae | A이속감소 도플초월 | Oae1=-0.7 | 빈칸 | 475 | e06K @초월 도플 거미줄 ← L16711 `DP_Attack` [H09E] 게이트 `if GetRandomInt(1,7)==4`<br>e0IW @초월 도플 새장 ← L16725 `DP_Attack_gaksung` [H09D] 게이트 `if GetRandomInt(1,5)==2` | [명시] `더미채널_초월_최상호_AP_79행_10000` OnHitChance[Damage×2] | Aura(2) · Slow Enemies, multiplier 0.3, duration 0(오라 지속), range 475 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | confirmed |
| A0VN | Aasl | 자석자석열매캡틴키드 희귀함 이속감 | Slo1=-0.25 | 빈칸 | 750 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.75, duration 0(오라 지속), range 750 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0VQ | AOae | A이속감소 계열 스킬 이감 이동속 | Oae1=-0.2 | 빈칸 | 800 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A0W6 | AOae | A이속감소 계열 크리마 | Oae1=-0.42 | 빈칸 | 725 | e0NR !전설 나미 더미3 ← L14931 `Legend25` [h02P] 게이트 `GetRandomInt(1,10000)<916` | [명시] `원작능력_전설적인_엄태웅` OnHitChance[Damage×2+AegrStack] | Aura(2) · Slow Enemies, multiplier 0.58, duration 0(오라 지속), range 725 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | confirmed |
| A0Z7 | AOae | A산적왕 | Oae1=-0.3 | 빈칸 | 800 | h0AC 마운틴.D.히그마 - 랜덤전용[제(패시브)<br>h0AE 마운틴.D.히그마 리얼리스트 - (패시브) | [원작 유닛 명시 — 새 에셋] `게이트_랜덤_미도리야_이즈쿠_079f80a5` OnHitCount[Damage]<br>`게이트_랜덤_미도리야_이즈쿠_2c7de21c` OnHitCount[Damage×2]<br>`버프게이트_랜덤_미도리야_이즈쿠_B05N` OnHitChance[ApplyBuff]<br>`원작능력_불멸_정준영_A0IT` OnHitChance[Damage×2]<br>`원작능력_불멸_정준영_A0SN` OnHitChance[Damage]<br>…외 8 | Aura(2) · Slow Enemies, multiplier 0.7, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A100 | AOae | A여진 | Oae1=-0.15 | 빈칸 | 800 | e00D !전설 흰수염 썬더클랩 ← L14733 `Legend5` [h03B] 게이트 `GetRandomInt(1,9)==3`<br>e03E #불멸 흰수염지진펀치 더미 ← L14739 `Legend51` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3`; L19171 `Ed_Skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3`<br>e066 @초월 검은수염지진 ← L16520 `Tichi_skill_4` [?] 게이트 ``; L16529 `Tichi_skill_4_tr` [?] 게이트 `(Trig_Tichi_skill_4_tr_Func002C()) {}` | [명시] `더미채널_초월_임채민_AP_79행_01000` OnHitChance[Damage×2] | Aura(2) · Slow Enemies, multiplier 0.85, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | confirmed |
| A10D | AOae | A자석자석 열매 | Oae1=-0.33 | 빈칸 | 1250 | h0AG 키드 유스타스'캡틴' - 초월함(패시브)<br>H0B4 유스타스'캡틴'(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_초월_황준석_ADAP_A03W` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A04D` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A051` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AG` OnHitChance[Damage]<br>`원작능력_초월_황준석_ADAP_A0AK` OnHitChance[Damage]<br>…외 12 | Aura(2) · Slow Enemies, multiplier 0.67, duration 0(오라 지속), range 1250 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A116 | AOae | A천본앵 | Oae1=-0.35 | 빈칸 | 950 | h08I 쿠치키 뱌쿠야 - 랜덤전용[제한됨(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_랜덤_한마_바키_355f5d39` OnHitChance[Damage×3]<br>`게이트_랜덤_한마_바키_9ffa9c45` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.65, duration 0(오라 지속), range 950 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A12Q | AOae | A이속감소 계열 스킬 이감 이동속 | Oae1=-0.2 | 빈칸 | 800 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 800 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A12X | AOae | A남은이속 | Oae1=-0.25 | 빈칸 | 빈칸 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.75, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A131 | AOae | A염계 | Oae1=-0.2 | 빈칸 | 빈칸 | h05V 포트거스.D.에이스 스페이드 해적(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_변화됨_박은석_8eab1c6f` OnHitCount[Damage]<br>`게이트_변화됨_박은석_9ffa9c45` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.8, duration 0(오라 지속), range 빈칸 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A132 | Aasl | 파충류 이속감소 | Slo1=-0.15 | 빈칸 | 825 | h09X X-드레이크 알로사우로스 - 희귀(패시브)<br>h0AH 킹 백수해적단-대간판 - 전설적인(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.85, duration 0(오라 지속), range 825 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A13C | Aasl | 타츠마키 이동속도감소 | Slo1=-0.5 | 빈칸 | 1025 | h0BC 타츠마키 - 랜덤전용[제한됨](패시브) | [원작 유닛 명시 — 새 에셋] `게이트_랜덤_이민형_4266b6e3` OnHitCount[Damage] | Aura(2) · Slow Enemies, multiplier 0.5, duration 0(오라 지속), range 1025 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A13Z | AOae | !아카초 - 지천을 녹이는 열기  | Oae1=-0.1 | 빈칸 | 500 | e01M H히든 아카이누 ← L14417 `Hidden5` [h03Z] 게이트 `GetRandomInt(1,7)==4`; L16171 `Akainu_03` [?] 게이트 `` | [후보·원작유닛 경유] `게이트_히든_호치킨_21297197` OnHitChance[Damage×3] | Aura(2) · Slow Enemies, multiplier 0.9, duration 0(오라 지속), range 500 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | unknown |
| A143 | AOae | A그림자그림자열매 | Oae1=-0.3 | 빈칸 | 850 | h02N 겟코 모리아 쉐도우 아스가르드 -(패시브) | [원작 유닛 명시 — 새 에셋] `게이트_전설적인_정윤식_355f5d39` OnHitChance[Damage×2] | Aura(2) · Slow Enemies, multiplier 0.7, duration 0(오라 지속), range 850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A14G | Aasl | 카이도 이동속도 감소 오라 | Slo1=빈칸 | 빈칸 | 1025 | h07M 카이도 사황'백수의 왕' - 불멸(패시브)<br>h0AD 카이도 사황'백수의 왕' - 불멸(패시브) | [원작 유닛 명시 — 새 에셋] `원작능력_불멸_신지우` OnHitChance[Damage×11]<br>`원작능력_불멸_정준영_A0IT` OnHitChance[Damage×2]<br>`원작능력_불멸_정준영_A0SN` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0YY` OnHitChance[Damage]<br>`원작능력_불멸_정준영_A0YZ` OnHitChance[Damage]<br>…외 6 | Slow — Slo1 빈칸 = 기반 기본값(미확인) | unknown |
| A15V | AOae | 8업글 | Oae1=-0.05 | 빈칸 | 1850 | h05U !#연구소 효과(패시브)<br>h08G 롤로노아 조로 수라의 검사 - 초(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.95, duration 0(오라 지속), range 1850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A167 | AOae | 8업글-아이템효과 오라1 | Oae1=-0.12 | 빈칸 | 1850 | h05U !#연구소 효과(패시브)<br>h08G 롤로노아 조로 수라의 검사 - 초(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.88, duration 0(오라 지속), range 1850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A16H | Aasl | 병기 이동속도 감소 | Slo1=-0.5 | 빈칸 | 825 | h0BU ??? 정체불명의 병기 - 불멸의(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.5, duration 0(오라 지속), range 825 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A16T | AOae | A룸 우타월드 | Oae1=-0.45 | 빈칸 | 1250 | e0RE E영원 우타월드 1 ← L20210 `Uta_World` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L20220 `Uta_World` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==3` | 없음 | Aura(2) · Slow Enemies, multiplier 0.55, duration 0(오라 지속), range 1250 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A173 | AOae | A풍압 | Oae1=-0.35 | 빈칸 | 925 | h08O 마르코 흰수염 유산의 수호자 - (패시브)<br>h09Q 불사조 흰수염 유산의 수호자 - (패시브) | [원작 유닛 명시 — 새 에셋] `게이트_제한_박성호_3a13fd3d` OnHitChance[Damage×3]<br>`게이트_제한_박성호_874ea782` OnHitChance[Damage×2]<br>`원작능력_제한_박성호` OnHitChance[Damage×2]<br>`원작능력_제한_최영민` OnHitChance[Damage×11]<br>`원작능력_제한_최영민_A17K` OnHitChance[Damage] | Aura(2) · Slow Enemies, multiplier 0.65, duration 0(오라 지속), range 925 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | likely |
| A17R | AOae | 8업글-아이템효과 오라 | Oae1=-0.12 | 빈칸 | 1850 | h05U !#연구소 효과(패시브) | 없음 | Aura(2) · Slow Enemies, multiplier 0.88, duration 0(오라 지속), range 1850 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |
| A17S | AOae | A덩쿨 뿌리 | Oae1=-0.25 | 빈칸 | 575 | 주인 없음(또는 발동 명령 없음) | 없음 | Aura(2) · Slow Enemies, multiplier 0.75, duration 0(오라 지속), range 575 — **코드 선행 필요(§2-1)** · 새 Aura 에셋을 그 유닛에 추가 | —(에셋 없음) |

### 5-2. 평타 부착 이감 (Aspo 독 · Afra 냉기) — 7종

매 평타 적중마다 건다. 우리엔 「항상 발동 평타 부착」이 OnHitChance(triggerChance 1)로 표현된다. Afra 값은 w3a에 없고 맵 상수 `FrostMoveSpeedDecrease=0.15`·`FrostAttackSpeedDecrease=0.01`(war3mapMisc.txt).

| 원작 ID | 기반 | 이름 | 원작값 | adur | aare | 원작 발동/주인 | 우리 에셋 (현재) | 제안 | 확신도 |
|---|---|---|---|---|---|---|---|---|---|
| A04R | Aspo | &독관련 | Spo2=빈칸 | 4 | 빈칸 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow — Spo2 빈칸 = 기반 기본값(미확인) | —(에셋 없음) |
| A04S | Aspo | &독독열매 | Spo2=0.8 | 4 | 빈칸 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow SingleTarget, multiplier 0.2, duration 4초 | —(에셋 없음) |
| A08M | Aspo | &포이즌 핑크 | Spo2=0.15 | 6 | 빈칸 | h033 빈스모크 레이쥬 포이즌 핑크 - (패시브) | [원작 유닛 명시 — 새 에셋] `게이트_전설적인_임건웅_87ed0ff5` OnHitChance[Damage×3]<br>`게이트_전설적인_임건웅_9ffa9c45` OnHitChance[Damage×3]<br>`원작능력_전설적인_임건웅` OnHitChance[Damage×6] | Slow SingleTarget, multiplier 0.85, duration 6초 | likely |
| A0ZF | Afra | !아이스 사브르1 | Misc Frost 0.15 | 10 | 빈칸 | h04V 쿠잔 전 해군대장 아오키지  - (패시브)<br>H097 전 해군대장 아오키지 (패시브)<br>H0B1 전 해군대장 아오키지 (패시브) | 없음 | Slow SingleTarget, multiplier 0.85, duration 10초 (공속 −1%는 우리 축 없음) | —(에셋 없음) |
| A0ZL | Afra | !아이스볼-동상 | Misc Frost 0.15 | 10 | 빈칸 | h041 [히든조합]쿠잔푸른 꿩(패시브) | 없음 | Slow SingleTarget, multiplier 0.85, duration 10초 (공속 −1%는 우리 축 없음) | —(에셋 없음) |
| A14D | Aspo | &양분흡수 | Spo2=0.15 | 빈칸 | 빈칸 | h03N [히든조합]료쿠규'신'해군대장(패시브) | 없음 | Slow SingleTarget, multiplier 0.85, duration 빈칸초 | —(에셋 없음) |
| A15I | Aspo | &양분흡수 | Spo2=0.85 | 빈칸 | 빈칸 | H0BL '신'해군대장-초록소(패시브)<br>H0BO 금증숲숲-초록소(패시브) | [원작 유닛 명시 — 새 에셋] `회수_초월_구주호_AD_3a13fd3d` OnHitChance[Damage] | Slow SingleTarget, multiplier 0.15, duration 빈칸초 | unknown |

### 5-3. 더미·트리거 이감 (AHtc 천둥강타 · ACt2 · AOeq 지진 · Apg2 퍼지 · Aslo) — 30종

§4와 같은 구조(트리거가 더미를 만들어 명령). 주인이 없거나 명령이 없는 것도 이 표에 남겼다(「주인 없음」 — 반영 대상 아님).

| 원작 ID | 기반 | 이름 | 원작값 | adur | aare | 원작 발동/주인 | 우리 에셋 (현재) | 제안 | 확신도 |
|---|---|---|---|---|---|---|---|---|---|
| A078 | AHtc | 3범위 도변 둔화 | Htc3=0.3 | 4 | 375 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow Enemies, multiplier 0.7, duration 4초 | —(에셋 없음) |
| A07F | ACt2 | 1범위 슬램계열 | Ctc3=빈칸 | 3 | 600 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow — Ctc3 빈칸 = 기반 기본값(미확인) | —(에셋 없음) |
| A07G | ACt2 | 1범위 흰수열전설 지진 | Ctc3=0.3 | 3 | 600 | e00D !전설 흰수염 썬더클랩 ← L14733 `Legend5` [h03B] 게이트 `GetRandomInt(1,9)==3` | [후보·원작유닛 경유] `게이트_전설적인_최상호_355f5d39` OnHitChance[Damage×2] | Slow Enemies, multiplier 0.7, duration 3초 | unknown |
| A09T | AHtc | 3범위 루피초월 둔화 | Htc3=0.33 | 2 | 425 | e054 @초월 루피 펀치더미 ← L15906 `Ruffy_AttackDamage` [?] 게이트 `s__TrigVariables__get_integerC(GlobalTV)<176`; L15907 `Ruffy_AttackDamage` [?] 게이트 `s__TrigVariables__get_integerC(GlobalTV)<176` | [명시] `더미채널_초월_신문철_AP_79행_10000` OnHitChance[Damage×2] | Slow Enemies, multiplier 0.67, duration 2초 | confirmed |
| A0A6 | ACt2 | 1범위 검수초월 지진 | Ctc3=0.35 | 3 | 600 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow Enemies, multiplier 0.65, duration 3초 | —(에셋 없음) |
| A0AM | AHtc | 3범위 도플각성흉탄 | Htc3=2.5 | 4.5 | 525 | e06R @초월 도플 흉탄더미 ← L16722 `DP_Attack_gaksung` [H09D] 게이트 ``<br>e0IW @초월 도플 새장 ← L16725 `DP_Attack_gaksung` [H09D] 게이트 `if GetRandomInt(1,5)==2` | [명시] `더미채널_초월_최상호_AP_79행_10000` OnHitChance[Damage×2] | Slow Enemies, multiplier 하한(70/적이속), duration 4.5초 | confirmed |
| A0C6 | ACt2 | 1범위 핸콕영원 패기 | Ctc3=0.6 | 2.5 | 600 | e08F E영원 핸콕 날아차기 스킬 ← L19592 `hancock_skill_9` [?] 게이트 `if(Trig_hancock_skill_9_Func003C()) {}`<br>e0KV !전설 핸콕발차기1 ← L14828 `Legend_han_1` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==1`; L19592 `hancock_skill_9` [?] 게이트 `if(Trig_hancock_skill_9_Func003C()) {}`<br>e0KW !전설 핸콕발차기 ← L14828 `Legend_han_1` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==1`; L19592 `hancock_skill_9` [?] 게이트 `if(Trig_hancock_skill_9_Func003C()) {}` | [후보·원작유닛 경유] `게이트_영원_김영원_5a6f48de` OnHitCount[Damage]<br>`게이트_전설적인_이현주_0e5cf375` OnHitChance[Damage×3]<br>`원작028_h05C` OnHitCount[Damage×3 ／ Damage×3] | Slow Enemies, multiplier 0.4, duration 2.5초 | unknown |
| A0C7 | ACt2 | 1범위 흰수열초월 지진 | Ctc3=0.45 | 3.5 | 600 | e0E9 #불멸 흰불 해일 2 ← L19186 `Ed_Skill_2sc` [?] 게이트 `` | 없음 | Slow Enemies, multiplier 0.55, duration 3.5초 | —(에셋 없음) |
| A0LD | AHtc | 3범위 츠바사 할퀴기 | Htc3=0.7 | 3 | 500 | e0CF R랜유 츠바사 더미1 ← L17777 `cat1` [h072] 게이트 `(Trig_cat1_Func001Func001C()) {}` | [명시] `더미채널_랜덤_이타도리_유지_1` OnHitChance[Damage×2]<br>`원작능력_랜덤_이타도리_유지` OnHitChance[Damage×6]<br>`원작능력_랜덤_이타도리_유지_A0LD` OnHitChance[Damage] | Slow Enemies, multiplier 0.3, duration 3초 | confirmed |
| A0LE | AHtc | 3범위 츠바사 할퀴기 2 | Htc3=0.85 | 3 | 600 | e0CG R랜유 츠바사 더미2 ← L17778 `cat1` [h072] 게이트 `(Trig_cat1_Func001Func001C()) {}` | [명시] `더미채널_랜덤_이타도리_유지_1` OnHitChance[Damage×2]<br>`원작능력_랜덤_이타도리_유지` OnHitChance[Damage×6]<br>`원작능력_랜덤_이타도리_유지_A0LD` OnHitChance[Damage] | Slow Enemies, multiplier 0.15, duration 3초 | confirmed |
| A0MJ | AHtc | 3범위 무통샷-상디초월 | Htc3=빈칸 | 3.5 | 500 | e044 @초월 상디 킥2 더미2 ← L16049 `SandiAttack_Upgrade` [H09G] 게이트 `GetRandomInt(1,18)==5`; L16053 `SandiAttack` [H09H] 게이트 `GetRandomInt(1,20)==5` | [후보·원작유닛 경유] `게이트_초월_배성령_AD_18aa2343` OnHitChance[Damage×2]<br>`게이트_초월_배성령_AD_3c5f8bd9` OnHitCount[Damage×3] | Slow — Htc3 빈칸 = 기반 기본값(미확인) | unknown |
| A0OG | AHtc | 3범위 로우초월 카운터1 | Htc3=0.4 | 3 | 485 | e09Q @초월 로우 메스 1 ← L16013 `Law_Skill_2` [?] 게이트 ``<br>e0NQ @초월 로우 카운터 더미 ← L16014 `Law_Skill_2_reinforce` [?] 게이트 ``; L17425 `Kid_Skill_3_item` [?] 게이트 `(Trig_Kid_Skill_3_item_Func004Func007C()) {}` | [명시] `원작006_H096` OnHitChance[Damage×2] | Slow Enemies, multiplier 0.6, duration 3초 | confirmed |
| A0OI | Aslo | !그림자 절단 | Slo1=0.5 | 5 | 빈칸 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow SingleTarget, multiplier 0.5, duration 5초 | —(에셋 없음) |
| A0OY | ACt2 | 1범위 로저 고대병기액티브 | Ctc3=2 | 3.25 | 800 | e081 #불멸 로저 마나 더미3 ← L19204 `roger_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==1`<br>e0LA #불멸 로저 마나 더미2 ← L19206 `roger_Mana` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2` | [후보·원작유닛 경유] `게이트_불멸_김용태_55b5cff0` OnHitCount[Damage] | Slow Enemies, multiplier 하한(70/적이속), duration 3.25초 | unknown |
| A0PI | AHtc | 3범위 도플초월 오색실1 | Htc3=2.5 | 4.5 | 525 | e06H @초월 도플 할퀴기 ← L16725 `DP_Attack_gaksung` [H09D] 게이트 `if GetRandomInt(1,5)==2`<br>e06K @초월 도플 거미줄 ← L16711 `DP_Attack` [H09E] 게이트 `if GetRandomInt(1,7)==4` | [명시] `더미채널_초월_최상호_AP_79행_10000` OnHitChance[Damage×2] | Slow Enemies, multiplier 하한(70/적이속), duration 4.5초 | confirmed |
| A0PT | AHtc | 3범위 사보초 화권 | Htc3=0.3 | 2.5 | 500 | e03Y @초월 사보 화권 더미2 ← L16198 `Sabo_Skill_3` [?] 게이트 `` | [후보·원작유닛 경유] `절대쿨_초월_두유찬_AD_3` OnHitCount[Damage] | Slow Enemies, multiplier 0.7, duration 2.5초 | unknown |
| A0Q6 | AHtc | 3범위 검수초 지진-강진 | Htc3=0.75 | 3 | 700 | e0PH @초월 검은수염지진2 ← L16520 `Tichi_skill_4` [?] 게이트 ``; L16528 `Tichi_skill_4_tr` [?] 게이트 `(Trig_Tichi_skill_4_tr_Func002C()) {}` | [명시] `더미채널_초월_임채민_AP_79행_01000` OnHitChance[Damage×2] | Slow Enemies, multiplier 0.25, duration 3초 | confirmed |
| A0QX | AHtc | 3범위 레베카 궤적 둔화 | Htc3=0.7 | 2.5 | 850 | e0EZ %제한됨변화 레베카 더미5 ← L15391 `Rebeca_Skill_3` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0` | [명시] `더미채널_제한_최영민_79행_00833` OnHitChance[Damage] | Slow Enemies, multiplier 0.3, duration 2.5초 | confirmed |
| A0SH | AOeq | 9로빈 지진1 | Oeq3=0.7 | 4 | 600 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow Enemies, multiplier 0.3, duration 4초 | —(에셋 없음) |
| A0VC | AOeq | 남은대지진 | Oeq3=0.8 | 2 | 550 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow Enemies, multiplier 0.2, duration 2초 | —(에셋 없음) |
| A0WZ | AHtc | 3범위 징베 해류 | Htc3=빈칸 | 3 | 625 | e0KH @초월 징베 더미1 ← L17226 `Jimbe` [?] 게이트 `(Trig_Jimbe_Func002Func001C()) {}` | [후보·원작유닛 경유] `게이트_초월_최상호_AD_b8d2fd85` OnHitChance[Damage×2] | Slow — Htc3 빈칸 = 기반 기본값(미확인) | unknown |
| A10V | Apg2 | 퍼지1 | Prg | 2.25 | 빈칸 | 주인 없음(또는 발동 명령 없음) | 없음 | Slow SingleTarget, **근사** — 퍼지는 「멈췄다가 duration 동안 서서히 회복」(Prg4 이동 갱신 1.25·Prg5 공격 갱신 1.25). multiplier 0.5·duration 2.25초 평균 근사 제안 | —(에셋 없음) |
| A10W | Apg2 |  | Prg | 3 | 빈칸 | e0NQ @초월 로우 카운터 더미 ← L16014 `Law_Skill_2_reinforce` [?] 게이트 ``; L17425 `Kid_Skill_3_item` [?] 게이트 `(Trig_Kid_Skill_3_item_Func004Func007C()) {}` | [명시] `원작006_H096` OnHitChance[Damage×2] | Slow SingleTarget, **근사** — 퍼지는 「멈췄다가 duration 동안 서서히 회복」(Prg4 이동 갱신 1.5·Prg5 공격 갱신 1.5). multiplier 0.5·duration 3초 평균 근사 제안 | likely |
| A123 | Apg2 |  | Prg | 0.85 | 빈칸 | e064 @초월 검은수염 크로우즈1 ← L14247 `Tich1_danil` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2`; L14939 `Legend26_tichi_crows` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2`<br>e0PK @초월 검은수염 크로우즈 ← L16485 `Tichi_skill_1` [?] 게이트 `if s__TrigVariables_Stage[GlobalTV]==2` | [후보·원작유닛 경유] `게이트_전설적인_홍인창_fbb3c4ff` OnHitCount[Damage×3]<br>`게이트_초월_임채민_AP_da9fb163` OnHitCount[Damage×3]<br>`게이트_희귀함_황준석_d1ddeea9` OnHitChance[Damage×2] | Slow SingleTarget, **근사** — 퍼지는 「멈췄다가 duration 동안 서서히 회복」(Prg4 이동 갱신 0.3·Prg5 공격 갱신 0.3). multiplier 0.5·duration 0.85초 평균 근사 제안 | unknown |
| A12S | Apg2 |  | Prg | 3 | 빈칸 | e09D @초월 로우 메스 2 ← L16013 `Law_Skill_2` [?] 게이트 `` | 없음 | Slow SingleTarget, **근사** — 퍼지는 「멈췄다가 duration 동안 서서히 회복」(Prg4 이동 갱신 1.25·Prg5 공격 갱신 1.25). multiplier 0.5·duration 3초 평균 근사 제안 | —(에셋 없음) |
| A12U | Apg2 |  | Prg | 1.5 | 빈칸 | e07W #불멸 로저 더미4 ← L19191 `roger_Attack` [h04J] 게이트 `if GetRandomInt(1,1000)<86` | [명시] `원작능력_불멸_김용태` OnHitChance[Damage×4]<br>`원작능력_불멸_김용태_A08V` OnHitChance[Damage]<br>`원작능력_불멸_김용태_A0FI` OnHitChance[Damage] | Slow SingleTarget, **근사** — 퍼지는 「멈췄다가 duration 동안 서서히 회복」(Prg4 이동 갱신 0.5·Prg5 공격 갱신 0.5). multiplier 0.5·duration 1.5초 평균 근사 제안 | likely |
| A13V | AHtc | 3범위 검수초 지진 | Htc3=0.7 | 3 | 650 | e066 @초월 검은수염지진 ← L16520 `Tichi_skill_4` [?] 게이트 ``; L16529 `Tichi_skill_4_tr` [?] 게이트 `(Trig_Tichi_skill_4_tr_Func002C()) {}` | [명시] `더미채널_초월_임채민_AP_79행_01000` OnHitChance[Damage×2] | Slow Enemies, multiplier 0.3, duration 3초 | confirmed |
| A140 | AHtc | 3범위 로우초월 카운터 | Htc3=0.4 | 3 | 485 | e03T @초월 로우 카운터 더미1 ← L16015 `Law_Skill_2_reinforce` [?] 게이트 `` | [명시] `원작006_H096` OnHitChance[Damage×2] | Slow Enemies, multiplier 0.6, duration 3초 | confirmed |
| A160 | AHtc | 3범위 료쿠규 초월 둔화 | Htc3=0.25 | 3 | 500 | e0QN @초월 로쿠규 거대나무1 ← L17635 `Rokugu_skill_1` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0`; L17637 `Rokugu_skill_1` [?] 게이트 `s__TrigVariables_Stage[GlobalTV]==0` | [명시] `더미채널_초월_구주호_AD_79행_01429` OnHitChance[Damage] | Slow Enemies, multiplier 0.75, duration 3초 | confirmed |
| A16K | Apg2 |  | Prg | 2.25 | 빈칸 | e0H8 !전설 스모커 더미3 ← L14761 `Legend9` [h02V] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=`; L14762 `Legend9` [h02V] 게이트 `GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())=` | [명시] `원작능력_전설적인_정준영` OnHitChance[Damage×9] | Slow SingleTarget, **근사** — 퍼지는 「멈췄다가 duration 동안 서서히 회복」(Prg4 이동 갱신 0.5·Prg5 공격 갱신 0.5). multiplier 0.5·duration 2.25초 평균 근사 제안 | likely |


---

## 6. 확인했으나 효과 없음 (항목 아님)

- **ACbh `adur`=0 명시: 365종.** 강타 피해만 있고 스턴은 없다. 목록은 §3 끝에 있다.
- **ACbh `Hbh1`=0: A0FU·A0YJ.** 확률 0이라 발동하지 않는다(§3 표에 unknown으로 남겼다).
- **뿌리묶기(Aenr) 14종:** A07Q·A07R·A07W·A099·A09C·A09D·A0A7·A0JN·A0M3·A0W3·A124·A12E·A13D·A13L. j의 `"entanglingroots"` 명령 8곳(`war3map_new.j:14737, 14802, 14805, 16185, 17592, 19122, 19168, 19178`)이 전부 `get_unitA`, 즉 시전한 우리 유닛을 대상으로 한다. 자기 정지 연출이다.
- **이감 없는 AHtc·ACt2·Aspo 21종:** A004·A02E·A04T·A077·A088·A09W·A0BC·A0BU·A0C8·A0CF·A0IA·A0M4·A0OC·A0PR·A0QA·**A0QY**·A0SP·A0TJ·A0WB·A16Z·A171. Htc3/Ctc3/Spo2=0이거나 `adur`=0이다. 대부분은 피해 전달용이거나 UI 버튼 껍데기(`[조합]`·`행운의 토큰`·`창고에서 나오기`)다.
- **스턴 0.01초 AHtb: A09Z·A0TO.** 피해 전달용 더미다. §4 표에 「반영 불필요」로 적었다.
- **도움소(h08A)의 A0JR 버스터콜(ANcs)·A0P2 낙뢰(Awfb):** 플레이어가 누르는 도움소 버튼이지 유닛 스킬이 아니다. 우리 SupportShop 쪽 일이다.
- **적 쪽 이감: 0종.** 이감·스턴 기반 능력(AHtc·ACt2·AOae 음수·Aasl·Aspo·Afra·AOeq·Apg2·Aslo·AOws·AHtb·ANfb·ANcs)을 가진 유닛은 전부 h/H(플레이어 유닛)·e(더미)이고, 예외는 o02P 메타몽(A0SH·A0RT) 하나뿐이다. 적 웨이브 유닛이 가진 것은 없다.
- **`SetUnitMoveSpeed` 2곳(`war3map_new.j:21267`·`:21291`):** 난독화된 시스템 함수 안이다(함수명 `I0LLOO` 계열). 스킬이 아니다.

## 7. 범위 밖 발견 (반영 단계 전에 PM 확인 필요)

### 7-1. 분할 에셋의 피해 값이 서로 섞였다
2026-09-06 「능력 하나에 파일 하나」 분할에서 **triggerChance는 각자 제 값(=Hbh1/100)인데, Damage의 multiplier/bonus(basis CasterAttackPower = Hbh2/Hbh3)는 같은 묶음의 다른 능력 값이 들어가 있다.** 예를 들면 A0AG에 A03W의 10/500000, A0IT에 A0KA의 1062500, A101에 A0ZV의 1/1, A0HK에 A0CS의 8.88, 정준영 A0IT에 A13O·A13Q의 120000·70000이 들어가 있다. 스턴을 넣을 때 같은 파일을 만지게 되니 같이 고칠지는 PM이 정한다.

| 에셋 | 능력 | triggerChance | 현재 multiplier/bonus | 원작 Hbh2/Hbh3 |
|---|---|---|---|---|
| `원작능력_초월_황준석_ADAP_A03W` | A03W | 0.12 | 0/0 | 10/500000 |
| `원작능력_초월_황준석_ADAP_A04D` | A04D | 0.175 | 0/625000 | 빈칸/0 |
| `원작능력_초월_황준석_ADAP_A051` | A051 | 0.18 | 0/0 | 5/0 |
| `원작능력_초월_황준석_ADAP_A0AG` | A0AG | 0.2 | 10/500000 | 7/0 |
| `원작능력_초월_황준석_ADAP_A0AK` | A0AK | 0.2 | 5/0 | 빈칸/0 |
| `원작능력_초월_황준석_ADAP_A0CS` | A0CS | 1.0 | 0/0 | 8.88/0 |
| `원작능력_초월_황준석_ADAP_A0G4` | A0G4 | 1.0 | 3/250000; 4/0 | 빈칸/0 |
| `원작능력_초월_황준석_ADAP_A0HA` | A0HA | 1.0 | 3.5/0 | 4/0 |
| `원작능력_초월_황준석_ADAP_A0HK` | A0HK | 1.0 | 8.88/0 | 3.5/0 |
| `원작능력_초월_황준석_ADAP_A0HN` | A0HN | 0.17 | 7/0 | 3/250000 |
| `원작능력_초월_황준석_ADAP_A0IT` | A0IT | 0.2 | 0/1.0625e+06 | 빈칸/0 |
| `원작능력_초월_황준석_ADAP_A0KA` | A0KA | 0.1062 | 0/0 | 빈칸/1.0625e+06 |
| `원작능력_초월_황준석_ADAP_A0KE` | A0KE | 0.0625 | 0/0 | 빈칸/625000 |
| `원작능력_초월_황준석_ADAP_A0X0` | A0X0 | 0.4 | 0/360000 | 빈칸/0 |
| `원작능력_초월_황준석_ADAP_A0ZV` | A0ZV | 0.3 | 0/0 | 1/1 |
| `원작능력_초월_황준석_ADAP_A101` | A101 | 0.35 | 1/1 | 빈칸/360000 |
| `원작능력_초월_황준석_ADAP_A103` | A103 | 0.2 | 0/0 | 빈칸/0 |
| `원작능력_불멸_정준영_A0IT` | A0IT | 0.2 | 0/120000; 0/70000 | 빈칸/0 |
| `원작능력_불멸_정준영_A0SN` | A0SN | 1.0 | 0/30000 | 빈칸/0 |
| `원작능력_불멸_정준영_A0YY` | A0YY | 0.5 | 0/20000 | 빈칸/20000 |
| `원작능력_불멸_정준영_A0YZ` | A0YZ | 0.2 | 0/0 | 빈칸/0 |
| `원작능력_불멸_정준영_A0Z8` | A0Z8 | 0.2 | 0/0 | 빈칸/0 |
| `원작능력_불멸_정준영_A0ZV` | A0ZV | 0.3 | 0/30000 | 1/1 |
| `원작능력_불멸_정준영_A13O` | A13O | 0.17 | 0/0 | 빈칸/120000 |
| `원작능력_불멸_정준영_A13P` | A13P | 0.2 | 0/0 | 빈칸/30000 |
| `원작능력_불멸_정준영_A13Q` | A13Q | 0.25 | 0/0 | 빈칸/70000 |
| `원작능력_불멸_정준영_A13R` | A13R | 0.35 | 효과0 | 빈칸/30000 |

### 7-2. Hbh1(확률) 빈칸을 제각각 옮겼다
Hbh1이 빈칸인 스턴 강타 10종은 확률이 ACbh 기본값이다(미확인). 우리 에셋은 그 자리를 1.0·0.15·0.143·0.1로 제각각 채웠다.
- A048: 임채민_A048 0.15 / 홍인창 1.0
- A052: 임채현 0.143
- A05R: 황준석 1.0
- A05S: 진연서 1.0
- A0CS: 황준석_ADAP 1.0
- A0EM: 고도현 1.0 / 정윤식_A0EM 0.15
- A0HK: 황준석_ADAP 1.0
- A0J7: 조세민 1.0(효과 0) / 최상호 0.1
- A0TW: 이유선 0.1
- A0DK: 에셋 없음

스턴을 넣으면 확률 1.0인 곳은 **매 타 스턴**이 된다(예: A0CS 3초, A0HK 5초). 기본값을 확인할 방법(워크3 원본 AbilityData.slk)이 생기기 전에는 이 10종의 스턴 반영을 보류하는 것을 권한다.

### 7-3. 확률 100% 강타 = 영구 스턴
원작 Hbh1=100인 강타가 있다: A05P 2.25초, A0G3 1.2초, A0G4 1초, A0GK 1.5초, A0GL 3초, A0QQ 0.1초, A0SN 0.37초. 공속이 빠르면 원작에서도 대상이 계속 묶여 있는 설계다. 그대로 옮기면 보스도 묶인다(원작 `ahdu`를 보스에 쓸지는 우리 보스가 「영웅」인지에 달렸다. 원작 보스 udty=large이고 영웅 여부는 이번 조사 밖).

## 8. 조사 범위 메모: 「전부」임을 어떻게 아는가

- **능력 모집단:** `w3a.parse`의 1,628개 전부를 기반(base)별로 나누고(115종), 기반마다 **실제로 채워진 필드 목록**을 뽑아 이동 감소·스턴 의미가 있는 필드를 가진 기반을 골랐다.
  - 스턴: ACbh·AOws·AHtb·ANfb·Awfb·ANcs·AUsl·Aenr
  - 이감: AHtc·ACt2·AOae(Oae1<0)·Aasl·Aspo·Afra·AOeq·Apg2·Aslo·ACsw
  - 뺀 것: 필드상 이감·스턴이 없는 기반(ACbz·ACrg 눈보라, ANst 쇄도, AHwe, Aroa 포효, AUau, AHad 방깎 등)
  - AOae·AUau·ACac·AHad에서 음수 이속이거나 enemies 대상인 것은 전수로 다시 확인했다. AUau·ACac에는 음수 이속이 없었다.
- **주인:** `war3map_new.w3u` 1,559 유닛의 `uabi`·`uabh`를 역색인했다.
- **발동 여부:** 더미가 가진 능력은 그 더미를 만드는 j 함수와 **같은 트리거 안에** 기반별 명령 문자열(`stomp`·`thunderclap`·`creepthunderclap`·`thunderbolt`·`firebolt`·`clusterrockets`·`earthquake`·`purge`·`slow`)이 있을 때만 「발동」으로 셌다. j 전체의 명령 집계:
  - `IssueImmediateOrderById` 116 · `IssueTargetOrderById` 83 · `IssueImmediateOrder(BJ)` 135 · `IssueTargetOrder(BJ)` 102 · `IssuePointOrder…` 65
  - `String2OrderIdBJ`: stomp 53 · thunderclap 16 · thunderbolt 14 · creepthunderclap 2 · purge 2 · firebolt 8
- **원작 유닛 ↔ 트리거:** `SaveTriggerHandle(udg_HashAttack,'유닛',0,gg_trg_X)` 204건으로 트리거 → 원작 유닛을 풀었다. 마나·특수 트리거 중 HashAttack에 없는 것(예: `Tichi_skill_4`·`Law_Skill_2`·`roger_Mana`)은 유닛이 안 풀려 매핑이 「후보」나 「없음」으로 남았다. §4의 unknown 59건 대부분이 이 경우다. 값 자체는 confirmed다.
- **원작 ↔ 우리 에셋:** 세 가지로만 이었다. 에셋 이름을 **지어서 잇지 않았다**(name-mapping-impossible).
  1. 에셋 `skillName`이 원작 능력 ID로 시작
  2. 에셋 텍스트에 더미 ID나 능력 ID가 명시
  3. 에셋 설명의 「원작 …(hXXX)의」 문구
- **못 본 것:**
  - ① 기반 능력 기본값(AbilityData.slk가 맵에 없음). 빈칸 칸은 전부 unknown이다.
  - ② 트리거 안 분기(`Stage==N` 상태머신)와 우리 게이트 에셋의 1:1 대응. 후보 표시만 했다.
  - ③ 두 명의 반박 검증(원 워크플로의 Verify 단계)은 하지 않았다. PM 검증이 그 자리다.
- **재현:** 스크래치 스크립트(census.py·assemble.py·gen.py)는 이 세션 임시 폴더에 있다. 필요하면 Tools/로 옮길 수 있다.
