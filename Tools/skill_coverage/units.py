import sys,re,json,collections
sys.path.insert(0,'Tools/w3x'); import w3u,wts
S=wts.parse('Tools/w3x/원본/war3map_new.wts')
U=w3u.parse('Tools/w3x/원본/war3map_new.w3u')
G=['랜덤전용','불멸의','영원한','초월함','제한됨','변화된','특수함','전설적인','희귀함','특별함','안흔함','흔함영웅','흔함']
def strip(n): return re.sub(r'\|[cC]\w{8}|\|r','',n)
rows=[]; skipped=[]
for u in U:
    f=u['mods']; raw=wts.resolve(str(f.get('unam','')),S); n=strip(raw).replace('\n',' ').strip()
    p=f.get('upoi')
    if u['id'][0] not in 'hH': continue
    if any(k in n for k in ('필요','금화','노획','토큰','묘비','[아이템]','항해일지','퇴치-','???')) and u['id']!='h0BU': skipped.append((u['id'],n,p)); continue
    grade=None
    if '[히든' in n: grade='히든'
    else:
        for g in G:
            if g in n: grade=g; break
    if grade is None:
        pm={70:'안흔함',80:'특별함',90:'희귀함',101:'전설적인',102:'특수함',103:'변화된',131:'초월함',151:'불멸의',161:'영원한'}
        if p in pm: grade=pm[p]
        elif isinstance(p,int) and 1<=p<=9: grade='흔함'
        elif isinstance(p,int) and 108<=p<=110: grade='제한됨'
        elif isinstance(p,int) and 111<=p<=119: grade='전설적인'
        elif isinstance(p,int) and 179<=p<=183: grade='랜덤전용'
    if grade is None or p is None: skipped.append((u['id'],n,p)); continue
    cat='랜덤전용' if '랜덤전용' in n else ''
    sub=''
    if grade=='랜덤전용':
        for g in G[1:]:
            if g in n.replace('랜덤전용',''): sub=g
    rows.append(dict(id=u['id'],name=n[:70],grade=grade,sub=sub,upoi=p,base=u['base'],uabi=str(f.get('uabi','')),hero=u['id'][0]=='H'))
json.dump(rows,open(sys.argv[1],'w'),ensure_ascii=False)
c=collections.Counter((r['grade'],r['upoi'] is None) for r in rows)
for k,v in sorted(c.items()): print(k,v)
print('skipped',len(skipped)); [print('  ',s) for s in skipped if s[2] not in (None,0)][:0]
print([ (r['id'],r['name'][:30]) for r in rows if r['upoi'] is None][:60])
