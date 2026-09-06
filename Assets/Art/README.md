# 아트 에셋 두는 곳

받아온 모델을 여기 넣는다. 링크와 라이선스는 `Docs/reference/CHARACTER_ASSETS.md`.

| 폴더 | 무엇 | git |
|---|---|---|
| `Monsters/` | 적 75라운드 + 보스 9종 | 올린다 |
| `Characters/` | 공용 애니메이션 클립(idle/walk/attack) + 이름 규칙 안 맞는 모델 | 올린다 |
| **`Units/<유닛이름>/`** | **유닛 스킨 — 폴더·fbx 이름이 로스터 에셋 이름과 같아야 한다** | 올린다 |
| `Props/` | 건물·소품 | 올린다 |
| **`Restricted/`** | **재배포 금지 조건이 붙은 것** | **안 올린다** |

## Restricted가 따로 있는 이유

Booth의 VRChat 아바타처럼 **"재배포·재판매·이식 금지"**가 붙은 에셋이 있다.
개인 플레이는 문제없지만 **git에 올리는 것은 재배포에 해당한다.**
그런 파일은 `Restricted/` 안에 두면 `.gitignore`가 걸러준다.

CC0(Quaternius 등)와 CC-BY(Sketchfab 다수)는 재배포 제한이 없으므로 위 세 폴더에 넣으면 된다.
**CC-BY는 저작자 이름을 크레딧 화면에 넣어야 한다** — 받을 때 정확한 이름을 같이 적어둘 것.

## 유닛 스킨 넣는 법

```
~/Downloads/<유닛이름>/ 에 받은 것(rar/zip/7z/fbx)을 두고
Tools/import_skin.sh ~/Downloads/<유닛이름>
```
→ `Units/<유닛이름>/<유닛이름>.fbx` + `Textures/` + `SOURCE.txt`(출처·리깅·애니 개수)로 정리된다.
로스터에 없는 이름이면 거부한다. 임포트 시 `UnitModelPostprocessor`가 Humanoid + Strip Bones 해제를
자동으로 맞춘다. 뼈가 없는 정적 메시(SOURCE.txt에 본 0)는 Mixamo 오토리깅을 거쳐 같은 자리에 덮어쓴다.

## 넣고 나서

`Tools > 아트 > 모델 배선`을 돌리면 프리팹을 만들고 데이터에 연결한다.
