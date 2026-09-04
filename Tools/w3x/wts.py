"""war3map.wts (문자열 테이블) 파서. TRIGSTR_숫자를 사람이 읽는 텍스트로 푼다.

형식은 텍스트 기반이다:
    STRING 123
    {
    실제 텍스트(여러 줄 가능)
    }
"""
import re

_BLOCK = re.compile(r'STRING\s+(\d+)\s*\{\r?\n(.*?)\r?\n\}', re.DOTALL)


def parse(path):
    text = open(path, encoding='utf-8', errors='replace').read()
    out = {}
    for m in _BLOCK.finditer(text):
        out[f'TRIGSTR_{m.group(1)}'] = m.group(2)
    return out


def resolve(value, table):
    """TRIGSTR_숫자면 풀어서 반환, 아니면 그대로."""
    if isinstance(value, str) and value.startswith('TRIGSTR_'):
        return table.get(value, value)
    return value
