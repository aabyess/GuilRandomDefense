"""original_asset_inventory.py가 낸 inventory.json을 종류별로 센다(2026-10-08). 실행: /usr/bin/python3 Tools/w3x/original_asset_inventory_stats.py  (먼저 original_asset_inventory.py)"""
import sys,os,re,collections,json
sys.path.insert(0,'Tools/w3x')
import w3u,wts
S=wts.parse('Tools/w3x/원본/war3map_new.wts')
inv=json.load(open(os.path.expanduser('~/GRD_motion_trial/inventory/inventory.json')))
byname={r['name'].lower():r for r in inv['files']}
U=w3u.parse('Tools/w3x/원본/풀린것/war3map.w3u')
def key(n):
    r=byname.get(n.replace('\\\\','\\').lower()); return r['file'].lower() if r and r['inmap'] else None
unit_char=set(); unit_dummy=set(); unit_other=set(); proj=set()
for u in U:
    m=u['mods'].get('umdl')
    if m:
        k=key(m)
        if k:
            (unit_dummy if u['id'][0]=='e' else unit_char if u['id'][0] in 'hHonN' else unit_other).add(k)
    a=u['mods'].get('ua1m')
    if a and key(a): proj.add(key(a))
uniq={}
for r in inv['files']:
    if r['inmap']: uniq.setdefault(r['file'].lower(),dict(file=r['file'],size=r['size'],roles=set())) ['roles'].update(r['roles'])
fx_roles={'시전자이펙트','대상이펙트','특수이펙트','효과이펙트','광역이펙트','번개','트리거이펙트'}
fx=set(k for k,u in uniq.items() if u['file'].lower().endswith('.mdx') and (u['roles']&fx_roles))
fx_d=unit_dummy
icons=set(k for k,u in uniq.items() if u['file'].lower().endswith('.blp') and (u['roles']&{'능력아이콘','유닛아이콘','연구아이콘','해제아이콘','데이터:아이템','데이터:업그레이드','데이터:버프'} and not u['roles']&{'텍스처'} or re.search(r'(^|\\)(btn|pasbtn|disbtn|atc)',u['file'].lower())))
mdx=set(k for k,u in uniq.items() if u['file'].lower().endswith('.mdx'))
blp=set(k for k,u in uniq.items() if u['file'].lower().endswith('.blp'))
tex=blp-icons
def mb(s): return round(sum(uniq[k]['size'] for k in s)/1e6,1)
print('mdx total',len(mdx),mb(mdx)); print('unit_char',len(unit_char),mb(unit_char)); print('unit_dummy',len(unit_dummy),mb(unit_dummy)); print('unit_other',len(unit_other))
print('proj',len(proj)); print('fx(w3a/j)',len(fx)); 
fx_all=fx|unit_dummy|proj
effect_only=(fx_all)-unit_char
print('effect union (dummy+fx+proj minus char)',len(effect_only),mb(effect_only))
print('char ∩ effect',len(unit_char&fx_all))
rest=mdx-unit_char-effect_only
print('mdx rest (하위모델 등, 장식/파괴물 등)',len(rest),[uniq[k]['file'] for k in list(rest)[:8]])
print('blp',len(blp),mb(blp),'icons',len(icons),mb(icons),'tex',len(tex),mb(tex))
snd=[k for k,u in uniq.items() if u['file'].lower().endswith(('.mp3','.wav'))]; print('sound',len(snd),mb(snd))
# stock-referenced names (not in map)
miss=collections.Counter()
for r in inv['files']:
    if not r['inmap'] and r['ext'] in('mdl','mdx','blp','mp3','wav','tga'):
        miss[r['ext']]+=1
print('referenced but not in map',miss)
# char model list for 15
json.dump(dict(unit_char=sorted(uniq[k]['file'] for k in unit_char),dummy=sorted(uniq[k]['file'] for k in unit_dummy)),open('/tmp/inv_models.json','w'),ensure_ascii=False)
st=collections.defaultdict(set)
for u in U:
    m=u['mods'].get('umdl')
    if m and u['id'][0] in 'hHonN' and not key(m): st[u['id'][0]].add(re.sub(r'\.mdl$|\.mdx$','',m.replace('\\\\','\\').lower()))
print({k:len(v) for k,v in st.items()}, len(set().union(*st.values())))
# distinct stock names by stem across all refs
stem=collections.defaultdict(set)
for r in inv['files']:
    if not r['inmap'] and r['ext'] in('mdl','mdx','blp','mp3','wav','tga'):
        e=r['ext']; e={'mdl':'모델','mdx':'모델'}.get(e,e)
        stem[e].add(re.sub(r'\.\w+$','',r['name'].lower()))
print({k:len(v) for k,v in stem.items()})
