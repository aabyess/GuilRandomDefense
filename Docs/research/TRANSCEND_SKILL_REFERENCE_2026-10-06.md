# 원작 초월 스킬 수치 참고표 (2026-10-06, 구현담당1)

초월 25종의 스킬을 효과 종류별로 모았다. **우리 값**은 SkillData 에셋(`Assets/Data/**/SkillData_*`)에서, **원작 값**은 `Tools/w3x/원본/war3map_new.w3a`를 `Tools/w3x/w3a.py`로 직접 디코드해 같은 줄에 붙였다(첫 레벨 값, 필드 ID 그대로). 조사 문서는 근거가 아니다.
생성: `python3 -I Tools/transcend_skill_reference.py` (에셋이 바뀌면 다시 돌린다).

열: 유닛 · 스킬 이름 · 트리거 · 대상 · multiplier(남는 속도 비율·%·고정값) · duration(초) · 효과 chance · 트리거 확률 · 쿨다운 · 범위 · 원작 w3a 필드.

## Slow (22건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강주혁_AP | A0GQ 귀기-조초 — 적 이속 -30% 오라 | 오라(Aura) | Enemies | 0.7 | 0.0 | 1 | 1.0 |  | 815.0 | AOae aher=0 atar=air,enemies,ground Oae1=-0.30000001192092896 Oae2=0.0 aare=815.0 abuf=B02B abpx=1 atat= abpy=1 |
| 초월_구주호_AD | A160 3범위 료쿠규 초월 둔화 | 평타 확률(OnHitChance) | SingleTarget | 0.75 | 3.0 | 1 | 0.14285714285714285 |  | 0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 adur=3.0 abuf= atar=air,enemies,neutral,ground aare=500.0 Htc1=200000.0 |
| 초월_구주호_AD | A12P 오오구치 — 적 이속 +15% 오라(손해 오라) | 오라(Aura) | Enemies | 1.15 | 0.0 | 1 | 1.0 |  | 925.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=0.15000000596046448 Oae2=0.0 abuf=B06P abpx=3 atat= aare=925.0 |
| 초월_노태현_AP | A0Q7 자장가 프람 — 적 이속 -5% 오라 | 오라(Aura) | Enemies | 0.95 | 0.0 | 1 | 1.0 |  | 99999.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.05000000074505806 Oae2=0.0 aare=99999.0 abuf=B03X abpx=1 atat= |
| 초월_노태현_AP | A0SR 오바드 꾸 드로와 — 적 이속 -15% 오라 | 오라(Aura) | Enemies | 0.85 | 0.0 | 1 | 1.0 |  | 875.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.15000000596046448 Oae2=0.0 aare=875.0 abuf=B04D abpx=1 atat= |
| 초월_두유찬_AD | 사보 — Sabo_Skill_3 화권(B00N 동안 1/10) | 평타 확률(OnHitChance) | Enemies | 0.7 | 2.5 | 1 | 0.1 |  | 415 |  |
| 초월_두유찬_AD | A0E6 투기 — 적 이속 −20% 오라 | 오라(Aura) | Enemies | 0.8 | 0.0 | 1 | 1.0 |  | 800.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.20000000298023224 Oae2=0.0 aare=800.0 abuf=B01C abpx=1 abpy=1 |
| 초월_박민수_AD | A0GQ 귀기-조초 — 적 이속 -30% 오라 | 오라(Aura) | Enemies | 0.7 | 0.0 | 1 | 1.0 |  | 815.0 | AOae aher=0 atar=air,enemies,ground Oae1=-0.30000001192092896 Oae2=0.0 aare=815.0 abuf=B02B abpx=1 atat= abpy=1 |
| 초월_신문철_AP | A09T 3범위 루피초월 둔화 | 평타 확률(OnHitChance) | Enemies | 0.67 | 2.0 | 1 | 0.175 |  | 425.0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 ahdu=1.0 adur=2.0 abuf= atar=air,enemies,neutral,ground aare=425.0 |
| 초월_양재모_AD | 로우 — B03Z 창(버프 부여) | N타째(OnHitCount) | Enemies | 0.55 | 5.0 | 1 | 1.0 |  | 825.0 |  |
| 초월_양재모_AD | 로키포트 사건의 주모자 — 1/8 | 평타 확률(OnHitChance) | Enemies | 0.6 | 3.0 | 1 | 0.125 |  | 485.0 |  |
| 초월_이태훈_AP | A0FW 중력중력열매 — 적 이속 −55% 오라 | 오라(Aura) | Enemies | 0.45 | 0.0 | 1 | 1.0 |  | 900.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.550000011920929 Oae2=0.0 abuf=B022 abpx=3 atat= |
| 초월_임채민_AP | A13V 3범위 검수초 지진 | 평타 확률(OnHitChance) | Enemies | 0.3 | 3.0 | 1 | 0.083333 |  | 650.0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 adur=3.0 abuf= atar=air,enemies,neutral,ground aare=650.0 Htc1=400000.0 |
| 초월_임채민_AP | 티치 — 강진 1/10 × 1/6(시전자 중심 700 범위 400000·이감 75% + 250000 + STR×3000) | 평타 확률(OnHitChance) | Enemies | 0.25 | 3.0 | 1 | 0.016667 |  | 700.0 |  |
| 초월_최상호_AP | A0PI 3범위 도플초월 오색실1 | 평타 확률(OnHitChance) | Enemies | 0.0 | 4.5 | 1 | 0.14285714285714285 |  | 525.0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 abuf= atar=air,enemies,neutral,ground aare=525.0 Htc1=205000.0 Htc4=0.0 |
| 초월_최상호_AP | 도플라밍고 [각성] — 새장 1/5(525 범위 150000·이감 4.5초 + 현재체력 1% + 10000 + 400000×1~1.5) | 평타 확률(OnHitChance) | Enemies | 0.0 | 4.5 | 1 | 0.2 |  | 525.0 |  |
| 초월_황준석_ADAP | A0E6 투기 — 적 이속 −20% 오라 | 오라(Aura) | Enemies | 0.8 | 0.0 | 1 | 1.0 |  | 800.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.20000000298023224 Oae2=0.0 aare=800.0 abuf=B01C abpx=1 abpy=1 |
| 초월_황준석_ADAP | A0Q7 자장가 프람 — 맵 전체 적 이속 −5% 오라 | 오라(Aura) | Enemies | 0.95 | 0.0 | 1 | 1.0 |  | 99999.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.05000000074505806 Oae2=0.0 aare=99999.0 abuf=B03X abpx=1 atat= |
| 초월_황준석_ADAP | A0SR 오바드 꾸 드로와 — 적 이속 −15% 오라 | 오라(Aura) | Enemies | 0.85 | 0.0 | 1 | 1.0 |  | 875.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.15000000596046448 Oae2=0.0 aare=875.0 abuf=B04D abpx=1 atat= |
| 초월_황준석_ADAP | A0DT 흉포한 광기 — 적 이속 −70% 오라 | 오라(Aura) | Enemies | 0.3 | 0.0 | 1 | 1.0 |  | 888.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.699999988079071 Oae2=0.0 aare=888.0 abuf=B01B abpx=1 atat= |
| 초월_황준석_ADAP | A10D 자석자석 열매 — 적 이속 −33% 오라 | 오라(Aura) | Enemies | 0.67 | 0.0 | 1 | 1.0 |  | 1250.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.33000001311302185 Oae2=0.0 aare=1250.0 abuf=B02O abpx=1 atat=L5.mdx |
| 초월_황준석_ADAP | A0SB 덩쿨 뿌리 — 적 이속 −12% 오라 | 오라(Aura) | Enemies | 0.88 | 0.0 | 1 | 1.0 |  | 500.0 | AOae aher=0 alev=1 atar=air,enemies,ground Oae1=-0.11999999731779099 Oae2=0.0 aare=500.0 abuf=B077 atat= abpx=1 |

요약: multiplier 범위 0 ~ 1.15, 서로 다른 값 14종.

## Stun (45건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/10 | 평타 확률(OnHitChance) | Enemies | 0.0 | 2.85 | 1 | 0.1 |  | 525.0 |  |
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/20 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.05 |  | 350.0 |  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000 + STR×15000)×1~1.5 ×3 + 2500000×1~2.5 · 스턴) | N타째(OnHitCount) | Enemies | 0.0 | 3.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_강주혁_AP | A15Z 엔마 — 25% ×11 +200000 · 스턴 3초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.25 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=200000.0 Hbh1=25.0 Hbh2=11.0 abpx=1 adur=3.0 ahdu=1.5 |
| 초월_구주호_AD | 키자루 — 빛의 기둥 1/12(500 범위 400000·스턴 2.75초) | 평타 확률(OnHitChance) | Enemies | 0.0 | 2.75 | 1 | 0.08333333333333333 |  | 500.0 |  |
| 초월_구주호_AD | A0CS !빛빛열매 — 15% ×8.88 · 스턴 3.0초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.15 |  | 0.0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 abuf= abpx=3 Hbh2=8.880000114440918 ahdu=1.5 adur=3.0 |
| 초월_구주호_AD | A065 금쇄봉 — 30% +50000 · 스턴 0.45초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.45 | 1 | 0.3 |  | 0.0 | ACbh arac=orc atar=air,enemies,ground Hbh3=50000.0 abuf= abpy=1 Hbh1=30.0 ahdu=0.44999998807907104 adur=0.44999998807907104 |
| 초월_김경현_AP | A0X4 고무고무 열매 — 20% ×3 · 스턴 0.5초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.5 | 1 | 0.2 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 adur=0.5 ahdu=0.25 abpx=3 Hbh2=3.0 Hbh1=20.0 |
| 초월_김만경_AD | 신 해군원수  — MANA게이지135.00 AND 버프B06B==true | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.075 |  | 600.0 |  |
| 초월_김만경_AD | 아카이누 — Akainu_03 용암분출 0.0925(450 범위 300000 + 대상 스턴 2.25초) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 2.25 | 1 | 0.0925 |  | 450.0 |  |
| 초월_김만경_AD | '아카이누 체력 스킬 — LIFE게이지50(PV 200 대상: 대분화)' | N타째(OnHitCount) | SingleTarget | 0.0 | 3.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_노태현_AP | 브룩 — MANA게이지115(600 범위 3000000·스턴 + AIsr +10) | N타째(OnHitCount) | Enemies | 0.0 | 3.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_노태현_AP | A04D 소울 솔리드 — 17.5% · 스턴 1.75초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.75 | 1 | 0.175 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 ahdu=0.8799999952316284 abpx=3 adur=1.75 Hbh1=17.5 |
| 초월_두유찬_AD | 혁명군 참모총장 — MANA게이지125.00 | N타째(OnHitCount) | Enemies | 0.0 | 1.25 | 1 | 1.0 |  | 500.0 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_4 작열(B00N 동안 9/10 × 1/6) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.85 | 1 | 0.15 |  | 415 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_1 화염용왕(절대쿨 12.5초 B00N) | 평타 확률(OnHitChance) | Enemies | 0.0 | 0.85 | 1 | 1.0 |  | 475 |  |
| 초월_박민수_AD | 수라의 검사 — MANA게이지145.00 | N타째(OnHitCount) | Enemies | 0.0 | 3.0 | 1 | 1.0 |  | 525.0 |  |
| 초월_박민수_AD | A0PZ 이강력참1 — 25% ×10 +200000 · 스턴 3초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.25 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=200000.0 Hbh1=25.0 Hbh2=10.0 abpx=1 adur=3.0 ahdu=1.5 |
| 초월_박민수_AD | 조로 — Zoro_saza1 1/6(405 범위 (602500 + STR×6000)×1~1.5 + 대상 (903750 + STR×60000)×1~1.5 · 스턴 1초) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.0 | 1 | 0.166667 |  | 405.0 |  |
| 초월_박민수_AD | 조로 — Zoro_tiger 1/33(500 범위 2500000 + STR×50000 · stomp 150000·스턴 2.5초) | 평타 확률(OnHitChance) | Enemies | 0.0 | 2.5 | 1 | 0.030303 |  | 500.0 |  |
| 초월_배성령_AD | A11Y 디아블 잠브-검은다리-상초 — 20% · 스턴 0.25초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.25 | 1 | 0.2 |  | 0.0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 Hbh1=20.0 adur=0.25 ahdu=0.11999999731779099 abuf= abpx=1 abpy=1 |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | SingleTarget | 0.0 | 2.15 | 1 | 1.0 |  | 625.0 |  |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | Enemies | 0.0 | 2.5 | 1 | 1.0 |  | 625.0 |  |
| 초월_신문철_AP | A09Q 0장풍 기간트개틀링1 | 평타 확률(OnHitChance) | Enemies | 0.0 | 1.0 | 1 | 0.0125 |  | 405.0 | ANcs alev=1 aher=0 amcs=0 aran=1500.0 ahdu=1.0 adur=1.0 Ncs2=0.14000000059604645 Ncs3=26 Ncs1=47500.0 |
| 초월_유재헌_ADAP | A0EQ !해류조정-시라호시 | 평타 확률(OnHitChance) | Enemies | 0 | 2.15 | 1 | 0.11111 |  | 600.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_유재헌_ADAP | A0EQ !해류조정-시라호시 | 평타 확률(OnHitChance) | Enemies | 0 | 2.35 | 1 | 0.11111 |  | 600.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_유재헌_ADAP | 시라호시 마나 — MANA게이지120 | N타째(OnHitCount) | Enemies | 0.0 | 3.0 | 1 | 1.0 |  | 800.0 |  |
| 초월_이태훈_AP | 후지토라 — 1/7 (Huji01 중력 베기) | 평타 확률(OnHitChance) | Enemies | 0.0 | 2.59 | 1 | 0.14285714285714285 |  | 485.0 |  |
| 초월_조성진_AD | G.O.D — MANA게이지140.00 | N타째(OnHitCount) | Enemies | 0.0 | 3.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_조성진_AD | A0HK 저격왕-우솝초 — 15% ×3.5 · 스턴 5초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 5.0 | 1 | 0.15 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 adur=5.0 ahdu=5.0 abpx=3 Hbh2=3.5 areq=h04X |
| 초월_조성진_AD | A0G4 화염성 — 100% · 스턴 1초 (PV 200 대상만) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.0 | 1 | 1.0 |  | 0.0 | ACbh arac=orc atar=air,ancient,enemies,ground Hbh3=0.0 adur=1.0 ahdu=1.0 abuf= areq=h04X Hbh1=100.0 |
| 초월_최상호_AD | 어인협객 — MANA게이지115.00 | N타째(OnHitCount) | Enemies | 0.0 | 3.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_최상호_AD | A09S !2범위 호크 개틀링 더미 | 평타 확률(OnHitChance) | Enemies | 0 | 1.5 | 1 | 0.125 |  | 500.0 | AOws alev=1 aher=0 atar=air,invulnerable,enemies,ground,vulnerable ahdu=0.2199999988079071 acdn=0.0 aare=500.0 Wrs1=102500.0 acat= amcs=0 |
| 초월_최상호_AD | A0X0 어인공수도-징베 — 40% · 스턴 1.5초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.5 | 1 | 0.4 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 Hbh1=40.0 abpx=3 adur=1.5 ahdu=0.75 |
| 초월_황준석_ADAP | A0G4 !화염성 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.0 | 1 | 1.0 |  | 0 | ACbh arac=orc atar=air,ancient,enemies,ground Hbh3=0.0 adur=1.0 ahdu=1.0 abuf= areq=h04X Hbh1=100.0 |
| 초월_황준석_ADAP | 키드 — Kid_Skill_Life2 펑크 1/16(500 범위 500000 + STR×5000 · 대상 (125000 + STR×5000)×2 · 스턴 3초) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.0625 |  | 500.0 |  |
| 초월_황준석_ADAP | A0IT !강철 풍선 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.75 | 1 | 0.2 |  | 0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 Hbh1=20.0 adur=0.75 ahdu=0.3799999952316284 abuf= abpx=3 amat=bigmamslash.mdx |
| 초월_황준석_ADAP | A04D !소울 솔리드 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.75 | 1 | 0.175 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 ahdu=0.8799999952316284 abpx=3 adur=1.75 Hbh1=17.5 |
| 초월_황준석_ADAP | A0X0 !어인공수도-징베 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.5 | 1 | 0.4 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 Hbh1=40.0 abpx=3 adur=1.5 ahdu=0.75 |
| 초월_황준석_ADAP | A0AG !10톤 해머 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.0 | 1 | 0.2 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 Hbh1=20.0 abpx=3 Hbh2=7.0 adur=1.0 ahdu=1.0 |
| 초월_황준석_ADAP | A0HK !저격왕-우솝초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 5.0 | 1 | 0.15 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 adur=5.0 ahdu=5.0 abpx=3 Hbh2=3.5 areq=h04X |
| 초월_황준석_ADAP | A0CS !빛빛열매 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 3.0 | 1 | 0.15 |  | 0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 abuf= abpx=3 Hbh2=8.880000114440918 ahdu=1.5 adur=3.0 |
| 초월_황준석_ADAP | A03W !패기의 검술 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.5 | 1 | 0.12 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=500000.0 Hbh1=12.0 abpx=3 Hbh2=10.0 ahdu=0.75 adur=1.5 |
| 초월_황준석_ADAP | A101 !자석 팔 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 1.5 | 1 | 0.35 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=360000.0 Hbh1=35.0 abpx=3 ahdu=0.75 adur=1.5 |
| 초월_황준석_ADAP | 키드 체력 스킬 — LIFE게이지50(500 범위 2000000·스턴 2초 + 대상 1000000 + STR×50000) | N타째(OnHitCount) | Enemies | 0.0 | 2.0 | 1 | 1.0 |  | 500.0 |  |

요약: multiplier 범위 0 ~ 0, 서로 다른 값 1종.

## ArmorBreak (9건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_김민준_AP | 해군의 홍일점 — 9% (Tasigi_02) | 평타 확률(OnHitChance) | Enemies | 5.0 | 0.0 | 1 | 0.09 |  | 550.0 |  |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | Enemies | 7.0 | 0.0 | 1 | 1.0 |  | 625.0 |  |
| 초월_이태훈_AP | A0GR !중력장 | 평타 확률(OnHitChance) | Enemies | 3.0 | 0.0 | 1 | 0.041666666666666664 |  | 485.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | A0GR !중력장 | 평타 확률(OnHitChance) | Enemies | 3.0 | 0.0 | 1 | 0.041666666666666664 |  | 485.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | 후지토라 — MANA게이지140 (Huji_03) | N타째(OnHitCount) | Enemies | 9.0 | 0.0 | 1 | 1.0 |  | 700.0 |  |
| 초월_이태훈_AP | 후지토라 — 1/7 (Huji01 중력 베기) | 평타 확률(OnHitChance) | Enemies | 2.0 | 0.0 | 1 | 0.14285714285714285 |  | 485.0 |  |
| 초월_최상호_AD | 어인협객 — 1/16 | 평타 확률(OnHitChance) | Enemies | 3.0 | 0.0 | 1 | 0.0625 |  | 625.0 |  |
| 초월_최상호_AD | 어인협객 — MANA게이지115.00 | N타째(OnHitCount) | Enemies | 5.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_최상호_AD | 징베 — Jimbe_jingak 1/7(450 범위 1000000 + STR×17500, 그 뒤 AId1 +1) | 평타 확률(OnHitChance) | Enemies | 1.0 | 0.0 | 1 | 0.14285714285714285 |  | 450.0 |  |

요약: multiplier 범위 1 ~ 9, 서로 다른 값 6종.

## AttackPowerBuffPercent (4건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강재규_AP | A0V8 밀 플루르-마노스 공증1 — 아군 공격력 +25% | 오라(Aura) | Self | 0.25 | 0.0 | 1 | 1.0 |  | 850.0 | ACac arac=orc aare=850.0 Cac1=0.25 abuf=B04R atar=air,invulnerable,organic,self,ground,vulnerable,friend abpx=3 atat= |
| 초월_강재규_AP | A0V8 밀 플루르-마노스 공증1 — 아군 공격력 +25% | 오라(Aura) | Allies | 0.25 | 0.0 | 1 | 1.0 |  | 850.0 | ACac arac=orc aare=850.0 Cac1=0.25 abuf=B04R atar=air,invulnerable,organic,self,ground,vulnerable,friend abpx=3 atat= |
| 초월_양재모_AD | 로우 — B03Z 창(버프 부여) | N타째(OnHitCount) | Self | 0.2 | 5.0 | 1 | 1.0 |  | 825.0 |  |
| 초월_양재모_AD | 로우 — B03Z 창(버프 부여) | N타째(OnHitCount) | Allies | 0.2 | 5.0 | 1 | 1.0 |  | 825.0 |  |

요약: multiplier 범위 0.2 ~ 0.25, 서로 다른 값 2종.

## AttackSpeedBuffPercent (14건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강주혁_AP | A0GZ 천재항해사 — 자기 공속 +12% | 오라(Aura) | Self | 0.12 | 0.0 | 1 | 1.0 |  | 850.0 | AOae aher=0 alev=1 atar=invulnerable,self Oae1=0.0 Oae2=0.11999999731779099 aare=850.0 abuf=B02C abpx=3 atat= |
| 초월_구주호_AD | 야마토 — 1/4 자기 공속 +400% 1.25초(B06N) | 평타 확률(OnHitChance) | Self | 4.0 | 1.25 | 1 | 0.25 |  | 0.0 |  |
| 초월_구주호_AD | 초록소 — MANA게이지115(405 범위 2000000×1~1.5 ×5 + 자기 공속 +5%·공격력 +4000 6.85초) | N타째(OnHitCount) | Self | 0.05 | 6.85 | 1 | 1.0 |  | 405.0 |  |
| 초월_김경현_AP | A0YS 고강도 탄성 — 자기 공속 +15% | 오라(Aura) | Self | 0.15 | 0.0 | 1 | 1.0 |  | 900.0 | AOae aher=0 alev=1 atar=invulnerable,self Oae1=0.0 Oae2=0.15000000596046448 abuf=B05K abpx=1 atat=Ora_shanks.mdx atac=1 |
| 초월_노태현_AP | 소울 킹 — 1/24(B00Y 창) | 평타 확률(OnHitChance) | Self | 1.0 | 0.85 | 1 | 0.041666666666666664 |  | 0.0 |  |
| 초월_배성령_AD | A0V4 요리사의 길 — 아군 공속 +15% | 오라(Aura) | Self | 0.15 | 0.0 | 1 | 1.0 |  | 900.0 | AOae aher=0 alev=1 atar=air,invulnerable,self,ground,vulnerable,friend Oae1=0.0 Oae2=0.15000000596046448 abuf=B04Q atat=Abilities\Weapons\FireBallMissile\FireBallMissile.mdl,Abilities\Weapons\FireBallMissile\FireBallMissile.mdl abpy=1 atac=2 |
| 초월_배성령_AD | A0V4 요리사의 길 — 아군 공속 +15% | 오라(Aura) | Allies | 0.15 | 0.0 | 1 | 1.0 |  | 900.0 | AOae aher=0 alev=1 atar=air,invulnerable,self,ground,vulnerable,friend Oae1=0.0 Oae2=0.15000000596046448 abuf=B04Q atat=Abilities\Weapons\FireBallMissile\FireBallMissile.mdl,Abilities\Weapons\FireBallMissile\FireBallMissile.mdl abpy=1 atac=2 |
| 초월_배성령_AD | A0QE 상초강 공속 — 자기 공속 +50%(상시) | 오라(Aura) | Self | 0.5 | 0.0 | 1 | 1.0 |  | 0.0 | AIsx Isx1=0.5 aite=0 |
| 초월_최상호_AD | A0WL 의협 — 아군 공속 +20% | 오라(Aura) | Self | 0.2 | 0.0 | 1 | 1.0 |  | 850.0 | AOae aher=0 alev=1 atar=air,invulnerable,self,ground,vulnerable,friend Oae1=0.0 Oae2=0.20000000298023224 aare=850.0 abuf=B052 abpx=3 atat= |
| 초월_최상호_AD | A0WL 의협 — 아군 공속 +20% | 오라(Aura) | Allies | 0.2 | 0.0 | 1 | 1.0 |  | 850.0 | AOae aher=0 alev=1 atar=air,invulnerable,self,ground,vulnerable,friend Oae1=0.0 Oae2=0.20000000298023224 aare=850.0 abuf=B052 abpx=3 atat= |
| 초월_황준석_ADAP | A0WL 의협 — 아군 공속 +20% 오라 | 오라(Aura) | Self | 0.2 | 0.0 | 1 | 1.0 |  | 850.0 | AOae aher=0 alev=1 atar=air,invulnerable,self,ground,vulnerable,friend Oae1=0.0 Oae2=0.20000000298023224 aare=850.0 abuf=B052 abpx=3 atat= |
| 초월_황준석_ADAP | A0WL 의협 — 아군 공속 +20% 오라 | 오라(Aura) | Allies | 0.2 | 0.0 | 1 | 1.0 |  | 850.0 | AOae aher=0 alev=1 atar=air,invulnerable,self,ground,vulnerable,friend Oae1=0.0 Oae2=0.20000000298023224 aare=850.0 abuf=B052 abpx=3 atat= |
| 초월_황준석_ADAP | A0I1 카리스마 — 자기 공속 +20% | 오라(Aura) | Self | 0.2 | 0.0 | 1 | 1.0 |  | 900.0 | AOae aher=0 alev=1 atar=invulnerable,self Oae1=0.0 Oae2=0.20000000298023224 abuf=B02L abpx=3 atat=Ora_shanks.mdx atac=1 |
| 초월_황준석_ADAP | A0QE 상초강 공속 — 자기 공속 +50%(상시) | 오라(Aura) | Self | 0.5 | 0.0 | 1 | 1.0 |  | 0.0 | AIsx Isx1=0.5 aite=0 |

요약: multiplier 범위 0.05 ~ 4, 서로 다른 값 7종.

## AttackPowerBuffFlat (5건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강재규_AP | A0VH 전투보조 — 아군 공격력 +5000 | 오라(Aura) | Self | 5000.0 | 0.0 | 1 | 1.0 |  | 850.0 | ACac arac=orc aare=850.0 Cac1=5000.0 abuf=B04U atar=air,invulnerable,organic,self,ground,vulnerable,friend abpx=1 atat= abpy=1 Ear4=1 |
| 초월_강재규_AP | A0VH 전투보조 — 아군 공격력 +5000 | 오라(Aura) | Allies | 5000.0 | 0.0 | 1 | 1.0 |  | 850.0 | ACac arac=orc aare=850.0 Cac1=5000.0 abuf=B04U atar=air,invulnerable,organic,self,ground,vulnerable,friend abpx=1 atat= abpy=1 Ear4=1 |
| 초월_구주호_AD | 초록소 — MANA게이지115(405 범위 2000000×1~1.5 ×5 + 자기 공속 +5%·공격력 +4000 6.85초) | N타째(OnHitCount) | Self | 4000.0 | 6.85 | 1 | 1.0 |  | 405.0 |  |
| 초월_양재모_AD | 로우 — B03Z 창(버프 부여) | N타째(OnHitCount) | Self | 30000.0 | 5.0 | 1 | 1.0 |  | 825.0 |  |
| 초월_황준석_ADAP | A102 자석자석열매 — 자기 평타 +50000 | 오라(Aura) | Self | 50000.0 | 0.0 | 1 | 1.0 |  | 0.0 | AIfb aite=0 atar=air,enemies,ground Idam=50000.0 aare=400.0 amat= atat= asat= aspt= abpx=2 |

요약: multiplier 범위 4000 ~ 50000, 서로 다른 값 4종.

## Damage (209건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/10 | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0 | 1 | 0.1 |  | 525.0 |  |
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/20 | 평타 확률(OnHitChance) | Enemies | 600000.0 | 0 | 1 | 0.05 |  | 350.0 |  |
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/20 | 평타 확률(OnHitChance) | SingleTarget | 120000.0 | 0.0 | 1 | 0.05 |  | 350.0 |  |
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40 | N타째(OnHitCount) | Enemies | 1850000.0 | 0.0 | 1 | 1.0 |  | 700.0 |  |
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40 | N타째(OnHitCount) | Enemies | 0.01 | 0.0 | 1 | 1.0 |  | 700.0 |  |
| 초월_강재규_AP | 오하라의 마지막 생존자 — LIFE게이지40 | N타째(OnHitCount) | SingleTarget | 200000.0 | 0.0 | 1 | 1.0 |  | 700.0 |  |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.111111 |  | 300.0 |  |
| 초월_강주혁_AP | 웨더리아의 항해사 — 1/9 | 평타 확률(OnHitChance) | SingleTarget | 400000.0 | 0.0 | 1 | 0.111111 |  | 300.0 |  |
| 초월_강주혁_AP | 나미 — 5절대쿨 | 평타 확률(OnHitChance) | Enemies | 350000.0 | 0 | 1 | 1.0 |  | 500 |  |
| 초월_강주혁_AP | 염왕 — 1/15 | 평타 확률(OnHitChance) | Enemies | 21000.0 | 0.0 | 1 | 0.066667 |  | 425.0 |  |
| 초월_강주혁_AP | 염왕 — 1/15 | 평타 확률(OnHitChance) | Enemies | 532500.0 | 0 | 1 | 0.066667 |  | 425.0 |  |
| 초월_강주혁_AP | A0B0 2범위 나미초월 블리자드3 | 평타 확률(OnHitChance) | Enemies | 500000.0 | 3.0 | 1 | 1.0 |  | 500.0 | ACbz amcs=0 acas=0.20000000298023224 Hbz1=3 Hbz2=500000.0 aare=500.0 Hbz4=0.0 acdn=0.0 aeff=X003 Hbz3=4 |
| 초월_강주혁_AP | A0B0 2범위 나미초월 블리자드3 | 평타 확률(OnHitChance) | Enemies | 500000.0 | 3.0 | 1 | 1.0 |  | 500.0 | ACbz amcs=0 acas=0.20000000298023224 Hbz1=3 Hbz2=500000.0 aare=500.0 Hbz4=0.0 acdn=0.0 aeff=X003 Hbz3=4 |
| 초월_강주혁_AP | 나미 — Nami_Skill_1 헤비레인 1/24(400 범위 300000×1~1.5 ×8) | 평타 확률(OnHitChance) | Enemies | 300000.0 | 3.0 | 1 | 0.041666666666666664 |  | 400.0 |  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000 + STR×15000)×1~1.5 ×3 + 2500000×1~2.5 · 스턴) | N타째(OnHitCount) | Enemies | 15000.0 | 0.18 | 1 | 1.0 |  | 600.0 |  |
| 초월_강주혁_AP | 염왕 — MANA게이지145(600 범위 (1500000 + STR×15000)×1~1.5 ×3 + 2500000×1~2.5 · 스턴) | N타째(OnHitCount) | Enemies | 2500000.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_강주혁_AP | A15Z 엔마 — 25% ×11 +200000 · 스턴 3초 | 평타 확률(OnHitChance) | SingleTarget | 11.0 | 0.0 | 1 | 0.25 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=200000.0 Hbh1=25.0 Hbh2=11.0 abpx=1 adur=3.0 ahdu=1.5 |
| 초월_구주호_AD | 오니히메 인수화 — 매 타(Yamato_Attack_Damage2, B06N 없을 때) | 평타 확률(OnHitChance) | Enemies | 0.75 | 0 | 1 | 1.0 |  | 325.0 |  |
| 초월_구주호_AD | 해군대장-보르살리노 — 1/17 | 평타 확률(OnHitChance) | Enemies | 200000.0 | 2.56 | 1 | 0.058824 |  | 385.0 |  |
| 초월_구주호_AD | '신'해군대장-초록소 — 1/7 | 평타 확률(OnHitChance) | Enemies | 700000.0 | 0 | 1 | 0.142857 |  | 485.0 |  |
| 초월_구주호_AD | A160 3범위 료쿠규 초월 둔화 | 평타 확률(OnHitChance) | SingleTarget | 200000.0 | 0 | 1 | 0.14285714285714285 |  | 0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 adur=3.0 abuf= atar=air,enemies,neutral,ground aare=500.0 Htc1=200000.0 |
| 초월_구주호_AD | 키자루 — 빛의 기둥 1/12(500 범위 400000·스턴 2.75초) | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.08333333333333333 |  | 500.0 |  |
| 초월_구주호_AD | 키자루 — Kizaru_01 빛의 발차기 1/12(450 범위 350000 ×3 + 대상 ×3.25) | 평타 확률(OnHitChance) | Enemies | 350000.0 | 0.54 | 1 | 0.08333333333333333 |  | 450.0 |  |
| 초월_구주호_AD | 키자루 — Kizaru_01 빛의 발차기 1/12(450 범위 350000 ×3 + 대상 ×3.25) | 평타 확률(OnHitChance) | SingleTarget | 1137500.0 | 0.54 | 1 | 0.08333333333333333 |  | 450.0 |  |
| 초월_구주호_AD | A0CS !빛빛열매 — 15% ×8.88 · 스턴 3.0초 | 평타 확률(OnHitChance) | SingleTarget | 8.88 | 0.0 | 1 | 0.15 |  | 0.0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 abuf= abpx=3 Hbh2=8.880000114440918 ahdu=1.5 adur=3.0 |
| 초월_구주호_AD | A065 금쇄봉 — 30% +50000 · 스턴 0.45초 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.0 | 1 | 0.3 |  | 0.0 | ACbh arac=orc atar=air,enemies,ground Hbh3=50000.0 abuf= abpy=1 Hbh1=30.0 ahdu=0.44999998807907104 adur=0.44999998807907104 |
| 초월_구주호_AD | '야마토 — 매 타(B06N 동안: 425 범위 K×0.75 + 15000 · K×0.20 + 15000)' | 평타 확률(OnHitChance) | Enemies | 0.75 | 0.0 | 1 | 1.0 |  | 425.0 |  |
| 초월_구주호_AD | '야마토 — 매 타(B06N 동안: 425 범위 K×0.75 + 15000 · K×0.20 + 15000)' | 평타 확률(OnHitChance) | Enemies | 0.2 | 0.0 | 1 | 1.0 |  | 425.0 |  |
| 초월_구주호_AD | 야마토 — 뇌명 15%(525 범위 (K×3.75 + 375000) × 이속 계수) | 평타 확률(OnHitChance) | Enemies | 4.2375 | 0.0 | 1 | 0.15 |  | 525.0 |  |
| 초월_구주호_AD | 야마토 — Yamato_Dash 매 타(450 범위 K×1.15 + 215000 · 대상 ×1.25) | 평타 확률(OnHitChance) | Enemies | 1.15 | 0.0 | 1 | 1.0 |  | 450.0 |  |
| 초월_구주호_AD | 야마토 — Yamato_Dash 매 타(450 범위 K×1.15 + 215000 · 대상 ×1.25) | 평타 확률(OnHitChance) | SingleTarget | 1.4375 | 0.0 | 1 | 1.0 |  | 450.0 |  |
| 초월_구주호_AD | 초록소 — MANA게이지115(405 범위 2000000×1~1.5 ×5 + 자기 공속 +5%·공격력 +4000 6.85초) | N타째(OnHitCount) | Enemies | 2000000.0 | 1.6 | 1 | 1.0 |  | 405.0 |  |
| 초월_구주호_AD | 초록소 — 전방위 흡수 1/25(시전자 중심 925 범위 1000000×1~1.5) | 평타 확률(OnHitChance) | Enemies | 1000000.0 | 0.0 | 1 | 0.04 |  | 925.0 |  |
| 초월_구주호_AD | 키자루 — 장풍 1/10(A0BV 피해 500000 · 길이 900 · 반경 250→375) | 평타 확률(OnHitChance) | Enemies | 500000.0 | 0.0 | 1 | 0.1 |  | 0.0 |  |
| 초월_김경현_AP | 다섯번째 황제 — 1/15 | 평타 확률(OnHitChance) | SingleTarget | 350000.0 | 0 | 1 | 0.066667 |  | 475.0 |  |
| 초월_김경현_AP | 다섯번째 황제 — 1/15 | 평타 확률(OnHitChance) | Enemies | 1400.0 | 0 | 1 | 0.066667 |  | 475.0 |  |
| 초월_김경현_AP | 다섯번째 황제 — MANA게이지85.00 | N타째(OnHitCount) | SingleTarget | 1875000.0 | 0 | 1 | 1.0 |  | 550.0 |  |
| 초월_김경현_AP | 다섯번째 황제 — MANA게이지85.00 | N타째(OnHitCount) | Enemies | 3500.0 | 0 | 1 | 1.0 |  | 550.0 |  |
| 초월_김경현_AP | A0X4 고무고무 열매 — 20% ×3 · 스턴 0.5초 | 평타 확률(OnHitChance) | SingleTarget | 3.0 | 0.0 | 1 | 0.2 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 adur=0.5 ahdu=0.25 abpx=3 Hbh2=3.0 Hbh1=20.0 |
| 초월_김경현_AP | 스네이크맨 — 평타 범위 35%(400 범위 75000 × (1 + 0.003×이속) ×1~2) | 평타 확률(OnHitChance) | Enemies | 225.0 | 0.0 | 1 | 0.35 |  | 400.0 |  |
| 초월_김만경_AD | A0HG !유성화산-아카초 | N타째(OnHitCount) | Enemies | 664286.0 | 0 | 1 | 1.0 |  | 465.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=4 abpx=0 |
| 초월_김만경_AD | A0HG !유성화산-아카초 | N타째(OnHitCount) | Enemies | 150000.0 | 1.595 | 1 | 1.0 |  | 465.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=4 abpx=0 |
| 초월_김만경_AD | A0HG !유성화산-아카초 | N타째(OnHitCount) | Enemies | 664286.0 | 0 | 1 | 1.0 |  | 465.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=4 abpx=0 |
| 초월_김만경_AD | A0HG !유성화산-아카초 | N타째(OnHitCount) | Enemies | 150000.0 | 1.595 | 1 | 1.0 |  | 465.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=4 abpx=0 |
| 초월_김만경_AD | 신 해군원수  — MANA게이지135.00 AND 버프B06B==true | 평타 확률(OnHitChance) | Enemies | 5000.0 | 0.0 | 1 | 0.075 |  | 600.0 |  |
| 초월_김만경_AD | 신 해군원수  — MANA게이지135.00 AND 버프B06B==true | 평타 확률(OnHitChance) | SingleTarget | 50000.0 | 0.0 | 1 | 0.075 |  | 600.0 |  |
| 초월_김만경_AD | 신 해군원수  — MANA게이지135.00 AND 버프B06B==true | 평타 확률(OnHitChance) | SingleTarget | 0.12 | 0.0 | 1 | 0.075 |  | 600.0 |  |
| 초월_김만경_AD | 신 해군원수  — MANA게이지135.00 AND 버프B06B==true | 평타 확률(OnHitChance) | Enemies | 150000.0 | 1.595 | 1 | 0.075 |  | 600.0 |  |
| 초월_김만경_AD | 아카이누 — Akainu_03 용암분출 0.0925(450 범위 300000 + 대상 스턴 2.25초) | 평타 확률(OnHitChance) | Enemies | 300000.0 | 0.0 | 1 | 0.0925 |  | 450.0 |  |
| 초월_김만경_AD | 아카이누 — Akainu_03 용암분출 0.0925(450 범위 300000 + 대상 스턴 2.25초) | 평타 확률(OnHitChance) | Enemies | 150000.0 | 1.595 | 1 | 0.0925 |  | 450.0 |  |
| 초월_김만경_AD | '아카이누 체력 스킬 — LIFE게이지50(PV 200 대상: 대분화)' | N타째(OnHitCount) | Enemies | 5000.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_김만경_AD | '아카이누 체력 스킬 — LIFE게이지50(PV 200 대상: 대분화)' | N타째(OnHitCount) | SingleTarget | 50000.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_김만경_AD | '아카이누 체력 스킬 — LIFE게이지50(PV 200 대상: 대분화)' | N타째(OnHitCount) | Enemies | 150000.0 | 1.595 | 1 | 1.0 |  | 600.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 16% (tahsigi, A0B7 Lv1) | 평타 확률(OnHitChance) | Enemies | 5000.0 | 0.0 | 1 | 0.16 |  | 450.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 16% (tahsigi, A0B7 Lv1) | 평타 확률(OnHitChance) | Enemies | 6.0 | 0 | 1 | 0.16 |  | 450.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 16% (tahsigi, A0B7 Lv1) | 평타 확률(OnHitChance) | Enemies | 2500.0 | 0 | 1 | 0.16 |  | 450.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 16% (tahsigi, A0B7 Lv1) | 평타 확률(OnHitChance) | SingleTarget | 500000.0 | 0 | 1 | 0.16 |  | 450.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — MANA게이지135.00 | N타째(OnHitCount) | SingleTarget | 400000.0 | 0 | 1 | 1.0 |  | 525.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — MANA게이지135.00 | N타째(OnHitCount) | SingleTarget | 0.1 | 0 | 1 | 1.0 |  | 525.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — MANA게이지135.00 | N타째(OnHitCount) | Enemies | 5000.0 | 0.0 | 1 | 1.0 |  | 525.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — MANA게이지135.00 | N타째(OnHitCount) | Enemies | 3500.0 | 1.1 | 1 | 1.0 |  | 525.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — MANA게이지135.00 | N타째(OnHitCount) | Enemies | 5000.0 | 0.0 | 1 | 1.0 |  | 525.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 9% (Tasigi_02) | 평타 확률(OnHitChance) | SingleTarget | 500000.0 | 0.0 | 1 | 0.09 |  | 550.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 9% (Tasigi_02) | 평타 확률(OnHitChance) | Enemies | 9.0 | 0.0 | 1 | 0.09 |  | 550.0 |  |
| 초월_김민준_AP | 해군의 홍일점 — 9% (Tasigi_02) | 평타 확률(OnHitChance) | Enemies | 6000.0 | 0.0 | 1 | 0.09 |  | 550.0 |  |
| 초월_노태현_AP | 소울 킹 — 1/7 | 평타 확률(OnHitChance) | Enemies | 350000.0 | 0 | 1 | 0.142857 |  | 360.0 |  |
| 초월_노태현_AP | 소울 킹 — 1/7 | 평타 확률(OnHitChance) | Enemies | 350000.0 | 0 | 0.4 | 0.142857 |  | 360.0 |  |
| 초월_노태현_AP | 소울 킹 — 버프B00Y==true | 평타 확률(OnHitChance) | SingleTarget | 0.06 | 0.0 | 1 | 1.0 |  | 0.0 |  |
| 초월_노태현_AP | 소울 킹 — 버프B00Y==true | 평타 확률(OnHitChance) | SingleTarget | 75000.0 | 0.0 | 1 | 1.0 |  | 0.0 |  |
| 초월_노태현_AP | 브룩 — MANA게이지115(600 범위 3000000·스턴 + AIsr +10) | N타째(OnHitCount) | Enemies | 3000000.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_두유찬_AD | 혁명군 참모총장 — MANA게이지125.00 | N타째(OnHitCount) | Enemies | 3750000.0 | 0 | 1 | 1.0 |  | 500.0 |  |
| 초월_두유찬_AD | 혁명군 참모총장 — MANA게이지125.00 | N타째(OnHitCount) | Enemies | 0.02 | 0 | 1 | 1.0 |  | 500.0 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_3 화권(B00N 동안 1/10) | 평타 확률(OnHitChance) | Enemies | 1150000.0 | 0 | 1 | 0.1 |  | 415 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_4 작열(B00N 동안 9/10 × 1/6) | 평타 확률(OnHitChance) | Enemies | 500000.0 | 0 | 1 | 0.15 |  | 415 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_4 작열(B00N 동안 9/10 × 1/6) | 평타 확률(OnHitChance) | SingleTarget | 500000.0 | 0.0 | 1 | 0.15 |  | 415 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_4 작열(B00N 동안 9/10 × 1/6) | 평타 확률(OnHitChance) | SingleTarget | 0.04 | 0.0 | 1 | 0.15 |  | 415 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_4 작열(B00N 동안 9/10 × 1/6) | 평타 확률(OnHitChance) | SingleTarget | 0.01 | 0.0 | 1 | 0.15 |  | 415 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_1 화염용왕(절대쿨 12.5초 B00N) | 평타 확률(OnHitChance) | Enemies | 3250000.0 | 0 | 1 | 1.0 |  | 475 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_1 화염용왕(절대쿨 12.5초 B00N) | 평타 확률(OnHitChance) | Enemies | 1.0 | 0.0 | 1 | 1.0 |  | 475 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_1 화염용왕(절대쿨 12.5초 B00N) | 평타 확률(OnHitChance) | Enemies | 0.015 | 0.0 | 1 | 1.0 |  | 475 |  |
| 초월_두유찬_AD | 사보 — Sabo_Skill_1 화염용왕(절대쿨 12.5초 B00N) | 평타 확률(OnHitChance) | SingleTarget | 2500000.0 | 0.0 | 1 | 1.0 |  | 475 |  |
| 초월_두유찬_AD | A0HA !용의 숨결 — 15% ×4.0 | 평타 확률(OnHitChance) | SingleTarget | 4.0 | 0.0 | 1 | 0.15 |  | 0.0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 adur=0.0 ahdu=0.0 abuf= abpx=0 Hbh2=4.0 |
| 초월_박기찬_AD | A069 !꾸드방-프랑초 | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0 | 1 | 0.1 |  | 400.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_박기찬_AD | A069 !꾸드방-프랑초 | 평타 확률(OnHitChance) | SingleTarget | 200000.0 | 0 | 1 | 0.1 |  | 400.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_박기찬_AD | A069 !꾸드방-프랑초 | 평타 확률(OnHitChance) | Enemies | 500000.0 | 0 | 1 | 0.1 |  | 400.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_박기찬_AD | A069 !꾸드방-프랑초 | 평타 확률(OnHitChance) | SingleTarget | 250000.0 | 0 | 1 | 0.1 |  | 400.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_박기찬_AD | 무적의 아이언 파이러츠 — MANA게이지100.00 | N타째(OnHitCount) | Enemies | 4000000.0 | 0 | 1 | 1.0 |  | 425.0 |  |
| 초월_박기찬_AD | 무적의 아이언 파이러츠 — 1/14 | 평타 확률(OnHitChance) | Enemies | 180000.0 | 0.2 | 1 | 0.071429 |  | 400.0 |  |
| 초월_박민수_AD | 수라의 검사 — MANA게이지145.00 | N타째(OnHitCount) | Enemies | 2500000.0 | 0 | 1 | 1.0 |  | 525.0 |  |
| 초월_박민수_AD | 수라의 검사 — 1/15 | 평타 확률(OnHitChance) | Enemies | 350000.0 | 0.35 | 1 | 0.066667 |  | 500.0 |  |
| 초월_박민수_AD | 수라의 검사 — 1/15 | 평타 확률(OnHitChance) | Enemies | 21000.0 | 0.0 | 1 | 0.066667 |  | 500.0 |  |
| 초월_박민수_AD | A0PZ 이강력참1 — 25% ×10 +200000 · 스턴 3초 | 평타 확률(OnHitChance) | SingleTarget | 10.0 | 0.0 | 1 | 0.25 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=200000.0 Hbh1=25.0 Hbh2=10.0 abpx=1 adur=3.0 ahdu=1.5 |
| 초월_박민수_AD | 조로 — Zoro_saza1 1/6(405 범위 (602500 + STR×6000)×1~1.5 + 대상 (903750 + STR×60000)×1~1.5 · 스턴 1초) | 평타 확률(OnHitChance) | Enemies | 6000.0 | 0.0 | 1 | 0.166667 |  | 405.0 |  |
| 초월_박민수_AD | 조로 — Zoro_saza1 1/6(405 범위 (602500 + STR×6000)×1~1.5 + 대상 (903750 + STR×60000)×1~1.5 · 스턴 1초) | 평타 확률(OnHitChance) | SingleTarget | 60000.0 | 0.0 | 1 | 0.166667 |  | 405.0 |  |
| 초월_박민수_AD | 조로 — Zoro_tiger 1/33(500 범위 2500000 + STR×50000 · stomp 150000·스턴 2.5초) | 평타 확률(OnHitChance) | Enemies | 150000.0 | 0.0 | 1 | 0.030303 |  | 500.0 |  |
| 초월_박민수_AD | 조로 — Zoro_tiger 1/33(500 범위 2500000 + STR×50000 · stomp 150000·스턴 2.5초) | 평타 확률(OnHitChance) | Enemies | 50000.0 | 0.0 | 1 | 0.030303 |  | 500.0 |  |
| 초월_배성령_AD | 정열의 요리사 — 1/6 | 평타 확률(OnHitChance) | SingleTarget | 0.1 | 0 | 1 | 0.166667 |  | 475.0 |  |
| 초월_배성령_AD | 정열의 요리사 — 1/6 | 평타 확률(OnHitChance) | SingleTarget | 450000.0 | 0 | 1 | 0.166667 |  | 475.0 |  |
| 초월_배성령_AD | 정열의 요리사 — 1/6 | 평타 확률(OnHitChance) | Enemies | 582500.0 | 0.0 | 1 | 0.166667 |  | 475.0 |  |
| 초월_배성령_AD | 정열의 요리사 — MANA게이지115.00 | N타째(OnHitCount) | Enemies | 3330000.0 | 0 | 1 | 1.0 |  | 600.0 |  |
| 초월_배성령_AD | 정열의 요리사 — MANA게이지115.00 | N타째(OnHitCount) | SingleTarget | 0.1 | 0 | 1 | 1.0 |  | 600.0 |  |
| 초월_배성령_AD | 정열의 요리사 — MANA게이지115.00 | N타째(OnHitCount) | SingleTarget | 2075000.0 | 0 | 1 | 1.0 |  | 600.0 |  |
| 초월_배성령_AD | 정열의 요리사 — (1/6 실패 뒤) 1/18 | 평타 확률(OnHitChance) | Enemies | 850000.0 | 0.0 | 1 | 0.055556 |  | 500.0 |  |
| 초월_배성령_AD | 정열의 요리사 — (1/6 실패 뒤) 1/18 | 평타 확률(OnHitChance) | SingleTarget | 0.1 | 0.0 | 1 | 0.055556 |  | 500.0 |  |
| 초월_배성령_AD | 정열의 요리사 — (1/6 실패 뒤) 1/18 | 평타 확률(OnHitChance) | SingleTarget | 450000.0 | 0.0 | 1 | 0.055556 |  | 500.0 |  |
| 초월_신문철_AP | '루피 — Ruffy_AttackDamage 제트 피스톨(17.5%: 425 범위 + 대상)' | 평타 확률(OnHitChance) | Enemies | 2.5 | 0.0 | 1 | 0.175 |  | 425.0 |  |
| 초월_신문철_AP | '루피 — Ruffy_AttackDamage 제트 피스톨(17.5%: 425 범위 + 대상)' | 평타 확률(OnHitChance) | SingleTarget | 2.5 | 0 | 1 | 0.175 |  | 425.0 |  |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | Enemies | 0.03 | 0 | 1 | 1.0 |  | 625.0 |  |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | SingleTarget | 0.045 | 0 | 1 | 1.0 |  | 625.0 |  |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | SingleTarget | 5000.0 | 0.0 | 1 | 1.0 |  | 625.0 |  |
| 초월_신문철_AP | 명왕 레일리의 제자 — MANA게이지160.00 | N타째(OnHitCount) | Enemies | 100000.0 | 0.0 | 1 | 1.0 |  | 625.0 |  |
| 초월_신문철_AP | A09Q 0장풍 기간트개틀링1 | 평타 확률(OnHitChance) | Enemies | 47500.0 | 0.0 | 1 | 0.0125 |  | 405.0 | ANcs alev=1 aher=0 amcs=0 aran=1500.0 ahdu=1.0 adur=1.0 Ncs2=0.14000000059604645 Ncs3=26 Ncs1=47500.0 |
| 초월_신문철_AP | A09T 3범위 루피초월 둔화 | 평타 확률(OnHitChance) | Enemies | 50000.0 | 0.0 | 1 | 0.175 |  | 425.0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 ahdu=1.0 adur=2.0 abuf= atar=air,enemies,neutral,ground aare=425.0 |
| 초월_신문철_AP | '루피 — Ruffy_AttackDamage 기본(82.5%: 345 범위 평타 피해×0.8 + 25000)' | 평타 확률(OnHitChance) | Enemies | 0.8 | 0.0 | 1 | 0.825 |  | 345.0 |  |
| 초월_신문철_AP | A0KE !숙련된 패기 — 6.25% +625000(PV 200 아닌 적) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.0 | 1 | 0.0625 |  | 0.0 | ACbh arac=orc atar=air,nonancient,enemies,neutral,ground Hbh3=625000.0 Hbh1=6.25 ahdu=1.0 amat=luffy_wano01_s2.mdx amsp=4500 |
| 초월_신문철_AP | A0KA !레드호크 — 10.62% +1062500(PV 200 대상만) | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.0 | 1 | 0.1062 |  | 0.0 | ACbh arac=orc atar=air,ancient,enemies,neutral,ground Hbh3=1062500.0 Hbh1=10.619999885559082 ahdu=1.0 abuf= abpx=0 amat=luffy_wano01_s2.mdx |
| 초월_신문철_AP | A051 !고무고무 열매 — 18% ×5.0 | 평타 확률(OnHitChance) | SingleTarget | 5.0 | 0.0 | 1 | 0.18 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 ahdu=1.0 abpx=3 Hbh2=5.0 Hbh1=18.0 |
| 초월_양재모_AD | 로우 — Law_skill_5 라디오 나이프(1/20) | 평타 확률(OnHitChance) | Enemies | 150000.0 | 0.1 | 1 | 0.05 |  | 575.0 |  |
| 초월_양재모_AD | 로우 — Law_skill_5 라디오 나이프(1/20) | 평타 확률(OnHitChance) | Enemies | 0.0125 | 0 | 1 | 0.05 |  | 575.0 |  |
| 초월_양재모_AD | 로키포트 사건의 주모자 — 1/8 | 평타 확률(OnHitChance) | Enemies | 350000.0 | 0.0 | 1 | 0.125 |  | 485.0 |  |
| 초월_양재모_AD | 로키포트 사건의 주모자 — 1/8 | 평타 확률(OnHitChance) | SingleTarget | 0.2 | 0.0 | 1 | 0.125 |  | 485.0 |  |
| 초월_유재헌_ADAP | A0EQ !해류조정-시라호시 | 평타 확률(OnHitChance) | Enemies | 380000.0 | 0 | 1 | 0.11111 |  | 600.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_유재헌_ADAP | A0EQ !해류조정-시라호시 | 평타 확률(OnHitChance) | Enemies | 450000.0 | 0 | 1 | 0.11111 |  | 600.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= alev=2 |
| 초월_유재헌_ADAP | 시라호시 마나 — MANA게이지120 | N타째(OnHitCount) | Enemies | 3350000.0 | 0.0 | 1 | 1.0 |  | 800.0 |  |
| 초월_유재헌_ADAP | 시라호시 마나 — MANA게이지120 | N타째(OnHitCount) | Enemies | 0.06 | 0.0 | 1 | 1.0 |  | 800.0 |  |
| 초월_유재헌_ADAP | A0HN !인어공주 — 17% ×3.0 + 250000 | 평타 확률(OnHitChance) | SingleTarget | 3.0 | 0.0 | 1 | 0.17 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=250000.0 Hbh1=17.0 adur=0.0 ahdu=0.0 abpx=3 Hbh2=3.0 |
| 초월_이태훈_AP | A0GR !중력장 | 평타 확률(OnHitChance) | Enemies | 5.0 | 0.0 | 1 | 0.041666666666666664 |  | 485.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | A0GR !중력장 | 평타 확률(OnHitChance) | Enemies | 280000.0 | 0.0 | 1 | 0.041666666666666664 |  | 485.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | A0GR !중력장 | 평타 확률(OnHitChance) | Enemies | 5.0 | 0.0 | 1 | 0.041666666666666664 |  | 485.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | A0GR !중력장 | 평타 확률(OnHitChance) | Enemies | 280000.0 | 0.0 | 1 | 0.041666666666666664 |  | 485.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | A0GR 중력장 Lv2 — 운석(1/24×1/4) | 평타 확률(OnHitChance) | Enemies | 315000.0 | 1.4 | 1 | 0.010416666666666666 |  | 535.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | A0GR 중력장 Lv2 — 운석(1/24×1/4) | 평타 확률(OnHitChance) | Enemies | 1500000.0 | 0.0 | 1 | 0.010416666666666666 |  | 535.0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=0.0 adur=0.0 ahdu=0.0 abuf= abpx=1 alev=2 |
| 초월_이태훈_AP | 후지토라 — MANA게이지140 (Huji_03) | N타째(OnHitCount) | Enemies | 6250000.0 | 0.0 | 1 | 1.0 |  | 700.0 |  |
| 초월_이태훈_AP | 후지토라 — 1/7 (Huji01 중력 베기) | 평타 확률(OnHitChance) | Enemies | 2.5 | 0.0 | 1 | 0.14285714285714285 |  | 485.0 |  |
| 초월_이태훈_AP | 후지토라 — 1/7 (Huji01 중력 베기) | 평타 확률(OnHitChance) | SingleTarget | 350000.0 | 0.0 | 1 | 0.14285714285714285 |  | 485.0 |  |
| 초월_이태훈_AP | 후지토라 — 6/7 (Huji01 여파) | 평타 확률(OnHitChance) | Enemies | 0.8 | 0.0 | 1 | 0.8571428571428571 |  | 295.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=5.25 | 평타 확률(OnHitChance) | Enemies | 1000000.0 | 0 | 1 | 0.0525 |  | 400.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=5.25 | 평타 확률(OnHitChance) | SingleTarget | 2000000.0 | 0 | 1 | 0.0525 |  | 400.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=5.25 | 평타 확률(OnHitChance) | SingleTarget | 0.4 | 0 | 1 | 0.0525 |  | 400.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=7.25 AND 특성(A0R2) | 평타 확률(OnHitChance) | Enemies | 600000.0 | 0.0 | 1 | 0.0725 |  | 495.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=7.25 AND 특성(A0R2) | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.0725 |  | 495.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=7.25 AND 특성(A0R2) | 평타 확률(OnHitChance) | SingleTarget | 0.04 | 0.0 | 1 | 0.0725 |  | 495.0 |  |
| 초월_임장혁_AD | CP.Zero — Real<=7.25 AND 특성(A0R2) | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.0725 |  | 495.0 |  |
| 초월_임장혁_AD | CP.Zero — 1/6 | 평타 확률(OnHitChance) | SingleTarget | 0.25 | 0.0 | 1 | 0.16666666666666666 |  | 0.0 |  |
| 초월_임장혁_AD | CP.Zero — 1/6 | 평타 확률(OnHitChance) | SingleTarget | 500000.0 | 0.0 | 1 | 0.16666666666666666 |  | 0.0 |  |
| 초월_임장혁_AD | CP.Zero — 1/6 | 평타 확률(OnHitChance) | SingleTarget | 500000.0 | 0.0 | 1 | 0.16666666666666666 |  | 0.0 |  |
| 초월_임장혁_AD | A0R5 !육식 — 25% +500000 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0.0 | 1 | 0.25 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=500000.0 Hbh1=25.0 ahdu=1.0 |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85.00 AND 1/25 | 평타 확률(OnHitChance) | Enemies | 2000.0 | 0.18 | 1 | 0.04 |  | 415.0 |  |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85.00 AND 1/5 | 평타 확률(OnHitChance) | SingleTarget | 4000000.0 | 0 | 1 | 0.2 |  | 0.0 |  |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85.00 AND 1/5 | 평타 확률(OnHitChance) | SingleTarget | 1000000.0 | 0 | 1 | 0.2 |  | 0.0 |  |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85.00 AND 1/5 | 평타 확률(OnHitChance) | SingleTarget | 2000000.0 | 0 | 1 | 0.2 |  | 0.0 |  |
| 초월_임채민_AP | A13V 3범위 검수초 지진 | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.083333 |  | 650.0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 adur=3.0 abuf= atar=air,enemies,neutral,ground aare=650.0 Htc1=400000.0 |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85 | N타째(OnHitCount) | Enemies | 50000.0 | 0.0 | 1 | 1.0 |  | 925.0 |  |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85 | N타째(OnHitCount) | Enemies | 0.03 | 0.0 | 1 | 1.0 |  | 925.0 |  |
| 초월_임채민_AP | 티치 — 강진 1/10 × 1/6(시전자 중심 700 범위 400000·이감 75% + 250000 + STR×3000) | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.016667 |  | 700.0 |  |
| 초월_임채민_AP | 티치 — 강진 1/10 × 1/6(시전자 중심 700 범위 400000·이감 75% + 250000 + STR×3000) | 평타 확률(OnHitChance) | Enemies | 3000.0 | 0.0 | 1 | 0.016667 |  | 700.0 |  |
| 초월_조성진_AD | G.O.D — MANA게이지140.00 | N타째(OnHitCount) | Enemies | 0.02 | 0 | 1 | 1.0 |  | 600.0 |  |
| 초월_조성진_AD | G.O.D — MANA게이지140.00 | N타째(OnHitCount) | Enemies | 2500000.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_조성진_AD | G.O.D — 1/5 | 평타 확률(OnHitChance) | Enemies | 262500.0 | 0 | 1 | 0.2 |  | 600.0 |  |
| 초월_조성진_AD | G.O.D — 1/5 | 평타 확률(OnHitChance) | SingleTarget | 350000.0 | 0 | 1 | 0.2 |  | 600.0 |  |
| 초월_조성진_AD | G.O.D — 1/5 | 평타 확률(OnHitChance) | SingleTarget | 0.0425 | 0.0 | 1 | 0.2 |  | 600.0 |  |
| 초월_조성진_AD | A0HK 저격왕-우솝초 — 15% ×3.5 · 스턴 5초 | 평타 확률(OnHitChance) | SingleTarget | 3.5 | 0.0 | 1 | 0.15 |  | 0.0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 adur=5.0 ahdu=5.0 abpx=3 Hbh2=3.5 areq=h04X |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상 (450000 + 1500×PV)×1~1.5) | 평타 확률(OnHitChance) | Enemies | 600000.0 | 0.0 | 1 | 0.111111 |  | 600.0 |  |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상 (450000 + 1500×PV)×1~1.5) | 평타 확률(OnHitChance) | SingleTarget | 600000.0 | 0.0 | 1 | 0.111111 |  | 600.0 |  |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상 (450000 + 1500×PV)×1~1.5) | 평타 확률(OnHitChance) | SingleTarget | 750000.0 | 0.0 | 1 | 0.111111 |  | 600.0 |  |
| 초월_조성진_AD | 우솝 — 1/9(600 범위 600000×1~1.5 + 대상 (450000 + 1500×PV)×1~1.5) | 평타 확률(OnHitChance) | SingleTarget | 900000.0 | 0.0 | 1 | 0.111111 |  | 600.0 |  |
| 초월_최상호_AD | 어인협객 — 1/16 | 평타 확률(OnHitChance) | Enemies | 17500.0 | 0 | 1 | 0.0625 |  | 625.0 |  |
| 초월_최상호_AD | 어인협객 — 1/16 | 평타 확률(OnHitChance) | SingleTarget | 48450.0 | 0 | 1 | 0.0625 |  | 625.0 |  |
| 초월_최상호_AD | 어인협객 — 1/16 | 평타 확률(OnHitChance) | Enemies | 500000.0 | 0.0 | 1 | 0.0625 |  | 625.0 |  |
| 초월_최상호_AD | 어인협객 — MANA게이지115.00 | N타째(OnHitCount) | Enemies | 43500.0 | 0 | 1 | 1.0 |  | 600.0 |  |
| 초월_최상호_AD | A09S !2범위 호크 개틀링 더미 | 평타 확률(OnHitChance) | Enemies | 102500.0 | 0 | 1 | 0.125 |  | 500.0 | AOws alev=1 aher=0 atar=air,invulnerable,enemies,ground,vulnerable ahdu=0.2199999988079071 acdn=0.0 aare=500.0 Wrs1=102500.0 acat= amcs=0 |
| 초월_최상호_AD | 징베 — Jimbe_jingak 1/7(450 범위 1000000 + STR×17500, 그 뒤 AId1 +1) | 평타 확률(OnHitChance) | Enemies | 17500.0 | 0.0 | 1 | 0.14285714285714285 |  | 450.0 |  |
| 초월_최상호_AD | 징베 — 평타 추가타 1/4(대상 1000000) | 평타 확률(OnHitChance) | SingleTarget | 1000000.0 | 0.0 | 1 | 0.25 |  | 0.0 |  |
| 초월_최상호_AP | 도플라밍고 [일반] — 매 타 275 범위 평타 피해×0.5 + 50000 | 평타 확률(OnHitChance) | Enemies | 0.5 | 0 | 1 | 1.0 |  | 275.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — DP_Skill_3 1/6 | 평타 확률(OnHitChance) | Enemies | 10000.0 | 0 | 1 | 0.166667 |  | 525.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — 매 타 350 범위 평타 피해×0.85 + 85000 | 평타 확률(OnHitChance) | Enemies | 0.85 | 0 | 1 | 1.0 |  | 350.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — DP_Skill_2 1/9 | 평타 확률(OnHitChance) | SingleTarget | 200000.0 | 0 | 1 | 0.111111 |  | 0.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — DP_Skill_2 1/9 | 평타 확률(OnHitChance) | SingleTarget | 0.25 | 0 | 1 | 0.111111 |  | 0.0 |  |
| 초월_최상호_AP | A0PI 3범위 도플초월 오색실1 | 평타 확률(OnHitChance) | Enemies | 205000.0 | 0 | 1 | 0.14285714285714285 |  | 525.0 | AHtc aher=0 alev=1 amcs=0 acdn=0.0 abuf= atar=air,enemies,neutral,ground aare=525.0 Htc1=205000.0 Htc4=0.0 |
| 초월_최상호_AP | 도플라밍고 [일반] — DP_Skill_3 1/8(525 범위 750000 + STR×10000) | 평타 확률(OnHitChance) | Enemies | 10000.0 | 0.0 | 1 | 0.125 |  | 525.0 |  |
| 초월_최상호_AP | 도플라밍고 [일반] — DP_Skill_2 1/10(대상 200000 + PV<200 현재체력 25%) | 평타 확률(OnHitChance) | SingleTarget | 200000.0 | 0.0 | 1 | 0.1 |  | 0.0 |  |
| 초월_최상호_AP | 도플라밍고 [일반] — DP_Skill_2 1/10(대상 200000 + PV<200 현재체력 25%) | 평타 확률(OnHitChance) | SingleTarget | 0.25 | 0.0 | 1 | 0.1 |  | 0.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — 새장 1/5(525 범위 150000·이감 4.5초 + 현재체력 1% + 10000 + 400000×1~1.5) | 평타 확률(OnHitChance) | Enemies | 150000.0 | 0.0 | 1 | 0.2 |  | 525.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — 새장 1/5(525 범위 150000·이감 4.5초 + 현재체력 1% + 10000 + 400000×1~1.5) | 평타 확률(OnHitChance) | Enemies | 0.01 | 0.0 | 1 | 0.2 |  | 525.0 |  |
| 초월_최상호_AP | 도플라밍고 [각성] — 새장 1/5(525 범위 150000·이감 4.5초 + 현재체력 1% + 10000 + 400000×1~1.5) | 평타 확률(OnHitChance) | Enemies | 400000.0 | 0.0 | 1 | 0.2 |  | 525.0 |  |
| 초월_황준석_ADAP | A0G4 !화염성 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 1.0 |  | 0 | ACbh arac=orc atar=air,ancient,enemies,ground Hbh3=0.0 adur=1.0 ahdu=1.0 abuf= areq=h04X Hbh1=100.0 |
| 초월_황준석_ADAP | A10Z 0장풍 키드초월 리펠1 | 평타 확률(OnHitChance) | Enemies | 300000.0 | 0 | 1 | 0.1 |  | 450.0 | ACca aran=1700.0 amcs=0 amsp=2950 aare=225.0 Ucs3=1100.0 Ucs1=300000.0 Ucs2=700000000.0 Ucs4=475.0 acdn=0.0 |
| 초월_황준석_ADAP | A10Z 0장풍 키드초월 리펠1 | 평타 확률(OnHitChance) | Enemies | 300000.0 | 0.0 | 1 | 0.1 |  | 450.0 | ACca aran=1700.0 amcs=0 amsp=2950 aare=225.0 Ucs3=1100.0 Ucs1=300000.0 Ucs2=700000000.0 Ucs4=475.0 acdn=0.0 |
| 초월_황준석_ADAP | 키드 — Kid_Skill_Life2 펑크 1/16(500 범위 500000 + STR×5000 · 대상 (125000 + STR×5000)×2 · 스턴 3초) | 평타 확률(OnHitChance) | Enemies | 5000.0 | 0.0 | 1 | 0.0625 |  | 500.0 |  |
| 초월_황준석_ADAP | 키드 — Kid_Skill_Life2 펑크 1/16(500 범위 500000 + STR×5000 · 대상 (125000 + STR×5000)×2 · 스턴 3초) | 평타 확률(OnHitChance) | SingleTarget | 10000.0 | 0.0 | 1 | 0.0625 |  | 500.0 |  |
| 초월_황준석_ADAP | 키드 — Kid_Skill_Life2 펑크 1/16(500 범위 500000 + STR×5000 · 대상 (125000 + STR×5000)×2 · 스턴 3초) | 평타 확률(OnHitChance) | SingleTarget | 99999.0 | 0.0 | 1 | 0.0625 |  | 500.0 |  |
| 초월_황준석_ADAP | A0IT !강철 풍선 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.2 |  | 0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 Hbh1=20.0 adur=0.75 ahdu=0.3799999952316284 abuf= abpx=3 amat=bigmamslash.mdx |
| 초월_황준석_ADAP | A04D !소울 솔리드 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.175 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 ahdu=0.8799999952316284 abpx=3 adur=1.75 Hbh1=17.5 |
| 초월_황준석_ADAP | A0AK !흉탄 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.2 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 Hbh1=20.0 adur=0.0 ahdu=0.0 abpx=1 |
| 초월_황준석_ADAP | A0X0 !어인공수도-징베 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.4 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 Hbh1=40.0 abpx=3 adur=1.5 ahdu=0.75 |
| 초월_황준석_ADAP | A0ZV !브레스-카이도 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.3 |  | 0 | ACbh arac=orc atar=none Hbh3=1.0 adur=0.0 ahdu=0.0 Hbh1=30.0 abpx=1 Hbh2=1.0 |
| 초월_황준석_ADAP | A0AG !10톤 해머 | 평타 확률(OnHitChance) | SingleTarget | 7.0 | 0 | 1 | 0.2 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 Hbh1=20.0 abpx=3 Hbh2=7.0 adur=1.0 ahdu=1.0 |
| 초월_황준석_ADAP | A0HK !저격왕-우솝초 | 평타 확률(OnHitChance) | SingleTarget | 3.5 | 0 | 1 | 0.15 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 adur=5.0 ahdu=5.0 abpx=3 Hbh2=3.5 areq=h04X |
| 초월_황준석_ADAP | A0HA !용의 숨결 | 평타 확률(OnHitChance) | SingleTarget | 4.0 | 0 | 1 | 0.15 |  | 0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 adur=0.0 ahdu=0.0 abuf= abpx=0 Hbh2=4.0 |
| 초월_황준석_ADAP | A0CS !빛빛열매 | 평타 확률(OnHitChance) | SingleTarget | 8.88 | 0 | 1 | 0.15 |  | 0 | ACbh arac=orc atar=air,enemies,ground Hbh3=0.0 abuf= abpx=3 Hbh2=8.880000114440918 ahdu=1.5 adur=3.0 |
| 초월_황준석_ADAP | A0HN !인어공주 | 평타 확률(OnHitChance) | SingleTarget | 3.0 | 0 | 1 | 0.17 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=250000.0 Hbh1=17.0 adur=0.0 ahdu=0.0 abpx=3 Hbh2=3.0 |
| 초월_황준석_ADAP | A03W !패기의 검술 | 평타 확률(OnHitChance) | SingleTarget | 10.0 | 0 | 1 | 0.12 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=500000.0 Hbh1=12.0 abpx=3 Hbh2=10.0 ahdu=0.75 adur=1.5 |
| 초월_황준석_ADAP | A101 !자석 팔 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.35 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=360000.0 Hbh1=35.0 abpx=3 ahdu=0.75 adur=1.5 |
| 초월_황준석_ADAP | A103 !펑크 바이스 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.2 |  | 0 | ACbh arac=orc atar=none Hbh3=0.0 Hbh1=20.0 adur=0.0 ahdu=0.0 abpx=0 |
| 초월_황준석_ADAP | A0KA !레드호크 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.1062 |  | 0 | ACbh arac=orc atar=air,ancient,enemies,neutral,ground Hbh3=1062500.0 Hbh1=10.619999885559082 ahdu=1.0 abuf= abpx=0 amat=luffy_wano01_s2.mdx |
| 초월_황준석_ADAP | A0KE !숙련된 패기 | 평타 확률(OnHitChance) | SingleTarget | 0.0 | 0 | 1 | 0.0625 |  | 0 | ACbh arac=orc atar=air,nonancient,enemies,neutral,ground Hbh3=625000.0 Hbh1=6.25 ahdu=1.0 amat=luffy_wano01_s2.mdx amsp=4500 |
| 초월_황준석_ADAP | A051 !고무고무 열매 | 평타 확률(OnHitChance) | SingleTarget | 5.0 | 0 | 1 | 0.18 |  | 0 | ACbh arac=orc atar=air,enemies,neutral,ground Hbh3=0.0 ahdu=1.0 abpx=3 Hbh2=5.0 Hbh1=18.0 |
| 초월_황준석_ADAP | 키드 체력 스킬 — LIFE게이지50(500 범위 2000000·스턴 2초 + 대상 1000000 + STR×50000) | N타째(OnHitCount) | Enemies | 2000000.0 | 0.0 | 1 | 1.0 |  | 500.0 |  |
| 초월_황준석_ADAP | 키드 체력 스킬 — LIFE게이지50(500 범위 2000000·스턴 2초 + 대상 1000000 + STR×50000) | N타째(OnHitCount) | SingleTarget | 50000.0 | 0.0 | 1 | 1.0 |  | 500.0 |  |

요약: multiplier 범위 0 ~ 6.25e+06, 서로 다른 값 94종.

## ApplyBuff (2건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_양재모_AD | 로우 — B03Z 창(버프 부여) | N타째(OnHitCount) | Self | 0 | 5.0 | 1 | 1.0 |  | 825.0 |  |
| 초월_최상호_AP | 도플라밍고 — MANA게이지150 각성 창(B00X 4.5초) | N타째(OnHitCount) | Self | 0.0 | 4.5 | 1 | 1.0 |  | 0.0 |  |

요약: multiplier 범위 0 ~ 0, 서로 다른 값 1종.

## ArmorBonus (12건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_강재규_AP | A0I8 마노스 기간테스 — 적 방어 -45 오라 | 오라(Aura) | Enemies | -45.0 | 0.0 | 1 | 1.0 |  | 851.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=851.0 Had1=-45.0 abpx=3 abuf=B028 atat= |
| 초월_강주혁_AP | A14E 삼도류 검술-조초 — 적 방어 -50 오라 | 오라(Aura) | Enemies | -50.0 | 0.0 | 1 | 1.0 |  | 850.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=850.0 Had1=-50.0 abpx=3 adur=0.0 ahdu=0.0 |
| 초월_구주호_AD | A12A 수배지령 — 적 방어 -25 오라 | 오라(Aura) | Enemies | -25.0 | 0.0 | 1 | 1.0 |  | 900.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground Had1=-25.0 abuf=B06L atat= abpx=1 abpy=1 |
| 초월_구주호_AD | A15G 숲숲 열매 — 적 방어 -35 오라 | 오라(Aura) | Enemies | -35.0 | 0.0 | 1 | 1.0 |  | 900.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground Had1=-35.0 abpx=3 abuf=B075 atat= |
| 초월_두유찬_AD | A0H2 용조권 — 적 방어 −30 오라 | 오라(Aura) | Enemies | -30.0 | 0.0 | 1 | 1.0 |  | 850.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=850.0 Had1=-30.0 abpx=3 abuf=B01M atat= |
| 초월_박민수_AD | A0GP 삼도류 검술-조초1 — 적 방어 -35 오라 | 오라(Aura) | Enemies | -35.0 | 0.0 | 1 | 1.0 |  | 825.0 | AHad alev=2 aher=0 arac=orc atar=air,enemies,ground aare=825.0 Had1=-35.0 abpx=3 adur=0.0 ahdu=0.0 |
| 초월_조성진_AD | A0TH 우솝갓 — 적 방어 -22 오라 | 오라(Aura) | Enemies | -22.0 | 0.0 | 1 | 1.0 |  | 1085.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=1085.0 Had1=-22.0 abpx=1 atat= abpy=1 |
| 초월_조성진_AD | A0PH 우솝의 허풍1 — 적 방어 -8 오라 | 오라(Aura) | Enemies | -8.0 | 0.0 | 1 | 1.0 |  | 99999.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=99999.0 Had1=-8.0 abpx=1 atat= abpy=1 |
| 초월_황준석_ADAP | A0H2 용조권 — 적 방어 −30 오라 | 오라(Aura) | Enemies | -30.0 | 0.0 | 1 | 1.0 |  | 850.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=850.0 Had1=-30.0 abpx=3 abuf=B01M atat= |
| 초월_황준석_ADAP | A0AJ 열매각성 방깍 — 적 방어 −60 오라 | 오라(Aura) | Enemies | -60.0 | 0.0 | 1 | 1.0 |  | 850.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=850.0 Had1=-60.0 abpx=3 abuf=B01E atat= |
| 초월_황준석_ADAP | A0TH 우솝갓 — 적 방어 −22 오라 | 오라(Aura) | Enemies | -22.0 | 0.0 | 1 | 1.0 |  | 1085.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=1085.0 Had1=-22.0 abpx=1 atat= abpy=1 |
| 초월_황준석_ADAP | A0PH 우솝의 허풍 — 맵 전체 적 방어 −8 오라 | 오라(Aura) | Enemies | -8.0 | 0.0 | 1 | 1.0 |  | 99999.0 | AHad alev=1 aher=0 arac=orc atar=air,enemies,ground aare=99999.0 Had1=-8.0 abpx=1 atat= abpy=1 |

요약: multiplier 범위 -60 ~ -8, 서로 다른 값 8종.

## AisrStack (2건)

| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| 초월_노태현_AP | 브룩 — MANA게이지115(600 범위 3000000·스턴 + AIsr +10) | N타째(OnHitCount) | Enemies | 10.0 | 0.0 | 1 | 1.0 |  | 600.0 |  |
| 초월_임채민_AP | 지진과 어둠의 검은수염 — LIFE게이지85.00 AND 1/5 | 평타 확률(OnHitChance) | SingleTarget | 10.0 | 0.0 | 1 | 0.2 |  | 0.0 |  |

요약: multiplier 범위 10 ~ 10, 서로 다른 값 1종.

## 요약: 이감(Slow)

- 총 22건 = 상시 오라 13건(AOae 음수·양수) + 발동형 9건. **환산식: 우리 multiplier(남는 속도 비율) = 1 + Oae1**(예: Oae1 −0.30 → 0.70). 맵이 MinUnitSpeed 70으로 하한을 둔다.
- 오라 multiplier 값: 0.3, 0.45, 0.67, 0.7, 0.8, 0.85, 0.88, 0.95, 1.15 · 범위(w3a aare): 500.0, 800.0, 815.0, 875.0, 888.0, 900.0, 925.0, 1250.0, 99999.0
- 발동형(평타 확률 등): 구주호_AD A160 3범위 료쿠규 초월 둔화 확률 0.143 · 남는속도 0.75 · 3.0초 / 두유찬_AD 사보 — Sabo_Skill_3  확률 0.1 · 남는속도 0.7 · 2.5초 / 신문철_AP A09T 3범위 루피초월 둔화 확률 0.175 · 남는속도 0.67 · 2.0초 / 양재모_AD 로우 — B03Z 창(버프 부여) 확률 1 · 남는속도 0.55 · 5.0초 / 양재모_AD 로키포트 사건의 주모자 — 1/8 확률 0.125 · 남는속도 0.6 · 3.0초 / 임채민_AP A13V 3범위 검수초 지진 확률 0.0833 · 남는속도 0.3 · 3.0초 / 임채민_AP 티치 — 강진 1/10 × 1/6 확률 0.0167 · 남는속도 0.25 · 3.0초 / 최상호_AP A0PI 3범위 도플초월 오색실1 확률 0.143 · 남는속도 0.0 · 4.5초 / 최상호_AP 도플라밍고 [각성] — 새장 1/ 확률 0.2 · 남는속도 0.0 · 4.5초

## 요약: 보스 상대 피해(보잡) 원작 근거

- 원작에는 「보스에게 피해 +N%」 **전용 능력(패시브)이 없다**. 보스(PV=200) 분기는 **체력 비례 스킬 173호출·121트리거**에서만 나온다(BrookAttack 보스 75,000 고정 · Hidden1 보스 300,000+5,000,000 고정 등) — 일반몹은 %체력, 보스는 고정값/더 작은 %로 가는 분기이고 「보스가 더 아프다」가 아니다(근거: Docs/research/SKILL_BOSS_BRANCH.md 표를 j로 다시 확인).
- 따라서 최상호 구일의 「보잡」은 **원작 근거 없음 — 제안값**: 보스(PV≥200) 상대 최종 피해 ×1.3.

## 요약: 오라 강화·소환·아군 부여(원작 근거)

- 「전설 이상 유닛 수 비례 오라 강화」: 원작 근거 없음(원작 오라는 고정 %: 공증 ACac 0.25(강재규 A0V8) · 불멸 김용태 0.6 · 정준영 −0.75(디버프)). 제안값: 기본 +20% + 전설 이상 1기당 +4%(상한 +40%).
- 「전설 이상 아군에게 아머브레이크(발동) 부여」: 원작 근거 없음. 방깎 발동 값은 위 ArmorBreak 표(확률 4~14%, 방깎 2~9) 기준.
- 소환수: 우리 코드엔 소환 효과가 없고 모리아 좀비 부활(raiseOnKill)과 TimedLife(지속)만 있다. 원작 초월 중 소환형은 로우 e0RR(MANA 135) 등 — 사장님 확정치(20초·동시 1기·쿨 10초·부채꼴)를 쓴다.

