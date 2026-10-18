import re,os,json,collections,sys
sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools');sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x')
b=os.path.expanduser('~/grd/GRD_s2_map/')
t=open(b+'s2/war3map.j',encoding='utf-8',errors='replace').read()
rects={}
for m in re.finditer(r'set ([A-Za-z_0-9]+)=Rect\((-?[\d.]+),(-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\)',t):
    rects[m.group(1)]=tuple(float(m.group(i)) for i in range(2,6))
funcs=re.split(r'\nfunction ',t)
def rc(h):
    v=int(h,16);return bytes([(v>>24)&255,(v>>16)&255,(v>>8)&255,v&255]).decode('latin1')
U={}
import rematch_s2_data as D
us,ab,ast,ustr=D.load_s2()
names={k:v['name_full'] or v['name'] for k,v in us.items()}
# 함수별: 쓰는 rect 변수, CreateUnit 유닛 코드
rows=[]
for f in funcs[1:]:
    head=f.split('\n',1)[0]
    fn=head.split(' ')[0]
    used=[v for v in rects if re.search(r'(?<![A-Za-z0-9_])%s(?![A-Za-z0-9_])'%re.escape(v),f)] if len(f)<200000 else []
    units=re.findall(r"CreateUnit\([^,]+,(?:'([^']{4})'|\$([0-9A-Fa-f]{8}))",f)
    codes=[a or rc(c) for a,c in units]
    if used and codes:rows.append((fn,[(v,[round(x) for x in rects[v]]) for v in used][:6],collections.Counter(codes).most_common(6)))
print(len(funcs),len(rects),len(rows))
for r in rows[:40]:print(r[0],r[1],[(c,names.get(c,'?')[:12],n) for c,n in r[2]])
print('----- CUo')
for f in funcs[1:]:
    if f.startswith('CUo '):
        for m in re.finditer(r"CreateUnit\([^;]{0,160}",f):print(m.group(0)[:170])
        print(len(f))
shops={k:v for k,v in names.items() if any(w in v for w in ('소 ','소','일지','연구','창고','포탈','상점','환전'))}
print({k:v for k,v in shops.items() if k[0]=='h' and any(w in v for w in ('도박소','강화소','도움소','일지','환전소','연구소'))})
