import re,collections
J=open('Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
# functions
FN={}
for m in re.finditer(r'function (\w+) takes (.*?) returns (\w+)(.*?)endfunction',J,re.S):
    FN[m.group(1)]=m.group(4)
# trigger -> actions / conditions
ACT=collections.defaultdict(list); COND=collections.defaultdict(list)
for m in re.finditer(r'TriggerAddAction\((gg_trg_\w+),function (\w+)\)',J): ACT[m.group(1)].append(m.group(2))
for m in re.finditer(r'TriggerAddCondition\((gg_trg_\w+),Condition\(function (\w+)\)\)',J): COND[m.group(1)].append(m.group(2))
HASH=collections.defaultdict(list)
for m in re.finditer(r"SaveTriggerHandle\(udg_HashAttack,'(\w{4})',(\d+),(gg_trg_\w+)\)",J): HASH[m.group(1)].append((int(m.group(2)),m.group(3)))
CALLRE=re.compile(r'\b(\w+)\(')
TRGRE=re.compile(r'(?:TriggerExecute|ConditionalTriggerExecute)\((gg_trg_\w+)\)')
def closure(start_fns=(),start_trgs=(),maxn=400):
    seenF=set(); seenT=set(); stack=[('T',t) for t in start_trgs]+[('F',f) for f in start_fns]
    while stack:
        k,x=stack.pop()
        if k=='T':
            if x in seenT: continue
            seenT.add(x)
            for f in ACT.get(x,[])+COND.get(x,[]): stack.append(('F',f))
        else:
            if x in seenF or x not in FN: continue
            seenF.add(x); b=FN[x]
            for t in TRGRE.findall(b): stack.append(('T',t))
            for c in set(CALLRE.findall(b)):
                if c in FN and c not in seenF: stack.append(('F',c))
            for c in re.findall(r'function (\w+)',b):
                if c in FN: stack.append(('F',c))
    return seenF,seenT
if __name__=='__main__':
    print(len(FN),'functions',len(ACT),'triggers',len(HASH),'hash units',sum(len(v) for v in HASH.values()))
    print([ (u,v) for u,v in HASH.items() if len(v)>1])
    # call freq of common helper funcs
    c=collections.Counter()
    for b in FN.values():
        for x in set(CALLRE.findall(b)):
            if x in FN: c[x]+=1
    print(c.most_common(40))
    print(FN.get('RRD'))
