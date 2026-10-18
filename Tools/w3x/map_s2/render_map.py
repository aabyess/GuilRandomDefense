import sys,os,re,struct,pickle,collections,json
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x')
import w3map
from PIL import Image,ImageDraw,ImageFont
BASE=os.path.expanduser('~/grd/GRD_s2_map/')
FRAME=(-13600,-13600,12100,11600)   # x0,y0,x1,y1 (WC3 단위)
PX=0.05   # px per WC3 unit → 25700*0.05=1285
W=int((FRAME[2]-FRAME[0])*PX);H=int((FRAME[3]-FRAME[1])*PX)
def P(x,y):return ((x-FRAME[0])*PX,(FRAME[3]-y)*PX)
FONT=None
for f in ['/System/Library/Fonts/AppleSDGothicNeo.ttc','/System/Library/Fonts/Supplemental/AppleGothic.ttf']:
    if os.path.exists(f):
        FONT=f;break
def font(sz):return ImageFont.truetype(FONT,sz) if FONT else ImageFont.load_default()
PAL=[(64,160,64),(150,110,60),(90,200,90),(200,170,90),(230,215,150),(120,120,140),(60,60,60),(210,90,60),(70,120,200),(200,80,200),(90,200,200),(240,240,120),(160,60,60),(100,160,100),(180,140,100),(110,90,70)]
def terrain(tag):
    e=w3map.parse_w3e(BASE+tag+'/war3map.w3e')
    w,h=e['w'],e['h']
    img=Image.new('RGB',(W,H),(20,20,40));dr=ImageDraw.Draw(img)
    pts=e['pts']
    def pt(ix,iy):return pts[iy*w+ix]
    stats=collections.Counter()
    for iy in range(h-1):
        for ix in range(w-1):
            hh,wf,tex,var,misc=pt(ix,iy)
            tid=tex&0x0F;flags=tex&0xF0
            layer=misc&0x0F
            gh=((hh-8192)/4.0)+(layer-2)*128
            x=e['ox']+ix*128;y=e['oy']+iy*128
            col=PAL[tid%len(PAL)]
            # 높이 음영
            k=max(0.55,min(1.25,1.0+gh/1500.0))
            col=tuple(int(max(0,min(255,c*k))) for c in col)
            gh_raw=hh+(layer-2)*512
            water=(flags&0x40)!=0 and gh_raw<=(wf&0x3FFF)+16
            if water:
                dep=((wf&0x3FFF)-gh_raw)/4.0
                a=max(0.25,min(1.0,dep/90.0));col=tuple(int(c*(1-a)+wc*a) for c,wc in zip(col,(30,70,170)))
            if flags&0x80 or (wf&0x4000):col=tuple(c//3 for c in col)
            x0,y0=P(x,y+128);x1,y1=P(x+128,y)
            dr.rectangle([x0,y0,x1,y1],fill=col)
            stats[(tid,water)]+=1
    return img,e,stats
def doodads(tag,img,names):
    ver,sub,ds,sp,left=w3map.parse_doo(BASE+tag+'/war3map.doo')
    dr=ImageDraw.Draw(img)
    cnt=collections.Counter()
    for d in ds:
        tid,var,x,y=d[0],d[1],d[2],d[3]
        nm=names.get(tid,tid)
        cnt[tid]+=1
        px,py=P(x,y)
        c=(255,255,255)
        if 'IOpr' in tid or tid.startswith('D'):c=(255,240,120)
        if any(k in nm for k in ('벽','Wall','wall','Gate')):c=(255,120,120)
        if any(k in nm for k in ('나무','Tree','Palm','야자')):c=(30,110,30)
        if any(k in nm for k in ('포탈','Portal','portal')):c=(200,80,255)
        dr.ellipse([px-1.5,py-1.5,px+1.5,py+1.5],fill=c)
    return ds,cnt
def rects(tag):
    t=open(BASE+tag+'/war3map.j',encoding='utf-8',errors='replace').read()
    out=[]
    for m in re.finditer(r'set ([A-Za-z_0-9]+)=Rect\((-?[\d.]+),(-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\)',t):
        out.append((m.group(1),float(m.group(2)),float(m.group(3)),float(m.group(4)),float(m.group(5))))
    return out
def draw_rects(img,rs,label_min=900,named=True):
    dr=ImageDraw.Draw(img);f=font(11)
    for i,(n,x0,y0,x1,y1) in enumerate(rs):
        a=P(x0,y1);b=P(x1,y0)
        big=(x1-x0)>=label_min or (y1-y0)>=label_min
        col=(255,60,60) if big else (255,255,0)
        dr.rectangle([a[0],a[1],b[0],b[1]],outline=col,width=1)
        if big:
            lab=n.replace('gg_rct_','') if named else '#%d'%i
            dr.text((a[0]+2,a[1]+1),lab,fill=(255,255,255),font=f)
if __name__=='__main__':
    tag=sys.argv[1]
    names={}
    import w3q
    sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x')
    for u in w3q.parse(BASE+tag+'/war3map.w3d'):
        nm=[v for f,l,v in u['mods'] if f in('bnam','dnam')];mdl=[v for f,l,v in u['mods'] if f=='dfil']
        names[u['id'] if u['id'].strip('\x00') else u['base']]=(nm[0].strip() if nm else '')+' '+(mdl[0] if mdl else '')
    img,e,st=terrain(tag)
    ds,cnt=doodads(tag,img,names)
    rs=rects(tag)
    draw_rects(img,rs,named=(tag=='old'))
    img.save(BASE+'%s_map.png'%tag)
    json.dump(dict(rects=rs,doodads=cnt.most_common(),ground=e['ground'],size=(e['w'],e['h']),off=(e['ox'],e['oy'])),open(BASE+'%s_info.json'%tag,'w'),ensure_ascii=False)
    print(tag,img.size,len(rs),len(ds))
