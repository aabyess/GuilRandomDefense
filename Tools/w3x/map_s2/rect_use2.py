import re,os,json,collections,sys
sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools');sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x')
import rematch_s2_data as D
b=os.path.expanduser('~/grd/GRD_s2_map/')
t=open(b+'s2/war3map.j',encoding='utf-8',errors='replace').read()
rects={}
for m in re.finditer(r'set ([A-Za-z_0-9]+)=Rect\((-?[\d.]+),(-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\)',t):
    rects[m.group(1)]=tuple(float(m.group(i)) for i in range(2,6))
us,ab,ast,ustr=D.load_s2()
def nm(c):
    s=ustr.get(c,{}).get('Name','') or ''
    return D.clean(s)[:20] if s else c
def rc(h):
    v=int(h,16);return bytes([(v>>24)&255,(v>>16)&255,(v>>8)&255,v&255]).decode('latin1')
funcs=re.split(r'\nfunction ',t)
res=collections.defaultdict(lambda:collections.Counter())
for f in funcs[1:]:
    fn=f.split(' ',1)[0]
    # CreateUnit·Bcy(...) 등 「유닛 코드 + rect 중심」 패턴
    for m in re.finditer(r"(?:CreateUnit|Bcy|CreateNUnits[A-Za-z]*)\([^;]*?(?:\$([0-9A-Fa-f]{8})|'(\w{4})')[^;]*?GetRect(?:Center|Min)X\(([A-Za-z0-9_]+)\)",f):
        code=m.group(2) or rc(m.group(1));v=m.group(3)
        if v in rects:res[v][nm(code)]+=1
for v,c in sorted(res.items(),key=lambda x:rects[x[0]][0]):
    r=rects[v];print(v,[round(x) for x in r],dict(c.most_common(8)))
print('======== 동사별 rect 사용')
use=collections.defaultdict(collections.Counter)
for m in re.finditer(r"(\w+)\(([^;\n]{0,120}?)(?:GetRect(?:Center|Min|Max)[XY]|\b)\(([A-Za-z0-9_]+)\)",t):
    pass
for m in re.finditer(r"(\w+)\(([^;\n]{0,160})",t):
    verb=m.group(1);args=m.group(2)
    for v in re.findall(r'GetRect(?:Center|Min|Max)[XY]\(([A-Za-z0-9_]+)\)',args):
        if v in rects:use[v][verb]+=1
for v,c in sorted(use.items(),key=lambda x:rects[x[0]][0]):
    r=rects[v];print(v,[round(x) for x in r],dict(c.most_common(5)))
