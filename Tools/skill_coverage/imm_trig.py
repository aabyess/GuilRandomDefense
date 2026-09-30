import sys,re
sys.path.insert(0,'Tools/w3x')
import w3a,w3u
A={a['id']:a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
U={u['id']:u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
J=open('Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
def clean(s): return re.sub(r'\|c[0-9a-fA-F]{8}|\|r','',str(s)).replace('|n',' / ')
SKIP={'aart','abpx','abpy','ahky','aret','arar','atat','aani','acat','acap','aeat','atp1','aite','arac','ata0','ata1','atac','aher','alev','achd','areq','arqa','aub1','amat','asat','aspt','amac','amsp','aefs','aefl'}
def abil(aid,ind='      '):
    a=A.get(aid)
    if not a: print(ind,aid,'(스톡)'); return
    f={}
    for m in a['mods']:
        if m['field'] in SKIP or m['level']>1: continue
        f[m['field']]=clean(m['value'])[:40] if isinstance(m['value'],str) else round(m['value'],4)
    print(ind,aid,'base',a['base'],f)
def unit(uid,ind='    '):
    u=U.get(uid)
    if not u: print(ind,uid,'(스톡 유닛)'); return
    mo=u['mods']; print(ind,uid,clean(mo.get('unam',''))[:30],'uabi',mo.get('uabi'),'dmg',mo.get('ua1b'),'cd',mo.get('ua1c'))
    for ab in str(mo.get('uabi','')).split(','):
        if ab and ab not in('Avul','Aloc'): abil(ab)
def trig(t,limit=6000,quiet=False):
    q=J.find('function Trig_%s_Actions takes'%t)
    if q<0: print('없음',t); return
    b=J[q:J.index('endfunction',q)]
    conds=[J[m.start():J.index('endfunction',m.start())] for m in re.finditer(r'function Trig_%s_(?:Func\w+|Conditions) takes'%re.escape(t),J)]
    print('#'*10,t,len(b))
    body=b
    # 시각·소리 줄 줄이기
    out=[]
    for l in body.split('\n'):
        if re.search(r'PlaySound|DestroyEffect|AddSpecialEffect|SetUnitAnimation|SetUnitFlyHeight|SetUnitScale|vibration|RemoveLocation|SetUnitLookAt|ResetUnitLookAt|SetUnitVertexColor|SetUnitTimeScale|CameraSet|PanCamera',l): continue
        l=l.replace('s__TrigVariables__get_','TV.').replace('s__TrigVariables_','TV_').replace('(GlobalTV)','').replace('GlobalTV,','')
        out.append(l)
    if not quiet: print('\n'.join(out)[:limit])
    for c in conds:
        cc=[l for l in c.split('\n')[1:] if l.strip() not in('return false','endif','return true','')]
        print('  조건',c.split(' takes')[0].split('_')[-1],':',' | '.join(x.strip() for x in cc)[:400].replace('s__TrigVariables','TV'))
    for uid in dict.fromkeys(re.findall(r"Create\w*\([^\n]*?'(\w{4})'",b)): unit(uid)
    for aid in dict.fromkeys(re.findall(r"'(A\w{3})'",b+''.join(conds))): abil(aid,'    능력참조')
if __name__=='__main__':
    for t in sys.argv[1:]: trig(t)
