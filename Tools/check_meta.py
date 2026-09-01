#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""git에 추적되는 에셋 중 .meta가 빠진 것을 찾는다.

.meta에는 GUID가 들어 있고 씬은 컴포넌트를 그 GUID로 참조한다.
.meta 없이 커밋하면 다른 데서 클론했을 때 Unity가 새 GUID를 발급하고,
그 스크립트를 쓰는 씬 참조가 전부 Missing Script가 된다.

새 스크립트를 만든 날 세 번 놓쳤다. 푸시 전에 이걸 돌린다.
"""
import subprocess, sys

tracked = set(subprocess.run(
    ['git', '-c', 'core.quotepath=false', 'ls-files', 'Assets'],
    capture_output=True, text=True).stdout.splitlines())

assets = [p for p in tracked if not p.endswith('.meta')]
missing = [p for p in assets if p + '.meta' not in tracked]

print(f"추적 에셋 {len(assets)}개 / .meta 누락 {len(missing)}개")
for path in missing:
    print("  ❌", path)

sys.exit(1 if missing else 0)
