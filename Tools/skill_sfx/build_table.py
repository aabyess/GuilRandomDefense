"""스킬 효과음 표 생성(2026-09-30 PM) — match.py 의 대응 결과로
  · Assets/Audio/Original/Resources/Sounds/Skill/ 에 쓰이는 mp3 만 복사(이름은 공백→_)
  · Assets/Audio/Original/Resources/Sounds/SkillSfxTable.txt (런타임 SkillSfx 가 읽는 표)
  · Docs/research/SKILL_SFX_MAPPING.csv (사람이 보는 근거표)
를 쓴다. 다시 돌려도 같은 결과(스킬 에셋이 늘면 새로 잡힌다).

두 층:
  S(스킬 단위) — 우리 스킬 에셋의 설명에 그 소리를 내는 원작 트리거 이름이 적혀 있는 것. 그 스킬이 나갈 때 난다.
  R(캐릭터 단위) — 트리거 앞머리(Zoro·Kaido…)가 가리키는 원작 캐릭터의 로스터. 어느 스킬인지는 우리 데이터로 못 가린다 →
     그 로스터가 S에 없는 스킬을 쓸 때 캐릭터 소리 묶음에서 하나를 낸다(간격 제한은 런타임). ⚠️ 「그 캐릭터의 소리」까지만 원작 근거.
소리 파일: ~/GRD_original_sounds(맵 임포트 153) + ~/GRD_original_sounds/_확장팩(확장팩 v2.41 zip 에서 꺼낸 것) + 이미 들여온 Summon 폴더.
"""
import collections, csv, os, re, shutil, sys

sys.path.insert(0, os.path.dirname(__file__))
import match

ROOT = match.ROOT
SRC = os.path.expanduser('~/GRD_original_sounds')
RES = os.path.join(ROOT, 'Assets/Audio/Original/Resources/Sounds')
SUMMON = os.path.join(RES, 'Summon')
OUT = os.path.join(RES, 'Skill')

# 트리거 앞머리 → 로스터. 「자동」은 스킬 에셋 설명에 같은 앞머리 트리거가 적힌 로스터(하나뿐이거나 뚜렷이 많은 쪽).
# 아래는 자동으로 안 갈리거나 에셋에 트리거 이름이 없는 것 — SummonVoice 표(소환 트리거 uid → 대응표)와 같은 캐릭터 대응.
MANUAL = {
    'kizaru': '초월_구주호_AD', 'zoro': '초월_박민수_AD', 'franky': '초월_박기찬_AD', 'jimbe': '초월_최상호_AD',
    'usop': '초월_조성진_AD', 'snake': '초월_김경현_AP', 'bigmam': '불멸_정준영', 'tahsigi': '초월_김민준_AP',
    'laillyskill': '불멸_정윤식', 'uta': '영원_김정래', 'z': '불멸_박은석', 'king': '제한_임준성', 'sinobu': '제한_이유범',
}
GENERIC = {'legend', 'unique', 'speical', 'transpom', 'quest', 'story', 'money', 'random', 'red', 'treasure', 'deathtimer',
           'unitjohabcounter', 'forever', 'kingnokuni'}


def token(t):
    m = re.match(r'Trig_([A-Za-z]+?)(?:\d|_|$)', t)
    return m.group(1).lower() if m else t


def clean(name):
    return os.path.splitext(os.path.basename(name))[0].replace(' ', '_')


def find_file(path):
    base = os.path.basename(path)
    p = os.path.join(SRC, base)
    if os.path.exists(p):
        return p, 'Skill'
    p = os.path.join(SRC, '_확장팩', base)   # 확장팩 zip 의 Sound\\ 경로 것 중 대응이 잡힌 것만 꺼내 둔 곳
    if os.path.exists(p):
        return p, 'Skill'
    for ext in ('.mp3', '.wav'):
        q = os.path.join(SUMMON, clean(base) + ext)
        if os.path.exists(q):
            return q, 'Summon'
    return None, None


def main():
    info, out, unmatched, skills = match.main()
    t2r = collections.defaultdict(collections.Counter)
    for s in skills:
        for t in s['T']:
            if s['roster']:
                t2r[token(t)][s['roster']] += 1
    clips = {}          # clean name -> (folder, vol127, pitch, min, cutoff)
    S = collections.OrderedDict()   # skill asset -> [(clip, vol%)]
    R = collections.OrderedDict()   # roster -> [(clip, vol%)]
    rows, missing = [], set()
    for r in out + unmatched:
        base = r['trigger']
        tk = token(base)
        snds = []
        for var, (call, vol) in r['sounds']:
            f = info[var]['file']
            src, folder = find_file(f)
            if not src:
                missing.add(f)
                continue
            c = clean(f)
            clips[c] = (folder, src, info[var])
            snds.append((c, vol))
        if not snds or tk in GENERIC:
            rows.append([base, '', '', '제외(UI·공용·파일 없음)', ' '.join(v[7:] for v, _ in r['sounds'])])
            continue
        exact = [n for sc, n in r['cands'] if sc >= 3 and any(base in s['T'] for s in skills if s['name'] == n)]
        for n in exact:
            for x in snds:
                if x not in S.setdefault(n, []):
                    S[n].append(x)
        ro = MANUAL.get(tk)
        if not ro and t2r.get(tk):
            top = t2r[tk].most_common(2)
            if len(top) == 1 or top[0][1] > top[1][1]:
                ro = top[0][0]
        if ro:
            for x in snds:
                if x not in R.setdefault(ro, []):
                    R[ro].append(x)
        rows.append([base, ' '.join(exact), ro or '', 'S' if exact else ('R' if ro else '대응 없음'), ' '.join(c for c, _ in snds)])
    used = {c for v in list(S.values()) + list(R.values()) for c, _ in v}
    os.makedirs(OUT, exist_ok=True)
    for c in sorted(used):
        folder, src, _ = clips[c]
        if folder == 'Skill':
            dst = os.path.join(OUT, c + os.path.splitext(src)[1].lower())   # 이름은 clean()(공백→_) — 표의 클립 이름과 같아야 한다
            if not os.path.exists(dst):
                shutil.copyfile(src, dst)
    with open(os.path.join(RES, 'SkillSfxTable.txt'), 'w', encoding='utf-8') as f:
        f.write('# Tools/skill_sfx/build_table.py 가 쓴다 — 손으로 고치지 말 것. C=클립(폴더 볼륨 피치 최소거리 끊김거리) S=스킬 R=로스터\n')
        for c in sorted(used):
            folder, _, i = clips[c]
            f.write('C\t%s\t%s\t%.3f\t%.2f\t%.0f\t%.0f\n' % (c, folder, i['vol'] / 127.0, i['pitch'], i['min'], i['cutoff']))
        for k, table in (('S', S), ('R', R)):
            for n, v in table.items():
                f.write('%s\t%s\t%s\n' % (k, n, ','.join('%s:%.2f' % (c, vol / 100.0) for c, vol in v)))
    with open(os.path.join(ROOT, 'Docs/research/SKILL_SFX_MAPPING.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['원작 트리거', '우리 스킬 에셋(S)', '로스터(R)', '층', '소리'])
        w.writerows(rows)
    layer = collections.Counter(r[3] for r in rows)
    print('클립', len(used), '· S 스킬', len(S), '· R 로스터', len(R), '· 트리거', dict(layer))
    print('파일 없음:', sorted(missing))
    for ro, v in R.items():
        print(' R', ro, [c for c, _ in v])


if __name__ == '__main__':
    main()
