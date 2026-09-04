# 원작 맵(.w3x) 읽기 도구

원랜디 원작 `ORD11.089.w3x`에서 데이터를 직접 꺼낸다. **워크3도 에디터도 필요 없다.**

## 왜 직접 만들었나

`.w3x`는 앞 512바이트가 워크3 헤더고 그 뒤가 MPQ 압축이다. 원랜디는 **보호된 맵**이라
`(listfile)`이 지워져 있고 내부 파일이 **암호화**되어 있어서, `mpyq` 같은 기성 라이브러리는
파일 목록도 못 읽고 암호화 블록도 못 푼다. `mpqread.py`가 그 둘을 직접 처리한다.

## 쓰는 법

```python
import sys; sys.path.insert(0, 'Tools/w3x')
from mpqread import Archive
import w3u, w3q, w3a, wts

# 1) 앞 512바이트를 잘라 MPQ만 남긴다
src = open('/경로/ORD11.089.w3x','rb').read()
open('/tmp/ord.mpq','wb').write(src[512:])

# 2) 원하는 파일을 꺼낸다
a = Archive('/tmp/ord.mpq')
open('/tmp/war3map.w3u','wb').write(a.read('war3map.w3u'))
open('/tmp/war3map.w3a','wb').write(a.read('war3map.w3a'))
open('/tmp/war3map.wts','wb').write(a.read('war3map.wts'))

# 3) 판다
units = w3u.parse('/tmp/war3map.w3u')
abilities = w3a.parse('/tmp/war3map.w3a')       # 능력 — w3q.py와 같은 포맷(레벨별 값)
strings = wts.parse('/tmp/war3map.wts')          # TRIGSTR_숫자 → 실제 텍스트
# 능력 이름은 anam 필드에 직접 텍스트로 들어있는 경우가 많다(TRIGSTR 아님) — wts.resolve()는
# TRIGSTR_로 시작할 때만 치환하고 아니면 그대로 돌려주므로 항상 걸어도 안전하다.
```

## 맵 안에 든 것

| 파일 | 내용 | 크기 |
|---|---|---|
| `war3map.w3u` | **유닛** — 체력·공격력·사거리·공속·이동속도·공격타입·방어타입·방어력 | 921 KB |
| `war3map.w3a` | **능력(스킬)** | 1,056 KB |
| `war3map.w3q` | **업그레이드** — 난이도 보정이 여기 있다 | 632 KB |
| `war3map.w3h` | 버프 | 64 KB |
| `war3map.w3t` | 아이템 | 20 KB |
| `war3map.wts` | 이름 문자열 — `TRIGSTR_숫자`를 이걸로 푼다 | 14 KB |
| `war3map.j` | **맵 스크립트 전체** — 난이도·라운드·이벤트 로직 | 5.3 MB |

## 읽을 때 걸리는 것

- **이름은 `TRIGSTR_숫자`로 들어 있다.** `war3map.wts`에서 풀어야 사람이 읽는 이름이 된다.
- **이름에 색 코드가 섞여 있다.** `|cff20b2aa나미|r - |cff00ff00흔함|r` — 등급이 이름 끝에
  색으로 붙어 있어서, 그걸로 등급을 읽을 수 있다.
- **필드가 없으면 "기본값"이다.** 베이스 유닛(`ewsp`·`hrif` 등)의 값을 따른다 — 없다고
  0이 아니다.
- **좌표계가 우리와 다르다.** 사거리 420, 이동속도 522 같은 값은 워크3 단위라
  **그대로 옮기면 안 된다.** 체력·공격력처럼 비율로 의미가 있는 값만 가져올 것.

## 이미 확인된 것

- 등급이 이름 색으로 구분된다 (흔함/안흔함/특별함/희귀함/특수함/전설적인/제한됨/초월함/랜덤전용/흔함영웅)
- 우리 게임의 히든·불멸·영원·다른세계·초월위습·변화됨은 **11.089에 없다**
- 공격 타입 × 방어 타입 배율표는 `war3mapMisc.txt`에 있고, **실제로 쓰이는 방어 타입 2종의
  배율이 전부 1.0이라 상성이 작동하지 않는다**
- 난이도는 적 체력을 직접 바꾸지 않고 **업그레이드(`R00A` 등)를 적 플레이어에 걸어서** 올린다
- 능력(`war3map.w3a`, 1,628개)은 **115종 기반 템플릿에서 파생**됐다. 방무뎀(방어무시 데미지)은
  고정 30%가 아니라 능력마다 다른 %(`nca1` 필드), 방깎(아머 브레이크)은 유닛당 1회가 아니라
  **원작 전역에서 흔하게 중첩되고 능력마다 다른 상한**(-75/-80/7중첩/9중첩 등)이 있다. 상세는
  `Docs/reference/ABILITIES_RESEARCH.md` 참고.
