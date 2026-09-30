"""uid(또는 트리거 이름) → HashAttack 평타 트리거에서 gg_trg_로 닿는 트리거 전부의 본문 덤프.
쓰기: python3 Tools/skill_coverage/closure_dump.py <출력폴더> H05N H08U …  (uid 대신 T:트리거이름도 됨)
출력: <출력폴더>/<uid>.txt — 유닛 uabi 능력 필드 + 트리거 본문(조건 함수 인라인·위치 레지스터·더미 능력·버프 출처)
       + 평타 등록 밖에서 그 uid를 언급하는 트리거 이름. 읽기 전용."""
import re,sys,subprocess,os
J=open('Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
def body(t):
    q=J.find('function Trig_%s_Actions takes'%t)
    if q<0: return ''
    b=J[q:J.index('endfunction',q)]
    for m in re.finditer(r'function Trig_%s_Func\w+ takes'%re.escape(t),J): b+=J[m.start():J.index('endfunction',m.start())]
    return b
def closure(roots):
    seen=list(roots); i=0
    while i<len(seen):
        for x in re.findall(r'gg_trg_(\w+)',body(seen[i])):
            if x not in seen: seen.append(x)
        i+=1
    return seen
out=sys.argv[1]; os.makedirs(out,exist_ok=True)
for a in sys.argv[2:]:
    if a.startswith('T:'): roots=[a[2:]]; name=a[2:]; head=''
    else:
        roots=re.findall(r"SaveTriggerHandle\(udg_HashAttack,'%s',\d+,gg_trg_(\w+)\)"%a,J); name=a
        head=subprocess.run(['python3','Tools/skill_coverage/imm_dump.py','U:'+a],capture_output=True,text=True).stdout
        other=set()
        for m in re.finditer("'%s'"%a,J):
            k=J.rfind('function ',0,m.start()); fn=J[k+9:k+90].split(' takes')[0]
            other.add(re.sub(r'^Trig_|_Actions$|_Func\w+$|_Conditions$','',fn))
        head+='\n  이 uid를 언급하는 함수(트리거): %s\n'%sorted(other)
    ts=closure(roots)
    o=subprocess.run(['python3','Tools/skill_coverage/imm_dump.py']+ts,capture_output=True,text=True).stdout if ts else '(평타 등록 없음)\n'
    open('%s/%s.txt'%(out,name),'w').write(head+'  평타 등록: %s\n  폐쇄: %s\n'%(roots,ts)+'\n'.join(l[:1800] for l in o.split('\n')))
    print(name,roots,len(ts),'트리거')
