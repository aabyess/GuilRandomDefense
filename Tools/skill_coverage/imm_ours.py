import os,re,sys,glob,unicodedata
N=lambda s:unicodedata.normalize('NFC',s)
KIND=['Damage','Stun','ArmorBreak','ExtraProjectile','ArmorBonus','HealOverTime','ApplyBuff','RemoveBuff','AegrStack','AisrStack','A11SStack','AtkFlat','ASpd%','Slow','Atk%']
TT=['OnHitChance','CooldownAuto','Aura','OnHitCount']
G2F={}
for m in glob.glob('Assets/Data/UnitSkills/*.asset.meta'):
    G2F[re.search(r'guid: (\w+)',open(m).read()).group(1)]=m[:-5]
def show(path,ind='  '):
    t=open(path,encoding='utf-8').read()
    desc=re.search(r'\n  description: (.*?)\n  triggerType',t,re.S)
    tt=int(re.search(r'\n  triggerType: (\d+)',t).group(1))
    print(ind+'■',N(os.path.basename(path))[10:-6],'|',TT[tt])
    print(ind+'   desc:',(desc.group(1) if desc else '').replace('\n',' ')[:int(os.environ.get('DL','260'))])
    lv=t[t.find('\n  levels:'):]
    levels=re.split(r'\n  - cooldown:',lv)[1:]
    for li,L in enumerate(levels[:1]):
        L='cooldown:'+L
        head,_,eff=L.partition('effects:')
        hd=dict(re.findall(r'(\w+): ([^\n]*)',head))
        keep={k:v for k,v in hd.items() if v not in('0','','""',"''",'0.0') or k in('triggerChance',)}
        print(ind+'   lvl(%d개):'%len(levels),keep)
        for e in re.split(r'\n    - kind:',eff)[1:]:
            d=dict(re.findall(r'(\w+): ([^\n]*)',' kind:'+e))
            k=KIND[int(d['kind'])] if int(d['kind'])<len(KIND) else d['kind']
            DEF={'chance':'1','hitCount':'1','randMin':'1','randMax':'1'}
            rest={x:y for x,y in d.items() if x!='kind' and y not in('0','','""','0.0') and DEF.get(x)!=y and not (x in('randMin','randMax') and y=='1.0')}
            print(ind+'     -',k,rest)
for r in sys.argv[1:]:
    p='Assets/Data/Units/Roster/%s.asset'%r
    t=open(p,encoding='utf-8').read()
    i=t.find('\n  skills:'); j=t.find('\n  trait:',i)
    gs=re.findall(r'guid: (\w+)',t[i:j])
    one=re.search(r'^  skill: \{fileID: 11400000, guid: (\w+)',t,re.M)
    tr=re.search(r'^  trait: \{fileID: (\d+)(?:, guid: (\w+))?',t,re.M)
    print('==========',r,'skills',len(gs),'single',bool(one),'trait',tr.group(2) if tr else None, {k:v for k,v in re.findall(r'\n  (attackDamage|attackSpeed|attackRange|attackInterval|damage|mana\w*|hpRegen\w*|startMana\w*): ([^\n]*)',t)})
    for g in gs or ([one.group(1)] if one else []):
        if g in G2F: show(G2F[g])
        else: print('   (dangling guid)',g)
