import re,sys
J=open('Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
def show(t):
    q=J.find('function Trig_%s_Actions takes'%t); b=J[q:J.index('endfunction',q)]
    conds={m.group(1):' & '.join(l.strip() for l in J[m.start():J.index('endfunction',m.start())].split('\n')[1:] if l.strip() not in('return false','endif','return true','')) for m in re.finditer(r'function Trig_%s_(Func\w+) takes'%re.escape(t),J)}
    out=[]
    for l in b.split('\n'):
        if re.search(r'PlaySound|DestroyEffect|AddSpecialEffect|SetUnitAnimation|SetUnitFlyHeight|SetUnitScale|vibration|RemoveLocation|SetUnitVertexColor|SetUnitTimeScale|SetlocationAutoRemove|Setreal\(GlobalTV,\d+,(Angle|GetRandomDirection)|TimedLife|SetUnitFacing|SetUnitPosition|ShowUnit|StopSound|bj_wantDestroyGroup|SetUnitLookAt|SetUnitPathing|SetUnitX|SetUnitY',l.strip()): continue
        m=re.match(r'if\(Trig_\w+?_(Func\w+)\(\)\)then',l.strip())
        if m: l='if ['+conds.get(m.group(1),'?')+']'
        l=re.sub(r"ForGroupBJ\((\S+?),function Trig_\w+?_(Func\w+)\)",lambda m:'FORGROUP %s → %s'%(m.group(1)[:90],conds.get(m.group(2),'?')),l)
        l=l.replace('s__TrigVariables__get_','TV.').replace('s__TrigVariables_','TV_').replace('(GlobalTV)','').replace('GlobalTV,','')
        out.append(l)
    print('#####',t); print('\n'.join(out))
for t in sys.argv[1:]: show(t)
