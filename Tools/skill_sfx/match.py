"""원작 스킬 효과음 → 우리 스킬 에셋 대응(2026-09-30 PM).

원작: war3map_new.j InitSounds의 gg_snd_X=CreateSound("파일") 과 그것을 PlaySoundOnUnitBJ/PlaySoundAtPointBJ/PlaySoundBJ 로
내는 트리거 함수. 우리: Assets/Data/UnitSkills/*.asset 의 skillName·description 에 적힌 원작 트리거 이름·능력/더미 코드,
파일 이름의 로스터 → Docs/reference/MASTER_UID_ROSTER_MAP.csv 의 원작 유닛 ID.
점수: 트리거 이름 일치 3 · 공유 능력/더미 코드 하나당 2 · 호출한 트리거 일치 1. 로스터 uid 가 그 트리거(또는 호출자)에
나오거나 트리거 이름이 맞아야 후보가 된다.
"""
import bisect, collections, csv, glob, os, re, unicodedata

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
NFC = lambda s: unicodedata.normalize('NFC', s)
CODE = re.compile(r"'([A-Za-z][0-9A-Za-z]{3})'")


def load_j():
    J = open(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.j'), encoding='utf-8', errors='replace').read().replace('\r', '')
    funcs = [(m.start(), m.group(1)) for m in re.finditer(r'function (\w+) takes', J)]
    starts = [s for s, _ in funcs]
    body = {}
    for i, (s, n) in enumerate(funcs):
        body[n] = J[s:(funcs[i + 1][0] if i + 1 < len(funcs) else len(J))]

    def fn(pos):
        return funcs[bisect.bisect_right(starts, pos) - 1][1]
    return J, body, fn


def trig_base(fname):
    m = re.match(r'(Trig_.+?)_(Actions|Conditions|Func\d+\w*)$', fname)
    return m.group(1) if m else fname


def sounds(J, fn):
    init = J[J.find('function InitSounds'):]
    init = init[:init.find('endfunction')]
    info = {}
    for m in re.finditer(r'set (gg_snd_\w+)=CreateSound\("([^"]+)"', init):
        info[m.group(1)] = {'file': m.group(2).replace('\\\\', '/'), 'vol': 127, 'pitch': 1.0, 'cutoff': 0.0, 'min': 0.0}
    for m in re.finditer(r'SetSoundVolume\((gg_snd_\w+),(\d+)\)', init):
        info[m.group(1)]['vol'] = int(m.group(2))
    for m in re.finditer(r'SetSoundPitch\((gg_snd_\w+),([\d.]+)\)', init):
        info[m.group(1)]['pitch'] = float(m.group(2))
    for m in re.finditer(r'SetSoundDistanceCutoff\((gg_snd_\w+),([\d.]+)\)', init):
        info[m.group(1)]['cutoff'] = float(m.group(2))
    for m in re.finditer(r'SetSoundDistances\((gg_snd_\w+),([\d.]+),', init):
        info[m.group(1)]['min'] = float(m.group(2))
    uses = collections.defaultdict(list)   # trigger base -> [(var, call, volume%)]
    for m in re.finditer(r'(PlaySoundOnUnitBJ|PlaySoundAtPointBJ|PlaySoundBJ|StartSound)\((gg_snd_\w+)(?:,([\d.]+))?', J):
        f = fn(m.start())
        if f == 'InitSounds':
            continue
        uses[trig_base(f)].append((m.group(2), m.group(1), float(m.group(3) or 100)))
    return info, uses


def main():
    J, body, fn = load_j()
    info, uses = sounds(J, fn)
    by_base = collections.defaultdict(str)
    for n, b in body.items():
        by_base[trig_base(n)] += b
    callers = collections.defaultdict(set)   # trigger base -> bases that reference gg_trg_<it>
    for n, b in body.items():
        for m in re.finditer(r'gg_trg_(\w+)', b):
            if n.startswith('Trig_') :
                callers['Trig_' + m.group(1)].add(trig_base(n))
    rmap = collections.defaultdict(set)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        rmap[NFC(r['로스터'])].add(r['유닛ID'])
    rosters = sorted(rmap, key=len, reverse=True)
    skills = []
    for p in sorted(glob.glob(os.path.join(ROOT, 'Assets/Data/UnitSkills/*.asset'))):
        t = open(p, encoding='utf-8').read()
        name = NFC(os.path.basename(p)[:-6])
        m = re.search(r'^  skillName: (.*)$', t, re.M)
        ds = re.search(r'^  description: (.*?)^  triggerType', t, re.M | re.S)
        desc = (m.group(1) if m else '') + ' ' + (ds.group(1) if ds else '')
        desc = desc.split('제거된 행')[0]
        T = set(re.findall(r'Trig_[A-Za-z0-9_]+', desc))
        T = {re.sub(r'_(Actions|Conditions)$', '', x).rstrip('_') for x in T}
        A = set(re.findall(r'(?<![A-Za-z0-9])([AaEe][0-9][0-9A-Z]{2})(?![A-Za-z0-9])', desc + ' ' + name))
        ro = next((r for r in rosters if r in name), None)
        skills.append({'name': name, 'T': T, 'A': A, 'roster': ro, 'uids': rmap.get(ro, set())})
    out = []
    unmatched = []
    for base, us in sorted(uses.items()):
        text = by_base[base]
        codes = set(CODE.findall(text))
        ab = {c for c in codes if c[0] in 'AaEe'}
        cal = callers.get(base, set())
        caltext = ''.join(by_base[c] for c in cal)
        owner = {c for c in codes | set(CODE.findall(caltext)) if c[0] in 'hHnNoOuU'}
        cands = []
        for s in skills:
            exact = base in s['T']
            sc = 3 * exact + 2 * len(ab & s['A']) + (1 if cal & s['T'] else 0)
            if sc and (exact or (owner & s['uids']) or (ab & s['A'])):
                cands.append((sc, s['name']))
        cands.sort(reverse=True)
        snds = collections.OrderedDict()
        for v, call, vol in us:
            snds.setdefault(v, (call, vol))
        row = {'trigger': base, 'sounds': list(snds.items()), 'owner': sorted(owner)[:6], 'cands': cands[:4]}
        (out if cands else unmatched).append(row)
    return info, out, unmatched, skills


if __name__ == '__main__':
    info, out, unmatched, skills = main()
    print('matched triggers', len(out), 'unmatched', len(unmatched))
    for r in out:
        print(r['trigger'], [v[7:] for v, _ in r['sounds']], '→', r['cands'][:3])
    print('---- unmatched')
    for r in unmatched:
        print(r['trigger'], [v[7:] for v, _ in r['sounds']], r['owner'])
