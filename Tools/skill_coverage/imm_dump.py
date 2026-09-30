import sys,re
sys.path.insert(0,'Tools/w3x')
import w3a,w3u
A={a['id']:a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
U={u['id']:u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
J=open('Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
def clean(s): return re.sub(r'\|c[0-9a-fA-F]{8}|\|r','',str(s)).replace('|n',' / ')
SKIP={'aart','abpx','abpy','ahky','aret','arar','atat','aani','acat','acap','aeat','atp1','aite','arac','ata0','ata1','atac','aher','alev','achd','areq','arqa','amat','asat','aspt','amac','amsp','aefs','aefl','ansf','aubx','auby','aut1','auu1','aun1','aunt','auar','arpx','arpy','arhk','aani','ahdu'}
def abil(aid,ind='      ',tip=False):
    a=A.get(aid)
    if not a: return ind+aid+' (스톡)'
    f={}
    for m in a['mods']:
        if m['field'] in SKIP or m['level']>1: continue
        if m['field']=='aub1' and not tip: continue
        f[m['field']]=clean(m['value'])[:(200 if m['field']=='aub1' else 40)] if isinstance(m['value'],str) else round(m['value'],4)
    lv=max([m['level'] for m in a['mods']] or [0])
    return '%s%s base %s lv%d %s'%(ind,aid,a['base'],lv,f)
def unit(uid,ind='    '):
    u=U.get(uid)
    if not u: return [ind+uid+' (스톡 유닛)']
    mo=u['mods']; out=[ind+'%s %s uabi %s dmg %s cd %s atk %s rng %s'%(uid,clean(mo.get('unam',''))[:30],mo.get('uabi'),mo.get('ua1b'),mo.get('ua1c'),mo.get('ua1w'),mo.get('ua1r'))]
    for ab in str(mo.get('uabi','')).split(','):
        if ab and ab not in('Avul','Aloc'): out.append(abil(ab))
    return out
VIS=r'PlaySound|DestroyEffect|AddSpecialEffect|SetUnitAnimation|SetUnitFlyHeight|SetUnitScale|vibration|RemoveLocation|SetUnitVertexColor|SetUnitTimeScale|SetlocationAutoRemove|SetUnitFacing|ShowUnit|StopSound|bj_wantDestroyGroup|SetUnitLookAt|ResetUnitLookAt|SetUnitPathing|CameraSet|PanCamera|SetSoundPosition|AttachSoundToUnit|TextTag|QueueUnitAnimation|DestroyGroup|SetUnitAnimationByIndex|SetUnitX|SetUnitY|UnitRemoveBuffs|PolledWait'
def short(l):
    return l.replace('s__TrigVariables__get_','TV.').replace('s__TrigVariables_','TV_').replace('(GlobalTV)','').replace('GlobalTV,','').replace('GetUnitStateSwap(UNIT_STATE_','ST(').replace('GetUnitState(','ST(').replace('UNIT_STATE_','')
def trig(t,limit=9000):
    q=J.find('function Trig_%s_Actions takes'%t)
    if q<0: print('없음',t); return
    b=J[q:J.index('endfunction',q)]
    conds={}
    for m in re.finditer(r'function Trig_%s_(Func\w+|Conditions) takes'%re.escape(t),J):
        conds[m.group(1)]=' & '.join(l.strip() for l in J[m.start():J.index('endfunction',m.start())].split('\n')[1:] if l.strip() not in('return false','endif','return true',''))
    out=[]
    for l in b.split('\n'):
        if re.search(VIS,l): continue
        m=re.match(r'\s*(else)?if\(Trig_\w+?_(Func\w+)\(\)\)then',l.strip())
        if m: l=('ELSEIF [' if m.group(1) else 'IF [')+conds.get(m.group(2),'?')+']'
        l=re.sub(r"ForGroupBJ\((.+),function Trig_\w+?_(Func\w+)\)",lambda m:'FORGROUP %s → {%s}'%(m.group(1)[:160],conds.get(m.group(2),'?')),l)
        l=re.sub(r"function Trig_\w+?_(Func\w+)",lambda m:'{%s}'%conds.get(m.group(1),'?'),l)
        out.append(short(l))
    print('#'*8,t,'(%d자)'%len(b))
    if 'Conditions' in conds: print('  _Conditions:',short(conds['Conditions']))
    print(short('\n'.join(out))[:limit])
    for mm in re.finditer(r"SaveTriggerHandle\(\w+,'(\w{4})',(\d+),gg_trg_%s\)"%re.escape(t),J): print('  HASH',mm.group(1),mm.group(2))
    callers=set()
    for mm in re.finditer(r'(ConditionalTriggerExecute|TriggerExecute)\(gg_trg_%s\)'%re.escape(t),J):
        k2=J.rfind('function ',0,mm.start()); callers.add(J[k2+9:k2+80].split(' takes')[0])
    print('  호출자:',sorted(callers) or '0건')
    reg=re.findall(r'call (TriggerRegister\w+)\(gg_trg_%s,([^\n]*)'%re.escape(t),J); print('  이벤트:',[(a,b[:60]) for a,b in reg][:4])
    for uid in dict.fromkeys(re.findall(r"'([a-zA-Z]\w{3})'",b)):
        if uid in U and uid[0] in 'ehHnou': print('\n'.join(unit(uid)))
    for aid in dict.fromkeys(re.findall(r"'(A\w{3})'",b+''.join(conds.values()))): print(abil(aid,'    능력참조 '))
    for bid in dict.fromkeys(re.findall(r"'(B\w{3})'",b+''.join(conds.values()))):
        own=[a['id'] for a in A.values() if any(m['field']=='abuf' and bid in str(m['value']) for m in a['mods'])]
        print('    버프',bid,'← 능력',[abil(x,'') for x in own][:3])
if __name__=='__main__':
    for t in sys.argv[1:]:
        if t.startswith('U:'): print('\n'.join(unit(t[2:],'')))
        elif t.startswith('A:'): print(abil(t[2:],'',True))
        else: trig(t)
