import sys,json,collections,re
sys.path.insert(0,'Tools/w3x'); import w3a,wts
S=wts.parse('Tools/w3x/원본/war3map_new.wts')
A={a['id']:a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
R=json.load(open(sys.argv[1]+'/cov/units.json'))
J=open('Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
cnt=collections.Counter(); byg=collections.defaultdict(set)
for r in R:
    for ab in r['uabi'].split(','):
        if ab: cnt[ab]+=1; byg[ab].add(r['grade'])
out=[]
for ab,n in cnt.most_common():
    a=A.get(ab)
    if not a: out.append((ab,'STOCK',n,'','','',sorted(byg[ab]))); continue
    fl=w3a.fields_by_level(a)
    def g(k):
        v=fl.get(k,{}); return v.get(1,v.get(0)) if v else None
    name=wts.resolve(str(g('anam') or ''),S)
    nums={k:g(k) for k in fl if k[:1].isupper() and isinstance(g(k),(int,float)) and g(k)}
    inj=("'%s'"%ab) in J
    out.append((ab,a['base'],n,name[:24],str(g('atar'))[:28],'j' if inj else '',sorted(byg[ab]),str(nums)[:90],g('aord') or ''))
json.dump(out,open(sys.argv[1]+'/cov/abils.json','w'),ensure_ascii=False)
bc=collections.Counter(o[1] for o in out); print(len(out),'distinct'); print(bc.most_common(40))
