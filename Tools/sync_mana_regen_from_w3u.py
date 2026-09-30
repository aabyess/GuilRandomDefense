"""로스터 마나 재생(원작 umpr)·최대 마나(umpm)·게이지 환산을 원작 w3u 대응 유닛 값으로 맞춘다 — 2026-09-30 구현담당1(PM 지시).

배경: 원작 게이지 스킬은 「평타 때 마나 검사」(HashAttack 트리거에서 `if 마나==N … else 마나+x`)인데 마나는 평타 +x 말고도
초당 재생(umpr, 영웅은 + INT×0.08 — war3mapMisc IntRegenBonus)으로 찬다. 우리 게이지(OnHitCount Mana)는 타격 수만 셌다.
UnitData에 쓰는 값(없으면 0 = 지금 동작):
  manaRegenPerSecond — 원작 umpr(마나/초). 비어 있으면 기반 유닛 스톡 기본값 — hrif(라이플맨, 마나 없는 유닛)는 0으로 둔다(가정).
  manaMax            — 원작 umpm(최대 마나). 게이지가 이 값(× 환산)에서 멈춘다.
  manaGaugePerMana   — 게이지 한 칸이 마나 몇에 해당하나의 역수(게이지 칸/마나). 평타 트리거가 마나 +x를 매 타 더하면
                       우리 게이지는 매 타 +1이라 1/x. 평타 +x가 없으면(우타처럼 확률 사건에서만 +2) 그 로스터 마나 게이지 에셋의
                       문턱 ÷ umpm(예: 우타 384칸 ≈ 마나 100 → 3.84).
  lifeGaugeRegenPerSecond·lifeGaugeMax — 원작 두 번째 유닛의 마나를 Life 카운터로 세어 둔 에셋(skillName에 「MANA」가 든
                       Life 게이지 — 영원_최상호 미호크 175처럼 한 로스터에 마나 유닛이 둘일 때)용. 게이지 칸 단위.
                       ⚠️ 진짜 체력 게이지의 재생(uhpr)은 여기서 안 채운다(hrif 스톡 기본 체력 재생 미확인 — PM 확인 대기).
로스터에 대응 원작 유닛이 여럿이면 umpm×환산이 그 로스터 Mana 게이지 에셋 문턱과 맞는 유닛을 쓴다.
2026-09-30 추가(PM 승인) — 시작값과 진짜 체력 게이지:
  manaGaugeStart     — 원작 umpi(시작 마나) × 환산. 빈칸 = 기반 hrif 스톡(마나 없는 유닛) 0으로 둔다.
  진짜 체력 게이지(skillName에 「MANA」가 없는 Life 게이지 에셋)를 가진 로스터: 대응 uid 중 평타 트리거가 체력을 검사하는 유닛에서
    lifeGaugeRegenPerSecond = uhpr, lifeGaugeMax = uhpm,
    lifeGaugeStart = uhpm(원작 유닛은 체력 가득으로 생긴다 → 「체력==최대」가 첫 평타에 참). 소환 자리에서 SetUnitLifeBJ로
                     체력을 내리는 유닛(카이도 h07M → 1.00)은 0(= 스킬 resetTo).
    lifeGaugeCustomHitGain·lifeGaugeHitGain — 평타 트리거에 체력 +1이 없으면(네코마무시 h09Z) 켜고 0, HIT_GAIN_OVERRIDE에 있으면 그 값.
  uhpr 빈칸(니카 H0BK·시노부 h084)은 기반 hrif 스톡값 HRIF_STOCK_UHPR. 재생 타입(uhrt)은 이 유닛들 전부 빈칸 = hrif 스톡.
  근거(맵 안, 스톡 slk는 저장소·맵에 없다): ① 니카 툴팁 A166 「자연회복:초당+0.25/공격시+1」 — 제작자가 빈칸 유닛의 재생을 0.25로 적음
  ② hrif 기반 350기 uhpr 명시값에 0.15·0.2·0.3·0.33·0.5가 있는데 0.25만 0건(에디터는 기본값과 같은 값을 저장하지 않는다)
  ③ hrif 기반 체력 게이지 유닛 툴팁이 필드값 그대로 「자연회복 초당 0.3/0.33/0.5」라 적음(= hrif 재생 타입이 항상 돈다는 제작자 전제).
2026-09-30 추가(PM 결정) — 마나 재생 오라(AIba 기반, UnitData.manaAura* 주석 참고):
  로스터 대응 uid의 uabi 중 기반 AIba이고 Hab1 > 0인 능력 → manaAuraRegenPerSecond(Hab1)·manaAuraRange(aare)·manaAuraBuffId(abuf)·
  manaAuraIncludesSelf(atar에 notself가 없으면 AURA_DEFAULT_INCLUDES_SELF — 맵 미확정, 정황 둘). 값 0 껍데기(A0WM·A0AC)·아이템(A16D)·
  대응 없는 유닛은 자연히 빠진다. 한 로스터에 여럿이면 값이 큰 것 하나(경고).
사용: python3 Tools/sync_mana_regen_from_w3u.py [--dry] [--list <바꾼 파일 목록을 쓸 경로>]
"""
import collections
import csv
import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
sys.path.insert(0, os.path.join(ROOT, 'Tools'))
import w3u  # noqa: E402
import w3a  # noqa: E402
import skill_asset_tool as sat  # noqa: E402

FIELDS = ['manaRegenPerSecond', 'manaMax', 'manaGaugePerMana', 'lifeGaugeRegenPerSecond', 'lifeGaugeMax',
          'manaGaugeStart', 'lifeGaugeStart', 'lifeGaugeCustomHitGain', 'lifeGaugeHitGain',
          'manaAuraRegenPerSecond', 'manaAuraRange', 'manaAuraBuffId', 'manaAuraIncludesSelf']
INT_FIELDS = ('lifeGaugeCustomHitGain', 'manaAuraIncludesSelf')
STR_FIELDS = ('manaAuraBuffId',)
HRIF_STOCK_UHPR = 0.25   # ⚠️ 스톡 slk 미확인(저장소·맵에 없음) — 맵 안 근거 셋, 맨 위 주석 ①②③
AURA_DEFAULT_INCLUDES_SELF = True   # atar 빈칸(스톡)일 때 — 맵 미확정. 뒤집으려면 여기만
# 평타 +1이 확률 굴림 안에만 있는 유닛: 카타쿠리 Trig_katakuriAttack — `GetRandomInt(1,7)==4`일 때 체력>36이면 −17, 아니면 +1.
HIT_GAIN_OVERRIDE = {'h07I': 1.0 / 7.0}


def main(dry, list_path=None):
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    AUR = {}
    for a in w3a.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3a')):
        if a['base'] != 'AIba':
            continue
        f = {}
        for m in a['mods']:
            f.setdefault(m['field'], m['value'])
        if float(f.get('Hab1') or 0) > 0:
            AUR[a['id']] = (float(f['Hab1']), float(f.get('aare') or 0), str(f.get('abuf') or ''),
                            (AURA_DEFAULT_INCLUDES_SELF if 'atar' not in f else ('notself' not in f['atar'] and 'self' in f['atar'])))
    J = open(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.j'), encoding='utf-8', errors='replace').read()
    HASH = collections.defaultdict(list)
    for m in re.finditer(r"SaveTriggerHandle\(\w+,'(\w{4})',\d+,gg_trg_(\w+)\)", J):
        HASH[m.group(1)].append(m.group(2))
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        rmap[r['로스터']].append(r['유닛ID'])
    guid_to_asset = {re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1): p
                     for p in glob.glob(os.path.join(ROOT, 'Assets/Data/UnitSkills/*.asset'))}

    def trigger_mana(uid):
        """(평타 트리거가 매 타 더하는 마나 x 또는 None, 마나 검사 있음)"""
        for t in HASH.get(uid, []):
            p = J.find('function Trig_%s_Actions takes' % t)
            if p < 0:
                continue
            b = J[p:J.index('endfunction', p)]
            checks = bool(re.search(r'UNIT_STATE_MANA,GetAttacker\(\)\)(==|>=)', b))
            # 맨 바깥(조건 없는) +x: `else` 갈래의 +x — 검사 if의 else에서 더하는 표준 모양
            m = re.search(r'UNIT_STATE_MANA,GetAttacker\(\)\)(?:==|>=)[\d.]+ then.*?else\s*call SetUnitManaBJ\(GetAttacker\(\),\(GetUnitStateSwap\(UNIT_STATE_MANA,GetAttacker\(\)\)\+([\d.]+)\)\)', b, re.S)
            if m:
                return float(m.group(1)), checks
            m = re.match(r'function \w+ takes nothing returns nothing\s*call SetUnitManaBJ\(GetAttacker\(\),\(GetUnitStateSwap\(UNIT_STATE_MANA,GetAttacker\(\)\)\+([\d.]+)\)\)', b)
            if m:
                return float(m.group(1)), checks
            return None, checks
        return None, False

    def trigger_life(uid):
        """(평타 트리거가 체력을 검사함, 체력 +1 있음)"""
        for t in HASH.get(uid, []):
            p = J.find('function Trig_%s_Actions takes' % t)
            if p < 0:
                continue
            b = J[p:J.index('endfunction', p)]
            # 조건이 Func…C 함수로 빠진 트리거(흰수염 Legend5·카이도)도 본다
            conds = ''.join(J[q.start():J.index('endfunction', q.start())]
                            for q in re.finditer(r'function Trig_%s_Func\w+C takes' % re.escape(t), J))
            checks = bool(re.search(r'UNIT_STATE_LIFE,[^)]*\)\)(==|>)', b + conds))
            return checks, bool(re.search(r'UNIT_STATE_LIFE,[^)]*\)\)\+1', b))
        return False, False

    def spawn_life(uid):
        """소환 자리에서 SetUnitLifeBJ(GetLastCreatedUnit(),N)으로 내리는 체력. 없으면 None(= 가득)."""
        for m in re.finditer(r"Create\w*\([^\n]*'%s'[^\n]*\n(?:[^\n]*\n){0,2}?call SetUnitLifeBJ\(GetLastCreatedUnit\(\),([\d.]+)\)" % uid, J):
            return float(m.group(1))
        return None

    changed, notes, same = [], [], 0
    for ro, uids in sorted(rmap.items()):
        p = os.path.join(ROOT, 'Assets/Data/Units/Roster', ro + '.asset')
        if not os.path.exists(p):
            continue
        cands = []
        for uid in uids:
            mo = U.get(uid, {}).get('mods', {})
            if not mo.get('umpm'):
                continue
            per_hit, checks = trigger_mana(uid)
            cands.append((checks, uid, float(mo.get('umpm')), float(mo.get('umpr') or 0.0), per_hit))
        text = open(p, encoding='utf-8').read()   # 매번 디스크에서 다시 읽는다(모델 배선이 prefab 줄을 바꾼다)
        # 로스터 게이지 에셋: (종류, 문턱, skillName)
        gauges = []
        for g in re.findall(r'guid: (\w+), type: 2', text.split('  trait:')[0]):
            ap = guid_to_asset.get(g)
            if not ap:
                continue
            a = sat.load(ap)
            if not re.search(r'^  triggerType: 3', a.head, re.M):
                continue
            sk = (re.search(r'^  skillName: (.*)$', a.head, re.M) or [None, ''])[1]
            for li in range(len(a.levels)):
                t = int(sat.get_level_field(a, li, 'hitCountThreshold') or 0) or int(sat.get_level_field(a, li, 'hitCountFloor') or 0)
                if t:
                    gauges.append((int(sat.get_level_field(a, li, 'gaugeKind') or 0), t, sk))
        mana_thr = [t for k, t, _ in gauges if k == 0]
        life_mana = [(t, sk) for k, t, sk in gauges if k == 1 and 'MANA' in sk.upper()]
        life_real = [(t, sk) for k, t, sk in gauges if k == 1 and 'MANA' not in sk.upper()]
        auras = sorted((AUR[ab] for uid in uids for ab in str(U.get(uid, {}).get('mods', {}).get('uabi', '')).split(',') if ab in AUR),
                       reverse=True)
        if len(set(auras)) > 1:
            notes.append('⚠️ %s: 마나 재생 오라가 여럿 %s — 큰 것 하나만' % (ro, auras))
        aura = auras[0] if auras else (0.0, 0.0, '', False)
        if not cands and not life_real and not auras:
            continue
        # 진짜 체력 게이지
        lstart, lcustom, lgain = 0.0, 0, 0.0
        lreal = None
        if life_real:
            lc = []
            for uid in uids:
                mo = U.get(uid, {}).get('mods', {})
                checks, plus1 = trigger_life(uid)
                if checks:
                    lc.append((uid, mo, plus1))
            if len(lc) > 1:
                # 한 로스터에 체력 검사 유닛이 여럿(불멸_정준영 = 빅맘 h04Q + 카이도 용형 h0AD) — 게이지 에셋 문턱과 uhpm이 같은 유닛 하나로
                thr_set = {float(t) for t, _ in life_real}
                narrowed = [x for x in lc if float(x[1].get('uhpm') or 0) in thr_set]
                if len(narrowed) == 1:
                    lc = narrowed
            if len(lc) != 1:
                notes.append('⚠️ %s: 체력 게이지 에셋은 있는데 체력 검사 트리거 유닛이 %d개 — 건너뜀' % (ro, len(lc)))
            else:
                luid, mo, plus1 = lc[0]
                base = U[luid]['base']
                if 'uhpr' in mo:
                    lr = float(mo['uhpr'])
                elif base == 'hrif':
                    lr = HRIF_STOCK_UHPR
                    notes.append('%s(%s): uhpr 빈칸 → hrif 스톡 %g' % (ro, luid, lr))
                else:
                    lr = 0.0
                    notes.append('⚠️ %s(%s): uhpr 빈칸, 기반 %s 스톡 모름 → 0' % (ro, luid, base))
                if 'uhrt' in mo and mo['uhrt'] != 'always':
                    notes.append('⚠️ %s(%s): 재생 타입 %s — 항상이 아님, 재생 0으로' % (ro, luid, mo['uhrt']))
                    lr = 0.0
                lm = float(mo.get('uhpm') or 0)
                sl = spawn_life(luid)
                lstart = lm if sl is None else 0.0
                if sl is not None:
                    notes.append('%s(%s): 소환 때 체력 %g으로 내림 → 시작값은 resetTo' % (ro, luid, sl))
                if luid in HIT_GAIN_OVERRIDE:
                    lcustom, lgain = 1, HIT_GAIN_OVERRIDE[luid]
                elif not plus1:
                    lcustom, lgain = 1, 0.0
                    notes.append('%s(%s): 평타 체력 +1 없음 → 재생으로만 참' % (ro, luid))
                thr = [t for t, _ in life_real if t]
                if thr and max(thr) > lm:
                    notes.append('⚠️ %s(%s): 체력 게이지 문턱 %s > uhpm %g' % (ro, luid, thr, lm))
                lreal = (luid, lr, lm)
        if not cands:
            cands = [(False, '-', 0.0, 0.0, None)]   # 마나 없는 로스터(카타쿠리) — 마나 필드는 0

        def gpm_of(c):
            return 1.0 / c[4] if c[4] else ((max(mana_thr) / c[2]) if mana_thr else 1.0)
        # Mana 게이지: umpm×환산이 문턱과 맞는 유닛 우선, 없으면 마나 검사하는 첫 유닛
        match = lambda c: mana_thr and any(abs(c[2] * gpm_of(c) - t) < 0.5 for t in mana_thr)
        pick = ([c for c in cands if c[4] and match(c)] or [c for c in cands if match(c)]
                or [c for c in cands if c[0]] or cands)   # 평타 +x를 읽은 유닛 먼저(환산을 문턱에서 역산하면 아무 유닛이나 맞는다)
        checks, uid, umpm, umpr, per_hit = pick[0]
        gpm = gpm_of(pick[0])
        if not per_hit:
            notes.append('%s(%s): 평타 +x 없음 → 게이지 환산 %.3f (마나 게이지 문턱 %s ÷ umpm %g)' % (ro, uid, gpm, mana_thr, umpm))
        if mana_thr and max(mana_thr) > umpm * gpm + 0.5:
            notes.append('⚠️ %s: 마나 게이지 문턱 %s > 상한 %g — 상한을 문턱으로 올림' % (ro, mana_thr, umpm * gpm))
            umpm = max(mana_thr) / gpm
        # Life 카운터로 센 두 번째 마나 유닛
        lrg, lmax = 0.0, 0.0
        for t, sk in life_mana:
            other = [c for c in cands if c[1] != uid and abs(c[2] - t) < 0.5]
            if other:
                lrg, lmax = other[0][3], other[0][2]
                notes.append('%s: Life 카운터 마나 %s ← %s(umpr %g · umpm %g)' % (ro, sk[:30], other[0][1], lrg, lmax))
        if uid == '-':
            gpm = 0.0
            notes[:] = [n for n in notes if not n.startswith(ro + '(-)')]
        if lreal:
            if lmax:
                notes.append('⚠️ %s: Life 카운터를 마나로도 쓰고 진짜 체력 게이지도 있음 — 체력 쪽을 씀' % ro)
            lrg, lmax = lreal[1], lreal[2]
        umpi = float(U.get(uid, {}).get('mods', {}).get('umpi') or 0.0)
        mstart = umpi * gpm
        if umpi:
            notes.append('%s(%s): umpi %g → 마나 게이지 시작 %g' % (ro, uid, umpi, mstart))
        target = [round(umpr, 4), round(umpm, 4), round(gpm, 4), round(lrg, 4), round(lmax, 4),
                  round(mstart, 4), round(lstart, 4), float(lcustom), round(lgain, 4),
                  round(aura[0], 4), round(aura[1], 4), aura[2], float(aura[3])]

        def get(k):
            m = re.search(r'^  %s: ?(\S*)' % k, text, re.M)
            if k in STR_FIELDS:
                return m.group(1) if m else ''
            return round(float(m.group(1)), 4) if m else 0.0
        if [get(k) for k in FIELDS] == target:
            same += 1
            continue
        changed.append((ro, uid, target))
        if dry:
            continue
        for k, v in zip(FIELDS, target):
            s = v if k in STR_FIELDS else (str(int(v)) if k in INT_FIELDS else repr(float(v)))
            if re.search(r'^  %s: ' % k, text, re.M):
                text = re.sub(r'^(  %s:) .*$' % k, lambda m: m.group(1) + ' ' + s, text, count=1, flags=re.M)
            else:
                text = text.rstrip('\n') + '\n  %s: %s\n' % (k, s)
        open(p, 'w', encoding='utf-8').write(text)
    print('같음 %d · 바꿈 %d' % (same, len(changed)))
    for ro, uid, t in changed:
        print('  %-24s %s  umpr %g · umpm %g · 게이지/마나 %g · Life 재생 %g/상한 %g · 시작 마나 %g/체력 %g · 타당 체력 %s' % (
            ro, uid, *t[:7], ('기본 +1' if not t[7] else '%.4g' % t[8]))
              + (' · 마나 오라 %g/초 반경 %g 버프 %s 자기%s' % (t[9], t[10], t[11], '포함' if t[12] else '제외') if t[9] else ''))
    if list_path and not dry:
        open(list_path, 'w', encoding='utf-8').write(''.join('Assets/Data/Units/Roster/%s.asset\n' % ro for ro, _, _ in changed))
        print('바꾼 파일 목록 → %s' % list_path)
    for n in notes:
        print('  참고', n)


if __name__ == '__main__':
    main('--dry' in sys.argv, sys.argv[sys.argv.index('--list') + 1] if '--list' in sys.argv else None)
