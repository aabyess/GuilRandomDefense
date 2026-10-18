import re,sys,os,collections,json
sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools');sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x')
b=os.path.expanduser('~/grd/GRD_s2_map/')
def rc(hexs):
    v=int(hexs,16);return bytes([(v>>24)&255,(v>>16)&255,(v>>8)&255,v&255]).decode('latin1')
def scan(tag):
    t=open(b+tag+'/war3map.j',encoding='utf-8',errors='replace').read()
    out=[]
    for m in re.finditer(r"CreateUnit\(([^,()]+(?:\([^()]*\))?),(?:'([^']{4})'|\$([0-9A-Fa-f]{8})),(-?[\d.]+),(-?[\d.]+),",t):
        rawc=m.group(2) or rc(m.group(3));out.append((rawc,float(m.group(4)),float(m.group(5)),m.group(1)))
    for m in re.finditer(r"CreateNUnitsAtLoc|CreateUnitAtLoc",t):pass
    return out,t
if __name__=='__main__':
    for tag in('s2','old'):
        u,t=scan(tag);c=collections.Counter(x[0] for x in u)
        print(tag,len(u),len(c),c.most_common(15))
