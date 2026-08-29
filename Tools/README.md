# Tools

## generate_recipe_assets.py

조합표(`Docs/reference/RECIPES.md`)를 Unity 에셋으로 생성하는 스크립트.

```bash
cd Tools && python3 generate_recipe_assets.py
```

생성물:
- `Assets/Data/Units/Roster/` — UnitData 213개
  - `초월_`/`히든_`/`불멸_`/`영원_`/`제한_`/`랜덤_`/`다른세계_` 접두사 = **등급 확정** (조합 결과물)
  - `기본_` 접두사 = **등급 미확정**. 조합 재료로만 등장해 등급표를 못 받은 유닛.
    현재 전부 `Common`으로 채워둠 → 하위 등급표를 받으면 `recipes_data.py`에 반영 후 재생성할 것.
- `Assets/Data/Items/` — ItemData 2개 (어둠의추천서, 분실된지갑)
- `Assets/Data/Recipes/` — CombineRecipe 78개

### 다단 조합 처리
재료가 **더 낮은 티어의 조합 결과물**이면 그 결과 에셋을 참조한다.
예) 초월 박민석의 재료 `감탄떡볶이` → `히든_감탄떡볶이`
같은 티어 이상이면 `기본_` 유닛을 참조해 순환을 막는다.
예) 초월 최상호의 재료 `최상호` → `기본_최상호`

### ⚠️ 재생성 시 주의
스크립트는 매번 **새 GUID를 발급**한다. 그냥 다시 돌리면 씬·기존 참조가 전부 끊긴다.
재생성이 필요하면 기존 에셋을 지우고 참조를 다시 연결하거나, GUID를 보존하도록 스크립트를 고칠 것.
